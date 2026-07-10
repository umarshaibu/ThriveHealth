using System.ComponentModel.DataAnnotations;

namespace ThriveHealth.Web.Models.Portal;

/// <summary>
/// A rotating refresh token issued to a patient mobile/native client. Only the SHA-256 hash of
/// the opaque token is stored — the raw value is returned to the client once and never persisted.
/// On use, a token is revoked and replaced (rotation), so a stolen-and-replayed token is detectable.
/// </summary>
public class PortalRefreshToken
{
    public long Id { get; set; }

    public int PortalAccountId { get; set; }
    public PortalAccount? PortalAccount { get; set; }

    /// <summary>SHA-256 (hex) of the opaque refresh token presented by the client.</summary>
    [Required, MaxLength(128)]
    public string TokenHash { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }

    [MaxLength(80)] public string? CreatedByIp { get; set; }
    [MaxLength(300)] public string? UserAgent { get; set; }

    public DateTime? RevokedAt { get; set; }
    /// <summary>Hash of the token that superseded this one during rotation (audit trail).</summary>
    [MaxLength(128)] public string? ReplacedByTokenHash { get; set; }

    public bool IsActive => RevokedAt is null && DateTime.UtcNow < ExpiresAt;
}
