using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Models.Hr;

public enum EmploymentType { Permanent = 1, Contract = 2, Locum = 3, Volunteer = 4, Intern = 5 }
public enum EmploymentStatus { Active = 1, OnLeave = 2, Suspended = 3, Resigned = 4, Dismissed = 5, Retired = 6 }

public class HrProfile
{
    public int Id { get; set; }

    [Required] public string UserId { get; set; } = string.Empty;
    public ApplicationUser? User { get; set; }

    public DateOnly? DateOfBirth { get; set; }
    public DateOnly? HireDate { get; set; }
    public DateOnly? EmploymentEndDate { get; set; }

    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;
    public EmploymentStatus Status { get; set; } = EmploymentStatus.Active;

    [MaxLength(20)] public string? GradeLevel { get; set; }
    [MaxLength(60)] public string? Position { get; set; }
    [MaxLength(60)] public string? UnitOrSection { get; set; }

    public decimal? GrossMonthlySalary { get; set; }

    [MaxLength(20)] public string? PfaPin { get; set; }
    [MaxLength(20)] public string? NhfNumber { get; set; }
    [MaxLength(20)] public string? PayeTin { get; set; }

    [MaxLength(120)] public string? BankName { get; set; }
    [MaxLength(60)] public string? BankAccountNumber { get; set; }

    [MaxLength(50)] public string? EmergencyContactName { get; set; }
    [MaxLength(50)] public string? EmergencyContactPhone { get; set; }

    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public enum ShiftType { Morning = 1, Afternoon = 2, Night = 3, OnCall = 4, FullDay = 5 }

public class RosterShift
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    public string StaffId { get; set; } = string.Empty;
    public ApplicationUser? Staff { get; set; }

    public DateOnly Date { get; set; }
    public ShiftType ShiftType { get; set; }

    public int? WardId { get; set; }
    public int? ClinicId { get; set; }
    [MaxLength(100)] public string? Assignment { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public string? CreatedById { get; set; }
}

public enum LeaveType { Annual = 1, Sick = 2, Maternity = 3, Paternity = 4, Bereavement = 5, Study = 6, Unpaid = 7 }
public enum LeaveStatus { Submitted = 1, Approved = 2, Rejected = 3, Cancelled = 4 }

public class LeaveRequest
{
    public int Id { get; set; }

    public string StaffId { get; set; } = string.Empty;
    public ApplicationUser? Staff { get; set; }

    public LeaveType Type { get; set; }
    public LeaveStatus Status { get; set; } = LeaveStatus.Submitted;

    public DateOnly StartDate { get; set; }
    public DateOnly EndDate { get; set; }
    public int Days { get; set; }

    [MaxLength(500)] public string? Reason { get; set; }
    [MaxLength(500)] public string? DecisionNotes { get; set; }

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? DecidedAt { get; set; }
    public string? DecidedById { get; set; }
    public ApplicationUser? DecidedBy { get; set; }
}
