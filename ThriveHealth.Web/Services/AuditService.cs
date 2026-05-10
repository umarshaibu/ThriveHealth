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

            // Resolve the tenant the audit row should belong to. The AuditEntries table is
            // tenant-scoped (FK to Tenants), so platform-level events (SuperAdmin signing in,
            // background jobs in admin context) can't be persisted here — they're emitted to
            // ILogger instead, which is the natural sink for cross-tenant operational events.
            int? tenantId = null;
            if (facilityId.HasValue)
                tenantId = await _db.Facilities.IgnoreQueryFilters()
                    .Where(f => f.Id == facilityId.Value)
                    .Select(f => (int?)f.TenantId)
                    .FirstOrDefaultAsync(ct);
            if (tenantId is null && userId is not null)
                tenantId = await _db.Users.IgnoreQueryFilters()
                    .Where(u => u.Id == userId)
                    .Select(u => u.TenantId)
                    .FirstOrDefaultAsync(ct);

            if (tenantId is null)
            {
                _log.LogInformation("[audit/platform] {Action} {Outcome} by {Actor}: {Summary}",
                    action, outcome, name ?? "anonymous", summary);
                return;
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
            // Stamp the shadow TenantId explicitly — the auto-stamp interceptor only fires when
            // a request-scoped ITenantContext has CurrentId set, which isn't the case for
            // marketing/admin requests where audit may also be needed (e.g. tenant signup).
            _db.Entry(entry).Property("TenantId").CurrentValue = tenantId.Value;
            await _db.SaveChangesAsync(ct);
        }
        catch (Exception ex)
        {
            // Audit must never break the user request
            _log.LogError(ex, "Failed to write audit entry for {Action}", action);
        }
    }
}
