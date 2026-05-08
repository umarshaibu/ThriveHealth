using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Audit;

namespace ThriveHealth.Web.Services;

public interface IAuditService
{
    Task LogAsync(string action,
        AuditCategory category = AuditCategory.BusinessAction,
        AuditOutcome outcome = AuditOutcome.Success,
        string? entityType = null,
        string? entityKey = null,
        string? summary = null,
        int? facilityId = null,
        string? actorOverride = null,
        string? metadata = null,
        CancellationToken ct = default);
}

public class AuditService : IAuditService
{
    private readonly ApplicationDbContext _db;
    private readonly IHttpContextAccessor _http;
    private readonly ILogger<AuditService> _log;

    public AuditService(ApplicationDbContext db, IHttpContextAccessor http, ILogger<AuditService> log)
    {
        _db = db; _http = http; _log = log;
    }

    public async Task LogAsync(string action,
        AuditCategory category = AuditCategory.BusinessAction,
        AuditOutcome outcome = AuditOutcome.Success,
        string? entityType = null,
        string? entityKey = null,
        string? summary = null,
        int? facilityId = null,
        string? actorOverride = null,
        string? metadata = null,
        CancellationToken ct = default)
    {
        try
        {
            var ctx = _http.HttpContext;
            string? userId = null;
            int? patientId = null;
            string? scheme = null;
            var name = actorOverride;

            if (ctx?.User.Identity?.IsAuthenticated == true)
            {
                scheme = ctx.User.Identity.AuthenticationType;
                name ??= ctx.User.Identity.Name;
                if (ctx.User.HasClaim(c => c.Type == PortalAuth.ClaimPatientId))
                {
                    var raw = ctx.User.FindFirstValue(PortalAuth.ClaimPatientId);
                    if (int.TryParse(raw, out var pid)) patientId = pid;
                }
                else
                {
                    userId = ctx.User.FindFirstValue(ClaimTypes.NameIdentifier);
                }
            }

            var entry = new AuditEntry
            {
                AtUtc = DateTime.UtcNow,
                Category = category,
                Outcome = outcome,
                Action = action,
                EntityType = entityType,
                EntityKey = entityKey,
                Summary = summary,
                FacilityId = facilityId,
                ActorUserId = userId,
                ActorPatientId = patientId,
                ActorName = name,
                ActorScheme = scheme,
                IpAddress = ctx?.Connection.RemoteIpAddress?.ToString(),
                UserAgent = ctx?.Request.Headers.UserAgent.ToString(),
                CorrelationId = ctx?.TraceIdentifier,
                Metadata = metadata
            };
            _db.AuditEntries.Add(entry);
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Audit must never break the user request
            _log.LogError(ex, "Failed to write audit entry for {Action}", action);
        }
    }
}
