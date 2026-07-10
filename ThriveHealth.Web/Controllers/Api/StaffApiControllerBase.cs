using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Services.Api;

namespace ThriveHealth.Web.Controllers.Api;

/// <summary>
/// Base for clinician (staff) mobile API controllers. Authenticated via the shared bearer scheme,
/// but restricted to clinical-doctor roles — so a patient token (which carries no role claims)
/// cannot reach these endpoints.
/// </summary>
[ApiController]
[Authorize(AuthenticationSchemes = PortalJwt.Scheme, Roles = ClinicalRoles)]
[Produces("application/json")]
public abstract class StaffApiControllerBase : ControllerBase
{
    public const string ClinicalRoles = "Consultant,Doctor,Medical Officer";

    protected string UserId => User.FindFirst(PortalJwt.ClaimUserId)?.Value ?? string.Empty;

    protected int FacilityId =>
        int.TryParse(User.FindFirst(PortalJwt.ClaimFacilityId)?.Value, out var n) ? n : 0;

    protected bool IsConsultant => User.IsInRole(Roles.Consultant);
}
