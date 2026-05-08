using System.ComponentModel.DataAnnotations;

namespace ThriveHealth.Web.Models.Patients;

public enum PayerType
{
    OutOfPocket = 1,
    Nhia = 2,
    Hmo = 3,
    StateInsurance = 4,
    Employer = 5,
    Donor = 6,
    FreeMaternalChild = 7,
    Bhcpf = 8
}

public class PatientPayer
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public PayerType Type { get; set; }

    public int? PayerId { get; set; }
    public ThriveHealth.Web.Models.Insurance.Payer? Payer { get; set; }

    public int? PayerPlanId { get; set; }
    public ThriveHealth.Web.Models.Insurance.PayerPlan? PayerPlan { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? PlanName { get; set; }

    [MaxLength(100)]
    public string? MembershipNumber { get; set; }

    [MaxLength(100)]
    public string? AuthorizationCode { get; set; }

    public bool IsPrimary { get; set; }
    public bool IsActive { get; set; } = true;

    public DateOnly? ValidFrom { get; set; }
    public DateOnly? ValidTo { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
