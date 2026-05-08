using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Ai;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Inpatient;
using ThriveHealth.Web.Models.Patients;
using ThriveHealth.Web.Models.Pharmacy;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;
using ThriveHealth.Web.Services.Ai;

namespace ThriveHealth.Web.Controllers;

[Authorize]
public class AdmissionsController : Controller
{
    private const string CanAdmit = Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private const string Clinical = Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.Nurse + "," + Roles.Midwife + "," + Roles.ChiefNursingOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IAdmissionService _admissions;
    private readonly IMarSlotGenerator _slots;
    private readonly IClinicalAiService _ai;

    public AdmissionsController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> userManager,
        IAdmissionService admissions,
        IMarSlotGenerator slots,
        IClinicalAiService ai)
    {
        _db = db;
        _userManager = userManager;
        _admissions = admissions;
        _slots = slots;
        _ai = ai;
    }

    private async Task<(int facilityId, string userId, string firstName)?> Ctx()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u?.FacilityId is null) return null;
        return (u.FacilityId.Value, u.Id, u.FirstName);
    }

    [HttpGet]
    public async Task<IActionResult> Index(bool includeDischarged = false)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var query = _db.Admissions.AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.Ward)
            .Include(a => a.Bed)
            .Include(a => a.AdmittingDoctor)
            .Where(a => a.FacilityId == ctx.Value.facilityId);
        if (!includeDischarged)
            query = query.Where(a => a.Status == AdmissionStatus.Active);

        var rows = await query
            .OrderByDescending(a => a.AdmittedAt).Take(200).ToListAsync();
        ViewBag.IncludeDischarged = includeDischarged;
        return View(rows);
    }

    [HttpGet, HasPermission(Permissions.AdmissionsManage)]
    public async Task<IActionResult> Admit(int? patientId, int? sourceEncounterId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var vm = new AdmitViewModel
        {
            SourceEncounterId = sourceEncounterId,
            AdmittingDoctorId = ctx.Value.userId
        };

        if (sourceEncounterId.HasValue)
        {
            var enc = await _db.Encounters.AsNoTracking()
                .Include(e => e.Patient)
                .FirstOrDefaultAsync(e => e.Id == sourceEncounterId && e.FacilityId == ctx.Value.facilityId);
            if (enc is not null)
            {
                vm.PatientId = enc.PatientId;
                vm.PatientLabel = $"{enc.Patient!.FullName} · {enc.Patient.HospitalNumber}";
                vm.ReasonForAdmission = enc.ChiefComplaint ?? string.Empty;
            }
        }
        else if (patientId.HasValue)
        {
            var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == patientId && x.FacilityId == ctx.Value.facilityId);
            if (p is not null)
            {
                vm.PatientId = p.Id;
                vm.PatientLabel = $"{p.FullName} · {p.HospitalNumber}";
            }
        }

        await PopulateAdmitLists(ctx.Value.facilityId, ctx.Value.userId);
        return View(vm);
    }

    [HttpPost, HasPermission(Permissions.AdmissionsManage), ValidateAntiForgeryToken]
    public async Task<IActionResult> Admit(AdmitViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        if (m.PatientId is null) ModelState.AddModelError(nameof(m.PatientId), "Pick a patient.");
        if (m.WardId is null) ModelState.AddModelError(nameof(m.WardId), "Pick a ward.");
        if (m.BedId is null) ModelState.AddModelError(nameof(m.BedId), "Pick a bed.");
        if (string.IsNullOrEmpty(m.AdmittingDoctorId)) ModelState.AddModelError(nameof(m.AdmittingDoctorId), "Pick admitting doctor.");

        if (!ModelState.IsValid)
        {
            await PopulateAdmitLists(ctx.Value.facilityId, ctx.Value.userId);
            return View(m);
        }

        try
        {
            var adm = await _admissions.AdmitAsync(new AdmitRequest(
                ctx.Value.facilityId, m.PatientId!.Value, m.WardId!.Value, m.BedId!.Value,
                m.AdmittingDoctorId!, m.ReasonForAdmission, m.WorkingDiagnosis, m.SourceEncounterId));
            TempData["Success"] = $"Admitted to bed {adm.Bed?.BedNumber}.";
            return RedirectToAction(nameof(Details), new { id = adm.Id });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
            await PopulateAdmitLists(ctx.Value.facilityId, ctx.Value.userId);
            return View(m);
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var adm = await _db.Admissions
            .Include(a => a.Patient)!.ThenInclude(p => p!.Allergies)
            .Include(a => a.Ward)
            .Include(a => a.Bed)
            .Include(a => a.AdmittingDoctor)
            .Include(a => a.AdmissionEncounter)
            .Include(a => a.Medications.OrderByDescending(m => m.PrescribedAt)).ThenInclude(m => m.Drug)
            .Include(a => a.Medications).ThenInclude(m => m.Slots)
            .Include(a => a.Fluids.OrderByDescending(f => f.RecordedUtc)).ThenInclude(f => f.RecordedBy)
            .Include(a => a.NursingNotes.OrderByDescending(n => n.RecordedUtc)).ThenInclude(n => n.RecordedBy)
            .Include(a => a.WardRounds.OrderByDescending(r => r.RecordedUtc)).ThenInclude(r => r.RecordedBy)
            .FirstOrDefaultAsync(a => a.Id == id && a.FacilityId == ctx.Value.facilityId);
        if (adm is null) return NotFound();

        var vitals = await _db.Vitals.AsNoTracking()
            .Where(v => v.PatientId == adm.PatientId && v.RecordedAt >= adm.AdmittedAt && (adm.DischargedAt == null || v.RecordedAt <= adm.DischargedAt))
            .OrderByDescending(v => v.RecordedAt)
            .ToListAsync();

        var since = DateTime.UtcNow.AddHours(-24);
        var fluids24 = adm.Fluids.Where(f => f.RecordedUtc >= since).ToList();

        var dueSlots = adm.Medications
            .SelectMany(m => m.Slots)
            .Where(s => s.Status == MarSlotStatus.Scheduled && s.ScheduledUtc <= DateTime.UtcNow.AddHours(2))
            .OrderBy(s => s.ScheduledUtc)
            .Take(20)
            .ToList();

        return View(new AdmissionViewModel
        {
            Admission = adm,
            Patient = adm.Patient!,
            Medications = adm.Medications.ToList(),
            DueSlots = dueSlots,
            Vitals = vitals,
            Fluids = adm.Fluids.ToList(),
            NursingNotes = adm.NursingNotes.ToList(),
            WardRounds = adm.WardRounds.ToList(),
            Input24h = fluids24.Where(f => f.Kind == FluidKind.Input).Sum(f => f.VolumeMl),
            Output24h = fluids24.Where(f => f.Kind == FluidKind.Output).Sum(f => f.VolumeMl)
        });
    }

    // ---------- Medication / MAR ----------
    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AdmissionsManage)]
    public async Task<IActionResult> AddMedication(AddMedicationViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var adm = await _db.Admissions
            .Include(a => a.Patient)!.ThenInclude(p => p!.Allergies)
            .FirstOrDefaultAsync(a => a.Id == m.AdmissionId && a.FacilityId == ctx.Value.facilityId);
        if (adm is null) return NotFound();
        if (adm.Status != AdmissionStatus.Active) return BadRequest();

        var hits = adm.Patient!.Allergies
            .Where(a => a.IsActive && a.Category == AllergyCategory.Drug)
            .Select(a => a.Substance.ToLowerInvariant())
            .Where(a => m.DrugName.ToLowerInvariant().Contains(a))
            .ToList();
        if (hits.Any())
        {
            TempData["Error"] = $"Allergy block: patient is allergic to {string.Join(", ", hits)}. Remove allergy or choose alternative.";
            return RedirectToAction(nameof(Details), new { id = m.AdmissionId });
        }

        Drug? drug = null;
        if (m.DrugId.HasValue) drug = await _db.Drugs.FindAsync(m.DrugId.Value);

        var start = m.StartUtc ?? DateTime.UtcNow;
        var end = m.EndDate.HasValue
            ? m.EndDate.Value.ToDateTime(TimeOnly.MaxValue, DateTimeKind.Utc)
            : start.AddDays(7);

        var med = new InpatientMedication
        {
            AdmissionId = adm.Id,
            DrugId = drug?.Id,
            DrugName = m.DrugName.Trim(),
            Strength = m.Strength ?? drug?.Strength,
            Dose = m.Dose,
            Route = m.Route,
            Frequency = m.Frequency,
            Instructions = m.Instructions,
            IsControlled = drug?.IsControlled ?? false,
            Kind = m.Kind,
            Status = InpatientMedicationStatus.Active,
            StartUtc = start,
            EndUtc = end,
            PrescribedById = ctx.Value.userId
        };

        foreach (var slot in _slots.GenerateSlots(m.Kind, m.Frequency, start, end))
        {
            med.Slots.Add(new MarSlot
            {
                ScheduledUtc = slot,
                Status = MarSlotStatus.Scheduled
            });
        }

        _db.InpatientMedications.Add(med);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Drug chart updated · {med.Slots.Count} slot(s) generated.";
        return RedirectToAction(nameof(Details), new { id = m.AdmissionId });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AdmissionsManage)]
    public async Task<IActionResult> StopMedication(int admissionId, int medicationId, string? reason)
    {
        var ctx = await Ctx();
        var med = await _db.InpatientMedications
            .Include(x => x.Admission)
            .Include(x => x.Slots)
            .FirstOrDefaultAsync(x => x.Id == medicationId && x.AdmissionId == admissionId);
        if (med is null || med.Admission?.FacilityId != ctx?.facilityId) return NotFound();

        med.Status = InpatientMedicationStatus.Discontinued;
        med.StopReason = reason;
        med.EndUtc = DateTime.UtcNow;
        foreach (var s in med.Slots.Where(x => x.Status == MarSlotStatus.Scheduled && x.ScheduledUtc > DateTime.UtcNow))
            s.Status = MarSlotStatus.Cancelled;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Medication discontinued.";
        return RedirectToAction(nameof(Details), new { id = admissionId });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.EncountersWrite)]
    public async Task<IActionResult> AdministerSlot(AdministerSlotViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var slot = await _db.MarSlots
            .Include(s => s.InpatientMedication).ThenInclude(im => im!.Admission)
            .FirstOrDefaultAsync(s => s.Id == m.SlotId);
        if (slot is null || slot.InpatientMedication?.Admission?.FacilityId != ctx.Value.facilityId) return NotFound();

        slot.Status = m.Status;
        slot.AdministeredUtc = DateTime.UtcNow;
        slot.AdministeredById = ctx.Value.userId;
        slot.ActualDose = m.ActualDose;
        slot.Route = m.Route;
        slot.BatchNumber = m.BatchNumber;
        slot.Notes = m.Notes;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = slot.InpatientMedication.AdmissionId });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.EncountersWrite)]
    public async Task<IActionResult> RecordPrn(int admissionId, int medicationId, string? actualDose, string? notes)
    {
        var ctx = await Ctx();
        var med = await _db.InpatientMedications
            .Include(x => x.Admission)
            .FirstOrDefaultAsync(x => x.Id == medicationId && x.AdmissionId == admissionId);
        if (med is null || med.Admission?.FacilityId != ctx?.facilityId) return NotFound();

        _db.MarSlots.Add(new MarSlot
        {
            InpatientMedicationId = med.Id,
            ScheduledUtc = DateTime.UtcNow,
            Status = MarSlotStatus.Given,
            AdministeredUtc = DateTime.UtcNow,
            AdministeredById = ctx?.userId,
            ActualDose = actualDose,
            Notes = notes
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "PRN dose recorded.";
        return RedirectToAction(nameof(Details), new { id = admissionId });
    }

    // ---------- Fluids / Notes / Rounds ----------
    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.EncountersWrite)]
    public async Task<IActionResult> AddFluid(FluidAddViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var ok = await _db.Admissions.AnyAsync(a => a.Id == m.AdmissionId && a.FacilityId == ctx.Value.facilityId);
        if (!ok) return NotFound();

        _db.FluidEntries.Add(new FluidEntry
        {
            AdmissionId = m.AdmissionId,
            Kind = m.Kind, Type = m.Type,
            VolumeMl = m.VolumeMl, Description = m.Description,
            RecordedById = ctx.Value.userId
        });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = m.AdmissionId });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.EncountersWrite)]
    public async Task<IActionResult> AddNursingNote(NursingNoteViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var ok = await _db.Admissions.AnyAsync(a => a.Id == m.AdmissionId && a.FacilityId == ctx.Value.facilityId);
        if (!ok) return NotFound();

        _db.NursingNotes.Add(new NursingNote
        {
            AdmissionId = m.AdmissionId, Shift = m.Shift,
            Body = m.Body, Handover = m.Handover,
            RecordedById = ctx.Value.userId
        });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = m.AdmissionId });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AdmissionsManage)]
    public async Task<IActionResult> AddWardRound(WardRoundViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var ok = await _db.Admissions.AnyAsync(a => a.Id == m.AdmissionId && a.FacilityId == ctx.Value.facilityId);
        if (!ok) return NotFound();

        _db.WardRoundEntries.Add(new WardRoundEntry
        {
            AdmissionId = m.AdmissionId,
            Body = m.Body, PlanChanges = m.PlanChanges,
            RecordedById = ctx.Value.userId
        });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Details), new { id = m.AdmissionId });
    }

    // ---------- Discharge / Transfer ----------
    [HttpGet, HasPermission(Permissions.AdmissionsManage)]
    public async Task<IActionResult> Discharge(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var adm = await _db.Admissions.AsNoTracking()
            .Include(a => a.Patient)
            .FirstOrDefaultAsync(a => a.Id == id && a.FacilityId == ctx.Value.facilityId);
        if (adm is null) return NotFound();
        ViewBag.Admission = adm;
        ViewBag.AiSuggestion = await _db.AiSuggestions.AsNoTracking()
            .Where(s => s.Feature == AiFeature.DischargeSummary && s.EntityType == "Admission" && s.EntityKey == id.ToString())
            .OrderByDescending(s => s.CreatedAtUtc)
            .FirstOrDefaultAsync();
        return View(new DischargeViewModel
        {
            AdmissionId = id,
            DischargeDiagnosis = adm.WorkingDiagnosis ?? string.Empty
        });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AiDischargeDraft)]
    public async Task<IActionResult> DraftDischarge(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var adm = await _db.Admissions.AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.AdmissionEncounter)!.ThenInclude(e => e!.Diagnoses)
            .Include(a => a.Medications).ThenInclude(m => m.Drug)
            .Include(a => a.Fluids)
            .Include(a => a.NursingNotes.OrderByDescending(n => n.RecordedUtc).Take(8))
            .Include(a => a.WardRounds.OrderByDescending(r => r.RecordedUtc).Take(8))
            .FirstOrDefaultAsync(a => a.Id == id && a.FacilityId == ctx.Value.facilityId);
        if (adm is null) return NotFound();

        var ageSex = adm.Patient!.DateOfBirth.HasValue
            ? $"{(int)((DateTime.UtcNow - adm.Patient.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)).TotalDays / 365)}y {adm.Patient.Sex}"
            : adm.Patient.Sex.ToString();

        var los = (int)Math.Max(1, ((adm.DischargedAt ?? DateTime.UtcNow) - adm.AdmittedAt).TotalDays);
        var since = DateTime.UtcNow.AddHours(-24);
        var fluids24 = adm.Fluids.Where(f => f.RecordedUtc >= since).ToList();

        var input = new DischargeInput(
            ctx.Value.facilityId,
            adm.Id,
            ageSex,
            adm.ReasonForAdmission ?? "Not documented",
            adm.WorkingDiagnosis,
            (adm.AdmissionEncounter?.Diagnoses ?? new List<EncounterDiagnosis>()).Select(d => d.Description ?? "").Where(s => !string.IsNullOrEmpty(s)),
            adm.Medications.Select(m => $"{m.DrugName} {m.Strength} {m.Dose} {m.Route} {m.Frequency} ({m.Status})"),
            los,
            fluids24.Where(f => f.Kind == FluidKind.Input).Sum(f => f.VolumeMl),
            fluids24.Where(f => f.Kind == FluidKind.Output).Sum(f => f.VolumeMl),
            adm.NursingNotes.Select(n => $"[N {n.RecordedUtc.ToLocalTime():dd MMM HH:mm}] {n.Body}")
                .Concat(adm.WardRounds.Select(r => $"[R {r.RecordedUtc.ToLocalTime():dd MMM HH:mm}] {r.Body}")));

        var outcome = await _ai.DraftDischargeSummaryAsync(input, ctx.Value.userId);
        if (!outcome.Ok) TempData["Error"] = "AI draft failed: " + (outcome.Error ?? "unknown");
        else TempData["Success"] = "Discharge draft generated · review and edit before discharging.";
        return RedirectToAction(nameof(Discharge), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AiDischargeDraft)]
    public async Task<IActionResult> ReviewDischargeDraft(long suggestionId, string status, string? edited)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        if (!Enum.TryParse<AiSuggestionStatus>(status, ignoreCase: true, out var parsed)) return BadRequest();
        var ok = await _ai.ReviewAsync(suggestionId, parsed, edited, ctx.Value.userId);
        if (!ok) return NotFound();
        var s = await _db.AiSuggestions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == suggestionId);
        TempData["Success"] = $"AI draft marked {parsed}.";
        return RedirectToAction(nameof(Discharge), new { id = int.Parse(s?.EntityKey ?? "0") });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AdmissionsManage)]
    public async Task<IActionResult> Discharge(DischargeViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        if (!ModelState.IsValid)
        {
            ViewBag.Admission = await _db.Admissions.AsNoTracking().Include(a => a.Patient).FirstOrDefaultAsync(a => a.Id == m.AdmissionId);
            return View(m);
        }

        var ok = await _admissions.DischargeAsync(new DischargeRequest(
            m.AdmissionId, m.Disposition, m.DischargeDiagnosis, m.Summary, m.FollowUp, ctx.Value.userId));
        if (!ok) { TempData["Error"] = "Could not discharge."; return RedirectToAction(nameof(Details), new { id = m.AdmissionId }); }

        TempData["Success"] = "Patient discharged.";
        return RedirectToAction(nameof(Details), new { id = m.AdmissionId });
    }

    [HttpGet, HasPermission(Permissions.AdmissionsManage)]
    public async Task<IActionResult> Transfer(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var adm = await _db.Admissions.AsNoTracking()
            .Include(a => a.Patient).Include(a => a.Bed).Include(a => a.Ward)
            .FirstOrDefaultAsync(a => a.Id == id && a.FacilityId == ctx.Value.facilityId);
        if (adm is null) return NotFound();

        var freeBeds = await _db.Beds.AsNoTracking()
            .Include(b => b.Ward)
            .Where(b => b.Ward!.FacilityId == ctx.Value.facilityId && b.Status == BedStatus.Free && b.Id != adm.BedId)
            .OrderBy(b => b.Ward!.Name).ThenBy(b => b.BedNumber)
            .Select(b => new { b.Id, Display = b.Ward!.Code + " · " + b.BedNumber })
            .ToListAsync();
        ViewBag.Admission = adm;
        ViewBag.Beds = new SelectList(freeBeds, "Id", "Display");
        return View(new TransferViewModel { AdmissionId = id });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AdmissionsManage)]
    public async Task<IActionResult> Transfer(TransferViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        try
        {
            await _admissions.TransferAsync(new TransferRequest(m.AdmissionId, m.NewBedId, m.Reason, ctx.Value.userId));
            TempData["Success"] = "Patient transferred.";
        }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Details), new { id = m.AdmissionId });
    }

    // ---------- Helpers ----------
    private async Task PopulateAdmitLists(int facilityId, string fallbackUserId)
    {
        var wards = await _db.Wards.AsNoTracking().Where(w => w.FacilityId == facilityId && w.IsActive).OrderBy(w => w.Name).ToListAsync();
        var beds = await _db.Beds.AsNoTracking()
            .Include(b => b.Ward)
            .Where(b => b.Ward!.FacilityId == facilityId && b.Status == BedStatus.Free)
            .OrderBy(b => b.Ward!.Name).ThenBy(b => b.BedNumber)
            .Select(b => new { b.Id, b.WardId, Display = b.Ward!.Code + " · " + b.BedNumber, Restriction = (int)b.Restriction })
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
