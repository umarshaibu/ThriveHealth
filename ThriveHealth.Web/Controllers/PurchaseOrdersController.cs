using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Inventory;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.PurchaseOrderManage)]
public class PurchaseOrdersController : Controller
{
    public const string ProcurementStaff = Roles.ProcurementOfficer + "," + Roles.StoreOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector + "," +
        Roles.ChiefFinancialOfficer + "," + Roles.Accountant + "," + Roles.Pharmacist;

    public const string CanApprove = Roles.ProcurementOfficer + "," + Roles.SystemAdministrator + "," +
        Roles.MedicalDirector + "," + Roles.ChiefFinancialOfficer;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IPurchaseOrderService _po;

    public PurchaseOrdersController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IPurchaseOrderService po)
    {
        _db = db;
        _userManager = userManager;
        _po = po;
    }

    private async Task<(int facilityId, string userId)?> Ctx()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u?.FacilityId is null) return null;
        return (u.FacilityId.Value, u.Id);
    }

    [HttpGet]
    public async Task<IActionResult> Index(PurchaseOrderStatus? status)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var query = _db.PurchaseOrders.AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Store)
            .Include(p => p.Items)
            .Where(p => p.FacilityId == ctx.Value.facilityId);
        if (status.HasValue) query = query.Where(p => p.Status == status.Value);

        var rows = await query
            .OrderByDescending(p => p.CreatedAt).Take(300).ToListAsync();
        var view = rows.Select(p => new PurchaseOrderListRow
        {
            PurchaseOrder = p,
            LineCount = p.Items.Count,
            OutstandingQuantity = p.Items.Sum(i => Math.Max(0, i.QuantityOrdered - i.QuantityReceived))
        }).ToList();

        var allPo = await _db.PurchaseOrders.AsNoTracking()
            .Include(p => p.Items)
            .Where(p => p.FacilityId == ctx.Value.facilityId)
            .ToListAsync();

        return View(new PurchaseOrderWorklistViewModel
        {
            Rows = view,
            FilterStatus = status,
            DraftCount = allPo.Count(p => p.Status == PurchaseOrderStatus.Draft),
            IssuedCount = allPo.Count(p => p.Status == PurchaseOrderStatus.Issued),
            PartiallyReceivedCount = allPo.Count(p => p.Status == PurchaseOrderStatus.PartiallyReceived),
            OutstandingValue = allPo.Where(p => p.Status == PurchaseOrderStatus.Issued || p.Status == PurchaseOrderStatus.PartiallyReceived)
                .Sum(p => p.Items.Sum(i => Math.Max(0, i.QuantityOrdered - i.QuantityReceived) * i.UnitPrice))
        });
    }

    [HttpGet]
    public async Task<IActionResult> Create()
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        await PopulateLists(ctx.Value.facilityId);
        return View(new PurchaseOrderEditViewModel { Lines = new List<PoLineEditViewModel> { new() } });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PurchaseOrderEditViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        if (m.SupplierId is null) ModelState.AddModelError(nameof(m.SupplierId), "Choose a supplier.");
        if (m.StoreId is null) ModelState.AddModelError(nameof(m.StoreId), "Choose a delivery store.");
        var validLines = (m.Lines ?? new()).Where(l => l.Quantity > 0 && !string.IsNullOrWhiteSpace(l.Description)).ToList();
        if (validLines.Count == 0) ModelState.AddModelError(string.Empty, "Add at least one line.");

        if (!ModelState.IsValid)
        {
            await PopulateLists(ctx.Value.facilityId);
            return View(m);
        }

        var lines = validLines.Select(l => new PoLineRequest(l.DrugId, l.InventoryItemId, l.Description, l.UnitOfIssue, l.Quantity, l.UnitPrice));
        var poId = await _po.CreateAsync(ctx.Value.facilityId, m.SupplierId!.Value, m.StoreId!.Value, m.ExpectedDate, m.Notes, lines, ctx.Value.userId);

        TempData["Success"] = "Purchase order saved as draft.";
        return RedirectToAction(nameof(Details), new { id = poId });
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var po = await _db.PurchaseOrders.AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Store)
            .Include(p => p.Items).ThenInclude(i => i.Drug)
            .Include(p => p.Items).ThenInclude(i => i.InventoryItem)
            .Include(p => p.Grns).ThenInclude(g => g.Items)
            .Include(p => p.CreatedBy)
            .Include(p => p.ApprovedBy)
            .FirstOrDefaultAsync(p => p.Id == id && p.FacilityId == ctx.Value.facilityId);
        if (po is null) return NotFound();
        return View(po);
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.PurchaseOrderManage)]
    public async Task<IActionResult> Approve(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var ok = await _po.ApproveAsync(id, ctx.Value.userId);
        TempData[ok ? "Success" : "Error"] = ok ? "PO approved. Issue to send to supplier." : "Cannot approve.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.PurchaseOrderManage)]
    public async Task<IActionResult> Issue(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var ok = await _po.IssueAsync(id, ctx.Value.userId);
        TempData[ok ? "Success" : "Error"] = ok ? "PO issued to supplier." : "PO must be approved first.";
        return RedirectToAction(nameof(Details), new { id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var ok = await _po.CancelAsync(id, ctx.Value.userId);
        TempData[ok ? "Success" : "Error"] = ok ? "PO cancelled." : "Cannot cancel a closed PO.";
        return RedirectToAction(nameof(Details), new { id });
    }

    private async Task PopulateLists(int facilityId)
    {
        ViewBag.Suppliers = await _db.Suppliers.AsNoTracking().Where(s => s.IsActive).OrderBy(s => s.Name).ToListAsync();
        ViewBag.Stores = await _db.PharmacyStores.AsNoTracking().Where(s => s.FacilityId == facilityId && s.IsActive).OrderBy(s => s.Name).ToListAsync();
    }
}
