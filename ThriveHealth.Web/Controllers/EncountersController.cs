using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Ai;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;
using ThriveHealth.Web.Services.Ai;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.EncountersWrite)]
public class EncountersController : Controller
{
    public const string Clinicians = Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IEncounterService _enc;
    private readonly IClinicalAiService _ai;

    public EncountersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IEncounterService enc, IClinicalAiService ai)
    {
        _db = db;
        _userManager = userManager;
        _enc = enc;
        _ai = ai;
    }

    private async Task<(int facilityId, string userId)?> Ctx()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u is null || u.FacilityId is null) return null;
        return (u.FacilityId.Value, u.Id);
    }

    [HttpGet]
    public async Task<IActionResult> OpenFromQueue(int queueEntryId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var qe = await _db.QueueEntries.FirstOrDefaultAsync(x => x.Id == queueEntryId && x.FacilityId == ctx.Value.facilityId);
        if (qe is null) return NotFound();

        var enc = await _enc.OpenFromQueueAsync(queueEntryId, ctx.Value.userId);
        return RedirectToAction(nameof(Edit), new { id = enc.Id });
    }

    [HttpGet]
    public async Task<IActionResult> OpenAdHoc(int patientId, int clinicId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var p = await _db.Patients.FirstOrDefaultAsync(x => x.Id == patientId && x.FacilityId == ctx.Value.facilityId);
        var c = await _db.Clinics.FirstOrDefaultAsync(x => x.Id == clinicId && x.FacilityId == ctx.Value.facilityId);
        if (p is null || c is null) return NotFound();

        var enc = await _enc.OpenAdHocAsync(ctx.Value.facilityId, p.Id, c.Id, ctx.Value.userId, EncounterType.OutpatientOpd, null);
        return RedirectToAction(nameof(Edit), new { id = enc.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var enc = await _db.Encounters
            .Include(e => e.Patient)
            .Include(e => e.Clinic)
            .Include(e => e.Clinician)
            .Include(e => e.Soap)
            .Include(e => e.Diagnoses)
            .Include(e => e.LabOrders)
            .Include(e => e.ImagingOrders)
            .Include(e => e.ProcedureOrders)
            .Include(e => e.Prescriptions).ThenInclude(p => p.Items)
            .FirstOrDefaultAsync(e => e.Id == id && e.FacilityId == ctx.Value.facilityId);
        if (enc is null) return NotFound();
        if (enc.Status == EncounterStatus.Signed) return RedirectToAction(nameof(Summary), new { id });

        var pid = enc.PatientId;
        var allergies = await _db.Allergies.AsNoTracking().Where(x => x.PatientId == pid && x.IsActive).OrderByDescending(x => x.Severity).ToListAsync();
        var problems = await _db.Problems.AsNoTracking().Where(x => x.PatientId == pid && x.Status != ProblemStatus.Resolved).ToListAsync();
        var meds = await _db.Medications.AsNoTracking().Where(x => x.PatientId == pid && x.IsCurrent).ToListAsync();
        var vitals = await _db.Vitals.AsNoTracking().Where(x => x.PatientId == pid).OrderByDescending(x => x.RecordedAt).FirstOrDefaultAsync();
        var past = await _db.Encounters.AsNoTracking()
            .Include(e => e.Clinician)
            .Include(e => e.Diagnoses)
            .Where(e => e.PatientId == pid && e.Id != id && e.Status == EncounterStatus.Signed)
            .OrderByDescending(e => e.SignedAt).Take(5).ToListAsync();
        var dots = await _db.DotPhrases.AsNoTracking()
            .Where(d => d.OwnerId == ctx.Value.userId)
            .OrderBy(d => d.Trigger).ToListAsync();

        ViewBag.AiSuggestion = await _db.AiSuggestions.AsNoTracking()
            .Where(s => s.Feature == AiFeature.Differential && s.EntityType == "Encounter" && s.EntityKey == id.ToString())
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync();

        // Data for the in-place Admit modal — wards + free beds + doctors so the modal can
        // post straight to /admissions/admit without the user navigating away.
        var wards = await _db.Wards.AsNoTracking()
            .Where(w => w.FacilityId == ctx.Value.facilityId && w.IsActive)
            .OrderBy(w => w.Name)
            .Select(w => new { w.Id, w.Name }).ToListAsync();
        var freeBeds = await _db.Beds.AsNoTracking()
            .Include(b => b.Ward)
            .Where(b => b.Ward!.FacilityId == ctx.Value.facilityId && b.Status == ThriveHealth.Web.Models.Inpatient.BedStatus.Free)
            .OrderBy(b => b.Ward!.Name).ThenBy(b => b.BedNumber)
            .Select(b => new { b.Id, b.WardId, Label = b.Ward!.Code + " · " + b.BedNumber })
            .ToListAsync();
        var clinicalRoleNames = new[] { Roles.Doctor, Roles.Consultant, Roles.MedicalOfficer };
        var clinicalRoleIds = await _db.Roles.Where(r => clinicalRoleNames.Contains(r.Name!)).Select(r => r.Id).ToListAsync();
        var clinicianIds = await _db.UserRoles.Where(ur => clinicalRoleIds.Contains(ur.RoleId)).Select(ur => ur.UserId).Distinct().ToListAsync();
        var doctors = await _db.Users
            .Where(u => clinicianIds.Contains(u.Id) && u.FacilityId == ctx.Value.facilityId && u.IsActive)
            .Select(u => new { u.Id, Label = u.FirstName + " " + u.LastName + (u.Designation != null ? " · " + u.Designation : "") })
            .ToListAsync();

        ViewBag.AdmitWards = wards;
        ViewBag.AdmitFreeBeds = freeBeds;
        ViewBag.AdmitDoctors = doctors;

        return View(new ConsultationViewModel
        {
            Encounter = enc,
            Patient = enc.Patient!,
            Allergies = allergies,
            ActiveProblems = problems,
            CurrentMedications = meds,
            LatestVitals = vitals,
            PastEncounters = past,
            DotPhrases = dots
        });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AiDifferential)]
    public async Task<IActionResult> SuggestDifferential(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var enc = await _db.Encounters.AsNoTracking()
            .Include(e => e.Patient)
            .Include(e => e.Soap)
            .FirstOrDefaultAsync(e => e.Id == id && e.FacilityId == ctx.Value.facilityId);
        if (enc is null) return NotFound();

        var pid = enc.PatientId;
        var allergies = await _db.Allergies.AsNoTracking().Where(a => a.PatientId == pid && a.IsActive).Select(a => a.Substance).ToListAsync();
        var meds = await _db.Medications.AsNoTracking().Where(m => m.PatientId == pid && m.IsCurrent).Select(m => m.DrugName + " " + m.Dose).ToListAsync();
        var vitals = await _db.Vitals.AsNoTracking().Where(v => v.PatientId == pid).OrderByDescending(v => v.RecordedAt).FirstOrDefaultAsync();
        var vitalsLine = vitals == null ? null :
            $"BP {vitals.SystolicBp}/{vitals.DiastolicBp}, HR {vitals.HeartRate}, RR {vitals.RespiratoryRate}, T {vitals.TemperatureCelsius}°C, SpO2 {vitals.SpO2}%, GCS {vitals.GcsTotal}";

        var ageSex = enc.Patient!.DateOfBirth.HasValue
            ? $"{(int)((DateTime.UtcNow - enc.Patient.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)).TotalDays / 365)}y {enc.Patient.Sex}"
            : enc.Patient.Sex.ToString();

        var input = new DifferentialInput(
            ctx.Value.facilityId,
            enc.Id,
            enc.ChiefComplaint ?? "Not documented",
            enc.Soap?.Subjective,
            vitalsLine,
            allergies,
            meds,
            ageSex);

        var outcome = await _ai.SuggestDifferentialAsync(input, ctx.Value.userId);
        if (!outcome.Ok) TempData["Error"] = "AI suggestion failed: " + (outcome.Error ?? "unknown");
        else TempData["Success"] = "AI differential generated · review and verify before acting.";
        return RedirectToAction(nameof(Edit), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AiDifferential)]
    public async Task<IActionResult> ReviewDifferential(long suggestionId, string status, string? edited)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        if (!Enum.TryParse<AiSuggestionStatus>(status, ignoreCase: true, out var parsed)) return BadRequest();
        var ok = await _ai.ReviewAsync(suggestionId, parsed, edited, ctx.Value.userId);
        if (!ok) return NotFound();
        var s = await _db.AiSuggestions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == suggestionId);
        TempData["Success"] = $"AI suggestion marked {parsed}.";
        return RedirectToAction(nameof(Edit), new { id = int.Parse(s?.EntityKey ?? "0") });
    }

    public class AiDrugCheckDto { public int EncounterId { get; set; } public List<string>? Proposed { get; set; } public List<string>? Existing { get; set; } }

    [HttpPost, HasPermission(Permissions.AiDrugCheck)]
    public async Task<IActionResult> DrugCheck([FromBody] AiDrugCheckDto dto)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();

        var enc = await _db.Encounters.AsNoTracking()
            .Include(e => e.Patient)
            .FirstOrDefaultAsync(e => e.Id == dto.EncounterId && e.FacilityId == ctx.Value.facilityId);
        if (enc is null) return NotFound();

        var pid = enc.PatientId;
        var allergies = await _db.Allergies.AsNoTracking().Where(a => a.PatientId == pid && a.IsActive).Select(a => a.Substance).ToListAsync();
        var conditions = await _db.Problems.AsNoTracking().Where(p => p.PatientId == pid && p.Status != ProblemStatus.Resolved).Select(p => p.Description).ToListAsync();
        var existing = dto.Existing ?? new List<string>();
        if (existing.Count == 0)
            existing = await _db.Medications.AsNoTracking().Where(m => m.PatientId == pid && m.IsCurrent).Select(m => m.DrugName + " " + m.Dose).ToListAsync();

        var ageSex = enc.Patient!.DateOfBirth.HasValue
            ? $"{(int)((DateTime.UtcNow - enc.Patient.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)).TotalDays / 365)}y {enc.Patient.Sex}"
            : enc.Patient.Sex.ToString();

        var input = new DrugContextCheckInput(
            ctx.Value.facilityId, dto.EncounterId,
            dto.Proposed ?? new List<string>(),
            existing, allergies, conditions,
            null, ageSex);

        var outcome = await _ai.CheckDrugContextAsync(input, ctx.Value.userId);
        return Json(new { ok = outcome.Ok, text = outcome.Text, error = outcome.Error });
    }

    public class AiIcdSuggestDto { public int EncounterId { get; set; } public string? DiagnosisText { get; set; } }

    [HttpPost, HasPermission(Permissions.AiIcdCoding)]
    public async Task<IActionResult> IcdSuggest([FromBody] AiIcdSuggestDto dto)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.DiagnosisText))
            return Json(new { ok = false, error = "Enter diagnosis text first." });

        var ok = await _db.Encounters.AnyAsync(e => e.Id == dto.EncounterId && e.FacilityId == ctx.Value.facilityId);
        if (!ok) return NotFound();

        var input = new IcdCodingInput(ctx.Value.facilityId, dto.EncounterId, dto.DiagnosisText);
        var outcome = await _ai.SuggestIcdCodingAsync(input, ctx.Value.userId);
        return Json(new { ok = outcome.Ok, text = outcome.Text, error = outcome.Error });
    }

    [HttpGet, HasPermission(Permissions.AiReferralDraft)]
    public async Task<IActionResult> Referral(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var enc = await _db.Encounters.AsNoTracking()
            .Include(e => e.Patient)
            .Include(e => e.Diagnoses)
            .Include(e => e.Soap)
            .FirstOrDefaultAsync(e => e.Id == id && e.FacilityId == ctx.Value.facilityId);
        if (enc is null) return NotFound();

        ViewBag.AiSuggestion = await _db.AiSuggestions.AsNoTracking()
            .Where(s => s.Feature == AiFeature.ReferralDraft && s.EntityType == "Encounter" && s.EntityKey == id.ToString())
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync();

        ViewBag.Encounter = enc;
        return View();
    }

    public class AiReferralDto { public int EncounterId { get; set; } public string? ReceivingFacility { get; set; } public string? Reason { get; set; } }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AiReferralDraft)]
    public async Task<IActionResult> DraftReferral(AiReferralDto dto)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.ReceivingFacility))
        {
            TempData["Error"] = "Specify the receiving facility / specialist.";
            return RedirectToAction(nameof(Referral), new { id = dto.EncounterId });
        }

        var enc = await _db.Encounters.AsNoTracking()
            .Include(e => e.Patient)
            .Include(e => e.Diagnoses)
            .Include(e => e.Soap)
            .Include(e => e.Prescriptions).ThenInclude(p => p.Items)
            .Include(e => e.LabOrders)
            .Include(e => e.ImagingOrders)
            .FirstOrDefaultAsync(e => e.Id == dto.EncounterId && e.FacilityId == ctx.Value.facilityId);
        if (enc is null) return NotFound();

        var ageSex = enc.Patient!.DateOfBirth.HasValue
            ? $"{(int)((DateTime.UtcNow - enc.Patient.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)).TotalDays / 365)}y {enc.Patient.Sex}"
            : enc.Patient.Sex.ToString();

        var ctxParts = new List<string>();
        if (!string.IsNullOrEmpty(enc.ChiefComplaint)) ctxParts.Add("Chief complaint: " + enc.ChiefComplaint);
        if (!string.IsNullOrEmpty(enc.Soap?.Subjective)) ctxParts.Add("Subjective: " + enc.Soap.Subjective);
        if (!string.IsNullOrEmpty(enc.Soap?.Objective)) ctxParts.Add("Objective: " + enc.Soap.Objective);
        if (!string.IsNullOrEmpty(enc.Soap?.Assessment)) ctxParts.Add("Assessment: " + enc.Soap.Assessment);
        if (!string.IsNullOrEmpty(enc.Soap?.Plan)) ctxParts.Add("Plan: " + enc.Soap.Plan);

        var diagnoses = string.Join("; ", enc.Diagnoses.Select(d => $"{d.IcdCode} {d.Description}").Where(s => !string.IsNullOrWhiteSpace(s)));
        var findings = enc.LabOrders.Select(l => "Lab: " + l.TestName)
            .Concat(enc.ImagingOrders.Select(i => $"Imaging: {i.Modality} {i.StudyDescription}"))
            .ToList();
        var meds = enc.Prescriptions.SelectMany(p => p.Items).Select(i => $"{i.DrugName} {i.Dose} {i.Route} {i.Frequency}").ToList();

        var input = new ReferralDraftInput(
            ctx.Value.facilityId, dto.EncounterId, null, ageSex,
            string.Join("\n", ctxParts), diagnoses, dto.ReceivingFacility, dto.Reason,
            findings, meds);

        var outcome = await _ai.DraftReferralLetterAsync(input, ctx.Value.userId);
        if (!outcome.Ok) TempData["Error"] = "AI draft failed: " + (outcome.Error ?? "unknown");
        else TempData["Success"] = "Referral drafted · review and edit before printing.";
        return RedirectToAction(nameof(Referral), new { id = dto.EncounterId });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AiReferralDraft)]
    public async Task<IActionResult> ReviewReferralDraft(long suggestionId, string status, string? edited)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        if (!Enum.TryParse<AiSuggestionStatus>(status, ignoreCase: true, out var parsed)) return BadRequest();
        var ok = await _ai.ReviewAsync(suggestionId, parsed, edited, ctx.Value.userId);
        if (!ok) return NotFound();
        var s = await _db.AiSuggestions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == suggestionId);
        TempData["Success"] = $"Referral marked {parsed}.";
        return RedirectToAction(nameof(Referral), new { id = int.Parse(s?.EntityKey ?? "0") });
    }

    public class AiPatientSummaryDto { public int EncounterId { get; set; } public string? Language { get; set; } }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AiPatientSummary)]
    public async Task<IActionResult> DraftPatientSummary(AiPatientSummaryDto dto)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        var enc = await _db.Encounters.AsNoTracking()
            .Include(e => e.Patient)
            .Include(e => e.Diagnoses)
            .Include(e => e.Prescriptions).ThenInclude(p => p.Items)
            .Include(e => e.LabOrders)
            .Include(e => e.ImagingOrders)
            .Include(e => e.Soap)
            .FirstOrDefaultAsync(e => e.Id == dto.EncounterId && e.FacilityId == ctx.Value.facilityId);
        if (enc is null) return NotFound();

        var ageSex = enc.Patient!.DateOfBirth.HasValue
            ? $"{(int)((DateTime.UtcNow - enc.Patient.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)).TotalDays / 365)}y {enc.Patient.Sex}"
            : enc.Patient.Sex.ToString();

        var diagnoses = enc.Diagnoses.Select(d => $"{d.IcdCode} {d.Description}".Trim());
        var rx = enc.Prescriptions.SelectMany(p => p.Items).Select(i => $"{i.DrugName} {i.Dose} {i.Route} {i.Frequency} for {i.Duration} ({i.Instructions})");
        var orders = enc.LabOrders.Select(l => "Lab: " + l.TestName)
            .Concat(enc.ImagingOrders.Select(i => $"Imaging: {i.Modality} {i.StudyDescription}"));

        var input = new PatientSummaryInput(
            ctx.Value.facilityId, dto.EncounterId, ageSex,
            enc.ChiefComplaint, diagnoses, rx, orders,
            enc.Soap?.Plan, dto.Language);

        var outcome = await _ai.DraftPatientSummaryAsync(input, ctx.Value.userId);
        if (!outcome.Ok) TempData["Error"] = "AI summary failed: " + (outcome.Error ?? "unknown");
        else TempData["Success"] = "Patient summary drafted.";
        return RedirectToAction(nameof(Summary), new { id = dto.EncounterId });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AiPatientSummary)]
    public async Task<IActionResult> ReviewPatientSummary(long suggestionId, string status, string? edited)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        if (!Enum.TryParse<AiSuggestionStatus>(status, ignoreCase: true, out var parsed)) return BadRequest();
        var ok = await _ai.ReviewAsync(suggestionId, parsed, edited, ctx.Value.userId);
        if (!ok) return NotFound();
        var s = await _db.AiSuggestions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == suggestionId);
        TempData["Success"] = $"Patient summary marked {parsed}.";
        return RedirectToAction(nameof(Summary), new { id = int.Parse(s?.EntityKey ?? "0") });
    }

    [HttpPost, HasPermission(Permissions.AiDocQuality)]
    public async Task<IActionResult> DocQuality(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        var enc = await _db.Encounters.AsNoTracking()
            .Include(e => e.Soap)
            .Include(e => e.Diagnoses)
            .Include(e => e.LabOrders)
            .Include(e => e.ImagingOrders)
            .Include(e => e.Prescriptions).ThenInclude(p => p.Items)
            .FirstOrDefaultAsync(e => e.Id == id && e.FacilityId == ctx.Value.facilityId);
        if (enc is null) return NotFound();

        var diagnoses = enc.Diagnoses.Select(d => $"{d.IcdCode} {d.Description}".Trim());
        var orders = enc.LabOrders.Select(l => "Lab: " + l.TestName)
            .Concat(enc.ImagingOrders.Select(i => $"Imaging: {i.Modality} {i.StudyDescription}"))
            .Concat(enc.Prescriptions.SelectMany(p => p.Items).Select(i => $"Rx: {i.DrugName} {i.Dose} {i.Frequency}"));

        var input = new DocQualityInput(
            ctx.Value.facilityId, enc.Id,
            enc.ChiefComplaint,
            enc.Soap?.Subjective, enc.Soap?.Objective,
            enc.Soap?.Assessment, enc.Soap?.Plan,
            diagnoses, orders);

        var outcome = await _ai.ScoreDocQualityAsync(input, ctx.Value.userId);
        return Json(new { ok = outcome.Ok, text = outcome.Text, error = outcome.Error });
    }

    public class AiSoapDto { public int EncounterId { get; set; } public string? Narrative { get; set; } }

    [HttpPost, HasPermission(Permissions.AiSoapStructure)]
    public async Task<IActionResult> SoapStructure([FromBody] AiSoapDto dto)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.Narrative))
            return Json(new { ok = false, error = "Dictate or paste a narrative first." });

        var enc = await _db.Encounters.AsNoTracking()
            .Include(e => e.Patient)
            .FirstOrDefaultAsync(e => e.Id == dto.EncounterId && e.FacilityId == ctx.Value.facilityId);
        if (enc is null) return NotFound();

        var ageSex = enc.Patient!.DateOfBirth.HasValue
            ? $"{(int)((DateTime.UtcNow - enc.Patient.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)).TotalDays / 365)}y {enc.Patient.Sex}"
            : enc.Patient.Sex.ToString();

        var input = new SoapStructureInput(ctx.Value.facilityId, dto.EncounterId, dto.Narrative!, ageSex);
        var outcome = await _ai.StructureSoapAsync(input, ctx.Value.userId);
        return Json(new { ok = outcome.Ok, text = outcome.Text, error = outcome.Error });
    }

    public class AiEcgDto { public int EncounterId { get; set; } public string? Findings { get; set; } public string? Context { get; set; } }

    [HttpPost, HasPermission(Permissions.AiEcgInterpret)]
    public async Task<IActionResult> EcgInterpret([FromBody] AiEcgDto dto)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.Findings))
            return Json(new { ok = false, error = "Enter ECG findings (rate, rhythm, intervals, abnormalities) first." });

        var enc = await _db.Encounters.AsNoTracking()
            .Include(e => e.Patient)
            .FirstOrDefaultAsync(e => e.Id == dto.EncounterId && e.FacilityId == ctx.Value.facilityId);
        if (enc is null) return NotFound();

        var ageSex = enc.Patient!.DateOfBirth.HasValue
            ? $"{(int)((DateTime.UtcNow - enc.Patient.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)).TotalDays / 365)}y {enc.Patient.Sex}"
            : enc.Patient.Sex.ToString();

        var input = new EcgInterpretInput(ctx.Value.facilityId, enc.PatientId, ageSex, dto.Context ?? enc.ChiefComplaint, dto.Findings);
        var outcome = await _ai.InterpretEcgAsync(input, ctx.Value.userId);
        return Json(new { ok = outcome.Ok, text = outcome.Text, error = outcome.Error });
    }

    [HttpPost]
    public async Task<IActionResult> SaveSoap([FromBody] SoapSaveDto dto)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        var ok = await _db.Encounters.AnyAsync(e => e.Id == dto.EncounterId && e.FacilityId == ctx.Value.facilityId && e.Status == EncounterStatus.InProgress);
        if (!ok) return NotFound();

        await _enc.SaveSoapAsync(dto.EncounterId, dto.Subjective, dto.Objective, dto.Assessment, dto.Plan, ctx.Value.userId);
        return Ok(new { savedAt = DateTime.UtcNow });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddDiagnosis(DiagnosisAddDto dto)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var ok = await _db.Encounters.AnyAsync(e => e.Id == dto.EncounterId && e.FacilityId == ctx.Value.facilityId);
        if (!ok) return NotFound();

        await _enc.AddDiagnosisAsync(dto.EncounterId, dto.IcdCode, dto.Description, dto.Status, dto.IsPrimary, ctx.Value.userId);
        return RedirectToAction(nameof(Edit), new { id = dto.EncounterId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveDiagnosis(int diagnosisId, int encounterId)
    {
        await _enc.RemoveDiagnosisAsync(diagnosisId);
        return RedirectToAction(nameof(Edit), new { id = encounterId });
    }

    [HttpGet]
    public async Task<IActionResult> SearchLabTests(string? q)
    {
        var query = _db.LabTests.AsNoTracking().Where(t => t.IsActive);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(t =>
                EF.Functions.ILike(t.Code, like) ||
                EF.Functions.ILike(t.Name, like));
        }
        var rows = await query.OrderBy(t => t.Name).Take(15)
            .Select(t => new { id = t.Id, code = t.Code, name = t.Name, section = t.Section.ToString(), specimen = t.Specimen, tat = t.TurnaroundHours })
            .ToListAsync();
        return Json(rows);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddLabOrder(LabOrderAddViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var enc = await _db.Encounters.FirstOrDefaultAsync(e => e.Id == m.EncounterId && e.FacilityId == ctx.Value.facilityId);
        if (enc is null) return NotFound();

        ThriveHealth.Web.Models.Diagnostics.LabTest? lt = null;
        if (m.LabTestId.HasValue) lt = await _db.LabTests.FindAsync(m.LabTestId.Value);
        _db.LabOrders.Add(new LabOrder
        {
            EncounterId = enc.Id,
            PatientId = enc.PatientId,
            LabTestId = lt?.Id,
            TestName = lt?.Name ?? m.TestName.Trim(),
            LoincCode = lt?.LoincCode,
            Specimen = string.IsNullOrEmpty(m.Specimen) ? lt?.Specimen : m.Specimen,
            Urgency = m.Urgency,
            ClinicalIndication = m.ClinicalIndication,
            OrderedById = ctx.Value.userId
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Lab test ordered.";
        return RedirectToAction(nameof(Edit), new { id = enc.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddImagingOrder(ImagingOrderAddViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var enc = await _db.Encounters.FirstOrDefaultAsync(e => e.Id == m.EncounterId && e.FacilityId == ctx.Value.facilityId);
        if (enc is null) return NotFound();

        _db.ImagingOrders.Add(new ImagingOrder
        {
            EncounterId = enc.Id,
            PatientId = enc.PatientId,
            Modality = m.Modality,
            StudyDescription = m.StudyDescription.Trim(),
            Urgency = m.Urgency,
            ClinicalIndication = m.ClinicalIndication,
            OrderedById = ctx.Value.userId
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Imaging study ordered.";
        return RedirectToAction(nameof(Edit), new { id = enc.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProcedureOrder(ProcedureOrderAddViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var enc = await _db.Encounters.FirstOrDefaultAsync(e => e.Id == m.EncounterId && e.FacilityId == ctx.Value.facilityId);
        if (enc is null) return NotFound();

        _db.ProcedureOrders.Add(new ProcedureOrder
        {
            EncounterId = enc.Id,
            PatientId = enc.PatientId,
            ProcedureName = m.ProcedureName.Trim(),
            CptCode = m.CptCode,
            Urgency = m.Urgency,
            Notes = m.Notes,
            OrderedById = ctx.Value.userId
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Procedure ordered.";
        return RedirectToAction(nameof(Edit), new { id = enc.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPrescription(PrescriptionAddViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var enc = await _db.Encounters
            .Include(e => e.Patient).ThenInclude(p => p!.Allergies)
            .FirstOrDefaultAsync(e => e.Id == m.EncounterId && e.FacilityId == ctx.Value.facilityId);
        if (enc is null) return NotFound();

        var validItems = m.Items
            .Where(i => !string.IsNullOrWhiteSpace(i.DrugName))
            .ToList();
        if (validItems.Count == 0)
        {
            TempData["Error"] = "Add at least one drug.";
            return RedirectToAction(nameof(Edit), new { id = enc.Id });
        }

        var allergyHits = enc.Patient!.Allergies
            .Where(a => a.IsActive && a.Category == AllergyCategory.Drug)
            .Select(a => a.Substance.ToLowerInvariant())
            .ToHashSet();
        var blocked = validItems
            .Where(i => allergyHits.Any(a => i.DrugName.ToLowerInvariant().Contains(a)))
            .Select(i => i.DrugName)
            .ToList();
        if (blocked.Any())
        {
            TempData["Error"] = $"Allergy block: patient is allergic to {string.Join(", ", blocked)}. Choose alternative or override (override workflow comes in Batch 5).";
            return RedirectToAction(nameof(Edit), new { id = enc.Id });
        }

        var rx = new Prescription
        {
            EncounterId = enc.Id,
            PatientId = enc.PatientId,
            PrescribedById = ctx.Value.userId,
            Notes = m.Notes,
            Items = validItems.Select(i => new PrescriptionItem
            {
                DrugId = i.DrugId,
                DrugName = i.DrugName.Trim(),
                Dose = i.Dose,
                Route = i.Route,
                Frequency = i.Frequency,
                Duration = i.Duration,
                Quantity = i.Quantity,
                Instructions = i.Instructions
            }).ToList()
        };

        if (rx.Items.Any(i => i.DrugId.HasValue))
        {
            var drugIds = rx.Items.Where(x => x.DrugId.HasValue).Select(x => x.DrugId!.Value).Distinct().ToList();
            var drugs = await _db.Drugs.Where(d => drugIds.Contains(d.Id)).ToListAsync();
            foreach (var i in rx.Items.Where(x => x.DrugId.HasValue))
            {
                var dr = drugs.FirstOrDefault(d => d.Id == i.DrugId);
                if (dr is null) continue;
                i.NafdacNumber = dr.NafdacNumber;
                i.IsControlled = dr.IsControlled;
            }
        }

        _db.Prescriptions.Add(rx);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Prescription with {validItems.Count} drug(s) issued.";
        return RedirectToAction(nameof(Edit), new { id = enc.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SignOff(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var enc = await _db.Encounters
            .Include(e => e.Diagnoses)
            .FirstOrDefaultAsync(e => e.Id == id && e.FacilityId == ctx.Value.facilityId);
        if (enc is null) return NotFound();

        var ok = await _enc.SignOffAsync(id, ctx.Value.userId);
        if (!ok)
        {
            TempData["Error"] = "Add at least one diagnosis before signing off.";
            return RedirectToAction(nameof(Edit), new { id });
        }
        TempData["Success"] = "Consultation signed off.";
        return RedirectToAction(nameof(Summary), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Summary(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var enc = await _db.Encounters
            .Include(e => e.Patient)
            .Include(e => e.Clinic)
            .Include(e => e.Clinician)
            .Include(e => e.Facility)
            .Include(e => e.Soap)
            .Include(e => e.Diagnoses)
            .Include(e => e.LabOrders)
            .Include(e => e.ImagingOrders)
            .Include(e => e.ProcedureOrders)
            .Include(e => e.Prescriptions).ThenInclude(p => p.Items)
            .FirstOrDefaultAsync(e => e.Id == id && e.FacilityId == ctx.Value.facilityId);
        if (enc is null) return NotFound();

        ViewBag.AiPatientSummary = await _db.AiSuggestions.AsNoTracking()
            .Where(s => s.Feature == AiFeature.PatientSummary && s.EntityType == "Encounter" && s.EntityKey == id.ToString())
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync();

        return View(new EncounterSummaryViewModel
        {
            Encounter = enc,
            Patient = enc.Patient!,
            FacilityName = enc.Facility?.Name ?? string.Empty
        });
    }

    [HttpGet]
    public async Task<IActionResult> ListByPatient(int patientId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var rows = await _db.Encounters.AsNoTracking()
            .Include(e => e.Clinician)
            .Include(e => e.Clinic)
            .Include(e => e.Diagnoses)
            .Where(e => e.PatientId == patientId && e.FacilityId == ctx.Value.facilityId)
            .OrderByDescending(e => e.StartedAt).ToListAsync();
        ViewBag.PatientId = patientId;
        return View(rows);
    }
}
