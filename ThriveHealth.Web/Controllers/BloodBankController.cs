using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.BloodBank;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Maternity;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.BloodBankManage)]
public class BloodBankController : Controller
{
    public const string BbStaff = Roles.LabScientist + "," + Roles.LabTechnician + "," +
        Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.Nurse + "," + Roles.Midwife + "," + Roles.ChiefNursingOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBatch13Numbering _numbering;

    public BloodBankController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IBatch13Numbering numbering)
    {
        _db = db; _userManager = userManager; _numbering = numbering;
    }

    private async Task<(int facilityId, string userId)?> Ctx()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u?.FacilityId is null) return null;
        return (u.FacilityId.Value, u.Id);
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var units = await _db.BloodUnits.AsNoTracking()
            .Where(u => u.FacilityId == ctx.Value.facilityId)
            .ToListAsync();
        var available = units.Where(u => u.Status == BloodUnitStatus.Available).ToList();

        var inventory = available
            .GroupBy(u => (u.BloodGroup, u.Component))
            .ToDictionary(g => g.Key, g => g.Count());

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekAhead = today.AddDays(7);

        var openCm = await _db.BloodCrossMatches.AsNoTracking()
            .Include(c => c.Patient)
            .Where(c => c.FacilityId == ctx.Value.facilityId
                     && (c.Status == CrossMatchStatus.Requested || c.Status == CrossMatchStatus.Compatible))
            .OrderByDescending(c => c.CreatedAt).Take(20).ToListAsync();

        return View(new BloodBankDashboardViewModel
        {
            Inventory = inventory,
            TotalAvailable = available.Count,
            Reserved = units.Count(u => u.Status == BloodUnitStatus.Reserved || u.Status == BloodUnitStatus.CrossMatched),
            ExpiringIn7Days = available.Count(u => u.ExpiryDate <= weekAhead),
            OpenCrossMatches = openCm.Count,
            RecentUnits = units.OrderByDescending(u => u.CreatedAt).Take(20).ToList(),
            OpenRequests = openCm
        });
    }

    [HttpGet]
    public async Task<IActionResult> Donors(string? q)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var query = _db.BloodDonors.AsNoTracking().Where(d => d.FacilityId == ctx.Value.facilityId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(d => EF.Functions.ILike(d.FullName, like) || EF.Functions.ILike(d.DonorNumber, like));
        }
        var rows = await query.OrderByDescending(d => d.CreatedAt).Take(300).ToListAsync();
        ViewBag.Search = q;
        return View(rows);
    }

    [HttpGet]
    public IActionResult NewDonor() => View(new BloodDonorInputViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NewDonor(BloodDonorInputViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        if (!ModelState.IsValid) return View(m);

        var donor = new BloodDonor
        {
            FacilityId = ctx.Value.facilityId,
            DonorNumber = await _numbering.NextDonorAsync(ctx.Value.facilityId),
            FullName = m.FullName,
            DateOfBirth = m.DateOfBirth,
            Sex = m.Sex,
            Phone = m.Phone,
            Address = m.Address,
            BloodGroup = m.BloodGroup,
            RhesusPositive = m.RhesusPositive,
            DonorType = m.DonorType,
            Status = m.Status,
            HivNegative = m.HivNegative,
            HepBNegative = m.HepBNegative,
            HepCNegative = m.HepCNegative,
            VdrlNegative = m.VdrlNegative,
            MalariaNegative = m.MalariaNegative,
            DeferralReason = m.DeferralReason,
            Notes = m.Notes
        };
        _db.BloodDonors.Add(donor);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Donor registered · {donor.DonorNumber}";
        return RedirectToAction(nameof(Donors));
    }

    [HttpGet]
    public async Task<IActionResult> Units(BloodUnitStatus? status)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var query = _db.BloodUnits.AsNoTracking()
            .Include(u => u.BloodDonor)
            .Where(u => u.FacilityId == ctx.Value.facilityId);
        if (status.HasValue) query = query.Where(u => u.Status == status.Value);
        var rows = await query.OrderByDescending(u => u.CreatedAt).Take(300).ToListAsync();
        ViewBag.FilterStatus = status;
        return View(rows);
    }

    [HttpGet]
    public IActionResult NewUnit() => View(new BloodUnitInputViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NewUnit(BloodUnitInputViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        if (m.ExpiryDate <= m.CollectionDate) ModelState.AddModelError(nameof(m.ExpiryDate), "Expiry must be after collection date.");
        if (!ModelState.IsValid) return View(m);

        var screeningComplete = m.HivNegative && m.HepBNegative && m.HepCNegative && m.VdrlNegative && m.MalariaNegative;

        var unit = new BloodUnit
        {
            FacilityId = ctx.Value.facilityId,
            UnitNumber = await _numbering.NextBloodUnitAsync(ctx.Value.facilityId),
            BloodDonorId = m.BloodDonorId,
            Component = m.Component,
            BloodGroup = m.BloodGroup,
            RhesusPositive = m.RhesusPositive,
            CollectionDate = m.CollectionDate,
            ExpiryDate = m.ExpiryDate,
            VolumeMl = m.VolumeMl,
            HivNegative = m.HivNegative,
            HepBNegative = m.HepBNegative,
            HepCNegative = m.HepCNegative,
            VdrlNegative = m.VdrlNegative,
            MalariaNegative = m.MalariaNegative,
            ScreeningComplete = screeningComplete,
            Status = screeningComplete ? BloodUnitStatus.Available : BloodUnitStatus.Quarantined,
            Notes = m.Notes
        };
        _db.BloodUnits.Add(unit);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Unit registered · {unit.UnitNumber} ({(screeningComplete ? "Available" : "Quarantined")})";
        return RedirectToAction(nameof(Units));
    }

    [HttpGet]
    public async Task<IActionResult> CrossMatches()
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var rows = await _db.BloodCrossMatches.AsNoTracking()
            .Include(c => c.Patient).Include(c => c.RequestedBy).Include(c => c.Units)
            .Where(c => c.FacilityId == ctx.Value.facilityId)
            .OrderByDescending(c => c.CreatedAt).Take(200).ToListAsync();
        return View(rows);
    }

    [HttpGet]
    public IActionResult NewCrossMatch(int? patientId, BloodGroup? group) => View(new CrossMatchInputViewModel
    {
        PatientId = patientId,
        PatientBloodGroup = group ?? BloodGroup.Unknown
    });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> NewCrossMatch(CrossMatchInputViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        if (m.PatientId is null) ModelState.AddModelError(nameof(m.PatientId), "Pick a patient.");
        if (!ModelState.IsValid) return View(m);

        var cm = new BloodCrossMatch
        {
            FacilityId = ctx.Value.facilityId,
            CrossMatchNumber = await _numbering.NextCrossMatchAsync(ctx.Value.facilityId),
            PatientId = m.PatientId!.Value,
            PatientBloodGroup = m.PatientBloodGroup,
            PatientRhesusPositive = m.PatientRhesusPositive,
            Component = m.Component,
            UnitsRequested = m.UnitsRequested,
            RequiredBy = m.RequiredBy,
            Indication = m.Indication,
            Notes = m.Notes,
            RequestedById = ctx.Value.userId
        };
        _db.BloodCrossMatches.Add(cm);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Cross-match request created · {cm.CrossMatchNumber}";
        return RedirectToAction(nameof(CrossMatchDetail), new { id = cm.Id });
    }

    [HttpGet]
    public async Task<IActionResult> CrossMatchDetail(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var cm = await _db.BloodCrossMatches
            .Include(c => c.Patient).Include(c => c.RequestedBy).Include(c => c.CompatibilityCheckedBy)
            .Include(c => c.Units).ThenInclude(u => u.BloodDonor)
            .FirstOrDefaultAsync(c => c.Id == id && c.FacilityId == ctx.Value.facilityId);
        if (cm is null) return NotFound();

        var compatible = await _db.BloodUnits.AsNoTracking()
            .Where(u => u.FacilityId == ctx.Value.facilityId
                     && u.Status == BloodUnitStatus.Available
                     && u.BloodGroup == cm.PatientBloodGroup
                     && u.RhesusPositive == cm.PatientRhesusPositive
                     && u.Component == cm.Component
                     && u.ExpiryDate >= DateOnly.FromDateTime(DateTime.UtcNow))
            .OrderBy(u => u.ExpiryDate).Take(20).ToListAsync();
        ViewBag.Compatible = compatible;
        return View(cm);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ReserveUnit(int crossMatchId, int unitId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var cm = await _db.BloodCrossMatches.FirstOrDefaultAsync(c => c.Id == crossMatchId && c.FacilityId == ctx.Value.facilityId);
        var unit = await _db.BloodUnits.FirstOrDefaultAsync(u => u.Id == unitId && u.FacilityId == ctx.Value.facilityId);
        if (cm is null || unit is null) return NotFound();
        if (unit.Status != BloodUnitStatus.Available) { TempData["Error"] = "Unit is not available."; return RedirectToAction(nameof(CrossMatchDetail), new { id = crossMatchId }); }

        unit.Status = BloodUnitStatus.CrossMatched;
        unit.CrossMatchId = cm.Id;
        unit.ReservedForPatientId = cm.PatientId;
        cm.Status = CrossMatchStatus.Compatible;
        cm.CompatibilityCheckedById = ctx.Value.userId;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Unit {unit.UnitNumber} cross-matched to {cm.CrossMatchNumber}";
        return RedirectToAction(nameof(CrossMatchDetail), new { id = crossMatchId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> IssueUnits(int crossMatchId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var cm = await _db.BloodCrossMatches
            .Include(c => c.Units)
            .FirstOrDefaultAsync(c => c.Id == crossMatchId && c.FacilityId == ctx.Value.facilityId);
        if (cm is null) return NotFound();
        if (!cm.Units.Any(u => u.Status == BloodUnitStatus.CrossMatched))
        {
            TempData["Error"] = "No cross-matched units to issue.";
            return RedirectToAction(nameof(CrossMatchDetail), new { id = crossMatchId });
        }
        foreach (var u in cm.Units.Where(u => u.Status == BloodUnitStatus.CrossMatched))
            u.Status = BloodUnitStatus.Issued;
        cm.Status = CrossMatchStatus.Issued;
        cm.IssuedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Issued {cm.Units.Count(u => u.Status == BloodUnitStatus.Issued)} unit(s) to ward.";
        return RedirectToAction(nameof(CrossMatchDetail), new { id = crossMatchId });
    }
}
