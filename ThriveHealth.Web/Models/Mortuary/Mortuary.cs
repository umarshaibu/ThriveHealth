using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.Mortuary;

public enum MortuaryStatus { Received = 1, Embalmed = 2, AwaitingRelease = 3, Released = 4, Buried = 5, Transferred = 6 }
public enum MannerOfDeath { Natural = 1, Accident = 2, Suicide = 3, Homicide = 4, Undetermined = 5 }

public class MortuaryEntry
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    [Required, MaxLength(40)] public string MortuaryNumber { get; set; } = string.Empty;
    [MaxLength(20)] public string? CabinetCode { get; set; }

    public int? PatientId { get; set; }
    public Patient? Patient { get; set; }
    public bool IsUnidentified { get; set; }

    [Required, MaxLength(120)] public string DeceasedName { get; set; } = string.Empty;
    [MaxLength(20)] public string? Sex { get; set; }
    public DateOnly? DateOfBirth { get; set; }
    public int? AgeYears { get; set; }
    [MaxLength(120)] public string? Tribe { get; set; }
    [MaxLength(200)] public string? AddressOfOrigin { get; set; }

    public DateTime DateOfDeathUtc { get; set; }
    [MaxLength(200)] public string? PlaceOfDeath { get; set; }
    [MaxLength(200)] public string? CauseOfDeath { get; set; }
    public MannerOfDeath? Manner { get; set; }

    public bool Embalmed { get; set; }
    public DateTime? EmbalmedAt { get; set; }
    [MaxLength(120)] public string? EmbalmedBy { get; set; }

    public bool PostMortemDone { get; set; }
    [MaxLength(200)] public string? PostMortemFinding { get; set; }

    [Required, MaxLength(120)] public string NextOfKinName { get; set; } = string.Empty;
    [MaxLength(60)] public string? NextOfKinRelationship { get; set; }
    [MaxLength(50)] public string? NextOfKinPhone { get; set; }
    [MaxLength(40)] public string? NextOfKinId { get; set; }

    public MortuaryStatus Status { get; set; } = MortuaryStatus.Received;
    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ReleasedAt { get; set; }
    [MaxLength(120)] public string? ReleasedTo { get; set; }
    [MaxLength(40)] public string? ReleasedToId { get; set; }
    [MaxLength(60)] public string? ReleaseAuthorityRef { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }

    public string? ReceivedById { get; set; }
    public ApplicationUser? ReceivedBy { get; set; }

    public string? ReleasedById { get; set; }
    public ApplicationUser? ReleasedBy { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public int LengthOfStayDays => (int)((ReleasedAt ?? DateTime.UtcNow) - ReceivedAt).TotalDays;
}
