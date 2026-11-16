using System.ComponentModel.DataAnnotations;

namespace ThriveHealth.Web.Models.Api;

// ---- Auth request bodies ----

// NOTE: validation attributes on record positional parameters must be applied to the PARAMETER
// (bare [Required]), not via [property: ...] — MVC throws at request time otherwise.
public record ApiLoginRequest(
    [Required] string Email,
    [Required] string Password);

public record ApiRegisterRequest(
    [Required] string HospitalNumber,
    [Required] string LastName,
    [Required] string DateOfBirth, // yyyy-MM-dd
    [Required, EmailAddress] string Email,
    string? Phone,
    [Required, MinLength(8)] string Password);

public record ApiRefreshRequest([Required] string RefreshToken);

public record ApiLogoutRequest(string? RefreshToken);

// ---- Auth responses ----

public record ApiTokenResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresInSeconds,
    ApiPatientProfile Patient);

// ---- Patient profile ----

public record ApiPatientProfile(
    int PatientId,
    string HospitalNumber,
    string FullName,
    string? Gender,
    string? DateOfBirth,
    string? Phone,
    string? Email,
    int FacilityId,
    string FacilityName,
    int TenantId);

// ---- Facility (public, for the registration facility picker) ----

public record ApiFacility(
    int Id,
    int TenantId,
    string Name,
    string Code,
    string? State,
    string? Lga);

// ---- Dashboard ----

public record ApiDashboard(
    int UpcomingAppointments,
    int OpenBills,
    decimal OpenBalance,
    int PendingResults,
    int ActiveTeleSessions,
    IReadOnlyList<ApiVisitSummary> RecentVisits);

// ---- Visits / encounters ----

public record ApiVisitSummary(
    int Id,
    DateTime StartedAt,
    string Status,
    string Type,
    string? ChiefComplaint,
    string? ClinicName,
    string? ClinicianName);

public record ApiVisitDetail(
    int Id,
    DateTime StartedAt,
    string Status,
    string Type,
    string? ChiefComplaint,
    string? ClinicName,
    string? ClinicianName,
    ApiSoap? Soap,
    IReadOnlyList<ApiDiagnosis> Diagnoses,
    IReadOnlyList<ApiPrescription> Prescriptions);

public record ApiSoap(string? Subjective, string? Objective, string? Assessment, string? Plan);

public record ApiDiagnosis(string IcdCode, string Description, string Status);

public record ApiPrescription(int Id, string Status, IReadOnlyList<string> Items);

// ---- Results ----

public record ApiLabResult(
    int OrderId,
    string TestName,
    string Status,
    DateTime OrderedAt,
    bool HasResult);

public record ApiImagingResult(
    int OrderId,
    string Modality,
    string Status,
    DateTime OrderedAt,
    bool HasReport);

public record ApiResults(
    IReadOnlyList<ApiLabResult> Labs,
    IReadOnlyList<ApiImagingResult> Imaging);

// ---- Vitals ----

public record ApiVitalsReading(
    int Id,
    DateTime RecordedAt,
    int? SystolicBp,
    int? DiastolicBp,
    int? HeartRate,
    int? RespiratoryRate,
    decimal? TemperatureCelsius,
    int? SpO2,
    decimal? WeightKg,
    decimal? Bmi);

// ---- Doctor directory ----

public record ApiDoctor(
    string Id,
    string Name,
    string? Title,
    bool IsConsultant,
    double? Rating,
    int RatingCount,
    int? YearsExperience);

// ---- Hospital finder ----

public record ApiHospital(
    string Key,
    string Name,
    string? Address,
    string? Phone,
    double Latitude,
    double Longitude,
    bool IsYours,
    bool InNetwork);

// ---- AI assistant ----

public record ApiAssistantRequest(
    [Required, StringLength(1000, MinimumLength = 2)] string Question,
    IReadOnlyList<string>? History);

public record ApiAssistantReply(string Text);

// ---- Appointments ----

public record ApiAppointment(
    int Id,
    DateTime ScheduledStartUtc,
    int DurationMinutes,
    string Status,
    string Type,
    string? ClinicName,
    string? ClinicianName,
    string? ReasonForVisit);

// ---- Bills ----

public record ApiBill(
    int Id,
    string BillNumber,
    string Status,
    DateTime CreatedAt,
    decimal GrossAmount,
    decimal NetAmount,
    decimal PaidAmount,
    decimal Balance);

// ---- Telemedicine ----

public record ApiTeleFees(decimal Video, decimal Audio, decimal Chat, string Currency);

public record ApiTeleSession(
    int Id,
    string SessionNumber,
    string Mode,
    string Status,
    DateTime scheduledStartUtc,
    string? consultationReason,
    string? clinicianName,
    int? billId,
    string? billStatus,
    decimal? billBalance,
    string roomToken,
    int? patientRating);

public record ApiTeleRequest(
    [Required] string Mode,                 // Video | Audio | Chat
    string? Reason,
    string? Symptoms,
    DateTime? ScheduledStartUtc);

public record ApiTeleToken(
    bool Configured,
    string? ServerUrl,
    string? Token,
    string? Room,
    string mode);

public record ApiRateRequest(int Rating, string? Feedback);

// ---- Appointment booking ----

public record ApiClinic(int Id, string Name, string Specialty);

public record ApiSlot(DateTime startUtc, int durationMinutes, string clinicianId, string clinicianName, int? roomId);

public record ApiBookRequest(
    int ClinicId,
    DateTime ScheduledStartUtc,
    string? ClinicianId,
    int? RoomId,
    int DurationMinutes,
    string? ReasonForVisit);

// ---- Payments ----

public record ApiPayInitiateResult(long TransactionId, string Reference, string AuthorizationUrl, string Provider, bool RequiresExternalCheckout);

public record ApiPaymentStatus(long TransactionId, string Status, decimal Amount, int BillId, string BillStatus);

// ---- Chat ----

public record ApiChatMessage(long Id, string Role, string Who, string Body, DateTime SentAt, bool ReadByOther);

public record ApiChatPackage(decimal Price, bool IsActive, DateTime? ExpiresAt);

public record ApiChatThread(int? SessionId, string? Status, ApiChatPackage Package, IReadOnlyList<ApiChatMessage> Messages);

public record ApiSendChatRequest([Required] string Body, long? RepliesToMessageId);

// ---- Push ----

public record ApiRegisterDeviceRequest([Required] string Token, string? Platform);

// ---- Staff (clinician) auth ----

public record ApiStaffLoginRequest([Required] string Email, [Required] string Password);

public record ApiStaffProfile(
    string UserId,
    string FullName,
    string? Email,
    string? Designation,
    string? Department,
    int FacilityId,
    string FacilityName,
    int TenantId,
    IReadOnlyList<string> Roles,
    bool IsConsultant);

public record ApiStaffTokenResponse(
    string AccessToken,
    string RefreshToken,
    string TokenType,
    int ExpiresInSeconds,
    ApiStaffProfile Staff);

// ---- Clinician dashboard / schedule ----

public record ApiClinicianDashboard(
    int TodayAppointments,
    int UpcomingAppointments,
    int PendingTeleRequests,
    int UnreadChats,
    int OpenEncounters,
    IReadOnlyList<ApiScheduleItem> NextUp);

public record ApiScheduleItem(
    int Id,
    DateTime scheduledStartUtc,
    int durationMinutes,
    string status,
    string type,
    int patientId,
    string patientName,
    string patientHospitalNumber,
    string? clinicName,
    string? reasonForVisit);

// ---- Patients (clinician view) ----

public record ApiPatientListItem(
    int Id,
    string HospitalNumber,
    string FullName,
    string? Gender,
    string? DateOfBirth,
    string? Phone);

public record ApiPatientChartProblem(string Description, string Status);

public record ApiPatientChart(
    ApiPatientListItem Patient,
    IReadOnlyList<string> Allergies,
    IReadOnlyList<ApiPatientChartProblem> Problems,
    IReadOnlyList<ApiVisitSummary> RecentVisits);

// ---- Clinician chat inbox ----

public record ApiChatInboxItem(
    int PatientId,
    string PatientName,
    string PatientHospitalNumber,
    int? SessionId,
    string? LastMessagePreview,
    DateTime? LastMessageAt,
    int UnreadCount);

public record ApiStaffSendChatRequest(int PatientId, [Required] string Body);

// ---- Clinician consult workspace (document a consult) ----

public record ApiOrderLine(int Id, string Name, string Status);

public record ApiConsultWorkspace(
    int SessionId,
    string PatientName,
    string PatientHospitalNumber,
    string Mode,
    string Status,
    ApiSoap Soap,
    IReadOnlyList<ApiPrescription> Prescriptions,
    IReadOnlyList<ApiOrderLine> LabOrders,
    IReadOnlyList<ApiOrderLine> ImagingOrders);

public record ApiSaveNotesRequest(string? Subjective, string? Objective, string? Assessment, string? Plan);

public record ApiPrescribeRequest(
    [Required] string DrugName,
    string? Dose,
    string? Route,
    string? Frequency,
    string? Duration,
    int? Quantity,
    string? Instructions);

public record ApiLabOrderRequest([Required] string TestName, string? ClinicalIndication, string? Urgency);

public record ApiImagingOrderRequest(
    [Required] string Modality,
    [Required] string StudyDescription,
    string? ClinicalIndication,
    string? Urgency);

// ---- Clinician tele inbox ----

public record ApiTeleInboxItem(
    int Id,
    string SessionNumber,
    string Mode,
    string Status,
    DateTime scheduledStartUtc,
    int patientId,
    string patientName,
    string patientHospitalNumber,
    string? consultationReason,
    bool assignedToMe);

// ---- Generic envelope for errors ----

public record ApiError(string Message, string? Code = null);
