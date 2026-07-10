using Microsoft.AspNetCore.Mvc;
using ThriveHealth.Web.Models.Api;
using ThriveHealth.Web.Services.Api;

namespace ThriveHealth.Web.Controllers.Api;

/// <summary>Registers/clears the device's native push token (Expo push token) for the signed-in patient.</summary>
[Route("api/v1/push")]
public class PushApiController : ApiControllerBase
{
    private readonly IDevicePushService _push;

    public PushApiController(IDevicePushService push) => _push = push;

    [HttpPost("register")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Register([FromBody] ApiRegisterDeviceRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid) return ValidationProblem(ModelState);
        await _push.RegisterAsync(PatientId, req.Token.Trim(), req.Platform, ct);
        return NoContent();
    }

    [HttpPost("unregister")]
    [ProducesResponseType(StatusCodes.Status204NoContent)]
    public async Task<IActionResult> Unregister([FromBody] ApiRegisterDeviceRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.Token)) return NoContent();
        await _push.UnregisterAsync(req.Token.Trim(), ct);
        return NoContent();
    }
}
