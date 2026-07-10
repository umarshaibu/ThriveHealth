using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Api;
using ThriveHealth.Web.Models.Billing;
using ThriveHealth.Web.Models.Portal;
using ThriveHealth.Web.Models.Telemedicine;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers.Api;

/// <summary>Patient telemedicine: list/request consults, get a LiveKit join token, cancel, rate.</summary>
[Route("api/v1/tele")]
public class TeleApiController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITelemedicineService _tele;
    private readonly ILiveKitTokenService _liveKit;

    public TeleApiController(ApplicationDbContext db, ITelemedicineService tele, ILiveKitTokenService liveKit)
    {
        _db = db; _tele = tele; _liveKit = liveKit;
    }

    private static ApiTeleSession ToDto(TeleSession s) => new(
        s.Id, s.SessionNumber, s.Mode.ToString(), s.Status.ToString(), s.ScheduledStartUtc,
        s.ConsultationReason, s.Clinician?.FullName, s.BillId, s.Bill?.Status.ToString(),
        s.Bill?.Balance, s.RoomToken, s.PatientRating);

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApiTeleSession>), StatusCodes.Status200OK)]
    public async Task<IActionResult> List(CancellationToken ct)
    {
        var pid = PatientId;
        var sessions = await _db.TeleSessions.AsNoTracking()
            .Include(s => s.Clinician).Include(s => s.Bill)
            .Where(s => s.PatientId == pid)
            .OrderByDescending(s => s.CreatedAt).Take(50).ToListAsync(ct);
        return Ok(sessions.Select(ToDto).ToList());
    }

    [HttpGet("fees")]
    [ProducesResponseType(typeof(ApiTeleFees), StatusCodes.Status200OK)]
    public IActionResult Fees() =>
        Ok(new ApiTeleFees(_tele.Fees.Video, _tele.Fees.Audio, _tele.Fees.Chat, _tele.Fees.Currency));

    [HttpPost("request")]
    [ProducesResponseType(typeof(ApiTeleSession), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Request([FromBody] ApiTeleRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        if (!Enum.TryParse<TeleSessionMode>(req.Mode, ignoreCase: true, out var mode))
            return BadRequest(new ApiError("Mode must be Video, Audio or Chat.", "bad_mode"));

        var pid = PatientId;
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid, ct);
        if (patient is null) return NotFound(new ApiError("Patient not found.", "not_found"));

        var gate = PatientRegistrationGate.Check(patient);
        if (!gate.IsComplete)
            return BadRequest(new ApiError("Complete your hospital profile before requesting a tele-consult.", "profile_incomplete"));

        var start = req.ScheduledStartUtc ?? DateTime.UtcNow.AddMinutes(15);
        if (start.Kind == DateTimeKind.Unspecified) start = DateTime.SpecifyKind(start, DateTimeKind.Utc);

        var sessionId = await _tele.RequestSessionAsync(patient.FacilityId, pid, req.Reason, mode, start.ToUniversalTime());

        _db.PortalSymptomIntakes.Add(new PortalSymptomIntake
        {
            PatientId = pid,
            TeleSessionId = sessionId,
            ChiefComplaint = req.Reason,
            Symptoms = req.Symptoms ?? string.Empty
        });
        await _db.SaveChangesAsync(ct);

        var created = await _db.TeleSessions.AsNoTracking()
            .Include(s => s.Clinician).Include(s => s.Bill)
            .FirstAsync(s => s.Id == sessionId, ct);
        return Ok(ToDto(created));
    }

    /// <summary>LiveKit join token. Requires the consult bill to be settled (pay-before-join).</summary>
    [HttpGet("{id:int}/token")]
    [ProducesResponseType(typeof(ApiTeleToken), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status402PaymentRequired)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Token(int id, CancellationToken ct)
    {
        var pid = PatientId;
        var s = await _db.TeleSessions.AsNoTracking()
            .Include(x => x.Patient).Include(x => x.Bill)
            .FirstOrDefaultAsync(x => x.Id == id && x.PatientId == pid, ct);
        if (s is null) return NotFound(new ApiError("Consult not found.", "not_found"));

        if (s.Bill is not null && s.Bill.Status != BillStatus.Paid)
            return StatusCode(StatusCodes.Status402PaymentRequired,
                new ApiError($"Settle bill {s.Bill.BillNumber} before joining.", "payment_required"));

        // Chat consults use the chat thread, not a LiveKit room.
        if (s.Mode == TeleSessionMode.Chat)
            return Ok(new ApiTeleToken(false, null, null, null, "Chat"));

        if (!_liveKit.IsConfigured)
            return Ok(new ApiTeleToken(false, null, null, null, s.Mode.ToString()));

        var displayName = s.Patient?.FullName ?? "Patient";
        var token = _liveKit.IssueAccessToken(s, $"patient-{pid}", displayName, canPublish: true);
        return Ok(new ApiTeleToken(true, _liveKit.ServerUrl, token, _liveKit.RoomName(s), s.Mode.ToString()));
    }

    [HttpPost("{id:int}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(int id, [FromBody] ApiLogoutRequest? _ = null, CancellationToken ct = default)
    {
        var pid = PatientId;
        var s = await _db.TeleSessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.PatientId == pid, ct);
        if (s is null) return NotFound(new ApiError("Consult not found.", "not_found"));
        if (s.Status is TeleSessionStatus.Completed or TeleSessionStatus.Cancelled
            or TeleSessionStatus.NoShowPatient or TeleSessionStatus.NoShowClinician or TeleSessionStatus.InCall)
            return BadRequest(new ApiError("This consult cannot be cancelled at this stage.", "not_cancellable"));

        var (ok, refund) = await _tele.CancelAsync(id, "Cancelled by patient (mobile)", patientInitiated: true, ct);
        if (!ok) return BadRequest(new ApiError("Could not cancel.", "cancel_failed"));
        return Ok(new { cancelled = true, refundAmount = refund });
    }

    [HttpPost("{id:int}/rate")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Rate(int id, [FromBody] ApiRateRequest req, CancellationToken ct)
    {
        if (req.Rating < 1 || req.Rating > 5)
            return BadRequest(new ApiError("Rating must be between 1 and 5.", "bad_rating"));
        var pid = PatientId;
        var s = await _db.TeleSessions.FirstOrDefaultAsync(x => x.Id == id && x.PatientId == pid, ct);
        if (s is null) return NotFound(new ApiError("Consult not found.", "not_found"));
        if (s.Status != TeleSessionStatus.Completed)
            return BadRequest(new ApiError("Only completed consults can be rated.", "not_completed"));
        s.PatientRating = req.Rating;
        s.PatientFeedback = string.IsNullOrWhiteSpace(req.Feedback) ? null : req.Feedback.Trim();
        await _db.SaveChangesAsync(ct);
        return Ok(new { rated = true });
    }
}
