using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Models.Diagnostics;

public enum LabResultStatus
{
    Preliminary = 1,
    Final = 2,
    Authorized = 3,
    Amended = 4,
    Cancelled = 5
}

public class LabResult
{
    public int Id { get; set; }

    public int LabOrderId { get; set; }
    public LabOrder? LabOrder { get; set; }

    public int LabTestId { get; set; }
    public LabTest? LabTest { get; set; }

    public LabResultStatus Status { get; set; } = LabResultStatus.Preliminary;

    [MaxLength(2000)] public string? GeneralComment { get; set; }
    [MaxLength(2000)] public string? Methodology { get; set; }

    public DateTime EnteredAt { get; set; } = DateTime.UtcNow;
    public string? EnteredById { get; set; }
    public ApplicationUser? EnteredBy { get; set; }

    public DateTime? AuthorizedAt { get; set; }
    public string? AuthorizedById { get; set; }
    public ApplicationUser? AuthorizedBy { get; set; }

    public bool HasCriticalValue { get; set; }
    public bool CriticalNotified { get; set; }
    public DateTime? CriticalNotifiedAt { get; set; }

    public ICollection<LabResultValue> Values { get; set; } = new List<LabResultValue>();
}

public enum AnalyteFlag
{
    Normal = 0,
    Low = 1,
    High = 2,
    CriticalLow = 3,
    CriticalHigh = 4,
    Abnormal = 5
}

public class LabResultValue
{
    public int Id { get; set; }

    public int LabResultId { get; set; }
    public LabResult? LabResult { get; set; }

    public int LabAnalyteId { get; set; }
    public LabAnalyte? LabAnalyte { get; set; }

    [MaxLength(80)] public string AnalyteName { get; set; } = string.Empty;
    [MaxLength(20)] public string? Unit { get; set; }

    [Required, MaxLength(80)] public string Value { get; set; } = string.Empty;
    public decimal? NumericValue { get; set; }

    public AnalyteFlag Flag { get; set; } = AnalyteFlag.Normal;

    [MaxLength(40)] public string? RefRangeDisplay { get; set; }
    [MaxLength(300)] public string? Note { get; set; }
}

public class ImagingReport
{
    public int Id { get; set; }

    public int ImagingOrderId { get; set; }
    public ImagingOrder? ImagingOrder { get; set; }

    [MaxLength(80)] public string? AccessionNumber { get; set; }

    [MaxLength(60)] public string? Technique { get; set; }
    [MaxLength(80)] public string? Contrast { get; set; }
    [MaxLength(120)] public string? DicomStudyUid { get; set; }
    [MaxLength(500)] public string? DicomViewerUrl { get; set; }

    public string? Findings { get; set; }
    public string? Impression { get; set; }
    public string? Recommendation { get; set; }

    public bool HasCriticalFinding { get; set; }

    public DateTime? PerformedAt { get; set; }
    public string? PerformedById { get; set; }
    public ApplicationUser? PerformedBy { get; set; }

    public DateTime? ReportedAt { get; set; }
    public string? ReportedById { get; set; }
    public ApplicationUser? ReportedBy { get; set; }

    public DateTime? AuthorizedAt { get; set; }
    public string? AuthorizedById { get; set; }
    public ApplicationUser? AuthorizedBy { get; set; }

    public bool IsAuthorized => AuthorizedAt.HasValue;
}
