using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.Clinical;

public enum OrderStatus
{
    Ordered = 1,
    Acknowledged = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5,
    Rejected = 6
}

public enum OrderUrgency { Routine = 1, Urgent = 2, Stat = 3 }

public class LabOrder
{
    public int Id { get; set; }
    public int EncounterId { get; set; }
    public Encounter? Encounter { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public int? LabTestId { get; set; }
    public ThriveHealth.Web.Models.Diagnostics.LabTest? LabTest { get; set; }

    [Required, MaxLength(150)] public string TestName { get; set; } = string.Empty;
    [MaxLength(20)] public string? LoincCode { get; set; }
    [MaxLength(80)] public string? Specimen { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Ordered;
    public OrderUrgency Urgency { get; set; } = OrderUrgency.Routine;

    [MaxLength(500)] public string? ClinicalIndication { get; set; }
    [MaxLength(500)] public string? ResultSummary { get; set; }

    [MaxLength(40)] public string? AccessionNumber { get; set; }
    public DateTime? CollectedAt { get; set; }
    public string? CollectedById { get; set; }
    public ApplicationUser? CollectedBy { get; set; }

    public DateTime OrderedAt { get; set; } = DateTime.UtcNow;
    public string? OrderedById { get; set; }
    public ApplicationUser? OrderedBy { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ThriveHealth.Web.Models.Diagnostics.LabResult? Result { get; set; }
}

public enum ImagingModality { XRay = 1, Ultrasound = 2, CT = 3, MRI = 4, Mammography = 5, Fluoroscopy = 6, Other = 99 }

public class ImagingOrder
{
    public int Id { get; set; }
    public int EncounterId { get; set; }
    public Encounter? Encounter { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public ImagingModality Modality { get; set; } = ImagingModality.XRay;
    [Required, MaxLength(150)] public string StudyDescription { get; set; } = string.Empty;
    [MaxLength(500)] public string? ClinicalIndication { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Ordered;
    public OrderUrgency Urgency { get; set; } = OrderUrgency.Routine;

    [MaxLength(40)] public string? AccessionNumber { get; set; }

    public DateTime OrderedAt { get; set; } = DateTime.UtcNow;
    public string? OrderedById { get; set; }
    public ApplicationUser? OrderedBy { get; set; }
    public DateTime? CompletedAt { get; set; }

    public ThriveHealth.Web.Models.Diagnostics.ImagingReport? Report { get; set; }
}

public class ProcedureOrder
{
    public int Id { get; set; }
    public int EncounterId { get; set; }
    public Encounter? Encounter { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    [Required, MaxLength(200)] public string ProcedureName { get; set; } = string.Empty;
    [MaxLength(20)] public string? CptCode { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public OrderStatus Status { get; set; } = OrderStatus.Ordered;
    public OrderUrgency Urgency { get; set; } = OrderUrgency.Routine;

    public DateTime OrderedAt { get; set; } = DateTime.UtcNow;
    public string? OrderedById { get; set; }
    public ApplicationUser? OrderedBy { get; set; }
    public DateTime? CompletedAt { get; set; }
}

public enum PrescriptionStatus { Issued = 1, PartiallyDispensed = 2, Dispensed = 3, Cancelled = 4 }

public class Prescription
{
    public int Id { get; set; }
    public int EncounterId { get; set; }
    public Encounter? Encounter { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public PrescriptionStatus Status { get; set; } = PrescriptionStatus.Issued;
    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
    public string? PrescribedById { get; set; }
    public ApplicationUser? PrescribedBy { get; set; }

    [MaxLength(500)] public string? Notes { get; set; }

    public ICollection<PrescriptionItem> Items { get; set; } = new List<PrescriptionItem>();
}

public class PrescriptionItem
{
    public int Id { get; set; }
    public int PrescriptionId { get; set; }
    public Prescription? Prescription { get; set; }

    public int? DrugId { get; set; }
    public ThriveHealth.Web.Models.Pharmacy.Drug? Drug { get; set; }

    [Required, MaxLength(200)] public string DrugName { get; set; } = string.Empty;

    [MaxLength(20)] public string? NafdacNumber { get; set; }

    [MaxLength(50)] public string? Dose { get; set; }
    [MaxLength(50)] public string? Route { get; set; }
    [MaxLength(80)] public string? Frequency { get; set; }
    [MaxLength(80)] public string? Duration { get; set; }
    public int? Quantity { get; set; }

    [MaxLength(300)] public string? Instructions { get; set; }
    public bool IsControlled { get; set; }

    public int QuantityDispensed { get; set; }
    public bool IsFullyDispensed => Quantity.HasValue && QuantityDispensed >= Quantity.Value;
}
