using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Inventory;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services.Ai;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.InventoryRead)]
public class InventoryController : Controller
{
    public const string StockStaff = Roles.StoreOfficer + "," + Roles.Pharmacist + "," + Roles.PharmacyTechnician + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector + "," + Roles.LabScientist;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IClinicalAiService _ai;

    public InventoryController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IClinicalAiService ai)
    {
        _db = db;
        _userManager = userManager;
        _ai = ai;
    }

    private async Task<int?> FacilityIdAsync()
    {
        var u = await _userManager.GetUserAsync(User);
        return u?.FacilityId;
    }

    [HttpGet]
    public async Task<IActionResult> Stock(string? q, int? storeId)
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return NotFound();

        var query = _db.InventoryStocks.AsNoTracking()
            .Include(s => s.InventoryItem)
            .Include(s => s.Store)
            .Where(s => s.Store!.FacilityId == fid);
        if (storeId.HasValue) query = query.Where(s => s.StoreId == storeId.Value);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var like = $"%{q.Trim()}%";
            query = query.Where(s =>
                EF.Functions.ILike(s.InventoryItem!.Name, like) ||
                EF.Functions.ILike(s.InventoryItem.Code, like) ||
                (s.BatchNumber != null && EF.Functions.ILike(s.BatchNumber, like)));
        }

        var rows = await query.OrderBy(s => s.InventoryItem!.Name).ThenBy(s => s.ExpiryDate).Take(500).ToListAsync();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var soon = today.AddDays(90);

        var view = rows.Select(s => new InventoryStockRow
        {
            Stock = s,
            IsLow = s.InventoryItem?.ReorderLevel.HasValue == true && s.QuantityOnHand <= s.InventoryItem.ReorderLevel,
            IsExpired = s.ExpiryDate.HasValue && s.ExpiryDate < today,
            IsExpiringSoon = s.ExpiryDate.HasValue && s.ExpiryDate >= today && s.ExpiryDate <= soon
        }).ToList();

        var stores = await _db.PharmacyStores.AsNoTracking()
            .Where(s => s.FacilityId == fid).OrderBy(s => s.Name).ToListAsync();

        return View(new InventoryStockListViewModel
        {
            Rows = view,
            Search = q,
            FilterStoreId = storeId,
            Stores = stores,
            LowStockCount = view.Count(r => r.IsLow),
            ExpiringSoonCount = view.Count(r => r.IsExpiringSoon),
            ExpiredCount = view.Count(r => r.IsExpired)
        });
    }

    [HttpPost, HasPermission(Permissions.AiInventoryForecast)]
    public async Task<IActionResult> ForecastDemand()
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return Unauthorized();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var since30 = DateTime.UtcNow.AddDays(-30);
        var since90 = DateTime.UtcNow.AddDays(-90);

        // top 30 items by 90d issue volume
        var movements = await _db.InventoryStockMovements.AsNoTracking()
            .Include(m => m.InventoryItem).Include(m => m.Store)
            .Where(m => m.Store!.FacilityId == fid && m.Kind == InventoryMovementKind.Issue && m.CreatedAt >= since90)
            .ToListAsync();

        var byItem = movements.GroupBy(m => m.InventoryItemId)
            .Select(g => new { Id = g.Key, Item = g.First().InventoryItem!,
                Issued30 = g.Where(x => x.CreatedAt >= since30).Sum(x => x.Quantity),
                Issued90 = g.Sum(x => x.Quantity) })
            .OrderByDescending(x => x.Issued90)
            .Take(30)
            .ToList();

        var stocks = await _db.InventoryStocks.AsNoTracking().Include(s => s.Store)
            .Where(s => s.Store!.FacilityId == fid)
            .ToListAsync();

        var snapshots = byItem.Select(b =>
        {
            var s = stocks.Where(x => x.InventoryItemId == b.Id).ToList();
            var onHand = s.Sum(x => x.QuantityOnHand);
            DateOnly? nearestExp = s.Where(x => x.ExpiryDate.HasValue && x.ExpiryDate.Value >= today).OrderBy(x => x.ExpiryDate).Select(x => x.ExpiryDate).FirstOrDefault();
            return new ItemDemandSnapshot(b.Item.Name, onHand, b.Item.ReorderLevel, b.Issued30, b.Issued90, nearestExp);
        }).ToList();

        var u = await _userManager.GetUserAsync(User);
        var outcome = await _ai.ForecastInventoryAsync(new InventoryForecastInput(fid.Value, snapshots, 30), u?.Id);
        return Json(new { ok = outcome.Ok, text = outcome.Text, error = outcome.Error });
    }
}
