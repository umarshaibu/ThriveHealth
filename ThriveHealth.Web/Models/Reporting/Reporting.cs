using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.Reporting;

public enum NotificationWindow { Immediate = 1, Weekly = 2, Monthly = 3 }
public enum DiseaseCategory { EpidemicProne = 1, OutbreakProne = 2, EliminationTarget = 3, EradicationTarget = 4, OtherPriority = 5 }

public class NotifiableDisease
{
    public int Id { get; set; }

    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(120)] public string Name { get; set; } = string.Empty;
    [MaxLength(200)] public string? CaseDefinition { get; set; }
    public DiseaseCategory Category { get; set; } = DiseaseCategory.OutbreakProne;
    public NotificationWindow Window { get; set; } = NotificationWindow.Weekly;
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }
}

public enum CaseClassification { Suspected = 1, Probable = 2, Confirmed = 3, Discarded = 4 }
public enum CaseOutcome { Unknown = 1, Alive = 2, Recovered = 3, Died = 4, LostToFollowUp = 5, Transferred = 6 }
public enum IdsrCaseStatus { Open = 1, Closed = 2, Discarded = 3 }

public class IdsrCase
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    [Required, MaxLength(40)] public string CaseNumber { get; set; } = string.Empty;

    public int NotifiableDiseaseId { get; set; }
    public NotifiableDisease? NotifiableDisease { get; set; }

    public int? PatientId { get; set; }
    public Patient? Patient { get; set; }

    [Required, MaxLength(120)] public string PatientName { get; set; } = string.Empty;
    public int? AgeYears { get; set; }
    [MaxLength(20)] public string? Sex { get; set; }
    [MaxLength(80)] public string? Lga { get; set; }
    [MaxLength(80)] public string? State { get; set; }
    [MaxLength(200)] public string? Address { get; set; }
    [MaxLength(50)] public string? Phone { get; set; }

    public DateOnly OnsetDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly ReportDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    public CaseClassification Classification { get; set; } = CaseClassification.Suspected;
    public CaseOutcome Outcome { get; set; } = CaseOutcome.Unknown;
    public IdsrCaseStatus Status { get; set; } = IdsrCaseStatus.Open;

    [MaxLength(2000)] public string? Symptoms { get; set; }
    [MaxLength(500)] public string? Exposure { get; set; }
    [MaxLength(500)] public string? Vaccinated { get; set; }

    public bool LabSampleCollected { get; set; }
    public DateOnly? LabSampleDate { get; set; }
    [MaxLength(80)] public string? LabSampleType { get; set; }
    [MaxLength(500)] public string? LabResult { get; set; }

    public DateOnly? OutcomeDate { get; set; }
    public bool NotifiedNcdc { get; set; }
    public DateTime? NotifiedNcdcAt { get; set; }
    [MaxLength(500)] public string? Comments { get; set; }

    public string? ReportedById { get; set; }
    public ApplicationUser? ReportedBy { get; set; }
    public DateTime ReportedAt { get; set; } = DateTime.UtcNow;
}

public enum NhmisReportStatus { Draft = 1, Generated = 2, Submitted = 3 }

public class NhmisReport
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    public int Year { get; set; }
    public int Month { get; set; }

    [Required, MaxLength(20)] public string Period { get; set; } = string.Empty; // yyyy-MM
    public NhmisReportStatus Status { get; set; } = NhmisReportStatus.Draft;

    public DateTime GeneratedAt { get; set; } = DateTime.UtcNow;
    public DateTime? SubmittedAt { get; set; }
    [MaxLength(120)] public string? SubmittedToWhom { get; set; }
    [MaxLength(60)] public string? SubmissionReference { get; set; }

    [Required] public string AggregatesJson { get; set; } = "{}";

    public string? GeneratedById { get; set; }
    public ApplicationUser? GeneratedBy { get; set; }

    public string? SubmittedById { get; set; }
    public ApplicationUser? SubmittedBy { get; set; }
}
