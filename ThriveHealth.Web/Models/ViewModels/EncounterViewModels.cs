using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.ViewModels;

public class ConsultationViewModel
{
    public Encounter Encounter { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public IReadOnlyList<Allergy> Allergies { get; set; } = Array.Empty<Allergy>();
    public IReadOnlyList<Problem> ActiveProblems { get; set; } = Array.Empty<Problem>();
    public IReadOnlyList<MedicationRecord> CurrentMedications { get; set; } = Array.Empty<MedicationRecord>();
    public VitalsRecord? LatestVitals { get; set; }
    public IReadOnlyList<Encounter> PastEncounters { get; set; } = Array.Empty<Encounter>();
    public IReadOnlyList<DotPhrase> DotPhrases { get; set; } = Array.Empty<DotPhrase>();
}

public class SoapSaveDto
{
    public int EncounterId { get; set; }
    public string? Subjective { get; set; }
    public string? Objective { get; set; }
    public string? Assessment { get; set; }
    public string? Plan { get; set; }
}

public class DiagnosisAddDto
{
    public int EncounterId { get; set; }
    [Required] public string IcdCode { get; set; } = string.Empty;
    [Required] public string Description { get; set; } = string.Empty;
    public DiagnosisStatus Status { get; set; } = DiagnosisStatus.Working;
    public bool IsPrimary { get; set; }
}

public class LabOrderAddViewModel
{
    public int EncounterId { get; set; }
    public int? LabTestId { get; set; }
    [Required, MaxLength(150)] public string TestName { get; set; } = string.Empty;
    [MaxLength(80)] public string? Specimen { get; set; }
    public OrderUrgency Urgency { get; set; } = OrderUrgency.Routine;
    [MaxLength(500)] public string? ClinicalIndication { get; set; }
}

public class ImagingOrderAddViewModel
{
    public int EncounterId { get; set; }
    public ImagingModality Modality { get; set; } = ImagingModality.XRay;
    [Required, MaxLength(150)] public string StudyDescription { get; set; } = string.Empty;
    public OrderUrgency Urgency { get; set; } = OrderUrgency.Routine;
    [MaxLength(500)] public string? ClinicalIndication { get; set; }
}

public class ProcedureOrderAddViewModel
{
    public int EncounterId { get; set; }
    [Required, MaxLength(200)] public string ProcedureName { get; set; } = string.Empty;
    [MaxLength(20)] public string? CptCode { get; set; }
    public OrderUrgency Urgency { get; set; } = OrderUrgency.Routine;
    [MaxLength(500)] public string? Notes { get; set; }
}

public class PrescriptionAddViewModel
{
    public int EncounterId { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    public List<PrescriptionItemViewModel> Items { get; set; } = new() { new() };
}

public class PrescriptionItemViewModel
{
    public int? DrugId { get; set; }
    [MaxLength(200)] public string DrugName { get; set; } = string.Empty;
    [MaxLength(50)] public string? Dose { get; set; }
    [MaxLength(50)] public string? Route { get; set; }
    [MaxLength(80)] public string? Frequency { get; set; }
    [MaxLength(80)] public string? Duration { get; set; }
    public int? Quantity { get; set; }
    [MaxLength(300)] public string? Instructions { get; set; }
}

public class EncounterSummaryViewModel
{
    public Encounter Encounter { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public string FacilityName { get; set; } = string.Empty;
}
