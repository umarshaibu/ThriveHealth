using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Models.Patients;

public enum PatientDocumentType
{
    CaseNote = 1,
    LabReport = 2,
    ScanReport = 3,
    ReferralLetter = 4,
    DischargeSummary = 5,
    ConsentForm = 6,
    Photo = 7,
    Other = 99
}

public class PatientDocument
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    [Required, MaxLength(200)]
    public string Title { get; set; } = string.Empty;

    public PatientDocumentType DocumentType { get; set; } = PatientDocumentType.Other;

    [Required, MaxLength(255)]
    public string FileName { get; set; } = string.Empty;

    [Required, MaxLength(120)]
    public string ContentType { get; set; } = string.Empty;

    public long ByteSize { get; set; }

    [Required, MaxLength(500)]
    public string FilePath { get; set; } = string.Empty;

    public string? UploadedById { get; set; }
    public ApplicationUser? UploadedBy { get; set; }
    public DateTime UploadedAt { get; set; } = DateTime.UtcNow;
}
