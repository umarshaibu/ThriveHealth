using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Integrations;

namespace ThriveHealth.Web.Services.Integrations;

public record EmailSendRequest(int FacilityId, string ToEmail, string? ToName, string Subject, string BodyHtml,
    MessagePurpose Purpose, int? PatientId = null, string? CreatedById = null);

public interface IEmailGateway
{
    string ProviderName { get; }
    Task<long> EnqueueAsync(EmailSendRequest request, CancellationToken ct = default);
    Task<int> ProcessQueueAsync(int max = 50, CancellationToken ct = default);
}

public class LoggingEmailGateway : IEmailGateway
{
    public string ProviderName => "Logging (no-op)";
    private readonly ApplicationDbContext _db;
    private readonly ILogger<LoggingEmailGateway> _log;

    public LoggingEmailGateway(ApplicationDbContext db, ILogger<LoggingEmailGateway> log) { _db = db; _log = log; }

    public async Task<long> EnqueueAsync(EmailSendRequest req, CancellationToken ct = default)
    {
        var msg = new EmailMessage
        {
            FacilityId = req.FacilityId,
            ToEmail = req.ToEmail,
            ToName = req.ToName,
            Subject = req.Subject,
            BodyHtml = req.BodyHtml,
            Purpose = req.Purpose,
            PatientId = req.PatientId,
            CreatedById = req.CreatedById,
            Provider = ProviderName,
            Status = MessageStatus.Queued
        };
        _db.EmailMessages.Add(msg);
        await _db.SaveChangesAsync(ct);
        return msg.Id;
    }

    public async Task<int> ProcessQueueAsync(int max = 50, CancellationToken ct = default)
    {
        var queued = await _db.EmailMessages
            .Where(m => m.Status == MessageStatus.Queued)
            .OrderBy(m => m.CreatedAt).Take(max).ToListAsync(ct);
        foreach (var m in queued)
        {
            _log.LogInformation("[Email:noop] To={Email} Purpose={Purpose} Subject={Subject}", m.ToEmail, m.Purpose, m.Subject);
            m.Status = MessageStatus.Sent;
            m.SentAt = DateTime.UtcNow;
            m.ProviderMessageId = $"NOOP-{Guid.NewGuid():N}".Substring(0, 32);
            m.ProviderResponse = "Logged (no real provider configured)";
        }
        if (queued.Count > 0) await _db.SaveChangesAsync(ct);
        return queued.Count;
    }
}
