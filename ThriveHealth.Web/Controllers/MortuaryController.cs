using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Ai;
using ThriveHealth.Web.Models.Mortuary;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;
using ThriveHealth.Web.Services.Ai;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.MortuaryManage)]
public class MortuaryController : Controller
{
    public const string MortuaryStaff = Roles.RecordsOfficer + "," + Roles.Receptionist + "," +
        Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.Nurse + "," + Roles.SystemAdministrator + "," + Roles.MedicalDirector + "," + Roles.HrOfficer;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBatch13Numbering _numbering;
    private readonly IClinicalAiService _ai;

    public MortuaryController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IBatch13Numbering numbering, IClinicalAiService ai)
    {
        _db = db; _userManager = userManager; _numbering = numbering; _ai = ai;
    }

    private async Task<(int facilityId, string userId)?> Ctx()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u?.FacilityId is null) return null;
        return (u.FacilityId.Value, u.Id);
    }

    [HttpGet]
    public async Task<IActionResult> Index(MortuaryStatus? status)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var query = _db.MortuaryEntries.AsNoTracking()
            .Include(e => e.Patient).Include(e => e.ReceivedBy).Include(e => e.ReleasedBy)
            .Where(e => e.FacilityId == ctx.Value.facilityId);
        if (status.HasValue) query = query.Where(e => e.Status == status.Value);

        var entries = await query.OrderByDescending(e => e.ReceivedAt).Take(300).ToListAsync();
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var current = entries.Where(e => e.Status != MortuaryStatus.Released && e.Status != MortuaryStatus.Buried && e.Status != MortuaryStatus.Transferred).ToList();

        return View(new MortuaryListViewModel
        {
            Entries = entries,
            FilterStatus = status,
            CurrentBodies = current.Count,
            ReleasedThisMonth = entries.Count(e => e.ReleasedAt.HasValue && e.ReleasedAt.Value >= monthStart),
            OverdueLengthOfStay = current.Count(e => (DateTime.UtcNow - e.ReceivedAt).TotalDays > 14)
        });
    }

    [HttpGet]
    public IActionResult Receive() => View(new MortuaryInputViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Receive(MortuaryInputViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        if (!ModelState.IsValid) return View(m);

        var entry = new MortuaryEntry
        {
            FacilityId = ctx.Value.facilityId,
            MortuaryNumber = await _numbering.NextMortuaryAsync(ctx.Value.facilityId),
            CabinetCode = m.CabinetCode,
            PatientId = m.PatientId,
            IsUnidentified = m.IsUnidentified,
            DeceasedName = m.DeceasedName,
            Sex = m.Sex,
            DateOfBirth = m.DateOfBirth,
            AgeYears = m.AgeYears,
            Tribe = m.Tribe,
            AddressOfOrigin = m.AddressOfOrigin,
            DateOfDeathUtc = m.DateOfDeathUtc,
            PlaceOfDeath = m.PlaceOfDeath,
            CauseOfDeath = m.CauseOfDeath,
            Manner = m.Manner,
            NextOfKinName = m.NextOfKinName,
            NextOfKinRelationship = m.NextOfKinRelationship,
            NextOfKinPhone = m.NextOfKinPhone,
            NextOfKinId = m.NextOfKinId,
            Notes = m.Notes,
            Status = MortuaryStatus.Received,
            ReceivedById = ctx.Value.userId
        };
        _db.MortuaryEntries.Add(entry);
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Body received · {entry.MortuaryNumber}";
        return RedirectToAction(nameof(Detail), new { id = entry.Id });
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var e = await _db.MortuaryEntries
            .Include(x => x.Patient).Include(x => x.ReceivedBy).Include(x => x.ReleasedBy)
            .FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (e is null) return NotFound();
        return View(e);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Embalm(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var e = await _db.MortuaryEntries.FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (e is null) return NotFound();
        e.Embalmed = true;
        e.EmbalmedAt = DateTime.UtcNow;
        if (e.Status == MortuaryStatus.Received) e.Status = MortuaryStatus.Embalmed;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Marked as embalmed.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Release(MortuaryReleaseViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var e = await _db.MortuaryEntries.FirstOrDefaultAsync(x => x.Id == m.Id && x.FacilityId == ctx.Value.facilityId);
        if (e is null) return NotFound();
        if (e.Status == MortuaryStatus.Released) { TempData["Error"] = "Already released."; return RedirectToAction(nameof(Detail), new { id = m.Id }); }

        e.Status = MortuaryStatus.Released;
        e.ReleasedAt = DateTime.UtcNow;
        e.ReleasedTo = m.ReleasedTo;
        e.ReleasedToId = m.ReleasedToId;
        e.ReleaseAuthorityRef = m.ReleaseAuthorityRef;
        if (!string.IsNullOrEmpty(m.Notes)) e.Notes = (e.Notes is null ? "" : e.Notes + "\n") + "Release: " + m.Notes;
        e.ReleasedById = ctx.Value.userId;
        await _db.SaveChangesAsync();
        TempData["Success"] = $"Body released to {m.ReleasedTo}.";
        return RedirectToAction(nameof(Detail), new { id = m.Id });
    }

    [HttpPost, HasPermission(Permissions.AiMortuaryDraft)]
    public async Task<IActionResult> DraftDocs(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        var e = await _db.MortuaryEntries.AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (e is null) return NotFound();

        var ageSex = e.AgeYears.HasValue ? $"{e.AgeYears}y {e.Sex}" : (e.Sex ?? "Unknown");
        var input = new MortuaryDraftInput(
            ctx.Value.facilityId, e.Id, ageSex,
            e.CauseOfDeath, e.Manner?.ToString(),
            e.PlaceOfDeath, e.DateOfDeathUtc,
            e.ReleasedTo, e.ReleaseAuthorityRef);
        var outcome = await _ai.DraftMortuaryDocsAsync(input, ctx.Value.userId);
        return Json(new { ok = outcome.Ok, text = outcome.Text, error = outcome.Error });
    }
}
