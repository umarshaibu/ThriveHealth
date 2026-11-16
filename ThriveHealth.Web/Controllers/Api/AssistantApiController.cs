using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Api;
using ThriveHealth.Web.Services.Ai;

namespace ThriveHealth.Web.Controllers.Api;

/// <summary>
/// Patient-facing AI assistant for the mobile app. Wraps the same triage-only symptom checker
/// as the web portal, so its safety prompt, facility feature flag and audit trail apply here too.
/// </summary>
[Route("api/v1/assistant")]
public class AssistantApiController : ApiControllerBase
{
    private const int MaxHistoryEntries = 12;
    private const int MaxHistoryEntryLength = 1500;

    private readonly ApplicationDbContext _db;
    private readonly IClinicalAiService _ai;

    public AssistantApiController(ApplicationDbContext db, IClinicalAiService ai)
    {
        _db = db;
        _ai = ai;
    }

    [HttpPost("ask"), EnableRateLimiting("assistant")]
    [ProducesResponseType(typeof(ApiAssistantReply), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status503ServiceUnavailable)]
    public async Task<IActionResult> Ask([FromBody] ApiAssistantRequest req, CancellationToken ct)
    {
        var patient = await _db.Patients.AsNoTracking()
            .Where(p => p.Id == PatientId)
            .Select(p => new { p.FacilityId, p.DateOfBirth, p.Sex })
            .FirstOrDefaultAsync(ct);
        if (patient is null) return NotFound(new ApiError("Patient not found.", "not_found"));

        var ageSex = patient.DateOfBirth.HasValue
            ? $"{(int)((DateTime.UtcNow - patient.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)).TotalDays / 365)}y {patient.Sex}"
            : patient.Sex.ToString();

        var history = (req.History ?? Array.Empty<string>())
            .Where(h => !string.IsNullOrWhiteSpace(h))
            .TakeLast(MaxHistoryEntries)
            .Select(h => h.Length > MaxHistoryEntryLength ? h[..MaxHistoryEntryLength] : h)
            .ToList();

        var input = new SymptomCheckerInput(patient.FacilityId, AccountId, ageSex, req.Question, history, Conversational: true);
        var outcome = await _ai.SymptomCheckAsync(input, requestedById: null, ct);

        // Provider and configuration details stay in the server's AI log, not the patient's screen.
        if (!outcome.Ok || string.IsNullOrWhiteSpace(outcome.Text))
            return StatusCode(StatusCodes.Status503ServiceUnavailable,
                new ApiError("The assistant isn't available right now. Please contact your clinic if you need help.", "assistant_unavailable"));

        return Ok(new ApiAssistantReply(outcome.Text));
    }
}
