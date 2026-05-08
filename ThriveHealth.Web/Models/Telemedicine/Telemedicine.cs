using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Billing;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.Telemedicine;

public enum TeleSessionMode { Video = 1, Audio = 2, Chat = 3 }

public enum TeleSessionStatus
{
    Requested = 1,
    Scheduled = 2,
    PatientWaiting = 3,
    InCall = 4,
    Completed = 5,
    Cancelled = 6,
    NoShowPatient = 7,
    NoShowClinician = 8
}

public class TeleSession
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    [Required, MaxLength(40)] public string SessionNumber { get; set; } = string.Empty;

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public string? ClinicianId { get; set; }
    public ApplicationUser? Clinician { get; set; }

    public int? EncounterId { get; set; }
    public Encounter? Encounter { get; set; }

    /// <summary>Bill raised for this consult — patient must settle before joining the call.</summary>
    public int? BillId { get; set; }
    public Bill? Bill { get; set; }

    public TeleSessionMode Mode { get; set; } = TeleSessionMode.Video;
    public TeleSessionStatus Status { get; set; } = TeleSessionStatus.Requested;

    public DateTime ScheduledStartUtc { get; set; } = DateTime.UtcNow.AddMinutes(15);
    public DateTime? PatientJoinedAt { get; set; }
    public DateTime? ClinicianJoinedAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? EndedAt { get; set; }

    [Required, MaxLength(60)] public string RoomToken { get; set; } = string.Empty;
    [MaxLength(200)] public string? JoinUrl { get; set; }

    [MaxLength(500)] public string? ConsultationReason { get; set; }
    [MaxLength(2000)] public string? ClinicianNotes { get; set; }
    [MaxLength(2000)] public string? PatientSymptoms { get; set; }
    public int? PatientRating { get; set; }
    [MaxLength(500)] public string? PatientFeedback { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public int DurationMinutes
    {
        get
        {
            if (StartedAt is null) return 0;
            var end = EndedAt ?? DateTime.UtcNow;
            return Math.Max(0, (int)(end - StartedAt.Value).TotalMinutes);
        }
    }
}
