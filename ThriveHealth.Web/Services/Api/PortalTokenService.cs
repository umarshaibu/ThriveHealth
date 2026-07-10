using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Api;
using ThriveHealth.Web.Models.Portal;

namespace ThriveHealth.Web.Services.Api;

/// <summary>Bearer-token auth surface for native/mobile patient clients (separate from the cookie portal).</summary>
public static class PortalJwt
{
    public const string Scheme = "PortalJwt";

    public const string ClaimPortalAccountId = "portal_account_id";
    public const string ClaimPatientId = "patient_id";
    public const string ClaimFacilityId = "facility_id";
    public const string ClaimTenantId = "tenant_id";
    public const string ClaimTenantSlug = "tenant_slug";

    // Distinguishes the two principal kinds carried by the (shared) bearer scheme.
    public const string ClaimKind = "thk";
    public const string ClaimUserId = "user_id";
    public const string KindPatient = "patient";
    public const string KindStaff = "staff";
}

public class JwtOptions
{
    public const string SectionName = "Jwt";

    public string Issuer { get; set; } = "ThriveHealth";
    public string Audience { get; set; } = "ThriveHealth.Mobile";
    /// <summary>HMAC-SHA256 signing key. MUST be overridden in production via config/secrets.</summary>
    public string SigningKey { get; set; } = string.Empty;
    public int AccessTokenMinutes { get; set; } = 30;
    public int RefreshTokenDays { get; set; } = 30;
}

public interface IPortalTokenService
{
    /// <summary>Issues a fresh access+refresh pair for a validated account and persists the refresh token.</summary>
    Task<ApiTokenResponse> IssueAsync(PortalAccount account, string? ip, string? userAgent, CancellationToken ct = default);

    /// <summary>Validates and rotates a refresh token, returning a new pair. Returns null if invalid/expired/revoked.</summary>
    Task<ApiTokenResponse?> RefreshAsync(string refreshToken, string? ip, string? userAgent, CancellationToken ct = default);

    /// <summary>Revokes a single refresh token (logout on this device). No-op if not found.</summary>
    Task RevokeAsync(string refreshToken, CancellationToken ct = default);
}

public class PortalTokenService : IPortalTokenService
{
    private readonly ApplicationDbContext _db;
    private readonly JwtOptions _opts;
    private readonly SigningCredentials _creds;

    public PortalTokenService(ApplicationDbContext db, Microsoft.Extensions.Options.IOptions<JwtOptions> opts)
    {
        _db = db;
        _opts = opts.Value;
        _creds = new SigningCredentials(SigningKeyBytes(_opts.SigningKey), SecurityAlgorithms.HmacSha256);
    }

    internal static SymmetricSecurityKey SigningKeyBytes(string key)
    {
        if (string.IsNullOrWhiteSpace(key) || key.Length < 32)
            // Dev fallback so the app boots without secrets configured. Never hit in prod once Jwt:SigningKey is set.
            key = "thrivehealth-dev-only-insecure-signing-key-change-me";
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(key));
    }

    public async Task<ApiTokenResponse> IssueAsync(PortalAccount account, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var patient = account.Patient
            ?? await _db.Patients.AsNoTracking().FirstAsync(p => p.Id == account.PatientId, ct);

        var facility = await _db.Facilities.AsNoTracking()
            .Where(f => f.Id == patient.FacilityId)
            .Select(f => new { f.Id, f.TenantId, f.Name })
            .FirstOrDefaultAsync(ct);

        var tenantSlug = facility is null ? null : await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == facility.TenantId).Select(t => t.Slug).FirstOrDefaultAsync(ct);

        var (access, expiresIn) = BuildAccessToken(account, patient.FacilityId, facility?.TenantId ?? 0, tenantSlug);
        var rawRefresh = await IssueRefreshTokenAsync(account.Id, ip, userAgent, ct);

        var profile = new ApiPatientProfile(
            patient.Id, patient.HospitalNumber, patient.FullName, patient.Gender,
            patient.DateOfBirth?.ToString("yyyy-MM-dd"), patient.Phone, patient.Email,
            patient.FacilityId, facility?.Name ?? "", facility?.TenantId ?? 0);

        return new ApiTokenResponse(access, rawRefresh, "Bearer", expiresIn, profile);
    }

    public async Task<ApiTokenResponse?> RefreshAsync(string refreshToken, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var hash = Sha256Hex(refreshToken);
        // Refresh runs anonymously (no tenant resolved); ignore the tenant query filter so the
        // joined PortalAccount/Patient aren't filtered out. The token hash is the security boundary.
        var stored = await _db.PortalRefreshTokens
            .IgnoreQueryFilters()
            .Include(t => t.PortalAccount).ThenInclude(a => a!.Patient)
            .FirstOrDefaultAsync(t => t.TokenHash == hash, ct);

        if (stored is null || !stored.IsActive || stored.PortalAccount is null || !stored.PortalAccount.IsActive)
            return null;

        // Rotate: revoke the presented token and issue a fresh one.
        stored.RevokedAt = DateTime.UtcNow;
        var account = stored.PortalAccount;
        var newRaw = await IssueRefreshTokenAsync(account.Id, ip, userAgent, ct, replaces: stored);

        var patient = account.Patient!;
        var facility = await _db.Facilities.AsNoTracking()
            .Where(f => f.Id == patient.FacilityId)
            .Select(f => new { f.Id, f.TenantId, f.Name })
            .FirstOrDefaultAsync(ct);
        var tenantSlug = facility is null ? null : await _db.Tenants.AsNoTracking()
            .Where(t => t.Id == facility.TenantId).Select(t => t.Slug).FirstOrDefaultAsync(ct);

        var (access, expiresIn) = BuildAccessToken(account, patient.FacilityId, facility?.TenantId ?? 0, tenantSlug);
        await _db.SaveChangesAsync(ct);

        var profile = new ApiPatientProfile(
            patient.Id, patient.HospitalNumber, patient.FullName, patient.Gender,
            patient.DateOfBirth?.ToString("yyyy-MM-dd"), patient.Phone, patient.Email,
            patient.FacilityId, facility?.Name ?? "", facility?.TenantId ?? 0);

        return new ApiTokenResponse(access, newRaw, "Bearer", expiresIn, profile);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = Sha256Hex(refreshToken);
        var stored = await _db.PortalRefreshTokens.FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    private (string token, int expiresInSeconds) BuildAccessToken(PortalAccount account, int facilityId, int tenantId, string? tenantSlug)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_opts.AccessTokenMinutes);

        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, account.Id.ToString()),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Email, account.Email),
            new(PortalJwt.ClaimKind, PortalJwt.KindPatient),
            new(PortalJwt.ClaimPortalAccountId, account.Id.ToString()),
            new(PortalJwt.ClaimPatientId, account.PatientId.ToString()),
            new(PortalJwt.ClaimFacilityId, facilityId.ToString()),
            new(PortalJwt.ClaimTenantId, tenantId.ToString()),
        };
        if (!string.IsNullOrEmpty(tenantSlug))
            claims.Add(new Claim(PortalJwt.ClaimTenantSlug, tenantSlug));

        var token = new JwtSecurityToken(
            issuer: _opts.Issuer, audience: _opts.Audience,
            claims: claims, notBefore: now, expires: expires, signingCredentials: _creds);

        return (new JwtSecurityTokenHandler().WriteToken(token), _opts.AccessTokenMinutes * 60);
    }

    private async Task<string> IssueRefreshTokenAsync(int accountId, string? ip, string? userAgent, CancellationToken ct, PortalRefreshToken? replaces = null)
    {
        var raw = Base64UrlRandom(32);
        var hash = Sha256Hex(raw);
        _db.PortalRefreshTokens.Add(new PortalRefreshToken
        {
            PortalAccountId = accountId,
            TokenHash = hash,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_opts.RefreshTokenDays),
            CreatedByIp = ip,
            UserAgent = userAgent?.Length > 300 ? userAgent[..300] : userAgent
        });
        if (replaces is not null) replaces.ReplacedByTokenHash = hash;
        await _db.SaveChangesAsync(ct);
        return raw;
    }

    private static string Base64UrlRandom(int bytes)
    {
        var buf = RandomNumberGenerator.GetBytes(bytes);
        return Convert.ToBase64String(buf).TrimEnd('=').Replace('+', '-').Replace('/', '_');
    }

    private static string Sha256Hex(string input)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes(input));
        return Convert.ToHexString(bytes).ToLowerInvariant();
    }
}
