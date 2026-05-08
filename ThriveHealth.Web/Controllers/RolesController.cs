using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.ComponentModel.DataAnnotations;
using System.Security.Claims;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Audit;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers;

[HasPermission(ThriveHealth.Web.Models.Identity.Permissions.RbacManage)]
public class RolesController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPermissionService _perms;
    private readonly IAuditService _audit;

    public RolesController(ApplicationDbContext db,
        RoleManager<IdentityRole> roleManager,
        UserManager<ApplicationUser> userManager,
        IPermissionService perms,
        IAuditService audit)
    {
        _db = db; _roleManager = roleManager; _userManager = userManager;
        _perms = perms; _audit = audit;
    }

    public class RoleRow
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public int PermissionCount { get; set; }
        public int UserCount { get; set; }
    }

    public class CreateRoleVm
    {
        [Required, MaxLength(120)] public string Name { get; set; } = string.Empty;
    }

    public class EditPermissionsVm
    {
        public string RoleId { get; set; } = string.Empty;
        public string RoleName { get; set; } = string.Empty;

        /// <summary>Permissions ticked on the form.</summary>
        public List<string> Selected { get; set; } = new();

        /// <summary>Every permission rendered as a checkbox on the form (whether ticked or not).
        /// Defines the editing scope: anything in DB but NOT here is preserved as-is on save.</summary>
        public List<string> Visible { get; set; } = new();
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var roles = await _roleManager.Roles.AsNoTracking().OrderBy(r => r.Name).ToListAsync();
        var permCounts = await _db.RolePermissions.AsNoTracking()
            .GroupBy(p => p.RoleId)
            .Select(g => new { g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.Key, x => x.Count);

        var rows = new List<RoleRow>();
        foreach (var r in roles)
        {
            var users = await _userManager.GetUsersInRoleAsync(r.Name!);
            rows.Add(new RoleRow
            {
                Id = r.Id,
                Name = r.Name ?? "",
                PermissionCount = permCounts.GetValueOrDefault(r.Id, 0),
                UserCount = users.Count
            });
        }
        return View(rows);
    }

    [HttpGet]
    public IActionResult Create() => View(new CreateRoleVm());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(CreateRoleVm m)
    {
        if (!ModelState.IsValid) return View(m);
        var name = m.Name.Trim();
        if (await _roleManager.RoleExistsAsync(name))
        {
            ModelState.AddModelError(nameof(m.Name), "A role with that name already exists.");
            return View(m);
        }
        var res = await _roleManager.CreateAsync(new IdentityRole(name));
        if (!res.Succeeded)
        {
            foreach (var e in res.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(m);
        }
        var role = await _roleManager.FindByNameAsync(name);
        await _audit.LogAsync("rbac.role.created", AuditCategory.Authorization, AuditOutcome.Success,
            entityType: "Role", entityKey: role?.Id, summary: $"Role '{name}' created");
        TempData["Success"] = $"Role '{name}' created.";
        return RedirectToAction(nameof(Permissions), new { id = role!.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Delete(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();

        // Guard: don't allow deletion of seeded system roles
        if (Roles.All.Contains(role.Name))
        {
            TempData["Error"] = "Built-in roles cannot be deleted.";
            return RedirectToAction(nameof(Index));
        }

        var users = await _userManager.GetUsersInRoleAsync(role.Name!);
        if (users.Any())
        {
            TempData["Error"] = $"Cannot delete '{role.Name}' — {users.Count} user(s) still hold this role.";
            return RedirectToAction(nameof(Index));
        }

        // Remove its permissions first
        var rp = await _db.RolePermissions.Where(p => p.RoleId == role.Id).ToListAsync();
        _db.RolePermissions.RemoveRange(rp);
        await _db.SaveChangesAsync();
        await _roleManager.DeleteAsync(role);
        _perms.InvalidateRole(role.Id);
        await _audit.LogAsync("rbac.role.deleted", AuditCategory.Authorization, AuditOutcome.Success,
            entityType: "Role", entityKey: id, summary: $"Role '{role.Name}' deleted");
        TempData["Success"] = $"Role '{role.Name}' deleted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Permissions(string id)
    {
        var role = await _roleManager.FindByIdAsync(id);
        if (role is null) return NotFound();
        var current = await _perms.GetForRoleAsync(role.Id);
        var users = await _userManager.GetUsersInRoleAsync(role.Name!);
        ViewBag.UserCount = users.Count;
        ViewBag.Groups = ThriveHealth.Web.Models.Identity.Permissions.Grouped();
        return View(new EditPermissionsVm
        {
            RoleId = role.Id,
            RoleName = role.Name ?? "",
            Selected = current.ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Permissions(EditPermissionsVm m)
    {
        var role = await _roleManager.FindByIdAsync(m.RoleId);
        if (role is null) return NotFound();
        var actorId = User.FindFirstValue(ClaimTypes.NameIdentifier);
        var allKnown = ThriveHealth.Web.Models.Identity.Permissions.All;

        // Scope = permissions the form actually rendered. Only these can be added/removed; anything
        // else on the role in the DB (e.g. catalog entries this app build doesn't know about, or rows
        // added by another admin in parallel) is preserved untouched.
        var scope = (m.Visible ?? new List<string>())
            .Distinct()
            .Where(p => allKnown.Contains(p))   // ignore garbage submissions
            .ToList();
        var selected = (m.Selected ?? new List<string>())
            .Distinct()
            .Where(p => scope.Contains(p))      // only count selections inside the rendered scope
            .ToList();

        // Safety: prevent removing rbac.manage from System Administrator if the form was scoped over it.
        var rbac = ThriveHealth.Web.Models.Identity.Permissions.RbacManage;
        if (role.Name == Roles.SystemAdministrator && scope.Contains(rbac) && !selected.Contains(rbac))
        {
            TempData["Error"] = "System Administrator must keep the 'rbac.manage' permission.";
            return RedirectToAction(nameof(Permissions), new { id = m.RoleId });
        }

        // Merge: only touch permissions the form rendered. Anything else on the role is preserved.
        await _perms.MergeForRoleAsync(role.Id, scope, selected, actorId);

        // Compute a human-friendly summary of what changed.
        var existingNow = await _perms.GetForRoleAsync(role.Id);
        await _audit.LogAsync("rbac.permissions.updated", AuditCategory.Authorization, AuditOutcome.Success,
            entityType: "Role", entityKey: role.Id,
            summary: $"Permissions merged for role '{role.Name}' · scope={scope.Count} · selected={selected.Count} · total={existingNow.Count}");
        TempData["Success"] = $"Saved {selected.Count} permission(s) within the displayed scope for '{role.Name}'. Role now has {existingNow.Count} total.";
        return RedirectToAction(nameof(Permissions), new { id = role.Id });
    }
}
