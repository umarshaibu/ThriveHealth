using System.ComponentModel.DataAnnotations;

namespace ThriveHealth.Web.Models.Scheduling;

public enum ReminderChannel { Sms = 1, WhatsApp = 2, Email = 3 }
public enum ReminderStatus { Pending = 1, Sent = 2, Failed = 3, Cancelled = 4 }

public class ReminderJob
{
    public int Id { get; set; }

    public int AppointmentId { get; set; }
    public Appointment? Appointment { get; set; }

    public ReminderChannel Channel { get; set; }
    public ReminderStatus Status { get; set; } = ReminderStatus.Pending;

    public DateTime ScheduledForUtc { get; set; }
    public DateTime? SentAt { get; set; }

    [MaxLength(500)]
    public string? Recipient { get; set; }

    [MaxLength(1000)]
    public string? Body { get; set; }

    [MaxLength(500)]
    public string? Error { get; set; }
}
