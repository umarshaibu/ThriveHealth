using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Memory;
using System.Security.Claims;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Services;

public interface IPermissionService
{
    Task<bool> UserHasAsync(ClaimsPrincipal user, string permission, CancellationToken ct = default);
    Task<HashSet<string>> GetForUserAsync(ClaimsPrincipal user, CancellationToken ct = default);
    Task<HashSet<string>> GetForRoleAsync(string roleId, CancellationToken ct = default);

    /// <summary>Replace the role's full permission set. Destructive — anything not in <paramref name="permissions"/>
    /// is removed. Use only when the caller is sure the input is the complete authoritative set.</summary>
    Task SetForRoleAsync(string roleId, IEnumerable<string> permissions, string? grantedById, CancellationToken ct = default);

    /// <summary>Merge: within <paramref name="scope"/>, ensure the role has exactly <paramref name="selectedInScope"/>.
    /// Permissions on the role outside the scope are left untouched. Use when the caller's view of the catalog
    /// may be partial (e.g. an older running app version doesn't render newer permissions).</summary>
    Task MergeForRoleAsync(string roleId, IEnumerable<string> scope, IEnumerable<string> selectedInScope, string? grantedById, CancellationToken ct = default);

    void InvalidateRole(string roleId);
    void InvalidateUser(string userId);
}

public class PermissionService : IPermissionService
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly IMemoryCache _cache;

    public PermissionService(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        RoleManager<IdentityRole> roleManager, IMemoryCache cache)
    {
        _db = db; _userManager = userManager; _roleManager = roleManager; _cache = cache;
    }

    private static string RoleKey(string roleId) => $"perms:role:{roleId}";
    private static string UserKey(string userId) => $"perms:user:{userId}";

    public async Task<HashSet<string>> GetForRoleAsync(string roleId, CancellationToken ct = default)
    {
        if (_cache.TryGetValue(RoleKey(roleId), out HashSet<string>? cached) && cached != null) return cached;
        var perms = await _db.RolePermissions.AsNoTracking()
            .Where(p => p.RoleId == roleId)
            .Select(p => p.Permission).ToListAsync(ct);
        var set = new HashSet<string>(perms, StringComparer.OrdinalIgnoreCase);
        _cache.Set(RoleKey(roleId), set, TimeSpan.FromMinutes(5));
        return set;
    }

    public async Task<HashSet<string>> GetForUserAsync(ClaimsPrincipal user, CancellationToken ct = default)
    {
        var userId = user.FindFirstValue(ClaimTypes.NameIdentifier) ?? "";
        if (string.IsNullOrEmpty(userId)) return new(StringComparer.OrdinalIgnoreCase);

        if (_cache.TryGetValue(UserKey(userId), out HashSet<string>? cached) && cached != null) return cached;

        var u = await _userManager.GetUserAsync(user);
        if (u is null) return new(StringComparer.OrdinalIgnoreCase);
        var roles = await _userManager.GetRolesAsync(u);
        var roleIds = await _db.Roles.AsNoTracking()
            .Where(r => roles.Contains(r.Name!))
            .Select(r => r.Id).ToListAsync(ct);

        var perms = await _db.RolePermissions.AsNoTracking()
            .Where(p => roleIds.Contains(p.RoleId))
            .Select(p => p.Permission).Distinct().ToListAsync(ct);

        var set = new HashSet<string>(perms, StringComparer.OrdinalIgnoreCase);
        _cache.Set(UserKey(userId), set, TimeSpan.FromMinutes(5));
        return set;
    }

    public async Task<bool> UserHasAsync(ClaimsPrincipal user, string permission, CancellationToken ct = default)
    {
        var set = await GetForUserAsync(user, ct);
        return set.Contains(permission);
    }

    public async Task SetForRoleAsync(string roleId, IEnumerable<string> permissions, string? grantedById, CancellationToken ct = default)
    {
        var existing = await _db.RolePermissions.Where(p => p.RoleId == roleId).ToListAsync(ct);
        var desired = new HashSet<string>(permissions.Where(p => !string.IsNullOrWhiteSpace(p)), StringComparer.OrdinalIgnoreCase);

        var toRemove = existing.Where(e => !desired.Contains(e.Permission)).ToList();
        if (toRemove.Count > 0) _db.RolePermissions.RemoveRange(toRemove);

        var existingSet = new HashSet<string>(existing.Select(e => e.Permission), StringComparer.OrdinalIgnoreCase);
        foreach (var p in desired)
        {
            if (existingSet.Contains(p)) continue;
            _db.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                Permission = p,
                GrantedAt = DateTime.UtcNow,
                GrantedById = grantedById
            });
        }
        await _db.SaveChangesAsync(ct);
        InvalidateRole(roleId);
        // Per-user caches will expire on TTL; for immediate effect we'd need a list of users in this role.
        var roleName = await _roleManager.FindByIdAsync(roleId);
        if (roleName?.Name != null)
        {
            var users = await _userManager.GetUsersInRoleAsync(roleName.Name);
            foreach (var u in users) InvalidateUser(u.Id);
        }
    }

    public async Task MergeForRoleAsync(string roleId, IEnumerable<string> scope, IEnumerable<string> selectedInScope, string? grantedById, CancellationToken ct = default)
    {
        var scopeSet = new HashSet<string>(scope.Where(p => !string.IsNullOrWhiteSpace(p)), StringComparer.OrdinalIgnoreCase);
        var selectedSet = new HashSet<string>(
            selectedInScope.Where(p => !string.IsNullOrWhiteSpace(p) && scopeSet.Contains(p)),
            StringComparer.OrdinalIgnoreCase);

        var existing = await _db.RolePermissions.Where(p => p.RoleId == roleId).ToListAsync(ct);
        var existingSet = new HashSet<string>(existing.Select(e => e.Permission), StringComparer.OrdinalIgnoreCase);

        // Within scope: remove what's no longer selected; add what's newly selected.
        // Outside scope: leave existing rows alone.
        var toRemove = existing.Where(e => scopeSet.Contains(e.Permission) && !selectedSet.Contains(e.Permission)).ToList();
        if (toRemove.Count > 0) _db.RolePermissions.RemoveRange(toRemove);

        foreach (var p in selectedSet)
        {
            if (existingSet.Contains(p)) continue;
            _db.RolePermissions.Add(new RolePermission
            {
                RoleId = roleId,
                Permission = p,
                GrantedAt = DateTime.UtcNow,
                GrantedById = grantedById
            });
        }

        await _db.SaveChangesAsync(ct);
        InvalidateRole(roleId);
        var role = await _roleManager.FindByIdAsync(roleId);
        if (role?.Name != null)
        {
            var users = await _userManager.GetUsersInRoleAsync(role.Name);
            foreach (var u in users) InvalidateUser(u.Id);
        }
    }

    public void InvalidateRole(string roleId) => _cache.Remove(RoleKey(roleId));
    public void InvalidateUser(string userId) => _cache.Remove(UserKey(userId));
}
