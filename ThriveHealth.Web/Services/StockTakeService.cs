using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Inventory;
using ThriveHealth.Web.Models.Pharmacy;

namespace ThriveHealth.Web.Services;

public record StockTakeCount(int StockTakeItemId, int CountedQuantity, string? Notes);

public interface IStockTakeService
{
    Task<string> GenerateTakeNumberAsync(CancellationToken ct = default);
    Task<int> StartAsync(int facilityId, int storeId, string? notes, string userId, CancellationToken ct = default);
    Task PostCountsAsync(int stockTakeId, IEnumerable<StockTakeCount> counts, CancellationToken ct = default);
    Task<bool> ApproveAndAdjustAsync(int stockTakeId, string userId, CancellationToken ct = default);
}

public class StockTakeService : IStockTakeService
{
    private readonly ApplicationDbContext _db;
    public StockTakeService(ApplicationDbContext db) => _db = db;

    public async Task<string> GenerateTakeNumberAsync(CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"STK-{year}-";
        var existing = await _db.StockTakes.AsNoTracking()
            .Where(s => s.TakeNumber.StartsWith(prefix))
            .Select(s => s.TakeNumber).ToListAsync(ct);
        int next = 1;
        foreach (var s in existing)
        {
            var tail = s.Substring(prefix.Length);
            if (int.TryParse(tail, out var n) && n >= next) next = n + 1;
        }
        return $"{prefix}{next:D4}";
    }

    public async Task<int> StartAsync(int facilityId, int storeId, string? notes, string userId, CancellationToken ct = default)
    {
        var take = new StockTake
        {
            FacilityId = facilityId,
            StoreId = storeId,
            TakeNumber = await GenerateTakeNumberAsync(ct),
            Notes = notes,
            CreatedById = userId,
            Status = StockTakeStatus.Open,
            TakeDate = DateOnly.FromDateTime(DateTime.UtcNow)
        };

        var drugStocks = await _db.DrugStocks.AsNoTracking()
            .Include(s => s.Drug)
            .Where(s => s.StoreId == storeId).ToListAsync(ct);
        foreach (var s in drugStocks)
        {
            take.Items.Add(new StockTakeItem
            {
                DrugId = s.DrugId,
                Description = $"{s.Drug?.GenericName} {s.Drug?.Strength}",
                BatchNumber = s.BatchNumber,
                ExpiryDate = s.ExpiryDate,
                ExpectedQuantity = s.QuantityOnHand,
                CountedQuantity = s.QuantityOnHand,
                UnitCost = s.UnitCost
            });
        }

        var invStocks = await _db.InventoryStocks.AsNoTracking()
            .Include(s => s.InventoryItem)
            .Where(s => s.StoreId == storeId).ToListAsync(ct);
        foreach (var s in invStocks)
        {
            take.Items.Add(new StockTakeItem
            {
                InventoryItemId = s.InventoryItemId,
                Description = s.InventoryItem?.Name ?? "?",
                BatchNumber = s.BatchNumber,
                ExpiryDate = s.ExpiryDate,
                ExpectedQuantity = s.QuantityOnHand,
                CountedQuantity = s.QuantityOnHand,
                UnitCost = s.UnitCost
            });
        }

        _db.StockTakes.Add(take);
        await _db.SaveChangesAsync(ct);
        return take.Id;
    }

    public async Task PostCountsAsync(int stockTakeId, IEnumerable<StockTakeCount> counts, CancellationToken ct = default)
    {
        var take = await _db.StockTakes.Include(t => t.Items).FirstAsync(t => t.Id == stockTakeId, ct);
        if (take.Status != StockTakeStatus.Open) throw new InvalidOperationException("Stock-take is not open.");

        var byId = take.Items.ToDictionary(i => i.Id);
        foreach (var c in counts)
        {
            if (!byId.TryGetValue(c.StockTakeItemId, out var item)) continue;
            item.CountedQuantity = Math.Max(0, c.CountedQuantity);
            item.Notes = c.Notes;
            item.VarianceValue = (item.CountedQuantity - item.ExpectedQuantity) * (item.UnitCost ?? 0m);
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> ApproveAndAdjustAsync(int stockTakeId, string userId, CancellationToken ct = default)
    {
        var take = await _db.StockTakes.Include(t => t.Items).FirstOrDefaultAsync(t => t.Id == stockTakeId, ct);
        if (take is null) return false;
        if (take.Status != StockTakeStatus.Open) return false;

        foreach (var item in take.Items.Where(i => i.CountedQuantity != i.ExpectedQuantity))
        {
            var delta = item.CountedQuantity - item.ExpectedQuantity;

            if (item.DrugId.HasValue)
            {
                var stock = await _db.DrugStocks.FirstOrDefaultAsync(
                    s => s.DrugId == item.DrugId.Value && s.StoreId == take.StoreId && s.BatchNumber == item.BatchNumber, ct);
                if (stock is null) continue;
                stock.QuantityOnHand = item.CountedQuantity;
                stock.UpdatedAt = DateTime.UtcNow;
                _db.StockMovements.Add(new StockMovement
                {
                    DrugId = item.DrugId.Value,
                    StoreId = take.StoreId,
                    BatchNumber = item.BatchNumber,
                    ExpiryDate = item.ExpiryDate,
                    Kind = StockMovementKind.Adjustment,
                    Quantity = delta,
                    RunningBalance = stock.QuantityOnHand,
                    UnitCost = item.UnitCost,
                    Reference = take.TakeNumber,
                    Notes = $"Stock-take variance ({(delta > 0 ? "+" : "")}{delta})",
                    PerformedById = userId
                });
            }
            else if (item.InventoryItemId.HasValue)
            {
                var stock = await _db.InventoryStocks.FirstOrDefaultAsync(
                    s => s.InventoryItemId == item.InventoryItemId.Value && s.StoreId == take.StoreId && s.BatchNumber == item.BatchNumber, ct);
                if (stock is null) continue;
                stock.QuantityOnHand = item.CountedQuantity;
                stock.UpdatedAt = DateTime.UtcNow;
                _db.InventoryStockMovements.Add(new InventoryStockMovement
                {
                    InventoryItemId = item.InventoryItemId.Value,
                    StoreId = take.StoreId,
                    BatchNumber = item.BatchNumber,
                    ExpiryDate = item.ExpiryDate,
                    Kind = InventoryMovementKind.Adjustment,
                    Quantity = delta,
                    RunningBalance = stock.QuantityOnHand,
                    UnitCost = item.UnitCost,
                    Reference = take.TakeNumber,
                    Notes = $"Stock-take variance ({(delta > 0 ? "+" : "")}{delta})",
                    PerformedById = userId
                });
            }
        }

        take.Status = StockTakeStatus.Posted;
        take.PostedAt = DateTime.UtcNow;
        take.PostedById = userId;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
