using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Models.Scheduling;

public enum ClinicSpecialty
{
    GeneralOpd = 1,
    Internal = 2,
    Surgery = 3,
    Paediatrics = 4,
    ObstetricsGynaecology = 5,
    Antenatal = 6,
    Immunization = 7,
    Cardiology = 8,
    Dermatology = 9,
    Ent = 10,
    Ophthalmology = 11,
    Orthopaedics = 12,
    Psychiatry = 13,
    Dental = 14,
    Physiotherapy = 15,
    Optometry = 16,
    Telemedicine = 17,
    Other = 99
}

public class Clinic
{
    public int Id { get; set; }
    public int FacilityId { get; set; }
    public Facility? Facility { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    public ClinicSpecialty Specialty { get; set; } = ClinicSpecialty.GeneralOpd;

    public int DefaultSlotMinutes { get; set; } = 15;

    [MaxLength(20)]
    public string ColorHex { get; set; } = "#1f6feb";

    public bool IsActive { get; set; } = true;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public ICollection<ClinicianAvailability> Availability { get; set; } = new List<ClinicianAvailability>();
}

public class Room
{
    public int Id { get; set; }
    public int FacilityId { get; set; }
    public Facility? Facility { get; set; }

    [Required, MaxLength(100)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(20)]
    public string Code { get; set; } = string.Empty;

    public bool IsActive { get; set; } = true;
}
