using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Models.Patients;

public enum Sex { Male = 1, Female = 2 }
public enum MaritalStatus { Single = 1, Married = 2, Divorced = 3, Widowed = 4, Separated = 5 }

public class Patient
{
    public int Id { get; set; }

    [Required, MaxLength(40)]
    public string HospitalNumber { get; set; } = string.Empty;

    public int FacilityId { get; set; }
    public Facility? Facility { get; set; }

    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? MiddleName { get; set; }

    [MaxLength(20)]
    public string? Title { get; set; }

    public DateOnly? DateOfBirth { get; set; }
    public bool IsDateOfBirthEstimated { get; set; }

    public Sex Sex { get; set; }

    [MaxLength(40)]
    public string? Gender { get; set; }

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(50)]
    public string? AlternatePhone { get; set; }

    public bool WhatsAppOptIn { get; set; }

    [MaxLength(150)]
    public string? Email { get; set; }

    [MaxLength(500)]
    public string? StreetAddress { get; set; }

    [MaxLength(100)]
    public string? Lga { get; set; }

    [MaxLength(100)]
    public string? State { get; set; }

    [MaxLength(20)]
    public string? Postcode { get; set; }

    public MaritalStatus? MaritalStatus { get; set; }

    [MaxLength(100)]
    public string? Occupation { get; set; }

    [MaxLength(100)]
    public string? Religion { get; set; }

    [MaxLength(100)]
    public string? StateOfOrigin { get; set; }

    [MaxLength(100)]
    public string? EthnicGroup { get; set; }

    [MaxLength(40)]
    public string? PreferredLanguage { get; set; }

    [MaxLength(20)]
    public string? Nin { get; set; }

    public bool NinVerified { get; set; }
    public DateTime? NinVerifiedAt { get; set; }

    [MaxLength(20)]
    public string? Bvn { get; set; }

    [MaxLength(50)]
    public string? DriversLicense { get; set; }

    [MaxLength(50)]
    public string? VotersCard { get; set; }

    [MaxLength(50)]
    public string? Passport { get; set; }

    [MaxLength(500)]
    public string? PhotoPath { get; set; }

    public bool IsDeceased { get; set; }
    public DateTime? DeceasedAt { get; set; }

    public bool IsActive { get; set; } = true;

    public bool IsMergedAlias { get; set; }
    public int? MergedIntoPatientId { get; set; }
    public Patient? MergedIntoPatient { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }

    public ICollection<PatientNextOfKin> NextOfKin { get; set; } = new List<PatientNextOfKin>();
    public ICollection<PatientPayer> Payers { get; set; } = new List<PatientPayer>();
    public ICollection<Allergy> Allergies { get; set; } = new List<Allergy>();
    public ICollection<Problem> Problems { get; set; } = new List<Problem>();
    public ICollection<MedicationRecord> Medications { get; set; } = new List<MedicationRecord>();
    public ICollection<VitalsRecord> Vitals { get; set; } = new List<VitalsRecord>();
    public ICollection<PatientDocument> Documents { get; set; } = new List<PatientDocument>();

    public string FullName => string.IsNullOrWhiteSpace(MiddleName)
        ? $"{FirstName} {LastName}".Trim()
        : $"{FirstName} {MiddleName} {LastName}".Trim();

    public int? AgeYears
    {
        get
        {
            if (DateOfBirth is null) return null;
            var today = DateOnly.FromDateTime(DateTime.Today);
            var dob = DateOfBirth.Value;
            var years = today.Year - dob.Year;
            if (today < dob.AddYears(years)) years--;
            return years;
        }
    }
}
