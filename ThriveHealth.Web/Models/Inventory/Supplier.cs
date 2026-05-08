using System.ComponentModel.DataAnnotations;

namespace ThriveHealth.Web.Models.Inventory;

public class Supplier
{
    public int Id { get; set; }

    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;

    [MaxLength(150)] public string? ContactPerson { get; set; }
    [MaxLength(50)] public string? Phone { get; set; }
    [MaxLength(150)] public string? Email { get; set; }
    [MaxLength(500)] public string? Address { get; set; }

    [MaxLength(40)] public string? TaxId { get; set; }
    [MaxLength(40)] public string? RcNumber { get; set; }

    [MaxLength(150)] public string? BankName { get; set; }
    [MaxLength(60)] public string? BankAccountNumber { get; set; }

    [MaxLength(60)] public string? PaymentTerms { get; set; }
    public int LeadTimeDays { get; set; } = 7;

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
