using System.ComponentModel.DataAnnotations;

namespace ThriveHealth.Web.Models.Identity;

public enum FacilityTier
{
    Primary = 1,
    Secondary = 2,
    Tertiary = 3
}

public enum FacilityType
{
    PrimaryHealthCentre = 1,
    Clinic = 2,
    GeneralHospital = 3,
    SpecialistHospital = 4,
    DiagnosticCentre = 5,
    TeachingHospital = 6,
    FederalMedicalCentre = 7
}

public class Facility
{
    public int Id { get; set; }

    /// <summary>Tenant (billable customer) this facility belongs to. Set on every row — query
    /// filters use this to scope all reads, so a tenant can never see another tenant's facilities.</summary>
    public int TenantId { get; set; }
    public ThriveHealth.Web.Models.Tenancy.Tenant? Tenant { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    public FacilityTier Tier { get; set; } = FacilityTier.Secondary;
    public FacilityType Type { get; set; } = FacilityType.GeneralHospital;

    [MaxLength(500)]
    public string? Address { get; set; }

    [MaxLength(100)]
    public string? Lga { get; set; }

    [MaxLength(100)]
    public string? State { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(150)]
    public string? Email { get; set; }

    public int BedCapacity { get; set; }

    [MaxLength(50)]
    public string? RegistrationNumber { get; set; }

    [MaxLength(20)]
    public string? HospitalNumberPrefix { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsActive { get; set; } = true;

    // AI feature flags (per-facility opt-in; cleared by default for compliance)
    public bool AiEnabled { get; set; } = false;
    public bool AiLabInterpretEnabled { get; set; } = false;
    public bool AiDifferentialEnabled { get; set; } = false;
    public bool AiDischargeDraftEnabled { get; set; } = false;
    public bool AiImagingDraftEnabled { get; set; } = false;
    public bool AiTriageAssistEnabled { get; set; } = false;
    public bool AiDrugCheckEnabled { get; set; } = false;
    public bool AiIcdCodingEnabled { get; set; } = false;
    public bool AiNlSearchEnabled { get; set; } = false;
    public bool AiSchedulingAssistEnabled { get; set; } = false;
    public bool AiInventoryForecastEnabled { get; set; } = false;
    public bool AiClaimsRiskEnabled { get; set; } = false;
    public bool AiBillAnomalyEnabled { get; set; } = false;
    public bool AiEcgInterpretEnabled { get; set; } = false;
    public bool AiAncRiskEnabled { get; set; } = false;
    public bool AiPaedsDoseEnabled { get; set; } = false;
    public bool AiIdsrOutbreakEnabled { get; set; } = false;
    public bool AiReferralDraftEnabled { get; set; } = false;
    public bool AiSoapStructureEnabled { get; set; } = false;
    public bool AiMortuaryDraftEnabled { get; set; } = false;
    public bool AiPatientSummaryEnabled { get; set; } = false;
    public bool AiSymptomCheckerEnabled { get; set; } = false;
    public bool AiAdherenceParseEnabled { get; set; } = false;
    public bool AiTranslateEnabled { get; set; } = false;
    public bool AiAuditAnomalyEnabled { get; set; } = false;
    public bool AiDocQualityEnabled { get; set; } = false;

    public ICollection<ApplicationUser> Users { get; set; } = new List<ApplicationUser>();
}
