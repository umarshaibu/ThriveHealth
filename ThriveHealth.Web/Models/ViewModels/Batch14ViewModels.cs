using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Patients;
using ThriveHealth.Web.Models.Portal;
using ThriveHealth.Web.Models.Telemedicine;

namespace ThriveHealth.Web.Models.ViewModels;

public class TeleSessionListRow
{
    public TeleSession Session { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
}

public class TeleSessionListViewModel
{
    public IReadOnlyList<TeleSessionListRow> Rows { get; set; } = Array.Empty<TeleSessionListRow>();
    public TeleSessionStatus? FilterStatus { get; set; }
    public int RequestedCount { get; set; }
    public int InCallCount { get; set; }
    public int CompletedTodayCount { get; set; }
}

public class TeleSessionRequestViewModel
{
    public int? PatientId { get; set; }
    public string? PatientLabel { get; set; }
    public TeleSessionMode Mode { get; set; } = TeleSessionMode.Video;
    [DataType(DataType.DateTime)] public DateTime ScheduledStartUtc { get; set; } = DateTime.UtcNow.AddMinutes(15);
    [MaxLength(500)] public string? ConsultationReason { get; set; }
}

// ---- Portal ----

public class PortalLoginViewModel
{
    [Required, EmailAddress, MaxLength(150)] public string Email { get; set; } = string.Empty;
    [Required, DataType(DataType.Password)] public string Password { get; set; } = string.Empty;
    public bool RememberMe { get; set; } = true;
    public string? ReturnUrl { get; set; }
}

public class PortalRegisterViewModel
{
    [Required, MaxLength(40)] public string HospitalNumber { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string LastName { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string DateOfBirth { get; set; } = string.Empty;
    [Required, EmailAddress, MaxLength(150)] public string Email { get; set; } = string.Empty;
    [MaxLength(50)] public string? Phone { get; set; }
    [Required, DataType(DataType.Password), MinLength(8)] public string Password { get; set; } = string.Empty;
    [Required, DataType(DataType.Password), Compare(nameof(Password))] public string ConfirmPassword { get; set; } = string.Empty;
}

public class PortalDashboardViewModel
{
    public Patient Patient { get; set; } = null!;
    public int UpcomingAppointments { get; set; }
    public int OpenBills { get; set; }
    public decimal OpenBalance { get; set; }
    public int PendingResults { get; set; }
    public int ActiveTeleSessions { get; set; }
    public IReadOnlyList<object> RecentVisits { get; set; } = Array.Empty<object>();
}

public class PortalIntakeViewModel
{
    public int? TeleSessionId { get; set; }
    [Required, MaxLength(500)] public string ChiefComplaint { get; set; } = string.Empty;
    [MaxLength(1500)] public string? Symptoms { get; set; }
    public int? DurationDays { get; set; }
    public SymptomSeverity Severity { get; set; } = SymptomSeverity.Mild;
    [MaxLength(500)] public string? CurrentMedications { get; set; }
    [MaxLength(500)] public string? KnownAllergies { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

public class PortalRequestTeleViewModel
{
    public TeleSessionMode Mode { get; set; } = TeleSessionMode.Video;
    [DataType(DataType.DateTime)] public DateTime ScheduledStartUtc { get; set; } = DateTime.UtcNow.AddMinutes(30);
    [Required, MaxLength(500)] public string ConsultationReason { get; set; } = string.Empty;
    [MaxLength(1500)] public string? Symptoms { get; set; }

    public decimal VideoFee { get; set; }
    public decimal AudioFee { get; set; }
    public decimal ChatFee { get; set; }
    public string Currency { get; set; } = "NGN";
}

public class PortalProfileViewModel
{
    public string HospitalNumber { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string FirstName { get; set; } = string.Empty;
    [Required, MaxLength(50)] public string LastName { get; set; } = string.Empty;
    [DataType(DataType.Date), Required] public DateOnly? DateOfBirth { get; set; }
    [Required] public Sex Sex { get; set; }
    [Required, MaxLength(50)] public string Phone { get; set; } = string.Empty;
    [MaxLength(200)] public string? StreetAddress { get; set; }
    [MaxLength(80)] public string? Lga { get; set; }
    [MaxLength(80)] public string? State { get; set; }
    public IReadOnlyList<string> MissingFields { get; set; } = Array.Empty<string>();
    public string? RedirectAfter { get; set; }
}

public class PortalPayViewModel
{
    public int TeleSessionId { get; set; }
    public int BillId { get; set; }
    public string SessionNumber { get; set; } = string.Empty;
    public string BillNumber { get; set; } = string.Empty;
    public TeleSessionMode Mode { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "NGN";
    public string? ConsultationReason { get; set; }
    public DateTime ScheduledStartUtc { get; set; }
}
