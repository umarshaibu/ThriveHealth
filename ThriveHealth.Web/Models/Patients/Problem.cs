using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Models.Patients;

public enum ProblemStatus { Active = 1, Chronic = 2, InRemission = 3, Resolved = 4 }

public class Problem
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    [Required, MaxLength(300)]
    public string Description { get; set; } = string.Empty;

    [MaxLength(20)]
    public string? IcdCode { get; set; }

    [MaxLength(20)]
    public string? SnomedCode { get; set; }

    public ProblemStatus Status { get; set; } = ProblemStatus.Active;

    public DateOnly? OnsetDate { get; set; }
    public DateOnly? ResolutionDate { get; set; }

    [MaxLength(1000)]
    public string? Notes { get; set; }

    public string? RecordedById { get; set; }
    public ApplicationUser? RecordedBy { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
