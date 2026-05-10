using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Tenancy;

namespace ThriveHealth.Web.Controllers;

/// <summary>
/// Manage the platform's subscription plan catalogue (Trial / Basic / Standard / Premium /
/// Enterprise). Edits propagate to new signups immediately; existing subscriptions keep the
/// price snapshot they were created with — see <see cref="TenantSubscription.PriceAmount"/>.
/// </summary>
[Authorize(Roles = Roles.SuperAdmin), Route("superadmin/plans")]
public class SuperAdminPlansController : Controller
{
    private readonly ApplicationDbContext _db;
    public SuperAdminPlansController(ApplicationDbContext db) { _db = db; }

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        var plans = await _db.Plans.AsNoTracking().OrderBy(p => p.SortOrder).ThenBy(p => p.Id).ToListAsync();
        // Active subscription count per plan — useful for impact assessment when editing prices.
        var subCounts = await _db.TenantSubscriptions.IgnoreQueryFilters()
            .Where(s => s.IsActive)
            .GroupBy(s => s.PlanId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);
        ViewBag.SubCounts = subCounts;
        return View(plans);
    }

    [HttpGet("new")]
    public IActionResult Create() => View("Edit", new Plan { IsActive = true });

    [HttpGet("{id:int}/edit")]
    public async Task<IActionResult> Edit(int id)
    {
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == id);
        if (plan is null) return NotFound();
        return View(plan);
    }

    [HttpPost(""), ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(Plan model)
    {
        if (!ModelState.IsValid) return View("Edit", model);

        // Code is the lookup key — keep it lowercase + URL-safe.
        model.Code = (model.Code ?? string.Empty).Trim().ToLowerInvariant();
        if (string.IsNullOrEmpty(model.Code) || string.IsNullOrEmpty(model.Name))
        {
            TempData["Error"] = "Code and Name are required.";
            return View("Edit", model);
        }

        if (model.Id == 0)
        {
            // Code uniqueness — DB has a unique index but we surface the error early.
            if (await _db.Plans.AnyAsync(p => p.Code == model.Code))
            {
                ModelState.AddModelError(nameof(Plan.Code), "A plan with this code already exists.");
                return View("Edit", model);
            }
            model.CreatedAt = DateTime.UtcNow;
            _db.Plans.Add(model);
        }
        else
        {
            var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == model.Id);
            if (plan is null) return NotFound();
            // Don't let edits change the code if any subscription references it — too risky.
            var hasSubs = await _db.TenantSubscriptions.IgnoreQueryFilters().AnyAsync(s => s.PlanId == plan.Id);
            if (hasSubs && plan.Code != model.Code)
            {
                ModelState.AddModelError(nameof(Plan.Code), $"Cannot change code while {plan.Code} has active subscriptions.");
                return View("Edit", model);
            }

            plan.Code = model.Code;
            plan.Name = model.Name;
            plan.Tagline = model.Tagline;
            plan.SortOrder = model.SortOrder;
            plan.MonthlyNgn = model.MonthlyNgn;
            plan.AnnualNgn = model.AnnualNgn;
            plan.MaxStaff = model.MaxStaff;
            plan.MaxPatientsPerMonth = model.MaxPatientsPerMonth;
            plan.MaxFacilities = model.MaxFacilities;
            plan.MaxTeleConsultsPerMonth = model.MaxTeleConsultsPerMonth;
            plan.TelemedicineEnabled = model.TelemedicineEnabled;
            plan.ChatPackagesEnabled = model.ChatPackagesEnabled;
            plan.AiEnabled = model.AiEnabled;
            plan.MultiFacilityEnabled = model.MultiFacilityEnabled;
            plan.ClaimsEnabled = model.ClaimsEnabled;
            plan.AnalyticsEnabled = model.AnalyticsEnabled;
            plan.PrioritySupport = model.PrioritySupport;
            plan.SsoEnabled = model.SsoEnabled;
            plan.OnPremiseAvailable = model.OnPremiseAvailable;
            plan.IsActive = model.IsActive;
            plan.IsCustomQuote = model.IsCustomQuote;
        }

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Plan '{model.Name}' saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("{id:int}/toggle"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(int id)
    {
        var plan = await _db.Plans.FirstOrDefaultAsync(p => p.Id == id);
        if (plan is null) return NotFound();
        plan.IsActive = !plan.IsActive;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Plan '{plan.Name}' {(plan.IsActive ? "enabled" : "hidden from signup")}.";
        return RedirectToAction(nameof(Index));
    }
}
