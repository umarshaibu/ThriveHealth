using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Models.Emergency;

public enum TriageColour
{
    Red = 1,
    Orange = 2,
    Yellow = 3,
    Green = 4,
    Blue = 5
}

public enum ArrivalMode { WalkIn = 1, Ambulance = 2, Police = 3, Referred = 4, Air = 5, Other = 9 }
public enum AvpuLevel { Alert = 1, ResponsiveToVoice = 2, ResponsiveToPain = 3, Unresponsive = 4 }

public enum ForensicCategory
{
    None = 0,
    RoadTrafficAccident = 1,
    GunshotWound = 2,
    Stabbing = 3,
    Assault = 4,
    SexualAssault = 5,
    SuicideAttempt = 6,
    Burns = 7,
    Poisoning = 8,
    DomesticViolence = 9,
    ChildAbuse = 10,
    Other = 99
}

public class TriageAssessment
{
    public int Id { get; set; }

    public int EncounterId { get; set; }
    public Encounter? Encounter { get; set; }

    public TriageColour Colour { get; set; } = TriageColour.Yellow;
    public ArrivalMode ArrivalMode { get; set; } = ArrivalMode.WalkIn;

    [Required, MaxLength(500)]
    public string ChiefComplaint { get; set; } = string.Empty;

    public bool IsTrauma { get; set; }
    [MaxLength(500)] public string? MechanismOfInjury { get; set; }

    public AvpuLevel? Avpu { get; set; }
    public int? GcsTotal { get; set; }

    public bool IsPregnant { get; set; }
    public DateTime? LastMealUtc { get; set; }

    public bool IsForensicCase { get; set; }
    public ForensicCategory ForensicCategory { get; set; } = ForensicCategory.None;
    [MaxLength(500)] public string? PoliceReportNumber { get; set; }
    [MaxLength(500)] public string? AccompanyingPerson { get; set; }

    [MaxLength(1000)] public string? KnownAllergies { get; set; }
    [MaxLength(1000)] public string? CurrentMedications { get; set; }

    public DateTime TriagedAt { get; set; } = DateTime.UtcNow;
    public string? TriagedById { get; set; }
    public ApplicationUser? TriagedBy { get; set; }

    public DateTime TargetSeenByUtc { get; set; }

    public static int TargetMinutesFor(TriageColour c) => c switch
    {
        TriageColour.Red => 0,
        TriageColour.Orange => 10,
        TriageColour.Yellow => 60,
        TriageColour.Green => 120,
        TriageColour.Blue => 240,
        _ => 60
    };
}

public class ResusBay
{
    public int Id { get; set; }
    public int FacilityId { get; set; }
    public Facility? Facility { get; set; }

    [Required, MaxLength(50)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;

    public bool IsTraumaBay { get; set; }
    public bool IsActive { get; set; } = true;
}

public enum ResusEventKind
{
    Note = 1,
    DrugGiven = 2,
    Fluid = 3,
    Procedure = 4,
    Defibrillation = 5,
    Intubation = 6,
    Cpr = 7,
    Imaging = 8,
    Lab = 9,
    BloodProduct = 10,
    HandoverIn = 11,
    HandoverOut = 12
}

public class ResusEvent
{
    public int Id { get; set; }

    public int EncounterId { get; set; }
    public Encounter? Encounter { get; set; }

    public ResusEventKind Kind { get; set; } = ResusEventKind.Note;

    [Required, MaxLength(500)] public string Description { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Details { get; set; }

    public DateTime AtUtc { get; set; } = DateTime.UtcNow;
    public string? RecordedById { get; set; }
    public ApplicationUser? RecordedBy { get; set; }
}
