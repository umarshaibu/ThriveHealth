using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Maternity;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;
using ThriveHealth.Web.Services.Ai;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.AncManage)]
public class MaternityController : Controller
{
    public const string MaternityStaff = Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.Nurse + "," + Roles.Midwife + "," + Roles.ChiefNursingOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IMaternityService _maternity;
    private readonly IClinicalAiService _ai;

    public MaternityController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IMaternityService maternity, IClinicalAiService ai)
    {
        _db = db; _userManager = userManager; _maternity = maternity; _ai = ai;
    }

    private async Task<(int facilityId, string userId)?> Ctx()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u?.FacilityId is null) return null;
        return (u.FacilityId.Value, u.Id);
    }

    [HttpGet]
    public async Task<IActionResult> Index(AnteNatalStatus? status, string? q)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var query = _db.AnteNatalRecords.AsNoTracking()
            .Include(r => r.Patient)
            .Include(r => r.Visits)
            .Where(r => r.FacilityId == ctx.Value.facilityId);
        if (status.HasValue) query = query.Where(r => r.Status == status.Value);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(r =>
                EF.Functions.ILike(r.AncNumber, like) ||
                EF.Functions.ILike(r.Patient!.FirstName, like) ||
                EF.Functions.ILike(r.Patient!.LastName, like) ||
                EF.Functions.ILike(r.Patient!.HospitalNumber, like));
        }

        var list = await query.OrderByDescending(r => r.BookingDate).Take(300).ToListAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekEnd = today.AddDays(7);

        var rows = list.Select(r => new AncListRow
        {
            Record = r,
            Patient = r.Patient!,
            VisitsCount = r.Visits.Count,
            GestationalAgeWeeks = _maternity.CalculateGestationalAgeWeeks(r.Lmp, today),
            IsHighRisk = !string.IsNullOrEmpty(r.RiskFactors)
                          || r.HivStatus == HivStatus.Positive
                          || (r.HaemoglobinGdl.HasValue && r.HaemoglobinGdl.Value < 9m)
                          || r.RhesusPositive == false
        }).ToList();

        return View(new AncListViewModel
        {
            Rows = rows,
            FilterStatus = status,
            Search = q,
            ActiveCount = rows.Count(r => r.Record.Status == AnteNatalStatus.Booked),
            HighRiskCount = rows.Count(r => r.IsHighRisk),
            DueThisWeekCount = rows.Count(r => r.Record.Edd.HasValue && r.Record.Edd.Value >= today && r.Record.Edd.Value <= weekEnd)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Book(int? patientId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var vm = new AncBookViewModel();
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
    public async Task<IActionResult> Book(AncBookViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        if (m.PatientId is null) ModelState.AddModelError(nameof(m.PatientId), "Pick a patient.");
        if (!ModelState.IsValid) return View(m);

        var record = new AnteNatalRecord
        {
            FacilityId = ctx.Value.facilityId,
            PatientId = m.PatientId!.Value,
            Lmp = m.Lmp,
            Edd = m.Edd ?? _maternity.CalculateEdd(m.Lmp),
            Gravida = m.Gravida,
            Para = m.Para,
            Abortions = m.Abortions,
            LivingChildren = m.LivingChildren,
            BloodGroup = m.BloodGroup,
            RhesusPositive = m.RhesusPositive,
            HeightCm = m.HeightCm,
            BookingWeightKg = m.BookingWeightKg,
            HaemoglobinGdl = m.HaemoglobinGdl,
            HivStatus = m.HivStatus,
            VdrlReactive = m.VdrlReactive,
            HepBPositive = m.HepBPositive,
            SicklingPositive = m.SicklingPositive,
            RiskFactors = m.RiskFactors,
            PreviousObstetricHistory = m.PreviousObstetricHistory,
            MedicalHistory = m.MedicalHistory,
            CreatedById = ctx.Value.userId
        };
        var id = await _maternity.CreateAnteNatalRecordAsync(record);
        TempData["Success"] = $"ANC booked · {record.AncNumber}";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var record = await _db.AnteNatalRecords
            .Include(r => r.Patient)
            .Include(r => r.Visits).ThenInclude(v => v.RecordedBy)
            .Include(r => r.Deliveries).ThenInclude(d => d.Newborns)
            .Include(r => r.PostnatalVisits).ThenInclude(v => v.RecordedBy)
            .FirstOrDefaultAsync(r => r.Id == id && r.FacilityId == ctx.Value.facilityId);
        if (record is null) return NotFound();
        return View(record);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddVisit(AncVisitInputViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var record = await _db.AnteNatalRecords.FirstOrDefaultAsync(r => r.Id == m.AnteNatalRecordId && r.FacilityId == ctx.Value.facilityId);
        if (record is null) return NotFound();

        var visitNumber = await _db.AnteNatalVisits.CountAsync(v => v.AnteNatalRecordId == m.AnteNatalRecordId) + 1;
        _db.AnteNatalVisits.Add(new AnteNatalVisit
        {
            AnteNatalRecordId = m.AnteNatalRecordId,
            VisitDate = m.VisitDate,
            VisitNumber = visitNumber,
            GestationalAgeWeeks = m.GestationalAgeWeeks ?? _maternity.CalculateGestationalAgeWeeks(record.Lmp, m.VisitDate),
            WeightKg = m.WeightKg,
            SystolicBp = m.SystolicBp,
            DiastolicBp = m.DiastolicBp,
            FundalHeightCm = m.FundalHeightCm,
            FetalHeartRate = m.FetalHeartRate,
            Presentation = m.Presentation,
            UrineProtein = m.UrineProtein,
            UrineSugar = m.UrineSugar,
            Oedema = m.Oedema,
            FetalMovements = m.FetalMovements,
            Complaints = m.Complaints,
            Plan = m.Plan,
            RecordedById = ctx.Value.userId
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Visit #{visitNumber} recorded.";
        return RedirectToAction(nameof(Detail), new { id = m.AnteNatalRecordId });
    }

    [HttpGet]
    public async Task<IActionResult> Deliver(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var record = await _db.AnteNatalRecords
            .Include(r => r.Patient)
            .FirstOrDefaultAsync(r => r.Id == id && r.FacilityId == ctx.Value.facilityId);
        if (record is null) return NotFound();
        await PopulateAccoucheurs(ctx.Value.facilityId);
        ViewBag.Record = record;
        return View(new DeliveryInputViewModel
        {
            AnteNatalRecordId = id,
            AccoucheurId = ctx.Value.userId,
            GestationAtDeliveryWeeks = _maternity.CalculateGestationalAgeWeeks(record.Lmp, DateOnly.FromDateTime(DateTime.UtcNow)) ?? 40
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Deliver(DeliveryInputViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var record = await _db.AnteNatalRecords.FirstOrDefaultAsync(r => r.Id == m.AnteNatalRecordId && r.FacilityId == ctx.Value.facilityId);
        if (record is null) return NotFound();

        if (m.DeliveryUtc < m.LabourOnsetUtc) ModelState.AddModelError(nameof(m.DeliveryUtc), "Delivery must be after labour onset.");
        if (!ModelState.IsValid)
        {
            await PopulateAccoucheurs(ctx.Value.facilityId);
            ViewBag.Record = record;
            return View(m);
        }

        var delivery = new Delivery
        {
            FacilityId = ctx.Value.facilityId,
            AnteNatalRecordId = record.Id,
            PatientId = record.PatientId,
            LabourOnsetUtc = m.LabourOnsetUtc,
            DeliveryUtc = m.DeliveryUtc,
            LabourMinutes = (int)(m.DeliveryUtc - m.LabourOnsetUtc).TotalMinutes,
            Mode = m.Mode,
            Outcome = m.Outcome,
            GestationAtDeliveryWeeks = m.GestationAtDeliveryWeeks,
            EpisiotomyPerformed = m.EpisiotomyPerformed,
            PerinealTear = m.PerinealTear,
            EstimatedBloodLossMl = m.EstimatedBloodLossMl,
            Complications = m.Complications,
            Notes = m.Notes,
            AccoucheurId = m.AccoucheurId
        };
        _db.Deliveries.Add(delivery);
        await _db.SaveChangesAsync();

        _db.Newborns.Add(new Newborn
        {
            DeliveryId = delivery.Id,
            Sex = m.BabySex,
            BirthWeightG = m.BirthWeightG,
            LengthCm = m.LengthCm,
            HeadCircumferenceCm = m.HeadCircumferenceCm,
            Apgar1Min = m.Apgar1Min,
            Apgar5Min = m.Apgar5Min,
            ResuscitationRequired = m.ResuscitationRequired,
            BreastfedWithin1Hr = m.BreastfedWithin1Hr,
            VitaminKGiven = m.VitaminKGiven,
            BcgGivenAtBirth = m.BcgGivenAtBirth,
            OpvGivenAtBirth = m.OpvGivenAtBirth,
            HepBGivenAtBirth = m.HepBGivenAtBirth
        });
        record.Status = AnteNatalStatus.Delivered;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Delivery recorded.";
        return RedirectToAction(nameof(Detail), new { id = record.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddPostnatal(PostnatalInputViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var record = await _db.AnteNatalRecords.FirstOrDefaultAsync(r => r.Id == m.AnteNatalRecordId && r.FacilityId == ctx.Value.facilityId);
        if (record is null) return NotFound();

        _db.PostnatalVisits.Add(new PostnatalVisit
        {
            AnteNatalRecordId = record.Id,
            VisitDate = m.VisitDate,
            Day = m.Day,
            MotherSystolicBp = m.MotherSystolicBp,
            MotherDiastolicBp = m.MotherDiastolicBp,
            MotherTemperatureC = m.MotherTemperatureC,
            Lochia = m.Lochia,
            FundalInvolution = m.FundalInvolution,
            BabyWeightKg = m.BabyWeightKg,
            BabyJaundice = m.BabyJaundice,
            BabyBreastfeeding = m.BabyBreastfeeding,
            CordHealthy = m.CordHealthy,
            Notes = m.Notes,
            RecordedById = ctx.Value.userId
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Postnatal visit recorded.";
        return RedirectToAction(nameof(Detail), new { id = m.AnteNatalRecordId });
    }

    private async Task PopulateAccoucheurs(int facilityId)
    {
        var clinicalRoles = new[] { Roles.Doctor, Roles.Consultant, Roles.MedicalOfficer, Roles.Midwife, Roles.Nurse, Roles.ChiefNursingOfficer };
        var roleIds = await _db.Roles.Where(r => clinicalRoles.Contains(r.Name!)).Select(r => r.Id).ToListAsync();
        var staffIds = await _db.UserRoles.Where(ur => roleIds.Contains(ur.RoleId)).Select(ur => ur.UserId).Distinct().ToListAsync();
        var staff = await _db.Users.Where(u => staffIds.Contains(u.Id) && u.FacilityId == facilityId && u.IsActive)
            .Select(u => new { u.Id, Display = u.FirstName + " " + u.LastName + (u.Designation != null ? " · " + u.Designation : "") })
            .ToListAsync();
        ViewBag.Accoucheurs = new SelectList(staff, "Id", "Display");
    }

    [HttpPost, HasPermission(Permissions.AiAncRisk)]
    public async Task<IActionResult> AssessAncRisk(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        var anc = await _db.AnteNatalRecords.AsNoTracking()
            .Include(a => a.Patient)
            .Include(a => a.Visits)
            .FirstOrDefaultAsync(a => a.Id == id && a.FacilityId == ctx.Value.facilityId);
        if (anc is null) return NotFound();

        var ageSex = anc.Patient!.DateOfBirth.HasValue
            ? $"{(int)((DateTime.UtcNow - anc.Patient.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)).TotalDays / 365)}y female"
            : "female";

        var latest = anc.Visits.OrderByDescending(v => v.VisitDate).FirstOrDefault();
        string? vitals = null;
        int? gestWeeks = null;
        if (latest != null)
        {
            var parts = new List<string>();
            if (latest.SystolicBp.HasValue || latest.DiastolicBp.HasValue) parts.Add($"BP {latest.SystolicBp}/{latest.DiastolicBp}");
            if (latest.WeightKg.HasValue) parts.Add($"weight {latest.WeightKg} kg");
            if (latest.FundalHeightCm.HasValue) parts.Add($"fundal height {latest.FundalHeightCm} cm");
            if (latest.FetalHeartRate.HasValue) parts.Add($"FHR {latest.FetalHeartRate}");
            if (latest.UrineProtein == true) parts.Add("urine protein +ve");
            if (latest.Oedema == true) parts.Add("oedema +ve");
            vitals = parts.Count == 0 ? null : string.Join(", ", parts);
            gestWeeks = latest.GestationalAgeWeeks;
        }

        decimal? bmi = null;
        if (anc.HeightCm.HasValue && anc.HeightCm > 0 && anc.BookingWeightKg.HasValue)
        {
            var hM = (decimal)anc.HeightCm.Value / 100m;
            bmi = anc.BookingWeightKg.Value / (hM * hM);
        }

        var input = new AncRiskInput(
            ctx.Value.facilityId, anc.Id, ageSex,
            anc.Gravida, anc.Para, gestWeeks,
            anc.PreviousObstetricHistory,
            anc.MedicalHistory,
            vitals, bmi,
            anc.HivStatus.ToString(),
            anc.RiskFactors);
        var outcome = await _ai.AssessAncRiskAsync(input, ctx.Value.userId);
        return Json(new { ok = outcome.Ok, text = outcome.Text, error = outcome.Error });
    }
}
