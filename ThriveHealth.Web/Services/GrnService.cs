using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Inventory;
using ThriveHealth.Web.Models.Pharmacy;

namespace ThriveHealth.Web.Services;

public record GrnLineRequest(int PurchaseOrderItemId, string? BatchNumber, DateOnly? ExpiryDate, int Quantity, decimal UnitCost, string? Notes);
public record GrnPostResult(int GrnId, string GrnNumber, decimal TotalReceivedValue, int LinesPosted);

public interface IGrnService
{
    Task<string> GenerateGrnNumberAsync(CancellationToken ct = default);
    Task<GrnPostResult> PostAsync(int facilityId, int purchaseOrderId, int storeId, string? supplierInvoice, string? deliveryNote, string? notes, IEnumerable<GrnLineRequest> lines, string userId, CancellationToken ct = default);
}

public class GrnService : IGrnService
{
    private readonly ApplicationDbContext _db;
    public GrnService(ApplicationDbContext db) => _db = db;

    public async Task<string> GenerateGrnNumberAsync(CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"GRN-{year}-";
        var existing = await _db.Grns.AsNoTracking()
            .Where(g => g.GrnNumber.StartsWith(prefix))
            .Select(g => g.GrnNumber).ToListAsync(ct);
        int next = 1;
        foreach (var g in existing)
        {
            var tail = g.Substring(prefix.Length);
            if (int.TryParse(tail, out var n) && n >= next) next = n + 1;
        }
        return $"{prefix}{next:D5}";
    }

    public async Task<GrnPostResult> PostAsync(
        int facilityId, int purchaseOrderId, int storeId,
        string? supplierInvoice, string? deliveryNote, string? notes,
        IEnumerable<GrnLineRequest> lines, string userId, CancellationToken ct = default)
    {
        var po = await _db.PurchaseOrders
            .Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.Id == purchaseOrderId, ct)
            ?? throw new InvalidOperationException("PO not found.");
        if (po.Status != PurchaseOrderStatus.Issued && po.Status != PurchaseOrderStatus.PartiallyReceived)
            throw new InvalidOperationException("PO must be Issued or Partially Received before receiving stock.");

        var requested = lines.Where(l => l.Quantity > 0).ToList();
        if (requested.Count == 0) throw new InvalidOperationException("No items to receive.");

        var grn = new Grn
        {
            FacilityId = facilityId,
            GrnNumber = await GenerateGrnNumberAsync(ct),
            PurchaseOrderId = po.Id,
            StoreId = storeId,
            SupplierInvoiceNumber = supplierInvoice,
            DeliveryNoteNumber = deliveryNote,
            Notes = notes,
            ReceivedAt = DateTime.UtcNow,
            ReceivedById = userId,
            Status = GrnStatus.Posted,
            PostedAt = DateTime.UtcNow,
            PostedById = userId
        };
        _db.Grns.Add(grn);

        decimal grandTotal = 0m;

        foreach (var line in requested)
        {
            var poItem = po.Items.FirstOrDefault(i => i.Id == line.PurchaseOrderItemId)
                ?? throw new InvalidOperationException($"PO line {line.PurchaseOrderItemId} not on this PO.");
            var outstanding = poItem.QuantityOrdered - poItem.QuantityReceived;
            if (line.Quantity > outstanding)
                throw new InvalidOperationException($"Line {poItem.Description}: receiving {line.Quantity} exceeds outstanding {outstanding}.");

            var lineTotal = line.UnitCost * line.Quantity;
            grandTotal += lineTotal;

            var grnItem = new GrnItem
            {
                Grn = grn,
                PurchaseOrderItemId = poItem.Id,
                BatchNumber = line.BatchNumber,
                ExpiryDate = line.ExpiryDate,
                QuantityReceived = line.Quantity,
                UnitCost = line.UnitCost,
                LineTotal = lineTotal,
                Notes = line.Notes
            };
            grn.Items.Add(grnItem);

            poItem.QuantityReceived += line.Quantity;

            if (poItem.DrugId.HasValue)
            {
                var batch = string.IsNullOrEmpty(line.BatchNumber)
                    ? $"GRN-{grn.GrnNumber}-{poItem.DrugId.Value}"
                    : line.BatchNumber!;
                var stock = await _db.DrugStocks.FirstOrDefaultAsync(
                    s => s.DrugId == poItem.DrugId.Value && s.StoreId == storeId && s.BatchNumber == batch, ct);
                if (stock is null)
                {
                    stock = new DrugStock
                    {
                        DrugId = poItem.DrugId.Value,
                        StoreId = storeId,
                        BatchNumber = batch,
                        ExpiryDate = line.ExpiryDate ?? DateOnly.FromDateTime(DateTime.UtcNow.AddYears(2)),
                        QuantityOnHand = 0,
                        UnitCost = line.UnitCost
                    };
                    _db.DrugStocks.Add(stock);
                }
                stock.QuantityOnHand += line.Quantity;
                stock.UnitCost = line.UnitCost;
                stock.UpdatedAt = DateTime.UtcNow;

                _db.StockMovements.Add(new StockMovement
                {
                    DrugId = poItem.DrugId.Value,
                    StoreId = storeId,
                    BatchNumber = batch,
                    ExpiryDate = stock.ExpiryDate,
                    Kind = StockMovementKind.Receive,
                    Quantity = line.Quantity,
                    RunningBalance = stock.QuantityOnHand,
                    UnitCost = line.UnitCost,
                    Reference = grn.GrnNumber,
                    Notes = $"Received from PO {po.PoNumber}",
                    PerformedById = userId
                });
            }
            else if (poItem.InventoryItemId.HasValue)
            {
                var batch = line.BatchNumber;
                var stock = await _db.InventoryStocks.FirstOrDefaultAsync(
                    s => s.InventoryItemId == poItem.InventoryItemId.Value
                      && s.StoreId == storeId
                      && s.BatchNumber == batch, ct);
                if (stock is null)
                {
                    stock = new InventoryStock
                    {
                        InventoryItemId = poItem.InventoryItemId.Value,
                        StoreId = storeId,
                        BatchNumber = batch,
                        ExpiryDate = line.ExpiryDate,
                        QuantityOnHand = 0,
                        UnitCost = line.UnitCost
                    };
                    _db.InventoryStocks.Add(stock);
                }
                stock.QuantityOnHand += line.Quantity;
                stock.UnitCost = line.UnitCost;
                stock.UpdatedAt = DateTime.UtcNow;

                _db.InventoryStockMovements.Add(new InventoryStockMovement
                {
                    InventoryItemId = poItem.InventoryItemId.Value,
                    StoreId = storeId,
                    BatchNumber = batch,
                    ExpiryDate = line.ExpiryDate,
                    Kind = InventoryMovementKind.Receive,
                    Quantity = line.Quantity,
                    RunningBalance = stock.QuantityOnHand,
                    UnitCost = line.UnitCost,
                    Reference = grn.GrnNumber,
                    Notes = $"Received from PO {po.PoNumber}",
                    PerformedById = userId
                });
            }
        }

        grn.TotalReceivedValue = grandTotal;

        var allFullyReceived = po.Items.All(i => i.QuantityReceived >= i.QuantityOrdered);
        var anyPartial = po.Items.Any(i => i.QuantityReceived > 0);
        if (allFullyReceived) { po.Status = PurchaseOrderStatus.Received; po.ClosedAt = DateTime.UtcNow; }
        else if (anyPartial) po.Status = PurchaseOrderStatus.PartiallyReceived;

        await _db.SaveChangesAsync(ct);

        return new GrnPostResult(grn.Id, grn.GrnNumber, grandTotal, requested.Count);
    }
}
