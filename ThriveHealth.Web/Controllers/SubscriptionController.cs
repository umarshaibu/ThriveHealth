using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Tenancy;
using ThriveHealth.Web.Services.Tenancy;

namespace ThriveHealth.Web.Controllers;

/// <summary>
/// Tenant-side subscription &amp; billing console. The hospital owner sees their plan, can change
/// it, and can submit bank-transfer receipts for the super-admin to verify. Lives at
/// <c>{slug}.thrivehealth.ng/Subscription</c>.
/// </summary>
[Authorize(Roles = Roles.SystemAdministrator)]
public class SubscriptionController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ITenantContext _tenant;

    public SubscriptionController(ApplicationDbContext db, UserManager<ApplicationUser> users, ITenantContext tenant)
    {
        _db = db; _users = users; _tenant = tenant;
    }

    private async Task<Tenant?> CurrentTenantAsync() =>
        _tenant.CurrentId.HasValue
            ? await _db.Tenants.FirstOrDefaultAsync(t => t.Id == _tenant.CurrentId.Value)
            : null;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var tenant = await CurrentTenantAsync();
        if (tenant is null) return NotFound();

        var subscription = await _db.TenantSubscriptions.AsNoTracking()
            .Include(s => s.Plan)
            .Where(s => s.TenantId == tenant.Id && s.IsActive)
            .OrderByDescending(s => s.StartedAt).FirstOrDefaultAsync();
        var plans = await _db.Plans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync();
        var payments = await _db.TenantPayments.AsNoTracking()
            .Where(p => p.TenantId == tenant.Id)
            .OrderByDescending(p => p.SubmittedAt).Take(20).ToListAsync();

        ViewBag.Tenant = tenant;
        ViewBag.Subscription = subscription;
        ViewBag.Plans = plans;
        ViewBag.Payments = payments;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePlan(int planId, BillingCycle cycle)
    {
        var tenant = await CurrentTenantAsync();
        if (tenant is null) return NotFound();
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == planId && p.IsActive);
        if (plan is null) { TempData["Error"] = "That plan isn't available."; return RedirectToAction(nameof(Index)); }

        var current = await _db.TenantSubscriptions.FirstOrDefaultAsync(s => s.TenantId == tenant.Id && s.IsActive);
        if (current != null) { current.IsActive = false; current.CancelledAt = DateTime.UtcNow; }

        var price = cycle == BillingCycle.Annual ? plan.AnnualNgn : plan.MonthlyNgn;
        var nextEnd = cycle == BillingCycle.Annual ? DateTime.UtcNow.AddYears(1) : DateTime.UtcNow.AddMonths(1);
        _db.TenantSubscriptions.Add(new TenantSubscription
        {
            TenantId = tenant.Id, PlanId = plan.Id, Cycle = cycle,
            StartedAt = DateTime.UtcNow, CurrentPeriodStart = DateTime.UtcNow, CurrentPeriodEnd = nextEnd,
            PriceAmount = price, PriceCurrency = "NGN", IsActive = true
        });
        // The new plan needs payment before access flips Active. Tenant goes to PendingPayment until
        // a TenantPayment is approved by the super-admin (or paid online via Paystack).
        if (!tenant.IsTeachingHospital && plan.Code != "trial" && tenant.Status != TenantStatus.Active)
            tenant.Status = TenantStatus.PendingPayment;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Plan changed to {plan.Name}. Submit a payment to activate.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Domain([FromServices] ICustomDomainVerifier verifier)
    {
        var tenant = await CurrentTenantAsync();
        if (tenant is null) return NotFound();

        ViewBag.Tenant = tenant;
        ViewBag.TxtRecordHost = "_thrivehealth." + (tenant.CustomDomain ?? "yourdomain.com");
        ViewBag.CnameTarget = tenant.Slug + ".thrivehealth.ng";
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveDomain(string? customDomain, [FromServices] ICustomDomainVerifier verifier)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == _tenant.CurrentId);
        if (tenant is null) return NotFound();

        var input = customDomain?.Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(input))
        {
            TempData["Error"] = "Enter a domain (e.g. app.yourhospital.com).";
            return RedirectToAction(nameof(Domain));
        }
        if (!verifier.IsHostnameValid(input))
        {
            TempData["Error"] = "That doesn't look like a valid hostname. Use a domain you own — not a thrivehealth.ng subdomain.";
            return RedirectToAction(nameof(Domain));
        }
        // Don't let two tenants race for the same hostname.
        var clash = await _db.Tenants.AnyAsync(x => x.CustomDomain == input && x.Id != tenant.Id);
        if (clash)
        {
            TempData["Error"] = "That domain is already claimed by another workspace. If it's yours, contact support.";
            return RedirectToAction(nameof(Domain));
        }

        // Changing/setting the domain always invalidates any prior verification — the tenant must
        // republish the new token even if they kept the same hostname.
        tenant.CustomDomain = input;
        tenant.CustomDomainVerificationToken = verifier.GenerateToken();
        tenant.CustomDomainVerifiedAt = null;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Domain saved. Add the TXT record below at your DNS provider, then click Verify.";
        return RedirectToAction(nameof(Domain));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> VerifyDomain([FromServices] ICustomDomainVerifier verifier, CancellationToken ct)
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == _tenant.CurrentId);
        if (tenant is null) return NotFound();
        if (string.IsNullOrEmpty(tenant.CustomDomain) || string.IsNullOrEmpty(tenant.CustomDomainVerificationToken))
        {
            TempData["Error"] = "Save a domain first.";
            return RedirectToAction(nameof(Domain));
        }

        var result = await verifier.VerifyAsync(tenant.CustomDomain, tenant.CustomDomainVerificationToken, ct);
        if (!result.Verified)
        {
            // Surface what we actually saw so the tenant can compare — the most common cause of
            // verification failure is a partly-propagated DNS change or a TXT record on the wrong host.
            TempData["Error"] = "Verification failed: " + (result.FailureReason ?? "no matching TXT record found.")
                + (result.FoundRecords is { Count: > 0 } ? " Records seen: " + string.Join(", ", result.FoundRecords) : "");
            return RedirectToAction(nameof(Domain));
        }

        tenant.CustomDomainVerifiedAt = DateTime.UtcNow;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Verified! {tenant.CustomDomain} now points at your workspace. Once your DNS CNAME is also live, your team can sign in there.";
        return RedirectToAction(nameof(Domain));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveDomain()
    {
        var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == _tenant.CurrentId);
        if (tenant is null) return NotFound();

        tenant.CustomDomain = null;
        tenant.CustomDomainVerificationToken = null;
        tenant.CustomDomainVerifiedAt = null;
        tenant.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();

        TempData["Success"] = "Custom domain removed. Your workspace remains reachable at " + tenant.Slug + ".thrivehealth.ng.";
        return RedirectToAction(nameof(Domain));
    }

    public record SubmitTransferRequest(decimal Amount, string Reference, string BankAccountUsed, DateTime PaidAt, string? Notes);

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> SubmitBankTransfer(SubmitTransferRequest req, IFormFile? receipt, [FromServices] IWebHostEnvironment env)
    {
        var tenant = await CurrentTenantAsync();
        if (tenant is null) return NotFound();
        if (req.Amount <= 0) { TempData["Error"] = "Enter the amount you transferred."; return RedirectToAction(nameof(Index)); }
        if (string.IsNullOrWhiteSpace(req.Reference)) { TempData["Error"] = "Enter the bank transaction reference."; return RedirectToAction(nameof(Index)); }

        var subscription = await _db.TenantSubscriptions
            .Where(s => s.TenantId == tenant.Id && s.IsActive)
            .OrderByDescending(s => s.StartedAt).FirstOrDefaultAsync();

        string? receiptUrl = null, receiptName = null;
        if (receipt is not null && receipt.Length > 0)
        {
            if (receipt.Length > 5 * 1024 * 1024) { TempData["Error"] = "Receipt must be under 5 MB."; return RedirectToAction(nameof(Index)); }
            var allowed = new[] { "image/jpeg", "image/png", "image/webp", "application/pdf" };
            if (!allowed.Contains(receipt.ContentType, StringComparer.OrdinalIgnoreCase))
            { TempData["Error"] = "Receipt must be JPG, PNG, WEBP or PDF."; return RedirectToAction(nameof(Index)); }
            var subdir = Path.Combine("uploads", "tenant-payments", DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
            var dir = Path.Combine(env.WebRootPath, subdir);
            Directory.CreateDirectory(dir);
            var name = $"{Guid.NewGuid():N}{Path.GetExtension(receipt.FileName)}";
            await using (var stream = System.IO.File.Create(Path.Combine(dir, name)))
                await receipt.CopyToAsync(stream);
            receiptUrl = "/" + Path.Combine(subdir, name).Replace('\\', '/');
            receiptName = receipt.FileName;
        }

        _db.TenantPayments.Add(new TenantPayment
        {
            TenantId = tenant.Id,
            SubscriptionId = subscription?.Id,
            Method = PaymentMethodKind.BankTransfer,
            Status = PaymentReceiptStatus.Pending,
            Amount = req.Amount,
            Currency = subscription?.PriceCurrency ?? "NGN",
            Reference = req.Reference,
            BankAccountUsed = req.BankAccountUsed,
            // Form date input arrives as Local/Unspecified DateTime; Postgres timestamptz needs UTC.
            PaidAt = req.PaidAt == default ? null : (DateTime?)DateTime.SpecifyKind(req.PaidAt, DateTimeKind.Utc),
            ReceiptUrl = receiptUrl,
            ReceiptFileName = receiptName,
            Notes = req.Notes,
            SubmittedAt = DateTime.UtcNow
        });

        if (tenant.Status is TenantStatus.Trialing or TenantStatus.PastDue)
            tenant.Status = TenantStatus.PendingPayment;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Receipt submitted. Our team will verify within one working day and your account will be activated.";
        return RedirectToAction(nameof(Index));
    }
}
