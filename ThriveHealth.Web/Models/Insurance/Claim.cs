using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.Insurance;

public enum ClaimStatus
{
    Draft = 1,
    Submitted = 2,
    Acknowledged = 3,
    PartiallyPaid = 4,
    Paid = 5,
    Denied = 6,
    Disputed = 7,
    Closed = 8
}

public enum ClaimItemKind
{
    Consultation = 1,
    Lab = 2,
    Imaging = 3,
    Drug = 4,
    Procedure = 5,
    BedDay = 6,
    Other = 99
}

public class Claim
{
    public int Id { get; set; }

    public int FacilityId { get; set; }
    public Facility? Facility { get; set; }

    public int PayerId { get; set; }
    public Payer? Payer { get; set; }

    public int? PayerPlanId { get; set; }
    public PayerPlan? PayerPlan { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public int? EncounterId { get; set; }
    public Encounter? Encounter { get; set; }

    [MaxLength(40)] public string? ClaimReference { get; set; }
    [MaxLength(40)] public string? AuthorizationCode { get; set; }
    [MaxLength(40)] public string? PayerReference { get; set; }

    public ClaimStatus Status { get; set; } = ClaimStatus.Draft;

    public DateOnly ServiceDate { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    public DateTime? RespondedAt { get; set; }
    public DateTime? ClosedAt { get; set; }

    public decimal GrossAmount { get; set; }
    public decimal CopayAmount { get; set; }
    public decimal ClaimableAmount { get; set; }
    public decimal ApprovedAmount { get; set; }
    public decimal PaidAmount { get; set; }

    [MaxLength(500)] public string? DenialReason { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }
    public string? SubmittedById { get; set; }

    public ICollection<ClaimItem> Items { get; set; } = new List<ClaimItem>();
}

public class ClaimItem
{
    public int Id { get; set; }

    public int ClaimId { get; set; }
    public Claim? Claim { get; set; }

    public ClaimItemKind Kind { get; set; }

    [Required, MaxLength(200)] public string Description { get; set; } = string.Empty;
    [MaxLength(20)] public string? ServiceCode { get; set; }
    [MaxLength(20)] public string? NafdacNumber { get; set; }

    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal CopayAmount { get; set; }
    public decimal ClaimableAmount { get; set; }
    public decimal ApprovedAmount { get; set; }

    public bool IsCovered { get; set; } = true;
    [MaxLength(500)] public string? DenialReason { get; set; }

    public int? LabOrderId { get; set; }
    public int? ImagingOrderId { get; set; }
    public int? PrescriptionItemId { get; set; }
    public int? ProcedureOrderId { get; set; }
    public int? DispenseItemId { get; set; }
}
