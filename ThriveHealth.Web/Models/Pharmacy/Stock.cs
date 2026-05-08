using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Models.Pharmacy;

public enum StoreType
{
    MainPharmacy = 1,
    WardSatellite = 2,
    Theatre = 3,
    Emergency = 4,
    CentralStore = 5
}

public class PharmacyStore
{
    public int Id { get; set; }
    public int FacilityId { get; set; }
    public Facility? Facility { get; set; }

    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;

    public StoreType Type { get; set; } = StoreType.MainPharmacy;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class DrugStock
{
    public int Id { get; set; }

    public int DrugId { get; set; }
    public Drug? Drug { get; set; }

    public int StoreId { get; set; }
    public PharmacyStore? Store { get; set; }

    [Required, MaxLength(40)] public string BatchNumber { get; set; } = string.Empty;

    public DateOnly ExpiryDate { get; set; }

    public int QuantityOnHand { get; set; }
    public decimal? UnitCost { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public bool IsExpired => ExpiryDate < DateOnly.FromDateTime(DateTime.UtcNow);
    public bool ExpiringSoon => !IsExpired && ExpiryDate <= DateOnly.FromDateTime(DateTime.UtcNow.AddDays(90));
}

public enum StockMovementKind
{
    OpeningBalance = 1,
    Receive = 2,
    Dispense = 3,
    ReturnToStock = 4,
    Adjustment = 5,
    ExpiredWriteOff = 6,
    Transfer = 7,
    Damage = 8
}

public class StockMovement
{
    public int Id { get; set; }

    public int DrugId { get; set; }
    public Drug? Drug { get; set; }

    public int StoreId { get; set; }
    public PharmacyStore? Store { get; set; }

    [MaxLength(40)] public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    public StockMovementKind Kind { get; set; }
    public int Quantity { get; set; }
    public int RunningBalance { get; set; }

    public decimal? UnitCost { get; set; }

    [MaxLength(200)] public string? Reference { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public string? PerformedById { get; set; }
    public ApplicationUser? PerformedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
