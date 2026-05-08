using System.ComponentModel.DataAnnotations;

namespace ThriveHealth.Web.Models.Integrations;

public enum PushOwnerType { Patient = 1, Clinician = 2 }

/// <summary>
/// One Web Push subscription registered by a browser/device. Each (OwnerType, OwnerKey, Endpoint)
/// is unique — a single user can have multiple devices subscribed.
/// </summary>
public class WebPushSubscription
{
    public long Id { get; set; }

    public PushOwnerType OwnerType { get; set; }

    /// <summary>Patient.Id (int as string) for patients, AspNetUsers.Id for clinicians.</summary>
    [Required, MaxLength(80)] public string OwnerKey { get; set; } = string.Empty;

    [Required, MaxLength(700)] public string Endpoint { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string P256dhKey { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string AuthKey { get; set; } = string.Empty;

    [MaxLength(300)] public string? UserAgent { get; set; }

    public int? FacilityId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastUsedAt { get; set; }
    public DateTime? FailedAt { get; set; }
    [MaxLength(300)] public string? FailureReason { get; set; }
}
