using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Api;
using ThriveHealth.Web.Models.Telemedicine;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers.Api;

/// <summary>Clinician telemedicine: inbox of requested/assigned consults, claim, join (LiveKit), end.</summary>
[Route("api/v1/staff/tele")]
public class StaffTeleApiController : StaffApiControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITelemedicineService _tele;
    private readonly ILiveKitTokenService _liveKit;
    private readonly ITeleNotifier _notifier;

    public StaffTeleApiController(ApplicationDbContext db, ITelemedicineService tele,
        ILiveKitTokenService liveKit, ITeleNotifier notifier)
    {
        _db = db; _tele = tele; _liveKit = liveKit; _notifier = notifier;
    }

    [HttpGet]
    [ProducesResponseType(typeof(IReadOnlyList<ApiTeleInboxItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Inbox(CancellationToken ct)
    {
        var me = UserId;
        var fid = FacilityId;
        var rows = await _db.TeleSessions.AsNoTracking().Include(t => t.Patient)
            .Where(t => t.FacilityId == fid && t.Mode != TeleSessionMode.Chat
                && (t.Status == TeleSessionStatus.Requested
                    || (t.ClinicianId == me && (t.Status == TeleSessionStatus.Scheduled
                        || t.Status == TeleSessionStatus.PatientWaiting || t.Status == TeleSessionStatus.InCall))))
            .OrderBy(t => t.ScheduledStartUtc).Take(50).ToListAsync(ct);

        return Ok(rows.Select(t => new ApiTeleInboxItem(
            t.Id, t.SessionNumber, t.Mode.ToString(), t.Status.ToString(), t.ScheduledStartUtc,
            t.PatientId, t.Patient?.FullName ?? "", t.Patient?.HospitalNumber ?? "",
            t.ConsultationReason, t.ClinicianId == me)).ToList());
    }

    [HttpPost("{id:int}/claim")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Claim(int id, CancellationToken ct)
    {
        var s = await _db.TeleSessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == FacilityId, ct);
        if (s is null) return NotFound(new ApiError("Consult not found.", "not_found"));
        var ok = await _tele.AssignClinicianAsync(id, UserId, ct);
        if (!ok) return BadRequest(new ApiError("Could not claim this consult.", "claim_failed"));
        return Ok(new { claimed = true });
    }

    /// <summary>Join token. Marks the clinician as ready (notifies/pushes the patient) and returns a LiveKit token.</summary>
    [HttpGet("{id:int}/token")]
    [ProducesResponseType(typeof(ApiTeleToken), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Token(int id, CancellationToken ct)
    {
        var me = UserId;
        var s = await _db.TeleSessions.AsNoTracking().Include(x => x.Patient)
            .FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == FacilityId, ct);
        if (s is null) return NotFound(new ApiError("Consult not found.", "not_found"));
        if (s.ClinicianId != me) return BadRequest(new ApiError("Claim this consult before joining.", "not_assigned"));
        if (s.Mode == TeleSessionMode.Chat) return Ok(new ApiTeleToken(false, null, null, null, "Chat"));

        await _tele.ClinicianJoinAsync(id, ct);
        await _notifier.NotifyClinicianReadyAsync(id, ct);

        if (!_liveKit.IsConfigured) return Ok(new ApiTeleToken(false, null, null, null, s.Mode.ToString()));

        var displayName = await _db.Users.AsNoTracking().Where(u => u.Id == me)
            .Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync(ct) ?? "Clinician";
        var token = _liveKit.IssueAccessToken(s, $"clinician-{me}", displayName, canPublish: true);
        return Ok(new ApiTeleToken(true, _liveKit.ServerUrl, token, _liveKit.RoomName(s), s.Mode.ToString()));
    }

    [HttpPost("{id:int}/end")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    public async Task<IActionResult> End(int id, CancellationToken ct)
    {
        var s = await _db.TeleSessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.ClinicianId == UserId, ct);
        if (s is null) return NotFound(new ApiError("Consult not found.", "not_found"));
        await _tele.EndSessionAsync(id, null, UserId, ct);
        return Ok(new { ended = true });
    }
}
