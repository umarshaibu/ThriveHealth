using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Pharmacy;

namespace ThriveHealth.Web.Models.Inventory;

public enum GrnStatus { Draft = 1, Posted = 2, Cancelled = 3 }

public class Grn
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    [Required, MaxLength(40)] public string GrnNumber { get; set; } = string.Empty;

    public int PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public int StoreId { get; set; }
    public PharmacyStore? Store { get; set; }

    public GrnStatus Status { get; set; } = GrnStatus.Draft;

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PostedAt { get; set; }

    [MaxLength(60)] public string? SupplierInvoiceNumber { get; set; }
    [MaxLength(60)] public string? DeliveryNoteNumber { get; set; }

    public decimal TotalReceivedValue { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public string? ReceivedById { get; set; }
    public ApplicationUser? ReceivedBy { get; set; }
    public string? PostedById { get; set; }
    public ApplicationUser? PostedBy { get; set; }

    public ICollection<GrnItem> Items { get; set; } = new List<GrnItem>();
}

public class GrnItem
{
    public int Id { get; set; }

    public int GrnId { get; set; }
    public Grn? Grn { get; set; }

    public int PurchaseOrderItemId { get; set; }
    public PurchaseOrderItem? PurchaseOrderItem { get; set; }

    [MaxLength(40)] public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    public int QuantityReceived { get; set; }
    public decimal UnitCost { get; set; }
    public decimal LineTotal { get; set; }

    [MaxLength(300)] public string? Notes { get; set; }
}

public enum StockTakeStatus { Open = 1, Posted = 2, Cancelled = 3 }

public class StockTake
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    [Required, MaxLength(40)] public string TakeNumber { get; set; } = string.Empty;

    public int StoreId { get; set; }
    public PharmacyStore? Store { get; set; }

    public StockTakeStatus Status { get; set; } = StockTakeStatus.Open;

    public DateOnly TakeDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? PostedAt { get; set; }

    [MaxLength(500)] public string? Notes { get; set; }

    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }
    public string? PostedById { get; set; }
    public ApplicationUser? PostedBy { get; set; }

    public ICollection<StockTakeItem> Items { get; set; } = new List<StockTakeItem>();
}

public class StockTakeItem
{
    public int Id { get; set; }

    public int StockTakeId { get; set; }
    public StockTake? StockTake { get; set; }

    public int? DrugId { get; set; }
    public Drug? Drug { get; set; }

    public int? InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    [Required, MaxLength(200)] public string Description { get; set; } = string.Empty;
    [MaxLength(40)] public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    public int ExpectedQuantity { get; set; }
    public int CountedQuantity { get; set; }
    public int Variance => CountedQuantity - ExpectedQuantity;

    public decimal? UnitCost { get; set; }
    public decimal VarianceValue { get; set; }

    [MaxLength(300)] public string? Notes { get; set; }
}
