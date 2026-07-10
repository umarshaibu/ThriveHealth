using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.Portal;

/// <summary>
/// A native push token (Expo push token / FCM / APNs) registered by a patient's mobile device.
/// Looked up by PatientId to deliver push notifications; tenant-unscoped (written from the app
/// before a tenant subdomain exists, and uniquely tied to a patient anyway).
/// </summary>
public class PortalDeviceToken
{
    public long Id { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    [Required, MaxLength(300)] public string Token { get; set; } = string.Empty;
    [MaxLength(20)] public string? Platform { get; set; } // ios | android

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
}
