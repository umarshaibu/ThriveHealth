using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Inventory;

namespace ThriveHealth.Web.Services;

public record PoLineRequest(int? DrugId, int? InventoryItemId, string Description, string? UnitOfIssue, int Quantity, decimal UnitPrice);

public interface IPurchaseOrderService
{
    Task<string> GeneratePoNumberAsync(CancellationToken ct = default);
    Task<int> CreateAsync(int facilityId, int supplierId, int storeId, DateOnly? expectedDate, string? notes, IEnumerable<PoLineRequest> lines, string userId, CancellationToken ct = default);
    Task UpdateLinesAsync(int poId, IEnumerable<PoLineRequest> lines, CancellationToken ct = default);
    Task<bool> ApproveAsync(int poId, string userId, CancellationToken ct = default);
    Task<bool> IssueAsync(int poId, string userId, CancellationToken ct = default);
    Task<bool> CancelAsync(int poId, string userId, CancellationToken ct = default);
}

public class PurchaseOrderService : IPurchaseOrderService
{
    private readonly ApplicationDbContext _db;
    public PurchaseOrderService(ApplicationDbContext db) => _db = db;

    public async Task<string> GeneratePoNumberAsync(CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"PO-{year}-";
        var existing = await _db.PurchaseOrders.AsNoTracking()
            .Where(p => p.PoNumber.StartsWith(prefix))
            .Select(p => p.PoNumber).ToListAsync(ct);
        int next = 1;
        foreach (var p in existing)
        {
            var tail = p.Substring(prefix.Length);
            if (int.TryParse(tail, out var n) && n >= next) next = n + 1;
        }
        return $"{prefix}{next:D5}";
    }

    public async Task<int> CreateAsync(int facilityId, int supplierId, int storeId, DateOnly? expectedDate, string? notes, IEnumerable<PoLineRequest> lines, string userId, CancellationToken ct = default)
    {
        var po = new PurchaseOrder
        {
            FacilityId = facilityId,
            SupplierId = supplierId,
            StoreId = storeId,
            ExpectedDate = expectedDate,
            Notes = notes,
            CreatedById = userId,
            CreatedAt = DateTime.UtcNow,
            Status = PurchaseOrderStatus.Draft,
            PoNumber = await GeneratePoNumberAsync(ct)
        };

        foreach (var l in lines.Where(x => x.Quantity > 0))
        {
            po.Items.Add(new PurchaseOrderItem
            {
                DrugId = l.DrugId,
                InventoryItemId = l.InventoryItemId,
                Description = l.Description.Trim(),
                UnitOfIssue = l.UnitOfIssue,
                QuantityOrdered = l.Quantity,
                UnitPrice = l.UnitPrice,
                LineTotal = l.UnitPrice * l.Quantity
            });
        }
        po.SubTotal = po.Items.Sum(i => i.LineTotal);
        po.TotalAmount = po.SubTotal + po.Tax;

        _db.PurchaseOrders.Add(po);
        await _db.SaveChangesAsync(ct);
        return po.Id;
    }

    public async Task UpdateLinesAsync(int poId, IEnumerable<PoLineRequest> lines, CancellationToken ct = default)
    {
        var po = await _db.PurchaseOrders.Include(p => p.Items).FirstAsync(p => p.Id == poId, ct);
        if (po.Status != PurchaseOrderStatus.Draft) throw new InvalidOperationException("PO not editable.");

        _db.PurchaseOrderItems.RemoveRange(po.Items);
        po.Items.Clear();
        foreach (var l in lines.Where(x => x.Quantity > 0))
        {
            po.Items.Add(new PurchaseOrderItem
            {
                PurchaseOrderId = po.Id,
                DrugId = l.DrugId,
                InventoryItemId = l.InventoryItemId,
                Description = l.Description.Trim(),
                UnitOfIssue = l.UnitOfIssue,
                QuantityOrdered = l.Quantity,
                UnitPrice = l.UnitPrice,
                LineTotal = l.UnitPrice * l.Quantity
            });
        }
        po.SubTotal = po.Items.Sum(i => i.LineTotal);
        po.TotalAmount = po.SubTotal + po.Tax;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> ApproveAsync(int poId, string userId, CancellationToken ct = default)
    {
        var po = await _db.PurchaseOrders.Include(p => p.Items).FirstOrDefaultAsync(p => p.Id == poId, ct);
        if (po is null) return false;
        if (po.Status != PurchaseOrderStatus.Draft) return false;
        if (!po.Items.Any()) return false;

        po.Status = PurchaseOrderStatus.Approved;
        po.ApprovedAt = DateTime.UtcNow;
        po.ApprovedById = userId;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> IssueAsync(int poId, string userId, CancellationToken ct = default)
    {
        var po = await _db.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == poId, ct);
        if (po is null) return false;
        if (po.Status != PurchaseOrderStatus.Approved) return false;

        po.Status = PurchaseOrderStatus.Issued;
        po.IssuedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> CancelAsync(int poId, string userId, CancellationToken ct = default)
    {
        var po = await _db.PurchaseOrders.FirstOrDefaultAsync(p => p.Id == poId, ct);
        if (po is null) return false;
        if (po.Status == PurchaseOrderStatus.Received || po.Status == PurchaseOrderStatus.Closed) return false;
        po.Status = PurchaseOrderStatus.Cancelled;
        po.ClosedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
