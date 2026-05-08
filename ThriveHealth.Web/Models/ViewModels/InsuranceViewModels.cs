using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Insurance;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.ViewModels;

public class PayerEditViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    public PayerOrgType OrgType { get; set; } = PayerOrgType.Hmo;
    [MaxLength(300)] public string? Address { get; set; }
    [MaxLength(50)] public string? Phone { get; set; }
    [MaxLength(150)] public string? Email { get; set; }
    [MaxLength(150), Display(Name = "Claims dispatch email")] public string? ClaimsDispatchEmail { get; set; }
    [MaxLength(20), Display(Name = "Regulator registration #")] public string? RegulatorRegistrationNumber { get; set; }
    public bool IsActive { get; set; } = true;
}

public class PayerPlanEditViewModel
{
    public int Id { get; set; }
    public int PayerId { get; set; }
    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    [Range(0, 5), Display(Name = "Tariff multiplier (× facility list)")] public decimal TariffMultiplier { get; set; } = 1m;
    [Range(0, 100), Display(Name = "Default copay %")] public decimal DefaultCopayPercent { get; set; }
    [Range(0, 100000), Display(Name = "Capitation rate (₦/enrollee/month)")] public decimal CapitationRatePerEnrolleeMonth { get; set; }
    [Display(Name = "Requires pre-authorization")] public bool RequiresPreAuthorization { get; set; }
    [Display(Name = "Default formulary covered")] public bool DefaultFormularyCovered { get; set; } = true;
    public bool IsActive { get; set; } = true;
}

public class ClaimsWorklistRow
{
    public Claim Claim { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
}

public class ClaimsWorklistViewModel
{
    public IReadOnlyList<ClaimsWorklistRow> Rows { get; set; } = Array.Empty<ClaimsWorklistRow>();
    public int? FilterPayerId { get; set; }
    public ClaimStatus? FilterStatus { get; set; }
    public IReadOnlyList<Payer> Payers { get; set; } = Array.Empty<Payer>();

    public int DraftCount { get; set; }
    public int SubmittedCount { get; set; }
    public int OutstandingCount { get; set; }
    public decimal OutstandingAmount { get; set; }
    public decimal PaidThisMonth { get; set; }
}

public class ClaimDetailViewModel
{
    public Claim Claim { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
}

public class BuildClaimViewModel
{
    public int EncounterId { get; set; }
    public int? PayerId { get; set; }
    public int? PayerPlanId { get; set; }
    public IReadOnlyList<Payer> Payers { get; set; } = Array.Empty<Payer>();
    public string PatientName { get; set; } = string.Empty;
    public string EncounterSummary { get; set; } = string.Empty;
    public int? SuggestedPayerId { get; set; }
    public int? SuggestedPayerPlanId { get; set; }
}

public class ClaimSettleViewModel
{
    public int ClaimId { get; set; }
    [Range(0, 100000000)] public decimal ApprovedAmount { get; set; }
    [Range(0, 100000000)] public decimal PaidAmount { get; set; }
    [MaxLength(40), Display(Name = "Payer reference / payment advice #")] public string? PayerReference { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

public class ClaimDenyViewModel
{
    public int ClaimId { get; set; }
    [Required, MaxLength(500), Display(Name = "Denial reason")] public string Reason { get; set; } = string.Empty;
    [MaxLength(500)] public string? Notes { get; set; }
}
