using System.Globalization;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Api;
using ThriveHealth.Web.Models.Audit;
using ThriveHealth.Web.Services;
using ThriveHealth.Web.Services.Api;
using ThriveHealth.Web.Services.Tenancy;

namespace ThriveHealth.Web.Controllers.Api;

/// <summary>Token-based auth for the patient mobile app: register, login, refresh, logout.</summary>
[ApiController]
[Route("api/v1/auth")]
[Produces("application/json")]
[AllowAnonymous]
[EnableRateLimiting("portal-auth")]
public class AuthApiController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IPortalAuthService _auth;
    private readonly IPortalTokenService _tokens;
    private readonly ITenantContext _tenant;
    private readonly IAuditService _audit;

    public AuthApiController(ApplicationDbContext db, IPortalAuthService auth, IPortalTokenService tokens,
        ITenantContext tenant, IAuditService audit)
    {
        _db = db; _auth = auth; _tokens = tokens; _tenant = tenant; _audit = audit;
    }

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? Agent => Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Login([FromBody] ApiLoginRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var account = await _auth.FindByEmailAsync(req.Email, ct);
        if (account is null || !await _auth.ValidateAsync(account, req.Password))
        {
            await _audit.LogAsync("portal.api.login", AuditCategory.Authentication, AuditOutcome.Failure,
                summary: $"Bad mobile login for {req.Email}", actorOverride: req.Email, ct: ct);
            return Unauthorized(new ApiError("Email or password is incorrect.", "invalid_credentials"));
        }

        var response = await _tokens.IssueAsync(account, Ip, Agent, ct);
        await _audit.LogAsync("portal.api.login", AuditCategory.Authentication, AuditOutcome.Success,
            entityType: "PortalAccount", entityKey: account.Id.ToString(),
            summary: $"Patient {account.Patient?.FullName ?? account.Email} signed in (mobile)",
            facilityId: account.Patient?.FacilityId, actorOverride: account.Email, ct: ct);
        return Ok(response);
    }

    [HttpPost("register")]
    [ProducesResponseType(typeof(ApiTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status409Conflict)]
    public async Task<IActionResult> Register([FromBody] ApiRegisterRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        if (!DateOnly.TryParseExact(req.DateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dob))
            return BadRequest(new ApiError("Date of birth must be yyyy-MM-dd.", "bad_dob"));

        // Verify the patient exists (cross-tenant lookup — no tenant resolved yet, so the query filter is off).
        var hosp = req.HospitalNumber.Trim();
        var last = req.LastName.Trim().ToLower();
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p =>
            p.HospitalNumber == hosp && p.LastName.ToLower() == last && p.DateOfBirth == dob && !p.IsMergedAlias, ct);

        if (patient is null)
        {
            await _audit.LogAsync("portal.api.register", AuditCategory.SecurityEvent, AuditOutcome.Failure,
                summary: $"Mobile patient verification failed for {hosp} / {req.LastName}", actorOverride: req.Email, ct: ct);
            return BadRequest(new ApiError(
                "Patient not found. Verify your hospital number, surname and date of birth match your registration.",
                "patient_not_found"));
        }

        // Resolve and set the tenant so the new PortalAccount row is stamped with the right TenantId.
        var facility = await _db.Facilities.AsNoTracking().FirstOrDefaultAsync(f => f.Id == patient.FacilityId, ct);
        if (facility is not null)
        {
            var tenant = await _db.Tenants.FirstOrDefaultAsync(t => t.Id == facility.TenantId, ct);
            if (tenant is not null) _tenant.Set(tenant);
        }

        var account = await _auth.RegisterAsync(patient.Id, req.Email, req.Phone, req.Password, ct);
        if (account is null)
            return Conflict(new ApiError("Email already in use, or this patient already has an account.", "account_exists"));

        account.Patient = patient;
        var response = await _tokens.IssueAsync(account, Ip, Agent, ct);
        await _audit.LogAsync("portal.api.register", AuditCategory.Authentication, AuditOutcome.Success,
            entityType: "PortalAccount", entityKey: account.Id.ToString(),
            summary: $"Mobile portal account created for {patient.FullName}",
            facilityId: patient.FacilityId, actorOverride: account.Email, ct: ct);
        return Ok(response);
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] ApiRefreshRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var response = await _tokens.RefreshAsync(req.RefreshToken, Ip, Agent, ct);
        if (response is null)
            return Unauthorized(new ApiError("Refresh token is invalid or expired.", "invalid_refresh"));
        return Ok(response);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] ApiLogoutRequest req, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(req.RefreshToken))
            await _tokens.RevokeAsync(req.RefreshToken, ct);
        return NoContent();
    }
}
