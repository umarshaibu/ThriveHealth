using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Audit;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Reporting;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;
using ThriveHealth.Web.Services.Ai;

namespace ThriveHealth.Web.Controllers;

[Authorize]
public class ReportingController : Controller
{
    public const string ReportingStaff = Roles.SystemAdministrator + "," + Roles.MedicalDirector + "," +
        Roles.ChiefExecutive + "," + Roles.ChiefFinancialOfficer + "," + Roles.ChiefNursingOfficer + "," +
        Roles.HrOfficer + "," + Roles.RecordsOfficer + "," + Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," + Roles.Nurse;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly INhmisReportService _nhmis;
    private readonly IIdsrService _idsr;
    private readonly IAuditService _audit;
    private readonly IClinicalAiService _ai;

    public ReportingController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        INhmisReportService nhmis, IIdsrService idsr, IAuditService audit, IClinicalAiService ai)
    {
        _db = db; _userManager = userManager; _nhmis = nhmis; _idsr = idsr; _audit = audit; _ai = ai;
    }

    private async Task<(int facilityId, string userId)?> Ctx()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u?.FacilityId is null) return null;
        return (u.FacilityId.Value, u.Id);
    }

    // ---- IDSR ----

    [HttpGet, HasPermission(Permissions.IdsrReport)]
    public async Task<IActionResult> Idsr(int? diseaseId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var query = _db.IdsrCases.AsNoTracking()
            .Include(c => c.NotifiableDisease)
            .Where(c => c.FacilityId == ctx.Value.facilityId);
        if (diseaseId.HasValue) query = query.Where(c => c.NotifiableDiseaseId == diseaseId.Value);
        var rows = await query.OrderByDescending(c => c.OnsetDate).Take(300).ToListAsync();

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var monthStartD = DateOnly.FromDateTime(monthStart);
        var diseases = await _db.NotifiableDiseases.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.SortOrder).ToListAsync();

        return View(new IdsrListViewModel
        {
            Rows = rows.Select(c => new IdsrListRow { Case = c, Disease = c.NotifiableDisease! }).ToList(),
            OpenCount = rows.Count(c => c.Status == IdsrCaseStatus.Open),
            ImmediateUnnotifiedCount = rows.Count(c => c.NotifiableDisease!.Window == NotificationWindow.Immediate && !c.NotifiedNcdc),
            ConfirmedThisMonthCount = rows.Count(c => c.Classification == CaseClassification.Confirmed && c.OnsetDate >= monthStartD),
            DeathsThisMonthCount = rows.Count(c => c.Outcome == CaseOutcome.Died && c.OnsetDate >= monthStartD),
            FilterDiseaseId = diseaseId,
            Diseases = diseases
        });
    }

    [HttpGet, HasPermission(Permissions.IdsrReport)]
    public async Task<IActionResult> NewIdsr(int? patientId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        await PopulateDiseases();
        var vm = new IdsrCaseInputViewModel();
        if (patientId.HasValue)
        {
            var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == patientId && x.FacilityId == ctx.Value.facilityId);
            if (p != null)
            {
                vm.PatientId = p.Id;
                vm.PatientLabel = $"{p.FullName} · {p.HospitalNumber}";
                vm.PatientName = p.FullName;
                vm.AgeYears = p.AgeYears;
                vm.Sex = p.Sex.ToString();
                vm.Phone = p.Phone;
                vm.Lga = p.Lga;
                vm.State = p.State;
                vm.Address = p.StreetAddress;
            }
        }
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.IdsrReport)]
    public async Task<IActionResult> NewIdsr(IdsrCaseInputViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        if (!ModelState.IsValid)
        {
            await PopulateDiseases();
            return View(m);
        }
        var c = new IdsrCase
        {
            FacilityId = ctx.Value.facilityId,
            NotifiableDiseaseId = m.NotifiableDiseaseId,
            PatientId = m.PatientId,
            PatientName = m.PatientName,
            AgeYears = m.AgeYears,
            Sex = m.Sex,
            Lga = m.Lga,
            State = m.State,
            Address = m.Address,
            Phone = m.Phone,
            OnsetDate = m.OnsetDate,
            ReportDate = m.ReportDate,
            Classification = m.Classification,
            Symptoms = m.Symptoms,
            Exposure = m.Exposure,
            Vaccinated = m.Vaccinated,
            LabSampleCollected = m.LabSampleCollected,
            LabSampleDate = m.LabSampleDate,
            LabSampleType = m.LabSampleType,
            LabResult = m.LabResult,
            Comments = m.Comments,
            Status = IdsrCaseStatus.Open,
            ReportedById = ctx.Value.userId
        };
        var id = await _idsr.ReportCaseAsync(c);
        TempData["Success"] = $"Case reported · {c.CaseNumber}";
        return RedirectToAction(nameof(IdsrDetail), new { id });
    }

    [HttpGet, HasPermission(Permissions.IdsrReport)]
    public async Task<IActionResult> IdsrDetail(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var c = await _db.IdsrCases.Include(x => x.NotifiableDisease).Include(x => x.Patient).Include(x => x.ReportedBy)
            .FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (c is null) return NotFound();
        return View(c);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.IdsrNotify)]
    public async Task<IActionResult> NotifyNcdc(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        await _idsr.NotifyNcdcAsync(id);
        await _audit.LogAsync("idsr.ncdc.notified", AuditCategory.BusinessAction, AuditOutcome.Success,
            entityType: "IdsrCase", entityKey: id.ToString(),
            summary: "IDSR case marked as notified to NCDC", facilityId: ctx.Value.facilityId);
        TempData["Success"] = "Marked as notified to NCDC.";
        return RedirectToAction(nameof(IdsrDetail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.IdsrReport)]
    public async Task<IActionResult> CloseIdsr(IdsrCloseViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        await _idsr.CloseCaseAsync(m.Id, m.Outcome, m.OutcomeDate, m.Comments);
        TempData["Success"] = "Case closed.";
        return RedirectToAction(nameof(IdsrDetail), new { id = m.Id });
    }

    [HttpPost, HasPermission(Permissions.AiIdsrOutbreak)]
    public async Task<IActionResult> AssessIdsrOutbreak(int windowDays = 14)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        windowDays = Math.Clamp(windowDays, 7, 90);

        var now = DateOnly.FromDateTime(DateTime.UtcNow);
        var winStart = now.AddDays(-windowDays);
        var priorStart = winStart.AddDays(-windowDays);

        var cases = await _db.IdsrCases.AsNoTracking()
            .Include(c => c.NotifiableDisease)
            .Include(c => c.Patient)
            .Where(c => c.FacilityId == ctx.Value.facilityId && c.OnsetDate >= priorStart)
            .ToListAsync();

        var clusters = cases
            .Where(c => c.NotifiableDisease != null)
            .GroupBy(c => new { c.NotifiableDisease!.Code, c.NotifiableDisease.Name })
            .Select(g => new DiseaseClusterSnapshot(
                g.Key.Code, g.Key.Name,
                g.Count(c => c.OnsetDate >= winStart),
                g.Count(c => c.OnsetDate >= priorStart && c.OnsetDate < winStart),
                g.Where(c => c.OnsetDate >= winStart && !string.IsNullOrEmpty(c.Patient?.Lga))
                    .Select(c => c.Patient!.Lga).Distinct().Count(),
                null))
            .Where(c => c.CasesInWindow > 0 || c.CasesPriorWindow > 0)
            .OrderByDescending(c => c.CasesInWindow)
            .ToList();

        if (clusters.Count == 0)
            return Json(new { ok = true, text = "No notifiable-disease cases in the last " + windowDays + " days. Nothing to assess.", error = (string?)null });

        var input = new IdsrOutbreakInput(ctx.Value.facilityId, windowDays, clusters);
        var outcome = await _ai.DetectIdsrOutbreakAsync(input, ctx.Value.userId);
        return Json(new { ok = outcome.Ok, text = outcome.Text, error = outcome.Error });
    }

    private async Task PopulateDiseases()
    {
        ViewBag.Diseases = await _db.NotifiableDiseases.AsNoTracking().Where(d => d.IsActive).OrderBy(d => d.SortOrder).ToListAsync();
    }

    // ---- NHMIS ----

    [HttpGet, HasPermission(Permissions.NhmisGenerate)]
    public async Task<IActionResult> Nhmis(int? year)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var y = year ?? DateTime.UtcNow.Year;
        var reports = await _db.NhmisReports.AsNoTracking()
            .Include(r => r.GeneratedBy).Include(r => r.SubmittedBy)
            .Where(r => r.FacilityId == ctx.Value.facilityId && r.Year == y)
            .OrderByDescending(r => r.Month).ToListAsync();
        return View(new NhmisListViewModel { Reports = reports, Year = y });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.NhmisGenerate)]
    public async Task<IActionResult> NhmisGenerate(int year, int month)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var report = await _nhmis.GenerateOrUpdateAsync(ctx.Value.facilityId, year, month, ctx.Value.userId);
        TempData["Success"] = $"NHMIS report for {year}-{month:D2} generated.";
        return RedirectToAction(nameof(NhmisDetail), new { id = report.Id });
    }

    [HttpGet, HasPermission(Permissions.NhmisGenerate)]
    public async Task<IActionResult> NhmisDetail(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var report = await _db.NhmisReports.Include(r => r.GeneratedBy).Include(r => r.SubmittedBy)
            .FirstOrDefaultAsync(r => r.Id == id && r.FacilityId == ctx.Value.facilityId);
        if (report is null) return NotFound();
        var aggregates = JsonSerializer.Deserialize<NhmisAggregates>(report.AggregatesJson)
            ?? throw new InvalidOperationException("Invalid aggregates JSON");
        return View(new NhmisDetailViewModel { Report = report, Aggregates = aggregates });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.NhmisSubmit)]
    public async Task<IActionResult> NhmisSubmit(NhmisSubmitViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        await _nhmis.SubmitAsync(m.Id, m.SubmittedToWhom, m.SubmissionReference, ctx.Value.userId);
        await _audit.LogAsync("nhmis.submitted", AuditCategory.BusinessAction, AuditOutcome.Success,
            entityType: "NhmisReport", entityKey: m.Id.ToString(),
            summary: $"NHMIS report submitted to {m.SubmittedToWhom}" + (string.IsNullOrEmpty(m.SubmissionReference) ? "" : $" (ref {m.SubmissionReference})"),
            facilityId: ctx.Value.facilityId);
        TempData["Success"] = "Report submitted.";
        return RedirectToAction(nameof(NhmisDetail), new { id = m.Id });
    }
}
