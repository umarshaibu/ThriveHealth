using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Scheduling;
using ThriveHealth.Web.Models.ViewModels;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.ClinicsManage)]
public class ClinicsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public ClinicsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db;
        _userManager = userManager;
    }

    private async Task<int?> FacilityIdAsync()
    {
        var u = await _userManager.GetUserAsync(User);
        return u?.FacilityId;
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return NotFound();

        var clinics = await _db.Clinics.AsNoTracking()
            .Where(c => c.FacilityId == fid)
            .OrderBy(c => c.Name)
            .ToListAsync();
        return View(clinics);
    }

    [HttpGet]
    public IActionResult Create() => View(new ClinicEditViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(ClinicEditViewModel model)
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return NotFound();
        if (!ModelState.IsValid) return View(model);

        _db.Clinics.Add(new Clinic
        {
            FacilityId = fid.Value,
            Name = model.Name,
            Code = model.Code.ToUpperInvariant(),
            Specialty = model.Specialty,
            DefaultSlotMinutes = model.DefaultSlotMinutes,
            ColorHex = model.ColorHex,
            IsActive = model.IsActive
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Clinic '{model.Name}' created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Edit(int id)
    {
        var fid = await FacilityIdAsync();
        var c = await _db.Clinics.FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == fid);
        if (c is null) return NotFound();
        return View(new ClinicEditViewModel
        {
            Id = c.Id, Name = c.Name, Code = c.Code, Specialty = c.Specialty,
            DefaultSlotMinutes = c.DefaultSlotMinutes, ColorHex = c.ColorHex, IsActive = c.IsActive
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Edit(ClinicEditViewModel model)
    {
        var fid = await FacilityIdAsync();
        var c = await _db.Clinics.FirstOrDefaultAsync(x => x.Id == model.Id && x.FacilityId == fid);
        if (c is null) return NotFound();
        if (!ModelState.IsValid) return View(model);

        c.Name = model.Name;
        c.Code = model.Code.ToUpperInvariant();
        c.Specialty = model.Specialty;
        c.DefaultSlotMinutes = model.DefaultSlotMinutes;
        c.ColorHex = model.ColorHex;
        c.IsActive = model.IsActive;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Clinic updated.";
        return RedirectToAction(nameof(Details), new { id = c.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var fid = await FacilityIdAsync();
        var c = await _db.Clinics.FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == fid);
        if (c is null) return NotFound();

        ViewBag.Availability = await _db.ClinicianAvailabilities
            .Include(a => a.Clinician)
            .Include(a => a.Room)
            .Where(a => a.ClinicId == id)
            .OrderBy(a => a.DayOfWeek).ThenBy(a => a.StartTime)
            .ToListAsync();
        return View(c);
    }

    [HttpGet]
    public async Task<IActionResult> AddAvailability(int clinicId)
    {
        var fid = await FacilityIdAsync();
        var c = await _db.Clinics.FirstOrDefaultAsync(x => x.Id == clinicId && x.FacilityId == fid);
        if (c is null) return NotFound();
        await PopulateLists(fid!.Value);
        return View(new AvailabilityEditViewModel { ClinicId = clinicId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddAvailability(AvailabilityEditViewModel model)
    {
        var fid = await FacilityIdAsync();
        var c = await _db.Clinics.FirstOrDefaultAsync(x => x.Id == model.ClinicId && x.FacilityId == fid);
        if (c is null) return NotFound();

        if (model.EndTime <= model.StartTime)
            ModelState.AddModelError(nameof(model.EndTime), "End time must be after start time.");

        if (!ModelState.IsValid)
        {
            await PopulateLists(fid!.Value);
            return View(model);
        }

        _db.ClinicianAvailabilities.Add(new ClinicianAvailability
        {
            ClinicId = model.ClinicId,
            ClinicianId = model.ClinicianId,
            RoomId = model.RoomId,
            DayOfWeek = model.DayOfWeek,
            StartTime = model.StartTime,
            EndTime = model.EndTime,
            SlotMinutesOverride = model.SlotMinutesOverride,
            IsActive = true
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Availability added.";
        return RedirectToAction(nameof(Details), new { id = model.ClinicId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveAvailability(int id)
    {
        var avail = await _db.ClinicianAvailabilities
            .Include(a => a.Clinic)
            .FirstOrDefaultAsync(a => a.Id == id);
        if (avail is null) return NotFound();
        var fid = await FacilityIdAsync();
        if (avail.Clinic?.FacilityId != fid) return NotFound();

        _db.ClinicianAvailabilities.Remove(avail);
        await _db.SaveChangesAsync();
        TempData["Success"] = "Availability removed.";
        return RedirectToAction(nameof(Details), new { id = avail.ClinicId });
    }

    private async Task PopulateLists(int facilityId)
    {
        var clinicianRoles = new[] { Roles.Doctor, Roles.Consultant, Roles.MedicalOfficer,
            Roles.Nurse, Roles.Midwife, Roles.ChiefNursingOfficer,
            Roles.Pharmacist, Roles.Physiotherapist, Roles.Radiographer };

        var roleIds = await _db.Roles
            .Where(r => clinicianRoles.Contains(r.Name!))
            .Select(r => r.Id)
            .ToListAsync();

        var clinicianIds = await _db.UserRoles
            .Where(ur => roleIds.Contains(ur.RoleId))
            .Select(ur => ur.UserId)
            .Distinct()
            .ToListAsync();

        var clinicians = await _db.Users
            .Where(u => clinicianIds.Contains(u.Id) && u.FacilityId == facilityId && u.IsActive)
            .OrderBy(u => u.LastName)
            .Select(u => new { u.Id, Display = u.FirstName + " " + u.LastName + (u.Designation != null ? " · " + u.Designation : "") })
            .ToListAsync();

        ViewBag.Clinicians = new SelectList(clinicians, "Id", "Display");
        ViewBag.Rooms = new SelectList(
            await _db.Rooms.Where(r => r.FacilityId == facilityId && r.IsActive).OrderBy(r => r.Name).ToListAsync(),
            nameof(Room.Id), nameof(Room.Name));
    }
}
