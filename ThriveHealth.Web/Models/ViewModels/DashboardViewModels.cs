namespace ThriveHealth.Web.Models.ViewModels;

public class DashboardStat
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Trend { get; set; }
    public string Tone { get; set; } = "primary";
}

public class DashboardAction
{
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-arrow-right-short";
    public string Controller { get; set; } = "Dashboard";
    public string Action { get; set; } = "Index";
    public object? RouteValues { get; set; }
    /// <summary>If set, this action is hidden when the current user lacks the permission.</summary>
    public string? Permission { get; set; }
}

public class DashboardViewModel
{
    public string FacilityName { get; set; } = string.Empty;
    public string GreetingName { get; set; } = string.Empty;
    public string PrimaryRole { get; set; } = string.Empty;
    public string Persona { get; set; } = "Default";
    /// <summary>
    /// Key matching a persona-specific partial under <c>Views/Dashboard/Partials/</c>.
    /// The Index view dispatches the role-specific working surface based on this value;
    /// when null/unknown the generic Stats + Activity + Quick-actions shell is used.
    /// </summary>
    public string? BoardKey { get; set; }
    /// <summary>Strongly-typed payload for the persona partial. Cast in the view.</summary>
    public object? Board { get; set; }
    public List<DashboardStat> Stats { get; set; } = new();
    public List<string> ActiveAlerts { get; set; } = new();
    public List<DashboardAction> NextActions { get; set; } = new();
    public List<DashboardActivityItem> RecentActivity { get; set; } = new();
    public int PatientCount { get; set; }
}

public class DashboardActivityItem
{
    public DateTime At { get; set; }
    public string Icon { get; set; } = "bi-circle";
    public string Tone { get; set; } = "primary";
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? LinkController { get; set; }
    public string? LinkAction { get; set; }
    public int? LinkId { get; set; }
}

// ============================================================================================
// Per-persona "board" view models — each persona has its own working surface (real lists of
// things to action, not just count tiles). The shape is unique to the work the role does.
// ============================================================================================

public record WaitingPatient(int QueueEntryId, int PatientId, string TicketNo, string PatientName,
    string? Complaint, string Priority, int WaitingMinutes);

public record TodayAppointment(int AppointmentId, int PatientId, string PatientName,
    DateTime ScheduledAt, string? Clinic, string Status);

public record LabReviewItem(int LabOrderId, int PatientId, string PatientName,
    string Tests, bool HasCritical, DateTime ResultedAt);

public record RecentConsult(int EncounterId, int PatientId, string PatientName,
    string? Diagnosis, DateTime SignedAt);

public class ClinicianBoardVm
{
    public List<WaitingPatient> WaitingList { get; set; } = new();
    public List<TodayAppointment> TodaysAppointments { get; set; } = new();
    public List<LabReviewItem> LabsToReview { get; set; } = new();
    public List<RecentConsult> RecentConsultations { get; set; } = new();
}

public record DueMedication(int MarSlotId, int AdmissionId, int PatientId, string PatientName,
    string Bed, string Drug, string Dose, string Route, DateTime ScheduledUtc, bool Overdue);

public record TriagePatient(int EncounterId, int PatientId, string PatientName,
    string? Complaint, string Colour, DateTime TriagedAt);

public record WardSnapshot(int WardId, string WardName, int Total, int Occupied, int Free);

public record VitalsDuePatient(int AdmissionId, int PatientId, string PatientName,
    string Bed, double HoursSinceVitals);

public class NursingBoardVm
{
    public List<DueMedication> MedicationDue { get; set; } = new();
    public List<TriagePatient> TriageQueue { get; set; } = new();
    public List<WardSnapshot> Wards { get; set; } = new();
    public List<VitalsDuePatient> VitalsDue { get; set; } = new();
}

public record PendingPrescription(int PrescriptionId, int PatientId, string PatientName,
    int ItemCount, string? Prescriber, DateTime PrescribedAt);

public record LowStockDrug(int DrugStockId, string DrugName, string StoreName,
    int Quantity, int ReorderPoint);

public record TodayDispense(int DispenseId, string PatientName, int Items, DateTime DispensedAt);

public class PharmacyBoardVm
{
    public List<PendingPrescription> PendingPrescriptions { get; set; } = new();
    public List<LowStockDrug> LowStock { get; set; } = new();
    public List<TodayDispense> TodaysDispenses { get; set; } = new();
}

public record OpenLabSample(int LabOrderId, int PatientId, string PatientName, string Tests,
    DateTime OrderedAt, string Status);

public record CriticalValue(int LabResultId, int LabOrderId, int PatientId, string PatientName,
    string Test, string Value, DateTime FlaggedAt);

public class LabBoardVm
{
    public List<OpenLabSample> OpenSamples { get; set; } = new();
    public List<OpenLabSample> AwaitingAuthorisation { get; set; } = new();
    public List<CriticalValue> CriticalUnnotified { get; set; } = new();
}

public record ClinicQueueCount(int ClinicId, string ClinicName, int Waiting, int InRoom, int Done);

public record TodayRegistration(int PatientId, string Name, DateTime At, bool NinVerified);

public class FrontOfficeBoardVm
{
    public List<ClinicQueueCount> QueueByClinic { get; set; } = new();
    public List<TodayAppointment> TodaysAppointments { get; set; } = new();
    public List<TodayRegistration> NewRegistrations { get; set; } = new();
}

public record OpenBill(int BillId, int PatientId, string PatientName, decimal Total,
    decimal Outstanding, DateTime OpenedAt);

public record RecentPayment(int PaymentId, string PatientName, decimal Amount, string Method, DateTime At);

public class FinanceBoardVm
{
    public List<OpenBill> OpenBills { get; set; } = new();
    public decimal CollectedToday { get; set; }
    public Dictionary<string, decimal> CollectedByMethod { get; set; } = new();
    public List<RecentPayment> RecentPayments { get; set; } = new();
}

public record AdmissionTile(int AdmissionId, int PatientId, string PatientName, string Ward,
    string Bed, DateTime AdmittedAt);

public class ExecutiveBoardVm
{
    public decimal RevenueToday { get; set; }
    public decimal RevenueMonth { get; set; }
    public int VisitsToday { get; set; }
    public int VisitsMonth { get; set; }
    public int ActiveAdmissions { get; set; }
    public int EmergencyInProgress { get; set; }
    public int BedsTotal { get; set; }
    public int BedsOccupied { get; set; }
    public List<AdmissionTile> RecentAdmissions { get; set; } = new();
}
