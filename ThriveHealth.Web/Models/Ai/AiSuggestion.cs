using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Models.Ai;

public enum AiFeature
{
    LabInterpret = 1,
    Differential = 2,
    DischargeSummary = 3,
    ImagingDraft = 4,
    TriageAssist = 5,
    DrugCheck = 6,
    IcdCoding = 7,
    NlSearch = 8,
    SchedulingAssist = 9,
    InventoryForecast = 10,
    ClaimsRisk = 11,
    BillAnomaly = 12,
    EcgInterpret = 13,
    AncRisk = 14,
    PaedsDose = 15,
    IdsrOutbreak = 16,
    ReferralDraft = 17,
    SoapStructure = 18,
    MortuaryDraft = 19,
    PatientSummary = 20,
    SymptomChecker = 21,
    AdherenceParse = 22,
    Translate = 23,
    AuditAnomaly = 24,
    DocQuality = 25
}

public enum AiSuggestionStatus
{
    Pending = 1,
    Accepted = 2,
    Edited = 3,
    Rejected = 4,
    Failed = 5
}

public class AiSuggestion
{
    public long Id { get; set; }
    public int FacilityId { get; set; }

    public AiFeature Feature { get; set; }
    public AiSuggestionStatus Status { get; set; } = AiSuggestionStatus.Pending;

    [MaxLength(80)] public string? EntityType { get; set; }
    [MaxLength(80)] public string? EntityKey { get; set; }

    [MaxLength(80)] public string Provider { get; set; } = string.Empty;
    [MaxLength(120)] public string Model { get; set; } = string.Empty;

    public string Prompt { get; set; } = string.Empty;
    public string? Response { get; set; }
    public string? EditedContent { get; set; }
    [MaxLength(500)] public string? ErrorMessage { get; set; }

    public int InputTokens { get; set; }
    public int OutputTokens { get; set; }
    public int LatencyMs { get; set; }

    public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ReviewedAtUtc { get; set; }

    public string? RequestedById { get; set; }
    public ApplicationUser? RequestedBy { get; set; }
    public string? ReviewedById { get; set; }
    public ApplicationUser? ReviewedBy { get; set; }
}
