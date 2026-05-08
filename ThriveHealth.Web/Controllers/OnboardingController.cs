using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Tenancy;
using ThriveHealth.Web.Models.ViewModels.Tenancy;
using ThriveHealth.Web.Services.Integrations;
using ThriveHealth.Web.Services.Tenancy;

namespace ThriveHealth.Web.Controllers;

/// <summary>
/// Marketing landing + self-onboarding for new hospital tenants. Lives at the apex / unrecognised
/// subdomain — does NOT require a tenant to be resolved.
/// </summary>
[AllowAnonymous]
public class OnboardingController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly ITenantProvisioningService _provisioning;
    private readonly IEmailGateway _email;
    private readonly ITenantContext _tenant;

    public OnboardingController(ApplicationDbContext db, ITenantProvisioningService provisioning, IEmailGateway email, ITenantContext tenant)
    {
        _db = db; _provisioning = provisioning; _email = email; _tenant = tenant;
    }

    [HttpGet("/")]
    [HttpGet("/home")]
    public async Task<IActionResult> Index()
    {
        // Under a tenant subdomain we never show the marketing page — staff go straight to login,
        // already-signed-in users to their dashboard.
        if (_tenant.IsResolved)
        {
            if (User.Identity?.IsAuthenticated == true) return RedirectToAction("Index", "Dashboard");
            return RedirectToAction("Login", "Account");
        }
        ViewBag.Plans = await _db.Plans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync();
        return View();
    }

    [HttpGet("/pricing")]
    public async Task<IActionResult> Pricing()
    {
        ViewBag.Plans = await _db.Plans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync();
        return View();
    }

    [HttpGet("/signup")]
    public async Task<IActionResult> SignUp(string? plan)
    {
        var plans = await _db.Plans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync();
        ViewBag.Plans = plans;
        return View(new TenantSignUpViewModel { PlanCode = plan ?? "trial" });
    }

    /// <summary>JSON endpoint the signup form polls to validate slug availability while the user types.</summary>
    [HttpGet("/signup/check-slug")]
    public async Task<IActionResult> CheckSlug(string slug)
    {
        var available = await _provisioning.IsSlugAvailableAsync(slug ?? string.Empty);
        return Json(new { ok = available, slug });
    }

    [HttpPost("/signup"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SignUp(TenantSignUpViewModel m)
    {
        if (!ModelState.IsValid)
        {
            ViewBag.Plans = await _db.Plans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync();
            return View(m);
        }

        var req = new ProvisionTenantRequest(
            Slug: m.Slug,
            LegalName: m.LegalName,
            BrandName: m.BrandName,
            OwnerEmail: m.OwnerEmail,
            OwnerName: m.OwnerName,
            OwnerPhone: m.OwnerPhone,
            CountryCode: m.CountryCode ?? "NG",
            CurrencyCode: m.CurrencyCode ?? "NGN",
            State: m.State, Lga: m.Lga, Address: m.Address,
            BedCapacity: m.BedCapacity,
            PlanCode: m.PlanCode,
            BillingCycle: m.AnnualBilling ? BillingCycle.Annual : BillingCycle.Monthly,
            IsTeachingHospital: m.IsTeachingHospital);

        var result = await _provisioning.ProvisionAsync(req);
        if (!result.Ok)
        {
            ModelState.AddModelError(string.Empty, result.Error ?? "Could not create your hospital. Please try again.");
            ViewBag.Plans = await _db.Plans.AsNoTracking().Where(p => p.IsActive).OrderBy(p => p.SortOrder).ToListAsync();
            return View(m);
        }

        // Best-effort: send a welcome email with login link + temporary password.
        try
        {
            var loginUrl = Url.Action("Login", "Account", new { area = "" }, Request.Scheme,
                $"{m.Slug}.{Request.Host.Host.Replace("www.", "")}");
            await _email.EnqueueAsync(new EmailSendRequest(
                FacilityId: 0,
                ToEmail: result.OwnerLoginEmail!,
                ToName: m.OwnerName,
                Subject: "Welcome to ThriveHealth — your hospital is ready",
                BodyHtml: $"<p>Hi {m.OwnerName},</p>" +
                          $"<p>Your hospital workspace <strong>{m.LegalName}</strong> has been created and your 30-day free trial has begun.</p>" +
                          $"<p><strong>Sign in:</strong> <a href='{loginUrl}'>{loginUrl}</a><br/>" +
                          $"<strong>Email:</strong> {result.OwnerLoginEmail}<br/>" +
                          $"<strong>Temporary password:</strong> <code>{result.OwnerTempPassword}</code></p>" +
                          $"<p>Please sign in and change your password right away. Need help? Reply to this email.</p>",
                Purpose: ThriveHealth.Web.Models.Integrations.MessagePurpose.AdHoc));
        }
        catch { /* email failures shouldn't block signup */ }

        return RedirectToAction(nameof(Welcome), new { slug = m.Slug });
    }

    [HttpGet("/signup/welcome")]
    public async Task<IActionResult> Welcome(string slug)
    {
        var tenant = await _db.Tenants.AsNoTracking().FirstOrDefaultAsync(t => t.Slug == slug);
        if (tenant is null) return RedirectToAction(nameof(Index));
        return View(tenant);
    }
}
