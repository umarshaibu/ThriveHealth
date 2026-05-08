using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Models.Inpatient;

public enum FluidKind { Input = 1, Output = 2 }

public enum FluidType
{
    Oral = 1,
    IvCrystalloid = 2,
    IvColloid = 3,
    Blood = 4,
    NgFeed = 5,
    Other = 9,

    Urine = 100,
    Stool = 101,
    Vomit = 102,
    Drain = 103,
    NgAspirate = 104,
    Blood_Loss = 105,
    Insensible = 199
}

public class FluidEntry
{
    public int Id { get; set; }
    public int AdmissionId { get; set; }
    public Admission? Admission { get; set; }

    public FluidKind Kind { get; set; }
    public FluidType Type { get; set; }

    [Required] public int VolumeMl { get; set; }
    [MaxLength(100)] public string? Description { get; set; }

    public DateTime RecordedUtc { get; set; } = DateTime.UtcNow;
    public string? RecordedById { get; set; }
    public ApplicationUser? RecordedBy { get; set; }
}

public class NursingNote
{
    public int Id { get; set; }
    public int AdmissionId { get; set; }
    public Admission? Admission { get; set; }

    [MaxLength(20)] public string? Shift { get; set; }
    [Required, MaxLength(4000)] public string Body { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Handover { get; set; }

    public DateTime RecordedUtc { get; set; } = DateTime.UtcNow;
    public string? RecordedById { get; set; }
    public ApplicationUser? RecordedBy { get; set; }
}

public class WardRoundEntry
{
    public int Id { get; set; }
    public int AdmissionId { get; set; }
    public Admission? Admission { get; set; }

    [Required, MaxLength(4000)] public string Body { get; set; } = string.Empty;
    [MaxLength(500)] public string? PlanChanges { get; set; }

    public DateTime RecordedUtc { get; set; } = DateTime.UtcNow;
    public string? RecordedById { get; set; }
    public ApplicationUser? RecordedBy { get; set; }
}
