using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Pharmacy;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers;

[Authorize]
public class DrugsController : Controller
{
    private const string CanManage = Roles.Pharmacist + "," + Roles.SystemAdministrator + "," + Roles.MedicalDirector;
    private readonly ApplicationDbContext _db;
    private readonly IDrugInteractionService _interactions;
    public DrugsController(ApplicationDbContext db, IDrugInteractionService interactions)
    {
        _db = db;
        _interactions = interactions;
    }

    [HttpGet, HasPermission(Permissions.PharmacyStock)]
    public async Task<IActionResult> Index(string? q)
    {
        var query = _db.Drugs.AsNoTracking().AsQueryable();
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(d =>
                EF.Functions.ILike(d.GenericName, like) ||
                (d.BrandName != null && EF.Functions.ILike(d.BrandName, like)) ||
                (d.NafdacNumber != null && EF.Functions.ILike(d.NafdacNumber, like)) ||
                (d.Category != null && EF.Functions.ILike(d.Category, like)));
        }

        var rows = await query.OrderBy(d => d.GenericName).Take(500).ToListAsync();
        return View(new DrugListViewModel { Search = q, Drugs = rows, Total = rows.Count });
    }

    [HttpGet, HasPermission(Permissions.PharmacyStock)]
    public IActionResult Create() => View(new DrugEditViewModel());

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.PharmacyStock)]
    public async Task<IActionResult> Create(DrugEditViewModel m)
    {
        if (!ModelState.IsValid) return View(m);
        _db.Drugs.Add(new Drug
        {
            GenericName = m.GenericName.Trim(),
            BrandName = m.BrandName,
            NafdacNumber = m.NafdacNumber,
            Strength = m.Strength,
            DoseForm = m.DoseForm,
            Manufacturer = m.Manufacturer,
            AtcCode = m.AtcCode,
            Category = m.Category,
            Schedule = m.Schedule,
            UnitOfIssue = m.UnitOfIssue,
            UnitPrice = m.UnitPrice,
            ReorderLevel = m.ReorderLevel,
            IsActive = m.IsActive
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Drug added to master.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, HasPermission(Permissions.PharmacyStock)]
    public async Task<IActionResult> Edit(int id)
    {
        var d = await _db.Drugs.FindAsync(id);
        if (d is null) return NotFound();
        return View(new DrugEditViewModel
        {
            Id = d.Id, GenericName = d.GenericName, BrandName = d.BrandName,
            NafdacNumber = d.NafdacNumber, Strength = d.Strength, DoseForm = d.DoseForm,
            Manufacturer = d.Manufacturer, AtcCode = d.AtcCode, Category = d.Category,
            Schedule = d.Schedule, UnitOfIssue = d.UnitOfIssue, UnitPrice = d.UnitPrice,
            ReorderLevel = d.ReorderLevel, IsActive = d.IsActive
        });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.PharmacyStock)]
    public async Task<IActionResult> Edit(DrugEditViewModel m)
    {
        var d = await _db.Drugs.FindAsync(m.Id);
        if (d is null) return NotFound();
        if (!ModelState.IsValid) return View(m);

        d.GenericName = m.GenericName.Trim();
        d.BrandName = m.BrandName;
        d.NafdacNumber = m.NafdacNumber;
        d.Strength = m.Strength;
        d.DoseForm = m.DoseForm;
        d.Manufacturer = m.Manufacturer;
        d.AtcCode = m.AtcCode;
        d.Category = m.Category;
        d.Schedule = m.Schedule;
        d.UnitOfIssue = m.UnitOfIssue;
        d.UnitPrice = m.UnitPrice;
        d.ReorderLevel = m.ReorderLevel;
        d.IsActive = m.IsActive;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Drug updated.";
        return RedirectToAction(nameof(Index));
    }

    public class CheckInteractionsDto
    {
        public List<string>? Proposed { get; set; }
        public List<string>? Existing { get; set; }
    }

    [HttpPost]
    public async Task<IActionResult> CheckInteractions([FromBody] CheckInteractionsDto dto)
    {
        var hits = await _interactions.CheckAsync(dto.Proposed ?? new(), dto.Existing ?? new());
        return Json(hits.Select(w => new
        {
            drugA = w.DrugA,
            drugB = w.DrugB,
            severity = (int)w.Severity,
            note = w.Note
        }));
    }

    [HttpGet]
    public async Task<IActionResult> Search(string? q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2) return Json(Array.Empty<object>());
        var like = $"%{q.Trim()}%";
        var rows = await _db.Drugs.AsNoTracking()
            .Where(d => d.IsActive && (
                EF.Functions.ILike(d.GenericName, like) ||
                (d.BrandName != null && EF.Functions.ILike(d.BrandName, like))))
            .OrderBy(d => d.GenericName)
            .Take(15)
            .Select(d => new
            {
                id = d.Id,
                generic = d.GenericName,
                brand = d.BrandName,
                strength = d.Strength,
                form = d.DoseForm.ToString(),
                nafdac = d.NafdacNumber,
                schedule = (int)d.Schedule,
                unitPrice = d.UnitPrice
            })
            .ToListAsync();
        return Json(rows);
    }
}
