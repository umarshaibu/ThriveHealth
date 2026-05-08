using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Allied;
using ThriveHealth.Web.Models.BloodBank;
using ThriveHealth.Web.Models.Critical;
using ThriveHealth.Web.Models.Inpatient;
using ThriveHealth.Web.Models.Maternity;
using ThriveHealth.Web.Models.Mortuary;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.ViewModels;

// ---- ICU ----

public class IcuBoardRow
{
    public Admission Admission { get; set; } = null!;
    public IcuChartEntry? Latest { get; set; }
    public int EntriesLast24h { get; set; }
}

public class IcuBoardViewModel
{
    public IReadOnlyList<IcuBoardRow> Rows { get; set; } = Array.Empty<IcuBoardRow>();
    public int IcuCount { get; set; }
    public int OnVentCount { get; set; }
    public int CriticalCount { get; set; }
}

public class IcuChartInputViewModel
{
    public int AdmissionId { get; set; }
    [DataType(DataType.DateTime)] public DateTime RecordedUtc { get; set; } = DateTime.UtcNow;
    public int? HeartRate { get; set; }
    public int? SystolicBp { get; set; }
    public int? DiastolicBp { get; set; }
    public int? RespiratoryRate { get; set; }
    public decimal? SpO2 { get; set; }
    public decimal? TemperatureC { get; set; }
    public int? GcsEye { get; set; }
    public int? GcsVerbal { get; set; }
    public int? GcsMotor { get; set; }
    public int? PainScore { get; set; }
    public SedationLevel? Sedation { get; set; }
    public int? UrineOutputMl { get; set; }
    public int? CrystalloidGivenMl { get; set; }
    public int? BloodGivenMl { get; set; }
    public VentilationMode? VentMode { get; set; }
    public decimal? FiO2 { get; set; }
    public int? Peep { get; set; }
    public int? TidalVolumeMl { get; set; }
    public int? VentRate { get; set; }
    [MaxLength(500)] public string? Inotropes { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

// ---- Dialysis ----

public class DialysisListRow
{
    public DialysisSession Session { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
}

public class DialysisListViewModel
{
    public IReadOnlyList<DialysisListRow> Rows { get; set; } = Array.Empty<DialysisListRow>();
    public int RunningCount { get; set; }
    public int CompletedTodayCount { get; set; }
    public int ThisMonthCount { get; set; }
}

public class DialysisInputViewModel
{
    public int? Id { get; set; }
    public int? PatientId { get; set; }
    public string? PatientLabel { get; set; }
    public int? AdmissionId { get; set; }
    public DialysisModality Modality { get; set; } = DialysisModality.Haemodialysis;
    public VascularAccess Access { get; set; } = VascularAccess.AvFistula;
    [DataType(DataType.DateTime)] public DateTime StartUtc { get; set; } = DateTime.UtcNow;
    [DataType(DataType.DateTime)] public DateTime? EndUtc { get; set; }
    [Range(0, 1440)] public int DurationMinutes { get; set; } = 240;
    public decimal? PreWeightKg { get; set; }
    public decimal? PostWeightKg { get; set; }
    public int? UfTargetMl { get; set; }
    public int? UfAchievedMl { get; set; }
    public int? PreSystolicBp { get; set; }
    public int? PreDiastolicBp { get; set; }
    public int? PostSystolicBp { get; set; }
    public int? PostDiastolicBp { get; set; }
    public decimal? BloodFlowMlMin { get; set; }
    public decimal? DialysateFlowMlMin { get; set; }
    public decimal? HeparinUnits { get; set; }
    [MaxLength(60)] public string? DialyserType { get; set; }
    [MaxLength(500)] public string? Complications { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

// ---- Blood Bank ----

public class BloodBankDashboardViewModel
{
    public IReadOnlyDictionary<(BloodGroup Group, BloodComponent Component), int> Inventory { get; set; }
        = new Dictionary<(BloodGroup, BloodComponent), int>();
    public int TotalAvailable { get; set; }
    public int Reserved { get; set; }
    public int ExpiringIn7Days { get; set; }
    public int OpenCrossMatches { get; set; }
    public IReadOnlyList<BloodUnit> RecentUnits { get; set; } = Array.Empty<BloodUnit>();
    public IReadOnlyList<BloodCrossMatch> OpenRequests { get; set; } = Array.Empty<BloodCrossMatch>();
}

public class BloodDonorInputViewModel
{
    public int? Id { get; set; }
    [Required, MaxLength(100)] public string FullName { get; set; } = string.Empty;
    [DataType(DataType.Date)] public DateOnly? DateOfBirth { get; set; }
    [MaxLength(20)] public string? Sex { get; set; }
    [MaxLength(50)] public string? Phone { get; set; }
    [MaxLength(200)] public string? Address { get; set; }
    public BloodGroup BloodGroup { get; set; } = BloodGroup.Unknown;
    public bool? RhesusPositive { get; set; } = true;
    public DonorType DonorType { get; set; } = DonorType.FamilyReplacement;
    public DonationStatus Status { get; set; } = DonationStatus.Accepted;
    public bool? HivNegative { get; set; }
    public bool? HepBNegative { get; set; }
    public bool? HepCNegative { get; set; }
    public bool? VdrlNegative { get; set; }
    public bool? MalariaNegative { get; set; }
    [MaxLength(500)] public string? DeferralReason { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

public class BloodUnitInputViewModel
{
    public int? Id { get; set; }
    public int? BloodDonorId { get; set; }
    public string? DonorLabel { get; set; }
    public BloodComponent Component { get; set; } = BloodComponent.WholeBlood;
    public BloodGroup BloodGroup { get; set; } = BloodGroup.OPos;
    public bool RhesusPositive { get; set; } = true;
    [DataType(DataType.Date)] public DateOnly CollectionDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [DataType(DataType.Date)] public DateOnly ExpiryDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(35));
    public int VolumeMl { get; set; } = 450;
    public bool HivNegative { get; set; }
    public bool HepBNegative { get; set; }
    public bool HepCNegative { get; set; }
    public bool VdrlNegative { get; set; }
    public bool MalariaNegative { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

public class CrossMatchInputViewModel
{
    public int? PatientId { get; set; }
    public string? PatientLabel { get; set; }
    public BloodGroup PatientBloodGroup { get; set; } = BloodGroup.Unknown;
    public bool PatientRhesusPositive { get; set; } = true;
    public BloodComponent Component { get; set; } = BloodComponent.WholeBlood;
    [Range(1, 20)] public int UnitsRequested { get; set; } = 1;
    [DataType(DataType.Date)] public DateOnly RequiredBy { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [MaxLength(200)] public string? Indication { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

// ---- Mortuary ----

public class MortuaryListRow
{
    public MortuaryEntry Entry { get; set; } = null!;
}

public class MortuaryListViewModel
{
    public IReadOnlyList<MortuaryEntry> Entries { get; set; } = Array.Empty<MortuaryEntry>();
    public MortuaryStatus? FilterStatus { get; set; }
    public int CurrentBodies { get; set; }
    public int ReleasedThisMonth { get; set; }
    public int OverdueLengthOfStay { get; set; }
}

public class MortuaryInputViewModel
{
    public int? Id { get; set; }
    public int? PatientId { get; set; }
    public string? PatientLabel { get; set; }
    public bool IsUnidentified { get; set; }
    [Required, MaxLength(120)] public string DeceasedName { get; set; } = string.Empty;
    [MaxLength(20)] public string? Sex { get; set; }
    [DataType(DataType.Date)] public DateOnly? DateOfBirth { get; set; }
    public int? AgeYears { get; set; }
    [MaxLength(120)] public string? Tribe { get; set; }
    [MaxLength(200)] public string? AddressOfOrigin { get; set; }
    [DataType(DataType.DateTime)] public DateTime DateOfDeathUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(200)] public string? PlaceOfDeath { get; set; }
    [MaxLength(200)] public string? CauseOfDeath { get; set; }
    public MannerOfDeath? Manner { get; set; }
    [MaxLength(20)] public string? CabinetCode { get; set; }
    [Required, MaxLength(120)] public string NextOfKinName { get; set; } = string.Empty;
    [MaxLength(60)] public string? NextOfKinRelationship { get; set; }
    [MaxLength(50)] public string? NextOfKinPhone { get; set; }
    [MaxLength(40)] public string? NextOfKinId { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

public class MortuaryReleaseViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(120)] public string ReleasedTo { get; set; } = string.Empty;
    [MaxLength(40)] public string? ReleasedToId { get; set; }
    [MaxLength(60)] public string? ReleaseAuthorityRef { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

// ---- Allied ----

public class AlliedListRow
{
    public AlliedSession Session { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
}

public class AlliedListViewModel
{
    public IReadOnlyList<AlliedListRow> Rows { get; set; } = Array.Empty<AlliedListRow>();
    public AlliedServiceLine ServiceLine { get; set; } = AlliedServiceLine.Dental;
    public int ScheduledCount { get; set; }
    public int CompletedTodayCount { get; set; }
    public int InProgressCount { get; set; }
}

public class AlliedSessionInputViewModel
{
    public int? Id { get; set; }
    public AlliedServiceLine ServiceLine { get; set; } = AlliedServiceLine.Dental;
    public int? PatientId { get; set; }
    public string? PatientLabel { get; set; }
    [DataType(DataType.DateTime)] public DateTime ScheduledUtc { get; set; } = DateTime.UtcNow;
    [MaxLength(500)] public string? ChiefComplaint { get; set; }
    [MaxLength(2000)] public string? Examination { get; set; }
    [MaxLength(2000)] public string? Assessment { get; set; }
    [MaxLength(2000)] public string? TreatmentGiven { get; set; }
    [MaxLength(1000)] public string? Plan { get; set; }
    [MaxLength(500)] public string? Modality { get; set; }
    [MaxLength(200)] public string? ToothChart { get; set; }
    [MaxLength(40)] public string? DentalProcedureCode { get; set; }
    [MaxLength(20)] public string? RightEyeAcuity { get; set; }
    [MaxLength(20)] public string? LeftEyeAcuity { get; set; }
    [MaxLength(60)] public string? RightEyeRefraction { get; set; }
    [MaxLength(60)] public string? LeftEyeRefraction { get; set; }
    public int? SessionsCompleted { get; set; }
    public int? SessionsPlanned { get; set; }
    [MaxLength(200)] public string? PhysioModalitiesUsed { get; set; }
    public string? ProviderId { get; set; }
}
