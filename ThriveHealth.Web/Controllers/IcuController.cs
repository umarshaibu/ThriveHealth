using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Critical;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Inpatient;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.IcuChart)]
public class IcuController : Controller
{
    public const string IcuStaff = Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.Nurse + "," + Roles.ChiefNursingOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBatch13Numbering _numbering;

    public IcuController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IBatch13Numbering numbering)
    {
        _db = db; _userManager = userManager; _numbering = numbering;
    }

    private async Task<(int facilityId, string userId)?> Ctx()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u?.FacilityId is null) return null;
        return (u.FacilityId.Value, u.Id);
    }

    [HttpGet]
    public async Task<IActionResult> Board()
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var icuTypes = new[] { WardType.Icu, WardType.Hdu, WardType.Nicu };
        var admissions = await _db.Admissions.AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.Ward)
            .Where(a => a.FacilityId == ctx.Value.facilityId
                     && a.Status == AdmissionStatus.Active
                     && icuTypes.Contains(a.Ward!.Type))
            .OrderBy(a => a.Ward!.Name).ThenBy(a => a.AdmittedAt)
            .ToListAsync();

        var since = DateTime.UtcNow.AddHours(-24);
        var ids = admissions.Select(a => a.Id).ToList();
        var entries = await _db.IcuChartEntries.AsNoTracking()
            .Where(e => ids.Contains(e.AdmissionId) && e.RecordedUtc >= since)
            .ToListAsync();

        var rows = admissions.Select(a => new IcuBoardRow
        {
            Admission = a,
            Latest = entries.Where(e => e.AdmissionId == a.Id).OrderByDescending(e => e.RecordedUtc).FirstOrDefault(),
            EntriesLast24h = entries.Count(e => e.AdmissionId == a.Id)
        }).ToList();

        return View(new IcuBoardViewModel
        {
            Rows = rows,
            IcuCount = rows.Count,
            OnVentCount = rows.Count(r => r.Latest?.VentMode is not null and not VentilationMode.None and not VentilationMode.SpontaneousRoomAir and not VentilationMode.NasalCannula),
            CriticalCount = rows.Count(r => r.Latest != null && (
                (r.Latest.SystolicBp.HasValue && r.Latest.SystolicBp < 90)
                || (r.Latest.SpO2.HasValue && r.Latest.SpO2 < 90)
                || ((r.Latest.GcsEye + r.Latest.GcsVerbal + r.Latest.GcsMotor) is int gcs and < 9)))
        });
    }

    [HttpGet]
    public async Task<IActionResult> Chart(int admissionId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var admission = await _db.Admissions.AsNoTracking()
            .Include(a => a.Patient).Include(a => a.Ward)
            .FirstOrDefaultAsync(a => a.Id == admissionId && a.FacilityId == ctx.Value.facilityId);
        if (admission is null) return NotFound();

        var entries = await _db.IcuChartEntries.AsNoTracking()
            .Include(e => e.RecordedBy)
            .Where(e => e.AdmissionId == admissionId)
            .OrderByDescending(e => e.RecordedUtc)
            .Take(48).ToListAsync();

        ViewBag.Admission = admission;
        ViewBag.Entries = entries;
        return View(new IcuChartInputViewModel { AdmissionId = admissionId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEntry(IcuChartInputViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var admission = await _db.Admissions.FirstOrDefaultAsync(a => a.Id == m.AdmissionId && a.FacilityId == ctx.Value.facilityId);
        if (admission is null) return NotFound();

        int? map = (m.SystolicBp.HasValue && m.DiastolicBp.HasValue)
            ? m.DiastolicBp + (m.SystolicBp - m.DiastolicBp) / 3
            : null;

        _db.IcuChartEntries.Add(new IcuChartEntry
        {
            FacilityId = ctx.Value.facilityId,
            AdmissionId = m.AdmissionId,
            RecordedUtc = m.RecordedUtc,
            HeartRate = m.HeartRate,
            SystolicBp = m.SystolicBp,
            DiastolicBp = m.DiastolicBp,
            MeanArterialPressure = map,
            RespiratoryRate = m.RespiratoryRate,
            SpO2 = m.SpO2,
            TemperatureC = m.TemperatureC,
            GcsEye = m.GcsEye,
            GcsVerbal = m.GcsVerbal,
            GcsMotor = m.GcsMotor,
            PainScore = m.PainScore,
            Sedation = m.Sedation,
            UrineOutputMl = m.UrineOutputMl,
            CrystalloidGivenMl = m.CrystalloidGivenMl,
            BloodGivenMl = m.BloodGivenMl,
            VentMode = m.VentMode,
            FiO2 = m.FiO2,
            Peep = m.Peep,
            TidalVolumeMl = m.TidalVolumeMl,
            VentRate = m.VentRate,
            Inotropes = m.Inotropes,
            Notes = m.Notes,
            RecordedById = ctx.Value.userId
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "ICU chart entry recorded.";
        return RedirectToAction(nameof(Chart), new { admissionId = m.AdmissionId });
    }

    [HttpGet]
    public async Task<IActionResult> Dialysis(int? page)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var sessions = await _db.DialysisSessions.AsNoTracking()
            .Where(s => s.FacilityId == ctx.Value.facilityId)
            .OrderByDescending(s => s.StartUtc).Take(200).ToListAsync();
        var pids = sessions.Select(s => s.PatientId).Distinct().ToList();
        var patients = await _db.Patients.AsNoTracking()
            .Where(p => pids.Contains(p.Id))
            .ToDictionaryAsync(p => p.Id);

        var rows = sessions.Select(s => new DialysisListRow
        {
            Session = s,
            Patient = patients.GetValueOrDefault(s.PatientId)!
        }).Where(r => r.Patient != null).ToList();

        var todayUtc = DateTime.UtcNow.Date;
        var monthStart = new DateTime(todayUtc.Year, todayUtc.Month, 1);

        return View(new DialysisListViewModel
        {
            Rows = rows,
            RunningCount = rows.Count(r => r.Session.EndUtc is null),
            CompletedTodayCount = rows.Count(r => r.Session.EndUtc.HasValue && r.Session.EndUtc.Value >= todayUtc),
            ThisMonthCount = rows.Count(r => r.Session.StartUtc >= monthStart)
        });
    }

    [HttpGet]
    public async Task<IActionResult> NewDialysis(int? patientId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var vm = new DialysisInputViewModel();
        if (patientId.HasValue)
        {
            var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == patientId && x.FacilityId == ctx.Value.facilityId);
            if (p != null) { vm.PatientId = p.Id; vm.PatientLabel = $"{p.FullName} · {p.HospitalNumber}"; }
        }
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NewDialysis(DialysisInputViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        if (m.PatientId is null) ModelState.AddModelError(nameof(m.PatientId), "Pick a patient.");
        if (!ModelState.IsValid) return View(m);

        var session = new DialysisSession
        {
            FacilityId = ctx.Value.facilityId,
            PatientId = m.PatientId!.Value,
            AdmissionId = m.AdmissionId,
            SessionNumber = await _numbering.NextDialysisAsync(ctx.Value.facilityId),
            Modality = m.Modality,
            Access = m.Access,
            StartUtc = m.StartUtc,
            EndUtc = m.EndUtc,
            DurationMinutes = m.DurationMinutes,
            PreWeightKg = m.PreWeightKg,
            PostWeightKg = m.PostWeightKg,
            UfTargetMl = m.UfTargetMl,
            UfAchievedMl = m.UfAchievedMl,
            PreSystolicBp = m.PreSystolicBp,
            PreDiastolicBp = m.PreDiastolicBp,
            PostSystolicBp = m.PostSystolicBp,
            PostDiastolicBp = m.PostDiastolicBp,
            BloodFlowMlMin = m.BloodFlowMlMin,
            DialysateFlowMlMin = m.DialysateFlowMlMin,
            HeparinUnits = m.HeparinUnits,
            DialyserType = m.DialyserType,
            Complications = m.Complications,
            Notes = m.Notes,
            OperatorId = ctx.Value.userId
        };
        _db.DialysisSessions.Add(session);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Dialysis session created · {session.SessionNumber}";
        return RedirectToAction(nameof(Dialysis));
    }
}
