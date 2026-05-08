using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Theatre;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.TheatreSchedule)]
public class TheatreController : Controller
{
    public const string TheatreStaff = Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.Nurse + "," + Roles.Midwife + "," + Roles.ChiefNursingOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITheatreService _theatre;

    public TheatreController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ITheatreService theatre)
    {
        _db = db; _userManager = userManager; _theatre = theatre;
    }

    private async Task<(int facilityId, string userId)?> Ctx()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u?.FacilityId is null) return null;
        return (u.FacilityId.Value, u.Id);
    }

    [HttpGet]
    public async Task<IActionResult> Schedule(DateOnly? date)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var d = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dayStart = d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var sessions = await _db.TheatreSessions.AsNoTracking()
            .Include(s => s.Patient)
            .Include(s => s.Theatre)
            .Include(s => s.LeadSurgeon)
            .Where(s => s.FacilityId == ctx.Value.facilityId
                     && s.ScheduledStartUtc >= dayStart && s.ScheduledStartUtc < dayEnd)
            .OrderBy(s => s.ScheduledStartUtc).ToListAsync();

        var theatres = await _db.Theatres.AsNoTracking()
            .Where(t => t.FacilityId == ctx.Value.facilityId && t.IsActive)
            .OrderBy(t => t.Code).ToListAsync();

        return View(new TheatreScheduleViewModel
        {
            Date = d,
            Theatres = theatres,
            Sessions = sessions.Select(s => new TheatreScheduleRow { Session = s, Patient = s.Patient! }).ToList(),
            ScheduledCount = sessions.Count(s => s.Status == TheatreSessionStatus.Scheduled),
            InProgressCount = sessions.Count(s => s.Status is TheatreSessionStatus.PreOp or TheatreSessionStatus.InTheatre or TheatreSessionStatus.Recovery),
            CompletedCount = sessions.Count(s => s.Status == TheatreSessionStatus.Completed)
        });
    }

    [HttpGet]
    public async Task<IActionResult> Book(int? patientId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        await PopulateLists(ctx.Value.facilityId);
        var vm = new TheatreBookViewModel { LeadSurgeonId = ctx.Value.userId };
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
    public async Task<IActionResult> Book(TheatreBookViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        if (m.PatientId is null) ModelState.AddModelError(nameof(m.PatientId), "Pick a patient.");
        if (m.TheatreId is null) ModelState.AddModelError(nameof(m.TheatreId), "Pick a theatre.");
        if (string.IsNullOrEmpty(m.LeadSurgeonId)) ModelState.AddModelError(nameof(m.LeadSurgeonId), "Pick a lead surgeon.");

        if (!ModelState.IsValid)
        {
            await PopulateLists(ctx.Value.facilityId);
            return View(m);
        }

        var id = await _theatre.CreateSessionAsync(
            ctx.Value.facilityId, m.TheatreId!.Value, m.PatientId!.Value, m.LeadSurgeonId!,
            m.AnaesthetistId, m.ScrubNurseId, m.ProcedureName, m.CptCode, m.Indication,
            m.Urgency, m.Anaesthesia, m.ScheduledStartUtc, m.EstimatedMinutes,
            ctx.Value.userId);
        TempData["Success"] = "Theatre session booked. WHO checklist pre-loaded.";
        return RedirectToAction(nameof(Session), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Session(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var s = await _db.TheatreSessions
            .Include(x => x.Patient)!.ThenInclude(p => p!.Allergies)
            .Include(x => x.Theatre)
            .Include(x => x.LeadSurgeon)
            .Include(x => x.Anaesthetist)
            .Include(x => x.ScrubNurse)
            .Include(x => x.Checklist).ThenInclude(c => c.ConfirmedBy)
            .Include(x => x.Events).ThenInclude(e => e.RecordedBy)
            .FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (s is null) return NotFound();
        return View(s);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmCheck(int itemId, int sessionId, bool confirmed, string? notes)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        await _theatre.ConfirmChecklistItemAsync(itemId, confirmed, notes, ctx.Value.userId);
        return RedirectToAction(nameof(Session), new { id = sessionId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Advance(int id, TheatreSessionStatus to)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var s = await _db.TheatreSessions
            .Include(x => x.Checklist)
            .FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (s is null) return NotFound();

        if (to == TheatreSessionStatus.InTheatre)
        {
            var unfinishedSignIn = s.Checklist.Any(c => c.Phase == ChecklistPhase.SignIn && !c.IsConfirmed);
            var unfinishedTimeOut = s.Checklist.Any(c => c.Phase == ChecklistPhase.TimeOut && !c.IsConfirmed);
            if (unfinishedSignIn || unfinishedTimeOut)
            {
                TempData["Error"] = "Sign In + Time Out checklists must be complete before knife on skin.";
                return RedirectToAction(nameof(Session), new { id });
            }
        }
        if (to == TheatreSessionStatus.Completed)
        {
            var unfinishedSignOut = s.Checklist.Any(c => c.Phase == ChecklistPhase.SignOut && !c.IsConfirmed);
            if (unfinishedSignOut)
            {
                TempData["Error"] = "Sign Out checklist must be complete before completing session.";
                return RedirectToAction(nameof(Session), new { id });
            }
        }
        await _theatre.AdvanceStatusAsync(id, to, ctx.Value.userId);
        return RedirectToAction(nameof(Session), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddEvent(SessionEventInputViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        await _theatre.LogEventAsync(m.TheatreSessionId, m.Kind, m.Description, m.Details, ctx.Value.userId);
        return RedirectToAction(nameof(Session), new { id = m.TheatreSessionId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Finalise(TheatreFinaliseViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var s = await _db.TheatreSessions.FirstOrDefaultAsync(x => x.Id == m.Id && x.FacilityId == ctx.Value.facilityId);
        if (s is null) return NotFound();

        s.OperativeNote = m.OperativeNote;
        s.PostOpInstructions = m.PostOpInstructions;
        s.EstimatedBloodLossMl = m.EstimatedBloodLossMl;
        s.CrystalloidGivenMl = m.CrystalloidGivenMl;
        s.Complications = m.Complications;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Operative record saved.";
        return RedirectToAction(nameof(Session), new { id = m.Id });
    }

    private async Task PopulateLists(int facilityId)
    {
        ViewBag.Theatres = await _db.Theatres.AsNoTracking()
            .Where(t => t.FacilityId == facilityId && t.IsActive)
            .OrderBy(t => t.Code).ToListAsync();

        var clinicalRoles = new[] { Roles.Doctor, Roles.Consultant, Roles.MedicalOfficer };
        var roleIds = await _db.Roles.Where(r => clinicalRoles.Contains(r.Name!)).Select(r => r.Id).ToListAsync();
        var docIds = await _db.UserRoles.Where(ur => roleIds.Contains(ur.RoleId)).Select(ur => ur.UserId).Distinct().ToListAsync();
        var doctors = await _db.Users.Where(u => docIds.Contains(u.Id) && u.FacilityId == facilityId && u.IsActive)
            .Select(u => new { u.Id, Display = u.FirstName + " " + u.LastName + (u.Designation != null ? " · " + u.Designation : "") })
            .ToListAsync();
        ViewBag.Doctors = new SelectList(doctors, "Id", "Display");

        var nurseRoles = new[] { Roles.Nurse, Roles.Midwife, Roles.ChiefNursingOfficer };
        var nurseRoleIds = await _db.Roles.Where(r => nurseRoles.Contains(r.Name!)).Select(r => r.Id).ToListAsync();
        var nurseIds = await _db.UserRoles.Where(ur => nurseRoleIds.Contains(ur.RoleId)).Select(ur => ur.UserId).Distinct().ToListAsync();
        var nurses = await _db.Users.Where(u => nurseIds.Contains(u.Id) && u.FacilityId == facilityId && u.IsActive)
            .Select(u => new { u.Id, Display = u.FirstName + " " + u.LastName })
            .ToListAsync();
        ViewBag.Nurses = new SelectList(nurses, "Id", "Display");
    }
}
