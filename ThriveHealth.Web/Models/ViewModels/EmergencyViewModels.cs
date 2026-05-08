using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Emergency;
using ThriveHealth.Web.Models.Patients;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Models.ViewModels;

public class EmergencyDashboardViewModel
{
    public IReadOnlyList<TriageQueueRow> Waiting { get; set; } = Array.Empty<TriageQueueRow>();
    public IReadOnlyList<TriageQueueRow> InResus { get; set; } = Array.Empty<TriageQueueRow>();
    public IReadOnlyList<ResusBay> Bays { get; set; } = Array.Empty<ResusBay>();
    public Dictionary<int, Encounter> BayOccupancy { get; set; } = new();
    public int RedCount { get; set; }
    public int OrangeCount { get; set; }
    public int YellowCount { get; set; }
    public int GreenBlueCount { get; set; }
    public int InResusCount { get; set; }
    public int OverdueCount { get; set; }
}

public class TriageQueueRow
{
    public Encounter Encounter { get; set; } = null!;
    public TriageAssessment Triage { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public int? MinutesOverdue { get; set; }
}

public class TriageFormViewModel
{
    public int? PatientId { get; set; }
    public string? PatientLabel { get; set; }

    public TriageColour Colour { get; set; } = TriageColour.Yellow;
    public ArrivalMode Arrival { get; set; } = ArrivalMode.WalkIn;

    [Required, MaxLength(500), Display(Name = "Chief complaint")]
    public string ChiefComplaint { get; set; } = string.Empty;

    [Display(Name = "Trauma case")] public bool IsTrauma { get; set; }
    [MaxLength(500), Display(Name = "Mechanism of injury")] public string? MechanismOfInjury { get; set; }

    public AvpuLevel? Avpu { get; set; }
    [Range(3, 15), Display(Name = "GCS total")] public int? GcsTotal { get; set; }

    [Display(Name = "Pregnant")] public bool IsPregnant { get; set; }
    [Display(Name = "Last meal (UTC)")] public DateTime? LastMealUtc { get; set; }

    [Display(Name = "Forensic / police case")] public bool IsForensicCase { get; set; }
    public ForensicCategory ForensicCategory { get; set; } = ForensicCategory.None;
    [MaxLength(500), Display(Name = "Police report #")] public string? PoliceReportNumber { get; set; }
    [MaxLength(500), Display(Name = "Accompanying person")] public string? AccompanyingPerson { get; set; }

    [MaxLength(1000), Display(Name = "Known allergies")] public string? KnownAllergies { get; set; }
    [MaxLength(1000), Display(Name = "Current medications")] public string? CurrentMedications { get; set; }

    [Range(40, 260), Display(Name = "Systolic BP")] public int? SystolicBp { get; set; }
    [Range(20, 200), Display(Name = "Diastolic BP")] public int? DiastolicBp { get; set; }
    [Range(20, 250), Display(Name = "Heart rate")] public int? HeartRate { get; set; }
    [Range(5, 80), Display(Name = "Respiratory rate")] public int? RespiratoryRate { get; set; }
    [Range(25, 45), Display(Name = "Temperature (°C)")] public decimal? TemperatureCelsius { get; set; }
    [Range(40, 100), Display(Name = "SpO₂ (%)")] public int? SpO2 { get; set; }
    [Range(0, 10), Display(Name = "Pain score")] public int? PainScore { get; set; }
}

public class ResusEventViewModel
{
    public int EncounterId { get; set; }
    public ResusEventKind Kind { get; set; } = ResusEventKind.Note;
    [Required, MaxLength(500)] public string Description { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Details { get; set; }
}

public class DispositionViewModel
{
    public int EncounterId { get; set; }
    public AeDisposition Disposition { get; set; } = AeDisposition.Discharged;
    [MaxLength(2000), Display(Name = "Disposition notes / summary")] public string? Notes { get; set; }
    [MaxLength(500), Display(Name = "Follow-up plan")] public string? FollowUp { get; set; }

    public int? AdmitWardId { get; set; }
    public int? AdmitBedId { get; set; }
    public string? AdmitDoctorId { get; set; }
    public string? AdmitReason { get; set; }
}

public enum AeDisposition
{
    Discharged = 1,
    DischargedWithFollowUp = 2,
    Admitted = 3,
    Theatre = 4,
    TransferredOut = 5,
    Deceased = 6,
    LeftWithoutBeingSeen = 7,
    Dama = 8
}

public class EmergencyEncounterViewModel
{
    public Encounter Encounter { get; set; } = null!;
    public TriageAssessment Triage { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public IReadOnlyList<ResusBay> Bays { get; set; } = Array.Empty<ResusBay>();
    public IReadOnlyList<ResusEvent> Events { get; set; } = Array.Empty<ResusEvent>();
    public IReadOnlyList<VitalsRecord> Vitals { get; set; } = Array.Empty<VitalsRecord>();
    public IReadOnlyList<OrderSetSummary> OrderSets { get; set; } = Array.Empty<OrderSetSummary>();
}
