using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;
using ThriveHealth.Web.Models.Pharmacy;

namespace ThriveHealth.Web.Models.Insurance;

public enum PayerOrgType
{
    Nhia = 1,
    Hmo = 2,
    StateInsurance = 3,
    Corporate = 4,
    Donor = 5,
    Bhcpf = 6,
    OutOfPocket = 7
}

public class Payer
{
    public int Id { get; set; }

    public PayerOrgType OrgType { get; set; } = PayerOrgType.Hmo;

    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;

    [MaxLength(300)] public string? Address { get; set; }
    [MaxLength(50)] public string? Phone { get; set; }
    [MaxLength(150)] public string? Email { get; set; }
    [MaxLength(150)] public string? ClaimsDispatchEmail { get; set; }
    [MaxLength(20)] public string? RegulatorRegistrationNumber { get; set; }

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<PayerPlan> Plans { get; set; } = new List<PayerPlan>();
}

public class PayerPlan
{
    public int Id { get; set; }
    public int PayerId { get; set; }
    public Payer? Payer { get; set; }

    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;

    public decimal TariffMultiplier { get; set; } = 1.0m;
    public decimal CapitationRatePerEnrolleeMonth { get; set; }
    public bool RequiresPreAuthorization { get; set; }

    public bool DefaultFormularyCovered { get; set; } = true;
    public decimal DefaultCopayPercent { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<PayerFormulary> Formulary { get; set; } = new List<PayerFormulary>();
}

public class PayerFormulary
{
    public int Id { get; set; }
    public int PayerPlanId { get; set; }
    public PayerPlan? PayerPlan { get; set; }

    public int DrugId { get; set; }
    public Drug? Drug { get; set; }

    public bool IsCovered { get; set; } = true;
    public decimal CopayPercent { get; set; }

    [MaxLength(300)] public string? Notes { get; set; }
}

public enum AuthorizationStatus
{
    Requested = 1,
    Approved = 2,
    Denied = 3,
    Expired = 4
}

public class Authorization
{
    public int Id { get; set; }

    public int PatientPayerId { get; set; }
    public PatientPayer? PatientPayer { get; set; }

    public int? EncounterId { get; set; }

    [MaxLength(60)] public string? AuthorizationCode { get; set; }
    [MaxLength(300)] public string? AuthorizedFor { get; set; }

    public AuthorizationStatus Status { get; set; } = AuthorizationStatus.Approved;

    public DateTime? ValidFromUtc { get; set; }
    public DateTime? ValidToUtc { get; set; }

    public decimal? ApprovedAmount { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }
}
