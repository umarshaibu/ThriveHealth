using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Integrations;

namespace ThriveHealth.Web.Services.Integrations;

public record SmsSendRequest(int FacilityId, string ToPhone, string Body, MessagePurpose Purpose,
    int? PatientId = null, string? CreatedById = null);

public interface ISmsGateway
{
    string ProviderName { get; }
    Task<long> EnqueueAsync(SmsSendRequest request, CancellationToken ct = default);
    Task<int> ProcessQueueAsync(int max = 50, CancellationToken ct = default);
}

public class LoggingSmsGateway : ISmsGateway
{
    public string ProviderName => "Logging (no-op)";
    private readonly ApplicationDbContext _db;
    private readonly ILogger<LoggingSmsGateway> _log;

    public LoggingSmsGateway(ApplicationDbContext db, ILogger<LoggingSmsGateway> log) { _db = db; _log = log; }

    public async Task<long> EnqueueAsync(SmsSendRequest req, CancellationToken ct = default)
    {
        var msg = new SmsMessage
        {
            FacilityId = req.FacilityId,
            ToPhone = NormalisePhone(req.ToPhone),
            Body = req.Body.Length > 640 ? req.Body[..640] : req.Body,
            Purpose = req.Purpose,
            PatientId = req.PatientId,
            CreatedById = req.CreatedById,
            Provider = ProviderName,
            Status = MessageStatus.Queued
        };
        _db.SmsMessages.Add(msg);
        await _db.SaveChangesAsync(ct);
        return msg.Id;
    }

    public async Task<int> ProcessQueueAsync(int max = 50, CancellationToken ct = default)
    {
        var queued = await _db.SmsMessages
            .Where(m => m.Status == MessageStatus.Queued)
            .OrderBy(m => m.CreatedAt)
            .Take(max).ToListAsync(ct);
        foreach (var m in queued)
        {
            // No real network call — log and mark as Sent. A real provider (Termii / Africa's Talking) would POST here.
            _log.LogInformation("[SMS:noop] To={Phone} Purpose={Purpose} Body={Body}", m.ToPhone, m.Purpose, m.Body);
            m.Status = MessageStatus.Sent;
            m.SentAt = DateTime.UtcNow;
            m.ProviderMessageId = $"NOOP-{Guid.NewGuid():N}".Substring(0, 24);
            m.ProviderResponse = "Logged (no real provider configured)";
        }
        if (queued.Count > 0) await _db.SaveChangesAsync(ct);
        return queued.Count;
    }

    private static string NormalisePhone(string p)
    {
        var s = (p ?? "").Trim().Replace(" ", "").Replace("-", "");
        if (s.StartsWith("0") && s.Length == 11) return "+234" + s.Substring(1);
        if (s.StartsWith("234") && s.Length == 13) return "+" + s;
        return s;
    }
}
