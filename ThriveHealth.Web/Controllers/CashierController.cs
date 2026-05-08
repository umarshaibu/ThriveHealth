using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Audit;
using ThriveHealth.Web.Models.Billing;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;
using ThriveHealth.Web.Services.Ai;

namespace ThriveHealth.Web.Controllers;

[Authorize]
public class CashierController : Controller
{
    public const string CashierStaff = Roles.Cashier + "," + Roles.Accountant + "," + Roles.ChiefFinancialOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    public const string CanBuild = Roles.Cashier + "," + Roles.Accountant + "," + Roles.ChiefFinancialOfficer + "," +
        Roles.Receptionist + "," + Roles.RecordsOfficer + "," +
        Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IBillingService _billing;
    private readonly ICashierShiftService _shifts;
    private readonly IAuditService _audit;
    private readonly IClinicalAiService _ai;

    public CashierController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        IBillingService billing, ICashierShiftService shifts, IAuditService audit, IClinicalAiService ai)
    {
        _db = db; _userManager = userManager; _billing = billing; _shifts = shifts; _audit = audit; _ai = ai;
    }

    private async Task<(int facilityId, string userId)?> Ctx()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u?.FacilityId is null) return null;
        return (u.FacilityId.Value, u.Id);
    }

    [HttpGet, HasPermission(Permissions.BillsRead)]
    public async Task<IActionResult> Bills(BillStatus? status)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var query = _db.Bills.AsNoTracking()
            .Include(b => b.Patient)
            .Include(b => b.Items)
            .Where(b => b.FacilityId == ctx.Value.facilityId);
        if (status.HasValue) query = query.Where(b => b.Status == status.Value);

        var rows = await query
            .OrderByDescending(b => b.CreatedAt).Take(300).ToListAsync();
        var view = rows.Select(b => new BillsListRow { Bill = b, Patient = b.Patient! }).ToList();

        var allOpen = await _db.Bills.AsNoTracking()
            .Where(b => b.FacilityId == ctx.Value.facilityId && (b.Status == BillStatus.Open || b.Status == BillStatus.PartiallyPaid))
            .Select(b => new { b.NetAmount, b.PaidAmount }).ToListAsync();

        var todayUtc = DateTime.UtcNow.Date;
        var collectedToday = await _db.Payments.AsNoTracking()
            .Where(p => p.Status == PaymentStatus.Recorded && p.ReceivedAt >= todayUtc
                && p.Bill!.FacilityId == ctx.Value.facilityId)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        return View(new BillsListViewModel
        {
            Rows = view,
            FilterStatus = status,
            OpenCount = allOpen.Count,
            OpenBalance = allOpen.Sum(b => b.NetAmount - b.PaidAmount),
            TodayCollected = collectedToday
        });
    }

    [HttpGet, HasPermission(Permissions.BillsBuild)]
    public async Task<IActionResult> BuildFromEncounter(int encounterId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        try
        {
            var billId = await _billing.BuildBillFromEncounterAsync(ctx.Value.facilityId, encounterId, ctx.Value.userId);
            TempData["Success"] = "Bill drafted from encounter charges.";
            return RedirectToAction(nameof(Bill), new { id = billId });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction("Summary", "Encounters", new { id = encounterId });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Bill(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var b = await _db.Bills
            .Include(x => x.Patient)
            .Include(x => x.Items)
            .Include(x => x.Payments)
            .Include(x => x.Encounter)
            .Include(x => x.CreatedBy)
            .FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (b is null) return NotFound();
        return View(new BillDetailViewModel { Bill = b, Patient = b.Patient! });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.BillsDiscount)]
    public async Task<IActionResult> Discount(BillDiscountViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        try
        {
            await _billing.ApplyDiscountAsync(m.BillId, m.DiscountAmount, m.Reason);
            await _audit.LogAsync("bill.discount", AuditCategory.BusinessAction, AuditOutcome.Success,
                entityType: "Bill", entityKey: m.BillId.ToString(),
                summary: $"Discount ₦{m.DiscountAmount:N2} applied · reason: {m.Reason}",
                facilityId: ctx.Value.facilityId);
            TempData["Success"] = "Discount applied.";
        }
        catch (InvalidOperationException ex) { TempData["Error"] = ex.Message; }
        return RedirectToAction(nameof(Bill), new { id = m.BillId });
    }

    [HttpGet, HasPermission(Permissions.BillsPaymentRecord)]
    public async Task<IActionResult> TakePayment(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var bill = await _db.Bills.AsNoTracking()
            .Include(x => x.Patient)
            .FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (bill is null) return NotFound();

        var openShift = await _shifts.GetCurrentShiftIdAsync(ctx.Value.userId);
        if (openShift is null)
        {
            TempData["Error"] = "Open a cashier shift first.";
            return RedirectToAction(nameof(Shift));
        }

        ViewBag.Bill = bill;
        return View(new TakePaymentViewModel
        {
            BillId = id,
            Cash = bill.NetAmount - bill.PaidAmount
        });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.BillsPaymentRecord)]
    public async Task<IActionResult> TakePayment(TakePaymentViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var shiftId = await _shifts.GetCurrentShiftIdAsync(ctx.Value.userId);
        if (shiftId is null)
        {
            TempData["Error"] = "Open a cashier shift first.";
            return RedirectToAction(nameof(Shift));
        }

        var inputs = new List<PaymentInput>();
        if (m.Cash > 0) inputs.Add(new(PaymentMethod.Cash, m.Cash, null, m.Notes));
        if (m.Pos > 0) inputs.Add(new(PaymentMethod.Pos, m.Pos, m.Reference, m.Notes));
        if (m.BankTransfer > 0) inputs.Add(new(PaymentMethod.BankTransfer, m.BankTransfer, m.Reference, m.Notes));
        if (m.MobileMoney > 0) inputs.Add(new(PaymentMethod.MobileMoney, m.MobileMoney, m.Reference, m.Notes));
        if (m.Cheque > 0) inputs.Add(new(PaymentMethod.Cheque, m.Cheque, m.Reference, m.Notes));

        if (inputs.Count == 0)
        {
            TempData["Error"] = "Enter at least one payment amount.";
            return RedirectToAction(nameof(TakePayment), new { id = m.BillId });
        }

        var n = await _billing.RecordPaymentsAsync(m.BillId, inputs, shiftId, ctx.Value.userId);
        var total = inputs.Sum(i => i.Amount);
        await _audit.LogAsync("bill.payment.recorded", AuditCategory.BusinessAction, AuditOutcome.Success,
            entityType: "Bill", entityKey: m.BillId.ToString(),
            summary: $"{n} payment(s) totalling ₦{total:N2} recorded against bill",
            facilityId: ctx.Value.facilityId);
        TempData["Success"] = $"Recorded {n} payment(s).";
        return RedirectToAction(nameof(Bill), new { id = m.BillId });
    }

    [HttpGet, HasPermission(Permissions.CashierShiftManage)]
    public async Task<IActionResult> Shift()
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var openShiftId = await _shifts.GetCurrentShiftIdAsync(ctx.Value.userId);
        if (openShiftId is null)
        {
            ViewBag.Open = false;
            return View("Shift", new OpenShiftViewModel { OpeningFloat = 5000m });
        }

        var summary = await _shifts.SummariseAsync(openShiftId.Value);
        var shift = await _db.CashierShifts
            .Include(s => s.Payments).ThenInclude(p => p.Bill)
            .FirstAsync(s => s.Id == openShiftId.Value);
        ViewBag.Open = true;
        ViewBag.Shift = shift;
        ViewBag.Summary = summary;
        return View("Shift", new CloseShiftViewModel
        {
            ShiftId = shift.Id,
            ExpectedCash = summary.Cash + shift.OpeningFloat,
            CountedCash = summary.Cash + shift.OpeningFloat
        });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.CashierShiftManage)]
    public async Task<IActionResult> OpenShift(OpenShiftViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        await _shifts.OpenAsync(ctx.Value.facilityId, ctx.Value.userId, m.OpeningFloat);
        TempData["Success"] = "Shift opened.";
        return RedirectToAction(nameof(Shift));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.CashierShiftManage)]
    public async Task<IActionResult> CloseShift(CloseShiftViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var ok = await _shifts.CloseAsync(m.ShiftId, m.CountedCash, m.Notes);
        TempData[ok ? "Success" : "Error"] = ok ? "Shift closed." : "Cannot close shift.";
        return RedirectToAction(nameof(ShiftReport), new { id = m.ShiftId });
    }

    [HttpGet, HasPermission(Permissions.CashierShiftManage)]
    public async Task<IActionResult> ShiftReport(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var shift = await _db.CashierShifts
            .Include(s => s.Cashier)
            .Include(s => s.Payments).ThenInclude(p => p.Bill).ThenInclude(b => b!.Patient)
            .FirstOrDefaultAsync(s => s.Id == id && s.FacilityId == ctx.Value.facilityId);
        if (shift is null) return NotFound();
        var summary = await _shifts.SummariseAsync(id);
        ViewBag.Summary = summary;
        return View(shift);
    }

    [HttpPost, HasPermission(Permissions.AiBillAnomaly)]
    public async Task<IActionResult> CheckBillAnomaly(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        var b = await _db.Bills.AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Items)
            .Include(x => x.Encounter)!.ThenInclude(e => e!.Diagnoses)
            .FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (b is null) return NotFound();

        var contextLines = new List<string>();
        if (b.Encounter != null)
        {
            contextLines.Add($"Encounter type: {b.Encounter.Type}");
            if (!string.IsNullOrEmpty(b.Encounter.ChiefComplaint)) contextLines.Add("Chief complaint: " + b.Encounter.ChiefComplaint);
            var dx = b.Encounter.Diagnoses?.FirstOrDefault(d => d.IsPrimary)?.Description ?? b.Encounter.Diagnoses?.FirstOrDefault()?.Description;
            if (!string.IsNullOrEmpty(dx)) contextLines.Add("Diagnosis: " + dx);
            contextLines.Add($"Encounter started: {b.Encounter.StartedAt:yyyy-MM-dd HH:mm} signed: {b.Encounter.SignedAt?.ToString("yyyy-MM-dd HH:mm") ?? "—"}");
        }
        var adm = await _db.Admissions.AsNoTracking()
            .Where(a => a.PatientId == b.PatientId && a.FacilityId == ctx.Value.facilityId
                && a.AdmittedAt <= b.CreatedAt && (a.DischargedAt == null || a.DischargedAt >= b.CreatedAt.AddDays(-1)))
            .OrderByDescending(a => a.AdmittedAt).FirstOrDefaultAsync();
        if (adm != null)
        {
            var los = (int)Math.Max(1, ((adm.DischargedAt ?? DateTime.UtcNow) - adm.AdmittedAt).TotalDays);
            contextLines.Add($"Admission length-of-stay: {los} day(s)");
        }
        var contextText = contextLines.Count == 0 ? "(no encounter/admission context)" : string.Join("\n", contextLines);

        var charges = b.Items.Select(i => $"{i.Description} qty {i.Quantity} @ ₦{i.UnitPrice:N2}");
        var input = new BillAnomalyInput(ctx.Value.facilityId, b.Id, contextText, charges);
        var outcome = await _ai.DetectBillAnomalyAsync(input, ctx.Value.userId);
        return Json(new { ok = outcome.Ok, text = outcome.Text, error = outcome.Error });
    }
}
