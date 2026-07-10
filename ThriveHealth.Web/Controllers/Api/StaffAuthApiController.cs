using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ThriveHealth.Web.Models.Api;
using ThriveHealth.Web.Models.Audit;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Services;
using ThriveHealth.Web.Services.Api;

namespace ThriveHealth.Web.Controllers.Api;

/// <summary>Token auth for the clinician mobile app (Doctor / Consultant / Medical Officer).</summary>
[ApiController]
[Route("api/v1/staff/auth")]
[Produces("application/json")]
[AllowAnonymous]
[EnableRateLimiting("portal-auth")]
public class StaffAuthApiController : ControllerBase
{
    private static readonly string[] ClinicalRoles = { Roles.Consultant, Roles.Doctor, Roles.MedicalOfficer };

    private readonly UserManager<ApplicationUser> _users;
    private readonly IStaffTokenService _tokens;
    private readonly IAuditService _audit;

    public StaffAuthApiController(UserManager<ApplicationUser> users, IStaffTokenService tokens, IAuditService audit)
    {
        _users = users; _tokens = tokens; _audit = audit;
    }

    private string? Ip => HttpContext.Connection.RemoteIpAddress?.ToString();
    private string? Agent => Request.Headers.UserAgent.ToString() is { Length: > 0 } ua ? ua : null;

    [HttpPost("login")]
    [ProducesResponseType(typeof(ApiStaffTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status403Forbidden)]
    public async Task<IActionResult> Login([FromBody] ApiStaffLoginRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);

        var user = await _users.FindByEmailAsync(req.Email);
        if (user is null || !user.IsActive || !await _users.CheckPasswordAsync(user, req.Password))
        {
            await _audit.LogAsync("staff.api.login", AuditCategory.Authentication, AuditOutcome.Failure,
                summary: $"Bad clinician login for {req.Email}", actorOverride: req.Email, ct: ct);
            return Unauthorized(new ApiError("Email or password is incorrect.", "invalid_credentials"));
        }

        var roles = await _users.GetRolesAsync(user);
        if (!roles.Any(r => ClinicalRoles.Contains(r)))
            return StatusCode(StatusCodes.Status403Forbidden,
                new ApiError("This app is for clinicians (doctors and consultants).", "not_clinician"));

        var response = await _tokens.IssueAsync(user, Ip, Agent, ct);
        user.LastLoginAt = DateTime.UtcNow;
        await _users.UpdateAsync(user);
        await _audit.LogAsync("staff.api.login", AuditCategory.Authentication, AuditOutcome.Success,
            entityType: "ApplicationUser", entityKey: user.Id,
            summary: $"Clinician {user.FullName} signed in (mobile)", facilityId: user.FacilityId,
            actorOverride: user.Email, ct: ct);
        return Ok(response);
    }

    [HttpPost("refresh")]
    [ProducesResponseType(typeof(ApiStaffTokenResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status401Unauthorized)]
    public async Task<IActionResult> Refresh([FromBody] ApiRefreshRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        var response = await _tokens.RefreshAsync(req.RefreshToken, Ip, Agent, ct);
        if (response is null) return Unauthorized(new ApiError("Refresh token is invalid or expired.", "invalid_refresh"));
        return Ok(response);
    }

    [HttpPost("logout")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Logout([FromBody] ApiLogoutRequest req, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(req.RefreshToken)) await _tokens.RevokeAsync(req.RefreshToken, ct);
        return NoContent();
    }
}
