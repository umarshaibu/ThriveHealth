using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Audit;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;
using ThriveHealth.Web.Services.Tenancy;

namespace ThriveHealth.Web.Controllers;

[Authorize]
public class AccountController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly SignInManager<ApplicationUser> _signInManager;
    private readonly RoleManager<IdentityRole> _roleManager;
    private readonly ApplicationDbContext _db;
    private readonly ILogger<AccountController> _logger;
    private readonly IAuditService _audit;
    private readonly ITenantContext _tenant;

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext db,
        ILogger<AccountController> logger,
        IAuditService audit,
        ITenantContext tenant)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _db = db;
        _logger = logger;
        _audit = audit;
        _tenant = tenant;
    }

    [HttpGet("/login"), AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost("/login"), AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("auth")]
    public async Task<IActionResult> Login(LoginViewModel model)
    {
        ViewData["ReturnUrl"] = model.ReturnUrl;
        if (!ModelState.IsValid) return View(model);

        var user = await _userManager.FindByEmailAsync(model.Email);
        if (user is null || !user.IsActive)
        {
            await _audit.LogAsync("staff.login", AuditCategory.Authentication, AuditOutcome.Failure,
                summary: $"Unknown email {model.Email}", actorOverride: model.Email);
            ModelState.AddModelError(string.Empty, "Invalid login attempt.");
            return View(model);
        }

        var result = await _signInManager.PasswordSignInAsync(
            user.UserName!, model.Password, model.RememberMe, lockoutOnFailure: true);

        if (result.Succeeded)
        {
            user.LastLoginAt = DateTime.UtcNow;
            await _userManager.UpdateAsync(user);
            _logger.LogInformation("User {Email} signed in.", model.Email);
            await _audit.LogAsync("staff.login", AuditCategory.Authentication, AuditOutcome.Success,
                entityType: "User", entityKey: user.Id, summary: $"{user.FullName} signed in",
                facilityId: user.FacilityId, actorOverride: user.FullName);
            return await RedirectAfterLoginAsync(user, model.ReturnUrl);
        }

        if (result.IsLockedOut)
        {
            await _audit.LogAsync("staff.login", AuditCategory.SecurityEvent, AuditOutcome.Warning,
                entityType: "User", entityKey: user.Id, summary: "Account locked",
                facilityId: user.FacilityId, actorOverride: user.FullName);
            ModelState.AddModelError(string.Empty, "Account locked. Try again later.");
            return View(model);
        }

        await _audit.LogAsync("staff.login", AuditCategory.Authentication, AuditOutcome.Failure,
            entityType: "User", entityKey: user.Id, summary: "Bad password",
            facilityId: user.FacilityId, actorOverride: user.FullName);
        ModelState.AddModelError(string.Empty, "Invalid login attempt.");
        return View(model);
    }

    [HttpPost("/logout"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var current = await _userManager.GetUserAsync(User);
        await _signInManager.SignOutAsync();
        if (current != null)
        {
            await _audit.LogAsync("staff.logout", AuditCategory.Authentication, AuditOutcome.Success,
                entityType: "User", entityKey: current.Id, facilityId: current.FacilityId,
                actorOverride: current.FullName);
        }
        return RedirectToAction("Login");
    }

    [HttpGet("/access-denied"), AllowAnonymous]
    public IActionResult AccessDenied() => View();

    [HttpGet("/profile")]
    public async Task<IActionResult> Profile()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        if (user.FacilityId.HasValue)
            user.Facility = await _db.Facilities.FindAsync(user.FacilityId.Value);

        var roles = await _userManager.GetRolesAsync(user);
        var vm = new ProfileViewModel
        {
            FirstName = user.FirstName,
            LastName = user.LastName,
            MiddleName = user.MiddleName,
            Email = user.Email ?? string.Empty,
            PhoneNumber = user.PhoneNumber,
            StaffNumber = user.StaffNumber,
            Department = user.Department,
            Designation = user.Designation,
            LicenseBody = user.LicenseBody,
            LicenseNumber = user.LicenseNumber,
            LicenseExpiry = user.LicenseExpiry,
            FacilityName = user.Facility?.Name,
            Roles = roles
        };
        return View(vm);
    }

    [HttpPost("/profile"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(ProfileViewModel model)
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        user.FirstName = model.FirstName;
        user.LastName = model.LastName;
        user.MiddleName = model.MiddleName;
        user.PhoneNumber = model.PhoneNumber;
        user.Department = model.Department;
        user.Designation = model.Designation;

        var result = await _userManager.UpdateAsync(user);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(model);
        }

        TempData["Success"] = "Profile updated.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet("/change-password")]
    public IActionResult ChangePassword() => View();

    [HttpPost("/change-password"), ValidateAntiForgeryToken]
    public async Task<IActionResult> ChangePassword(ChangePasswordViewModel model)
    {
        if (!ModelState.IsValid) return View(model);
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();

        var result = await _userManager.ChangePasswordAsync(user, model.CurrentPassword, model.NewPassword);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            return View(model);
        }

        await _signInManager.RefreshSignInAsync(user);
        TempData["Success"] = "Password changed.";
        return RedirectToAction(nameof(Profile));
    }

    [HttpGet("/staff/register"), HasPermission(Permissions.StaffManage)]
    public async Task<IActionResult> RegisterStaff()
    {
        await PopulateRegistrationLists();
        return View(new RegisterStaffViewModel());
    }

    [HttpPost("/staff/register"), ValidateAntiForgeryToken, HasPermission(Permissions.StaffManage)]
    public async Task<IActionResult> RegisterStaff(RegisterStaffViewModel model)
    {
        if (!ModelState.IsValid)
        {
            await PopulateRegistrationLists();
            return View(model);
        }

        // Pin the new user to the caller's tenant. Without this, a System Admin could
        // (intentionally or by URL tampering) pick a facility from a different tenant via
        // the dropdown — we only render facilities from their tenant, but defence in depth.
        var caller = await _userManager.GetUserAsync(User);
        var callerTenant = _tenant.CurrentId ?? caller?.TenantId;
        if (callerTenant is null)
        {
            TempData["Error"] = "No tenant context — cannot create staff.";
            return RedirectToAction(nameof(StaffList));
        }
        var facilityBelongs = await _db.Facilities.AnyAsync(f => f.Id == model.FacilityId && f.TenantId == callerTenant.Value);
        if (!facilityBelongs)
        {
            ModelState.AddModelError(nameof(RegisterStaffViewModel.FacilityId), "Pick a facility within your hospital.");
            await PopulateRegistrationLists();
            return View(model);
        }

        var user = new ApplicationUser
        {
            UserName = model.Email,
            Email = model.Email,
            PhoneNumber = model.PhoneNumber,
            EmailConfirmed = true,
            FirstName = model.FirstName,
            LastName = model.LastName,
            MiddleName = model.MiddleName,
            StaffNumber = model.StaffNumber,
            Designation = model.Designation,
            Department = model.Department,
            LicenseBody = model.LicenseBody,
            LicenseNumber = model.LicenseNumber,
            LicenseExpiry = model.LicenseExpiry,
            FacilityId = model.FacilityId,
            TenantId = callerTenant.Value,
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            await PopulateRegistrationLists();
            return View(model);
        }

        // Tenant System Admins can't grant SuperAdmin or Patient — those are platform-only / portal-only.
        if (model.Role != Roles.SuperAdmin && model.Role != Roles.Patient && Roles.All.Contains(model.Role))
            await _userManager.AddToRoleAsync(user, model.Role);

        TempData["Success"] = $"Staff '{user.FullName}' created.";
        return RedirectToAction(nameof(StaffList));
    }

    [HttpGet("/staff"), HasPermission(Permissions.StaffManage)]
    public async Task<IActionResult> StaffList(string? q, string? role, bool activeOnly = true)
    {
        // Tenant scope: AspNetUsers is intentionally excluded from the global query filter
        // (auth flows touch it from contexts where the filter would block sign-in lookups),
        // so we apply the scope explicitly here. Falls back to current user's tenant when
        // _tenant.CurrentId is null (e.g. dev override on localhost).
        var currentUser = await _userManager.GetUserAsync(User);
        var tenantId = _tenant.CurrentId ?? currentUser?.TenantId;
        if (tenantId is null)
        {
            TempData["Error"] = "No tenant context — cannot list staff.";
            return RedirectToAction("Index", "Dashboard");
        }

        var query = _userManager.Users
            .Where(u => u.TenantId == tenantId.Value)
            .Where(u => !_db.UserRoles
                .Where(ur => ur.UserId == u.Id)
                .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                .Any(rn => rn == Roles.Patient));

        if (!string.IsNullOrWhiteSpace(q))
        {
            var lower = q.Trim().ToLower();
            query = query.Where(u =>
                u.Email!.ToLower().Contains(lower) ||
                u.FirstName.ToLower().Contains(lower) ||
                u.LastName.ToLower().Contains(lower) ||
                (u.StaffNumber != null && u.StaffNumber.ToLower().Contains(lower)));
        }
        if (activeOnly) query = query.Where(u => u.IsActive);

        var users = await query.OrderBy(u => u.LastName).Take(500).ToListAsync();

        // Pull each user's roles in one batched join.
        var userIds = users.Select(u => u.Id).ToList();
        var roleMap = await (
            from ur in _db.UserRoles
            join r in _db.Roles on ur.RoleId equals r.Id
            where userIds.Contains(ur.UserId)
            select new { ur.UserId, RoleName = r.Name! }
        ).ToListAsync();

        if (!string.IsNullOrWhiteSpace(role))
            users = users.Where(u => roleMap.Any(rm => rm.UserId == u.Id && rm.RoleName == role)).ToList();

        ViewBag.RolesByUser = roleMap.GroupBy(x => x.UserId)
            .ToDictionary(g => g.Key, g => g.Select(x => x.RoleName).OrderBy(n => n).ToList());
        ViewBag.Q = q;
        ViewBag.Role = role;
        ViewBag.ActiveOnly = activeOnly;
        ViewBag.AllRoles = Roles.All.Where(r => r != Roles.Patient && r != Roles.SuperAdmin).OrderBy(r => r).ToList();
        return View(users);
    }

    /// <summary>Edit form for an existing staff member of the current tenant.</summary>
    [HttpGet("/staff/{id}/edit"), HasPermission(Permissions.StaffManage)]
    public async Task<IActionResult> EditStaff(string id)
    {
        var (user, deny) = await LoadTenantUser(id);
        if (deny is not null) return deny;

        ViewBag.Roles = await _userManager.GetRolesAsync(user!);
        ViewBag.AllRoles = Roles.All.Where(r => r != Roles.Patient && r != Roles.SuperAdmin).OrderBy(r => r).ToList();
        ViewBag.Facilities = new SelectList(
            await _db.Facilities.Where(f => f.TenantId == user!.TenantId).OrderBy(f => f.Name).ToListAsync(),
            nameof(Facility.Id), nameof(Facility.Name), user!.FacilityId);
        return View(user);
    }

    [HttpPost("/staff/{id}/edit"), ValidateAntiForgeryToken, HasPermission(Permissions.StaffManage)]
    public async Task<IActionResult> EditStaff(string id, ApplicationUser updated)
    {
        var (user, deny) = await LoadTenantUser(id);
        if (deny is not null) return deny;

        user!.FirstName = updated.FirstName;
        user.LastName = updated.LastName;
        user.MiddleName = updated.MiddleName;
        user.PhoneNumber = updated.PhoneNumber;
        user.Designation = updated.Designation;
        user.Department = updated.Department;
        user.StaffNumber = updated.StaffNumber;
        user.LicenseBody = updated.LicenseBody;
        user.LicenseNumber = updated.LicenseNumber;
        user.LicenseExpiry = updated.LicenseExpiry;
        user.FacilityId = updated.FacilityId;

        await _userManager.UpdateAsync(user);
        TempData["Success"] = $"Updated {user.FullName}.";
        return RedirectToAction(nameof(StaffList));
    }

    [HttpPost("/staff/{id}/toggle-active"), ValidateAntiForgeryToken, HasPermission(Permissions.StaffManage)]
    public async Task<IActionResult> ToggleStaffActive(string id)
    {
        var (user, deny) = await LoadTenantUser(id);
        if (deny is not null) return deny;
        if (user!.UserName == User.Identity?.Name)
        {
            TempData["Error"] = "You can't deactivate yourself.";
            return RedirectToAction(nameof(StaffList));
        }

        user.IsActive = !user.IsActive;
        await _userManager.UpdateAsync(user);
        TempData["Success"] = $"{user.FullName} is now {(user.IsActive ? "active" : "deactivated")}.";
        return RedirectToAction(nameof(StaffList));
    }

    [HttpPost("/staff/{id}/reset-password"), ValidateAntiForgeryToken, HasPermission(Permissions.StaffManage)]
    public async Task<IActionResult> ResetStaffPassword(string id)
    {
        var (user, deny) = await LoadTenantUser(id);
        if (deny is not null) return deny;

        var token = await _userManager.GeneratePasswordResetTokenAsync(user!);
        var temp = GenerateTempPassword();
        var result = await _userManager.ResetPasswordAsync(user!, token, temp);
        if (!result.Succeeded)
        {
            TempData["Error"] = string.Join("; ", result.Errors.Select(e => e.Description));
            return RedirectToAction(nameof(EditStaff), new { id });
        }
        TempData["TempPassword"] = temp;
        TempData["Success"] = $"Password reset for {user!.FullName}. Share this temporary password — they won't be shown it again.";
        return RedirectToAction(nameof(EditStaff), new { id });
    }

    [HttpPost("/staff/{id}/add-role"), ValidateAntiForgeryToken, HasPermission(Permissions.StaffManage)]
    public async Task<IActionResult> AddStaffRole(string id, string role)
    {
        var (user, deny) = await LoadTenantUser(id);
        if (deny is not null) return deny;

        // Tenant admins cannot grant SuperAdmin — that's reserved for the platform owner.
        if (role == Roles.SuperAdmin || !Roles.All.Contains(role))
        {
            TempData["Error"] = "That role can't be assigned from here.";
            return RedirectToAction(nameof(EditStaff), new { id });
        }
        if (!await _roleManager.RoleExistsAsync(role)) await _roleManager.CreateAsync(new IdentityRole(role));
        await _userManager.AddToRoleAsync(user!, role);
        TempData["Success"] = $"Added '{role}' to {user!.FullName}.";
        return RedirectToAction(nameof(EditStaff), new { id });
    }

    [HttpPost("/staff/{id}/remove-role"), ValidateAntiForgeryToken, HasPermission(Permissions.StaffManage)]
    public async Task<IActionResult> RemoveStaffRole(string id, string role)
    {
        var (user, deny) = await LoadTenantUser(id);
        if (deny is not null) return deny;

        if (role == Roles.SuperAdmin)
        {
            TempData["Error"] = "Tenant admins can't change Super Admin role.";
            return RedirectToAction(nameof(EditStaff), new { id });
        }
        await _userManager.RemoveFromRoleAsync(user!, role);
        TempData["Success"] = $"Removed '{role}' from {user!.FullName}.";
        return RedirectToAction(nameof(EditStaff), new { id });
    }

    /// <summary>
    /// Loads a user, but only if they belong to the caller's tenant. Stops a System
    /// Administrator on tenant A from operating on tenant B's users by guessing IDs.
    /// </summary>
    private async Task<(ApplicationUser? User, IActionResult? Deny)> LoadTenantUser(string id)
    {
        var caller = await _userManager.GetUserAsync(User);
        var callerTenant = _tenant.CurrentId ?? caller?.TenantId;
        if (callerTenant is null) return (null, RedirectToAction("Index", "Dashboard"));

        var user = await _userManager.Users.FirstOrDefaultAsync(u => u.Id == id && u.TenantId == callerTenant);
        if (user is null) return (null, NotFound());
        return (user, null);
    }

    private static string GenerateTempPassword()
    {
        var rnd = Random.Shared;
        const string upper = "ABCDEFGHJKLMNPQRSTUVWXYZ";
        const string lower = "abcdefghjkmnpqrstuvwxyz";
        const string digit = "23456789";
        const string sym = "!@#$%&*?";
        Span<char> buf = stackalloc char[12];
        buf[0] = upper[rnd.Next(upper.Length)];
        buf[1] = lower[rnd.Next(lower.Length)];
        buf[2] = digit[rnd.Next(digit.Length)];
        buf[3] = sym[rnd.Next(sym.Length)];
        var pool = upper + lower + digit + sym;
        for (var i = 4; i < buf.Length; i++) buf[i] = pool[rnd.Next(pool.Length)];
        for (var i = buf.Length - 1; i > 0; i--) { var j = rnd.Next(i + 1); (buf[i], buf[j]) = (buf[j], buf[i]); }
        return new string(buf);
    }

    private async Task PopulateRegistrationLists()
    {
        // Tenant admins can assign any role except Patient (portal-only) and SuperAdmin (platform-only).
        var assignableRoles = Roles.All
            .Where(r => r != Roles.Patient && r != Roles.SuperAdmin)
            .OrderBy(r => r);
        ViewBag.Roles = new SelectList(assignableRoles);

        // Facility list is restricted to the caller's tenant — keeps the form honest.
        var caller = await _userManager.GetUserAsync(User);
        var tenantId = _tenant.CurrentId ?? caller?.TenantId;
        var facilities = tenantId.HasValue
            ? await _db.Facilities.Where(f => f.TenantId == tenantId.Value).OrderBy(f => f.Name).ToListAsync()
            : new List<Facility>();
        ViewBag.Facilities = new SelectList(facilities, nameof(Facility.Id), nameof(Facility.Name));
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Dashboard");
    }

    /// <summary>
    /// Sends a freshly-signed-in user to the right home page for their role.
    /// SuperAdmins always land on the platform console — they don't belong to a tenant
    /// and the staff dashboard would either error out or leak the wrong tenant's data.
    /// Everyone else honours the original returnUrl, then falls back to the staff dashboard.
    /// </summary>
    private async Task<IActionResult> RedirectAfterLoginAsync(ApplicationUser user, string? returnUrl)
    {
        if (await _userManager.IsInRoleAsync(user, Roles.SuperAdmin))
            return RedirectToAction("Index", "SuperAdmin");
        return RedirectToLocal(returnUrl);
    }
}
