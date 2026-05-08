using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Allied;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.AlliedSession)]
public class AlliedController : Controller
{
    public const string AlliedStaff = Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.Nurse + "," + Roles.Physiotherapist + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBatch13Numbering _numbering;

    public AlliedController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IBatch13Numbering numbering)
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
    public async Task<IActionResult> Index(AlliedServiceLine line = AlliedServiceLine.Dental)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var sessions = await _db.AlliedSessions.AsNoTracking()
            .Include(s => s.Patient)
            .Include(s => s.Provider)
            .Where(s => s.FacilityId == ctx.Value.facilityId && s.ServiceLine == line)
            .OrderByDescending(s => s.ScheduledUtc).Take(200).ToListAsync();
        var todayUtc = DateTime.UtcNow.Date;
        var rows = sessions.Select(s => new AlliedListRow { Session = s, Patient = s.Patient! }).ToList();
        return View(new AlliedListViewModel
        {
            Rows = rows,
            ServiceLine = line,
            ScheduledCount = rows.Count(r => r.Session.Status == SessionStatus.Scheduled),
            InProgressCount = rows.Count(r => r.Session.Status == SessionStatus.InProgress),
            CompletedTodayCount = rows.Count(r => r.Session.Status == SessionStatus.Completed && r.Session.CompletedUtc.HasValue && r.Session.CompletedUtc.Value >= todayUtc)
        });
    }

    [HttpGet]
    public async Task<IActionResult> New(AlliedServiceLine line, int? patientId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        await PopulateProviders(ctx.Value.facilityId);
        var vm = new AlliedSessionInputViewModel { ServiceLine = line };
        if (patientId.HasValue)
        {
            var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == patientId && x.FacilityId == ctx.Value.facilityId);
            if (p != null) { vm.PatientId = p.Id; vm.PatientLabel = $"{p.FullName} · {p.HospitalNumber}"; }
        }
        return View("Form", vm);
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var s = await _db.AlliedSessions.Include(x => x.Patient).FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (s is null) return NotFound();
        await PopulateProviders(ctx.Value.facilityId);
        return View("Form", new AlliedSessionInputViewModel
        {
            Id = s.Id,
            ServiceLine = s.ServiceLine,
            PatientId = s.PatientId,
            PatientLabel = $"{s.Patient!.FullName} · {s.Patient.HospitalNumber}",
            ScheduledUtc = s.ScheduledUtc,
            ChiefComplaint = s.ChiefComplaint,
            Examination = s.Examination,
            Assessment = s.Assessment,
            TreatmentGiven = s.TreatmentGiven,
            Plan = s.Plan,
            Modality = s.Modality,
            ToothChart = s.ToothChart,
            DentalProcedureCode = s.DentalProcedureCode,
            RightEyeAcuity = s.RightEyeAcuity,
            LeftEyeAcuity = s.LeftEyeAcuity,
            RightEyeRefraction = s.RightEyeRefraction,
            LeftEyeRefraction = s.LeftEyeRefraction,
            SessionsCompleted = s.SessionsCompleted,
            SessionsPlanned = s.SessionsPlanned,
            PhysioModalitiesUsed = s.PhysioModalitiesUsed,
            ProviderId = s.ProviderId
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(AlliedSessionInputViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        if (m.PatientId is null && m.Id is null) ModelState.AddModelError(nameof(m.PatientId), "Pick a patient.");
        if (!ModelState.IsValid) { await PopulateProviders(ctx.Value.facilityId); return View("Form", m); }

        AlliedSession s;
        if (m.Id.HasValue)
        {
            s = await _db.AlliedSessions.FirstOrDefaultAsync(x => x.Id == m.Id && x.FacilityId == ctx.Value.facilityId)
                ?? throw new InvalidOperationException("Session not found");
        }
        else
        {
            var prefix = m.ServiceLine switch
            {
                AlliedServiceLine.Dental => "DEN",
                AlliedServiceLine.Physiotherapy => "PHY",
                AlliedServiceLine.Optometry => "OPT",
                _ => "ALD"
            };
            s = new AlliedSession
            {
                FacilityId = ctx.Value.facilityId,
                PatientId = m.PatientId!.Value,
                ServiceLine = m.ServiceLine,
                SessionNumber = await _numbering.NextAlliedAsync(ctx.Value.facilityId, prefix),
                Status = SessionStatus.Scheduled
            };
            _db.AlliedSessions.Add(s);
        }
        s.ScheduledUtc = m.ScheduledUtc;
        s.ChiefComplaint = m.ChiefComplaint;
        s.Examination = m.Examination;
        s.Assessment = m.Assessment;
        s.TreatmentGiven = m.TreatmentGiven;
        s.Plan = m.Plan;
        s.Modality = m.Modality;
        s.ToothChart = m.ToothChart;
        s.DentalProcedureCode = m.DentalProcedureCode;
        s.RightEyeAcuity = m.RightEyeAcuity;
        s.LeftEyeAcuity = m.LeftEyeAcuity;
        s.RightEyeRefraction = m.RightEyeRefraction;
        s.LeftEyeRefraction = m.LeftEyeRefraction;
        s.SessionsCompleted = m.SessionsCompleted;
        s.SessionsPlanned = m.SessionsPlanned;
        s.PhysioModalitiesUsed = m.PhysioModalitiesUsed;
        s.ProviderId = m.ProviderId ?? ctx.Value.userId;

        await _db.SaveChangesAsync();
        TempData["Success"] = $"Session saved · {s.SessionNumber}";
        return RedirectToAction(nameof(Index), new { line = s.ServiceLine });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Complete(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var s = await _db.AlliedSessions.FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (s is null) return NotFound();
        s.Status = SessionStatus.Completed;
        s.CompletedUtc = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Session completed.";
        return RedirectToAction(nameof(Index), new { line = s.ServiceLine });
    }

    private async Task PopulateProviders(int facilityId)
    {
        var clinical = new[] { Roles.Doctor, Roles.Consultant, Roles.MedicalOfficer, Roles.Physiotherapist, Roles.Nurse };
        var roleIds = await _db.Roles.Where(r => clinical.Contains(r.Name!)).Select(r => r.Id).ToListAsync();
        var staffIds = await _db.UserRoles.Where(ur => roleIds.Contains(ur.RoleId)).Select(ur => ur.UserId).Distinct().ToListAsync();
        var providers = await _db.Users.Where(u => staffIds.Contains(u.Id) && u.FacilityId == facilityId && u.IsActive)
            .Select(u => new { u.Id, Display = u.FirstName + " " + u.LastName + (u.Designation != null ? " · " + u.Designation : "") })
            .ToListAsync();
        ViewBag.Providers = new SelectList(providers, "Id", "Display");
    }
}
