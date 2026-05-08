using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Maternity;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.BloodBank;

public enum BloodComponent
{
    WholeBlood = 1,
    PackedRedCells = 2,
    FreshFrozenPlasma = 3,
    Platelets = 4,
    Cryoprecipitate = 5,
    AlbumIn = 6
}

public enum BloodUnitStatus
{
    Quarantined = 1,
    Available = 2,
    Reserved = 3,
    CrossMatched = 4,
    Issued = 5,
    Transfused = 6,
    Discarded = 7,
    Expired = 8
}

public enum DonorType { Voluntary = 1, FamilyReplacement = 2, Paid = 3, Autologous = 4 }
public enum DonationStatus { Accepted = 1, Deferred = 2, Rejected = 3 }

public class BloodDonor
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    [Required, MaxLength(40)] public string DonorNumber { get; set; } = string.Empty;
    [Required, MaxLength(100)] public string FullName { get; set; } = string.Empty;
    public DateOnly? DateOfBirth { get; set; }
    [MaxLength(20)] public string? Sex { get; set; }
    [MaxLength(50)] public string? Phone { get; set; }
    [MaxLength(200)] public string? Address { get; set; }

    public BloodGroup BloodGroup { get; set; } = BloodGroup.Unknown;
    public bool? RhesusPositive { get; set; }

    public DonorType DonorType { get; set; } = DonorType.FamilyReplacement;
    public DonationStatus Status { get; set; } = DonationStatus.Accepted;

    public DateOnly? LastDonationDate { get; set; }
    public int TotalDonations { get; set; }

    // Screening
    public bool? HivNegative { get; set; }
    public bool? HepBNegative { get; set; }
    public bool? HepCNegative { get; set; }
    public bool? VdrlNegative { get; set; }
    public bool? MalariaNegative { get; set; }

    [MaxLength(500)] public string? DeferralReason { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public class BloodUnit
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    [Required, MaxLength(40)] public string UnitNumber { get; set; } = string.Empty;

    public int? BloodDonorId { get; set; }
    public BloodDonor? BloodDonor { get; set; }

    public BloodComponent Component { get; set; } = BloodComponent.WholeBlood;
    public BloodGroup BloodGroup { get; set; }
    public bool RhesusPositive { get; set; } = true;

    public DateOnly CollectionDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateOnly ExpiryDate { get; set; }
    public int VolumeMl { get; set; }

    public BloodUnitStatus Status { get; set; } = BloodUnitStatus.Quarantined;

    public bool? HivNegative { get; set; }
    public bool? HepBNegative { get; set; }
    public bool? HepCNegative { get; set; }
    public bool? VdrlNegative { get; set; }
    public bool? MalariaNegative { get; set; }
    public bool ScreeningComplete { get; set; }

    public int? ReservedForPatientId { get; set; }
    public Patient? ReservedForPatient { get; set; }
    public int? CrossMatchId { get; set; }
    public BloodCrossMatch? CrossMatch { get; set; }

    [MaxLength(500)] public string? Notes { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}

public enum CrossMatchStatus { Requested = 1, Compatible = 2, Incompatible = 3, Issued = 4, Cancelled = 5 }

public class BloodCrossMatch
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    [Required, MaxLength(40)] public string CrossMatchNumber { get; set; } = string.Empty;

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public BloodGroup PatientBloodGroup { get; set; }
    public bool PatientRhesusPositive { get; set; } = true;

    public BloodComponent Component { get; set; } = BloodComponent.WholeBlood;
    public int UnitsRequested { get; set; } = 1;
    public DateOnly RequiredBy { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);

    [MaxLength(200)] public string? Indication { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public CrossMatchStatus Status { get; set; } = CrossMatchStatus.Requested;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? IssuedAt { get; set; }

    public string? RequestedById { get; set; }
    public ApplicationUser? RequestedBy { get; set; }

    public string? CompatibilityCheckedById { get; set; }
    public ApplicationUser? CompatibilityCheckedBy { get; set; }

    public ICollection<BloodUnit> Units { get; set; } = new List<BloodUnit>();
}
