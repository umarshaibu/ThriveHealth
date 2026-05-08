using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Billing;
using ThriveHealth.Web.Models.Hr;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;
using ThriveHealth.Web.Models.Theatre;

namespace ThriveHealth.Web.Models.ViewModels;

// ---- HR ----

public class HrStaffRow
{
    public ApplicationUser User { get; set; } = null!;
    public HrProfile? Profile { get; set; }
    public bool LicenseExpiringSoon { get; set; }
    public bool LicenseExpired { get; set; }
}

public class HrStaffListViewModel
{
    public IReadOnlyList<HrStaffRow> Staff { get; set; } = Array.Empty<HrStaffRow>();
    public int Total { get; set; }
    public int LicenseExpiredCount { get; set; }
    public int LicenseExpiringSoonCount { get; set; }
    public string? Search { get; set; }
}

public class HrProfileEditViewModel
{
    public string UserId { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? StaffNumber { get; set; }
    public string? Designation { get; set; }
    public string? LicenseBody { get; set; }
    public string? LicenseNumber { get; set; }
    [DataType(DataType.Date)] public DateOnly? LicenseExpiry { get; set; }

    [DataType(DataType.Date)] public DateOnly? DateOfBirth { get; set; }
    [DataType(DataType.Date)] public DateOnly? HireDate { get; set; }
    public EmploymentType EmploymentType { get; set; } = EmploymentType.Permanent;
    public EmploymentStatus Status { get; set; } = EmploymentStatus.Active;
    [MaxLength(20)] public string? GradeLevel { get; set; }
    [MaxLength(60)] public string? Position { get; set; }
    [MaxLength(60), Display(Name = "Unit / section")] public string? UnitOrSection { get; set; }
    [Display(Name = "Gross monthly salary (₦)")] public decimal? GrossMonthlySalary { get; set; }
    [MaxLength(20), Display(Name = "PFA PIN")] public string? PfaPin { get; set; }
    [MaxLength(20), Display(Name = "NHF #")] public string? NhfNumber { get; set; }
    [MaxLength(20), Display(Name = "PAYE TIN")] public string? PayeTin { get; set; }
    [MaxLength(120)] public string? BankName { get; set; }
    [MaxLength(60), Display(Name = "Account #")] public string? BankAccountNumber { get; set; }
    [MaxLength(50), Display(Name = "Emergency contact")] public string? EmergencyContactName { get; set; }
    [MaxLength(50), Display(Name = "Emergency phone")] public string? EmergencyContactPhone { get; set; }
}

public class RosterGridViewModel
{
    public DateOnly WeekStart { get; set; }
    public IReadOnlyList<DateOnly> Days { get; set; } = Array.Empty<DateOnly>();
    public IReadOnlyList<RosterRow> Rows { get; set; } = Array.Empty<RosterRow>();
}

public class RosterRow
{
    public ApplicationUser Staff { get; set; } = null!;
    public Dictionary<DateOnly, IReadOnlyList<RosterShift>> ByDay { get; set; } = new();
}

public class RosterShiftEditViewModel
{
    public int? Id { get; set; }
    public string? StaffId { get; set; }
    [DataType(DataType.Date)] public DateOnly Date { get; set; }
    public ShiftType ShiftType { get; set; } = ShiftType.Morning;
    public int? WardId { get; set; }
    public int? ClinicId { get; set; }
    [MaxLength(100)] public string? Assignment { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
}

public class LeaveRequestEditViewModel
{
    public int? Id { get; set; }
    public LeaveType Type { get; set; } = LeaveType.Annual;
    [DataType(DataType.Date)] public DateOnly StartDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [DataType(DataType.Date)] public DateOnly EndDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    [MaxLength(500)] public string? Reason { get; set; }
}

// ---- Theatre ----

public class TheatreScheduleRow
{
    public TheatreSession Session { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
}

public class TheatreScheduleViewModel
{
    public DateOnly Date { get; set; }
    public IReadOnlyList<ThriveHealth.Web.Models.Theatre.Theatre> Theatres { get; set; } = Array.Empty<ThriveHealth.Web.Models.Theatre.Theatre>();
    public IReadOnlyList<TheatreScheduleRow> Sessions { get; set; } = Array.Empty<TheatreScheduleRow>();
    public int ScheduledCount { get; set; }
    public int InProgressCount { get; set; }
    public int CompletedCount { get; set; }
}

public class TheatreBookViewModel
{
    public int? PatientId { get; set; }
    public string? PatientLabel { get; set; }
    public int? TheatreId { get; set; }
    public string? LeadSurgeonId { get; set; }
    public string? AnaesthetistId { get; set; }
    public string? ScrubNurseId { get; set; }

    [Required, MaxLength(200), Display(Name = "Procedure")] public string ProcedureName { get; set; } = string.Empty;
    [MaxLength(20)] public string? CptCode { get; set; }
    public CaseUrgency Urgency { get; set; } = CaseUrgency.Elective;
    public AnaesthesiaType Anaesthesia { get; set; } = AnaesthesiaType.GeneralAnaesthesia;

    [Required, DataType(DataType.DateTime), Display(Name = "Scheduled start")] public DateTime ScheduledStartUtc { get; set; } = DateTime.UtcNow.AddDays(1).Date.AddHours(9);
    [Range(15, 1440), Display(Name = "Estimated minutes")] public int EstimatedMinutes { get; set; } = 90;
    [MaxLength(500)] public string? Indication { get; set; }
    [MaxLength(60)] public string? AsaScore { get; set; }
}

public class SessionEventInputViewModel
{
    public int TheatreSessionId { get; set; }
    public SessionEventKind Kind { get; set; } = SessionEventKind.Note;
    [Required, MaxLength(500)] public string Description { get; set; } = string.Empty;
    [MaxLength(1000)] public string? Details { get; set; }
}

public class TheatreFinaliseViewModel
{
    public int Id { get; set; }
    [MaxLength(2000)] public string? OperativeNote { get; set; }
    [MaxLength(2000)] public string? PostOpInstructions { get; set; }
    public int? EstimatedBloodLossMl { get; set; }
    public int? CrystalloidGivenMl { get; set; }
    [MaxLength(500)] public string? Complications { get; set; }
}

// ---- Billing / Cashier ----

public class BillsListRow
{
    public Bill Bill { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
}

public class BillsListViewModel
{
    public IReadOnlyList<BillsListRow> Rows { get; set; } = Array.Empty<BillsListRow>();
    public BillStatus? FilterStatus { get; set; }
    public int OpenCount { get; set; }
    public decimal OpenBalance { get; set; }
    public decimal TodayCollected { get; set; }
}

public class BillDetailViewModel
{
    public Bill Bill { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
}

public class BillDiscountViewModel
{
    public int BillId { get; set; }
    [Range(0, 100000000)] public decimal DiscountAmount { get; set; }
    [Required, MaxLength(200)] public string Reason { get; set; } = string.Empty;
}

public class TakePaymentViewModel
{
    public int BillId { get; set; }
    public decimal Cash { get; set; }
    public decimal Pos { get; set; }
    public decimal BankTransfer { get; set; }
    public decimal MobileMoney { get; set; }
    public decimal Cheque { get; set; }
    [MaxLength(60), Display(Name = "POS terminal / transfer reference")] public string? Reference { get; set; }
    [MaxLength(300)] public string? Notes { get; set; }
}

public class OpenShiftViewModel
{
    [Range(0, 1000000), Display(Name = "Opening cash float (₦)")] public decimal OpeningFloat { get; set; }
}

public class CloseShiftViewModel
{
    public int ShiftId { get; set; }
    public decimal ExpectedCash { get; set; }
    [Range(0, 100000000), Display(Name = "Counted cash on hand (₦)")] public decimal CountedCash { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}
