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

[HasPermission(Permissions.ImagingRead)]
public class ImagingController : Controller
{
    public const string RadStaff = Roles.Radiographer + "," + Roles.Doctor + "," + Roles.Consultant + "," +
        Roles.MedicalOfficer + "," + Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    public const string CanAuthorize = Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IImagingService _imaging;
    private readonly IClinicalAiService _ai;

    public ImagingController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IImagingService imaging, IClinicalAiService ai)
    {
        _db = db;
        _userManager = userManager;
        _imaging = imaging;
        _ai = ai;
    }

    private async Task<(int facilityId, string userId)?> Ctx()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u?.FacilityId is null) return null;
        return (u.FacilityId.Value, u.Id);
    }

    [HttpGet]
    public async Task<IActionResult> Worklist(ImagingModality? modality, string status = "open")
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var query = _db.ImagingOrders.AsNoTracking()
            .Include(o => o.Patient)
            .Include(o => o.Report)
            .Where(o => o.Patient!.FacilityId == ctx.Value.facilityId);

        query = status switch
        {
            "performed" => query.Where(o => o.Report != null && o.Report.PerformedAt != null && o.Report.ReportedAt == null),
            "reported" => query.Where(o => o.Report != null && o.Report.ReportedAt != null && o.Report.AuthorizedAt == null),
            "completed" => query.Where(o => o.Status == OrderStatus.Completed),
            "critical" => query.Where(o => o.Report != null && o.Report.HasCriticalFinding),
            _ => query.Where(o => o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
        };

        if (modality.HasValue)
            query = query.Where(o => o.Modality == modality.Value);

        var rows = await query
            .OrderByDescending(o => o.Urgency).ThenByDescending(o => o.OrderedAt)
            .Take(300).ToListAsync();

        var view = rows.Select(o => new ImagingWorklistRow
        {
            Order = o,
            Patient = o.Patient!,
            Report = o.Report
        }).ToList();

        var allOpen = await _db.ImagingOrders.AsNoTracking()
            .Include(o => o.Patient).Include(o => o.Report)
            .Where(o => o.Patient!.FacilityId == ctx.Value.facilityId
                     && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled)
            .ToListAsync();

        return View(new ImagingWorklistViewModel
        {
            Rows = view,
            FilterModality = modality,
            FilterStatus = status,
            OpenCount = allOpen.Count,
            AwaitingAuthCount = allOpen.Count(o => o.Report != null && o.Report.ReportedAt != null && o.Report.AuthorizedAt == null),
            CriticalCount = allOpen.Count(o => o.Report?.HasCriticalFinding == true)
        });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.ImagingPerform)]
    public async Task<IActionResult> Perform(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var o = await _db.ImagingOrders.Include(x => x.Patient).FirstOrDefaultAsync(x => x.Id == id);
        if (o is null || o.Patient?.FacilityId != ctx.Value.facilityId) return NotFound();
        await _imaging.PerformAsync(id, ctx.Value.userId);
        TempData["Success"] = "Marked performed · accession assigned.";
        return RedirectToAction(nameof(Report), new { id });
    }

    [HttpGet, HasPermission(Permissions.ImagingPerform)]
    public async Task<IActionResult> Report(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var o = await _db.ImagingOrders
            .Include(x => x.Patient)
            .Include(x => x.OrderedBy)
            .Include(x => x.Report)!.ThenInclude(r => r!.PerformedBy)
            .FirstOrDefaultAsync(x => x.Id == id && x.Patient!.FacilityId == ctx.Value.facilityId);
        if (o is null) return NotFound();

        ViewBag.AiSuggestion = await _db.AiSuggestions.AsNoTracking()
            .Where(s => s.Feature == AiFeature.ImagingDraft && s.EntityType == "ImagingOrder" && s.EntityKey == id.ToString())
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync();

        return View(new ImagingReportEntryViewModel
        {
            Order = o,
            Patient = o.Patient!,
            Report = o.Report,
            Technique = o.Report?.Technique,
            Contrast = o.Report?.Contrast,
            Findings = o.Report?.Findings,
            Impression = o.Report?.Impression,
            Recommendation = o.Report?.Recommendation,
            DicomStudyUid = o.Report?.DicomStudyUid,
            DicomViewerUrl = o.Report?.DicomViewerUrl,
            HasCriticalFinding = o.Report?.HasCriticalFinding ?? false
        });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AiImagingDraft)]
    public async Task<IActionResult> DraftImaging(int id, string? findings, string? technique, string? contrast)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var o = await _db.ImagingOrders.AsNoTracking()
            .Include(x => x.Patient)
            .FirstOrDefaultAsync(x => x.Id == id && x.Patient!.FacilityId == ctx.Value.facilityId);
        if (o is null) return NotFound();

        var ageSex = o.Patient!.DateOfBirth.HasValue
            ? $"{(int)((DateTime.UtcNow - o.Patient.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)).TotalDays / 365)}y {o.Patient.Sex}"
            : o.Patient.Sex.ToString();

        var input = new ImagingDraftInput(
            ctx.Value.facilityId, o.Id,
            o.Modality.ToString(), o.StudyDescription,
            o.ClinicalIndication, technique, contrast,
            findings, ageSex);

        var outcome = await _ai.DraftImagingReportAsync(input, ctx.Value.userId);
        if (!outcome.Ok) TempData["Error"] = "AI draft failed: " + (outcome.Error ?? "unknown");
        else TempData["Success"] = "AI draft generated · review and edit before finalising the report.";
        return RedirectToAction(nameof(Report), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AiImagingDraft)]
    public async Task<IActionResult> ReviewImagingDraft(long suggestionId, string status, string? edited)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        if (!Enum.TryParse<AiSuggestionStatus>(status, ignoreCase: true, out var parsed)) return BadRequest();
        var ok = await _ai.ReviewAsync(suggestionId, parsed, edited, ctx.Value.userId);
        if (!ok) return NotFound();
        var s = await _db.AiSuggestions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == suggestionId);
        TempData["Success"] = $"AI draft marked {parsed}.";
        return RedirectToAction(nameof(Report), new { id = int.Parse(s?.EntityKey ?? "0") });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.ImagingPerform)]
    public async Task<IActionResult> Report(ImagingReportEntryViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var ok = await _db.ImagingOrders.AnyAsync(o => o.Id == m.Order.Id && o.Patient!.FacilityId == ctx.Value.facilityId);
        if (!ok) return NotFound();

        await _imaging.SaveReportAsync(m.Order.Id, new ReportInput(
            m.Technique, m.Contrast,
            m.Findings, m.Impression, m.Recommendation,
            m.DicomStudyUid, m.DicomViewerUrl,
            m.HasCriticalFinding), ctx.Value.userId, m.Finalize);

        TempData["Success"] = m.Finalize
            ? "Report finalised · awaiting authorisation."
            : "Report saved as draft.";
        return RedirectToAction(nameof(View), new { id = m.Order.Id });
    }

    [HttpGet]
    public async Task<IActionResult> View(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var o = await _db.ImagingOrders
            .Include(x => x.Patient)
            .Include(x => x.OrderedBy)
            .Include(x => x.Report)!.ThenInclude(r => r!.PerformedBy)
            .Include(x => x.Report)!.ThenInclude(r => r!.ReportedBy)
            .Include(x => x.Report)!.ThenInclude(r => r!.AuthorizedBy)
            .FirstOrDefaultAsync(x => x.Id == id && x.Patient!.FacilityId == ctx.Value.facilityId);
        if (o is null) return NotFound();
        return View(o);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.ImagingReport)]
    public async Task<IActionResult> Authorize(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var o = await _db.ImagingOrders.Include(x => x.Patient).FirstOrDefaultAsync(x => x.Id == id);
        if (o is null || o.Patient?.FacilityId != ctx.Value.facilityId) return NotFound();

        var ok = await _imaging.AuthorizeAsync(id, ctx.Value.userId);
        if (!ok) TempData["Error"] = "Report must be finalised before authorisation.";
        else TempData["Success"] = "Report authorised.";
        return RedirectToAction(nameof(View), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Patient(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (p is null) return NotFound();
        var orders = await _db.ImagingOrders.AsNoTracking()
            .Include(o => o.Report)
            .Where(o => o.PatientId == id)
            .OrderByDescending(o => o.OrderedAt)
            .ToListAsync();
        return View(new PatientImagingHistory { Patient = p, Orders = orders });
    }
}
