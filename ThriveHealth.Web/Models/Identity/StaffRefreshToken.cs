using System.ComponentModel.DataAnnotations;

namespace ThriveHealth.Web.Models.Identity;

/// <summary>
/// Rotating refresh token for a staff (clinician) mobile session. Mirrors the patient
/// PortalRefreshToken but keyed by the AspNetUsers string id. Only the SHA-256 hash is stored.
/// </summary>
public class StaffRefreshToken
{
    public long Id { get; set; }

    [Required, MaxLength(450)] public string UserId { get; set; } = string.Empty;

    [Required, MaxLength(128)] public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    [MaxLength(80)] public string? CreatedByIp { get; set; }
    [MaxLength(300)] public string? UserAgent { get; set; }

    public DateTime? RevokedAt { get; set; }
    [MaxLength(128)] public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}
