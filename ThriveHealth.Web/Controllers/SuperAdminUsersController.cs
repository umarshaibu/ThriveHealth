using System.Security.Cryptography;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Tenancy;

namespace ThriveHealth.Web.Controllers;

/// <summary>
/// Cross-tenant user administration — visible only to <see cref="Roles.SuperAdmin"/>.
/// Tenants manage their own users via <c>/staff</c>; this console manages every user on
/// the platform (including patients via the portal flow if they ever surface here, plus
/// other Super Admins). Mounted at <c>/superadmin/users</c>.
/// </summary>
[Authorize(Roles = Roles.SuperAdmin), Route("superadmin/users")]
public class SuperAdminUsersController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    private readonly RoleManager<IdentityRole> _roles;
    private readonly ILogger<SuperAdminUsersController> _log;

    public SuperAdminUsersController(
        ApplicationDbContext db,
        UserManager<ApplicationUser> users,
        RoleManager<IdentityRole> roles,
        ILogger<SuperAdminUsersController> log)
    {
        _db = db; _users = users; _roles = roles; _log = log;
    }

    public record UserRow(string Id, string Email, string FullName, int? TenantId,
        string? TenantName, IReadOnlyList<string> Roles, bool IsActive, DateTime? LastLoginAt);

    public record TenantOption(int Id, string LegalName);

    [HttpGet("")]
    public async Task<IActionResult> Index(string? q, int? tenantId, string? role, bool? activeOnly = true, int page = 1)
    {
        const int pageSize = 50;
        page = Math.Max(1, page);

        // Pull users + their tenant + roles in three queries, then merge in memory. Avoids
        // EF cross-join on Identity tables which doesn't compose well across IgnoreQueryFilters.
        var query = _db.Users.IgnoreQueryFilters().AsNoTracking().AsQueryable();

        if (!string.IsNullOrWhiteSpace(q))
        {
            var lower = q.Trim().ToLower();
            query = query.Where(u =>
                u.Email!.ToLower().Contains(lower) ||
                u.FirstName.ToLower().Contains(lower) ||
                u.LastName.ToLower().Contains(lower) ||
                (u.StaffNumber != null && u.StaffNumber.ToLower().Contains(lower)));
        }
        if (tenantId.HasValue) query = query.Where(u => u.TenantId == tenantId.Value);
        if (activeOnly == true) query = query.Where(u => u.IsActive);

        var total = await query.CountAsync();
        var users = await query
            .OrderBy(u => u.LastName).ThenBy(u => u.FirstName)
            .Skip((page - 1) * pageSize)
            .Take(pageSize)
            .ToListAsync();

        var userIds = users.Select(u => u.Id).ToList();
        var roleMap = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.Id
            where userIds.Contains(ur.UserId)
            select new { ur.UserId, RoleName = r.Name! }
        ).ToListAsync();

        var tenants = await _db.Tenants.IgnoreQueryFilters().AsNoTracking()
            .Select(t => new TenantOption(t.Id, t.LegalName)).ToListAsync();
        var tenantNameById = tenants.ToDictionary(t => t.Id, t => t.LegalName);

        var rows = users.Select(u => new UserRow(
            u.Id, u.Email ?? "", u.FullName, u.TenantId,
            u.TenantId.HasValue && tenantNameById.TryGetValue(u.TenantId.Value, out var n) ? n : null,
            roleMap.Where(r => r.UserId == u.Id).Select(r => r.RoleName).OrderBy(x => x).ToList(),
            u.IsActive, u.LastLoginAt)).ToList();

        // Optional post-filter on role (cheap given page size is small).
        if (!string.IsNullOrWhiteSpace(role))
            rows = rows.Where(r => r.Roles.Contains(role)).ToList();

        ViewBag.Q = q;
        ViewBag.TenantId = tenantId;
        ViewBag.Role = role;
        ViewBag.ActiveOnly = activeOnly ?? true;
        ViewBag.Page = page;
        ViewBag.PageSize = pageSize;
        ViewBag.Total = total;
        ViewBag.Tenants = tenants.OrderBy(t => t.LegalName).ToList();
        ViewBag.AllRoles = await _roles.Roles.OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync();
        return View(rows);
    }

    [HttpGet("{id}")]
    public async Task<IActionResult> Detail(string id)
    {
        var user = await _users.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        ViewBag.User = user;
        ViewBag.Roles = await _users.GetRolesAsync(user);
        ViewBag.AllRoles = await _roles.Roles.OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync();
        ViewBag.Tenant = user.TenantId.HasValue
            ? await _db.Tenants.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(t => t.Id == user.TenantId.Value)
            : null;
        ViewBag.Facility = user.FacilityId.HasValue
            ? await _db.Facilities.IgnoreQueryFilters().AsNoTracking().FirstOrDefaultAsync(f => f.Id == user.FacilityId.Value)
            : null;
        return View(user);
    }

    [HttpGet("new")]
    public async Task<IActionResult> Create()
    {
        ViewBag.AllRoles = await _roles.Roles.OrderBy(r => r.Name).Select(r => r.Name!).ToListAsync();
        ViewBag.Tenants = await _db.Tenants.IgnoreQueryFilters().AsNoTracking().OrderBy(t => t.LegalName).ToListAsync();
        return View(new CreateUserModel());
    }

    public class CreateUserModel
    {
        public string Email { get; set; } = string.Empty;
        public string FirstName { get; set; } = string.Empty;
        public string LastName { get; set; } = string.Empty;
        public string? Designation { get; set; }
        public string? StaffNumber { get; set; }
        public int? TenantId { get; set; }
        public int? FacilityId { get; set; }
        public List<string> Roles { get; set; } = new();
    }

    [HttpPost(""), ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(CreateUserModel model)
    {
        if (string.IsNullOrWhiteSpace(model.Email))
        {
            TempData["Error"] = "Email is required.";
            return RedirectToAction(nameof(Create));
        }

        var existing = await _users.FindByEmailAsync(model.Email.Trim().ToLowerInvariant());
        if (existing is not null)
        {
            TempData["Error"] = $"A user with email {model.Email} already exists.";
            return RedirectToAction(nameof(Create));
        }

        // SuperAdmin role is platform-only — those users have no tenant or facility.
        var isSuperAdmin = model.Roles.Contains(Roles.SuperAdmin);

        var user = new ApplicationUser
        {
            UserName = model.Email.Trim().ToLowerInvariant(),
            Email = model.Email.Trim().ToLowerInvariant(),
            EmailConfirmed = true,
            FirstName = model.FirstName.Trim(),
            LastName = model.LastName.Trim(),
            StaffNumber = model.StaffNumber,
            Designation = model.Designation,
            TenantId = isSuperAdmin ? null : model.TenantId,
            FacilityId = isSuperAdmin ? null : model.FacilityId,
            IsActive = true
        };

        var tempPassword = GenerateTempPassword();
        var result = await _users.CreateAsync(user, tempPassword);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Create));
        }

        foreach (var roleName in model.Roles.Where(r => Roles.All.Contains(r) || r == Roles.SuperAdmin))
        {
            if (!await _roles.RoleExistsAsync(roleName)) await _roles.CreateAsync(new IdentityRole(roleName));
            await _users.AddToRoleAsync(user, roleName);
        }

        _log.LogInformation("[audit/platform] superadmin.user.create — {Actor} created {Email} with roles {Roles}",
            User.Identity?.Name, user.Email, string.Join(",", model.Roles));

        TempData["Success"] = $"Created {user.Email}. Temporary password: {tempPassword}";
        TempData["TempPassword"] = tempPassword;
        return RedirectToAction(nameof(Detail), new { id = user.Id });
    }

    [HttpPost("{id}/toggle-active"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive(string id)
    {
        var user = await _users.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        // Self-lockout guard: a Super Admin cannot deactivate themselves.
        if (user.UserName == User.Identity?.Name)
        {
            TempData["Error"] = "You can't deactivate your own account from here.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        user.IsActive = !user.IsActive;
        await _users.UpdateAsync(user);
        _log.LogInformation("[audit/platform] superadmin.user.toggle — {Actor} set {Email} active={Active}",
            User.Identity?.Name, user.Email, user.IsActive);
        TempData["Success"] = $"{user.Email} is now {(user.IsActive ? "active" : "deactivated")}.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id}/reset-password"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ResetPassword(string id)
    {
        var user = await _users.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        var token = await _users.GeneratePasswordResetTokenAsync(user);
        var temp = GenerateTempPassword();
        var result = await _users.ResetPasswordAsync(user, token, temp);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(Detail), new { id });
        }

        _log.LogInformation("[audit/platform] superadmin.user.reset-password — {Actor} reset password for {Email}",
            User.Identity?.Name, user.Email);
        // One-shot display of the temp password — the operator must copy it now and pass it on.
        TempData["TempPassword"] = temp;
        TempData["Success"] = $"Password reset. Share this temporary password with {user.Email} — they won't be shown it again.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id}/add-role"), ValidateAntiForgeryToken]
    public async Task<IActionResult> AddRole(string id, string role)
    {
        var user = await _users.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();
        if (string.IsNullOrEmpty(role)) return RedirectToAction(nameof(Detail), new { id });

        if (!await _roles.RoleExistsAsync(role)) await _roles.CreateAsync(new IdentityRole(role));
        await _users.AddToRoleAsync(user, role);
        _log.LogInformation("[audit/platform] superadmin.user.role-add — {Actor} added {Role} to {Email}",
            User.Identity?.Name, role, user.Email);
        TempData["Success"] = $"Added role '{role}' to {user.Email}.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost("{id}/remove-role"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveRole(string id, string role)
    {
        var user = await _users.Users.IgnoreQueryFilters().FirstOrDefaultAsync(u => u.Id == id);
        if (user is null) return NotFound();

        // Self-lockout guard: a Super Admin cannot remove their own SuperAdmin role.
        if (user.UserName == User.Identity?.Name && role == Roles.SuperAdmin)
        {
            TempData["Error"] = "You can't remove your own Super Admin role.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        await _users.RemoveFromRoleAsync(user, role);
        _log.LogInformation("[audit/platform] superadmin.user.role-remove — {Actor} removed {Role} from {Email}",
            User.Identity?.Name, role, user.Email);
        TempData["Success"] = $"Removed role '{role}' from {user.Email}.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    private static string GenerateTempPassword()
    {
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghjkmnpqrstuvwxyz";
        const string digit = "23456789";
        const string sym = "!@#$%&*?";
        var pool = upper + lower + digit + sym;
        Span<char> buf = stackalloc char[12];
        var rng = RandomNumberGenerator.Create();
        var bytes = new byte[buf.Length];
        rng.GetBytes(bytes);
        buf[0] = upper[bytes[0] % upper.Length];
        buf[1] = lower[bytes[1] % lower.Length];
        buf[2] = digit[bytes[2] % digit.Length];
        buf[3] = sym[bytes[3] % sym.Length];
        for (var i = 4; i < buf.Length; i++) buf[i] = pool[bytes[i] % pool.Length];
        // Shuffle Fisher-Yates so guaranteed positions aren't predictable.
        for (var i = buf.Length - 1; i > 0; i--)
        {
            var j = bytes[i] % (i + 1);
            (buf[i], buf[j]) = (buf[j], buf[i]);
        }
        return new string(buf);
    }
}
