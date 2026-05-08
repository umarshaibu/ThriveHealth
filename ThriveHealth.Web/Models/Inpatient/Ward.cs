using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Models.Inpatient;

public enum WardType
{
    GeneralMedical = 1,
    GeneralSurgical = 2,
    Female = 3,
    Male = 4,
    Paediatric = 5,
    Maternity = 6,
    LabourWard = 7,
    Postnatal = 8,
    Icu = 9,
    Hdu = 10,
    Nicu = 11,
    Isolation = 12,
    Private = 13,
    Other = 99
}

public class Ward
{
    public int Id { get; set; }
    public int FacilityId { get; set; }
    public Facility? Facility { get; set; }

    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    public WardType Type { get; set; } = WardType.GeneralMedical;

    [MaxLength(20)] public string ColorHex { get; set; } = "#1f6feb";

    public bool IsActive { get; set; } = true;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<Bed> Beds { get; set; } = new List<Bed>();
}

public enum BedStatus
{
    Free = 1,
    Occupied = 2,
    Reserved = 3,
    Cleaning = 4,
    Maintenance = 5,
    Blocked = 6
}

public enum BedRestriction { None = 0, MaleOnly = 1, FemaleOnly = 2, PaediatricOnly = 3 }

public class Bed
{
    public int Id { get; set; }
    public int WardId { get; set; }
    public Ward? Ward { get; set; }

    [Required, MaxLength(20)] public string BedNumber { get; set; } = string.Empty;

    public BedStatus Status { get; set; } = BedStatus.Free;
    public BedRestriction Restriction { get; set; } = BedRestriction.None;

    public bool IsActive { get; set; } = true;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public int? CurrentAdmissionId { get; set; }
}
