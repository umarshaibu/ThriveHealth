using System.ComponentModel.DataAnnotations;

namespace ThriveHealth.Web.Models.Diagnostics;

public enum LabSection
{
    Haematology = 1,
    Chemistry = 2,
    Microbiology = 3,
    Endocrinology = 4,
    Immunology = 5,
    Histopathology = 6,
    Cytology = 7,
    Coagulation = 8,
    BloodBank = 9,
    Parasitology = 10,
    Other = 99
}

public class LabTest
{
    public int Id { get; set; }

    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(150)] public string Name { get; set; } = string.Empty;

    public LabSection Section { get; set; } = LabSection.Chemistry;

    [MaxLength(20)] public string? LoincCode { get; set; }
    [MaxLength(80)] public string? Specimen { get; set; }
    [MaxLength(40)] public string? Container { get; set; }

    public int TurnaroundHours { get; set; } = 24;

    public decimal? Price { get; set; }

    public bool IsActive { get; set; } = true;

    public ICollection<LabAnalyte> Analytes { get; set; } = new List<LabAnalyte>();
}

public class LabAnalyte
{
    public int Id { get; set; }
    public int LabTestId { get; set; }
    public LabTest? LabTest { get; set; }

    [Required, MaxLength(80)] public string Name { get; set; } = string.Empty;
    [MaxLength(20)] public string? Code { get; set; }
    [MaxLength(20)] public string? Unit { get; set; }

    public decimal? RefLow { get; set; }
    public decimal? RefHigh { get; set; }
    public decimal? CriticalLow { get; set; }
    public decimal? CriticalHigh { get; set; }

    [MaxLength(40)] public string? AgeGroup { get; set; }
    [MaxLength(40)] public string? Sex { get; set; }

    public int SortOrder { get; set; }
}
