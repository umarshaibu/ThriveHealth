using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Diagnostics;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Ai;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;
using ThriveHealth.Web.Services.Ai;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.LabRead)]
public class LabController : Controller
{
    public const string LabStaff = Roles.LabScientist + "," + Roles.LabTechnician + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    public const string CanAuthorize = Roles.LabScientist + "," + Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ILabService _lab;
    private readonly IClinicalAiService _ai;

    public LabController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ILabService lab, IClinicalAiService ai)
    {
        _db = db;
        _userManager = userManager;
        _lab = lab;
        _ai = ai;
    }

    private async Task<(int facilityId, string userId)?> Ctx()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u?.FacilityId is null) return null;
        return (u.FacilityId.Value, u.Id);
    }

    [HttpGet]
    public async Task<IActionResult> Worklist(LabSection? section, string status = "open")
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var query = _db.LabOrders.AsNoTracking()
            .Include(o => o.Patient)
            .Include(o => o.LabTest)
            .Include(o => o.Result)
            .Where(o => o.Patient!.FacilityId == ctx.Value.facilityId);

        query = status switch
        {
            "collected" => query.Where(o => o.CollectedAt != null && (o.Result == null || o.Result.Status == LabResultStatus.Preliminary)),
            "auth" => query.Where(o => o.Result != null && o.Result.Status == LabResultStatus.Final),
            "completed" => query.Where(o => o.Status == OrderStatus.Completed),
            "critical" => query.Where(o => o.Result != null && o.Result.HasCriticalValue),
            _ => query.Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
        };

        if (section.HasValue)
            query = query.Where(o => o.LabTest != null && o.LabTest.Section == section.Value);

        var rows = await query
            .OrderByDescending(o => o.Urgency)
            .ThenByDescending(o => o.OrderedAt)
            .Take(300)
            .ToListAsync();

        var view = rows.Select(o => new LabWorklistRow
        {
            Order = o,
            Patient = o.Patient!,
            Section = o.LabTest?.Section,
            ResultStatus = o.Result?.Status,
            HasCriticalValue = o.Result?.HasCriticalValue ?? false
        }).ToList();

        var allOpen = await _db.LabOrders.AsNoTracking()
            .Include(o => o.Patient).Include(o => o.Result)
            .Where(o => o.Patient!.FacilityId == ctx.Value.facilityId
                     && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
            .ToListAsync();

        return View(new LabWorklistViewModel
        {
            Rows = view,
            FilterSection = section,
            FilterStatus = status,
            OpenCount = allOpen.Count,
            AwaitingAuthCount = allOpen.Count(o => o.Result != null && o.Result.Status == LabResultStatus.Final),
            CriticalCount = allOpen.Count(o => o.Result?.HasCriticalValue == true)
        });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.LabPerform)]
    public async Task<IActionResult> Collect(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var o = await _db.LabOrders.Include(x => x.Patient).FirstOrDefaultAsync(x => x.Id == id);
        if (o is null || o.Patient?.FacilityId != ctx.Value.facilityId) return NotFound();

        await _lab.CollectAsync(id, ctx.Value.userId);
        TempData["Success"] = $"Specimen collected · accession {o.AccessionNumber ?? "(generated)"}.";
        return RedirectToAction(nameof(Worklist));
    }

    [HttpGet, HasPermission(Permissions.LabPerform)]
    public async Task<IActionResult> Enter(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var o = await _db.LabOrders
            .Include(x => x.Patient)
            .Include(x => x.LabTest)!.ThenInclude(t => t!.Analytes)
            .Include(x => x.Result)!.ThenInclude(r => r!.Values)
            .FirstOrDefaultAsync(x => x.Id == id && x.Patient!.FacilityId == ctx.Value.facilityId);
        if (o is null) return NotFound();
        if (o.LabTest is null)
        {
            TempData["Error"] = "This order is free-text and not linked to a structured lab test. Use the order's notes field instead.";
            return RedirectToAction(nameof(Worklist));
        }

        if (string.IsNullOrEmpty(o.AccessionNumber))
            await _lab.CollectAsync(id, ctx.Value.userId);

        var existing = o.Result?.Values.ToDictionary(v => v.LabAnalyteId, v => v.Value) ?? new();

        var entries = o.LabTest.Analytes.OrderBy(a => a.SortOrder).Select(a => new AnalyteEntry
        {
            LabAnalyteId = a.Id,
            Name = a.Name,
            Unit = a.Unit,
            RefRange = (a.RefLow.HasValue || a.RefHigh.HasValue) ? $"{a.RefLow} – {a.RefHigh}" : null,
            CriticalRange = (a.CriticalLow.HasValue || a.CriticalHigh.HasValue) ? $"crit {a.CriticalLow} – {a.CriticalHigh}" : null,
            Value = existing.GetValueOrDefault(a.Id)
        }).ToList();

        return View(new LabResultEntryViewModel
        {
            Order = o,
            Test = o.LabTest,
            Patient = o.Patient!,
            ExistingResult = o.Result,
            GeneralComment = o.Result?.GeneralComment,
            Entries = entries
        });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.LabPerform)]
    public async Task<IActionResult> Enter(LabResultEntrySubmit dto)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var ok = await _db.LabOrders.AnyAsync(o => o.Id == dto.LabOrderId && o.Patient!.FacilityId == ctx.Value.facilityId);
        if (!ok) return NotFound();

        var n = await _lab.EnterResultsAsync(
            dto.LabOrderId,
            dto.Values.Select(v => new LabValueInput(v.LabAnalyteId, v.Value ?? string.Empty)),
            dto.GeneralComment, ctx.Value.userId, dto.Finalize);

        TempData["Success"] = dto.Finalize
            ? $"Result finalised · {n} analyte(s). Awaiting authorisation by a lab scientist."
            : $"Result saved as preliminary · {n} analyte(s).";
        return RedirectToAction(nameof(View), new { id = dto.LabOrderId });
    }

    [HttpGet]
    public async Task<IActionResult> View(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var o = await _db.LabOrders
            .Include(x => x.Patient)
            .Include(x => x.OrderedBy)
            .Include(x => x.CollectedBy)
            .Include(x => x.LabTest)!.ThenInclude(t => t!.Analytes)
            .Include(x => x.Result)!.ThenInclude(r => r!.Values)
            .Include(x => x.Result)!.ThenInclude(r => r!.EnteredBy)
            .Include(x => x.Result)!.ThenInclude(r => r!.AuthorizedBy)
            .FirstOrDefaultAsync(x => x.Id == id && x.Patient!.FacilityId == ctx.Value.facilityId);
        if (o is null) return NotFound();

        ViewBag.AiSuggestion = await _db.AiSuggestions.AsNoTracking()
            .Where(s => s.Feature == AiFeature.LabInterpret && s.EntityType == "LabOrder" && s.EntityKey == id.ToString())
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync();

        return View(o);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AiLabInterpret)]
    public async Task<IActionResult> Interpret(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var o = await _db.LabOrders.AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.LabTest)
            .Include(x => x.Result)!.ThenInclude(r => r!.Values)
            .FirstOrDefaultAsync(x => x.Id == id && x.Patient!.FacilityId == ctx.Value.facilityId);
        if (o is null || o.Result is null)
        {
            TempData["Error"] = "Cannot interpret: enter results first.";
            return RedirectToAction(nameof(View), new { id });
        }

        var ageSex = o.Patient!.DateOfBirth.HasValue
            ? $"{(int)((DateTime.UtcNow - o.Patient.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)).TotalDays / 365)}y {o.Patient.Sex}"
            : o.Patient.Sex.ToString();

        var input = new LabInterpretInput(
            ctx.Value.facilityId,
            o.Id,
            o.LabTest?.Name ?? o.TestName ?? "Lab test",
            (o.Result.Values ?? new List<LabResultValue>()).Select(v => new LabAnalyteSnapshot(
                v.AnalyteName, v.Value, v.Unit, v.RefRangeDisplay, v.Flag.ToString())),
            ageSex,
            o.Result.GeneralComment);

        var outcome = await _ai.InterpretLabAsync(input, ctx.Value.userId);
        if (!outcome.Ok) TempData["Error"] = "AI interpretation failed: " + (outcome.Error ?? "unknown");
        else TempData["Success"] = "AI draft interpretation generated · review before relying on it.";
        return RedirectToAction(nameof(View), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AiLabInterpret)]
    public async Task<IActionResult> ReviewInterpretation(long suggestionId, string status, string? edited)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        if (!Enum.TryParse<AiSuggestionStatus>(status, ignoreCase: true, out var parsed)) return BadRequest();
        var ok = await _ai.ReviewAsync(suggestionId, parsed, edited, ctx.Value.userId);
        if (!ok) return NotFound();
        var s = await _db.AiSuggestions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == suggestionId);
        TempData["Success"] = $"AI suggestion marked {parsed}.";
        return RedirectToAction(nameof(View), new { id = int.Parse(s?.EntityKey ?? "0") });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.LabAuthorize)]
    public async Task<IActionResult> Authorize(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var result = await _db.LabResults.Include(r => r.LabOrder).ThenInclude(o => o!.Patient)
            .FirstOrDefaultAsync(r => r.Id == id);
        if (result is null || result.LabOrder?.Patient?.FacilityId != ctx.Value.facilityId) return NotFound();

        var ok = await _lab.AuthorizeAsync(id, ctx.Value.userId);
        if (!ok) TempData["Error"] = "Result must be marked Final before authorisation.";
        else TempData["Success"] = "Result authorised and released to ordering clinician.";
        return RedirectToAction(nameof(View), new { id = result.LabOrderId });
    }

    [HttpGet]
    public async Task<IActionResult> Patient(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (p is null) return NotFound();

        var orders = await _db.LabOrders.AsNoTracking()
            .Include(o => o.LabTest)
            .Include(o => o.Result)!.ThenInclude(r => r!.Values)
            .Where(o => o.PatientId == id)
            .OrderByDescending(o => o.OrderedAt)
            .ToListAsync();

        return View(new PatientLabHistory { Patient = p, Orders = orders });
    }
}
