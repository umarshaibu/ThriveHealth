using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;
using ThriveHealth.Web.Models.ViewModels;

namespace ThriveHealth.Web.Controllers;

[Authorize]
public class EmrController : Controller
{
    private const string Clinical =
        Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.Nurse + "," + Roles.Midwife + "," + Roles.ChiefNursingOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public EmrController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private async Task<(int facilityId, string userId)?> Ctx()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u?.FacilityId is null) return null;
        return (u.FacilityId.Value, u.Id);
    }

    private async Task<bool> PatientInFacility(int patientId, int facilityId) =>
        await _db.Patients.AnyAsync(p => p.Id == patientId && p.FacilityId == facilityId);

    // -------- Allergies --------
    [HttpGet, HasPermission(Permissions.EncountersWrite)]
    public async Task<IActionResult> AddAllergy(int patientId)
    {
        var ctx = await Ctx();
        if (ctx is null || !await PatientInFacility(patientId, ctx.Value.facilityId)) return NotFound();
        return View(new AllergyEditViewModel { PatientId = patientId });
    }

    [HttpPost, HasPermission(Permissions.EncountersWrite), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAllergy(AllergyEditViewModel model)
    {
        var ctx = await Ctx();
        if (ctx is null || !await PatientInFacility(model.PatientId, ctx.Value.facilityId)) return NotFound();
        if (!ModelState.IsValid) return View(model);

        _db.Allergies.Add(new Allergy
        {
            PatientId = model.PatientId,
            Category = model.Category,
            Substance = model.Substance.Trim(),
            Reaction = model.Reaction,
            Severity = model.Severity,
            OnsetDate = model.OnsetDate,
            Notes = model.Notes,
            RecordedById = ctx.Value.userId
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Allergy recorded.";
        return RedirectToAction("Profile", "Patients", new { id = model.PatientId });
    }

    // -------- Problems --------
    [HttpGet, HasPermission(Permissions.EncountersWrite)]
    public async Task<IActionResult> AddProblem(int patientId)
    {
        var ctx = await Ctx();
        if (ctx is null || !await PatientInFacility(patientId, ctx.Value.facilityId)) return NotFound();
        return View(new ProblemEditViewModel { PatientId = patientId });
    }

    [HttpPost, HasPermission(Permissions.EncountersWrite), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddProblem(ProblemEditViewModel model)
    {
        var ctx = await Ctx();
        if (ctx is null || !await PatientInFacility(model.PatientId, ctx.Value.facilityId)) return NotFound();
        if (!ModelState.IsValid) return View(model);

        _db.Problems.Add(new Problem
        {
            PatientId = model.PatientId,
            Description = model.Description.Trim(),
            IcdCode = model.IcdCode,
            Status = model.Status,
            OnsetDate = model.OnsetDate,
            ResolutionDate = model.ResolutionDate,
            Notes = model.Notes,
            RecordedById = ctx.Value.userId
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Problem added to list.";
        return RedirectToAction("Profile", "Patients", new { id = model.PatientId });
    }

    // -------- Medications --------
    [HttpGet, HasPermission(Permissions.EncountersWrite)]
    public async Task<IActionResult> AddMedication(int patientId)
    {
        var ctx = await Ctx();
        if (ctx is null || !await PatientInFacility(patientId, ctx.Value.facilityId)) return NotFound();
        return View(new MedicationEditViewModel { PatientId = patientId });
    }

    [HttpPost, HasPermission(Permissions.EncountersWrite), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddMedication(MedicationEditViewModel model)
    {
        var ctx = await Ctx();
        if (ctx is null || !await PatientInFacility(model.PatientId, ctx.Value.facilityId)) return NotFound();
        if (!ModelState.IsValid) return View(model);

        _db.Medications.Add(new MedicationRecord
        {
            PatientId = model.PatientId,
            DrugName = model.DrugName.Trim(),
            Dose = model.Dose,
            Route = model.Route,
            Frequency = model.Frequency,
            StartDate = model.StartDate,
            EndDate = model.EndDate,
            IsCurrent = model.IsCurrent,
            Source = model.Source,
            Notes = model.Notes,
            RecordedById = ctx.Value.userId
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Medication recorded.";
        return RedirectToAction("Profile", "Patients", new { id = model.PatientId });
    }

    // -------- Vitals --------
    [HttpGet, HasPermission(Permissions.EncountersWrite)]
    public async Task<IActionResult> AddVitals(int patientId)
    {
        var ctx = await Ctx();
        if (ctx is null || !await PatientInFacility(patientId, ctx.Value.facilityId)) return NotFound();
        return View(new VitalsEditViewModel { PatientId = patientId });
    }

    [HttpPost, HasPermission(Permissions.EncountersWrite), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddVitals(VitalsEditViewModel model)
    {
        var ctx = await Ctx();
        if (ctx is null || !await PatientInFacility(model.PatientId, ctx.Value.facilityId)) return NotFound();
        if (!ModelState.IsValid) return View(model);

        _db.Vitals.Add(new VitalsRecord
        {
            PatientId = model.PatientId,
            SystolicBp = model.SystolicBp,
            DiastolicBp = model.DiastolicBp,
            HeartRate = model.HeartRate,
            RespiratoryRate = model.RespiratoryRate,
            TemperatureCelsius = model.TemperatureCelsius,
            SpO2 = model.SpO2,
            WeightKg = model.WeightKg,
            HeightCm = model.HeightCm,
            PainScore = model.PainScore,
            GcsTotal = model.GcsTotal,
            Notes = model.Notes,
            RecordedById = ctx.Value.userId,
            RecordedAt = DateTime.UtcNow
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Vitals recorded.";
        return RedirectToAction("Profile", "Patients", new { id = model.PatientId });
    }
}
