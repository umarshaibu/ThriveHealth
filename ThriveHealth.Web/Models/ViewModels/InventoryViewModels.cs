using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Inventory;
using ThriveHealth.Web.Models.Pharmacy;

namespace ThriveHealth.Web.Models.ViewModels;

public class SupplierEditViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    [MaxLength(150), Display(Name = "Contact person")] public string? ContactPerson { get; set; }
    [Phone, MaxLength(50)] public string? Phone { get; set; }
    [EmailAddress, MaxLength(150)] public string? Email { get; set; }
    [MaxLength(500)] public string? Address { get; set; }
    [MaxLength(40), Display(Name = "Tax ID")] public string? TaxId { get; set; }
    [MaxLength(40), Display(Name = "RC #")] public string? RcNumber { get; set; }
    [MaxLength(150), Display(Name = "Bank")] public string? BankName { get; set; }
    [MaxLength(60), Display(Name = "Account #")] public string? BankAccountNumber { get; set; }
    [MaxLength(60), Display(Name = "Payment terms")] public string? PaymentTerms { get; set; }
    [Range(0, 365), Display(Name = "Lead time (days)")] public int LeadTimeDays { get; set; } = 7;
    public bool IsActive { get; set; } = true;
}

public class InventoryItemEditViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    [MaxLength(40)] public string? Barcode { get; set; }
    public InventoryCategory Category { get; set; } = InventoryCategory.Consumable;
    [MaxLength(20), Display(Name = "Unit of issue")] public string? UnitOfIssue { get; set; }
    [Display(Name = "Unit price (₦)")] public decimal? UnitPrice { get; set; }
    [Display(Name = "Reorder level")] public int? ReorderLevel { get; set; }
    [MaxLength(150)] public string? Manufacturer { get; set; }
    [MaxLength(20), Display(Name = "NAFDAC #")] public string? NafdacNumber { get; set; }
    [Display(Name = "Track expiry")] public bool IsExpiringTracked { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class PurchaseOrderEditViewModel
{
    public int Id { get; set; }
    public int? SupplierId { get; set; }
    public int? StoreId { get; set; }
    [DataType(DataType.Date)] public DateOnly? ExpectedDate { get; set; }
    [MaxLength(60)] public string? PaymentTerms { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public List<PoLineEditViewModel> Lines { get; set; } = new();
}

public class PoLineEditViewModel
{
    public int? DrugId { get; set; }
    public int? InventoryItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public string? UnitOfIssue { get; set; }
    public int Quantity { get; set; }
    public decimal UnitPrice { get; set; }
}

public class PurchaseOrderListRow
{
    public PurchaseOrder PurchaseOrder { get; set; } = null!;
    public int LineCount { get; set; }
    public int OutstandingQuantity { get; set; }
}

public class PurchaseOrderWorklistViewModel
{
    public IReadOnlyList<PurchaseOrderListRow> Rows { get; set; } = Array.Empty<PurchaseOrderListRow>();
    public PurchaseOrderStatus? FilterStatus { get; set; }
    public int DraftCount { get; set; }
    public int IssuedCount { get; set; }
    public int PartiallyReceivedCount { get; set; }
    public decimal OutstandingValue { get; set; }
}

public class GrnReceiveViewModel
{
    public int PurchaseOrderId { get; set; }
    public int StoreId { get; set; }
    public string? SupplierInvoiceNumber { get; set; }
    public string? DeliveryNoteNumber { get; set; }
    public string? Notes { get; set; }
    public List<GrnLineEditViewModel> Lines { get; set; } = new();
}

public class GrnLineEditViewModel
{
    public int PurchaseOrderItemId { get; set; }
    public string Description { get; set; } = string.Empty;
    public int QuantityOrdered { get; set; }
    public int QuantityAlreadyReceived { get; set; }
    public int Outstanding { get; set; }
    public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public int Quantity { get; set; }
    public decimal UnitCost { get; set; }
    public string? Notes { get; set; }
}

public class StockTakeListRow
{
    public StockTake StockTake { get; set; } = null!;
    public int LineCount { get; set; }
    public int VarianceLines { get; set; }
    public decimal VarianceValue { get; set; }
}

public class StockTakeViewModel
{
    public StockTake StockTake { get; set; } = null!;
    public IReadOnlyList<StockTakeItem> Items { get; set; } = Array.Empty<StockTakeItem>();
    public decimal VarianceValue { get; set; }
    public int VarianceLines { get; set; }
}

public class StockTakeStartViewModel
{
    public int? StoreId { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

public class InventoryStockListViewModel
{
    public IReadOnlyList<InventoryStockRow> Rows { get; set; } = Array.Empty<InventoryStockRow>();
    public string? Search { get; set; }
    public int? FilterStoreId { get; set; }
    public IReadOnlyList<PharmacyStore> Stores { get; set; } = Array.Empty<PharmacyStore>();
    public int LowStockCount { get; set; }
    public int ExpiringSoonCount { get; set; }
    public int ExpiredCount { get; set; }
}

public class InventoryStockRow
{
    public InventoryStock Stock { get; set; } = null!;
    public bool IsLow { get; set; }
    public bool IsExpired { get; set; }
    public bool IsExpiringSoon { get; set; }
}
