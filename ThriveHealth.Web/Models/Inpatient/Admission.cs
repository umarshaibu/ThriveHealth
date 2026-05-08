using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.Inpatient;

public enum AdmissionStatus
{
    Active = 1,
    Discharged = 2,
    Cancelled = 3,
    DamaSelfDischarge = 4,
    Absconded = 5,
    Deceased = 6,
    Transferred = 7
}

public enum DischargeDisposition
{
    Home = 1,
    HomeWithFollowUp = 2,
    Transferred = 3,
    Dama = 4,
    Absconded = 5,
    Deceased = 6
}

public class Admission
{
    public int Id { get; set; }
    public int FacilityId { get; set; }
    public Facility? Facility { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public int WardId { get; set; }
    public Ward? Ward { get; set; }

    public int BedId { get; set; }
    public Bed? Bed { get; set; }

    public string AdmittingDoctorId { get; set; } = string.Empty;
    public ApplicationUser? AdmittingDoctor { get; set; }

    public int? SourceEncounterId { get; set; }
    public Encounter? SourceEncounter { get; set; }

    public int? AdmissionEncounterId { get; set; }
    public Encounter? AdmissionEncounter { get; set; }

    [MaxLength(500)] public string ReasonForAdmission { get; set; } = string.Empty;
    [MaxLength(500)] public string? WorkingDiagnosis { get; set; }

    public AdmissionStatus Status { get; set; } = AdmissionStatus.Active;

    public DateTime AdmittedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DischargedAt { get; set; }

    public DischargeDisposition? DischargeDisposition { get; set; }
    [MaxLength(500)] public string? DischargeDiagnosis { get; set; }
    [MaxLength(2000)] public string? DischargeSummary { get; set; }
    [MaxLength(1000)] public string? FollowUpPlan { get; set; }

    public string? DischargedById { get; set; }
    public ApplicationUser? DischargedBy { get; set; }

    public ICollection<BedAllocation> BedHistory { get; set; } = new List<BedAllocation>();
    public ICollection<InpatientMedication> Medications { get; set; } = new List<InpatientMedication>();
    public ICollection<FluidEntry> Fluids { get; set; } = new List<FluidEntry>();
    public ICollection<NursingNote> NursingNotes { get; set; } = new List<NursingNote>();
    public ICollection<WardRoundEntry> WardRounds { get; set; } = new List<WardRoundEntry>();

    public TimeSpan? LengthOfStay =>
        DischargedAt.HasValue ? DischargedAt.Value - AdmittedAt : DateTime.UtcNow - AdmittedAt;
}

public class BedAllocation
{
    public int Id { get; set; }
    public int AdmissionId { get; set; }
    public Admission? Admission { get; set; }

    public int BedId { get; set; }
    public Bed? Bed { get; set; }

    public DateTime FromUtc { get; set; } = DateTime.UtcNow;
    public DateTime? ToUtc { get; set; }

    [MaxLength(300)] public string? Reason { get; set; }
    public string? AllocatedById { get; set; }
}
