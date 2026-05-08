using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Tenancy;

namespace ThriveHealth.Web.Services.Tenancy;

public record ProvisionTenantRequest(
    string Slug,
    string LegalName,
    string? BrandName,
    string OwnerEmail,
    string OwnerName,
    string? OwnerPhone,
    string CountryCode,
    string CurrencyCode,
    string? State,
    string? Lga,
    string? Address,
    int? BedCapacity,
    string? PlanCode,
    BillingCycle BillingCycle,
    bool IsTeachingHospital);

public record ProvisionTenantResult(
    bool Ok,
    int? TenantId,
    string? OwnerLoginEmail,
    string? OwnerTempPassword,
    string? Error);

public interface ITenantProvisioningService
{
    Task<bool> IsSlugAvailableAsync(string slug, CancellationToken ct = default);
    Task<ProvisionTenantResult> ProvisionAsync(ProvisionTenantRequest req, CancellationToken ct = default);
}

/// <summary>
/// Creates a brand-new tenant: tenant row, default facility, owner user with SystemAdministrator
/// role, default subscription (Trial), and minimal seed data so the new hospital can use the
/// system from day 1. Idempotent on slug — returns an error if the slug is taken.
/// </summary>
public class TenantProvisioningService : ITenantProvisioningService
{
    private static readonly HashSet<string> ReservedSlugs = new(StringComparer.OrdinalIgnoreCase)
    {
        "admin", "api", "app", "www", "mail", "ftp", "dev", "staging", "test", "support",
        "billing", "help", "docs", "blog", "status", "thrivehealth", "default"
    };

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly ILogger<TenantProvisioningService> _log;

    public TenantProvisioningService(ApplicationDbContext db, UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles, ILogger<TenantProvisioningService> log)
    {
        _db = db; _users = users; _roles = roles; _log = log;
    }

    public async Task<bool> IsSlugAvailableAsync(string slug, CancellationToken ct = default)
    {
        slug = NormalizeSlug(slug);
        if (string.IsNullOrEmpty(slug)) return false;
        if (ReservedSlugs.Contains(slug)) return false;
        if (slug.Length is < 3 or > 40) return false;
        if (!System.Text.RegularExpressions.Regex.IsMatch(slug, "^[a-z0-9-]+$")) return false;
        return !await _db.Tenants.AnyAsync(t => t.Slug == slug, ct);
    }

    public async Task<ProvisionTenantResult> ProvisionAsync(ProvisionTenantRequest req, CancellationToken ct = default)
    {
        var slug = NormalizeSlug(req.Slug);
        if (!await IsSlugAvailableAsync(slug, ct))
            return new ProvisionTenantResult(false, null, null, null, "That subdomain is taken or not allowed.");

        // Choose the requested plan (default to Trial if omitted/invalid).
        var planCode = string.IsNullOrEmpty(req.PlanCode) ? "trial" : req.PlanCode!.ToLowerInvariant();
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Code == planCode && p.IsActive, ct)
                 ?? await _db.Plans.FirstAsync(p => p.Code == "trial", ct);

        // 1. Tenant row — Trialing for the first 30 days regardless of plan picked
        var now = DateTime.UtcNow;
        var tenant = new Tenant
        {
            Slug = slug,
            LegalName = req.LegalName.Trim(),
            BrandName = req.BrandName?.Trim(),
            OwnerEmail = req.OwnerEmail.Trim().ToLowerInvariant(),
            OwnerName = req.OwnerName.Trim(),
            OwnerPhone = req.OwnerPhone,
            CountryCode = string.IsNullOrEmpty(req.CountryCode) ? "NG" : req.CountryCode,
            CurrencyCode = string.IsNullOrEmpty(req.CurrencyCode) ? "NGN" : req.CurrencyCode,
            State = req.State, Lga = req.Lga, Address = req.Address,
            Status = TenantStatus.Trialing,
            EmailVerifiedAt = now, // for now we'll trust signup; email-verification flow can be added later
            TrialEndsAt = now.AddDays(30),
            IsTeachingHospital = req.IsTeachingHospital,
            CreatedAt = now, UpdatedAt = now
        };
        _db.Tenants.Add(tenant);
        await _db.SaveChangesAsync(ct);

        // 2. Default facility for this tenant — clone the tenant's location into a sensible default
        var facility = new Facility
        {
            TenantId = tenant.Id,
            Name = tenant.BrandName ?? tenant.LegalName,
            Code = (slug.Length > 8 ? slug[..8] : slug).ToUpperInvariant(),
            Type = req.IsTeachingHospital ? FacilityType.TeachingHospital : FacilityType.GeneralHospital,
            Tier = FacilityTier.Secondary,
            State = tenant.State, Lga = tenant.Lga, Address = tenant.Address,
            Phone = tenant.OwnerPhone, Email = tenant.OwnerEmail,
            BedCapacity = req.BedCapacity ?? 0,
            HospitalNumberPrefix = (slug.Length > 4 ? slug[..4] : slug).ToUpperInvariant(),
            CreatedAt = now, IsActive = true
        };
        _db.Facilities.Add(facility);
        await _db.SaveChangesAsync(ct);

        // 3. Owner user — gets SystemAdministrator role for this tenant
        var ownerUser = new ApplicationUser
        {
            UserName = tenant.OwnerEmail,
            Email = tenant.OwnerEmail,
            EmailConfirmed = true,
            FirstName = ParseFirst(req.OwnerName),
            LastName = ParseLast(req.OwnerName),
            FacilityId = facility.Id,
            TenantId = tenant.Id,
            CreatedAt = now,
            IsActive = true
        };
        var tempPassword = GenerateTempPassword();
        var createResult = await _users.CreateAsync(ownerUser, tempPassword);
        if (!createResult.Succeeded)
        {
            _log.LogError("Tenant {Slug} owner-user creation failed: {Errors}",
                slug, string.Join("; ", createResult.Errors.Select(e => e.Description)));
            // Roll back tenant + facility so the slug is freed
            _db.Facilities.Remove(facility);
            _db.Tenants.Remove(tenant);
            await _db.SaveChangesAsync(ct);
            return new ProvisionTenantResult(false, null, null, null,
                createResult.Errors.FirstOrDefault()?.Description ?? "Could not create owner user.");
        }

        // Make sure the SystemAdministrator role exists, then assign it
        if (!await _roles.RoleExistsAsync(Roles.SystemAdministrator))
            await _roles.CreateAsync(new IdentityRole(Roles.SystemAdministrator));
        await _users.AddToRoleAsync(ownerUser, Roles.SystemAdministrator);

        // 4. Default subscription — Trialing
        var subscription = new TenantSubscription
        {
            TenantId = tenant.Id,
            PlanId = plan.Id,
            Cycle = req.BillingCycle,
            StartedAt = now,
            CurrentPeriodStart = now,
            CurrentPeriodEnd = now.AddDays(30), // first period = 30-day trial
            PriceAmount = req.BillingCycle == BillingCycle.Annual ? plan.AnnualNgn : plan.MonthlyNgn,
            PriceCurrency = "NGN",
            IsActive = true
        };
        _db.TenantSubscriptions.Add(subscription);
        await _db.SaveChangesAsync(ct);

        _log.LogInformation("Provisioned tenant {Slug} (id {Id}) on plan {Plan}", slug, tenant.Id, plan.Code);
        return new ProvisionTenantResult(true, tenant.Id, ownerUser.Email, tempPassword, null);
    }

    private static string NormalizeSlug(string slug) => (slug ?? string.Empty).Trim().ToLowerInvariant();
    private static string ParseFirst(string fullName) => fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries).FirstOrDefault() ?? "Owner";
    private static string ParseLast(string fullName)
    {
        var parts = fullName.Split(' ', StringSplitOptions.RemoveEmptyEntries);
        return parts.Length > 1 ? parts[^1] : "—";
    }

    private static string GenerateTempPassword()
    {
        // 12 chars, mixed case + digits + symbol — meets default Identity password requirements
        var rnd = Random.Shared;
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghjkmnpqrstuvwxyz";
        const string digit = "23456789";
        const string sym = "!@#$%&*?";
        Span<char> buf = stackalloc char[12];
        buf[0] = upper[rnd.Next(upper.Length)];
        buf[1] = lower[rnd.Next(lower.Length)];
        buf[2] = digit[rnd.Next(digit.Length)];
        buf[3] = sym[rnd.Next(sym.Length)];
        var pool = upper + lower + digit + sym;
        for (var i = 4; i < buf.Length; i++) buf[i] = pool[rnd.Next(pool.Length)];
        // Shuffle to avoid predictable positions
        for (var i = buf.Length - 1; i > 0; i--) { var j = rnd.Next(i + 1); (buf[i], buf[j]) = (buf[j], buf[i]); }
        return new string(buf);
    }
}
