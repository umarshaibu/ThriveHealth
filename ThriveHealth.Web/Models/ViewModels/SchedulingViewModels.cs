using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Patients;
using ThriveHealth.Web.Models.Scheduling;

namespace ThriveHealth.Web.Models.ViewModels;

public class ClinicEditViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    public ClinicSpecialty Specialty { get; set; } = ClinicSpecialty.GeneralOpd;
    [Range(5, 240)] public int DefaultSlotMinutes { get; set; } = 15;
    [MaxLength(20)] public string ColorHex { get; set; } = "#1f6feb";
    public bool IsActive { get; set; } = true;
}

public class AvailabilityEditViewModel
{
    public int? Id { get; set; }
    public int ClinicId { get; set; }
    [Required] public string ClinicianId { get; set; } = string.Empty;
    public int? RoomId { get; set; }
    public DayOfWeek DayOfWeek { get; set; }
    [Required] public TimeOnly StartTime { get; set; } = new(8, 0);
    [Required] public TimeOnly EndTime { get; set; } = new(17, 0);
    public int? SlotMinutesOverride { get; set; }
}

public class AppointmentBookViewModel
{
    public int? PatientId { get; set; }
    public string? PatientLabel { get; set; }
    public int? ClinicId { get; set; }
    public DateOnly Date { get; set; } = DateOnly.FromDateTime(DateTime.Today);

    public DateTime? ScheduledStartUtc { get; set; }
    public string? ClinicianId { get; set; }
    public int? RoomId { get; set; }
    public int DurationMinutes { get; set; } = 15;

    public AppointmentType Type { get; set; } = AppointmentType.NewOpd;
    public AppointmentPriority Priority { get; set; } = AppointmentPriority.Routine;

    [MaxLength(500)] public string? ReasonForVisit { get; set; }
}

public class AppointmentListViewModel
{
    public DateOnly Date { get; set; }
    public IReadOnlyList<Appointment> Appointments { get; set; } = Array.Empty<Appointment>();
}

public class CheckInViewModel
{
    public int? PatientId { get; set; }
    public string? PatientLabel { get; set; }
    public int ClinicId { get; set; }
    public string? ClinicianId { get; set; }
    public AppointmentPriority Priority { get; set; } = AppointmentPriority.Routine;
    [MaxLength(500)] public string? Complaint { get; set; }
    public int? AppointmentId { get; set; }
}

public class TriageViewModel
{
    public int QueueEntryId { get; set; }
    public AppointmentPriority Priority { get; set; } = AppointmentPriority.Routine;
    [Range(0, 14)] public int? Mews { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

public class QueueBoardViewModel
{
    public Clinic Clinic { get; set; } = null!;
    public IReadOnlyList<QueueEntry> Entries { get; set; } = Array.Empty<QueueEntry>();
}

public class MyQueueViewModel
{
    public IReadOnlyList<QueueEntry> Entries { get; set; } = Array.Empty<QueueEntry>();
    public IReadOnlyList<Clinic> Clinics { get; set; } = Array.Empty<Clinic>();
    public int? FilterClinicId { get; set; }
}
