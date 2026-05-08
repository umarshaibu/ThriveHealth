using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Models.Patients;

public enum AllergySeverity { Mild = 1, Moderate = 2, Severe = 3, Anaphylaxis = 4 }
public enum AllergyCategory { Drug = 1, Food = 2, Environmental = 3, Latex = 4, Other = 5 }

public class Allergy
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public AllergyCategory Category { get; set; } = AllergyCategory.Drug;

    [Required, MaxLength(200)]
    public string Substance { get; set; } = string.Empty;

    [MaxLength(300)]
    public string? Reaction { get; set; }

    public AllergySeverity Severity { get; set; } = AllergySeverity.Moderate;

    public DateOnly? OnsetDate { get; set; }

    [MaxLength(500)]
    public string? Notes { get; set; }

    public bool IsActive { get; set; } = true;

    public string? RecordedById { get; set; }
    public ApplicationUser? RecordedBy { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
