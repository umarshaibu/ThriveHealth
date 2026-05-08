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

    public AccountController(
        UserManager<ApplicationUser> userManager,
        SignInManager<ApplicationUser> signInManager,
        RoleManager<IdentityRole> roleManager,
        ApplicationDbContext db,
        ILogger<AccountController> logger,
        IAuditService audit)
    {
        _userManager = userManager;
        _signInManager = signInManager;
        _roleManager = roleManager;
        _db = db;
        _logger = logger;
        _audit = audit;
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl = null)
    {
        ViewData["ReturnUrl"] = returnUrl;
        return View(new LoginViewModel { ReturnUrl = returnUrl });
    }

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("auth")]
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
            return RedirectToLocal(model.ReturnUrl);
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

    [HttpPost, ValidateAntiForgeryToken]
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

    [HttpGet, AllowAnonymous]
    public IActionResult AccessDenied() => View();

    [HttpGet]
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

    [HttpPost, ValidateAntiForgeryToken]
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

    [HttpGet]
    public IActionResult ChangePassword() => View();

    [HttpPost, ValidateAntiForgeryToken]
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

    [HttpGet, HasPermission(Permissions.StaffManage)]
    public async Task<IActionResult> RegisterStaff()
    {
        await PopulateRegistrationLists();
        return View(new RegisterStaffViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.StaffManage)]
    public async Task<IActionResult> RegisterStaff(RegisterStaffViewModel model)
    {
        if (!ModelState.IsValid)
        {
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
            IsActive = true
        };

        var result = await _userManager.CreateAsync(user, model.Password);
        if (!result.Succeeded)
        {
            foreach (var e in result.Errors) ModelState.AddModelError(string.Empty, e.Description);
            await PopulateRegistrationLists();
            return View(model);
        }

        if (Roles.All.Contains(model.Role))
            await _userManager.AddToRoleAsync(user, model.Role);

        TempData["Success"] = $"Staff '{user.FullName}' created.";
        return RedirectToAction(nameof(StaffList));
    }

    [HttpGet, HasPermission(Permissions.StaffManage)]
    public async Task<IActionResult> StaffList()
    {
        var users = await _userManager.Users
            .Where(u => !_db.UserRoles
                .Where(ur => ur.UserId == u.Id)
                .Join(_db.Roles, ur => ur.RoleId, r => r.Id, (ur, r) => r.Name)
                .Any(rn => rn == Roles.Patient))
            .OrderBy(u => u.LastName)
            .Take(500)
            .ToListAsync();
        return View(users);
    }

    private async Task PopulateRegistrationLists()
    {
        var nonPatientRoles = Roles.All.Where(r => r != Roles.Patient).OrderBy(r => r);
        ViewBag.Roles = new SelectList(nonPatientRoles);
        ViewBag.Facilities = new SelectList(
            await _db.Facilities.OrderBy(f => f.Name).ToListAsync(),
            nameof(Facility.Id), nameof(Facility.Name));
    }

    private IActionResult RedirectToLocal(string? returnUrl)
    {
        if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
            return Redirect(returnUrl);
        return RedirectToAction("Index", "Dashboard");
    }
}
