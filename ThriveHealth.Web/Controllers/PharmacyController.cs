using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Pharmacy;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.PharmacyDispense)]
public class PharmacyController : Controller
{
    public const string Pharmacy = Roles.Pharmacist + "," + Roles.PharmacyTechnician + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IDispenseService _dispense;
    private readonly IDrugInteractionService _interactions;

    private readonly IFormularyService _formulary;

    public PharmacyController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IDispenseService dispense,
        IDrugInteractionService interactions,
        IFormularyService formulary)
    {
        _db = db;
        _userManager = userManager;
        _dispense = dispense;
        _interactions = interactions;
        _formulary = formulary;
    }

    private async Task<(int facilityId, string userId)?> Ctx()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u?.FacilityId is null) return null;
        return (u.FacilityId.Value, u.Id);
    }

    [HttpGet]
    public async Task<IActionResult> Worklist()
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var open = await _db.Prescriptions
            .Include(r => r.Patient)
            .Include(r => r.PrescribedBy)
            .Include(r => r.Items)
            .Where(r => r.Patient!.FacilityId == ctx.Value.facilityId
                && r.Status != PrescriptionStatus.Cancelled
                && r.Status != PrescriptionStatus.Dispensed)
            .OrderByDescending(r => r.IssuedAt)
            .Take(200)
            .ToListAsync();

        var rows = open.Select(r => new PharmacyWorklistRow
        {
            Prescription = r,
            ItemCount = r.Items.Count,
            OutstandingItems = r.Items.Count(i => !i.Quantity.HasValue || i.QuantityDispensed < i.Quantity.Value),
            OutstandingQuantity = r.Items.Sum(i => Math.Max(0, (i.Quantity ?? 0) - i.QuantityDispensed))
        }).ToList();

        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Dispense(int prescriptionId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var rx = await _db.Prescriptions
            .Include(r => r.Patient).ThenInclude(p => p!.Allergies)
            .Include(r => r.Patient).ThenInclude(p => p!.Medications)
            .Include(r => r.PrescribedBy)
            .Include(r => r.Items).ThenInclude(i => i.Drug)
            .Include(r => r.Encounter).ThenInclude(e => e!.Clinic)
            .FirstOrDefaultAsync(r => r.Id == prescriptionId && r.Patient!.FacilityId == ctx.Value.facilityId);
        if (rx is null) return NotFound();

        var stores = await _db.PharmacyStores.AsNoTracking()
            .Where(s => s.FacilityId == ctx.Value.facilityId && s.IsActive)
            .OrderBy(s => s.Name).ToListAsync();

        var drugIds = rx.Items.Where(i => i.DrugId.HasValue).Select(i => i.DrugId!.Value).Distinct().ToList();
        var stocks = drugIds.Count == 0
            ? new List<DrugStock>()
            : await _db.DrugStocks.AsNoTracking()
                .Include(s => s.Store)
                .Where(s => drugIds.Contains(s.DrugId)
                    && s.Store!.FacilityId == ctx.Value.facilityId
                    && s.QuantityOnHand > 0
                    && s.ExpiryDate >= DateOnly.FromDateTime(DateTime.UtcNow))
                .OrderBy(s => s.ExpiryDate)
                .ToListAsync();

        var stocksByDrug = stocks.GroupBy(s => s.DrugId)
            .ToDictionary(g => g.Key, g => (IReadOnlyList<DrugStock>)g.ToList());

        var proposed = rx.Items.Select(i => i.DrugName);
        var existing = rx.Patient!.Medications.Where(m => m.IsCurrent).Select(m => m.DrugName);
        var rawWarnings = await _interactions.CheckAsync(proposed, existing);
        var warnings = rawWarnings.Select(w => new ThriveHealth.Web.Models.ViewModels.InteractionWarning
        {
            DrugA = w.DrugA, DrugB = w.DrugB, Severity = w.Severity, Note = w.Note
        }).ToList();

        var rxDrugIds = rx.Items.Where(i => i.DrugId.HasValue).Select(i => i.DrugId!.Value).Distinct().ToList();
        var formularyChecks = rxDrugIds.Count > 0
            ? await _formulary.CheckManyAsync(rx.PatientId, rxDrugIds)
            : Array.Empty<FormularyCheck>();
        ViewBag.Formulary = formularyChecks.ToDictionary(c => c.DrugId);

        return View(new DispenseViewModel
        {
            Prescription = rx,
            Stores = stores,
            StocksByDrug = stocksByDrug,
            Warnings = warnings
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Dispense(DispenseSubmitDto dto)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var lines = (dto.Lines ?? new()).Select(l => new DispenseLineRequest(
            l.PrescriptionItemId, l.DrugId, l.DrugName, l.Strength, l.NafdacNumber,
            l.Quantity, l.BatchNumber, l.ExpiryDate, l.UnitPrice,
            l.IsSubstitution, l.SubstitutionReason, l.PatientInstructions));

        var result = await _dispense.DispenseAsync(
            ctx.Value.facilityId, dto.PrescriptionId, dto.StoreId,
            lines, dto.CounsellingNotes, dto.Notes, ctx.Value.userId);

        if (!result.Ok)
        {
            TempData["Error"] = result.ErrorMessage;
            return RedirectToAction(nameof(Dispense), new { prescriptionId = dto.PrescriptionId });
        }

        TempData["Success"] = "Dispensed successfully.";
        return RedirectToAction(nameof(DispenseDetail), new { id = result.DispenseId });
    }

    [HttpGet]
    public async Task<IActionResult> DispenseDetail(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var d = await _db.Dispenses
            .Include(x => x.Patient)
            .Include(x => x.Prescription).ThenInclude(p => p!.PrescribedBy)
            .Include(x => x.Store)
            .Include(x => x.DispensedBy)
            .Include(x => x.Items)
            .FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (d is null) return NotFound();
        return View(d);
    }

    [HttpGet]
    public async Task<IActionResult> Stock(string? q, int? storeId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var query = _db.DrugStocks.AsNoTracking()
            .Include(s => s.Drug)
            .Include(s => s.Store)
            .Where(s => s.Store!.FacilityId == ctx.Value.facilityId);

        if (storeId.HasValue) query = query.Where(s => s.StoreId == storeId.Value);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(s =>
                EF.Functions.ILike(s.Drug!.GenericName, like) ||
                (s.Drug!.BrandName != null && EF.Functions.ILike(s.Drug.BrandName, like)) ||
                EF.Functions.ILike(s.BatchNumber, like));
        }

        var rows = await query.OrderBy(s => s.Drug!.GenericName).ThenBy(s => s.ExpiryDate).Take(500).ToListAsync();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var soon = today.AddDays(90);

        ViewBag.Stores = await _db.PharmacyStores.AsNoTracking()
            .Where(s => s.FacilityId == ctx.Value.facilityId).OrderBy(s => s.Name).ToListAsync();
        ViewBag.SelectedStore = storeId;

        return View(new StockListViewModel
        {
            Stocks = rows,
            Filter = q,
            LowStockCount = rows.Count(s => s.Drug?.ReorderLevel.HasValue == true && s.QuantityOnHand <= s.Drug.ReorderLevel),
            ExpiringSoonCount = rows.Count(s => s.ExpiryDate >= today && s.ExpiryDate <= soon),
            ExpiredCount = rows.Count(s => s.ExpiryDate < today)
        });
    }

    [HttpGet]
    public async Task<IActionResult> ControlledRegister(DateOnly? from, DateOnly? to)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var f = from ?? DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
        var t = to ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var fromUtc = f.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var toUtc = t.AddDays(1).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);

        var entries = await _db.DispenseItems.AsNoTracking()
            .Include(i => i.Dispense).ThenInclude(d => d!.Patient)
            .Include(i => i.Dispense).ThenInclude(d => d!.DispensedBy)
            .Include(i => i.Dispense).ThenInclude(d => d!.Prescription).ThenInclude(p => p!.PrescribedBy)
            .Include(i => i.Drug)
            .Where(i => i.Dispense!.FacilityId == ctx.Value.facilityId
                && i.Dispense.DispensedAt >= fromUtc && i.Dispense.DispensedAt < toUtc
                && i.Drug != null && (
                    i.Drug.Schedule == DrugCategory.ControlledSchedule1 ||
                    i.Drug.Schedule == DrugCategory.ControlledSchedule2 ||
                    i.Drug.Schedule == DrugCategory.ControlledSchedule3 ||
                    i.Drug.Schedule == DrugCategory.ControlledSchedule4))
            .OrderByDescending(i => i.Dispense!.DispensedAt)
            .ToListAsync();

        return View(new ControlledRegisterViewModel { From = f, To = t, Entries = entries });
    }
}
