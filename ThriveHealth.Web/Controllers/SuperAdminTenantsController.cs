using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Tenancy;

namespace ThriveHealth.Web.Controllers;

/// <summary>
/// Tenant directory + per-tenant operational actions (suspend, reactivate, extend trial,
/// flag teaching hospital). Mounted at <c>/superadmin/tenants</c>.
/// </summary>
[Authorize(Roles = Roles.SuperAdmin), Route("superadmin/tenants")]
public class SuperAdminTenantsController : Controller
{
    private readonly ApplicationDbContext _db;
    public SuperAdminTenantsController(ApplicationDbContext db) { _db = db; }

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, TenantStatus? status)
    {
        var query = _db.Tenants.AsNoTracking().IgnoreQueryFilters().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var lower = q.Trim().ToLower();
            query = query.Where(t => t.Slug.ToLower().Contains(lower)
                || t.LegalName.ToLower().Contains(lower)
                || t.OwnerEmail.ToLower().Contains(lower));
        }
        if (status.HasValue) query = query.Where(t => t.Status == status.Value);
        var rows = await query.OrderByDescending(t => t.CreatedAt).Take(200).ToListAsync();
        ViewBag.Q = q; ViewBag.Status = status;
        return View(rows);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> Detail(int id)
    {
        var tenant = await _db.Tenants.AsNoTracking().IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Id == id);
        if (tenant is null) return NotFound();
        ViewBag.Subscription = await _db.TenantSubscriptions.AsNoTracking().IgnoreQueryFilters().Include(s => s.Plan)
            .Where(s => s.TenantId == id).OrderByDescending(s => s.StartedAt).FirstOrDefaultAsync();
        ViewBag.Payments = await _db.TenantPayments.AsNoTracking().IgnoreQueryFilters()
            .Where(p => p.TenantId == id).OrderByDescending(p => p.SubmittedAt).Take(20).ToListAsync();
        ViewBag.Facilities = await _db.Facilities.AsNoTracking().IgnoreQueryFilters()
            .Where(f => f.TenantId == id).ToListAsync();
        ViewBag.UserCount = await _db.Users.IgnoreQueryFilters().CountAsync(u => u.TenantId == id);
        return View(tenant);
    }

    [HttpPost("suspend"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Suspend(int id, string? reason)
    {
        var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        t.Status = TenantStatus.Suspended;
        t.SuspendedAt = DateTime.UtcNow;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{t.LegalName} suspended.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("reactivate"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Reactivate(int id)
    {
        var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        t.Status = TenantStatus.Active;
        t.SuspendedAt = null;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"{t.LegalName} reactivated.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("extend-trial"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ExtendTrial(int id, int days)
    {
        var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        t.TrialEndsAt = (t.TrialEndsAt ?? DateTime.UtcNow).AddDays(days);
        if (t.Status != TenantStatus.Trialing) t.Status = TenantStatus.Trialing;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Trial extended by {days} days.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("teaching"), ValidateAntiForgeryToken]
    public async Task<IActionResult> SetTeachingHospital(int id, bool verified)
    {
        var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        t.IsTeachingHospital = verified;
        t.TeachingVerifiedAt = verified ? DateTime.UtcNow : null;
        if (verified && t.Status != TenantStatus.Active) t.Status = TenantStatus.Active;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = verified ? "Marked as verified teaching hospital — platform access is free." : "Teaching-hospital flag cleared.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("clear-domain"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ClearCustomDomain(int id)
    {
        var t = await _db.Tenants.FirstOrDefaultAsync(x => x.Id == id);
        if (t is null) return NotFound();
        t.CustomDomain = null;
        t.CustomDomainVerificationToken = null;
        t.CustomDomainVerifiedAt = null;
        t.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Custom domain cleared. Tenant can set a fresh one from their billing console.";
        return RedirectToAction(nameof(Detail), new { id });
    }
}
