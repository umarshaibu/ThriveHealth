using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Pharmacy;

namespace ThriveHealth.Web.Models.ViewModels;

public class DrugEditViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(150), Display(Name = "Generic name")] public string GenericName { get; set; } = string.Empty;
    [MaxLength(150), Display(Name = "Brand name")] public string? BrandName { get; set; }
    [MaxLength(20), Display(Name = "NAFDAC #")] public string? NafdacNumber { get; set; }
    [Required, MaxLength(50)] public string Strength { get; set; } = string.Empty;
    public DoseForm DoseForm { get; set; } = DoseForm.Tablet;
    [MaxLength(150)] public string? Manufacturer { get; set; }
    [MaxLength(20), Display(Name = "ATC")] public string? AtcCode { get; set; }
    [MaxLength(50)] public string? Category { get; set; }
    public DrugCategory Schedule { get; set; } = DrugCategory.PrescriptionOnly;
    [MaxLength(20), Display(Name = "Unit of issue")] public string? UnitOfIssue { get; set; }
    [Display(Name = "Unit price (₦)")] public decimal? UnitPrice { get; set; }
    [Display(Name = "Reorder level")] public int? ReorderLevel { get; set; }
    public bool IsActive { get; set; } = true;
}

public class DrugListViewModel
{
    public string? Search { get; set; }
    public IReadOnlyList<Drug> Drugs { get; set; } = Array.Empty<Drug>();
    public int Total { get; set; }
}

public class PharmacyWorklistRow
{
    public Prescription Prescription { get; set; } = null!;
    public int ItemCount { get; set; }
    public int OutstandingItems { get; set; }
    public int OutstandingQuantity { get; set; }
}

public class DispenseViewModel
{
    public Prescription Prescription { get; set; } = null!;
    public IReadOnlyList<PharmacyStore> Stores { get; set; } = Array.Empty<PharmacyStore>();
    public Dictionary<int, IReadOnlyList<DrugStock>> StocksByDrug { get; set; } = new();
    public IReadOnlyList<InteractionWarning> Warnings { get; set; } = Array.Empty<InteractionWarning>();
}

public class InteractionWarning
{
    public string DrugA { get; set; } = string.Empty;
    public string DrugB { get; set; } = string.Empty;
    public InteractionSeverity Severity { get; set; }
    public string Note { get; set; } = string.Empty;
}

public class DispenseSubmitDto
{
    public int PrescriptionId { get; set; }
    public int StoreId { get; set; }
    public string? CounsellingNotes { get; set; }
    public string? Notes { get; set; }
    public List<DispenseLineDto> Lines { get; set; } = new();
}

public class DispenseLineDto
{
    public int PrescriptionItemId { get; set; }
    public int? DrugId { get; set; }
    public string DrugName { get; set; } = string.Empty;
    public string? Strength { get; set; }
    public string? NafdacNumber { get; set; }
    public int Quantity { get; set; }
    public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    public decimal? UnitPrice { get; set; }
    public bool IsSubstitution { get; set; }
    public string? SubstitutionReason { get; set; }
    public string? PatientInstructions { get; set; }
}

public class StockListViewModel
{
    public IReadOnlyList<DrugStock> Stocks { get; set; } = Array.Empty<DrugStock>();
    public string? Filter { get; set; }
    public int LowStockCount { get; set; }
    public int ExpiringSoonCount { get; set; }
    public int ExpiredCount { get; set; }
}

public class ControlledRegisterViewModel
{
    public DateOnly From { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-30));
    public DateOnly To { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public IReadOnlyList<DispenseItem> Entries { get; set; } = Array.Empty<DispenseItem>();
}
