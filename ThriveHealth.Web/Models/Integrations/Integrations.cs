using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Billing;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.Integrations;

public enum MessageStatus { Queued = 1, Sending = 2, Sent = 3, Delivered = 4, Failed = 5, Suppressed = 6 }
public enum MessagePurpose
{
    AppointmentReminder = 1,
    LabResultReady = 2,
    Otp = 3,
    PortalVerification = 4,
    BillReceipt = 5,
    PaymentReceipt = 6,
    TeleSessionReady = 7,
    AdHoc = 99
}

public class SmsMessage
{
    public long Id { get; set; }
    public int FacilityId { get; set; }

    [Required, MaxLength(20)] public string ToPhone { get; set; } = string.Empty;
    [Required, MaxLength(640)] public string Body { get; set; } = string.Empty;

    public MessagePurpose Purpose { get; set; } = MessagePurpose.AdHoc;
    public MessageStatus Status { get; set; } = MessageStatus.Queued;

    public int? PatientId { get; set; }
    public Patient? Patient { get; set; }

    [MaxLength(60)] public string? Provider { get; set; }
    [MaxLength(60)] public string? ProviderMessageId { get; set; }
    [MaxLength(500)] public string? ProviderResponse { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? FailedAt { get; set; }

    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }
}

public class EmailMessage
{
    public long Id { get; set; }
    public int FacilityId { get; set; }

    [Required, MaxLength(150)] public string ToEmail { get; set; } = string.Empty;
    [MaxLength(150)] public string? ToName { get; set; }
    [Required, MaxLength(200)] public string Subject { get; set; } = string.Empty;
    [Required] public string BodyHtml { get; set; } = string.Empty;

    public MessagePurpose Purpose { get; set; } = MessagePurpose.AdHoc;
    public MessageStatus Status { get; set; } = MessageStatus.Queued;

    public int? PatientId { get; set; }
    public Patient? Patient { get; set; }

    [MaxLength(60)] public string? Provider { get; set; }
    [MaxLength(120)] public string? ProviderMessageId { get; set; }
    [MaxLength(500)] public string? ProviderResponse { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SentAt { get; set; }
    public DateTime? DeliveredAt { get; set; }
    public DateTime? FailedAt { get; set; }

    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }
}

public enum PaymentTransactionStatus { Initiated = 1, Pending = 2, Successful = 3, Failed = 4, Cancelled = 5, Refunded = 6 }

public class PaymentTransaction
{
    public long Id { get; set; }
    public int FacilityId { get; set; }

    [Required, MaxLength(60)] public string Reference { get; set; } = string.Empty;
    [MaxLength(60)] public string? ProviderReference { get; set; }
    [MaxLength(60)] public string Provider { get; set; } = "Logging";

    public int? BillId { get; set; }
    public Bill? Bill { get; set; }

    public int? PatientId { get; set; }
    public Patient? Patient { get; set; }

    public decimal Amount { get; set; }
    [MaxLength(10)] public string Currency { get; set; } = "NGN";
    [MaxLength(150)] public string? CustomerEmail { get; set; }
    [MaxLength(20)] public string? CustomerPhone { get; set; }

    public PaymentTransactionStatus Status { get; set; } = PaymentTransactionStatus.Initiated;
    [MaxLength(500)] public string? ProviderResponse { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    public int? PaymentId { get; set; }
    public Payment? Payment { get; set; }

    public string? InitiatedById { get; set; }
    public ApplicationUser? InitiatedBy { get; set; }
}
