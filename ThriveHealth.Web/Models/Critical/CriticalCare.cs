using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Inpatient;

namespace ThriveHealth.Web.Models.Critical;

public enum VentilationMode
{
    None = 0,
    SpontaneousRoomAir = 1,
    NasalCannula = 2,
    FaceMask = 3,
    NonRebreather = 4,
    BiPap = 5,
    CPap = 6,
    CMV = 10,
    SIMV = 11,
    PSV = 12,
    PCV = 13,
    APRV = 14,
    HighFrequency = 15
}

public enum SedationLevel
{
    Awake = 1,
    LightSedation = 2,
    DeepSedation = 3,
    Paralysed = 4
}

public class IcuChartEntry
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    public int AdmissionId { get; set; }
    public Admission? Admission { get; set; }

    public DateTime RecordedUtc { get; set; } = DateTime.UtcNow;

    // Vitals
    public int? HeartRate { get; set; }
    public int? SystolicBp { get; set; }
    public int? DiastolicBp { get; set; }
    public int? MeanArterialPressure { get; set; }
    public int? RespiratoryRate { get; set; }
    public decimal? SpO2 { get; set; }
    public decimal? TemperatureC { get; set; }
    public int? GcsEye { get; set; }
    public int? GcsVerbal { get; set; }
    public int? GcsMotor { get; set; }

    // Pain / Sedation / Pupils
    public int? PainScore { get; set; }
    public SedationLevel? Sedation { get; set; }
    [MaxLength(40)] public string? Pupils { get; set; }

    // Fluids
    public int? UrineOutputMl { get; set; }
    public int? CrystalloidGivenMl { get; set; }
    public int? BloodGivenMl { get; set; }

    // Vent
    public VentilationMode? VentMode { get; set; }
    public decimal? FiO2 { get; set; }
    public int? Peep { get; set; }
    public int? TidalVolumeMl { get; set; }
    public int? VentRate { get; set; }
    public int? PeakInspiratoryPressure { get; set; }

    // Drips (text)
    [MaxLength(500)] public string? Inotropes { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public string? RecordedById { get; set; }
    public ApplicationUser? RecordedBy { get; set; }

    public int GcsTotal => (GcsEye ?? 0) + (GcsVerbal ?? 0) + (GcsMotor ?? 0);
}

public enum DialysisModality { Haemodialysis = 1, PeritonealDialysis = 2, CRRT = 3 }
public enum VascularAccess { CentralLine = 1, AvFistula = 2, AvGraft = 3, PdCatheter = 4, FemoralLine = 5 }

public class DialysisSession
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    public int? AdmissionId { get; set; }
    public Admission? Admission { get; set; }

    public int PatientId { get; set; }

    [Required, MaxLength(40)] public string SessionNumber { get; set; } = string.Empty;
    public DialysisModality Modality { get; set; } = DialysisModality.Haemodialysis;
    public VascularAccess Access { get; set; } = VascularAccess.AvFistula;

    public DateTime StartUtc { get; set; }
    public DateTime? EndUtc { get; set; }
    public int DurationMinutes { get; set; }

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

    public string? OperatorId { get; set; }
    public ApplicationUser? Operator { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
