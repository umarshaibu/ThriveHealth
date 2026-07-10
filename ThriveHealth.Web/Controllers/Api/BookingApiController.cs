using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Api;
using ThriveHealth.Web.Models.Scheduling;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers.Api;

/// <summary>Patient self-service appointment booking: clinics, available slots, book, cancel.
/// (Listing existing appointments lives on PatientApiController at GET /api/v1/appointments.)</summary>
[Route("api/v1")]
public class BookingApiController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ISlotService _slots;

    public BookingApiController(ApplicationDbContext db, ISlotService slots)
    {
        _db = db; _slots = slots;
    }

    [HttpGet("clinics")]
    [ProducesResponseType(typeof(IReadOnlyList<ApiClinic>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Clinics(CancellationToken ct)
    {
        var fid = FacilityId;
        var clinics = await _db.Clinics.AsNoTracking()
            .Where(c => c.FacilityId == fid && c.IsActive)
            .OrderBy(c => c.Name)
            .Select(c => new ApiClinic(c.Id, c.Name, c.Specialty.ToString()))
            .ToListAsync(ct);
        return Ok(clinics);
    }

    [HttpGet("booking/slots")]
    [ProducesResponseType(typeof(IReadOnlyList<ApiSlot>), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Slots([FromQuery] int clinicId, [FromQuery] string date, CancellationToken ct)
    {
        if (clinicId <= 0) return BadRequest(new ApiError("clinicId is required.", "bad_clinic"));
        if (!DateOnly.TryParse(date, out var day)) return BadRequest(new ApiError("date must be YYYY-MM-DD.", "bad_date"));

        // Guard: only clinics in the caller's facility.
        var clinicOk = await _db.Clinics.AsNoTracking().AnyAsync(c => c.Id == clinicId && c.FacilityId == FacilityId, ct);
        if (!clinicOk) return BadRequest(new ApiError("Unknown clinic.", "bad_clinic"));

        var slots = await _slots.GetAvailableSlotsAsync(clinicId, day, ct);
        return Ok(slots.Select(s => new ApiSlot(s.StartUtc, s.DurationMinutes, s.ClinicianId, s.ClinicianName, s.RoomId)).ToList());
    }

    [HttpPost("booking")]
    [ProducesResponseType(typeof(ApiAppointment), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Book([FromBody] ApiBookRequest req, CancellationToken ct)
    {
        var pid = PatientId;
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid, ct);
        if (patient is null) return NotFound(new ApiError("Patient not found.", "not_found"));

        var clinic = await _db.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == req.ClinicId && c.FacilityId == patient.FacilityId, ct);
        if (clinic is null) return BadRequest(new ApiError("Unknown clinic.", "bad_clinic"));
        if (req.ScheduledStartUtc == default) return BadRequest(new ApiError("Choose a slot.", "no_slot"));

        var start = req.ScheduledStartUtc.Kind == DateTimeKind.Unspecified
            ? DateTime.SpecifyKind(req.ScheduledStartUtc, DateTimeKind.Utc)
            : req.ScheduledStartUtc.ToUniversalTime();

        var appt = new Appointment
        {
            FacilityId = patient.FacilityId,
            PatientId = pid,
            ClinicId = req.ClinicId,
            ClinicianId = string.IsNullOrWhiteSpace(req.ClinicianId) ? null : req.ClinicianId,
            RoomId = req.RoomId,
            Type = AppointmentType.NewOpd,
            Channel = BookingChannel.PatientPortal,
            Status = AppointmentStatus.Scheduled,
            ScheduledStartUtc = start,
            DurationMinutes = req.DurationMinutes > 0 ? req.DurationMinutes : clinic.DefaultSlotMinutes,
            ReasonForVisit = req.ReasonForVisit?.Trim()
        };
        _db.Appointments.Add(appt);
        await _db.SaveChangesAsync(ct);

        var clinicianName = appt.ClinicianId is null ? null :
            await _db.Users.AsNoTracking().Where(u => u.Id == appt.ClinicianId)
                .Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync(ct);

        return Ok(new ApiAppointment(appt.Id, appt.ScheduledStartUtc, appt.DurationMinutes, appt.Status.ToString(),
            appt.Type.ToString(), clinic.Name, clinicianName, appt.ReasonForVisit));
    }

    [HttpPost("appointments/{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(int id, CancellationToken ct)
    {
        var pid = PatientId;
        var appt = await _db.Appointments.FirstOrDefaultAsync(a => a.Id == id && a.PatientId == pid, ct);
        if (appt is null) return NotFound(new ApiError("Appointment not found.", "not_found"));
        if (appt.Status is AppointmentStatus.Completed or AppointmentStatus.Cancelled or AppointmentStatus.NoShow)
            return BadRequest(new ApiError("This appointment cannot be cancelled.", "not_cancellable"));

        appt.Status = AppointmentStatus.Cancelled;
        appt.CancelledAt = DateTime.UtcNow;
        appt.CancelReason = "Cancelled by patient (mobile)";
        await _db.SaveChangesAsync(ct);
        return Ok(new { cancelled = true });
    }
}
