using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.Immunization;

public enum VaccineRoute { IntraMuscular = 1, SubCutaneous = 2, IntraDermal = 3, Oral = 4, IntraNasal = 5 }

public class Vaccine
{
    public int Id { get; set; }
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(60)] public string Name { get; set; } = string.Empty;
    [MaxLength(200)] public string? Description { get; set; }
    public VaccineRoute Route { get; set; }
    [MaxLength(40)] public string? Site { get; set; }
    public bool IsActive { get; set; } = true;
    public int SortOrder { get; set; }

    public ICollection<VaccineSchedule> Schedule { get; set; } = new List<VaccineSchedule>();
}

public class VaccineSchedule
{
    public int Id { get; set; }
    public int VaccineId { get; set; }
    public Vaccine? Vaccine { get; set; }

    [Required, MaxLength(20)] public string DoseLabel { get; set; } = string.Empty;
    public int RecommendedAgeWeeks { get; set; }
    public int SortOrder { get; set; }
    [MaxLength(200)] public string? Notes { get; set; }
}

public enum DoseStatus { Due = 1, Administered = 2, Missed = 3, Refused = 4, NotApplicable = 5 }

public class ImmunizationDose
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public int VaccineId { get; set; }
    public Vaccine? Vaccine { get; set; }

    public int? VaccineScheduleId { get; set; }
    public VaccineSchedule? VaccineSchedule { get; set; }

    [Required, MaxLength(20)] public string DoseLabel { get; set; } = string.Empty;

    public DateOnly DueDate { get; set; }
    public DateTime? AdministeredAt { get; set; }

    public DoseStatus Status { get; set; } = DoseStatus.Due;
    [MaxLength(40)] public string? BatchNumber { get; set; }
    public DateOnly? ExpiryDate { get; set; }
    [MaxLength(40)] public string? Site { get; set; }

    public bool AdverseEventReported { get; set; }
    [MaxLength(500)] public string? AdverseEventNotes { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public string? AdministeredById { get; set; }
    public ApplicationUser? AdministeredBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
