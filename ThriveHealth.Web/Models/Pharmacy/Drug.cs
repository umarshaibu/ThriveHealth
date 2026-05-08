using System.ComponentModel.DataAnnotations;

namespace ThriveHealth.Web.Models.Pharmacy;

public enum DoseForm
{
    Tablet = 1,
    Capsule = 2,
    Syrup = 3,
    Suspension = 4,
    Injection = 5,
    Infusion = 6,
    Cream = 7,
    Ointment = 8,
    Drops = 9,
    Inhaler = 10,
    Suppository = 11,
    Pessary = 12,
    Powder = 13,
    Gel = 14,
    Lozenge = 15,
    Other = 99
}

public enum DrugCategory
{
    OverTheCounter = 1,
    PrescriptionOnly = 2,
    ControlledSchedule1 = 3,
    ControlledSchedule2 = 4,
    ControlledSchedule3 = 5,
    ControlledSchedule4 = 6
}

public class Drug
{
    public int Id { get; set; }

    [Required, MaxLength(150)] public string GenericName { get; set; } = string.Empty;
    [MaxLength(150)] public string? BrandName { get; set; }

    [MaxLength(20), Display(Name = "NAFDAC #")] public string? NafdacNumber { get; set; }

    [Required, MaxLength(50)] public string Strength { get; set; } = string.Empty;
    public DoseForm DoseForm { get; set; }

    [MaxLength(150)] public string? Manufacturer { get; set; }
    [MaxLength(20)] public string? AtcCode { get; set; }
    [MaxLength(50)] public string? Category { get; set; }

    public DrugCategory Schedule { get; set; } = DrugCategory.PrescriptionOnly;

    public bool IsControlled =>
        Schedule == DrugCategory.ControlledSchedule1 ||
        Schedule == DrugCategory.ControlledSchedule2 ||
        Schedule == DrugCategory.ControlledSchedule3 ||
        Schedule == DrugCategory.ControlledSchedule4;

    [MaxLength(20)] public string? UnitOfIssue { get; set; }
    public decimal? UnitPrice { get; set; }
    public int? ReorderLevel { get; set; }

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public string Display =>
        string.IsNullOrEmpty(BrandName)
            ? $"{GenericName} {Strength}"
            : $"{GenericName} ({BrandName}) {Strength}";
}

public class DrugInteraction
{
    public int Id { get; set; }

    [Required, MaxLength(150)] public string DrugAKey { get; set; } = string.Empty;
    [Required, MaxLength(150)] public string DrugBKey { get; set; } = string.Empty;

    public InteractionSeverity Severity { get; set; } = InteractionSeverity.Moderate;

    [Required, MaxLength(500)] public string Note { get; set; } = string.Empty;
}

public enum InteractionSeverity { Minor = 1, Moderate = 2, Severe = 3, Contraindicated = 4 }
