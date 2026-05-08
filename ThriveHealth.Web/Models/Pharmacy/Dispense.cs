using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.Pharmacy;

public enum DispenseStatus { Draft = 1, Completed = 2, Cancelled = 3 }

public class Dispense
{
    public int Id { get; set; }

    public int FacilityId { get; set; }

    public int PrescriptionId { get; set; }
    public Prescription? Prescription { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public int StoreId { get; set; }
    public PharmacyStore? Store { get; set; }

    public string? DispensedById { get; set; }
    public ApplicationUser? DispensedBy { get; set; }
    public DateTime DispensedAt { get; set; } = DateTime.UtcNow;

    public DispenseStatus Status { get; set; } = DispenseStatus.Draft;

    [MaxLength(1000)] public string? CounsellingNotes { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public decimal TotalAmount { get; set; }

    public ICollection<DispenseItem> Items { get; set; } = new List<DispenseItem>();
}

public class DispenseItem
{
    public int Id { get; set; }

    public int DispenseId { get; set; }
    public Dispense? Dispense { get; set; }

    public int? PrescriptionItemId { get; set; }
    public PrescriptionItem? PrescriptionItem { get; set; }

    public int? DrugId { get; set; }
    public Drug? Drug { get; set; }

    [Required, MaxLength(200)] public string DrugName { get; set; } = string.Empty;
    [MaxLength(50)] public string? Strength { get; set; }
    [MaxLength(20)] public string? NafdacNumber { get; set; }

    public int QuantityDispensed { get; set; }

    [MaxLength(40)] public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }

    public decimal? UnitPrice { get; set; }
    public decimal LineTotal { get; set; }

    public bool IsSubstitution { get; set; }
    [MaxLength(300)] public string? SubstitutionReason { get; set; }
    [MaxLength(300)] public string? PatientInstructions { get; set; }
}
