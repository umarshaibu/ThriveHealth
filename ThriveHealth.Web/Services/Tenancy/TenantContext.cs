using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Tenancy;

namespace ThriveHealth.Web.Services.Tenancy;

/// <summary>
/// Per-request resolved tenant. Set by <see cref="TenantResolverMiddleware"/> before MVC runs;
/// every request inside a tenant's subdomain has CurrentId / CurrentSlug populated.
/// </summary>
public interface ITenantContext
{
    /// <summary>Tenant id, or null when on a non-tenant URL (admin / marketing / sign-up).</summary>
    int? CurrentId { get; }
    string? CurrentSlug { get; }
    Tenant? Current { get; }

    bool IsResolved { get; }
    bool IsAdminContext { get; }   // request is for admin.thrivehealth.ng
    bool IsMarketingContext { get; } // request is for the bare apex / signup flow

    void Set(Tenant tenant);
    void SetAdmin();
    void SetMarketing();
}

public sealed class TenantContext : ITenantContext
{
    public int? CurrentId { get; private set; }
    public string? CurrentSlug { get; private set; }
    public Tenant? Current { get; private set; }

    public bool IsResolved { get; private set; }
    public bool IsAdminContext { get; private set; }
    public bool IsMarketingContext { get; private set; }

    public void Set(Tenant tenant)
    {
        Current = tenant; CurrentId = tenant.Id; CurrentSlug = tenant.Slug;
        IsResolved = true; IsAdminContext = false; IsMarketingContext = false;
    }
    public void SetAdmin() { IsAdminContext = true; IsResolved = false; IsMarketingContext = false; }
    public void SetMarketing() { IsMarketingContext = true; IsResolved = false; IsAdminContext = false; }
}

/// <summary>
/// Resolves the tenant for the current request from the host name.
///   • <c>admin.thrivehealth.ng</c>  → super-admin context (no tenant scoping).
///   • <c>{slug}.thrivehealth.ng</c> → tenant <em>slug</em>.
///   • <c>thrivehealth.ng</c> apex / localhost / unknown subdomain → marketing /
///     onboarding context (sign-up, public pricing pages — no tenant scoping).
///
/// In dev (localhost) we accept a <c>?tenant=slug</c> override on every request, plus
/// fall back to the cookie <c>th_tenant</c> if previously set, so a single dev box can
/// serve any tenant without DNS gymnastics.
/// </summary>
public sealed class TenantResolverMiddleware
{
    public const string AdminHostPrefix = "admin.";
    public const string DevTenantQueryParam = "tenant";
    public const string DevTenantCookie = "th_tenant";

    private static readonly HashSet<string> ApexHosts = new(StringComparer.OrdinalIgnoreCase)
    { "thrivehealth.ng", "www.thrivehealth.ng", "thrivehealth.local", "localhost" };

    private readonly RequestDelegate _next;

    public TenantResolverMiddleware(RequestDelegate next) { _next = next; }

    public async Task InvokeAsync(HttpContext ctx, ITenantContext tenantCtx, ApplicationDbContext db)
    {
        var host = ctx.Request.Host.Host?.Trim().ToLowerInvariant() ?? "";

        if (host.StartsWith(AdminHostPrefix))
        {
            tenantCtx.SetAdmin();
            await _next(ctx); return;
        }

        // Apex / dev — first try the dev override, then the cookie, otherwise marketing context.
        if (ApexHosts.Contains(host) || host.EndsWith(".localhost"))
        {
            var devSlug = ctx.Request.Query[DevTenantQueryParam].ToString();
            if (string.IsNullOrEmpty(devSlug)) devSlug = ctx.Request.Cookies[DevTenantCookie] ?? string.Empty;

            if (!string.IsNullOrEmpty(devSlug))
            {
                var t = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == devSlug);
                if (t is not null)
                {
                    tenantCtx.Set(t);
                    // Persist the dev override so subsequent requests don't need the query param
                    ctx.Response.Cookies.Append(DevTenantCookie, devSlug, new CookieOptions
                    {
                        IsEssential = true, SameSite = SameSiteMode.Lax, HttpOnly = true,
                        Expires = DateTimeOffset.UtcNow.AddDays(7)
                    });
                    await _next(ctx); return;
                }
            }
            tenantCtx.SetMarketing();
            await _next(ctx); return;
        }

        // Production: try a verified custom domain first. We only honour the row when
        // CustomDomainVerifiedAt is set — an unverified row could be claim-jacked by a tenant
        // who saved a domain they don't actually control, then waited for traffic.
        var byCustom = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(
            x => x.CustomDomain == host && x.CustomDomainVerifiedAt != null);
        if (byCustom is not null) { tenantCtx.Set(byCustom); await _next(ctx); return; }

        // Fall back to subdomain-based resolution against thrivehealth.ng.
        var slug = host.Split('.')[0];
        var tenant = await db.Tenants.AsNoTracking().FirstOrDefaultAsync(x => x.Slug == slug);
        if (tenant is null)
        {
            // Unknown host — treat as marketing so onboarding still works.
            tenantCtx.SetMarketing();
        }
        else
        {
            tenantCtx.Set(tenant);
        }
        await _next(ctx);
    }
}
