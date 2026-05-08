using System.ComponentModel.DataAnnotations;

namespace ThriveHealth.Web.Models.ViewModels;

public class LoginViewModel
{
    [Required, EmailAddress]
    [Display(Name = "Email")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [Display(Name = "Password")]
    public string Password { get; set; } = string.Empty;

    [Display(Name = "Keep me signed in")]
    public bool RememberMe { get; set; }

    public string? ReturnUrl { get; set; }
}

public class RegisterStaffViewModel
{
    [Required, MaxLength(100)]
    public string FirstName { get; set; } = string.Empty;

    [Required, MaxLength(100)]
    public string LastName { get; set; } = string.Empty;

    [MaxLength(100)]
    public string? MiddleName { get; set; }

    [Required, EmailAddress]
    public string Email { get; set; } = string.Empty;

    [Phone]
    public string? PhoneNumber { get; set; }

    [Required]
    public string Role { get; set; } = string.Empty;

    [MaxLength(20)]
    [Display(Name = "Staff Number")]
    public string? StaffNumber { get; set; }

    [MaxLength(100)]
    public string? Department { get; set; }

    [MaxLength(50)]
    public string? Designation { get; set; }

    [MaxLength(50)]
    [Display(Name = "License Body")]
    public string? LicenseBody { get; set; }

    [MaxLength(50)]
    [Display(Name = "License Number")]
    public string? LicenseNumber { get; set; }

    [DataType(DataType.Date)]
    [Display(Name = "License Expiry")]
    public DateTime? LicenseExpiry { get; set; }

    public int? FacilityId { get; set; }

    [Required, DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8)]
    public string Password { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(Password), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ChangePasswordViewModel
{
    [Required, DataType(DataType.Password)]
    [Display(Name = "Current Password")]
    public string CurrentPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password)]
    [StringLength(100, MinimumLength = 8)]
    [Display(Name = "New Password")]
    public string NewPassword { get; set; } = string.Empty;

    [DataType(DataType.Password)]
    [Compare(nameof(NewPassword), ErrorMessage = "Passwords do not match.")]
    [Display(Name = "Confirm New Password")]
    public string ConfirmPassword { get; set; } = string.Empty;
}

public class ProfileViewModel
{
    [Display(Name = "First Name")]
    public string FirstName { get; set; } = string.Empty;

    [Display(Name = "Last Name")]
    public string LastName { get; set; } = string.Empty;

    [Display(Name = "Middle Name")]
    public string? MiddleName { get; set; }

    public string Email { get; set; } = string.Empty;

    [Phone]
    [Display(Name = "Phone Number")]
    public string? PhoneNumber { get; set; }

    [Display(Name = "Staff Number")]
    public string? StaffNumber { get; set; }

    public string? Department { get; set; }
    public string? Designation { get; set; }

    [Display(Name = "License Body")]
    public string? LicenseBody { get; set; }

    [Display(Name = "License Number")]
    public string? LicenseNumber { get; set; }

    [Display(Name = "License Expiry")]
    public DateTime? LicenseExpiry { get; set; }

    public string? FacilityName { get; set; }
    public IEnumerable<string> Roles { get; set; } = Enumerable.Empty<string>();
}
