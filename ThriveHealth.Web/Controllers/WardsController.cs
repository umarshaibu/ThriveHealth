using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Inpatient;
using ThriveHealth.Web.Models.ViewModels;

namespace ThriveHealth.Web.Controllers;

[Authorize]
public class WardsController : Controller
{
    private const string CanManage = Roles.SystemAdministrator + "," + Roles.MedicalDirector + "," + Roles.HrOfficer;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public WardsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
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
    public async Task<IActionResult> Board()
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return NotFound();

        var wards = await _db.Wards.AsNoTracking()
            .Include(w => w.Beds)
            .Where(w => w.FacilityId == fid && w.IsActive)
            .OrderBy(w => w.Name)
            .ToListAsync();

        var bedIds = wards.SelectMany(w => w.Beds.Select(b => b.Id)).ToList();
        var admissions = await _db.Admissions.AsNoTracking()
            .Include(a => a.Patient)
            .Where(a => a.FacilityId == fid && a.Status == AdmissionStatus.Active && bedIds.Contains(a.BedId))
            .ToDictionaryAsync(a => a.BedId);

        var bedViews = wards.Select(w => new WardWithBeds
        {
            Ward = w,
            Beds = w.Beds.OrderBy(b => b.BedNumber).Select(b => new BedView
            {
                Bed = b,
                CurrentAdmission = admissions.GetValueOrDefault(b.Id),
                CurrentPatient = admissions.GetValueOrDefault(b.Id)?.Patient
            }).ToList()
        }).ToList();

        var allBeds = wards.SelectMany(w => w.Beds).ToList();
        ViewBag.FacilityId = fid.Value;
        return View(new BedBoardViewModel
        {
            Wards = bedViews,
            TotalBeds = allBeds.Count,
            FreeBeds = allBeds.Count(b => b.Status == BedStatus.Free),
            OccupiedBeds = allBeds.Count(b => b.Status == BedStatus.Occupied),
            OutOfServiceBeds = allBeds.Count(b => b.Status == BedStatus.Maintenance || b.Status == BedStatus.Cleaning || b.Status == BedStatus.Blocked)
        });
    }

    [HttpGet, HasPermission(Permissions.WardsManage)]
    public async Task<IActionResult> Index()
    {
        var fid = await FacilityIdAsync();
        var wards = await _db.Wards.Include(w => w.Beds).Where(w => w.FacilityId == fid).OrderBy(w => w.Name).ToListAsync();
        return View(wards);
    }

    [HttpGet, HasPermission(Permissions.WardsManage)]
    public IActionResult Create() => View(new WardEditViewModel());

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.WardsManage)]
    public async Task<IActionResult> Create(WardEditViewModel m)
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return NotFound();
        if (!ModelState.IsValid) return View(m);

        _db.Wards.Add(new Ward
        {
            FacilityId = fid.Value,
            Name = m.Name, Code = m.Code.ToUpperInvariant(),
            Type = m.Type, ColorHex = m.ColorHex, IsActive = m.IsActive
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Ward created.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, HasPermission(Permissions.WardsManage)]
    public async Task<IActionResult> Edit(int id)
    {
        var fid = await FacilityIdAsync();
        var w = await _db.Wards.FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == fid);
        if (w is null) return NotFound();
        return View(new WardEditViewModel
        {
            Id = w.Id, Name = w.Name, Code = w.Code, Type = w.Type, ColorHex = w.ColorHex, IsActive = w.IsActive
        });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.WardsManage)]
    public async Task<IActionResult> Edit(WardEditViewModel m)
    {
        var fid = await FacilityIdAsync();
        var w = await _db.Wards.FirstOrDefaultAsync(x => x.Id == m.Id && x.FacilityId == fid);
        if (w is null) return NotFound();
        if (!ModelState.IsValid) return View(m);

        w.Name = m.Name; w.Code = m.Code.ToUpperInvariant();
        w.Type = m.Type; w.ColorHex = m.ColorHex; w.IsActive = m.IsActive;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Ward updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.WardsManage)]
    public async Task<IActionResult> AddBed(int wardId, string bedNumber, BedRestriction restriction)
    {
        var fid = await FacilityIdAsync();
        var w = await _db.Wards.FirstOrDefaultAsync(x => x.Id == wardId && x.FacilityId == fid);
        if (w is null) return NotFound();

        if (!string.IsNullOrWhiteSpace(bedNumber))
        {
            _db.Beds.Add(new Bed { WardId = wardId, BedNumber = bedNumber, Restriction = restriction });
            await _db.SaveChangesAsync();
        }
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.WardsManage)]
    public async Task<IActionResult> SetBedStatus(int bedId, BedStatus status)
    {
        var fid = await FacilityIdAsync();
        var b = await _db.Beds.Include(x => x.Ward).FirstOrDefaultAsync(x => x.Id == bedId && x.Ward!.FacilityId == fid);
        if (b is null) return NotFound();
        if (b.Status == BedStatus.Occupied) return BadRequest("Cannot change status of an occupied bed; discharge first.");
        b.Status = status;
        b.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        return RedirectToAction(nameof(Board));
    }
}
