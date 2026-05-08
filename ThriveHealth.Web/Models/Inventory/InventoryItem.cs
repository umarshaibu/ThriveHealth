using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Pharmacy;

namespace ThriveHealth.Web.Models.Inventory;

public enum InventoryCategory
{
    Consumable = 1,
    Reagent = 2,
    Equipment = 3,
    OfficeSupply = 4,
    Linen = 5,
    Food = 6,
    Cleaning = 7,
    Other = 99
}

public class InventoryItem
{
    public int Id { get; set; }

    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    [MaxLength(40)] public string? Barcode { get; set; }

    public InventoryCategory Category { get; set; } = InventoryCategory.Consumable;

    [MaxLength(20)] public string? UnitOfIssue { get; set; }
    public decimal? UnitPrice { get; set; }
    public int? ReorderLevel { get; set; }

    [MaxLength(150)] public string? Manufacturer { get; set; }
    [MaxLength(20)] public string? NafdacNumber { get; set; }

    public bool IsExpiringTracked { get; set; } = true;
    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class InventoryStock
{
    public int Id { get; set; }

    public int InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    public int StoreId { get; set; }
    public PharmacyStore? Store { get; set; }

    [MaxLength(40)] public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    public int QuantityOnHand { get; set; }
    public decimal? UnitCost { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum InventoryMovementKind
{
    OpeningBalance = 1,
    Receive = 2,
    Issue = 3,
    Adjustment = 4,
    Transfer = 5,
    Damage = 6,
    ExpiredWriteOff = 7,
    Return = 8
}

public class InventoryStockMovement
{
    public int Id { get; set; }

    public int InventoryItemId { get; set; }
    public InventoryItem? InventoryItem { get; set; }

    public int StoreId { get; set; }
    public PharmacyStore? Store { get; set; }

    [MaxLength(40)] public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    public InventoryMovementKind Kind { get; set; }
    public int Quantity { get; set; }
    public int RunningBalance { get; set; }

    public decimal? UnitCost { get; set; }

    [MaxLength(200)] public string? Reference { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public string? PerformedById { get; set; }
    public ApplicationUser? PerformedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
