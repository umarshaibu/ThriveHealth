using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Portal;

namespace ThriveHealth.Web.Services;

public static class PortalAuth
{
    public const string Scheme = "PortalCookie";
    public const string ClaimPortalAccountId = "portal-account-id";
    public const string ClaimPatientId = "patient-id";
    public const string ClaimFacilityId = "facility-id";
}

public interface IPortalAuthService
{
    Task<PortalAccount?> FindByEmailAsync(string email, CancellationToken ct = default);
    Task<PortalAccount?> RegisterAsync(int patientId, string email, string? phone, string password, CancellationToken ct = default);
    Task<bool> ValidateAsync(PortalAccount account, string password);
    Task SignInAsync(HttpContext ctx, PortalAccount account, bool persist);
    Task SignOutAsync(HttpContext ctx);
}

public class PortalAuthService : IPortalAuthService
{
    private readonly ApplicationDbContext _db;
    private readonly IPasswordHasher<PortalAccount> _hasher;

    public PortalAuthService(ApplicationDbContext db, IPasswordHasher<PortalAccount> hasher)
    {
        _db = db; _hasher = hasher;
    }

    public Task<PortalAccount?> FindByEmailAsync(string email, CancellationToken ct = default) =>
        _db.PortalAccounts.Include(a => a.Patient).FirstOrDefaultAsync(a => a.Email.ToLower() == email.ToLower() && a.IsActive, ct);

    public async Task<PortalAccount?> RegisterAsync(int patientId, string email, string? phone, string password, CancellationToken ct = default)
    {
        var lower = email.ToLower();
        if (await _db.PortalAccounts.AnyAsync(a => a.Email.ToLower() == lower, ct)) return null;
        if (await _db.PortalAccounts.AnyAsync(a => a.PatientId == patientId, ct)) return null;

        var account = new PortalAccount
        {
            PatientId = patientId,
            Email = email,
            Phone = phone,
            IsActive = true
        };
        account.PasswordHash = _hasher.HashPassword(account, password);
        _db.PortalAccounts.Add(account);
        await _db.SaveChangesAsync(ct);
        return account;
    }

    public Task<bool> ValidateAsync(PortalAccount account, string password)
    {
        var r = _hasher.VerifyHashedPassword(account, account.PasswordHash, password);
        return Task.FromResult(r is PasswordVerificationResult.Success or PasswordVerificationResult.SuccessRehashNeeded);
    }

    public async Task SignInAsync(HttpContext ctx, PortalAccount account, bool persist)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.NameIdentifier, account.Id.ToString()),
            new(ClaimTypes.Name, account.Patient?.FullName ?? account.Email),
            new(ClaimTypes.Email, account.Email),
            new(PortalAuth.ClaimPortalAccountId, account.Id.ToString()),
            new(PortalAuth.ClaimPatientId, account.PatientId.ToString())
        };
        if (account.Patient?.FacilityId != null)
            claims.Add(new Claim(PortalAuth.ClaimFacilityId, account.Patient.FacilityId.ToString()));

        var identity = new ClaimsIdentity(claims, PortalAuth.Scheme);
        var principal = new ClaimsPrincipal(identity);

        await ctx.SignInAsync(PortalAuth.Scheme, principal, new AuthenticationProperties
        {
            IsPersistent = persist,
            ExpiresUtc = DateTimeOffset.UtcNow.AddDays(persist ? 30 : 1)
        });

        account.LastLoginAt = DateTime.UtcNow;
        account.LastLoginIp = ctx.Connection.RemoteIpAddress?.ToString();
        await _db.SaveChangesAsync();
    }

    public Task SignOutAsync(HttpContext ctx) => ctx.SignOutAsync(PortalAuth.Scheme);
}
