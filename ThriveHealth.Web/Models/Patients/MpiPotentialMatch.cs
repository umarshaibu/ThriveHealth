using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Models.Patients;

public enum MpiMatchStatus { Pending = 1, Merged = 2, Dismissed = 3 }

public class MpiPotentialMatch
{
    public int Id { get; set; }

    public int PatientAId { get; set; }
    public Patient? PatientA { get; set; }

    public int PatientBId { get; set; }
    public Patient? PatientB { get; set; }

    public decimal ConfidenceScore { get; set; }

    public MpiMatchStatus Status { get; set; } = MpiMatchStatus.Pending;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ResolvedAt { get; set; }

    public string? ResolvedById { get; set; }
    public ApplicationUser? ResolvedBy { get; set; }

    public string? Notes { get; set; }
}

public class MpiMergeAudit
{
    public int Id { get; set; }

    public int MasterPatientId { get; set; }
    public Patient? MasterPatient { get; set; }

    public int MergedFromPatientId { get; set; }

    public string? MergedById { get; set; }
    public ApplicationUser? MergedBy { get; set; }
    public DateTime MergedAt { get; set; } = DateTime.UtcNow;

    public string? ReversedById { get; set; }
    public DateTime? ReversedAt { get; set; }

    public string? Reason { get; set; }
}
