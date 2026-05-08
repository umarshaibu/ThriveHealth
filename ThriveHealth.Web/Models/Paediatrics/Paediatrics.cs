using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.Paediatrics;

public enum FeedingType { ExclusiveBreast = 1, Mixed = 2, Formula = 3, ComplementaryFeeding = 4, FamilyDiet = 5 }

public class ChildProfile
{
    public int Id { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public int? MotherPatientId { get; set; }
    public Patient? MotherPatient { get; set; }

    public int? FatherPatientId { get; set; }
    public Patient? FatherPatient { get; set; }

    public int? BirthWeightG { get; set; }
    public decimal? BirthLengthCm { get; set; }
    public decimal? BirthHeadCircCm { get; set; }
    public int? GestationalAgeAtBirthWeeks { get; set; }

    public FeedingType CurrentFeeding { get; set; } = FeedingType.ExclusiveBreast;

    [MaxLength(500)] public string? KnownAllergies { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<GrowthMeasurement> Measurements { get; set; } = new List<GrowthMeasurement>();
}

public class GrowthMeasurement
{
    public int Id { get; set; }
    public int FacilityId { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public DateOnly DateOfMeasurement { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public int AgeMonths { get; set; }

    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public decimal? HeadCircumferenceCm { get; set; }
    public decimal? MuacCm { get; set; }
    public decimal? BmiKgM2 { get; set; }

    [MaxLength(40)] public string? NutritionalStatus { get; set; }
    [MaxLength(60)] public string? DevelopmentalMilestoneNote { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public string? RecordedById { get; set; }
    public ApplicationUser? RecordedBy { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
