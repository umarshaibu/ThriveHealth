using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Models.Patients;

public enum MedicationSource { Prescribed = 1, OverTheCounter = 2, External = 3, Herbal = 4 }

public class MedicationRecord
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    [Required, MaxLength(200)]
    public string DrugName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? Dose { get; set; }

    [MaxLength(50)]
    public string? Route { get; set; }

    [MaxLength(80)]
    public string? Frequency { get; set; }

    public DateOnly? StartDate { get; set; }
    public DateOnly? EndDate { get; set; }

    public bool IsCurrent { get; set; } = true;

    public MedicationSource Source { get; set; } = MedicationSource.External;

    [MaxLength(500)]
    public string? Notes { get; set; }

    public string? RecordedById { get; set; }
    public ApplicationUser? RecordedBy { get; set; }
    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
}
