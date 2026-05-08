using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Patients;
using ThriveHealth.Web.Models.Telemedicine;

namespace ThriveHealth.Web.Models.Portal;

public class PortalAccount
{
    public int Id { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    [Required, MaxLength(150)] public string Email { get; set; } = string.Empty;
    [MaxLength(50)] public string? Phone { get; set; }
    [Required, MaxLength(500)] public string PasswordHash { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
    public bool EmailVerified { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }

    [MaxLength(80)] public string? LastLoginIp { get; set; }
}

public enum SymptomSeverity { Mild = 1, Moderate = 2, Severe = 3 }

public class PortalSymptomIntake
{
    public int Id { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public int? TeleSessionId { get; set; }
    public TeleSession? TeleSession { get; set; }

    [Required, MaxLength(500)] public string ChiefComplaint { get; set; } = string.Empty;
    [MaxLength(1500)] public string? Symptoms { get; set; }
    public int? DurationDays { get; set; }
    public SymptomSeverity Severity { get; set; } = SymptomSeverity.Mild;
    [MaxLength(500)] public string? CurrentMedications { get; set; }
    [MaxLength(500)] public string? KnownAllergies { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public DateTime SubmittedAt { get; set; } = DateTime.UtcNow;
}
