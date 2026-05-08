using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Identity;

namespace ThriveHealth.Web.Models.Identity;

public class ApplicationUser : IdentityUser
{
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? MiddleName { get; set; }

    [MaxLength(20)]
    public string? StaffNumber { get; set; }

    [MaxLength(50)]
    public string? Designation { get; set; }

    [MaxLength(100)]
    public string? Department { get; set; }

    [MaxLength(50)]
    public string? LicenseBody { get; set; }

    [MaxLength(50)]
    public string? LicenseNumber { get; set; }

    public DateTime? LicenseExpiry { get; set; }

    [MaxLength(20)]
    public string? Nin { get; set; }

    public int? FacilityId { get; set; }
    public Facility? Facility { get; set; }

    /// <summary>Owning tenant. Mirrored from <see cref="Facility.TenantId"/> for fast tenant filtering
    /// on user lookups (since auth happens before facility is loaded).</summary>
    public int? TenantId { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;

    public string FullName => string.IsNullOrWhiteSpace(MiddleName)
        ? $"{FirstName} {LastName}".Trim()
        : $"{FirstName} {MiddleName} {LastName}".Trim();
}
