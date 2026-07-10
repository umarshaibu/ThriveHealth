using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveHealth.Web.Services.Api;

namespace ThriveHealth.Web.Controllers.Api;

/// <summary>
/// Base for all patient-facing mobile/native API controllers. Authenticated via the PortalJwt
/// bearer scheme; exposes the signed-in patient/account ids from the validated token claims.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = PortalJwt.Scheme)]
[Produces("application/json")]
public abstract class ApiControllerBase : ControllerBase
{
    protected int PatientId =>
        int.TryParse(User.FindFirst(PortalJwt.ClaimPatientId)?.Value, out var n) ? n : 0;

    protected int AccountId =>
        int.TryParse(User.FindFirst(PortalJwt.ClaimPortalAccountId)?.Value, out var n) ? n : 0;

    protected int FacilityId =>
        int.TryParse(User.FindFirst(PortalJwt.ClaimFacilityId)?.Value, out var n) ? n : 0;
}
