using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Api;
using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Services.Api;

public interface IStaffTokenService
{
    Task<ApiStaffTokenResponse> IssueAsync(ApplicationUser user, string? ip, string? userAgent, CancellationToken ct = default);
    Task<ApiStaffTokenResponse?> RefreshAsync(string refreshToken, string? ip, string? userAgent, CancellationToken ct = default);
    Task RevokeAsync(string refreshToken, CancellationToken ct = default);
}

public class StaffTokenService : IStaffTokenService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly JwtOptions _opts;
    private readonly SigningCredentials _creds;

    public StaffTokenService(ApplicationDbContext db, UserManager<ApplicationUser> users, IOptions<JwtOptions> opts)
    {
        _db = db; _users = users; _opts = opts.Value;
        _creds = new SigningCredentials(PortalTokenService.SigningKeyBytes(_opts.SigningKey), SecurityAlgorithms.HmacSha256);
    }

    public async Task<ApiStaffTokenResponse> IssueAsync(ApplicationUser user, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var roles = await _users.GetRolesAsync(user);
        var profile = await BuildProfileAsync(user, roles, ct);
        var (access, expiresIn) = BuildAccessToken(user, roles, profile.TenantId, profile.FacilityId);
        var refresh = await IssueRefreshTokenAsync(user.Id, ip, userAgent, ct);
        return new ApiStaffTokenResponse(access, refresh, "Bearer", expiresIn, profile);
    }

    public async Task<ApiStaffTokenResponse?> RefreshAsync(string refreshToken, string? ip, string? userAgent, CancellationToken ct = default)
    {
        var hash = Sha256Hex(refreshToken);
        var stored = await _db.StaffRefreshTokens.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is null || !stored.IsActive) return null;

        var user = await _users.FindByIdAsync(stored.UserId);
        if (user is null || !user.IsActive) return null;

        stored.RevokedAt = DateTime.UtcNow;
        var newRaw = await IssueRefreshTokenAsync(user.Id, ip, userAgent, ct, replaces: stored);

        var roles = await _users.GetRolesAsync(user);
        var profile = await BuildProfileAsync(user, roles, ct);
        var (access, expiresIn) = BuildAccessToken(user, roles, profile.TenantId, profile.FacilityId);
        await _db.SaveChangesAsync(ct);
        return new ApiStaffTokenResponse(access, newRaw, "Bearer", expiresIn, profile);
    }

    public async Task RevokeAsync(string refreshToken, CancellationToken ct = default)
    {
        var hash = Sha256Hex(refreshToken);
        var stored = await _db.StaffRefreshTokens.IgnoreQueryFilters().FirstOrDefaultAsync(t => t.TokenHash == hash, ct);
        if (stored is { RevokedAt: null })
        {
            stored.RevokedAt = DateTime.UtcNow;
            await _db.SaveChangesAsync(ct);
        }
    }

    private async Task<ApiStaffProfile> BuildProfileAsync(ApplicationUser user, IList<string> roles, CancellationToken ct)
    {
        var facilityId = user.FacilityId ?? 0;
        var facility = facilityId == 0 ? null : await _db.Facilities.AsNoTracking()
            .Where(f => f.Id == facilityId).Select(f => new { f.Name, f.TenantId }).FirstOrDefaultAsync(ct);
        var tenantId = user.TenantId ?? facility?.TenantId ?? 0;
        var isConsultant = roles.Contains(Roles.Consultant);
        return new ApiStaffProfile(user.Id, user.FullName, user.Email, user.Designation, user.Department,
            facilityId, facility?.Name ?? "", tenantId, roles.ToList(), isConsultant);
    }

    private (string token, int expiresInSeconds) BuildAccessToken(ApplicationUser user, IList<string> roles, int tenantId, int facilityId)
    {
        var now = DateTime.UtcNow;
        var expires = now.AddMinutes(_opts.AccessTokenMinutes);
        var claims = new List<Claim>
        {
            new(JwtRegisteredClaimNames.Sub, user.Id),
            new(JwtRegisteredClaimNames.Jti, Guid.NewGuid().ToString()),
            new(ClaimTypes.NameIdentifier, user.Id),
            new(PortalJwt.ClaimKind, PortalJwt.KindStaff),
            new(PortalJwt.ClaimUserId, user.Id),
            new(PortalJwt.ClaimFacilityId, facilityId.ToString()),
            new(PortalJwt.ClaimTenantId, tenantId.ToString()),
        };
        if (!string.IsNullOrEmpty(user.Email)) claims.Add(new Claim(ClaimTypes.Email, user.Email));
        foreach (var r in roles) claims.Add(new Claim(ClaimTypes.Role, r));

        var token = new JwtSecurityToken(_opts.Issuer, _opts.Audience, claims, now, expires, _creds);
        return (new JwtSecurityTokenHandler().WriteToken(token), _opts.AccessTokenMinutes * 60);
    }

    private async Task<string> IssueRefreshTokenAsync(string userId, string? ip, string? userAgent, CancellationToken ct, StaffRefreshToken? replaces = null)
    {
        var raw = Base64UrlRandom(32);
        var hash = Sha256Hex(raw);
        _db.StaffRefreshTokens.Add(new StaffRefreshToken
        {
            UserId = userId, TokenHash = hash, CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddDays(_opts.RefreshTokenDays),
            CreatedByIp = ip, UserAgent = userAgent?.Length > 300 ? userAgent[..300] : userAgent
        });
        if (replaces is not null) replaces.ReplacedByTokenHash = hash;
        await _db.SaveChangesAsync(ct);
        return raw;
    }

    private static string Base64UrlRandom(int bytes) =>
        Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private static string Sha256Hex(string input) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(input))).ToLowerInvariant();
}
