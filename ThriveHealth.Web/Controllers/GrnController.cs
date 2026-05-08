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

[HasPermission(Permissions.GrnReceive)]
public class GrnController : Controller
{
    public const string ReceiveStaff = Roles.StoreOfficer + "," + Roles.ProcurementOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector + "," + Roles.Pharmacist;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IGrnService _grn;

    public GrnController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IGrnService grn)
    {
        _db = db;
        _userManager = userManager;
        _grn = grn;
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
        var rows = await _db.Grns.AsNoTracking()
            .Include(g => g.PurchaseOrder).ThenInclude(p => p!.Supplier)
            .Include(g => g.Store)
            .Include(g => g.ReceivedBy)
            .Include(g => g.Items)
            .Where(g => g.FacilityId == ctx.Value.facilityId)
            .OrderByDescending(g => g.ReceivedAt).Take(200).ToListAsync();
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Receive(int purchaseOrderId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var po = await _db.PurchaseOrders.AsNoTracking()
            .Include(p => p.Supplier)
            .Include(p => p.Store)
            .Include(p => p.Items).ThenInclude(i => i.Drug)
            .Include(p => p.Items).ThenInclude(i => i.InventoryItem)
            .FirstOrDefaultAsync(p => p.Id == purchaseOrderId && p.FacilityId == ctx.Value.facilityId);
        if (po is null) return NotFound();
        if (po.Status != PurchaseOrderStatus.Issued && po.Status != PurchaseOrderStatus.PartiallyReceived)
        {
            TempData["Error"] = $"Cannot receive: PO must be Issued (currently {po.Status}).";
            return RedirectToAction("Details", "PurchaseOrders", new { id = purchaseOrderId });
        }

        ViewBag.PurchaseOrder = po;
        ViewBag.Stores = await _db.PharmacyStores.AsNoTracking()
            .Where(s => s.FacilityId == ctx.Value.facilityId && s.IsActive)
            .OrderBy(s => s.Name).ToListAsync();

        return View(new GrnReceiveViewModel
        {
            PurchaseOrderId = po.Id,
            StoreId = po.StoreId,
            Lines = po.Items.Select(i => new GrnLineEditViewModel
            {
                PurchaseOrderItemId = i.Id,
                Description = i.Description,
                QuantityOrdered = i.QuantityOrdered,
                QuantityAlreadyReceived = i.QuantityReceived,
                Outstanding = Math.Max(0, i.QuantityOrdered - i.QuantityReceived),
                Quantity = Math.Max(0, i.QuantityOrdered - i.QuantityReceived),
                UnitCost = i.UnitPrice,
                ExpiryDate = (i.Drug?.IsControlled == true || i.InventoryItem?.IsExpiringTracked == true || i.DrugId.HasValue)
                    ? DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)) : null
            }).ToList()
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Receive(GrnReceiveViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        try
        {
            var lines = (m.Lines ?? new())
                .Where(l => l.Quantity > 0)
                .Select(l => new GrnLineRequest(l.PurchaseOrderItemId, l.BatchNumber, l.ExpiryDate, l.Quantity, l.UnitCost, l.Notes));

            var result = await _grn.PostAsync(
                ctx.Value.facilityId, m.PurchaseOrderId, m.StoreId,
                m.SupplierInvoiceNumber, m.DeliveryNoteNumber, m.Notes, lines, ctx.Value.userId);

            TempData["Success"] = $"GRN {result.GrnNumber} posted · {result.LinesPosted} line(s) · ₦{result.TotalReceivedValue:N2} received.";
            return RedirectToAction(nameof(Details), new { id = result.GrnId });
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
            return RedirectToAction(nameof(Receive), new { purchaseOrderId = m.PurchaseOrderId });
        }
    }

    [HttpGet]
    public async Task<IActionResult> Details(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var grn = await _db.Grns.AsNoTracking()
            .Include(g => g.PurchaseOrder).ThenInclude(p => p!.Supplier)
            .Include(g => g.Store)
            .Include(g => g.ReceivedBy)
            .Include(g => g.PostedBy)
            .Include(g => g.Items).ThenInclude(i => i.PurchaseOrderItem).ThenInclude(p => p!.Drug)
            .Include(g => g.Items).ThenInclude(i => i.PurchaseOrderItem).ThenInclude(p => p!.InventoryItem)
            .FirstOrDefaultAsync(g => g.Id == id && g.FacilityId == ctx.Value.facilityId);
        if (grn is null) return NotFound();
        return View(grn);
    }
}
