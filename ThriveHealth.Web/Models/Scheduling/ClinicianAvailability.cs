using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Models.Scheduling;

public class ClinicianAvailability
{
    public int Id { get; set; }

    public int ClinicId { get; set; }
    public Clinic? Clinic { get; set; }

    public string ClinicianId { get; set; } = string.Empty;
    public ApplicationUser? Clinician { get; set; }

    public int? RoomId { get; set; }
    public Room? Room { get; set; }

    public DayOfWeek DayOfWeek { get; set; }

    public TimeOnly StartTime { get; set; }
    public TimeOnly EndTime { get; set; }

    public int? SlotMinutesOverride { get; set; }

    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }

    public bool IsActive { get; set; } = true;
}

public class ClinicianTimeOff
{
    public int Id { get; set; }
    public string ClinicianId { get; set; } = string.Empty;
    public ApplicationUser? Clinician { get; set; }

    public DateTime StartUtc { get; set; }
    public DateTime EndUtc { get; set; }

    public string? Reason { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
