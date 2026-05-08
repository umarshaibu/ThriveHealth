using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Models.Audit;

public enum AuditCategory
{
    Authentication = 1,
    Authorization = 2,
    DataChange = 3,
    SecurityEvent = 4,
    BusinessAction = 5,
    System = 6
}

public enum AuditOutcome { Success = 1, Failure = 2, Warning = 3 }

public class AuditEntry
{
    public long Id { get; set; }
    public int? FacilityId { get; set; }

    public DateTime AtUtc { get; set; } = DateTime.UtcNow;

    public AuditCategory Category { get; set; } = AuditCategory.BusinessAction;
    public AuditOutcome Outcome { get; set; } = AuditOutcome.Success;

    [Required, MaxLength(80)] public string Action { get; set; } = string.Empty;
    [MaxLength(80)] public string? EntityType { get; set; }
    [MaxLength(80)] public string? EntityKey { get; set; }
    [MaxLength(500)] public string? Summary { get; set; }

    public string? ActorUserId { get; set; }
    public ApplicationUser? ActorUser { get; set; }
    public int? ActorPatientId { get; set; }
    [MaxLength(200)] public string? ActorName { get; set; }
    [MaxLength(40)] public string? ActorScheme { get; set; }

    [MaxLength(80)] public string? IpAddress { get; set; }
    [MaxLength(300)] public string? UserAgent { get; set; }
    [MaxLength(80)] public string? CorrelationId { get; set; }

    public string? Metadata { get; set; }
}
