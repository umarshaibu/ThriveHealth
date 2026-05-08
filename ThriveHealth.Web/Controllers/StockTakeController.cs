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

[HasPermission(Permissions.StockTakeManage)]
public class StockTakeController : Controller
{
    public const string StockStaff = Roles.StoreOfficer + "," + Roles.Pharmacist + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IStockTakeService _takes;

    public StockTakeController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IStockTakeService takes)
    {
        _db = db;
        _userManager = userManager;
        _takes = takes;
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

        var takes = await _db.StockTakes.AsNoTracking()
            .Include(t => t.Store)
            .Include(t => t.Items)
            .Include(t => t.CreatedBy)
            .Where(t => t.FacilityId == ctx.Value.facilityId)
            .OrderByDescending(t => t.CreatedAt).Take(100).ToListAsync();

        var rows = takes.Select(t => new StockTakeListRow
        {
            StockTake = t,
            LineCount = t.Items.Count,
            VarianceLines = t.Items.Count(i => i.CountedQuantity != i.ExpectedQuantity),
            VarianceValue = t.Items.Sum(i => (i.CountedQuantity - i.ExpectedQuantity) * (i.UnitCost ?? 0m))
        }).ToList();
        return View(rows);
    }

    [HttpGet]
    public async Task<IActionResult> Start()
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        ViewBag.Stores = await _db.PharmacyStores.AsNoTracking()
            .Where(s => s.FacilityId == ctx.Value.facilityId && s.IsActive)
            .OrderBy(s => s.Name).ToListAsync();
        return View(new StockTakeStartViewModel());
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Start(StockTakeStartViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        if (m.StoreId is null) ModelState.AddModelError(nameof(m.StoreId), "Choose a store.");
        if (!ModelState.IsValid)
        {
            ViewBag.Stores = await _db.PharmacyStores.AsNoTracking()
                .Where(s => s.FacilityId == ctx.Value.facilityId).OrderBy(s => s.Name).ToListAsync();
            return View(m);
        }
        var id = await _takes.StartAsync(ctx.Value.facilityId, m.StoreId!.Value, m.Notes, ctx.Value.userId);
        TempData["Success"] = "Stock-take started · count sheet generated from current stock.";
        return RedirectToAction(nameof(Count), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Count(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var t = await _db.StockTakes.AsNoTracking()
            .Include(s => s.Store)
            .Include(s => s.Items).ThenInclude(i => i.Drug)
            .Include(s => s.Items).ThenInclude(i => i.InventoryItem)
            .FirstOrDefaultAsync(s => s.Id == id && s.FacilityId == ctx.Value.facilityId);
        if (t is null) return NotFound();

        return View(new StockTakeViewModel
        {
            StockTake = t,
            Items = t.Items.OrderBy(i => i.Description).ToList(),
            VarianceLines = t.Items.Count(i => i.CountedQuantity != i.ExpectedQuantity),
            VarianceValue = t.Items.Sum(i => (i.CountedQuantity - i.ExpectedQuantity) * (i.UnitCost ?? 0m))
        });
    }

    public class CountSubmitDto
    {
        public int StockTakeId { get; set; }
        public List<StockTakeCountDto> Counts { get; set; } = new();
    }
    public class StockTakeCountDto
    {
        public int StockTakeItemId { get; set; }
        public int CountedQuantity { get; set; }
        public string? Notes { get; set; }
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveCounts(CountSubmitDto dto)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var ok = await _db.StockTakes.AnyAsync(t => t.Id == dto.StockTakeId && t.FacilityId == ctx.Value.facilityId);
        if (!ok) return NotFound();
        await _takes.PostCountsAsync(dto.StockTakeId, dto.Counts.Select(c => new StockTakeCount(c.StockTakeItemId, c.CountedQuantity, c.Notes)));
        TempData["Success"] = "Counts saved.";
        return RedirectToAction(nameof(Count), new { id = dto.StockTakeId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var t = await _db.StockTakes.FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (t is null) return NotFound();
        var ok = await _takes.ApproveAndAdjustAsync(id, ctx.Value.userId);
        TempData[ok ? "Success" : "Error"] = ok ? "Stock-take posted · stock balances + movements updated." : "Cannot post.";
        return RedirectToAction(nameof(Count), new { id });
    }
}
