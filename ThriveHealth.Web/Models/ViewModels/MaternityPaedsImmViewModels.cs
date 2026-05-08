using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Immunization;
using ThriveHealth.Web.Models.Maternity;
using ThriveHealth.Web.Models.Paediatrics;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.ViewModels;

// ---- Maternity ----

public class AncListRow
{
    public AnteNatalRecord Record { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public int? GestationalAgeWeeks { get; set; }
    public int VisitsCount { get; set; }
    public bool IsHighRisk { get; set; }
}

public class AncListViewModel
{
    public IReadOnlyList<AncListRow> Rows { get; set; } = Array.Empty<AncListRow>();
    public AnteNatalStatus? FilterStatus { get; set; }
    public string? Search { get; set; }
    public int ActiveCount { get; set; }
    public int HighRiskCount { get; set; }
    public int DueThisWeekCount { get; set; }
}

public class AncBookViewModel
{
    public int? PatientId { get; set; }
    public string? PatientLabel { get; set; }

    [DataType(DataType.Date)] public DateOnly? Lmp { get; set; }
    [DataType(DataType.Date)] public DateOnly? Edd { get; set; }
    [Range(0, 20)] public int Gravida { get; set; } = 1;
    [Range(0, 20)] public int Para { get; set; } = 0;
    [Range(0, 20)] public int Abortions { get; set; } = 0;
    [Range(0, 20)] public int LivingChildren { get; set; } = 0;

    public BloodGroup BloodGroup { get; set; } = BloodGroup.Unknown;
    public bool? RhesusPositive { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? BookingWeightKg { get; set; }
    public decimal? HaemoglobinGdl { get; set; }
    public HivStatus HivStatus { get; set; } = HivStatus.Unknown;
    public bool? VdrlReactive { get; set; }
    public bool? HepBPositive { get; set; }
    public bool? SicklingPositive { get; set; }

    [MaxLength(500)] public string? RiskFactors { get; set; }
    [MaxLength(500)] public string? PreviousObstetricHistory { get; set; }
    [MaxLength(500)] public string? MedicalHistory { get; set; }
}

public class AncVisitInputViewModel
{
    public int AnteNatalRecordId { get; set; }
    [DataType(DataType.Date)] public DateOnly VisitDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public int VisitNumber { get; set; }
    public int? GestationalAgeWeeks { get; set; }
    public decimal? WeightKg { get; set; }
    public int? SystolicBp { get; set; }
    public int? DiastolicBp { get; set; }
    public decimal? FundalHeightCm { get; set; }
    public int? FetalHeartRate { get; set; }
    public FetalPresentation? Presentation { get; set; }
    public bool? UrineProtein { get; set; }
    public bool? UrineSugar { get; set; }
    public bool? Oedema { get; set; }
    public bool? FetalMovements { get; set; }
    [MaxLength(500)] public string? Complaints { get; set; }
    [MaxLength(500)] public string? Plan { get; set; }
}

public class DeliveryInputViewModel
{
    public int AnteNatalRecordId { get; set; }
    [DataType(DataType.DateTime)] public DateTime LabourOnsetUtc { get; set; } = DateTime.UtcNow.AddHours(-6);
    [DataType(DataType.DateTime)] public DateTime DeliveryUtc { get; set; } = DateTime.UtcNow;
    public DeliveryMode Mode { get; set; } = DeliveryMode.SpontaneousVertex;
    public LabourOutcome Outcome { get; set; } = LabourOutcome.LiveBorn;
    [Range(20, 45)] public int GestationAtDeliveryWeeks { get; set; } = 40;
    public bool EpisiotomyPerformed { get; set; }
    [MaxLength(60)] public string? PerinealTear { get; set; }
    public int? EstimatedBloodLossMl { get; set; }
    [MaxLength(500)] public string? Complications { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
    public string? AccoucheurId { get; set; }

    public NewbornSex BabySex { get; set; } = NewbornSex.Female;
    public int? BirthWeightG { get; set; }
    public decimal? LengthCm { get; set; }
    public decimal? HeadCircumferenceCm { get; set; }
    public int? Apgar1Min { get; set; }
    public int? Apgar5Min { get; set; }
    public bool ResuscitationRequired { get; set; }
    public bool BreastfedWithin1Hr { get; set; } = true;
    public bool VitaminKGiven { get; set; } = true;
    public bool BcgGivenAtBirth { get; set; }
    public bool OpvGivenAtBirth { get; set; }
    public bool HepBGivenAtBirth { get; set; }
}

public class PostnatalInputViewModel
{
    public int AnteNatalRecordId { get; set; }
    [DataType(DataType.Date)] public DateOnly VisitDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public PostnatalDay Day { get; set; } = PostnatalDay.Day1;
    public int? MotherSystolicBp { get; set; }
    public int? MotherDiastolicBp { get; set; }
    public decimal? MotherTemperatureC { get; set; }
    [MaxLength(60)] public string? Lochia { get; set; }
    [MaxLength(60)] public string? FundalInvolution { get; set; }
    public decimal? BabyWeightKg { get; set; }
    public bool BabyJaundice { get; set; }
    public bool BabyBreastfeeding { get; set; } = true;
    public bool CordHealthy { get; set; } = true;
    [MaxLength(500)] public string? Notes { get; set; }
}

// ---- Paediatrics ----

public class ChildListRow
{
    public Patient Patient { get; set; } = null!;
    public ChildProfile? Profile { get; set; }
    public GrowthMeasurement? LastMeasurement { get; set; }
    public int? AgeMonths { get; set; }
    public int OverdueDoses { get; set; }
}

public class ChildListViewModel
{
    public IReadOnlyList<ChildListRow> Rows { get; set; } = Array.Empty<ChildListRow>();
    public string? Search { get; set; }
    public int Under5Count { get; set; }
    public int Under1Count { get; set; }
    public int OverdueCount { get; set; }
}

public class GrowthInputViewModel
{
    public int PatientId { get; set; }
    [DataType(DataType.Date)] public DateOnly DateOfMeasurement { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public int AgeMonths { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? HeadCircumferenceCm { get; set; }
    public decimal? MuacCm { get; set; }
    [MaxLength(60)] public string? DevelopmentalMilestoneNote { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

public class ChildProfileInputViewModel
{
    public int PatientId { get; set; }
    public int? MotherPatientId { get; set; }
    public string? MotherLabel { get; set; }
    public int? FatherPatientId { get; set; }
    public string? FatherLabel { get; set; }
    public int? BirthWeightG { get; set; }
    public decimal? BirthLengthCm { get; set; }
    public decimal? BirthHeadCircCm { get; set; }
    public int? GestationalAgeAtBirthWeeks { get; set; }
    public FeedingType CurrentFeeding { get; set; } = FeedingType.ExclusiveBreast;
    [MaxLength(500)] public string? KnownAllergies { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

// ---- Immunization ----

public class ImmunizationCardViewModel
{
    public Patient Patient { get; set; } = null!;
    public DateOnly DateOfBirth { get; set; }
    public IReadOnlyList<Services.ImmunizationCardRow> Rows { get; set; } = Array.Empty<Services.ImmunizationCardRow>();
    public int AdministeredCount { get; set; }
    public int DueOrOverdueCount { get; set; }
}

public class AdministerDoseViewModel
{
    public int DoseId { get; set; }
    public int PatientId { get; set; }
    [MaxLength(40)] public string? BatchNumber { get; set; }
    [DataType(DataType.Date)] public DateOnly? ExpiryDate { get; set; }
    [MaxLength(40)] public string? Site { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

public class ImmunizationWorklistRow
{
    public ImmunizationDose Dose { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public int DaysOverdue { get; set; }
}

public class ImmunizationWorklistViewModel
{
    public IReadOnlyList<ImmunizationWorklistRow> Rows { get; set; } = Array.Empty<ImmunizationWorklistRow>();
    public int DueTodayCount { get; set; }
    public int OverdueCount { get; set; }
    public int AdministeredTodayCount { get; set; }
}
