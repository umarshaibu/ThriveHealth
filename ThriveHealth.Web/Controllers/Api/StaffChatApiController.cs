using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Api;
using ThriveHealth.Web.Models.Telemedicine;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers.Api;

/// <summary>Clinician chat: inbox of patient threads, read a thread, and reply.</summary>
[Route("api/v1/staff/chat")]
public class StaffChatApiController : StaffApiControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITeleChatService _chat;

    public StaffChatApiController(ApplicationDbContext db, ITeleChatService chat)
    {
        _db = db; _chat = chat;
    }

    [HttpGet("inbox")]
    [ProducesResponseType(typeof(IReadOnlyList<ApiChatInboxItem>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Inbox(CancellationToken ct)
    {
        // Consultants see threads where they're primary or added; doctors see the facility inbox too.
        var threads = await _chat.ListClinicianInboxAsync(FacilityId, null, ct);
        return Ok(threads.Select(t => new ApiChatInboxItem(
            t.PatientId, t.PatientName, t.PatientHospitalNumber, t.ActiveSessionId,
            t.LastMessagePreview, t.LastMessageAt, t.UnreadForClinicianCount)).ToList());
    }

    [HttpGet("{patientId:int}/messages")]
    [ProducesResponseType(typeof(IReadOnlyList<ApiChatMessage>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Messages(int patientId, [FromQuery] long? sinceId, CancellationToken ct)
    {
        var session = await _chat.GetOrCreateActiveChatSessionAsync(FacilityId, patientId, ct);
        if (session is null) return Ok(Array.Empty<ApiChatMessage>());

        var patientName = await _db.Patients.AsNoTracking().Where(p => p.Id == patientId)
            .Select(p => p.FirstName + " " + p.LastName).FirstOrDefaultAsync(ct) ?? "Patient";

        await _chat.MarkReadAsync(session.Id, ChatSenderRole.Clinician, ct);
        var msgs = await _db.TeleChatMessages.AsNoTracking()
            .Where(m => m.TeleSessionId == session.Id && (sinceId == null || m.Id > sinceId))
            .OrderBy(m => m.SentAt).ToListAsync(ct);

        return Ok(msgs.Select(m => new ApiChatMessage(
            m.Id, m.SenderRole.ToString(),
            m.SenderRole == ChatSenderRole.Clinician ? "You" : m.SenderRole == ChatSenderRole.System ? "System" : patientName,
            m.Body, m.SentAt,
            m.SenderRole == ChatSenderRole.Clinician && m.ReadByPatientAt.HasValue)).ToList());
    }

    [HttpPost("{patientId:int}/messages")]
    [ProducesResponseType(typeof(ApiChatMessage), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Reply(int patientId, [FromBody] ApiSendChatRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Body)) return BadRequest(new ApiError("Message cannot be empty.", "empty"));
        var session = await _chat.GetOrCreateActiveChatSessionAsync(FacilityId, patientId, ct);
        if (session is null) return BadRequest(new ApiError("No active chat thread for this patient.", "no_thread"));

        var msg = await _chat.AddMessageAsync(session.Id, ChatSenderRole.Clinician, UserId, req.Body.Trim(), null, ct);
        return Ok(new ApiChatMessage(msg.Id, "Clinician", "You", msg.Body, msg.SentAt, false));
    }
}
