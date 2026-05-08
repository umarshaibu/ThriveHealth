using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.Scheduling;

public enum QueueStatus
{
    Waiting = 1,
    InTriage = 2,
    Triaged = 3,
    Called = 4,
    InConsultation = 5,
    Completed = 6,
    Skipped = 7,
    LeftWithoutBeingSeen = 8
}

public class QueueEntry
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    public int? AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public int ClinicId { get; set; }
    public Clinic? Clinic { get; set; }

    public string? ClinicianId { get; set; }
    public ApplicationUser? Clinician { get; set; }

    [Required, MaxLength(20)]
    public string TicketNumber { get; set; } = string.Empty;

    public DateOnly TicketDate { get; set; }

    public AppointmentPriority Priority { get; set; } = AppointmentPriority.Routine;
    public QueueStatus Status { get; set; } = QueueStatus.Waiting;

    public DateTime CheckedInAt { get; set; } = DateTime.UtcNow;
    public DateTime? TriagedAt { get; set; }
    public DateTime? CalledAt { get; set; }
    public DateTime? ConsultStartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public string? CheckedInById { get; set; }
    public string? TriagedById { get; set; }

    public int? TriageMews { get; set; }

    [MaxLength(500)]
    public string? TriageNotes { get; set; }

    [MaxLength(500)]
    public string? Complaint { get; set; }

    public bool IsActive => Status != QueueStatus.Completed
                         && Status != QueueStatus.Skipped
                         && Status != QueueStatus.LeftWithoutBeingSeen;
}

public class TicketCounter
{
    public int FacilityId { get; set; }
    public int ClinicId { get; set; }
    public DateOnly Date { get; set; }
    public int LastSequence { get; set; }
}
