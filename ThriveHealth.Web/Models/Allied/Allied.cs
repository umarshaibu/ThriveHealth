using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.Allied;

public enum AlliedServiceLine { Dental = 1, Physiotherapy = 2, Optometry = 3 }

public enum SessionStatus { Scheduled = 1, InProgress = 2, Completed = 3, Cancelled = 4, NoShow = 5 }

public class AlliedSession
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    [Required, MaxLength(40)] public string SessionNumber { get; set; } = string.Empty;
    public AlliedServiceLine ServiceLine { get; set; }
    public SessionStatus Status { get; set; } = SessionStatus.Scheduled;

    public DateTime ScheduledUtc { get; set; }
    public DateTime? StartedUtc { get; set; }
    public DateTime? CompletedUtc { get; set; }

    [MaxLength(500)] public string? ChiefComplaint { get; set; }
    [MaxLength(2000)] public string? Examination { get; set; }
    [MaxLength(2000)] public string? Assessment { get; set; }
    [MaxLength(2000)] public string? TreatmentGiven { get; set; }
    [MaxLength(1000)] public string? Plan { get; set; }
    [MaxLength(500)] public string? Modality { get; set; }

    // Dental-specific
    [MaxLength(200)] public string? ToothChart { get; set; }
    [MaxLength(40)] public string? DentalProcedureCode { get; set; }

    // Optometry-specific
    [MaxLength(20)] public string? RightEyeAcuity { get; set; }
    [MaxLength(20)] public string? LeftEyeAcuity { get; set; }
    [MaxLength(60)] public string? RightEyeRefraction { get; set; }
    [MaxLength(60)] public string? LeftEyeRefraction { get; set; }

    // Physio-specific
    public int? SessionsCompleted { get; set; }
    public int? SessionsPlanned { get; set; }
    [MaxLength(200)] public string? PhysioModalitiesUsed { get; set; }

    public string? ProviderId { get; set; }
    public ApplicationUser? Provider { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
