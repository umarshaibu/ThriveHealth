using FirebaseAdmin;
using FirebaseAdmin.Messaging;
using Google.Apis.Auth.OAuth2;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Portal;

namespace ThriveHealth.Web.Services.Api;

/// <summary>
/// Native push for the patient app via Firebase Cloud Messaging (FCM). Sends are gated on a
/// configured service account (<c>Push:Fcm:ServiceAccountPath</c>) — without it, registration still
/// works and sends are logged no-ops, so the app runs end-to-end before Firebase is provisioned.
/// </summary>
public interface IDevicePushService
{
    Task RegisterAsync(int patientId, string token, string? platform, CancellationToken ct = default);
    Task UnregisterAsync(string token, CancellationToken ct = default);
    Task SendToPatientAsync(int patientId, string title, string body, IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default);
}

public class FcmPushService : IDevicePushService
{
    private static readonly object InitLock = new();
    private static bool _initAttempted;
    private static FirebaseApp? _app;

    private readonly ApplicationDbContext _db;
    private readonly IConfiguration _config;
    private readonly ILogger<FcmPushService> _log;

    public FcmPushService(ApplicationDbContext db, IConfiguration config, ILogger<FcmPushService> log)
    {
        _db = db; _config = config; _log = log;
    }

    private FirebaseApp? Firebase()
    {
        if (_initAttempted) return _app;
        lock (InitLock)
        {
            if (_initAttempted) return _app;
            _initAttempted = true;
            var path = _config["Push:Fcm:ServiceAccountPath"];
            if (!string.IsNullOrWhiteSpace(path) && File.Exists(path))
            {
                _app = FirebaseApp.DefaultInstance
                    ?? FirebaseApp.Create(new AppOptions { Credential = GoogleCredential.FromFile(path) });
            }
            else
            {
                _log.LogInformation("[Push] FCM service account not configured — push sends are no-ops.");
            }
            return _app;
        }
    }

    public async Task RegisterAsync(int patientId, string token, string? platform, CancellationToken ct = default)
    {
        var existing = await _db.PortalDeviceTokens.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.Token == token, ct);
        if (existing is null)
        {
            _db.PortalDeviceTokens.Add(new PortalDeviceToken
            {
                PatientId = patientId, Token = token, Platform = platform,
                CreatedAt = DateTime.UtcNow, LastSeenAt = DateTime.UtcNow
            });
        }
        else
        {
            existing.PatientId = patientId;
            existing.Platform = platform ?? existing.Platform;
            existing.LastSeenAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task UnregisterAsync(string token, CancellationToken ct = default)
    {
        var rows = await _db.PortalDeviceTokens.IgnoreQueryFilters().Where(t => t.Token == token).ToListAsync(ct);
        if (rows.Count > 0)
        {
            _db.PortalDeviceTokens.RemoveRange(rows);
            await _db.SaveChangesAsync(ct);
        }
    }

    public async Task SendToPatientAsync(int patientId, string title, string body, IReadOnlyDictionary<string, string>? data = null, CancellationToken ct = default)
    {
        try
        {
            var app = Firebase();
            if (app is null) return; // not configured — no-op

            var tokens = await _db.PortalDeviceTokens.IgnoreQueryFilters().AsNoTracking()
                .Where(t => t.PatientId == patientId).Select(t => t.Token).ToListAsync(ct);
            if (tokens.Count == 0) return;

            var message = new MulticastMessage
            {
                Tokens = tokens,
                Notification = new Notification { Title = title, Body = body },
                Data = data?.ToDictionary(kv => kv.Key, kv => kv.Value),
            };

            var resp = await FirebaseMessaging.GetMessaging(app).SendEachForMulticastAsync(message, ct);
            if (resp.FailureCount > 0)
                _log.LogWarning("[Push] FCM: {Fail}/{Total} sends failed for patient {Patient}", resp.FailureCount, tokens.Count, patientId);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "[Push] FCM send threw for patient {Patient}", patientId);
        }
    }
}
