using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Reporting;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Models.ViewModels;

// ---- IDSR ----

public class IdsrListRow
{
    public IdsrCase Case { get; set; } = null!;
    public NotifiableDisease Disease { get; set; } = null!;
}

public class IdsrListViewModel
{
    public IReadOnlyList<IdsrListRow> Rows { get; set; } = Array.Empty<IdsrListRow>();
    public int OpenCount { get; set; }
    public int ImmediateUnnotifiedCount { get; set; }
    public int ConfirmedThisMonthCount { get; set; }
    public int DeathsThisMonthCount { get; set; }
    public int? FilterDiseaseId { get; set; }
    public IReadOnlyList<NotifiableDisease> Diseases { get; set; } = Array.Empty<NotifiableDisease>();
}

public class IdsrCaseInputViewModel
{
    public int? Id { get; set; }
    [Required] public int NotifiableDiseaseId { get; set; }
    public int? PatientId { get; set; }
    public string? PatientLabel { get; set; }
    [Required, MaxLength(120)] public string PatientName { get; set; } = string.Empty;
    public int? AgeYears { get; set; }
    [MaxLength(20)] public string? Sex { get; set; }
    [MaxLength(80)] public string? Lga { get; set; }
    [MaxLength(80)] public string? State { get; set; }
    [MaxLength(200)] public string? Address { get; set; }
    [MaxLength(50)] public string? Phone { get; set; }
    [DataType(DataType.Date)] public DateOnly OnsetDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [DataType(DataType.Date)] public DateOnly ReportDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public CaseClassification Classification { get; set; } = CaseClassification.Suspected;
    [MaxLength(2000)] public string? Symptoms { get; set; }
    [MaxLength(500)] public string? Exposure { get; set; }
    [MaxLength(500)] public string? Vaccinated { get; set; }
    public bool LabSampleCollected { get; set; }
    [DataType(DataType.Date)] public DateOnly? LabSampleDate { get; set; }
    [MaxLength(80)] public string? LabSampleType { get; set; }
    [MaxLength(500)] public string? LabResult { get; set; }
    [MaxLength(500)] public string? Comments { get; set; }
}

public class IdsrCloseViewModel
{
    public int Id { get; set; }
    public CaseOutcome Outcome { get; set; } = CaseOutcome.Recovered;
    [DataType(DataType.Date)] public DateOnly? OutcomeDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [MaxLength(500)] public string? Comments { get; set; }
}

// ---- NHMIS ----

public class NhmisListViewModel
{
    public IReadOnlyList<NhmisReport> Reports { get; set; } = Array.Empty<NhmisReport>();
    public int Year { get; set; } = DateTime.UtcNow.Year;
}

public class NhmisDetailViewModel
{
    public NhmisReport Report { get; set; } = null!;
    public NhmisAggregates Aggregates { get; set; } = null!;
}

public class NhmisSubmitViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string SubmittedToWhom { get; set; } = "LGA M&E Officer";
    [MaxLength(60)] public string? SubmissionReference { get; set; }
}

// ---- Analytics ----

public class AnalyticsSnapshotViewModel
{
    public int Days { get; set; } = 30;
    public int TotalPatients { get; set; }
    public int NewRegistrationsInWindow { get; set; }
    public int OutpatientVisitsInWindow { get; set; }
    public int AdmissionsInWindow { get; set; }
    public int DischargesInWindow { get; set; }
    public int InpatientDeathsInWindow { get; set; }
    public int DeliveriesInWindow { get; set; }
    public int CSectionsInWindow { get; set; }
    public int LabsInWindow { get; set; }
    public int ImmunizationsInWindow { get; set; }
    public decimal RevenueCollectedInWindow { get; set; }
    public decimal OutstandingBalance { get; set; }
    public int OpenIdsrCases { get; set; }

    public IReadOnlyList<DailySeriesPoint> EncountersDaily { get; set; } = Array.Empty<DailySeriesPoint>();
    public IReadOnlyList<DailySeriesPoint> AdmissionsDaily { get; set; } = Array.Empty<DailySeriesPoint>();
    public IReadOnlyList<DailySeriesPoint> RevenueDaily { get; set; } = Array.Empty<DailySeriesPoint>();
    public IReadOnlyDictionary<string, int> PaymentsByMethod { get; set; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> EncountersByClinic { get; set; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> ImmunizationByVaccine { get; set; } = new Dictionary<string, int>();
    public IReadOnlyDictionary<string, int> IdsrByDisease { get; set; } = new Dictionary<string, int>();
}

public record DailySeriesPoint(DateOnly Date, decimal Value);
