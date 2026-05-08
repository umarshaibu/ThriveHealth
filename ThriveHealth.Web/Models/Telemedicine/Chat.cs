using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Billing;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.Telemedicine;

public enum ChatSenderRole { Patient = 1, Clinician = 2, System = 3 }

/// <summary>
/// One persisted chat message from a tele-medicine chat thread. Unlike the LiveKit data-channel chat
/// in video/audio rooms, chat-mode sessions persist messages here so patients and clinicians can pick
/// up the conversation asynchronously.
/// </summary>
public class TeleChatMessage
{
    public long Id { get; set; }

    public int TeleSessionId { get; set; }
    public TeleSession? TeleSession { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public ChatSenderRole SenderRole { get; set; }
    public string? SenderUserId { get; set; }     // populated for Clinician + System
    public ApplicationUser? SenderUser { get; set; }

    [Required, MaxLength(2000)] public string Body { get; set; } = string.Empty;

    /// <summary>If this message is a reply, the id of the message being replied to.</summary>
    public long? RepliesToMessageId { get; set; }
    public TeleChatMessage? RepliesToMessage { get; set; }

    public DateTime SentAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReadByPatientAt { get; set; }
    public DateTime? ReadByClinicianAt { get; set; }
}

/// <summary>
/// File uploaded into a chat thread (image of a rash, pharmacy receipt, lab printout). Stored on the
/// app's local filesystem under wwwroot/uploads/chat — sufficient for self-hosted deployments.
/// </summary>
public class TeleChatAttachment
{
    public long Id { get; set; }

    public long MessageId { get; set; }
    public TeleChatMessage? Message { get; set; }

    [Required, MaxLength(255)] public string FileName { get; set; } = string.Empty;
    [Required, MaxLength(120)] public string ContentType { get; set; } = string.Empty;
    public long SizeBytes { get; set; }

    /// <summary>Web-accessible URL — relative to wwwroot, e.g. /uploads/chat/2026/05/abcd.jpg.</summary>
    [Required, MaxLength(500)] public string Url { get; set; } = string.Empty;

    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}

/// <summary>
/// Additional clinicians invited into a tele-session (group / multi-disciplinary chat). The session's
/// primary clinician sits on TeleSession.ClinicianId; everyone else lives here.
/// </summary>
public class TeleSessionParticipant
{
    public int Id { get; set; }
    public int TeleSessionId { get; set; }
    public TeleSession? TeleSession { get; set; }

    public string ClinicianId { get; set; } = string.Empty;
    public ApplicationUser? Clinician { get; set; }

    [MaxLength(120)] public string? Role { get; set; }   // free-text e.g. "Cardiology consultant"

    public DateTime AddedAt { get; set; } = DateTime.UtcNow;
    public string? AddedById { get; set; }
}

/// <summary>
/// 24-hour chat package — patients can buy unlimited chat consults within the validity window.
/// While active, request-tele-chat skips the per-consult fee.
/// </summary>
public class ChatPackage
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public int? BillId { get; set; }
    public Bill? Bill { get; set; }

    [Required, MaxLength(40)] public string PackageNumber { get; set; } = string.Empty;
    public decimal Price { get; set; }

    public DateTime PurchasedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    public bool IsActiveAt(DateTime utc) => utc < ExpiresAt;
}
