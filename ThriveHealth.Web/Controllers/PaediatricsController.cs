using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Immunization;
using ThriveHealth.Web.Models.Paediatrics;
using ThriveHealth.Web.Models.Patients;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services.Ai;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.PaedsManage)]
public class PaediatricsController : Controller
{
    public const string PaedsStaff = Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.Nurse + "," + Roles.Midwife + "," + Roles.ChiefNursingOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IClinicalAiService _ai;

    public PaediatricsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IClinicalAiService ai)
    {
        _db = db; _userManager = userManager; _ai = ai;
    }

    private async Task<(int facilityId, string userId)?> Ctx()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u?.FacilityId is null) return null;
        return (u.FacilityId.Value, u.Id);
    }

    private static int? AgeMonths(DateOnly? dob, DateOnly today)
    {
        if (dob is null) return null;
        var months = (today.Year - dob.Value.Year) * 12 + (today.Month - dob.Value.Month);
        if (today.Day < dob.Value.Day) months--;
        return Math.Max(0, months);
    }

    [HttpGet]
    public async Task<IActionResult> Index(string? q)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var fiveYrCutoff = today.AddYears(-5);

        var query = _db.Patients.AsNoTracking()
            .Where(p => p.FacilityId == ctx.Value.facilityId && !p.IsMergedAlias && p.DateOfBirth.HasValue && p.DateOfBirth >= fiveYrCutoff);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(p =>
                EF.Functions.ILike(p.FirstName, like) ||
                EF.Functions.ILike(p.LastName, like) ||
                EF.Functions.ILike(p.HospitalNumber, like));
        }

        var children = await query.OrderBy(p => p.LastName).Take(300).ToListAsync();
        var ids = children.Select(c => c.Id).ToList();
        var profiles = await _db.ChildProfiles.Where(c => ids.Contains(c.PatientId)).ToDictionaryAsync(c => c.PatientId);
        var lastMeasures = await _db.GrowthMeasurements
            .Where(g => ids.Contains(g.PatientId))
            .GroupBy(g => g.PatientId)
            .Select(g => g.OrderByDescending(x => x.DateOfMeasurement).First())
            .ToListAsync();
        var lastByPid = lastMeasures.ToDictionary(g => g.PatientId);

        var overdue = await _db.ImmunizationDoses
            .Where(d => ids.Contains(d.PatientId) && d.Status == DoseStatus.Due && d.DueDate < today)
            .GroupBy(d => d.PatientId)
            .Select(g => new { Pid = g.Key, Count = g.Count() })
            .ToListAsync();
        var overdueByPid = overdue.ToDictionary(x => x.Pid, x => x.Count);

        var rows = children.Select(c => new ChildListRow
        {
            Patient = c,
            Profile = profiles.GetValueOrDefault(c.Id),
            LastMeasurement = lastByPid.GetValueOrDefault(c.Id),
            AgeMonths = AgeMonths(c.DateOfBirth, today),
            OverdueDoses = overdueByPid.GetValueOrDefault(c.Id, 0)
        }).ToList();

        return View(new ChildListViewModel
        {
            Rows = rows,
            Search = q,
            Under5Count = rows.Count,
            Under1Count = rows.Count(r => r.AgeMonths is < 12),
            OverdueCount = rows.Count(r => r.OverdueDoses > 0)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Profile(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var patient = await _db.Patients.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == id && p.FacilityId == ctx.Value.facilityId);
        if (patient is null) return NotFound();
        var profile = await _db.ChildProfiles
            .Include(c => c.MotherPatient).Include(c => c.FatherPatient)
            .FirstOrDefaultAsync(c => c.PatientId == id);
        var measurements = await _db.GrowthMeasurements.AsNoTracking()
            .Where(g => g.PatientId == id)
            .OrderByDescending(g => g.DateOfMeasurement).Take(50).ToListAsync();
        ViewBag.Patient = patient;
        ViewBag.Profile = profile;
        ViewBag.Measurements = measurements;
        return View(new ChildProfileInputViewModel
        {
            PatientId = id,
            MotherPatientId = profile?.MotherPatientId,
            MotherLabel = profile?.MotherPatient is null ? null : $"{profile.MotherPatient.FullName} · {profile.MotherPatient.HospitalNumber}",
            FatherPatientId = profile?.FatherPatientId,
            FatherLabel = profile?.FatherPatient is null ? null : $"{profile.FatherPatient.FullName} · {profile.FatherPatient.HospitalNumber}",
            BirthWeightG = profile?.BirthWeightG,
            BirthLengthCm = profile?.BirthLengthCm,
            BirthHeadCircCm = profile?.BirthHeadCircCm,
            GestationalAgeAtBirthWeeks = profile?.GestationalAgeAtBirthWeeks,
            CurrentFeeding = profile?.CurrentFeeding ?? FeedingType.ExclusiveBreast,
            KnownAllergies = profile?.KnownAllergies,
            Notes = profile?.Notes
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ChildProfileInputViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == m.PatientId && p.FacilityId == ctx.Value.facilityId);
        if (patient is null) return NotFound();

        var profile = await _db.ChildProfiles.FirstOrDefaultAsync(c => c.PatientId == m.PatientId);
        if (profile is null)
        {
            profile = new ChildProfile { PatientId = m.PatientId };
            _db.ChildProfiles.Add(profile);
        }
        profile.MotherPatientId = m.MotherPatientId;
        profile.FatherPatientId = m.FatherPatientId;
        profile.BirthWeightG = m.BirthWeightG;
        profile.BirthLengthCm = m.BirthLengthCm;
        profile.BirthHeadCircCm = m.BirthHeadCircCm;
        profile.GestationalAgeAtBirthWeeks = m.GestationalAgeAtBirthWeeks;
        profile.CurrentFeeding = m.CurrentFeeding;
        profile.KnownAllergies = m.KnownAllergies;
        profile.Notes = m.Notes;
        profile.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = "Child profile saved.";
        return RedirectToAction(nameof(Profile), new { id = m.PatientId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddGrowth(GrowthInputViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == m.PatientId && p.FacilityId == ctx.Value.facilityId);
        if (patient is null) return NotFound();

        var ageMonths = m.AgeMonths > 0 ? m.AgeMonths : (AgeMonths(patient.DateOfBirth, m.DateOfMeasurement) ?? 0);
        decimal? bmi = null;
        if (m.WeightKg.HasValue && m.HeightCm.HasValue && m.HeightCm.Value > 0)
        {
            var hM = m.HeightCm.Value / 100m;
            bmi = Math.Round(m.WeightKg.Value / (hM * hM), 2);
        }

        // Simple MUAC / weight-based status (Nigerian under-5 IMNCI bands)
        string? status = null;
        if (m.MuacCm.HasValue)
        {
            status = m.MuacCm.Value < 11.5m ? "Severe acute malnutrition (SAM)"
                   : m.MuacCm.Value < 12.5m ? "Moderate acute malnutrition (MAM)"
                   : "Within normal range";
        }

        _db.GrowthMeasurements.Add(new GrowthMeasurement
        {
            FacilityId = ctx.Value.facilityId,
            PatientId = m.PatientId,
            DateOfMeasurement = m.DateOfMeasurement,
            AgeMonths = ageMonths,
            WeightKg = m.WeightKg,
            HeightCm = m.HeightCm,
            HeadCircumferenceCm = m.HeadCircumferenceCm,
            MuacCm = m.MuacCm,
            BmiKgM2 = bmi,
            NutritionalStatus = status,
            DevelopmentalMilestoneNote = m.DevelopmentalMilestoneNote,
            Notes = m.Notes,
            RecordedById = ctx.Value.userId
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Growth measurement saved.";
        return RedirectToAction(nameof(Profile), new { id = m.PatientId });
    }

    [HttpGet, HasPermission(Permissions.AiPaedsDose)]
    public IActionResult DoseCheck() => View();

    public class AiPaedsDoseDto { public int? PatientId { get; set; } public decimal WeightKg { get; set; } public int? AgeMonths { get; set; } public string? Drug { get; set; } public string? ProposedDose { get; set; } public string? Route { get; set; } public string? Frequency { get; set; } public string? Indication { get; set; } }

    [HttpPost, HasPermission(Permissions.AiPaedsDose)]
    public async Task<IActionResult> CheckDose([FromBody] AiPaedsDoseDto dto)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.Drug) || string.IsNullOrWhiteSpace(dto.ProposedDose) || dto.WeightKg <= 0)
            return Json(new { ok = false, error = "Drug, proposed dose, and weight (kg > 0) are required." });

        var allergies = new List<string>();
        if (dto.PatientId.HasValue)
        {
            var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dto.PatientId && x.FacilityId == ctx.Value.facilityId);
            if (p is null) return NotFound();
            allergies = await _db.Allergies.AsNoTracking().Where(a => a.PatientId == p.Id && a.IsActive).Select(a => a.Substance).ToListAsync();
        }

        var input = new PaedsDoseInput(ctx.Value.facilityId, dto.PatientId, dto.WeightKg, dto.AgeMonths, dto.Drug!, dto.ProposedDose!, dto.Route, dto.Frequency, dto.Indication, allergies);
        var outcome = await _ai.CheckPaedsDoseAsync(input, ctx.Value.userId);
        return Json(new { ok = outcome.Ok, text = outcome.Text, error = outcome.Error });
    }
}
