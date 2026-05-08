using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Pharmacy;

namespace ThriveHealth.Web.Models.Inpatient;

public enum InpatientMedicationKind { Regular = 1, Prn = 2, Stat = 3, Once = 4 }
public enum InpatientMedicationStatus { Active = 1, Held = 2, Discontinued = 3, Completed = 4 }

public class InpatientMedication
{
    public int Id { get; set; }
    public int AdmissionId { get; set; }
    public Admission? Admission { get; set; }

    public int? DrugId { get; set; }
    public Drug? Drug { get; set; }

    [Required, MaxLength(200)] public string DrugName { get; set; } = string.Empty;
    [MaxLength(50)] public string? Strength { get; set; }
    [MaxLength(50)] public string? Dose { get; set; }
    [MaxLength(50)] public string? Route { get; set; }
    [MaxLength(80)] public string? Frequency { get; set; }
    [MaxLength(300)] public string? Instructions { get; set; }
    public bool IsControlled { get; set; }

    public InpatientMedicationKind Kind { get; set; } = InpatientMedicationKind.Regular;
    public InpatientMedicationStatus Status { get; set; } = InpatientMedicationStatus.Active;

    public DateTime StartUtc { get; set; } = DateTime.UtcNow;
    public DateTime? EndUtc { get; set; }

    public string? PrescribedById { get; set; }
    public ApplicationUser? PrescribedBy { get; set; }
    public DateTime PrescribedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)] public string? StopReason { get; set; }

    public ICollection<MarSlot> Slots { get; set; } = new List<MarSlot>();
}

public enum MarSlotStatus
{
    Scheduled = 1,
    Given = 2,
    Missed = 3,
    Refused = 4,
    Held = 5,
    Cancelled = 6,
    NotApplicable = 7
}

public class MarSlot
{
    public int Id { get; set; }
    public int InpatientMedicationId { get; set; }
    public InpatientMedication? InpatientMedication { get; set; }

    public DateTime ScheduledUtc { get; set; }
    public MarSlotStatus Status { get; set; } = MarSlotStatus.Scheduled;

    public DateTime? AdministeredUtc { get; set; }
    public string? AdministeredById { get; set; }
    public ApplicationUser? AdministeredBy { get; set; }

    [MaxLength(50)] public string? ActualDose { get; set; }
    [MaxLength(50)] public string? Route { get; set; }
    [MaxLength(40)] public string? BatchNumber { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}
