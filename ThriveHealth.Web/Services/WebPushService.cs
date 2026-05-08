using System.Text.Json;
using Lib.Net.Http.WebPush;
using Lib.Net.Http.WebPush.Authentication;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Integrations;

namespace ThriveHealth.Web.Services;

public record WebPushPayload(string Title, string Body, string? Url, string? Tag);

public interface IWebPushService
{
    string PublicKey { get; }
    bool IsConfigured { get; }

    Task<long> SubscribeAsync(PushOwnerType ownerType, string ownerKey, string endpoint, string p256dh, string auth, string? userAgent, int? facilityId, CancellationToken ct = default);
    Task UnsubscribeAsync(string endpoint, CancellationToken ct = default);
    Task SendToOwnerAsync(PushOwnerType ownerType, string ownerKey, WebPushPayload payload, CancellationToken ct = default);
}

/// <summary>
/// Web Push (PWA) sender. Generates a VAPID key pair on first run and persists it under
/// <c>App_Data/vapid-keys.json</c> so the same public key serves browsers across restarts.
/// Production environments can override by setting <c>WebPush:PublicKey</c> + <c>WebPush:PrivateKey</c>
/// in config.
/// </summary>
public class WebPushService : IWebPushService
{
    private readonly ApplicationDbContext _db;
    private readonly PushServiceClient _push;
    private readonly VapidAuthentication _vapid;
    private readonly string _publicKey;
    private readonly string _privateKey;
    private readonly string _subject;
    private readonly ILogger<WebPushService> _log;

    public WebPushService(ApplicationDbContext db, IHttpClientFactory httpFactory, IConfiguration config,
        IWebHostEnvironment env, ILogger<WebPushService> log)
    {
        _db = db; _log = log;
        _subject = config["WebPush:Subject"] ?? "mailto:noreply@thrivehealth.local";

        var (pub, priv) = LoadOrGenerateKeys(config, env);
        _publicKey = pub;
        _privateKey = priv;
        _vapid = new VapidAuthentication(_publicKey, _privateKey) { Subject = _subject };
        _push = new PushServiceClient(httpFactory.CreateClient("webpush")) { DefaultAuthentication = _vapid };
    }

    public string PublicKey => _publicKey;
    public bool IsConfigured => !string.IsNullOrEmpty(_publicKey) && !string.IsNullOrEmpty(_privateKey);

    public async Task<long> SubscribeAsync(PushOwnerType ownerType, string ownerKey, string endpoint, string p256dh, string auth, string? userAgent, int? facilityId, CancellationToken ct = default)
    {
        var existing = await _db.WebPushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint, ct);
        if (existing is not null)
        {
            existing.OwnerType = ownerType;
            existing.OwnerKey = ownerKey;
            existing.P256dhKey = p256dh;
            existing.AuthKey = auth;
            existing.UserAgent = userAgent;
            existing.FacilityId = facilityId;
            existing.LastUsedAt = DateTime.UtcNow;
            existing.FailedAt = null;
            existing.FailureReason = null;
            await _db.SaveChangesAsync(ct);
            return existing.Id;
        }
        var sub = new WebPushSubscription
        {
            OwnerType = ownerType,
            OwnerKey = ownerKey,
            Endpoint = endpoint,
            P256dhKey = p256dh,
            AuthKey = auth,
            UserAgent = userAgent,
            FacilityId = facilityId,
            CreatedAt = DateTime.UtcNow
        };
        _db.WebPushSubscriptions.Add(sub);
        await _db.SaveChangesAsync(ct);
        return sub.Id;
    }

    public async Task UnsubscribeAsync(string endpoint, CancellationToken ct = default)
    {
        var sub = await _db.WebPushSubscriptions.FirstOrDefaultAsync(s => s.Endpoint == endpoint, ct);
        if (sub is null) return;
        _db.WebPushSubscriptions.Remove(sub);
        await _db.SaveChangesAsync(ct);
    }

    public async Task SendToOwnerAsync(PushOwnerType ownerType, string ownerKey, WebPushPayload payload, CancellationToken ct = default)
    {
        if (!IsConfigured) return;
        var subs = await _db.WebPushSubscriptions.AsNoTracking()
            .Where(s => s.OwnerType == ownerType && s.OwnerKey == ownerKey)
            .ToListAsync(ct);
        if (subs.Count == 0) return;

        var message = new PushMessage(JsonSerializer.Serialize(payload))
        {
            Topic = payload.Tag,
            Urgency = PushMessageUrgency.High,
            TimeToLive = 60 * 60
        };

        foreach (var s in subs)
        {
            var subscription = new PushSubscription
            {
                Endpoint = s.Endpoint,
                Keys = new Dictionary<string, string> { ["p256dh"] = s.P256dhKey, ["auth"] = s.AuthKey }
            };
            try
            {
                await _push.RequestPushMessageDeliveryAsync(subscription, message, ct);
                var tracked = await _db.WebPushSubscriptions.FirstOrDefaultAsync(x => x.Id == s.Id, ct);
                if (tracked is not null) { tracked.LastUsedAt = DateTime.UtcNow; await _db.SaveChangesAsync(ct); }
            }
            catch (Lib.Net.Http.WebPush.PushServiceClientException ex)
                when ((int)ex.StatusCode == 404 || (int)ex.StatusCode == 410)
            {
                // Subscription expired or unsubscribed at the browser end — clean up.
                _db.WebPushSubscriptions.Remove(new WebPushSubscription { Id = s.Id });
                try { await _db.SaveChangesAsync(ct); } catch { /* race; ignore */ }
                _log.LogInformation("Removed expired push subscription {Id} ({Status})", s.Id, ex.StatusCode);
            }
            catch (Exception ex)
            {
                _log.LogWarning(ex, "Push send failed for subscription {Id}", s.Id);
                var tracked = await _db.WebPushSubscriptions.FirstOrDefaultAsync(x => x.Id == s.Id, ct);
                if (tracked is not null)
                {
                    tracked.FailedAt = DateTime.UtcNow;
                    tracked.FailureReason = ex.Message[..Math.Min(300, ex.Message.Length)];
                    try { await _db.SaveChangesAsync(ct); } catch { /* race; ignore */ }
                }
            }
        }
    }

    private static (string pub, string priv) LoadOrGenerateKeys(IConfiguration config, IWebHostEnvironment env)
    {
        // Configured keys win.
        var configPub = config["WebPush:PublicKey"];
        var configPriv = config["WebPush:PrivateKey"];
        if (!string.IsNullOrEmpty(configPub) && !string.IsNullOrEmpty(configPriv))
            return (configPub, configPriv);

        // Otherwise persist a generated pair under App_Data so subsequent restarts are stable.
        var dir = Path.Combine(env.ContentRootPath, "App_Data");
        Directory.CreateDirectory(dir);
        var path = Path.Combine(dir, "vapid-keys.json");
        if (File.Exists(path))
        {
            var json = File.ReadAllText(path);
            var loaded = JsonSerializer.Deserialize<KeyPairFile>(json);
            if (loaded is not null && !string.IsNullOrEmpty(loaded.Public) && !string.IsNullOrEmpty(loaded.Private))
                return (loaded.Public, loaded.Private);
        }
        var (genPub, genPriv) = GenerateEcdhP256KeyPair();
        File.WriteAllText(path, JsonSerializer.Serialize(new KeyPairFile { Public = genPub, Private = genPriv }, new JsonSerializerOptions { WriteIndented = true }));
        return (genPub, genPriv);
    }

    /// <summary>
    /// Generates a P-256 ECDH key pair in the format the Web Push VAPID spec expects:
    /// public key is 65 raw bytes (0x04 || X || Y) base64url; private key is 32 raw D bytes base64url.
    /// </summary>
    private static (string pub, string priv) GenerateEcdhP256KeyPair()
    {
        using var ec = System.Security.Cryptography.ECDsa.Create(System.Security.Cryptography.ECCurve.NamedCurves.nistP256);
        var p = ec.ExportParameters(true);
        var pub = new byte[65];
        pub[0] = 0x04;
        Buffer.BlockCopy(p.Q.X!, 0, pub, 1, 32);
        Buffer.BlockCopy(p.Q.Y!, 0, pub, 33, 32);
        return (Base64UrlEncode(pub), Base64UrlEncode(p.D!));
    }

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class KeyPairFile { public string Public { get; set; } = ""; public string Private { get; set; } = ""; }
}
