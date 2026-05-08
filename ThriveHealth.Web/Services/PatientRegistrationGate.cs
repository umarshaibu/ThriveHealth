using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Services;

/// <summary>
/// Determines whether a patient record is "complete enough" to be billed and consult through tele-medicine.
/// Used by the portal to redirect patients to a profile-completion page before they can request a consult.
/// </summary>
public static class PatientRegistrationGate
{
    public record CompletenessResult(bool IsComplete, IReadOnlyList<string> MissingFields);

    public static CompletenessResult Check(Patient? patient)
    {
        if (patient is null) return new(false, new[] { "Patient record" });
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(patient.HospitalNumber)) missing.Add("Hospital number");
        if (string.IsNullOrWhiteSpace(patient.FirstName)) missing.Add("First name");
        if (string.IsNullOrWhiteSpace(patient.LastName)) missing.Add("Last name");
        if (patient.DateOfBirth is null) missing.Add("Date of birth");
        if (patient.Sex == 0) missing.Add("Sex");
        if (string.IsNullOrWhiteSpace(patient.Phone)) missing.Add("Phone number");
        return new(missing.Count == 0, missing);
    }
}
