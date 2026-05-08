using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;
using ThriveHealth.Web.Models.Scheduling;

namespace ThriveHealth.Web.Models.Clinical;

public enum EncounterType
{
    OutpatientOpd = 1,
    OutpatientFollowUp = 2,
    SpecialistClinic = 3,
    Telemedicine = 4,
    Emergency = 5,
    InpatientAdmission = 6,
    AntenatalVisit = 7,
    Procedure = 8,
    Other = 99
}

public enum EncounterStatus
{
    InProgress = 1,
    Signed = 2,
    Cancelled = 3
}

public class Encounter
{
    public int Id { get; set; }
    public int FacilityId { get; set; }
    public Facility? Facility { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public int? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    public int? QueueEntryId { get; set; }
    public QueueEntry? QueueEntry { get; set; }

    public int ClinicId { get; set; }
    public Clinic? Clinic { get; set; }

    public string ClinicianId { get; set; } = string.Empty;
    public ApplicationUser? Clinician { get; set; }

    public EncounterType Type { get; set; } = EncounterType.OutpatientOpd;
    public EncounterStatus Status { get; set; } = EncounterStatus.InProgress;

    public DateTime StartedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SignedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    [MaxLength(500)] public string? ChiefComplaint { get; set; }

    public int? ResusBayId { get; set; }
    public ThriveHealth.Web.Models.Emergency.ResusBay? ResusBay { get; set; }
    public DateTime? ResusStartedAt { get; set; }
    public DateTime? ResusEndedAt { get; set; }

    public SoapNote? Soap { get; set; }
    public ThriveHealth.Web.Models.Emergency.TriageAssessment? Triage { get; set; }
    public ICollection<EncounterDiagnosis> Diagnoses { get; set; } = new List<EncounterDiagnosis>();
    public ICollection<LabOrder> LabOrders { get; set; } = new List<LabOrder>();
    public ICollection<ImagingOrder> ImagingOrders { get; set; } = new List<ImagingOrder>();
    public ICollection<ProcedureOrder> ProcedureOrders { get; set; } = new List<ProcedureOrder>();
    public ICollection<Prescription> Prescriptions { get; set; } = new List<Prescription>();
    public ICollection<ThriveHealth.Web.Models.Emergency.ResusEvent> ResusEvents { get; set; } = new List<ThriveHealth.Web.Models.Emergency.ResusEvent>();
}

public class SoapNote
{
    public int Id { get; set; }
    public int EncounterId { get; set; }
    public Encounter? Encounter { get; set; }

    public string? Subjective { get; set; }
    public string? Objective { get; set; }
    public string? Assessment { get; set; }
    public string? Plan { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public string? UpdatedById { get; set; }
}

public enum DiagnosisStatus { Working = 1, Confirmed = 2, RuledOut = 3 }

public class EncounterDiagnosis
{
    public int Id { get; set; }
    public int EncounterId { get; set; }
    public Encounter? Encounter { get; set; }

    [Required, MaxLength(20)] public string IcdCode { get; set; } = string.Empty;
    [Required, MaxLength(300)] public string Description { get; set; } = string.Empty;

    public DiagnosisStatus Status { get; set; } = DiagnosisStatus.Working;
    public bool IsPrimary { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedById { get; set; }
}
