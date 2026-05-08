using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Hr;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers;

[Authorize]
public class HrController : Controller
{
    public const string HrStaff = Roles.HrOfficer + "," + Roles.SystemAdministrator + "," + Roles.MedicalDirector + "," +
        Roles.ChiefExecutive + "," + Roles.ChiefFinancialOfficer + "," + Roles.ChiefNursingOfficer;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPermissionService _perms;
    public HrController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IPermissionService perms)
    {
        _db = db; _userManager = userManager; _perms = perms;
    }

    private async Task<int?> FacilityIdAsync()
    {
        var u = await _userManager.GetUserAsync(User);
        return u?.FacilityId;
    }

    [HttpGet, HasPermission(Permissions.HrRead)]
    public async Task<IActionResult> Index(string? q)
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return NotFound();

        var users = _db.Users.AsNoTracking()
            .Where(u => u.FacilityId == fid && u.IsActive);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            users = users.Where(u =>
                EF.Functions.ILike(u.FirstName, like) ||
                EF.Functions.ILike(u.LastName, like) ||
                (u.StaffNumber != null && EF.Functions.ILike(u.StaffNumber, like)) ||
                (u.LicenseNumber != null && EF.Functions.ILike(u.LicenseNumber, like)));
        }

        var list = await users.OrderBy(u => u.LastName).ThenBy(u => u.FirstName).Take(500).ToListAsync();
        var profiles = await _db.HrProfiles.AsNoTracking()
            .Where(p => list.Select(u => u.Id).Contains(p.UserId))
            .ToDictionaryAsync(p => p.UserId);

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var soon = today.AddDays(60);

        var rows = list.Select(u => new HrStaffRow
        {
            User = u,
            Profile = profiles.GetValueOrDefault(u.Id),
            LicenseExpired = u.LicenseExpiry.HasValue && DateOnly.FromDateTime(u.LicenseExpiry.Value) < today,
            LicenseExpiringSoon = u.LicenseExpiry.HasValue && DateOnly.FromDateTime(u.LicenseExpiry.Value) >= today
                                  && DateOnly.FromDateTime(u.LicenseExpiry.Value) <= soon
        }).ToList();

        return View(new HrStaffListViewModel
        {
            Staff = rows,
            Total = rows.Count,
            LicenseExpiredCount = rows.Count(r => r.LicenseExpired),
            LicenseExpiringSoonCount = rows.Count(r => r.LicenseExpiringSoon),
            Search = q
        });
    }

    [HttpGet, HasPermission(Permissions.HrRead)]
    public async Task<IActionResult> Profile(string id)
    {
        var fid = await FacilityIdAsync();
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == fid);
        if (u is null) return NotFound();
        var p = await _db.HrProfiles.FirstOrDefaultAsync(x => x.UserId == id);

        return View(new HrProfileEditViewModel
        {
            UserId = u.Id,
            FullName = u.FullName,
            StaffNumber = u.StaffNumber,
            Designation = u.Designation,
            LicenseBody = u.LicenseBody,
            LicenseNumber = u.LicenseNumber,
            LicenseExpiry = u.LicenseExpiry.HasValue ? DateOnly.FromDateTime(u.LicenseExpiry.Value) : null,
            DateOfBirth = p?.DateOfBirth,
            HireDate = p?.HireDate,
            EmploymentType = p?.EmploymentType ?? EmploymentType.Permanent,
            Status = p?.Status ?? EmploymentStatus.Active,
            GradeLevel = p?.GradeLevel,
            Position = p?.Position ?? u.Designation,
            UnitOrSection = p?.UnitOrSection ?? u.Department,
            GrossMonthlySalary = p?.GrossMonthlySalary,
            PfaPin = p?.PfaPin,
            NhfNumber = p?.NhfNumber,
            PayeTin = p?.PayeTin,
            BankName = p?.BankName,
            BankAccountNumber = p?.BankAccountNumber,
            EmergencyContactName = p?.EmergencyContactName,
            EmergencyContactPhone = p?.EmergencyContactPhone
        });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.HrEdit)]
    public async Task<IActionResult> Profile(HrProfileEditViewModel m)
    {
        var fid = await FacilityIdAsync();
        var u = await _db.Users.FirstOrDefaultAsync(x => x.Id == m.UserId && x.FacilityId == fid);
        if (u is null) return NotFound();

        u.LicenseBody = m.LicenseBody;
        u.LicenseNumber = m.LicenseNumber;
        u.LicenseExpiry = m.LicenseExpiry?.ToDateTime(TimeOnly.MinValue);
        u.Designation = m.Position ?? u.Designation;
        u.Department = m.UnitOrSection ?? u.Department;

        var p = await _db.HrProfiles.FirstOrDefaultAsync(x => x.UserId == m.UserId);
        if (p is null)
        {
            p = new HrProfile { UserId = m.UserId };
            _db.HrProfiles.Add(p);
        }
        p.DateOfBirth = m.DateOfBirth;
        p.HireDate = m.HireDate;
        p.EmploymentType = m.EmploymentType;
        p.Status = m.Status;
        p.GradeLevel = m.GradeLevel;
        p.Position = m.Position;
        p.UnitOrSection = m.UnitOrSection;
        p.GrossMonthlySalary = m.GrossMonthlySalary;
        p.PfaPin = m.PfaPin;
        p.NhfNumber = m.NhfNumber;
        p.PayeTin = m.PayeTin;
        p.BankName = m.BankName;
        p.BankAccountNumber = m.BankAccountNumber;
        p.EmergencyContactName = m.EmergencyContactName;
        p.EmergencyContactPhone = m.EmergencyContactPhone;
        p.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync();
        TempData["Success"] = "HR profile updated.";
        return RedirectToAction(nameof(Profile), new { id = m.UserId });
    }

    [HttpGet, HasPermission(Permissions.RosterManage)]
    public async Task<IActionResult> Roster(DateOnly? weekStart)
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return NotFound();

        var ws = weekStart ?? DateOnly.FromDateTime(DateTime.UtcNow);
        ws = ws.AddDays(-(int)ws.DayOfWeek + (ws.DayOfWeek == DayOfWeek.Sunday ? -6 : 1));
        var days = Enumerable.Range(0, 7).Select(i => ws.AddDays(i)).ToList();
        var weekEnd = ws.AddDays(7);

        var clinicalRoles = new[] { Roles.Doctor, Roles.Consultant, Roles.MedicalOfficer, Roles.Nurse, Roles.Midwife, Roles.ChiefNursingOfficer };
        var roleIds = await _db.Roles.Where(r => clinicalRoles.Contains(r.Name!)).Select(r => r.Id).ToListAsync();
        var clinicianIds = await _db.UserRoles.Where(ur => roleIds.Contains(ur.RoleId)).Select(ur => ur.UserId).Distinct().ToListAsync();

        var staff = await _db.Users.AsNoTracking()
            .Where(u => clinicianIds.Contains(u.Id) && u.FacilityId == fid && u.IsActive)
            .OrderBy(u => u.LastName).ToListAsync();

        var shifts = await _db.RosterShifts.AsNoTracking()
            .Where(s => s.FacilityId == fid && s.Date >= ws && s.Date < weekEnd)
            .ToListAsync();

        var rows = staff.Select(s => new RosterRow
        {
            Staff = s,
            ByDay = days.ToDictionary(d => d, d => (IReadOnlyList<RosterShift>)shifts.Where(x => x.StaffId == s.Id && x.Date == d).ToList())
        }).ToList();

        return View(new RosterGridViewModel { WeekStart = ws, Days = days, Rows = rows });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.RosterManage)]
    public async Task<IActionResult> AddShift(RosterShiftEditViewModel m)
    {
        var fid = await FacilityIdAsync();
        if (fid is null || string.IsNullOrEmpty(m.StaffId)) return NotFound();
        var u = await _userManager.GetUserAsync(User);

        _db.RosterShifts.Add(new RosterShift
        {
            FacilityId = fid.Value,
            StaffId = m.StaffId,
            Date = m.Date,
            ShiftType = m.ShiftType,
            WardId = m.WardId,
            ClinicId = m.ClinicId,
            Assignment = m.Assignment,
            Notes = m.Notes,
            CreatedById = u?.Id
        });
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Roster), new { weekStart = m.Date.AddDays(-(int)m.Date.DayOfWeek + (m.Date.DayOfWeek == DayOfWeek.Sunday ? -6 : 1)) });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.RosterManage)]
    public async Task<IActionResult> RemoveShift(int id, DateOnly weekStart)
    {
        var fid = await FacilityIdAsync();
        var s = await _db.RosterShifts.FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == fid);
        if (s is null) return NotFound();
        _db.RosterShifts.Remove(s);
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Roster), new { weekStart });
    }

    [HttpGet, HasPermission(Permissions.LeaveRequest)]
    public async Task<IActionResult> Leave(LeaveStatus? status)
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return NotFound();
        var u = await _userManager.GetUserAsync(User);
        if (u is null) return Challenge();

        // Managers (LeaveDecide) see every request in the facility; everyone else sees only their own.
        var canDecide = await _perms.UserHasAsync(User, Permissions.LeaveDecide);
        var query = _db.LeaveRequests.AsNoTracking()
            .Include(l => l.Staff)
            .Include(l => l.DecidedBy)
            .Where(l => l.Staff!.FacilityId == fid);
        if (!canDecide) query = query.Where(l => l.StaffId == u.Id);
        if (status.HasValue) query = query.Where(l => l.Status == status.Value);
        var rows = await query.OrderByDescending(l => l.CreatedAt).Take(200).ToListAsync();
        ViewBag.Status = status;
        ViewBag.CanDecide = canDecide;
        return View(rows);
    }

    [HttpGet, HasPermission(Permissions.LeaveRequest)]
    public IActionResult RequestLeave() => View(new LeaveRequestEditViewModel());

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.LeaveRequest)]
    public async Task<IActionResult> RequestLeave(LeaveRequestEditViewModel m)
    {
        if (m.EndDate < m.StartDate) ModelState.AddModelError(nameof(m.EndDate), "End must be on or after start.");
        if (!ModelState.IsValid) return View(m);
        var u = await _userManager.GetUserAsync(User);
        if (u is null) return Challenge();

        _db.LeaveRequests.Add(new LeaveRequest
        {
            StaffId = u.Id,
            Type = m.Type,
            StartDate = m.StartDate,
            EndDate = m.EndDate,
            Days = m.EndDate.DayNumber - m.StartDate.DayNumber + 1,
            Reason = m.Reason,
            Status = LeaveStatus.Submitted
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Leave request submitted.";
        return RedirectToAction(nameof(Leave));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.LeaveDecide)]
    public async Task<IActionResult> DecideLeave(int id, LeaveStatus decision, string? notes)
    {
        var fid = await FacilityIdAsync();
        var leave = await _db.LeaveRequests.Include(l => l.Staff).FirstOrDefaultAsync(l => l.Id == id);
        if (leave is null || leave.Staff?.FacilityId != fid) return NotFound();
        var u = await _userManager.GetUserAsync(User);

        leave.Status = decision == LeaveStatus.Approved ? LeaveStatus.Approved : LeaveStatus.Rejected;
        leave.DecisionNotes = notes;
        leave.DecidedAt = DateTime.UtcNow;
        leave.DecidedById = u?.Id;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Leave request {decision}.";
        return RedirectToAction(nameof(Leave));
    }
}
