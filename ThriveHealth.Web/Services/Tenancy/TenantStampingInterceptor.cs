using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Diagnostics;

namespace ThriveHealth.Web.Services.Tenancy;

/// <summary>
/// Auto-stamps the <c>TenantId</c> column on every newly-inserted row using the current
/// <see cref="ITenantContext"/>. This means controllers don't have to remember to set
/// <c>TenantId</c> explicitly — the data layer guarantees it. Inserts performed in admin
/// or design-time contexts (where <see cref="ITenantContext.CurrentId"/> is null) are
/// passed through unchanged so the caller can supply the value themselves
/// (e.g. SuperAdmin operations creating rows on behalf of a specific tenant).
/// </summary>
public sealed class TenantStampingInterceptor : SaveChangesInterceptor
{
    private readonly ITenantContext _tenant;

    public TenantStampingInterceptor(ITenantContext tenant) { _tenant = tenant; }

    public override InterceptionResult<int> SavingChanges(DbContextEventData eventData, InterceptionResult<int> result)
    {
        Stamp(eventData.Context);
        return base.SavingChanges(eventData, result);
    }

    public override ValueTask<InterceptionResult<int>> SavingChangesAsync(
        DbContextEventData eventData, InterceptionResult<int> result, CancellationToken cancellationToken = default)
    {
        Stamp(eventData.Context);
        return base.SavingChangesAsync(eventData, result, cancellationToken);
    }

    private void Stamp(DbContext? ctx)
    {
        if (ctx is null) return;
        if (!_tenant.CurrentId.HasValue) return; // admin / migration / marketing — caller is responsible

        var tenantId = _tenant.CurrentId.Value;

        foreach (var entry in ctx.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added) continue;
            var prop = TryFindTenantProperty(entry);
            if (prop is null) continue;

            // Don't override an explicit value (e.g. SuperAdmin tooling that knows what it's doing).
            if (prop.CurrentValue is int existing && existing != 0) continue;

            prop.CurrentValue = tenantId;
        }
    }

    private static PropertyEntry? TryFindTenantProperty(EntityEntry entry)
    {
        foreach (var p in entry.Properties)
            if (p.Metadata.Name == "TenantId") return p;
        return null;
    }
}
