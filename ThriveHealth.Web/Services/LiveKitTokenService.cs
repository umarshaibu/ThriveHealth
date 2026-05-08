using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using ThriveHealth.Web.Models.Telemedicine;

namespace ThriveHealth.Web.Services;

public interface ILiveKitTokenService
{
    bool IsConfigured { get; }
    string ServerUrl { get; }
    string IssueAccessToken(TeleSession session, string participantIdentity, string displayName, bool canPublish);
    string RoomName(TeleSession session);
}

public class LiveKitOptions
{
    public string Url { get; set; } = string.Empty;
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public int TokenLifetimeMinutes { get; set; } = 90;
}

public class LiveKitTokenService : ILiveKitTokenService
{
    private readonly LiveKitOptions _opts;

    public LiveKitTokenService(IConfiguration config)
    {
        _opts = config.GetSection("Telemedicine:LiveKit").Get<LiveKitOptions>() ?? new LiveKitOptions();
    }

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(_opts.Url) &&
        !string.IsNullOrWhiteSpace(_opts.ApiKey) &&
        !string.IsNullOrWhiteSpace(_opts.ApiSecret);

    public string ServerUrl => _opts.Url;

    public string RoomName(TeleSession session) => $"th-{session.FacilityId}-{session.Id}-{session.RoomToken}";

    /// <summary>
    /// Mints a LiveKit-compatible JWT (HS256) granting the participant access to a single room.
    /// LiveKit's JWT format is documented at https://docs.livekit.io/home/get-started/authentication/.
    /// </summary>
    public string IssueAccessToken(TeleSession session, string participantIdentity, string displayName, bool canPublish)
    {
        if (!IsConfigured)
            throw new InvalidOperationException("LiveKit is not configured. Set Telemedicine:LiveKit:{Url,ApiKey,ApiSecret} in appsettings or user-secrets.");

        var nowSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var lifetime = _opts.TokenLifetimeMinutes <= 0 ? 90 : _opts.TokenLifetimeMinutes;
        var expSeconds = nowSeconds + lifetime * 60;

        var header = new JwtHeader();
        var payload = new JwtPayload
        {
            Issuer = _opts.ApiKey,
            Subject = participantIdentity,
            Name = displayName,
            IssuedAt = nowSeconds,
            NotBefore = nowSeconds,
            Expiration = expSeconds,
            Video = new JwtVideoGrant
            {
                Room = RoomName(session),
                RoomJoin = true,
                CanPublish = canPublish,
                CanSubscribe = true,
                CanPublishData = true
            }
        };

        var encodedHeader = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(header, JsonOpts));
        var encodedPayload = Base64UrlEncode(JsonSerializer.SerializeToUtf8Bytes(payload, JsonOpts));
        var signingInput = $"{encodedHeader}.{encodedPayload}";
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(_opts.ApiSecret));
        var signature = hmac.ComputeHash(Encoding.UTF8.GetBytes(signingInput));
        return $"{signingInput}.{Base64UrlEncode(signature)}";
    }

    private static readonly JsonSerializerOptions JsonOpts = new(JsonSerializerDefaults.Web)
    {
        DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull
    };

    private static string Base64UrlEncode(byte[] bytes) =>
        Convert.ToBase64String(bytes).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private sealed class JwtHeader
    {
        [JsonPropertyName("alg")] public string Alg { get; set; } = "HS256";
        [JsonPropertyName("typ")] public string Typ { get; set; } = "JWT";
    }

    private sealed class JwtPayload
    {
        [JsonPropertyName("iss")] public string Issuer { get; set; } = string.Empty;
        [JsonPropertyName("sub")] public string Subject { get; set; } = string.Empty;
        [JsonPropertyName("name")] public string Name { get; set; } = string.Empty;
        [JsonPropertyName("iat")] public long IssuedAt { get; set; }
        [JsonPropertyName("nbf")] public long NotBefore { get; set; }
        [JsonPropertyName("exp")] public long Expiration { get; set; }
        [JsonPropertyName("video")] public JwtVideoGrant Video { get; set; } = new();
    }

    private sealed class JwtVideoGrant
    {
        [JsonPropertyName("room")] public string Room { get; set; } = string.Empty;
        [JsonPropertyName("roomJoin")] public bool RoomJoin { get; set; }
        [JsonPropertyName("canPublish")] public bool CanPublish { get; set; }
        [JsonPropertyName("canSubscribe")] public bool CanSubscribe { get; set; }
        [JsonPropertyName("canPublishData")] public bool CanPublishData { get; set; }
    }
}
