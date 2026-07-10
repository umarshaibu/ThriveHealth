using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Api;
using ThriveHealth.Web.Models.Telemedicine;
using ThriveHealth.Web.Services;
using ThriveHealth.Web.Services.Integrations;

namespace ThriveHealth.Web.Controllers.Api;

/// <summary>
/// Patient async chat with clinicians (chat-mode tele-consults / 24-hour chat packages). Reuses the
/// same <see cref="ITeleChatService"/> that backs the web portal; the app polls GET /chat/messages.
/// </summary>
[Route("api/v1/chat")]
public class ChatApiController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITeleChatService _chat;
    private readonly IBillingService _billing;
    private readonly IPaymentGateway _payment;

    public ChatApiController(ApplicationDbContext db, ITeleChatService chat, IBillingService billing, IPaymentGateway payment)
    {
        _db = db; _chat = chat; _billing = billing; _payment = payment;
    }

    private static ApiChatMessage Map(TeleChatMessage m) => new(
        m.Id,
        m.SenderRole.ToString(),
        m.SenderRole == ChatSenderRole.Clinician ? (m.SenderUser != null ? "Dr " + m.SenderUser.FullName : "Clinician")
            : m.SenderRole == ChatSenderRole.System ? "System" : "You",
        m.Body,
        m.SentAt,
        m.SenderRole == ChatSenderRole.Patient && m.ReadByClinicianAt.HasValue);

    private async Task<TeleSession?> ActiveSessionAsync(int? sessionId, CancellationToken ct)
    {
        var pid = PatientId;
        if (sessionId.HasValue)
            return await _db.TeleSessions.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sessionId.Value && s.PatientId == pid && s.Mode == TeleSessionMode.Chat, ct);
        return await _db.TeleSessions.AsNoTracking()
            .Where(s => s.PatientId == pid && s.Mode == TeleSessionMode.Chat
                && s.Status != TeleSessionStatus.Completed && s.Status != TeleSessionStatus.Cancelled)
            .OrderByDescending(s => s.CreatedAt).FirstOrDefaultAsync(ct);
    }

    private async Task<IReadOnlyList<ApiChatMessage>> MessagesAsync(int teleSessionId, long? sinceId, CancellationToken ct)
    {
        var msgs = await _db.TeleChatMessages.AsNoTracking()
            .Include(m => m.SenderUser)
            .Where(m => m.TeleSessionId == teleSessionId && (sinceId == null || m.Id > sinceId))
            .OrderBy(m => m.SentAt).ToListAsync(ct);
        return msgs.Select(Map).ToList();
    }

    /// <summary>The current chat thread (auto-created if a chat package is active) + package + history.</summary>
    [HttpGet]
    [ProducesResponseType(typeof(ApiChatThread), StatusCodes.Status200OK)]
    public async Task<IActionResult> Thread(CancellationToken ct)
    {
        var pid = PatientId;
        var session = await _chat.GetOrCreateActiveChatSessionAsync(FacilityId, pid, ct);
        var pkg = await _chat.GetActivePackageAsync(pid, ct);

        IReadOnlyList<ApiChatMessage> messages = session is null
            ? Array.Empty<ApiChatMessage>()
            : await MessagesAsync(session.Id, null, ct);
        if (session is not null) await _chat.MarkReadAsync(session.Id, ChatSenderRole.Patient, ct);

        return Ok(new ApiChatThread(
            session?.Id, session?.Status.ToString(),
            new ApiChatPackage(pkg.Price, pkg.IsActive, pkg.ExpiresAt),
            messages));
    }

    /// <summary>Poll for messages newer than <paramref name="sinceId"/> on the active thread.</summary>
    [HttpGet("messages")]
    [ProducesResponseType(typeof(IReadOnlyList<ApiChatMessage>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Messages([FromQuery] long? sinceId, [FromQuery] int? sessionId, CancellationToken ct)
    {
        var session = await ActiveSessionAsync(sessionId, ct);
        if (session is null) return Ok(Array.Empty<ApiChatMessage>());
        await _chat.MarkReadAsync(session.Id, ChatSenderRole.Patient, ct);
        return Ok(await MessagesAsync(session.Id, sinceId, ct));
    }

    [HttpPost("messages")]
    [ProducesResponseType(typeof(ApiChatMessage), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Send([FromBody] ApiSendChatRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(req.Body))
            return BadRequest(new ApiError("Message cannot be empty.", "empty"));

        var session = await _chat.GetOrCreateActiveChatSessionAsync(FacilityId, PatientId, ct);
        if (session is null)
            return BadRequest(new ApiError("Buy a chat package or request a chat consult to start a conversation.", "no_thread"));

        var msg = await _chat.AddMessageAsync(session.Id, ChatSenderRole.Patient, null, req.Body.Trim(), req.RepliesToMessageId, ct);
        return Ok(Map(msg));
    }

    [HttpGet("package")]
    [ProducesResponseType(typeof(ApiChatPackage), StatusCodes.Status200OK)]
    public async Task<IActionResult> Package(CancellationToken ct)
    {
        var pkg = await _chat.GetActivePackageAsync(PatientId, ct);
        return Ok(new ApiChatPackage(pkg.Price, pkg.IsActive, pkg.ExpiresAt));
    }

    /// <summary>Buys a 24-hour chat package: creates a bill and initiates payment (settle via /payments/{tx}/confirm).</summary>
    [HttpPost("package/buy")]
    [ProducesResponseType(typeof(ApiPayInitiateResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> BuyPackage(CancellationToken ct)
    {
        var pid = PatientId;
        var pkg = await _chat.GetActivePackageAsync(pid, ct);
        if (pkg.IsActive) return BadRequest(new ApiError("You already have an active chat package.", "already_active"));

        var billId = await _chat.CreateChatPackageBillAsync(FacilityId, pid, pkg.Price, _billing, ct);
        var bill = await _db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == billId, ct);
        var email = await _db.PortalAccounts.AsNoTracking().Where(a => a.PatientId == pid).Select(a => a.Email).FirstOrDefaultAsync(ct);
        var phone = await _db.Patients.AsNoTracking().Where(p => p.Id == pid).Select(p => p.Phone).FirstOrDefaultAsync(ct);

        var init = await _payment.InitiateAsync(new PaymentInitiateRequest(
            FacilityId: FacilityId, BillId: billId, PatientId: pid, Amount: bill?.NetAmount ?? pkg.Price,
            CustomerEmail: email, CustomerPhone: phone, InitiatedById: $"portal-{pid}"), ct);

        var requiresExternal = init.AuthorizationUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase);
        return Ok(new ApiPayInitiateResult(init.TransactionId, init.Reference, init.AuthorizationUrl, _payment.ProviderName, requiresExternal));
    }
}
