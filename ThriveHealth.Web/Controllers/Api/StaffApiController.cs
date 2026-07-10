using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Api;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;
using ThriveHealth.Web.Models.Scheduling;
using ThriveHealth.Web.Models.Telemedicine;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers.Api;

/// <summary>Clinician home: profile, dashboard, schedule, and the patient list/chart.</summary>
[Route("api/v1/staff")]
public class StaffApiController : StaffApiControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly ITeleChatService _chat;

    public StaffApiController(ApplicationDbContext db, UserManager<ApplicationUser> users, ITeleChatService chat)
    {
        _db = db; _users = users; _chat = chat;
    }

    private static ApiScheduleItem ToSchedule(Appointment a) => new(
        a.Id, a.ScheduledStartUtc, a.DurationMinutes, a.Status.ToString(), a.Type.ToString(),
        a.PatientId, a.Patient?.FullName ?? "", a.Patient?.HospitalNumber ?? "",
        a.Clinic?.Name, a.ReasonForVisit);

    private static ApiPatientListItem ToPatient(Patient p) => new(
        p.Id, p.HospitalNumber, p.FullName, p.Gender, p.DateOfBirth?.ToString("yyyy-MM-dd"), p.Phone);

    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiStaffProfile), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var user = await _users.FindByIdAsync(UserId);
        if (user is null) return NotFound(new ApiError("User not found.", "not_found"));
        var roles = await _users.GetRolesAsync(user);
        var facility = user.FacilityId is int fid && fid != 0
            ? await _db.Facilities.AsNoTracking().Where(f => f.Id == fid).Select(f => new { f.Name, f.TenantId }).FirstOrDefaultAsync(ct)
            : null;
        return Ok(new ApiStaffProfile(user.Id, user.FullName, user.Email, user.Designation, user.Department,
            user.FacilityId ?? 0, facility?.Name ?? "", user.TenantId ?? facility?.TenantId ?? 0,
            roles.ToList(), roles.Contains(Roles.Consultant)));
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiClinicianDashboard), StatusCodes.Status200OK)]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var me = UserId;
        var fid = FacilityId;
        var todayStart = DateTime.UtcNow.Date;
        var todayEnd = todayStart.AddDays(1);

        var todayCount = await _db.Appointments.CountAsync(a =>
            a.ClinicianId == me && a.ScheduledStartUtc >= todayStart && a.ScheduledStartUtc < todayEnd
            && a.Status != AppointmentStatus.Cancelled, ct);

        var upcoming = await _db.Appointments.CountAsync(a =>
            a.ClinicianId == me && a.ScheduledStartUtc >= todayEnd && a.Status == AppointmentStatus.Scheduled, ct);

        var pendingTele = await _db.TeleSessions.CountAsync(t =>
            t.FacilityId == fid && (t.Status == TeleSessionStatus.Requested
            || (t.ClinicianId == me && (t.Status == TeleSessionStatus.Scheduled || t.Status == TeleSessionStatus.PatientWaiting))), ct);

        var unread = await _chat.UnreadForClinicianAsync(fid, ct);

        var openEnc = await _db.Encounters.CountAsync(e =>
            e.ClinicianId == me && e.Status == EncounterStatus.InProgress, ct);

        var next = await _db.Appointments.AsNoTracking()
            .Include(a => a.Patient).Include(a => a.Clinic)
            .Where(a => a.ClinicianId == me && a.ScheduledStartUtc >= todayStart && a.Status == AppointmentStatus.Scheduled)
            .OrderBy(a => a.ScheduledStartUtc).Take(5).ToListAsync(ct);

        return Ok(new ApiClinicianDashboard(todayCount, upcoming, pendingTele, unread, openEnc,
            next.Select(ToSchedule).ToList()));
    }

    [HttpGet("schedule")]
    [ProducesResponseType(typeof(IReadOnlyList<ApiScheduleItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Schedule([FromQuery] string? date, CancellationToken ct)
    {
        var day = DateOnly.TryParse(date, out var d) ? d : DateOnly.FromDateTime(DateTime.UtcNow);
        var start = day.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var end = start.AddDays(1);
        var appts = await _db.Appointments.AsNoTracking()
            .Include(a => a.Patient).Include(a => a.Clinic)
            .Where(a => a.ClinicianId == UserId && a.ScheduledStartUtc >= start && a.ScheduledStartUtc < end)
            .OrderBy(a => a.ScheduledStartUtc).ToListAsync(ct);
        return Ok(appts.Select(ToSchedule).ToList());
    }

    [HttpGet("patients")]
    [ProducesResponseType(typeof(IReadOnlyList<ApiPatientListItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Patients([FromQuery] string? q, CancellationToken ct)
    {
        var fid = FacilityId;
        var query = _db.Patients.AsNoTracking().Where(p => p.FacilityId == fid && !p.IsMergedAlias);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var term = q.Trim().ToLower();
            query = query.Where(p =>
                p.HospitalNumber.ToLower().Contains(term) ||
                p.FirstName.ToLower().Contains(term) ||
                p.LastName.ToLower().Contains(term) ||
                (p.Phone != null && p.Phone.Contains(term)));
        }
        var rows = await query.OrderByDescending(p => p.Id).Take(40).ToListAsync(ct);
        return Ok(rows.Select(ToPatient).ToList());
    }

    [HttpGet("patients/{id:int}")]
    [ProducesResponseType(typeof(ApiPatientChart), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> PatientChart(int id, CancellationToken ct)
    {
        var fid = FacilityId;
        var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == fid, ct);
        if (p is null) return NotFound(new ApiError("Patient not found.", "not_found"));

        var allergies = await _db.Allergies.AsNoTracking()
            .Where(a => a.PatientId == id)
            .Select(a => a.Reaction == null ? a.Substance : a.Substance + " (" + a.Reaction + ")")
            .ToListAsync(ct);
        var problems = await _db.Problems.AsNoTracking()
            .Where(pr => pr.PatientId == id)
            .OrderByDescending(pr => pr.RecordedAt).Take(20)
            .Select(pr => new ApiPatientChartProblem(pr.Description, pr.Status.ToString()))
            .ToListAsync(ct);
        var visits = await _db.Encounters.AsNoTracking()
            .Include(e => e.Clinic).Include(e => e.Clinician)
            .Where(e => e.PatientId == id)
            .OrderByDescending(e => e.StartedAt).Take(10).ToListAsync(ct);
        var visitDtos = visits.Select(e => new ApiVisitSummary(
            e.Id, e.StartedAt, e.Status.ToString(), e.Type.ToString(), e.ChiefComplaint,
            e.Clinic?.Name, e.Clinician?.FullName)).ToList();

        return Ok(new ApiPatientChart(ToPatient(p), allergies, problems, visitDtos));
    }
}
