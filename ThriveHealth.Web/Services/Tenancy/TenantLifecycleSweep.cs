using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Tenancy;

namespace ThriveHealth.Web.Services.Tenancy;

/// <summary>
/// Hourly background sweep that drives the tenant subscription state machine:
///   • Trialing tenants whose trial ended → PastDue (read-only grace period).
///   • PastDue tenants whose grace period (14 days) has run out → Suspended.
///   • Active tenants whose <see cref="TenantSubscription.CurrentPeriodEnd"/> has passed without
///     a fresh successful payment → PastDue.
/// Idempotent — runs again next hour and only changes rows that actually need transitions.
/// </summary>
public class TenantLifecycleSweep : BackgroundService
{
    private static readonly TimeSpan SweepInterval = TimeSpan.FromHours(1);
    private static readonly TimeSpan PastDueGrace = TimeSpan.FromDays(14);

    private readonly IServiceScopeFactory _scopes;
    private readonly ILogger<TenantLifecycleSweep> _log;

    public TenantLifecycleSweep(IServiceScopeFactory scopes, ILogger<TenantLifecycleSweep> log)
    { _scopes = scopes; _log = log; }

    protected override async Task ExecuteAsync(CancellationToken stop)
    {
        // Stagger initial run to avoid thundering-herd on app start
        try { await Task.Delay(TimeSpan.FromSeconds(45), stop); } catch (OperationCanceledException) { return; }

        while (!stop.IsCancellationRequested)
        {
            try { await SweepAsync(stop); }
            catch (Exception ex) { _log.LogError(ex, "Tenant lifecycle sweep failed"); }
            try { await Task.Delay(SweepInterval, stop); } catch (OperationCanceledException) { return; }
        }
    }

    private async Task SweepAsync(CancellationToken ct)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var now = DateTime.UtcNow;

        // 1. Trial expired → PastDue. Teaching hospitals stay free indefinitely.
        var trialEnded = await db.Tenants
            .Where(t => t.Status == TenantStatus.Trialing
                && !t.IsTeachingHospital
                && t.TrialEndsAt != null && t.TrialEndsAt < now)
            .ToListAsync(ct);
        foreach (var t in trialEnded)
        {
            t.Status = TenantStatus.PastDue;
            t.UpdatedAt = now;
            _log.LogInformation("Tenant {Slug} trial ended → PastDue", t.Slug);
        }

        // 2. PastDue beyond grace → Suspended.
        var graceCutoff = now - PastDueGrace;
        var pastDueExpired = await db.Tenants
            .Where(t => t.Status == TenantStatus.PastDue && t.UpdatedAt < graceCutoff)
            .ToListAsync(ct);
        foreach (var t in pastDueExpired)
        {
            t.Status = TenantStatus.Suspended;
            t.SuspendedAt = now;
            t.UpdatedAt = now;
            _log.LogInformation("Tenant {Slug} past-due grace expired → Suspended", t.Slug);
        }

        // 3. Active subs whose period ended without renewal → PastDue.
        var lapsedSubs = await db.TenantSubscriptions.Include(s => s.Tenant)
            .Where(s => s.IsActive && s.CurrentPeriodEnd < now
                && s.Tenant != null && s.Tenant.Status == TenantStatus.Active
                && !s.Tenant.IsTeachingHospital)
            .ToListAsync(ct);
        foreach (var s in lapsedSubs)
        {
            if (s.Tenant is null) continue;
            s.Tenant.Status = TenantStatus.PastDue;
            s.Tenant.UpdatedAt = now;
            _log.LogInformation("Tenant {Slug} subscription period lapsed → PastDue", s.Tenant.Slug);
        }

        await db.SaveChangesAsync(ct);
    }
}
