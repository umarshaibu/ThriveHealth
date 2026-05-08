using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.Scheduling;

public enum AppointmentType
{
    NewOpd = 1,
    FollowUp = 2,
    SpecialistClinic = 3,
    Antenatal = 4,
    Immunization = 5,
    Telemedicine = 6,
    TheatreBooking = 7,
    Procedure = 8,
    Other = 99
}

public enum AppointmentStatus
{
    Scheduled = 1,
    CheckedIn = 2,
    InProgress = 3,
    Completed = 4,
    Cancelled = 5,
    NoShow = 6,
    Rescheduled = 7
}

public enum BookingChannel
{
    FrontDesk = 1,
    WalkIn = 2,
    PatientPortal = 3,
    Phone = 4,
    WhatsApp = 5,
    Ussd = 6
}

public enum AppointmentPriority
{
    Routine = 1,
    Urgent = 2,
    Emergency = 3
}

public class Appointment
{
    public int Id { get; set; }
    public int FacilityId { get; set; }
    public Facility? Facility { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public int ClinicId { get; set; }
    public Clinic? Clinic { get; set; }

    public string? ClinicianId { get; set; }
    public ApplicationUser? Clinician { get; set; }

    public int? RoomId { get; set; }
    public Room? Room { get; set; }

    public AppointmentType Type { get; set; } = AppointmentType.NewOpd;
    public AppointmentStatus Status { get; set; } = AppointmentStatus.Scheduled;
    public AppointmentPriority Priority { get; set; } = AppointmentPriority.Routine;
    public BookingChannel Channel { get; set; } = BookingChannel.FrontDesk;

    public DateTime ScheduledStartUtc { get; set; }
    public int DurationMinutes { get; set; } = 15;

    [MaxLength(500)]
    public string? ReasonForVisit { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public string? BookedById { get; set; }
    public ApplicationUser? BookedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public DateTime? CheckedInAt { get; set; }
    public DateTime? StartedAt { get; set; }
    public DateTime? CompletedAt { get; set; }

    public DateTime? CancelledAt { get; set; }
    public string? CancelReason { get; set; }
    public string? CancelledById { get; set; }

    public DateTime? NoShowMarkedAt { get; set; }

    public int? RescheduledFromId { get; set; }
    public Appointment? RescheduledFrom { get; set; }
}
