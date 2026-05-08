using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.Clinical;

public enum ReferralStatus
{
    Sent = 1,
    Acknowledged = 2,
    Completed = 3,
    Cancelled = 4
}

/// <summary>
/// In-facility referral from one clinician to another (e.g. tele-consult MO → in-house cardiology).
/// </summary>
public class Referral
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    [Required, MaxLength(40)] public string ReferralNumber { get; set; } = string.Empty;

    public int EncounterId { get; set; }
    public Encounter? Encounter { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public string? ReferringClinicianId { get; set; }
    public ApplicationUser? ReferringClinician { get; set; }

    public string? ReferredToClinicianId { get; set; }
    public ApplicationUser? ReferredToClinician { get; set; }

    [MaxLength(150)] public string? Specialty { get; set; }
    [MaxLength(500)] public string? Reason { get; set; }
    [MaxLength(2000)] public string? ClinicalSummary { get; set; }

    public ReferralStatus Status { get; set; } = ReferralStatus.Sent;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? AcknowledgedAt { get; set; }
    public DateTime? CompletedAt { get; set; }
}

/// <summary>
/// Sick note / fitness-to-work certificate issued during a consult. Patients download the printable
/// version for HMO reimbursement / employer records.
/// </summary>
public class MedicalCertificate
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    [Required, MaxLength(40)] public string CertificateNumber { get; set; } = string.Empty;

    public int EncounterId { get; set; }
    public Encounter? Encounter { get; set; }

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public string? IssuedById { get; set; }
    public ApplicationUser? IssuedBy { get; set; }

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int DaysOff => (int)(EndDate.ToDateTime(TimeOnly.MinValue) - StartDate.ToDateTime(TimeOnly.MinValue)).TotalDays + 1;

    [MaxLength(300)] public string? Diagnosis { get; set; }
    [MaxLength(20)] public string? IcdCode { get; set; }
    [MaxLength(800)] public string? Recommendations { get; set; }

    public DateTime IssuedAt { get; set; } = DateTime.UtcNow;
}
