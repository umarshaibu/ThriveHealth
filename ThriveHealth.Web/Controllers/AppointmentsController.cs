using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Services.Ai;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Scheduling;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.AppointmentsRead)]
public class AppointmentsController : Controller
{
    private const string CanBook = Roles.Receptionist + "," + Roles.RecordsOfficer + "," + Roles.TriageClerk + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector + "," +
        Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.Nurse + "," + Roles.Midwife + "," + Roles.ChiefNursingOfficer;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISlotService _slots;
    private readonly IClinicalAiService _ai;

    public AppointmentsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, ISlotService slots, IClinicalAiService ai)
    {
        _db = db;
        _userManager = userManager;
        _slots = slots;
        _ai = ai;
    }

    private async Task<int?> FacilityIdAsync()
    {
        var u = await _userManager.GetUserAsync(User);
        return u?.FacilityId;
    }

    [HttpGet]
    public async Task<IActionResult> Index(DateOnly? date)
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return NotFound();

        var d = date ?? DateOnly.FromDateTime(DateTime.UtcNow);
        var dayStart = d.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEnd = dayStart.AddDays(1);

        var rows = await _db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Clinic)
            .Include(a => a.Clinician)
            .Where(a => a.FacilityId == fid
                && a.ScheduledStartUtc >= dayStart && a.ScheduledStartUtc < dayEnd)
            .OrderBy(a => a.ScheduledStartUtc)
            .ToListAsync();

        return View(new AppointmentListViewModel { Date = d, Appointments = rows });
    }

    [HttpGet, HasPermission(Permissions.AppointmentsBook)]
    public async Task<IActionResult> Book(int? patientId)
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return NotFound();

        var clinics = await _db.Clinics.AsNoTracking()
            .Where(c => c.FacilityId == fid && c.IsActive)
            .OrderBy(c => c.Name)
            .ToListAsync();
        ViewBag.Clinics = clinics;

        var vm = new AppointmentBookViewModel { Date = DateOnly.FromDateTime(DateTime.UtcNow) };
        if (patientId.HasValue)
        {
            var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == patientId && x.FacilityId == fid);
            if (p is not null)
            {
                vm.PatientId = p.Id;
                vm.PatientLabel = $"{p.FullName} · {p.HospitalNumber}";
            }
        }
        return View(vm);
    }

    [HttpGet, HasPermission(Permissions.AppointmentsBook)]
    public async Task<IActionResult> GetSlots(int clinicId, DateOnly date)
    {
        var slots = await _slots.GetAvailableSlotsAsync(clinicId, date);
        return Json(slots.Select(s => new
        {
            startUtc = s.StartUtc,
            startLocal = s.StartUtc.ToLocalTime().ToString("HH:mm"),
            duration = s.DurationMinutes,
            clinicianId = s.ClinicianId,
            clinicianName = s.ClinicianName,
            roomId = s.RoomId
        }));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AppointmentsBook)]
    public async Task<IActionResult> Book(AppointmentBookViewModel model)
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return NotFound();

        if (model.PatientId is null) ModelState.AddModelError(nameof(model.PatientId), "Choose a patient.");
        if (model.ClinicId is null) ModelState.AddModelError(nameof(model.ClinicId), "Choose a clinic.");
        if (model.ScheduledStartUtc is null) ModelState.AddModelError(nameof(model.ScheduledStartUtc), "Choose a slot.");
        if (string.IsNullOrEmpty(model.ClinicianId)) ModelState.AddModelError(nameof(model.ClinicianId), "Choose a clinician.");

        if (!ModelState.IsValid)
        {
            ViewBag.Clinics = await _db.Clinics.AsNoTracking()
                .Where(c => c.FacilityId == fid && c.IsActive).OrderBy(c => c.Name).ToListAsync();
            return View(model);
        }

        var u = await _userManager.GetUserAsync(User);
        var appt = new Appointment
        {
            FacilityId = fid.Value,
            PatientId = model.PatientId!.Value,
            ClinicId = model.ClinicId!.Value,
            ClinicianId = model.ClinicianId,
            RoomId = model.RoomId,
            Type = model.Type,
            Priority = model.Priority,
            Channel = BookingChannel.FrontDesk,
            ScheduledStartUtc = DateTime.SpecifyKind(model.ScheduledStartUtc!.Value, DateTimeKind.Utc),
            DurationMinutes = model.DurationMinutes,
            ReasonForVisit = model.ReasonForVisit,
            BookedById = u?.Id,
            Status = AppointmentStatus.Scheduled
        };
        _db.Appointments.Add(appt);
        await _db.SaveChangesAsync();

        TempData["Success"] = "Appointment booked.";
        return RedirectToAction(nameof(Details), new { id = appt.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var fid = await FacilityIdAsync();
        var appt = await _db.Appointments
            .Include(a => a.Patient)
            .Include(a => a.Clinic)
            .Include(a => a.Clinician)
            .Include(a => a.Room)
            .Include(a => a.BookedBy)
            .FirstOrDefaultAsync(a => a.Id == id && a.FacilityId == fid);
        if (appt is null) return NotFound();
        return View(appt);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AppointmentsBook)]
    public async Task<IActionResult> Cancel(int id, string reason)
    {
        var fid = await FacilityIdAsync();
        var appt = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == id && a.FacilityId == fid);
        if (appt is null) return NotFound();

        var u = await _userManager.GetUserAsync(User);
        appt.Status = AppointmentStatus.Cancelled;
        appt.CancelledAt = DateTime.UtcNow;
        appt.CancelReason = reason;
        appt.CancelledById = u?.Id;
        appt.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Appointment cancelled.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.AppointmentsBook)]
    public async Task<IActionResult> MarkNoShow(int id)
    {
        var fid = await FacilityIdAsync();
        var appt = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == id && a.FacilityId == fid);
        if (appt is null) return NotFound();
        appt.Status = AppointmentStatus.NoShow;
        appt.NoShowMarkedAt = DateTime.UtcNow;
        appt.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Marked as no-show.";
        return RedirectToAction(nameof(Details), new { id });
    }

    public class AiSchedulingDto { public string? Complaint { get; set; } public string? Urgency { get; set; } public int? PatientId { get; set; } }

    [HttpPost, HasPermission(Permissions.AiSchedulingAssist)]
    public async Task<IActionResult> AssistScheduling([FromBody] AiSchedulingDto dto)
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.Complaint))
            return Json(new { ok = false, error = "Enter a chief complaint first." });

        var clinics = await _db.Clinics.AsNoTracking()
            .Where(c => c.FacilityId == fid && c.IsActive)
            .Select(c => new { c.Id, c.Name, Specialty = c.Specialty.ToString() }).ToListAsync();

        var todayStart = DateTime.UtcNow.Date;
        var weekEnd = todayStart.AddDays(7);
        var booked = await _db.Appointments.AsNoTracking()
            .Where(a => a.FacilityId == fid && a.ScheduledStartUtc >= todayStart && a.ScheduledStartUtc < weekEnd && a.Status != AppointmentStatus.Cancelled)
            .GroupBy(a => new { a.ClinicId, IsToday = a.ScheduledStartUtc < todayStart.AddDays(1) })
            .Select(g => new { g.Key.ClinicId, g.Key.IsToday, Count = g.Count() })
            .ToListAsync();

        // crude open-slot estimate: assume 24 slots/day capacity per clinic minus booked
        var snapshots = clinics.Select(c =>
        {
            var todayBooked = booked.FirstOrDefault(b => b.ClinicId == c.Id && b.IsToday)?.Count ?? 0;
            var weekBooked = booked.Where(b => b.ClinicId == c.Id).Sum(b => b.Count);
            return new ClinicCapacitySnapshot(c.Id, c.Name, c.Specialty, Math.Max(0, 24 - todayBooked), Math.Max(0, 24 * 5 - weekBooked));
        }).ToList();

        string? ageSex = null;
        if (dto.PatientId.HasValue)
        {
            var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == dto.PatientId && x.FacilityId == fid);
            if (p != null)
                ageSex = p.DateOfBirth.HasValue ? $"{(int)((DateTime.UtcNow - p.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)).TotalDays / 365)}y {p.Sex}" : p.Sex.ToString();
        }

        var u = await _userManager.GetUserAsync(User);
        var input = new SchedulingAssistInput(fid.Value, dto.Complaint, ageSex, dto.Urgency, snapshots);
        var outcome = await _ai.SuggestSchedulingAsync(input, u?.Id);
        return Json(new { ok = outcome.Ok, text = outcome.Text, error = outcome.Error });
    }
}
