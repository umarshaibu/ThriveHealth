using System.ComponentModel.DataAnnotations;

namespace ThriveHealth.Web.Models.ViewModels.Tenancy;

public class TenantSignUpViewModel
{
    [Required, MaxLength(40)]
    [RegularExpression("^[a-z0-9](?:[a-z0-9-]{1,38}[a-z0-9])?$", ErrorMessage = "Subdomains must be 3–40 chars, lowercase letters, digits and hyphens only.")]
    public string Slug { get; set; } = string.Empty;

    [Required, MaxLength(200)]
    public string LegalName { get; set; } = string.Empty;

    [MaxLength(100)] public string? BrandName { get; set; }

    [Required, EmailAddress, MaxLength(150)] public string OwnerEmail { get; set; } = string.Empty;
    [Required, MaxLength(150)] public string OwnerName { get; set; } = string.Empty;
    [MaxLength(50)] public string? OwnerPhone { get; set; }

    [Required, MaxLength(2)] public string CountryCode { get; set; } = "NG";
    [Required, MaxLength(3)] public string CurrencyCode { get; set; } = "NGN";
    [MaxLength(100)] public string? State { get; set; }
    [MaxLength(100)] public string? Lga { get; set; }
    [MaxLength(500)] public string? Address { get; set; }

    [Range(0, 5000)] public int? BedCapacity { get; set; }

    [Required] public string PlanCode { get; set; } = "trial";
    public bool AnnualBilling { get; set; }
    public bool IsTeachingHospital { get; set; }

    [Display(Name = "I accept the terms of service and privacy policy")]
    public bool AcceptTerms { get; set; }
}
