using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Diagnostics;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.ViewModels;

public class LabWorklistRow
{
    public LabOrder Order { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public LabSection? Section { get; set; }
    public LabResultStatus? ResultStatus { get; set; }
    public bool HasCriticalValue { get; set; }
}

public class LabWorklistViewModel
{
    public IReadOnlyList<LabWorklistRow> Rows { get; set; } = Array.Empty<LabWorklistRow>();
    public LabSection? FilterSection { get; set; }
    public string FilterStatus { get; set; } = "open";
    public int OpenCount { get; set; }
    public int AwaitingAuthCount { get; set; }
    public int CriticalCount { get; set; }
}

public class LabResultEntryViewModel
{
    public LabOrder Order { get; set; } = null!;
    public LabTest Test { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public LabResult? ExistingResult { get; set; }
    public string? GeneralComment { get; set; }
    public List<AnalyteEntry> Entries { get; set; } = new();
}

public class AnalyteEntry
{
    public int LabAnalyteId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string? Unit { get; set; }
    public string? RefRange { get; set; }
    public string? CriticalRange { get; set; }
    public string? Value { get; set; }
}

public class LabResultEntrySubmit
{
    public int LabOrderId { get; set; }
    public string? GeneralComment { get; set; }
    public List<AnalyteValueDto> Values { get; set; } = new();
    public bool Finalize { get; set; }
}

public class AnalyteValueDto
{
    public int LabAnalyteId { get; set; }
    public string? Value { get; set; }
}

public class ImagingWorklistRow
{
    public ImagingOrder Order { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public ImagingReport? Report { get; set; }
}

public class ImagingWorklistViewModel
{
    public IReadOnlyList<ImagingWorklistRow> Rows { get; set; } = Array.Empty<ImagingWorklistRow>();
    public ImagingModality? FilterModality { get; set; }
    public string FilterStatus { get; set; } = "open";
    public int OpenCount { get; set; }
    public int AwaitingAuthCount { get; set; }
    public int CriticalCount { get; set; }
}

public class ImagingReportEntryViewModel
{
    public ImagingOrder Order { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public ImagingReport? Report { get; set; }

    [MaxLength(60)] public string? Technique { get; set; }
    [MaxLength(80)] public string? Contrast { get; set; }
    public string? Findings { get; set; }
    public string? Impression { get; set; }
    public string? Recommendation { get; set; }
    [MaxLength(120)] public string? DicomStudyUid { get; set; }
    [MaxLength(500)] public string? DicomViewerUrl { get; set; }
    public bool HasCriticalFinding { get; set; }
    public bool Finalize { get; set; }
}

public class PatientLabHistory
{
    public Patient Patient { get; set; } = null!;
    public IReadOnlyList<LabOrder> Orders { get; set; } = Array.Empty<LabOrder>();
}

public class PatientImagingHistory
{
    public Patient Patient { get; set; } = null!;
    public IReadOnlyList<ImagingOrder> Orders { get; set; } = Array.Empty<ImagingOrder>();
}
