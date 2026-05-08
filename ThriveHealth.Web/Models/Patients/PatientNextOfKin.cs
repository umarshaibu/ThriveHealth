using System.ComponentModel.DataAnnotations;

namespace ThriveHealth.Web.Models.Patients;

public class PatientNextOfKin
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    [Required, MaxLength(200)]
    public string Name { get; set; } = string.Empty;

    [Required, MaxLength(50)]
    public string Relationship { get; set; } = string.Empty;

    [MaxLength(50)]
    public string? Phone { get; set; }

    [MaxLength(500)]
    public string? Address { get; set; }

    public bool IsPrimary { get; set; } = true;
}
