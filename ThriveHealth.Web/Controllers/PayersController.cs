using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Insurance;
using ThriveHealth.Web.Models.ViewModels;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.PayersManage)]
public class PayersController : Controller
{
    public const string CanManage = Roles.SystemAdministrator + "," + Roles.MedicalDirector + "," +
        Roles.ChiefFinancialOfficer + "," + Roles.Accountant + "," + Roles.ClaimsOfficer;

    private readonly ApplicationDbContext _db;
    public PayersController(ApplicationDbContext db) => _db = db;

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var payers = await _db.Payers.AsNoTracking()
            .Include(p => p.Plans)
            .OrderBy(p => p.OrgType).ThenBy(p => p.Name)
            .ToListAsync();
        return View(payers);
    }

    [HttpGet]
    public IActionResult Create() => View(new PayerEditViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PayerEditViewModel m)
    {
        if (!ModelState.IsValid) return View(m);
        _db.Payers.Add(new Payer
        {
            Name = m.Name, Code = m.Code.ToUpperInvariant(), OrgType = m.OrgType,
            Address = m.Address, Phone = m.Phone, Email = m.Email,
            ClaimsDispatchEmail = m.ClaimsDispatchEmail,
            RegulatorRegistrationNumber = m.RegulatorRegistrationNumber,
            IsActive = m.IsActive
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Payer '{m.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var p = await _db.Payers.FindAsync(id);
        if (p is null) return NotFound();
        return View(new PayerEditViewModel
        {
            Id = p.Id, Name = p.Name, Code = p.Code, OrgType = p.OrgType,
            Address = p.Address, Phone = p.Phone, Email = p.Email,
            ClaimsDispatchEmail = p.ClaimsDispatchEmail,
            RegulatorRegistrationNumber = p.RegulatorRegistrationNumber,
            IsActive = p.IsActive
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(PayerEditViewModel m)
    {
        var p = await _db.Payers.FindAsync(m.Id);
        if (p is null) return NotFound();
        if (!ModelState.IsValid) return View(m);
        p.Name = m.Name; p.Code = m.Code.ToUpperInvariant(); p.OrgType = m.OrgType;
        p.Address = m.Address; p.Phone = m.Phone; p.Email = m.Email;
        p.ClaimsDispatchEmail = m.ClaimsDispatchEmail;
        p.RegulatorRegistrationNumber = m.RegulatorRegistrationNumber;
        p.IsActive = m.IsActive;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Payer updated.";
        return RedirectToAction(nameof(Details), new { id = m.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var p = await _db.Payers.AsNoTracking()
            .Include(x => x.Plans).ThenInclude(pl => pl.Formulary)
            .FirstOrDefaultAsync(x => x.Id == id);
        if (p is null) return NotFound();
        return View(p);
    }

    [HttpGet]
    public async Task<IActionResult> AddPlan(int payerId)
    {
        var payer = await _db.Payers.FindAsync(payerId);
        if (payer is null) return NotFound();
        ViewBag.Payer = payer;
        return View(new PayerPlanEditViewModel { PayerId = payerId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPlan(PayerPlanEditViewModel m)
    {
        var payer = await _db.Payers.FindAsync(m.PayerId);
        if (payer is null) return NotFound();
        if (!ModelState.IsValid) { ViewBag.Payer = payer; return View(m); }

        _db.PayerPlans.Add(new PayerPlan
        {
            PayerId = m.PayerId,
            Name = m.Name, Code = m.Code.ToUpperInvariant(),
            TariffMultiplier = m.TariffMultiplier,
            DefaultCopayPercent = m.DefaultCopayPercent,
            CapitationRatePerEnrolleeMonth = m.CapitationRatePerEnrolleeMonth,
            RequiresPreAuthorization = m.RequiresPreAuthorization,
            DefaultFormularyCovered = m.DefaultFormularyCovered,
            IsActive = m.IsActive
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Plan added.";
        return RedirectToAction(nameof(Details), new { id = m.PayerId });
    }

    [HttpGet]
    public async Task<IActionResult> Search(string? q, int? type)
    {
        var query = _db.Payers.AsNoTracking().Where(p => p.IsActive);
        if (type.HasValue) query = query.Where(p => p.OrgType == (PayerOrgType)type.Value);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.Name, like) ||
                EF.Functions.ILike(p.Code, like));
        }
        var rows = await query
            .OrderBy(p => p.Name)
            .Select(p => new
            {
                id = p.Id,
                name = p.Name,
                code = p.Code,
                type = p.OrgType.ToString(),
                plans = p.Plans.Where(pl => pl.IsActive).Select(pl => new { id = pl.Id, name = pl.Name, code = pl.Code })
            })
            .Take(15)
            .ToListAsync();
        return Json(rows);
    }
}
