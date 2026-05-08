using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Inventory;
using ThriveHealth.Web.Models.ViewModels;

namespace ThriveHealth.Web.Controllers;

[Authorize]
public class InventoryItemsController : Controller
{
    public const string CanManage = Roles.StoreOfficer + "," + Roles.ProcurementOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector + "," + Roles.Pharmacist;

    private readonly ApplicationDbContext _db;
    public InventoryItemsController(ApplicationDbContext db) => _db = db;

    [HttpGet, HasPermission(Permissions.PurchaseOrderManage)]
    public async Task<IActionResult> Index(string? q, InventoryCategory? category)
    {
        var query = _db.InventoryItems.AsNoTracking().AsQueryable();
        if (category.HasValue) query = query.Where(i => i.Category == category.Value);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(i => EF.Functions.ILike(i.Name, like) || EF.Functions.ILike(i.Code, like));
        }
        var rows = await query.OrderBy(i => i.Name).Take(500).ToListAsync();
        ViewBag.Search = q;
        ViewBag.Category = category;
        return View(rows);
    }

    [HttpGet, HasPermission(Permissions.PurchaseOrderManage)]
    public IActionResult Create() => View(new InventoryItemEditViewModel());

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.PurchaseOrderManage)]
    public async Task<IActionResult> Create(InventoryItemEditViewModel m)
    {
        if (!ModelState.IsValid) return View(m);
        _db.InventoryItems.Add(new InventoryItem
        {
            Name = m.Name, Code = m.Code.ToUpperInvariant(), Barcode = m.Barcode,
            Category = m.Category, UnitOfIssue = m.UnitOfIssue, UnitPrice = m.UnitPrice,
            ReorderLevel = m.ReorderLevel, Manufacturer = m.Manufacturer, NafdacNumber = m.NafdacNumber,
            IsExpiringTracked = m.IsExpiringTracked, IsActive = m.IsActive
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Item added to inventory master.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, HasPermission(Permissions.PurchaseOrderManage)]
    public async Task<IActionResult> Edit(int id)
    {
        var i = await _db.InventoryItems.FindAsync(id);
        if (i is null) return NotFound();
        return View(new InventoryItemEditViewModel
        {
            Id = i.Id, Name = i.Name, Code = i.Code, Barcode = i.Barcode,
            Category = i.Category, UnitOfIssue = i.UnitOfIssue, UnitPrice = i.UnitPrice,
            ReorderLevel = i.ReorderLevel, Manufacturer = i.Manufacturer, NafdacNumber = i.NafdacNumber,
            IsExpiringTracked = i.IsExpiringTracked, IsActive = i.IsActive
        });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.PurchaseOrderManage)]
    public async Task<IActionResult> Edit(InventoryItemEditViewModel m)
    {
        var i = await _db.InventoryItems.FindAsync(m.Id);
        if (i is null) return NotFound();
        if (!ModelState.IsValid) return View(m);

        i.Name = m.Name; i.Code = m.Code.ToUpperInvariant(); i.Barcode = m.Barcode;
        i.Category = m.Category; i.UnitOfIssue = m.UnitOfIssue; i.UnitPrice = m.UnitPrice;
        i.ReorderLevel = m.ReorderLevel; i.Manufacturer = m.Manufacturer; i.NafdacNumber = m.NafdacNumber;
        i.IsExpiringTracked = m.IsExpiringTracked; i.IsActive = m.IsActive;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Item updated.";
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Search(string? q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2) return Json(Array.Empty<object>());
        var like = $"%{q.Trim()}%";
        var rows = await _db.InventoryItems.AsNoTracking()
            .Where(i => i.IsActive && (
                EF.Functions.ILike(i.Name, like) ||
                EF.Functions.ILike(i.Code, like)))
            .OrderBy(i => i.Name).Take(15)
            .Select(i => new { id = i.Id, name = i.Name, code = i.Code, unit = i.UnitOfIssue, price = i.UnitPrice, category = i.Category.ToString() })
            .ToListAsync();
        return Json(rows);
    }
}
