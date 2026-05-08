using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.Theatre;

public class Theatre
{
    public int Id { get; set; }
    public int FacilityId { get; set; }
    public Facility? Facility { get; set; }

    [Required, MaxLength(60)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    [MaxLength(40)] public string? Specialty { get; set; }
    public bool IsEmergencyTheatre { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum TheatreSessionStatus
{
    Scheduled = 1,
    PreOp = 2,
    InTheatre = 3,
    Recovery = 4,
    Completed = 5,
    Cancelled = 6,
    Postponed = 7
}

public enum AnaesthesiaType
{
    GeneralAnaesthesia = 1,
    Spinal = 2,
    Epidural = 3,
    Regional = 4,
    Local = 5,
    Sedation = 6,
    Combined = 7,
    None = 0
}

public enum CaseUrgency { Elective = 1, ScheduledUrgent = 2, Urgent = 3, Emergency = 4 }

public class TheatreSession
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    public int TheatreId { get; set; }
    public Theatre? Theatre { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public string LeadSurgeonId { get; set; } = string.Empty;
    public ApplicationUser? LeadSurgeon { get; set; }

    public string? AnaesthetistId { get; set; }
    public ApplicationUser? Anaesthetist { get; set; }

    public string? ScrubNurseId { get; set; }
    public ApplicationUser? ScrubNurse { get; set; }

    [Required, MaxLength(200)] public string ProcedureName { get; set; } = string.Empty;
    [MaxLength(20)] public string? CptCode { get; set; }

    public CaseUrgency Urgency { get; set; } = CaseUrgency.Elective;
    public AnaesthesiaType Anaesthesia { get; set; } = AnaesthesiaType.GeneralAnaesthesia;

    public TheatreSessionStatus Status { get; set; } = TheatreSessionStatus.Scheduled;

    public DateTime ScheduledStartUtc { get; set; }
    public int EstimatedMinutes { get; set; } = 60;

    public DateTime? PreOpAt { get; set; }
    public DateTime? KnifeOnSkinAt { get; set; }
    public DateTime? KnifeOffSkinAt { get; set; }
    public DateTime? RecoveryAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    [MaxLength(500)] public string? Indication { get; set; }
    [MaxLength(2000)] public string? PreOpAssessment { get; set; }
    [MaxLength(2000)] public string? OperativeNote { get; set; }
    [MaxLength(2000)] public string? PostOpInstructions { get; set; }

    public int? EstimatedBloodLossMl { get; set; }
    public int? CrystalloidGivenMl { get; set; }
    [MaxLength(500)] public string? ImplantsUsed { get; set; }
    [MaxLength(500)] public string? Complications { get; set; }
    [MaxLength(60)] public string? AsaScore { get; set; }

    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ChecklistItem> Checklist { get; set; } = new List<ChecklistItem>();
    public ICollection<SessionEvent> Events { get; set; } = new List<SessionEvent>();
}

public enum ChecklistPhase { SignIn = 1, TimeOut = 2, SignOut = 3 }

public class ChecklistItem
{
    public int Id { get; set; }

    public int TheatreSessionId { get; set; }
    public TheatreSession? TheatreSession { get; set; }

    public ChecklistPhase Phase { get; set; }
    [Required, MaxLength(200)] public string Question { get; set; } = string.Empty;
    public bool IsConfirmed { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }

    public DateTime? ConfirmedAt { get; set; }
    public string? ConfirmedById { get; set; }
    public ApplicationUser? ConfirmedBy { get; set; }

    public int SortOrder { get; set; }
}

public enum SessionEventKind
{
    Note = 1,
    DrugGiven = 2,
    Fluid = 3,
    BloodProduct = 4,
    Specimen = 5,
    Complication = 6,
    Phase = 7
}

public class SessionEvent
{
    public int Id { get; set; }

    public int TheatreSessionId { get; set; }
    public TheatreSession? TheatreSession { get; set; }

    public SessionEventKind Kind { get; set; }
    [Required, MaxLength(500)] public string Description { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Details { get; set; }

    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    public string? RecordedById { get; set; }
    public ApplicationUser? RecordedBy { get; set; }
}
