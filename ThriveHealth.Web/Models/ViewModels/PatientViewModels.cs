using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.ViewModels;

public class PatientRegisterViewModel
{
    [Required, MaxLength(100), Display(Name = "First name")]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100), Display(Name = "Last name (Surname)")]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(100), Display(Name = "Middle name")]
    public string? MiddleName { get; set; }

    [MaxLength(20)]
    public string? Title { get; set; }

    [DataType(DataType.Date), Display(Name = "Date of birth")]
    public DateOnly? DateOfBirth { get; set; }

    [Display(Name = "DOB is estimated")]
    public bool IsDateOfBirthEstimated { get; set; }

    [Required]
    public Sex Sex { get; set; }

    [MaxLength(40)]
    public string? Gender { get; set; }

    [Phone, MaxLength(50), Display(Name = "Primary phone")]
    public string? Phone { get; set; }

    [Phone, MaxLength(50), Display(Name = "Alternate phone")]
    public string? AlternatePhone { get; set; }

    [Display(Name = "WhatsApp opt-in")]
    public bool WhatsAppOptIn { get; set; } = true;

    [EmailAddress, MaxLength(150)]
    public string? Email { get; set; }

    [MaxLength(500), Display(Name = "Street address")]
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

    [MaxLength(100), Display(Name = "State of origin")]
    public string? StateOfOrigin { get; set; }

    [MaxLength(100), Display(Name = "Ethnic group")]
    public string? EthnicGroup { get; set; }

    [MaxLength(40), Display(Name = "Preferred language")]
    public string? PreferredLanguage { get; set; } = "English";

    [MaxLength(20), Display(Name = "NIN")]
    [RegularExpression(@"^\d{11}$", ErrorMessage = "NIN must be 11 digits.")]
    public string? Nin { get; set; }

    [MaxLength(50), Display(Name = "Driver's licence")]
    public string? DriversLicense { get; set; }

    [MaxLength(50), Display(Name = "Voter's card")]
    public string? VotersCard { get; set; }

    [MaxLength(50), Display(Name = "Passport")]
    public string? Passport { get; set; }

    [Display(Name = "Next of kin name")]
    public string? NextOfKinName { get; set; }

    [Display(Name = "Next of kin relationship")]
    public string? NextOfKinRelationship { get; set; }

    [Phone, Display(Name = "Next of kin phone")]
    public string? NextOfKinPhone { get; set; }

    [Display(Name = "Next of kin address")]
    public string? NextOfKinAddress { get; set; }

    [Display(Name = "Primary payer")]
    public PayerType PrimaryPayerType { get; set; } = PayerType.OutOfPocket;

    [Display(Name = "Payer name")]
    public string? PrimaryPayerName { get; set; }

    [Display(Name = "HMO")]
    public int? PrimaryHmoId { get; set; }

    [Display(Name = "Membership / enrolment number")]
    public string? PrimaryPayerMembershipNumber { get; set; }

    public bool ConfirmAcceptDuplicate { get; set; }
}

public class PatientListViewModel
{
    public string? Search { get; set; }
    public IReadOnlyList<Patient> Patients { get; set; } = Array.Empty<Patient>();
    public int Total { get; set; }
}

public class PatientProfileViewModel
{
    public Patient Patient { get; set; } = null!;
    public IReadOnlyList<PatientNextOfKin> NextOfKin { get; set; } = Array.Empty<PatientNextOfKin>();
    public IReadOnlyList<PatientPayer> Payers { get; set; } = Array.Empty<PatientPayer>();
    public IReadOnlyList<Allergy> Allergies { get; set; } = Array.Empty<Allergy>();
    public IReadOnlyList<Problem> Problems { get; set; } = Array.Empty<Problem>();
    public IReadOnlyList<MedicationRecord> Medications { get; set; } = Array.Empty<MedicationRecord>();
    public IReadOnlyList<VitalsRecord> Vitals { get; set; } = Array.Empty<VitalsRecord>();
    public IReadOnlyList<PatientDocument> Documents { get; set; } = Array.Empty<PatientDocument>();
    public IReadOnlyList<ThriveHealth.Web.Models.Clinical.Encounter> Encounters { get; set; } = Array.Empty<ThriveHealth.Web.Models.Clinical.Encounter>();
}

public class AllergyEditViewModel
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public AllergyCategory Category { get; set; } = AllergyCategory.Drug;
    [Required, MaxLength(200)] public string Substance { get; set; } = string.Empty;
    [MaxLength(300)] public string? Reaction { get; set; }
    public AllergySeverity Severity { get; set; } = AllergySeverity.Moderate;
    [DataType(DataType.Date)] public DateOnly? OnsetDate { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

public class ProblemEditViewModel
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    [Required, MaxLength(300)] public string Description { get; set; } = string.Empty;
    [MaxLength(20), Display(Name = "ICD code")] public string? IcdCode { get; set; }
    public ProblemStatus Status { get; set; } = ProblemStatus.Active;
    [DataType(DataType.Date), Display(Name = "Onset")] public DateOnly? OnsetDate { get; set; }
    [DataType(DataType.Date), Display(Name = "Resolved on")] public DateOnly? ResolutionDate { get; set; }
    [MaxLength(1000)] public string? Notes { get; set; }
}

public class MedicationEditViewModel
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    [Required, MaxLength(200), Display(Name = "Drug name")] public string DrugName { get; set; } = string.Empty;
    [MaxLength(100)] public string? Dose { get; set; }
    [MaxLength(50)] public string? Route { get; set; }
    [MaxLength(80)] public string? Frequency { get; set; }
    [DataType(DataType.Date)] public DateOnly? StartDate { get; set; }
    [DataType(DataType.Date)] public DateOnly? EndDate { get; set; }
    public bool IsCurrent { get; set; } = true;
    public MedicationSource Source { get; set; } = MedicationSource.External;
    [MaxLength(500)] public string? Notes { get; set; }
}

public class VitalsEditViewModel
{
    public int PatientId { get; set; }
    [Range(40, 260), Display(Name = "Systolic BP")] public int? SystolicBp { get; set; }
    [Range(20, 200), Display(Name = "Diastolic BP")] public int? DiastolicBp { get; set; }
    [Range(20, 250), Display(Name = "Heart rate")] public int? HeartRate { get; set; }
    [Range(5, 80), Display(Name = "Respiratory rate")] public int? RespiratoryRate { get; set; }
    [Range(25, 45), Display(Name = "Temperature (°C)")] public decimal? TemperatureCelsius { get; set; }
    [Range(40, 100), Display(Name = "SpO₂ (%)")] public int? SpO2 { get; set; }
    [Range(0.5, 400), Display(Name = "Weight (kg)")] public decimal? WeightKg { get; set; }
    [Range(20, 250), Display(Name = "Height (cm)")] public decimal? HeightCm { get; set; }
    [Range(0, 10), Display(Name = "Pain score")] public int? PainScore { get; set; }
    [Range(3, 15), Display(Name = "GCS total")] public int? GcsTotal { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}
