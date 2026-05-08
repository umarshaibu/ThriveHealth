using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Ai;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Emergency;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Inpatient;
using ThriveHealth.Web.Models.Patients;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;
using ThriveHealth.Web.Services.Ai;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.EmergencyBoardRead)]
public class EmergencyController : Controller
{
    public const string AnE = Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.Nurse + "," + Roles.Midwife + "," + Roles.ChiefNursingOfficer + "," +
        Roles.TriageClerk + "," + Roles.Receptionist + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    public const string CanDispose = Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITriageService _triage;
    private readonly IOrderSetService _orderSets;
    private readonly IAdmissionService _admissions;
    private readonly IClinicalAiService _ai;

    public EmergencyController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        ITriageService triage,
        IOrderSetService orderSets,
        IAdmissionService admissions,
        IClinicalAiService ai)
    {
        _db = db;
        _userManager = userManager;
        _triage = triage;
        _orderSets = orderSets;
        _admissions = admissions;
        _ai = ai;
    }

    private async Task<(int facilityId, string userId)?> Ctx()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u?.FacilityId is null) return null;
        return (u.FacilityId.Value, u.Id);
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var openEncs = await _db.Encounters
            .Include(e => e.Patient)
            .Include(e => e.Triage)
            .Include(e => e.ResusBay)
            .Where(e => e.FacilityId == ctx.Value.facilityId
                     && e.Type == EncounterType.Emergency
                     && e.Status == EncounterStatus.InProgress)
            .OrderByDescending(e => e.StartedAt)
            .ToListAsync();

        var rows = openEncs.Where(e => e.Triage != null).Select(e =>
        {
            var min = (int)(DateTime.UtcNow - e.Triage!.TargetSeenByUtc).TotalMinutes;
            return new TriageQueueRow
            {
                Encounter = e,
                Triage = e.Triage!,
                Patient = e.Patient!,
                MinutesOverdue = min > 0 ? min : null
            };
        }).ToList();

        var waiting = rows.Where(r => r.Encounter.ResusBayId == null)
            .OrderBy(r => r.Triage.Colour).ThenBy(r => r.Triage.TriagedAt).ToList();
        var inResus = rows.Where(r => r.Encounter.ResusBayId != null)
            .OrderBy(r => r.Encounter.ResusStartedAt).ToList();

        var bays = await _db.ResusBays.AsNoTracking()
            .Where(r => r.FacilityId == ctx.Value.facilityId && r.IsActive)
            .OrderBy(r => r.Code).ToListAsync();
        var occ = inResus.ToDictionary(r => r.Encounter.ResusBayId!.Value, r => r.Encounter);

        return View(new EmergencyDashboardViewModel
        {
            Waiting = waiting,
            InResus = inResus,
            Bays = bays,
            BayOccupancy = occ,
            RedCount = rows.Count(r => r.Triage.Colour == TriageColour.Red),
            OrangeCount = rows.Count(r => r.Triage.Colour == TriageColour.Orange),
            YellowCount = rows.Count(r => r.Triage.Colour == TriageColour.Yellow),
            GreenBlueCount = rows.Count(r => r.Triage.Colour == TriageColour.Green || r.Triage.Colour == TriageColour.Blue),
            InResusCount = inResus.Count,
            OverdueCount = rows.Count(r => r.MinutesOverdue.HasValue)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Triage(int? patientId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var vm = new TriageFormViewModel();
        if (patientId.HasValue)
        {
            var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == patientId && x.FacilityId == ctx.Value.facilityId);
            if (p is not null)
            {
                vm.PatientId = p.Id;
                vm.PatientLabel = $"{p.FullName} · {p.HospitalNumber}";
            }
        }
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Triage(TriageFormViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        if (m.PatientId is null) ModelState.AddModelError(nameof(m.PatientId), "Pick a patient (or quick-register).");
        if (string.IsNullOrWhiteSpace(m.ChiefComplaint)) ModelState.AddModelError(nameof(m.ChiefComplaint), "Chief complaint is required.");
        if (!ModelState.IsValid) return View(m);

        var encounter = await _triage.TriageAsync(new TriageRequest(
            ctx.Value.facilityId, m.PatientId!.Value, ctx.Value.userId,
            m.Colour, m.Arrival, m.ChiefComplaint, m.IsTrauma, m.MechanismOfInjury,
            m.Avpu, m.GcsTotal, m.IsPregnant, m.LastMealUtc,
            m.IsForensicCase, m.ForensicCategory, m.PoliceReportNumber, m.AccompanyingPerson,
            m.KnownAllergies, m.CurrentMedications));

        if (m.SystolicBp.HasValue || m.HeartRate.HasValue || m.TemperatureCelsius.HasValue || m.SpO2.HasValue)
        {
            _db.Vitals.Add(new VitalsRecord
            {
                PatientId = m.PatientId.Value,
                RecordedById = ctx.Value.userId,
                RecordedAt = DateTime.UtcNow,
                SystolicBp = m.SystolicBp,
                DiastolicBp = m.DiastolicBp,
                HeartRate = m.HeartRate,
                RespiratoryRate = m.RespiratoryRate,
                TemperatureCelsius = m.TemperatureCelsius,
                SpO2 = m.SpO2,
                PainScore = m.PainScore,
                GcsTotal = m.GcsTotal,
                Notes = "Recorded at A&E triage"
            });
            await _db.SaveChangesAsync();
        }

        TempData["Success"] = $"Triaged · {m.Colour}";
        return RedirectToAction(nameof(Encounter), new { id = encounter.Id });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AiTriageAssist)]
    public async Task<IActionResult> AssistTriage([FromForm] TriageFormViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(m.ChiefComplaint))
            return Json(new { ok = false, error = "Enter chief complaint first." });

        string? ageSex = null;
        if (m.PatientId.HasValue)
        {
            var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == m.PatientId && x.FacilityId == ctx.Value.facilityId);
            if (p != null)
            {
                ageSex = p.DateOfBirth.HasValue
                    ? $"{(int)((DateTime.UtcNow - p.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)).TotalDays / 365)}y {p.Sex}"
                    : p.Sex.ToString();
            }
        }

        var vitals = new List<string>();
        if (m.SystolicBp.HasValue || m.DiastolicBp.HasValue) vitals.Add($"BP {m.SystolicBp}/{m.DiastolicBp}");
        if (m.HeartRate.HasValue) vitals.Add($"HR {m.HeartRate}");
        if (m.RespiratoryRate.HasValue) vitals.Add($"RR {m.RespiratoryRate}");
        if (m.TemperatureCelsius.HasValue) vitals.Add($"T {m.TemperatureCelsius:F1}°C");
        if (m.SpO2.HasValue) vitals.Add($"SpO2 {m.SpO2}%");
        if (m.PainScore.HasValue) vitals.Add($"Pain {m.PainScore}/10");

        var input = new TriageAssistInput(
            ctx.Value.facilityId, m.PatientId, m.ChiefComplaint,
            m.MechanismOfInjury,
            vitals.Count == 0 ? null : string.Join(", ", vitals),
            m.Avpu?.ToString(), m.GcsTotal,
            m.IsTrauma, m.IsPregnant, ageSex);

        var outcome = await _ai.AssistTriageAsync(input, ctx.Value.userId);
        return Json(new { ok = outcome.Ok, text = outcome.Text, error = outcome.Error });
    }

    [HttpGet]
    public async Task<IActionResult> Encounter(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var enc = await _db.Encounters
            .Include(e => e.Patient)!.ThenInclude(p => p!.Allergies)
            .Include(e => e.Triage)!.ThenInclude(t => t!.TriagedBy)
            .Include(e => e.ResusBay)
            .Include(e => e.Soap)
            .Include(e => e.Diagnoses)
            .Include(e => e.LabOrders)
            .Include(e => e.ImagingOrders)
            .Include(e => e.ProcedureOrders)
            .Include(e => e.Prescriptions).ThenInclude(p => p.Items)
            .Include(e => e.ResusEvents).ThenInclude(r => r.RecordedBy)
            .FirstOrDefaultAsync(e => e.Id == id && e.FacilityId == ctx.Value.facilityId && e.Type == EncounterType.Emergency);
        if (enc is null) return NotFound();

        var bays = await _db.ResusBays.AsNoTracking()
            .Where(b => b.FacilityId == ctx.Value.facilityId && b.IsActive)
            .OrderBy(b => b.Code).ToListAsync();

        var vitals = await _db.Vitals.AsNoTracking()
            .Where(v => v.PatientId == enc.PatientId && v.RecordedAt >= enc.StartedAt)
            .OrderByDescending(v => v.RecordedAt).ToListAsync();

        return View(new EmergencyEncounterViewModel
        {
            Encounter = enc,
            Triage = enc.Triage!,
            Patient = enc.Patient!,
            Bays = bays,
            Events = enc.ResusEvents.OrderByDescending(r => r.AtUtc).ToList(),
            Vitals = vitals,
            OrderSets = _orderSets.List()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AssignBay(int encounterId, int bayId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var ok = await _triage.AssignToBayAsync(encounterId, bayId, ctx.Value.userId);
        if (!ok) TempData["Error"] = "Bay is occupied or encounter not found.";
        else TempData["Success"] = "Patient moved to resus.";
        return RedirectToAction(nameof(Encounter), new { id = encounterId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReleaseBay(int encounterId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        await _triage.ReleaseBayAsync(encounterId, ctx.Value.userId);
        TempData["Success"] = "Patient released from resus bay.";
        return RedirectToAction(nameof(Encounter), new { id = encounterId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ApplyOrderSet(int encounterId, string setKey)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        try
        {
            var r = await _orderSets.ApplyAsync(setKey, encounterId, ctx.Value.userId);
            TempData["Success"] = $"Order set fired · {r.LabsAdded} labs · {r.ImagingAdded} imaging · {r.DrugsAdded} drugs · {r.ProceduresAdded} procedures.";
        }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Encounter), new { id = encounterId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddResusEvent(ResusEventViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var ok = await _db.Encounters.AnyAsync(e => e.Id == m.EncounterId && e.FacilityId == ctx.Value.facilityId);
        if (!ok) return NotFound();

        _db.ResusEvents.Add(new ResusEvent
        {
            EncounterId = m.EncounterId,
            Kind = m.Kind,
            Description = m.Description,
            Details = m.Details,
            AtUtc = DateTime.UtcNow,
            RecordedById = ctx.Value.userId
        });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Encounter), new { id = m.EncounterId });
    }

    [HttpGet, HasPermission(Permissions.AdmissionsManage)]
    public async Task<IActionResult> Dispose(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var enc = await _db.Encounters
            .Include(e => e.Patient).Include(e => e.Triage)
            .FirstOrDefaultAsync(e => e.Id == id && e.FacilityId == ctx.Value.facilityId);
        if (enc is null) return NotFound();

        await PopulateAdmitLists(ctx.Value.facilityId);
        ViewBag.Encounter = enc;
        return View(new DispositionViewModel
        {
            EncounterId = id,
            AdmitDoctorId = ctx.Value.userId,
            AdmitReason = enc.ChiefComplaint
        });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AdmissionsManage)]
    public async Task<IActionResult> Dispose(DispositionViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var enc = await _db.Encounters
            .Include(e => e.Diagnoses)
            .Include(e => e.Soap)
            .FirstOrDefaultAsync(e => e.Id == m.EncounterId && e.FacilityId == ctx.Value.facilityId);
        if (enc is null) return NotFound();

        if (!enc.Diagnoses.Any())
        {
            TempData["Error"] = "Add at least one diagnosis before disposing.";
            return RedirectToAction(nameof(Encounter), new { id = m.EncounterId });
        }

        if (m.Disposition == AeDisposition.Admitted)
        {
            if (m.AdmitWardId is null || m.AdmitBedId is null || string.IsNullOrEmpty(m.AdmitDoctorId))
            {
                TempData["Error"] = "Pick admitting ward, bed, and doctor.";
                return RedirectToAction(nameof(Dispose), new { id = m.EncounterId });
            }
            try
            {
                var adm = await _admissions.AdmitAsync(new AdmitRequest(
                    ctx.Value.facilityId, enc.PatientId, m.AdmitWardId.Value, m.AdmitBedId.Value,
                    m.AdmitDoctorId!, m.AdmitReason ?? enc.ChiefComplaint ?? "Admitted from A&E",
                    enc.Diagnoses.FirstOrDefault()?.Description, enc.Id));
                TempData["Success"] = $"Admitted to bed {adm.Bed?.BedNumber}.";
            }
            catch (InvalidOperationException ex)
            {
                TempData["Error"] = ex.Message;
                return RedirectToAction(nameof(Dispose), new { id = m.EncounterId });
            }
        }

        enc.Status = EncounterStatus.Signed;
        enc.SignedAt = DateTime.UtcNow;
        enc.UpdatedAt = DateTime.UtcNow;
        if (enc.ResusBayId.HasValue)
        {
            enc.ResusBayId = null;
            enc.ResusEndedAt = DateTime.UtcNow;
        }

        if (enc.Soap is null) enc.Soap = new SoapNote { EncounterId = enc.Id };
        if (!string.IsNullOrEmpty(m.Notes))
            enc.Soap.Plan = (enc.Soap.Plan ?? string.Empty) + (string.IsNullOrEmpty(enc.Soap.Plan) ? "" : "\n\n") +
                $"--- A&E disposition: {m.Disposition} ---\n{m.Notes}" +
                (string.IsNullOrEmpty(m.FollowUp) ? "" : $"\n\nFollow-up: {m.FollowUp}");

        _db.ResusEvents.Add(new ResusEvent
        {
            EncounterId = enc.Id,
            Kind = ResusEventKind.HandoverOut,
            Description = $"A&E disposition: {m.Disposition}",
            Details = m.Notes,
            RecordedById = ctx.Value.userId
        });

        await _db.SaveChangesAsync();
        TempData["Success"] = $"A&E encounter signed off · {m.Disposition}.";
        return RedirectToAction("Summary", "Encounters", new { id = enc.Id });
    }

    private async Task PopulateAdmitLists(int facilityId)
    {
        var wards = await _db.Wards.AsNoTracking()
            .Where(w => w.FacilityId == facilityId && w.IsActive)
            .OrderBy(w => w.Name).ToListAsync();
        var beds = await _db.Beds.AsNoTracking()
            .Include(b => b.Ward)
            .Where(b => b.Ward!.FacilityId == facilityId && b.Status == BedStatus.Free)
            .OrderBy(b => b.Ward!.Name).ThenBy(b => b.BedNumber)
            .Select(b => new { b.Id, b.WardId, Display = b.Ward!.Code + " · " + b.BedNumber })
            .ToListAsync();
        var clinicalRoles = new[] { Roles.Doctor, Roles.Consultant, Roles.MedicalOfficer };
        var roleIds = await _db.Roles.Where(r => clinicalRoles.Contains(r.Name!)).Select(r => r.Id).ToListAsync();
        var docIds = await _db.UserRoles.Where(ur => roleIds.Contains(ur.RoleId)).Select(ur => ur.UserId).Distinct().ToListAsync();
        var doctors = await _db.Users.Where(u => docIds.Contains(u.Id) && u.FacilityId == facilityId && u.IsActive)
            .Select(u => new { u.Id, Display = u.FirstName + " " + u.LastName + (u.Designation != null ? " · " + u.Designation : "") })
            .ToListAsync();
        ViewBag.Wards = new SelectList(wards, nameof(Ward.Id), nameof(Ward.Name));
        ViewBag.Beds = beds;
        ViewBag.Doctors = new SelectList(doctors, "Id", "Display");
    }
}
