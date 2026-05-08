using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Pharmacy;

namespace ThriveHealth.Web.Models.Inventory;

public enum PurchaseOrderStatus
{
    Draft = 1,
    Approved = 2,
    Issued = 3,
    PartiallyReceived = 4,
    Received = 5,
    Cancelled = 6,
    Closed = 7
}

public class PurchaseOrder
{
    public int Id { get; set; }
    public int FacilityId { get; set; }
    public Facility? Facility { get; set; }

    [Required, MaxLength(40)] public string PoNumber { get; set; } = string.Empty;

    public int SupplierId { get; set; }
    public Supplier? Supplier { get; set; }

    public int StoreId { get; set; }
    public PharmacyStore? Store { get; set; }

    public PurchaseOrderStatus Status { get; set; } = PurchaseOrderStatus.Draft;

    public DateOnly? ExpectedDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovedAt { get; set; }
    public DateTime? IssuedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public decimal SubTotal { get; set; }
    public decimal Tax { get; set; }
    public decimal TotalAmount { get; set; }

    [MaxLength(60)] public string? PaymentTerms { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }
    public string? ApprovedById { get; set; }
    public ApplicationUser? ApprovedBy { get; set; }

    public ICollection<PurchaseOrderItem> Items { get; set; } = new List<PurchaseOrderItem>();
    public ICollection<Grn> Grns { get; set; } = new List<Grn>();
}

public class PurchaseOrderItem
{
    public int Id { get; set; }

    public int PurchaseOrderId { get; set; }
    public PurchaseOrder? PurchaseOrder { get; set; }

    public int? DrugId { get; set; }
    public Drug? Drug { get; set; }

    public int? InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    [Required, MaxLength(200)] public string Description { get; set; } = string.Empty;
    [MaxLength(20)] public string? UnitOfIssue { get; set; }

    public int QuantityOrdered { get; set; }
    public int QuantityReceived { get; set; }
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    public bool IsFullyReceived => QuantityReceived >= QuantityOrdered;
    public int OutstandingQuantity => Math.Max(0, QuantityOrdered - QuantityReceived);
}
