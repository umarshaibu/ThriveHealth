using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Insurance;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;
using ThriveHealth.Web.Services.Ai;

namespace ThriveHealth.Web.Controllers;

[Authorize]
public class ClaimsController : Controller
{
    public const string ClaimStaff = Roles.ClaimsOfficer + "," + Roles.Accountant + "," + Roles.ChiefFinancialOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    public const string CanBuild = Roles.ClaimsOfficer + "," + Roles.Accountant + "," + Roles.ChiefFinancialOfficer + "," +
        Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IClaimsService _claims;
    private readonly IClinicalAiService _ai;

    public ClaimsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IClaimsService claims, IClinicalAiService ai)
    {
        _db = db;
        _userManager = userManager;
        _claims = claims;
        _ai = ai;
    }

    private async Task<(int facilityId, string userId)?> Ctx()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u?.FacilityId is null) return null;
        return (u.FacilityId.Value, u.Id);
    }

    [HttpGet, HasPermission(Permissions.ClaimsManage)]
    public async Task<IActionResult> Index(int? payerId, ClaimStatus? status)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var query = _db.Claims.AsNoTracking()
            .Include(c => c.Payer)
            .Include(c => c.PayerPlan)
            .Include(c => c.Patient)
            .Where(c => c.FacilityId == ctx.Value.facilityId);
        if (payerId.HasValue) query = query.Where(c => c.PayerId == payerId.Value);
        if (status.HasValue) query = query.Where(c => c.Status == status.Value);

        var rows = await query
            .OrderByDescending(c => c.CreatedAt).Take(300).ToListAsync();
        var view = rows.Select(c => new ClaimsWorklistRow { Claim = c, Patient = c.Patient! }).ToList();

        var allClaims = await _db.Claims.AsNoTracking()
            .Where(c => c.FacilityId == ctx.Value.facilityId)
            .Select(c => new { c.Status, c.ClaimableAmount, c.PaidAmount, c.SubmittedAt, c.RespondedAt })
            .ToListAsync();

        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var paidThisMonth = allClaims
            .Where(c => c.RespondedAt.HasValue && c.RespondedAt >= monthStart)
            .Sum(c => c.PaidAmount);

        var payers = await _db.Payers.AsNoTracking()
            .Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();

        return View(new ClaimsWorklistViewModel
        {
            Rows = view,
            FilterPayerId = payerId,
            FilterStatus = status,
            Payers = payers,
            DraftCount = allClaims.Count(c => c.Status == ClaimStatus.Draft),
            SubmittedCount = allClaims.Count(c => c.Status == ClaimStatus.Submitted || c.Status == ClaimStatus.Acknowledged),
            OutstandingCount = allClaims.Count(c => c.Status == ClaimStatus.Submitted || c.Status == ClaimStatus.Acknowledged || c.Status == ClaimStatus.PartiallyPaid),
            OutstandingAmount = allClaims.Where(c => c.Status == ClaimStatus.Submitted || c.Status == ClaimStatus.Acknowledged || c.Status == ClaimStatus.PartiallyPaid).Sum(c => c.ClaimableAmount - c.PaidAmount),
            PaidThisMonth = paidThisMonth
        });
    }

    [HttpGet, HasPermission(Permissions.ClaimsManage)]
    public async Task<IActionResult> Build(int encounterId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var enc = await _db.Encounters.AsNoTracking()
            .Include(e => e.Patient)!.ThenInclude(p => p!.Payers)!.ThenInclude(pp => pp.Payer)
            .FirstOrDefaultAsync(e => e.Id == encounterId && e.FacilityId == ctx.Value.facilityId);
        if (enc is null) return NotFound();

        var existing = await _db.Claims.AnyAsync(c => c.EncounterId == encounterId && c.Status != ClaimStatus.Closed);
        if (existing)
        {
            var existingClaim = await _db.Claims.FirstAsync(c => c.EncounterId == encounterId && c.Status != ClaimStatus.Closed);
            TempData["Error"] = $"A claim already exists for this encounter (#{existingClaim.Id}).";
            return RedirectToAction(nameof(Details), new { id = existingClaim.Id });
        }

        var primary = enc.Patient!.Payers.FirstOrDefault(p => p.IsPrimary && p.IsActive);
        var payers = await _db.Payers.AsNoTracking().Include(p => p.Plans).Where(p => p.IsActive).OrderBy(p => p.Name).ToListAsync();

        return View(new BuildClaimViewModel
        {
            EncounterId = encounterId,
            Payers = payers,
            PatientName = enc.Patient.FullName,
            EncounterSummary = $"{enc.Type} · {enc.StartedAt.ToLocalTime():dd MMM HH:mm}",
            SuggestedPayerId = primary?.PayerId,
            SuggestedPayerPlanId = primary?.PayerPlanId,
            PayerId = primary?.PayerId,
            PayerPlanId = primary?.PayerPlanId
        });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.ClaimsManage)]
    public async Task<IActionResult> Build(BuildClaimViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        if (m.PayerId is null)
        {
            TempData["Error"] = "Pick a payer.";
            return RedirectToAction(nameof(Build), new { encounterId = m.EncounterId });
        }
        var claimId = await _claims.BuildFromEncounterAsync(
            ctx.Value.facilityId, m.EncounterId, m.PayerId.Value, m.PayerPlanId, ctx.Value.userId);
        TempData["Success"] = "Claim drafted from encounter.";
        return RedirectToAction(nameof(Details), new { id = claimId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var c = await _db.Claims
            .Include(x => x.Patient)
            .Include(x => x.Payer)
            .Include(x => x.PayerPlan)
            .Include(x => x.Encounter)
            .Include(x => x.Items)
            .Include(x => x.CreatedBy)
            .FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (c is null) return NotFound();
        return View(new ClaimDetailViewModel { Claim = c, Patient = c.Patient! });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.ClaimsManage)]
    public async Task<IActionResult> Submit(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var ok = await _claims.SubmitAsync(id, ctx.Value.userId);
        if (!ok) TempData["Error"] = "Cannot submit (already submitted or not found).";
        else TempData["Success"] = "Claim submitted to payer.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, HasPermission(Permissions.AiClaimsRisk)]
    public async Task<IActionResult> AssessRisk(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        var c = await _db.Claims.AsNoTracking()
            .Include(x => x.Payer).Include(x => x.PayerPlan)
            .Include(x => x.Items)
            .Include(x => x.Encounter)!.ThenInclude(e => e!.Diagnoses)
            .FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (c is null) return NotFound();

        var diagnosis = c.Encounter?.Diagnoses?.FirstOrDefault(d => d.IsPrimary)?.Description
                     ?? c.Encounter?.Diagnoses?.FirstOrDefault()?.Description;
        var items = c.Items.Select(i => $"{i.Description} (₦{i.ClaimableAmount:N2})");
        var input = new ClaimsRiskInput(ctx.Value.facilityId, c.Id, c.Payer?.Name ?? "Unknown payer", c.PayerPlan?.Name, c.ClaimableAmount, items, diagnosis);
        var outcome = await _ai.AssessClaimRiskAsync(input, ctx.Value.userId);
        return Json(new { ok = outcome.Ok, text = outcome.Text, error = outcome.Error });
    }

    [HttpGet, HasPermission(Permissions.ClaimsManage)]
    public async Task<IActionResult> Settle(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var c = await _db.Claims.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (c is null) return NotFound();
        ViewBag.Claim = c;
        return View(new ClaimSettleViewModel
        {
            ClaimId = id,
            ApprovedAmount = c.ClaimableAmount,
            PaidAmount = c.ClaimableAmount
        });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.ClaimsManage)]
    public async Task<IActionResult> Settle(ClaimSettleViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        if (!ModelState.IsValid)
        {
            ViewBag.Claim = await _db.Claims.AsNoTracking().FirstOrDefaultAsync(x => x.Id == m.ClaimId);
            return View(m);
        }
        var ok = await _claims.SettleAsync(m.ClaimId,
            new ClaimSettlement(m.ApprovedAmount, m.PaidAmount, m.PayerReference, m.Notes), ctx.Value.userId);
        TempData[ok ? "Success" : "Error"] = ok ? "Claim settled." : "Could not settle.";
        return RedirectToAction(nameof(Details), new { id = m.ClaimId });
    }

    [HttpGet, HasPermission(Permissions.ClaimsManage)]
    public async Task<IActionResult> Deny(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var c = await _db.Claims.AsNoTracking().FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (c is null) return NotFound();
        ViewBag.Claim = c;
        return View(new ClaimDenyViewModel { ClaimId = id });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.ClaimsManage)]
    public async Task<IActionResult> Deny(ClaimDenyViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        if (!ModelState.IsValid)
        {
            ViewBag.Claim = await _db.Claims.AsNoTracking().FirstOrDefaultAsync(x => x.Id == m.ClaimId);
            return View(m);
        }
        await _claims.DenyAsync(m.ClaimId, new DenialDetails(m.Reason, m.Notes), ctx.Value.userId);
        TempData["Success"] = "Claim marked denied.";
        return RedirectToAction(nameof(Details), new { id = m.ClaimId });
    }
}
