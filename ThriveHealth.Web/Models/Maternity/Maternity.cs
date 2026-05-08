using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.Maternity;

public enum AnteNatalStatus { Booked = 1, Delivered = 2, LostToFollowUp = 3, Transferred = 4, Aborted = 5 }
public enum HivStatus { Unknown = 0, Negative = 1, Positive = 2, Indeterminate = 3 }
public enum BloodGroup { Unknown = 0, OPos = 1, ONeg = 2, APos = 3, ANeg = 4, BPos = 5, BNeg = 6, ABPos = 7, ABNeg = 8 }

public class AnteNatalRecord
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    [Required, MaxLength(40)] public string AncNumber { get; set; } = string.Empty;
    public DateOnly BookingDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly? Lmp { get; set; }
    public DateOnly? Edd { get; set; }

    public int Gravida { get; set; }
    public int Para { get; set; }
    public int Abortions { get; set; }
    public int LivingChildren { get; set; }

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

    public AnteNatalStatus Status { get; set; } = AnteNatalStatus.Booked;
    [MaxLength(500)] public string? StatusNotes { get; set; }

    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<AnteNatalVisit> Visits { get; set; } = new List<AnteNatalVisit>();
    public ICollection<PostnatalVisit> PostnatalVisits { get; set; } = new List<PostnatalVisit>();
    public ICollection<Delivery> Deliveries { get; set; } = new List<Delivery>();
}

public enum FetalPresentation { Cephalic = 1, Breech = 2, Transverse = 3, Oblique = 4, Compound = 5 }
public enum Lie { Longitudinal = 1, Transverse = 2, Oblique = 3 }

public class AnteNatalVisit
{
    public int Id { get; set; }
    public int AnteNatalRecordId { get; set; }
    public AnteNatalRecord? AnteNatalRecord { get; set; }

    public DateOnly VisitDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public int VisitNumber { get; set; }
    public int? GestationalAgeWeeks { get; set; }

    public decimal? WeightKg { get; set; }
    public int? SystolicBp { get; set; }
    public int? DiastolicBp { get; set; }
    public decimal? FundalHeightCm { get; set; }
    public int? FetalHeartRate { get; set; }
    public FetalPresentation? Presentation { get; set; }
    public Lie? Lie { get; set; }

    public bool? UrineProtein { get; set; }
    public bool? UrineSugar { get; set; }
    public bool? Oedema { get; set; }
    public bool? FetalMovements { get; set; }

    [MaxLength(500)] public string? Complaints { get; set; }
    [MaxLength(500)] public string? Plan { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public string? RecordedById { get; set; }
    public ApplicationUser? RecordedBy { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}

public enum DeliveryMode
{
    SpontaneousVertex = 1,
    AssistedBreech = 2,
    Forceps = 3,
    VacuumExtraction = 4,
    ElectiveCS = 5,
    EmergencyCS = 6,
    Vbac = 7
}

public enum LabourOutcome { LiveBorn = 1, Stillborn = 2, FreshStillBirth = 3, MaceratedStillBirth = 4 }

public class Delivery
{
    public int Id { get; set; }
    public int FacilityId { get; set; }
    public int AnteNatalRecordId { get; set; }
    public AnteNatalRecord? AnteNatalRecord { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public DateTime LabourOnsetUtc { get; set; }
    public DateTime DeliveryUtc { get; set; }
    public int LabourMinutes { get; set; }

    public DeliveryMode Mode { get; set; }
    public LabourOutcome Outcome { get; set; } = LabourOutcome.LiveBorn;
    public int GestationAtDeliveryWeeks { get; set; } = 40;

    public bool EpisiotomyPerformed { get; set; }
    public string? PerinealTear { get; set; }
    public int? EstimatedBloodLossMl { get; set; }
    public bool ActiveMgmtThirdStage { get; set; } = true;
    public bool OxytocinGiven { get; set; } = true;

    [MaxLength(500)] public string? Complications { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public string? AccoucheurId { get; set; }
    public ApplicationUser? Accoucheur { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Newborn> Newborns { get; set; } = new List<Newborn>();
}

public enum NewbornSex { Male = 1, Female = 2, Ambiguous = 3 }

public class Newborn
{
    public int Id { get; set; }
    public int DeliveryId { get; set; }
    public Delivery? Delivery { get; set; }

    public int? PatientId { get; set; }
    public Patient? Patient { get; set; }

    public NewbornSex Sex { get; set; }
    public int? BirthWeightG { get; set; }
    public decimal? LengthCm { get; set; }
    public decimal? HeadCircumferenceCm { get; set; }

    public int? Apgar1Min { get; set; }
    public int? Apgar5Min { get; set; }
    public int? Apgar10Min { get; set; }

    public bool ResuscitationRequired { get; set; }
    public bool BreastfedWithin1Hr { get; set; } = true;
    public bool VitaminKGiven { get; set; } = true;
    public bool BcgGivenAtBirth { get; set; }
    public bool OpvGivenAtBirth { get; set; }
    public bool HepBGivenAtBirth { get; set; }

    [MaxLength(500)] public string? Anomalies { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum PostnatalDay { Day1 = 1, Day3 = 3, Day7 = 7, Day14 = 14, Day42 = 42 }

public class PostnatalVisit
{
    public int Id { get; set; }
    public int AnteNatalRecordId { get; set; }
    public AnteNatalRecord? AnteNatalRecord { get; set; }

    public DateOnly VisitDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public PostnatalDay Day { get; set; } = PostnatalDay.Day1;

    // Mother
    public int? MotherSystolicBp { get; set; }
    public int? MotherDiastolicBp { get; set; }
    public decimal? MotherTemperatureC { get; set; }
    [MaxLength(60)] public string? Lochia { get; set; }
    [MaxLength(60)] public string? FundalInvolution { get; set; }

    // Baby
    public decimal? BabyWeightKg { get; set; }
    public bool BabyJaundice { get; set; }
    public bool BabyBreastfeeding { get; set; } = true;
    public bool CordHealthy { get; set; } = true;

    [MaxLength(500)] public string? Notes { get; set; }
    public string? RecordedById { get; set; }
    public ApplicationUser? RecordedBy { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
