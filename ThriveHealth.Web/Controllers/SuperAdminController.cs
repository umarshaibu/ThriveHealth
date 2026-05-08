using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Tenancy;

namespace ThriveHealth.Web.Controllers;

/// <summary>
/// Platform-level operations console — visible only to ThriveHealth staff with the
/// <see cref="Roles.SuperAdmin"/> role. Mounted at <c>/superadmin</c> on the admin host
/// (<c>admin.thrivehealth.ng</c>) where the tenant resolver sets an admin context, so
/// queries here intentionally bypass tenant scoping.
/// </summary>
[Authorize(Roles = Roles.SuperAdmin), Route("superadmin")]
public class SuperAdminController : Controller
{
    private readonly ApplicationDbContext _db;
    public SuperAdminController(ApplicationDbContext db) { _db = db; }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        // Tenant scoping is bypassed via IgnoreQueryFilters so the dashboard reads every
        // tenant's data regardless of which host the super-admin happens to hit (admin
        // subdomain in prod, or a tenant subdomain via the dev-override cookie).
        var tenants = await _db.Tenants.AsNoTracking().IgnoreQueryFilters().ToListAsync();
        var pendingPayments = await _db.TenantPayments.AsNoTracking().IgnoreQueryFilters()
            .Where(p => p.Status == PaymentReceiptStatus.Pending).CountAsync();
        var activeSubs = await _db.TenantSubscriptions.AsNoTracking().IgnoreQueryFilters()
            .Include(s => s.Plan).Where(s => s.IsActive).ToListAsync();

        ViewBag.TotalTenants = tenants.Count;
        ViewBag.Trialing = tenants.Count(t => t.Status == TenantStatus.Trialing);
        ViewBag.Active = tenants.Count(t => t.Status == TenantStatus.Active);
        ViewBag.PastDue = tenants.Count(t => t.Status == TenantStatus.PastDue);
        ViewBag.PendingPayments = pendingPayments;
        ViewBag.MrrNgn = activeSubs
            .Where(s => s.Cycle == BillingCycle.Monthly)
            .Sum(s => s.PriceAmount)
            + activeSubs.Where(s => s.Cycle == BillingCycle.Annual).Sum(s => s.PriceAmount / 12);

        ViewBag.RecentTenants = tenants.OrderByDescending(t => t.CreatedAt).Take(10).ToList();
        return View();
    }
}
