using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Hubs;
using ThriveHealth.Web.Models.Billing;
using ThriveHealth.Web.Models.Integrations;
using ThriveHealth.Web.Models.Telemedicine;

namespace ThriveHealth.Web.Services;

public record ChatThreadSummary(
    int PatientId,
    string PatientName,
    string PatientHospitalNumber,
    int? ActiveSessionId,
    string? ActiveSessionNumber,
    TeleSessionStatus? ActiveStatus,
    string? LastMessagePreview,
    DateTime? LastMessageAt,
    ChatSenderRole? LastMessageRole,
    int UnreadForClinicianCount);

public record ChatPackageInfo(int? Id, string? Number, decimal Price, DateTime? PurchasedAt, DateTime? ExpiresAt, bool IsActive);

public interface ITeleChatService
{
    Task<TeleChatMessage> AddMessageAsync(int teleSessionId, ChatSenderRole sender, string? senderUserId, string body, long? repliesToMessageId = null, CancellationToken ct = default);
    Task AttachToMessageAsync(long messageId, string fileName, string contentType, long sizeBytes, string url, CancellationToken ct = default);
    Task<IReadOnlyList<TeleChatMessage>> ListMessagesAsync(int teleSessionId, CancellationToken ct = default);
    Task<IReadOnlyList<TeleChatMessage>> ListMessagesByPatientAsync(int patientId, int take = 200, CancellationToken ct = default);
    Task<IReadOnlyDictionary<long, List<TeleChatAttachment>>> ListAttachmentsAsync(IEnumerable<long> messageIds, CancellationToken ct = default);
    Task MarkReadAsync(int teleSessionId, ChatSenderRole reader, CancellationToken ct = default);
    Task<int> UnreadForClinicianAsync(int facilityId, CancellationToken ct = default);

    /// <summary>Returns the inbox: one row per patient with an active or recent chat thread.
    /// If <paramref name="filterClinicianUserId"/> is set, only threads where that clinician is
    /// the primary OR an added participant are returned.</summary>
    Task<IReadOnlyList<ChatThreadSummary>> ListClinicianInboxAsync(int facilityId, string? filterClinicianUserId = null, CancellationToken ct = default);

    /// <summary>Patient-side: latest active TeleSession (chat mode) or null. Auto-creates one if a chat package is active.</summary>
    Task<TeleSession?> GetOrCreateActiveChatSessionAsync(int facilityId, int patientId, CancellationToken ct = default);

    Task<ChatPackageInfo> GetActivePackageAsync(int patientId, CancellationToken ct = default);

    /// <summary>Buys a 24-hour chat package — creates a Bill that the patient pays via the existing gateway.</summary>
    Task<int> CreateChatPackageBillAsync(int facilityId, int patientId, decimal price, IBillingService billing, CancellationToken ct = default);

    /// <summary>Activates a previously-purchased chat package once payment lands. Idempotent.</summary>
    Task<int?> ActivatePackageForBillAsync(int billId, CancellationToken ct = default);
}

public class TeleChatService : ITeleChatService
{
    private static readonly TimeSpan PackageDuration = TimeSpan.FromHours(24);
    private const decimal DefaultChatPackagePrice = 2_500m;

    private readonly ApplicationDbContext _db;
    private readonly IHubContext<TeleChatHub> _hub;
    private readonly IWebPushService _push;
    public TeleChatService(ApplicationDbContext db, IHubContext<TeleChatHub> hub, IWebPushService push)
    { _db = db; _hub = hub; _push = push; }

    private async Task BroadcastAsync(TeleChatMessage msg, CancellationToken ct)
    {
        var session = await _db.TeleSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == msg.TeleSessionId, ct);
        if (session is null) return;
        var sender = msg.SenderUserId is null ? null
            : await _db.Users.AsNoTracking().Where(u => u.Id == msg.SenderUserId).Select(u => u.FullName).FirstOrDefaultAsync(ct);
        var attachments = await _db.TeleChatAttachments.AsNoTracking()
            .Where(a => a.MessageId == msg.Id)
            .Select(a => new { a.Id, a.FileName, a.ContentType, a.SizeBytes, a.Url })
            .ToListAsync(ct);
        object? replyTo = null;
        if (msg.RepliesToMessageId.HasValue)
        {
            var src = await _db.TeleChatMessages.AsNoTracking()
                .Include(m => m.SenderUser)
                .Where(m => m.Id == msg.RepliesToMessageId.Value)
                .Select(m => new { m.Id, m.Body, m.SenderRole, who = m.SenderUser != null ? m.SenderUser.FullName : null })
                .FirstOrDefaultAsync(ct);
            if (src != null)
                replyTo = new { id = src.Id, role = src.SenderRole.ToString(), who = src.who, snippet = src.Body.Length > 120 ? src.Body[..120] + "…" : src.Body };
        }
        var payload = new
        {
            id = msg.Id,
            patientId = msg.PatientId,
            sessionId = msg.TeleSessionId,
            role = msg.SenderRole.ToString(),
            who = msg.SenderRole switch
            {
                ChatSenderRole.Patient => "Patient",
                ChatSenderRole.Clinician => sender is null ? "Clinician" : "Dr " + sender,
                _ => "System"
            },
            body = msg.Body,
            sentAt = msg.SentAt,
            // Brand-new messages can't have been read by the other party yet.
            readByOther = false,
            replyTo,
            attachments
        };
        await _hub.Clients.Group(TeleChatHub.GroupForPatient(msg.PatientId)).SendAsync("messageReceived", payload, ct);
        await _hub.Clients.Group(TeleChatHub.GroupForInbox(session.FacilityId)).SendAsync("inboxUpdated", new
        {
            patientId = msg.PatientId,
            preview = msg.Body.Length > 100 ? msg.Body[..100] + "…" : msg.Body,
            sentAt = msg.SentAt,
            role = msg.SenderRole.ToString()
        }, ct);

        // Web Push (PWA) — fire to the OTHER side so closed-tab / mobile users get a notification.
        // Patient sends → push to all assigned clinicians (primary + participants).
        // Clinician sends → push to the patient.
        await SendPushForMessageAsync(msg, session, sender, ct);
    }

    private async Task SendPushForMessageAsync(TeleChatMessage msg, TeleSession session, string? senderName, CancellationToken ct)
    {
        if (msg.SenderRole == ChatSenderRole.System) return;
        var url = "/Portal/Chat";
        var preview = msg.Body.Length > 120 ? msg.Body[..120] + "…" : msg.Body;
        var tag = $"telechat-{msg.PatientId}";

        if (msg.SenderRole == ChatSenderRole.Patient)
        {
            // Notify primary clinician + all participants
            var clinicianIds = new List<string>();
            if (!string.IsNullOrEmpty(session.ClinicianId)) clinicianIds.Add(session.ClinicianId);
            var extras = await _db.TeleSessionParticipants.AsNoTracking()
                .Where(p => p.TeleSessionId == session.Id).Select(p => p.ClinicianId).ToListAsync(ct);
            clinicianIds.AddRange(extras);

            var patientName = await _db.Patients.AsNoTracking().Where(p => p.Id == msg.PatientId)
                .Select(p => p.FirstName + " " + p.LastName).FirstOrDefaultAsync(ct) ?? "Patient";
            var title = $"{patientName} sent a chat message";
            var clinicianUrl = $"/Telemedicine/Chat?patientId={msg.PatientId}";
            foreach (var cid in clinicianIds.Distinct())
                await _push.SendToOwnerAsync(PushOwnerType.Clinician, cid, new WebPushPayload(title, preview, clinicianUrl, tag), ct);
        }
        else
        {
            var title = senderName is null ? "New message from your clinician" : $"Dr {senderName}";
            await _push.SendToOwnerAsync(PushOwnerType.Patient, msg.PatientId.ToString(),
                new WebPushPayload(title, preview, url, tag), ct);
        }
    }

    public async Task<TeleChatMessage> AddMessageAsync(int teleSessionId, ChatSenderRole sender, string? senderUserId, string body, long? repliesToMessageId = null, CancellationToken ct = default)
    {
        var session = await _db.TeleSessions.FirstOrDefaultAsync(s => s.Id == teleSessionId, ct)
            ?? throw new InvalidOperationException("Tele-session not found.");

        // Ignore reply target if it doesn't belong to this patient (defence in depth: don't allow
        // quoting messages from a different patient's thread).
        long? validatedReplyId = null;
        if (repliesToMessageId.HasValue)
        {
            var ok = await _db.TeleChatMessages.AsNoTracking()
                .AnyAsync(m => m.Id == repliesToMessageId.Value && m.PatientId == session.PatientId, ct);
            if (ok) validatedReplyId = repliesToMessageId;
        }

        var msg = new TeleChatMessage
        {
            TeleSessionId = teleSessionId,
            PatientId = session.PatientId,
            SenderRole = sender,
            SenderUserId = senderUserId,
            Body = body.Trim(),
            RepliesToMessageId = validatedReplyId,
            SentAt = DateTime.UtcNow,
            // The sender has obviously read their own message.
            ReadByPatientAt = sender == ChatSenderRole.Patient ? DateTime.UtcNow : null,
            ReadByClinicianAt = sender == ChatSenderRole.Clinician ? DateTime.UtcNow : null
        };
        _db.TeleChatMessages.Add(msg);
        await _db.SaveChangesAsync(ct);
        await BroadcastAsync(msg, ct);
        return msg;
    }

    public async Task AttachToMessageAsync(long messageId, string fileName, string contentType, long sizeBytes, string url, CancellationToken ct = default)
    {
        var att = new TeleChatAttachment
        {
            MessageId = messageId,
            FileName = fileName,
            ContentType = contentType,
            SizeBytes = sizeBytes,
            Url = url,
            UploadedAt = DateTime.UtcNow
        };
        _db.TeleChatAttachments.Add(att);
        await _db.SaveChangesAsync(ct);
        // Re-broadcast so subscribers see the updated message with its attachment
        var msg = await _db.TeleChatMessages.AsNoTracking().FirstOrDefaultAsync(m => m.Id == messageId, ct);
        if (msg is not null) await BroadcastAsync(msg, ct);
    }

    public async Task<IReadOnlyList<TeleChatMessage>> ListMessagesAsync(int teleSessionId, CancellationToken ct = default) =>
        await _db.TeleChatMessages.AsNoTracking()
            .Include(m => m.SenderUser)
            .Include(m => m.RepliesToMessage).ThenInclude(rm => rm!.SenderUser)
            .Where(m => m.TeleSessionId == teleSessionId)
            .OrderBy(m => m.SentAt)
            .ToListAsync(ct);

    public async Task<IReadOnlyList<TeleChatMessage>> ListMessagesByPatientAsync(int patientId, int take = 200, CancellationToken ct = default)
    {
        var rows = await _db.TeleChatMessages.AsNoTracking()
            .Include(m => m.SenderUser)
            .Include(m => m.RepliesToMessage).ThenInclude(rm => rm!.SenderUser)
            .Where(m => m.PatientId == patientId)
            .OrderByDescending(m => m.SentAt).Take(take)
            .ToListAsync(ct);
        rows.Reverse();
        return rows;
    }

    public async Task<IReadOnlyDictionary<long, List<TeleChatAttachment>>> ListAttachmentsAsync(IEnumerable<long> messageIds, CancellationToken ct = default)
    {
        var ids = messageIds.ToHashSet();
        if (ids.Count == 0) return new Dictionary<long, List<TeleChatAttachment>>();
        var atts = await _db.TeleChatAttachments.AsNoTracking()
            .Where(a => ids.Contains(a.MessageId)).ToListAsync(ct);
        return atts.GroupBy(a => a.MessageId).ToDictionary(g => g.Key, g => g.ToList());
    }

    public async Task MarkReadAsync(int teleSessionId, ChatSenderRole reader, CancellationToken ct = default)
    {
        var now = DateTime.UtcNow;
        var session = await _db.TeleSessions.AsNoTracking().FirstOrDefaultAsync(s => s.Id == teleSessionId, ct);
        if (session is null) return;
        var unread = await _db.TeleChatMessages
            .Where(m => m.TeleSessionId == teleSessionId
                && (reader == ChatSenderRole.Clinician
                        ? m.ReadByClinicianAt == null && m.SenderRole == ChatSenderRole.Patient
                        : m.ReadByPatientAt == null && m.SenderRole == ChatSenderRole.Clinician))
            .ToListAsync(ct);
        if (unread.Count == 0) return;

        foreach (var m in unread)
        {
            if (reader == ChatSenderRole.Clinician) m.ReadByClinicianAt = now;
            else m.ReadByPatientAt = now;
        }
        await _db.SaveChangesAsync(ct);

        // Tell the OPPOSITE side (the original senders) that their messages have been read.
        await _hub.Clients.Group(TeleChatHub.GroupForPatient(session.PatientId)).SendAsync("messagesRead", new
        {
            patientId = session.PatientId,
            reader = reader.ToString(),
            messageIds = unread.Select(m => m.Id).ToArray(),
            at = now
        }, ct);
    }

    public async Task<int> UnreadForClinicianAsync(int facilityId, CancellationToken ct = default) =>
        await _db.TeleChatMessages.AsNoTracking()
            .Where(m => m.SenderRole == ChatSenderRole.Patient
                && m.ReadByClinicianAt == null
                && m.TeleSession!.FacilityId == facilityId)
            .CountAsync(ct);

    public async Task<IReadOnlyList<ChatThreadSummary>> ListClinicianInboxAsync(int facilityId, string? filterClinicianUserId = null, CancellationToken ct = default)
    {
        // If a filter is supplied, narrow to patients whose latest chat session has the clinician
        // as primary OR has them as an added participant.
        HashSet<int>? allowedPatientIds = null;
        if (!string.IsNullOrEmpty(filterClinicianUserId))
        {
            var primary = await _db.TeleSessions.AsNoTracking()
                .Where(s => s.FacilityId == facilityId && s.Mode == TeleSessionMode.Chat && s.ClinicianId == filterClinicianUserId)
                .Select(s => s.PatientId).ToListAsync(ct);
            var participated = await _db.TeleSessionParticipants.AsNoTracking()
                .Where(p => p.ClinicianId == filterClinicianUserId && p.TeleSession!.FacilityId == facilityId
                    && p.TeleSession.Mode == TeleSessionMode.Chat)
                .Select(p => p.TeleSession!.PatientId).ToListAsync(ct);
            allowedPatientIds = new HashSet<int>(primary.Concat(participated));
            if (allowedPatientIds.Count == 0) return Array.Empty<ChatThreadSummary>();
        }

        // Pull the latest message per patient (within facility, chat-mode) and count unread.
        // Doing this in two queries — neat enough at hospital-scale.
        var query = _db.TeleChatMessages.AsNoTracking()
            .Where(m => m.TeleSession!.FacilityId == facilityId && m.TeleSession.Mode == TeleSessionMode.Chat);
        if (allowedPatientIds != null) query = query.Where(m => allowedPatientIds.Contains(m.PatientId));
        var patientLatest = await query
            .GroupBy(m => m.PatientId)
            .Select(g => new
            {
                PatientId = g.Key,
                LastMessageAt = g.Max(x => x.SentAt),
                UnreadCount = g.Count(x => x.ReadByClinicianAt == null && x.SenderRole == ChatSenderRole.Patient)
            })
            .ToListAsync(ct);

        if (patientLatest.Count == 0) return Array.Empty<ChatThreadSummary>();

        var ids = patientLatest.Select(p => p.PatientId).ToHashSet();
        var lastMessages = await _db.TeleChatMessages.AsNoTracking()
            .Include(m => m.TeleSession)
            .Include(m => m.Patient)
            .Where(m => ids.Contains(m.PatientId)
                && m.TeleSession!.FacilityId == facilityId
                && m.TeleSession.Mode == TeleSessionMode.Chat)
            .ToListAsync(ct);
        var byPatient = lastMessages
            .GroupBy(m => m.PatientId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(x => x.SentAt).First());

        return patientLatest
            .OrderByDescending(p => p.LastMessageAt)
            .Select(p =>
            {
                var msg = byPatient[p.PatientId];
                return new ChatThreadSummary(
                    PatientId: p.PatientId,
                    PatientName: msg.Patient?.FullName ?? "Patient",
                    PatientHospitalNumber: msg.Patient?.HospitalNumber ?? "",
                    ActiveSessionId: msg.TeleSession?.Id,
                    ActiveSessionNumber: msg.TeleSession?.SessionNumber,
                    ActiveStatus: msg.TeleSession?.Status,
                    LastMessagePreview: msg.Body.Length > 100 ? msg.Body[..100] + "…" : msg.Body,
                    LastMessageAt: p.LastMessageAt,
                    LastMessageRole: msg.SenderRole,
                    UnreadForClinicianCount: p.UnreadCount);
            }).ToList();
    }

    public async Task<TeleSession?> GetOrCreateActiveChatSessionAsync(int facilityId, int patientId, CancellationToken ct = default)
    {
        var existing = await _db.TeleSessions
            .Where(s => s.PatientId == patientId && s.Mode == TeleSessionMode.Chat
                && s.Status != TeleSessionStatus.Completed && s.Status != TeleSessionStatus.Cancelled
                && s.Status != TeleSessionStatus.NoShowPatient && s.Status != TeleSessionStatus.NoShowClinician)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync(ct);
        if (existing != null) return existing;

        // Only auto-create a session if the patient has an active package — otherwise they must go
        // through the per-consult Pay flow (existing RequestTelemed path).
        var pkg = await GetActivePackageAsync(patientId, ct);
        if (!pkg.IsActive) return null;

        var token = Guid.NewGuid().ToString("N");
        var year = DateTime.UtcNow.Year;
        var prefix = $"TM-{year}-";
        var last = await _db.TeleSessions
            .Where(s => s.FacilityId == facilityId && s.SessionNumber.StartsWith(prefix))
            .OrderByDescending(s => s.SessionNumber).Select(s => s.SessionNumber).FirstOrDefaultAsync(ct);
        var n = 1;
        if (!string.IsNullOrEmpty(last) && int.TryParse(last[prefix.Length..], out var parsed)) n = parsed + 1;
        var session = new TeleSession
        {
            FacilityId = facilityId,
            SessionNumber = $"{prefix}{n:D5}",
            PatientId = patientId,
            Mode = TeleSessionMode.Chat,
            Status = TeleSessionStatus.Requested,
            ScheduledStartUtc = DateTime.UtcNow,
            RoomToken = token,
            JoinUrl = $"/Telemedicine/Room/{token}",
            ConsultationReason = "Chat consult (covered by chat package)"
        };
        _db.TeleSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return session;
    }

    public async Task<ChatPackageInfo> GetActivePackageAsync(int patientId, CancellationToken ct = default)
    {
        var pkg = await _db.ChatPackages.AsNoTracking()
            .Where(p => p.PatientId == patientId && p.ExpiresAt > DateTime.UtcNow && p.BillId != null
                && p.Bill!.Status == BillStatus.Paid)
            .OrderByDescending(p => p.PurchasedAt)
            .FirstOrDefaultAsync(ct);
        if (pkg is null) return new ChatPackageInfo(null, null, DefaultChatPackagePrice, null, null, false);
        return new ChatPackageInfo(pkg.Id, pkg.PackageNumber, pkg.Price, pkg.PurchasedAt, pkg.ExpiresAt, true);
    }

    public async Task<int> CreateChatPackageBillAsync(int facilityId, int patientId, decimal price, IBillingService billing, CancellationToken ct = default)
    {
        if (price <= 0) price = DefaultChatPackagePrice;
        var packageNumber = await NextPackageNumberAsync(facilityId, ct);
        var pkg = new ChatPackage
        {
            FacilityId = facilityId,
            PatientId = patientId,
            PackageNumber = packageNumber,
            Price = price,
            PurchasedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.Add(PackageDuration)
        };
        _db.ChatPackages.Add(pkg);
        await _db.SaveChangesAsync(ct);

        var billItem = new BillItem
        {
            Kind = BillItemKind.Consultation,
            Description = $"24-hour chat package — {packageNumber}",
            ServiceCode = "CHAT-PKG-24H",
            Quantity = 1,
            UnitPrice = price
        };
        var billId = await billing.CreateAdHocBillAsync(facilityId, patientId, new[] { billItem }, "system", ct);
        pkg.BillId = billId;
        await _db.SaveChangesAsync(ct);
        return billId;
    }

    public async Task<int?> ActivatePackageForBillAsync(int billId, CancellationToken ct = default)
    {
        var pkg = await _db.ChatPackages.FirstOrDefaultAsync(p => p.BillId == billId, ct);
        if (pkg is null) return null;
        // Reset the validity window to "now + 24h" only on the moment of activation, so a patient who
        // bought ahead of time gets the full 24-hour window from when payment lands.
        pkg.PurchasedAt = DateTime.UtcNow;
        pkg.ExpiresAt = DateTime.UtcNow.Add(PackageDuration);
        await _db.SaveChangesAsync(ct);
        return pkg.Id;
    }

    private async Task<string> NextPackageNumberAsync(int facilityId, CancellationToken ct)
    {
        var prefix = $"CPKG-{DateTime.UtcNow.Year}-";
        var last = await _db.ChatPackages
            .Where(p => p.FacilityId == facilityId && p.PackageNumber.StartsWith(prefix))
            .OrderByDescending(p => p.PackageNumber).Select(p => p.PackageNumber).FirstOrDefaultAsync(ct);
        var n = 1;
        if (!string.IsNullOrEmpty(last) && int.TryParse(last[prefix.Length..], out var parsed)) n = parsed + 1;
        return $"{prefix}{n:D5}";
    }
}
