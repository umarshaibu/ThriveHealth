using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Scheduling;
using ThriveHealth.Web.Models.Telemedicine;

namespace ThriveHealth.Web.Services;

/// <summary>
/// Wraps the in-room consult actions a clinician can take during a tele-session: prescribe, order
/// labs / imaging, issue a sick note, refer to a specialist, book a follow-up. Each action is scoped
/// to the encounter the clinician opened on join — so everything lands on the patient's clinical
/// record automatically.
/// </summary>
public interface ITeleConsultActions
{
    Task<int?> AddPrescriptionItemAsync(int sessionId, string clinicianId, PrescriptionItemInput input, CancellationToken ct = default);
    Task<int?> CreateLabOrderAsync(int sessionId, string clinicianId, LabOrderInput input, CancellationToken ct = default);
    Task<int?> CreateImagingOrderAsync(int sessionId, string clinicianId, ImagingOrderInput input, CancellationToken ct = default);
    Task<int?> IssueMedicalCertificateAsync(int sessionId, string clinicianId, MedicalCertificateInput input, CancellationToken ct = default);
    Task<int?> CreateReferralAsync(int sessionId, string clinicianId, ReferralInput input, CancellationToken ct = default);
    Task<int?> BookFollowUpAsync(int sessionId, string clinicianId, FollowUpInput input, CancellationToken ct = default);
}

public record PrescriptionItemInput(int? DrugId, string DrugName, string? Dose, string? Route, string? Frequency, string? Duration, int? Quantity, string? Instructions);
public record LabOrderInput(int? LabTestId, string TestName, string? ClinicalIndication, OrderUrgency Urgency);
public record ImagingOrderInput(ImagingModality Modality, string StudyDescription, string? ClinicalIndication, OrderUrgency Urgency);
public record MedicalCertificateInput(DateOnly StartDate, DateOnly EndDate, string? Diagnosis, string? IcdCode, string? Recommendations);
public record ReferralInput(string? ReferredToClinicianId, string? Specialty, string? Reason, string? ClinicalSummary);
public record FollowUpInput(DateTime ScheduledStartUtc, AppointmentType Type, int DurationMinutes, string? ReasonForVisit);

public class TeleConsultActions : ITeleConsultActions
{
    private readonly ApplicationDbContext _db;
    public TeleConsultActions(ApplicationDbContext db) { _db = db; }

    private async Task<TeleSession?> LoadSessionAsync(int sessionId, string clinicianId, CancellationToken ct) =>
        await _db.TeleSessions.Include(s => s.Encounter)
            .FirstOrDefaultAsync(s => s.Id == sessionId && s.ClinicianId == clinicianId, ct);

    public async Task<int?> AddPrescriptionItemAsync(int sessionId, string clinicianId, PrescriptionItemInput input, CancellationToken ct = default)
    {
        var session = await LoadSessionAsync(sessionId, clinicianId, ct);
        if (session?.Encounter is null) return null;

        // Reuse the encounter's open prescription so multiple items group on one Rx (single signature).
        var rx = await _db.Prescriptions.Include(p => p.Items)
            .FirstOrDefaultAsync(p => p.EncounterId == session.EncounterId && p.Status == PrescriptionStatus.Issued, ct);
        if (rx is null)
        {
            rx = new Prescription
            {
                EncounterId = session.EncounterId!.Value,
                PatientId = session.PatientId,
                Status = PrescriptionStatus.Issued,
                IssuedAt = DateTime.UtcNow,
                PrescribedById = clinicianId
            };
            _db.Prescriptions.Add(rx);
            await _db.SaveChangesAsync(ct);
        }

        var drug = input.DrugId.HasValue ? await _db.Drugs.AsNoTracking().FirstOrDefaultAsync(d => d.Id == input.DrugId.Value, ct) : null;
        var item = new PrescriptionItem
        {
            PrescriptionId = rx.Id,
            DrugId = drug?.Id,
            DrugName = drug?.Display ?? input.DrugName,
            NafdacNumber = drug?.NafdacNumber,
            Dose = input.Dose,
            Route = input.Route,
            Frequency = input.Frequency,
            Duration = input.Duration,
            Quantity = input.Quantity,
            Instructions = input.Instructions,
            IsControlled = drug?.IsControlled ?? false
        };
        _db.Set<PrescriptionItem>().Add(item);
        await _db.SaveChangesAsync(ct);
        return item.Id;
    }

    public async Task<int?> CreateLabOrderAsync(int sessionId, string clinicianId, LabOrderInput input, CancellationToken ct = default)
    {
        var session = await LoadSessionAsync(sessionId, clinicianId, ct);
        if (session?.Encounter is null) return null;

        var test = input.LabTestId.HasValue ? await _db.LabTests.AsNoTracking().FirstOrDefaultAsync(t => t.Id == input.LabTestId.Value, ct) : null;
        var order = new LabOrder
        {
            EncounterId = session.EncounterId!.Value,
            PatientId = session.PatientId,
            LabTestId = test?.Id,
            TestName = test?.Name ?? input.TestName,
            LoincCode = test?.LoincCode,
            Specimen = test?.Specimen,
            Urgency = input.Urgency,
            ClinicalIndication = input.ClinicalIndication,
            OrderedAt = DateTime.UtcNow,
            OrderedById = clinicianId
        };
        _db.LabOrders.Add(order);
        await _db.SaveChangesAsync(ct);
        return order.Id;
    }

    public async Task<int?> CreateImagingOrderAsync(int sessionId, string clinicianId, ImagingOrderInput input, CancellationToken ct = default)
    {
        var session = await LoadSessionAsync(sessionId, clinicianId, ct);
        if (session?.Encounter is null) return null;

        var order = new ImagingOrder
        {
            EncounterId = session.EncounterId!.Value,
            PatientId = session.PatientId,
            Modality = input.Modality,
            StudyDescription = input.StudyDescription,
            ClinicalIndication = input.ClinicalIndication,
            Urgency = input.Urgency,
            OrderedAt = DateTime.UtcNow,
            OrderedById = clinicianId
        };
        _db.ImagingOrders.Add(order);
        await _db.SaveChangesAsync(ct);
        return order.Id;
    }

    public async Task<int?> IssueMedicalCertificateAsync(int sessionId, string clinicianId, MedicalCertificateInput input, CancellationToken ct = default)
    {
        var session = await LoadSessionAsync(sessionId, clinicianId, ct);
        if (session?.Encounter is null) return null;
        if (input.EndDate < input.StartDate) return null;

        var cert = new MedicalCertificate
        {
            FacilityId = session.FacilityId,
            CertificateNumber = await NextCertNumberAsync(session.FacilityId, ct),
            EncounterId = session.EncounterId!.Value,
            PatientId = session.PatientId,
            IssuedById = clinicianId,
            StartDate = input.StartDate,
            EndDate = input.EndDate,
            Diagnosis = input.Diagnosis,
            IcdCode = input.IcdCode,
            Recommendations = input.Recommendations,
            IssuedAt = DateTime.UtcNow
        };
        _db.MedicalCertificates.Add(cert);
        await _db.SaveChangesAsync(ct);
        return cert.Id;
    }

    public async Task<int?> CreateReferralAsync(int sessionId, string clinicianId, ReferralInput input, CancellationToken ct = default)
    {
        var session = await LoadSessionAsync(sessionId, clinicianId, ct);
        if (session?.Encounter is null) return null;

        var referral = new Referral
        {
            FacilityId = session.FacilityId,
            ReferralNumber = await NextReferralNumberAsync(session.FacilityId, ct),
            EncounterId = session.EncounterId!.Value,
            PatientId = session.PatientId,
            ReferringClinicianId = clinicianId,
            ReferredToClinicianId = input.ReferredToClinicianId,
            Specialty = input.Specialty,
            Reason = input.Reason,
            ClinicalSummary = input.ClinicalSummary,
            Status = ReferralStatus.Sent,
            CreatedAt = DateTime.UtcNow
        };
        _db.Referrals.Add(referral);
        await _db.SaveChangesAsync(ct);
        return referral.Id;
    }

    public async Task<int?> BookFollowUpAsync(int sessionId, string clinicianId, FollowUpInput input, CancellationToken ct = default)
    {
        var session = await LoadSessionAsync(sessionId, clinicianId, ct);
        if (session is null) return null;

        // Pick the same clinic the encounter ran in if we have one, else the facility's OPD/Telemed clinic.
        var clinicId = session.Encounter?.ClinicId ?? await _db.Clinics.AsNoTracking()
            .Where(c => c.FacilityId == session.FacilityId)
            .OrderBy(c => c.Code == "OPD" ? 0 : 1)
            .Select(c => (int?)c.Id).FirstOrDefaultAsync(ct);
        if (clinicId is null) return null;

        var appt = new Appointment
        {
            FacilityId = session.FacilityId,
            PatientId = session.PatientId,
            ClinicId = clinicId.Value,
            ClinicianId = clinicianId,
            Type = input.Type,
            Status = AppointmentStatus.Scheduled,
            Channel = BookingChannel.FrontDesk,
            ScheduledStartUtc = DateTime.SpecifyKind(input.ScheduledStartUtc, DateTimeKind.Utc),
            DurationMinutes = input.DurationMinutes <= 0 ? 15 : input.DurationMinutes,
            ReasonForVisit = input.ReasonForVisit,
            BookedById = clinicianId
        };
        _db.Appointments.Add(appt);
        await _db.SaveChangesAsync(ct);
        return appt.Id;
    }

    private async Task<string> NextCertNumberAsync(int facilityId, CancellationToken ct)
    {
        var prefix = $"MC-{DateTime.UtcNow.Year}-";
        var last = await _db.MedicalCertificates
            .Where(c => c.FacilityId == facilityId && c.CertificateNumber.StartsWith(prefix))
            .OrderByDescending(c => c.CertificateNumber).Select(c => c.CertificateNumber).FirstOrDefaultAsync(ct);
        var next = 1;
        if (!string.IsNullOrEmpty(last) && int.TryParse(last.Substring(prefix.Length), out var n)) next = n + 1;
        return $"{prefix}{next:D5}";
    }

    private async Task<string> NextReferralNumberAsync(int facilityId, CancellationToken ct)
    {
        var prefix = $"REF-{DateTime.UtcNow.Year}-";
        var last = await _db.Referrals
            .Where(r => r.FacilityId == facilityId && r.ReferralNumber.StartsWith(prefix))
            .OrderByDescending(r => r.ReferralNumber).Select(r => r.ReferralNumber).FirstOrDefaultAsync(ct);
        var next = 1;
        if (!string.IsNullOrEmpty(last) && int.TryParse(last.Substring(prefix.Length), out var n)) next = n + 1;
        return $"{prefix}{next:D5}";
    }
}
