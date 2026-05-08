using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Scheduling;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.QueueRead)]
public class QueueController : Controller
{
    private const string CanCheckIn = Roles.Receptionist + "," + Roles.RecordsOfficer + "," + Roles.TriageClerk + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private const string CanTriage = Roles.Nurse + "," + Roles.Midwife + "," + Roles.ChiefNursingOfficer + "," +
        Roles.TriageClerk + "," + Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private const string CanConsult = Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IQueueService _queue;

    public QueueController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IQueueService queue)
    {
        _db = db;
        _userManager = userManager;
        _queue = queue;
    }

    private async Task<int?> FacilityIdAsync()
    {
        var u = await _userManager.GetUserAsync(User);
        return u?.FacilityId;
    }

    [HttpGet]
    public async Task<IActionResult> Board(int clinicId)
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return NotFound();

        var clinic = await _db.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId && c.FacilityId == fid);
        if (clinic is null) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var entries = await _db.QueueEntries
            .Include(e => e.Patient)
            .Include(e => e.Clinician)
            .Where(e => e.FacilityId == fid && e.ClinicId == clinicId && e.TicketDate == today
                && e.Status != QueueStatus.Completed
                && e.Status != QueueStatus.Skipped
                && e.Status != QueueStatus.LeftWithoutBeingSeen)
            .OrderByDescending(e => e.Priority)
            .ThenBy(e => e.CheckedInAt)
            .ToListAsync();

        ViewBag.FacilityId = fid.Value;
        return View(new QueueBoardViewModel { Clinic = clinic, Entries = entries });
    }

    [HttpGet]
    public async Task<IActionResult> Display(int clinicId)
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return NotFound();

        var clinic = await _db.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId && c.FacilityId == fid);
        if (clinic is null) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var entries = await _db.QueueEntries
            .Include(e => e.Patient)
            .Where(e => e.FacilityId == fid && e.ClinicId == clinicId && e.TicketDate == today
                && (e.Status == QueueStatus.Waiting || e.Status == QueueStatus.Triaged
                    || e.Status == QueueStatus.Called || e.Status == QueueStatus.InConsultation))
            .OrderByDescending(e => e.Priority)
            .ThenBy(e => e.CheckedInAt)
            .ToListAsync();

        ViewBag.FacilityId = fid.Value;
        return View(new QueueBoardViewModel { Clinic = clinic, Entries = entries });
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return NotFound();
        var clinics = await _db.Clinics.AsNoTracking()
            .Where(c => c.FacilityId == fid && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
        return View(clinics);
    }

    [HttpGet, HasPermission(Permissions.QueueCheckIn)]
    public async Task<IActionResult> CheckIn(int? patientId, int? appointmentId)
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return NotFound();

        var vm = new CheckInViewModel();
        if (appointmentId.HasValue)
        {
            var a = await _db.Appointments
                .Include(x => x.Patient)
                .FirstOrDefaultAsync(x => x.Id == appointmentId.Value && x.FacilityId == fid);
            if (a is not null)
            {
                vm.AppointmentId = a.Id;
                vm.PatientId = a.PatientId;
                vm.PatientLabel = $"{a.Patient!.FullName} · {a.Patient.HospitalNumber}";
                vm.ClinicId = a.ClinicId;
                vm.ClinicianId = a.ClinicianId;
                vm.Priority = a.Priority;
            }
        }
        else if (patientId.HasValue)
        {
            var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == patientId && x.FacilityId == fid);
            if (p is not null)
            {
                vm.PatientId = p.Id;
                vm.PatientLabel = $"{p.FullName} · {p.HospitalNumber}";
            }
        }

        ViewBag.Clinics = await _db.Clinics.AsNoTracking()
            .Where(c => c.FacilityId == fid && c.IsActive).OrderBy(c => c.Name).ToListAsync();
        return View(vm);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.QueueCheckIn)]
    public async Task<IActionResult> CheckIn(CheckInViewModel model)
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return NotFound();

        if (model.PatientId is null) ModelState.AddModelError(nameof(model.PatientId), "Pick a patient.");
        if (model.ClinicId == 0) ModelState.AddModelError(nameof(model.ClinicId), "Pick a clinic.");

        if (!ModelState.IsValid)
        {
            ViewBag.Clinics = await _db.Clinics.AsNoTracking()
                .Where(c => c.FacilityId == fid && c.IsActive).OrderBy(c => c.Name).ToListAsync();
            return View(model);
        }

        var u = await _userManager.GetUserAsync(User);
        var entry = await _queue.CheckInAsync(
            fid.Value, model.PatientId!.Value, model.ClinicId, model.ClinicianId,
            model.Priority, model.Complaint, model.AppointmentId, u?.Id);

        TempData["Success"] = $"Checked in. Ticket {entry.TicketNumber}.";
        return RedirectToAction(nameof(Board), new { clinicId = model.ClinicId });
    }

    [HttpGet, HasPermission(Permissions.TriageCreate)]
    public async Task<IActionResult> Triage(int id)
    {
        var fid = await FacilityIdAsync();
        var entry = await _db.QueueEntries
            .Include(e => e.Patient)
            .Include(e => e.Clinic)
            .FirstOrDefaultAsync(e => e.Id == id && e.FacilityId == fid);
        if (entry is null) return NotFound();

        ViewBag.Entry = entry;
        return View(new TriageViewModel { QueueEntryId = id, Priority = entry.Priority });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.TriageCreate)]
    public async Task<IActionResult> Triage(TriageViewModel model)
    {
        var fid = await FacilityIdAsync();
        var entry = await _db.QueueEntries.FirstOrDefaultAsync(e => e.Id == model.QueueEntryId && e.FacilityId == fid);
        if (entry is null) return NotFound();

        var u = await _userManager.GetUserAsync(User);
        await _queue.TriageAsync(model.QueueEntryId, model.Priority, model.Mews, model.Notes, u?.Id);
        TempData["Success"] = "Triaged.";
        return RedirectToAction(nameof(Board), new { clinicId = entry.ClinicId });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.QueueServe)]
    public async Task<IActionResult> Call(int id)
    {
        var fid = await FacilityIdAsync();
        var entry = await _db.QueueEntries.FirstOrDefaultAsync(e => e.Id == id && e.FacilityId == fid);
        if (entry is null) return NotFound();
        var u = await _userManager.GetUserAsync(User);
        await _queue.CallAsync(id, u?.Id);
        return RedirectToAction(nameof(Board), new { clinicId = entry.ClinicId });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.QueueServe)]
    public async Task<IActionResult> Start(int id)
    {
        var fid = await FacilityIdAsync();
        var entry = await _db.QueueEntries.FirstOrDefaultAsync(e => e.Id == id && e.FacilityId == fid);
        if (entry is null) return NotFound();
        return RedirectToAction("OpenFromQueue", "Encounters", new { queueEntryId = id });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.QueueServe)]
    public async Task<IActionResult> Complete(int id)
    {
        var fid = await FacilityIdAsync();
        var entry = await _db.QueueEntries.FirstOrDefaultAsync(e => e.Id == id && e.FacilityId == fid);
        if (entry is null) return NotFound();
        var u = await _userManager.GetUserAsync(User);
        await _queue.CompleteAsync(id, u?.Id);
        TempData["Success"] = "Marked complete.";
        return RedirectToAction(nameof(Board), new { clinicId = entry.ClinicId });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.QueueCheckIn)]
    public async Task<IActionResult> Skip(int id)
    {
        var fid = await FacilityIdAsync();
        var entry = await _db.QueueEntries.FirstOrDefaultAsync(e => e.Id == id && e.FacilityId == fid);
        if (entry is null) return NotFound();
        var u = await _userManager.GetUserAsync(User);
        await _queue.SkipAsync(id, u?.Id);
        return RedirectToAction(nameof(Board), new { clinicId = entry.ClinicId });
    }

    [HttpGet, HasPermission(Permissions.QueueServe)]
    public async Task<IActionResult> MyQueue(int? clinicId)
    {
        var fid = await FacilityIdAsync();
        var u = await _userManager.GetUserAsync(User);
        if (fid is null || u is null) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var query = _db.QueueEntries
            .Include(e => e.Patient)
            .Include(e => e.Clinic)
            .Where(e => e.FacilityId == fid && e.TicketDate == today
                && (e.ClinicianId == u.Id || e.ClinicianId == null)
                && e.Status != QueueStatus.Completed
                && e.Status != QueueStatus.Skipped);

        if (clinicId.HasValue)
            query = query.Where(e => e.ClinicId == clinicId.Value);

        var entries = await query
            .OrderByDescending(e => e.Priority)
            .ThenBy(e => e.CheckedInAt)
            .ToListAsync();

        var clinics = await _db.Clinics.AsNoTracking()
            .Where(c => c.FacilityId == fid && c.IsActive)
            .OrderBy(c => c.Name).ToListAsync();

        return View(new MyQueueViewModel { Entries = entries, Clinics = clinics, FilterClinicId = clinicId });
    }
}
