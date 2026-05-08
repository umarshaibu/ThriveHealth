using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Scheduling;

namespace ThriveHealth.Web.Services;

public interface IEncounterService
{
    Task<Encounter> OpenFromQueueAsync(int queueEntryId, string clinicianId, CancellationToken ct = default);
    Task<Encounter> OpenAdHocAsync(int facilityId, int patientId, int clinicId, string clinicianId, EncounterType type, string? complaint, CancellationToken ct = default);
    Task SaveSoapAsync(int encounterId, string? subjective, string? objective, string? assessment, string? plan, string? userId, CancellationToken ct = default);
    Task<EncounterDiagnosis> AddDiagnosisAsync(int encounterId, string icdCode, string description, DiagnosisStatus status, bool isPrimary, string? userId, CancellationToken ct = default);
    Task RemoveDiagnosisAsync(int diagnosisId, CancellationToken ct = default);
    Task<bool> SignOffAsync(int encounterId, string clinicianId, CancellationToken ct = default);
}

public class EncounterService : IEncounterService
{
    private readonly ApplicationDbContext _db;
    private readonly IQueueService _queue;

    public EncounterService(ApplicationDbContext db, IQueueService queue)
    {
        _db = db;
        _queue = queue;
    }

    public async Task<Encounter> OpenFromQueueAsync(int queueEntryId, string clinicianId, CancellationToken ct = default)
    {
        var existing = await _db.Encounters
            .FirstOrDefaultAsync(e => e.QueueEntryId == queueEntryId && e.Status == EncounterStatus.InProgress, ct);
        if (existing is not null) return existing;

        var qe = await _db.QueueEntries
            .Include(q => q.Clinic)
            .FirstAsync(q => q.Id == queueEntryId, ct);

        var enc = new Encounter
        {
            FacilityId = qe.FacilityId,
            PatientId = qe.PatientId,
            QueueEntryId = qe.Id,
            AppointmentId = qe.AppointmentId,
            ClinicId = qe.ClinicId,
            ClinicianId = clinicianId,
            Type = MapClinicToType(qe.Clinic!.Specialty),
            Status = EncounterStatus.InProgress,
            ChiefComplaint = qe.Complaint,
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Soap = new SoapNote()
        };
        _db.Encounters.Add(enc);
        await _db.SaveChangesAsync(ct);

        if (qe.Status != QueueStatus.InConsultation)
            await _queue.StartConsultationAsync(qe.Id, clinicianId, ct);

        return enc;
    }

    public async Task<Encounter> OpenAdHocAsync(int facilityId, int patientId, int clinicId, string clinicianId, EncounterType type, string? complaint, CancellationToken ct = default)
    {
        var enc = new Encounter
        {
            FacilityId = facilityId,
            PatientId = patientId,
            ClinicId = clinicId,
            ClinicianId = clinicianId,
            Type = type,
            Status = EncounterStatus.InProgress,
            ChiefComplaint = complaint,
            Soap = new SoapNote()
        };
        _db.Encounters.Add(enc);
        await _db.SaveChangesAsync(ct);
        return enc;
    }

    public async Task SaveSoapAsync(int encounterId, string? subjective, string? objective, string? assessment, string? plan, string? userId, CancellationToken ct = default)
    {
        var enc = await _db.Encounters.Include(e => e.Soap).FirstAsync(e => e.Id == encounterId, ct);
        enc.Soap ??= new SoapNote { EncounterId = encounterId };
        enc.Soap.Subjective = subjective;
        enc.Soap.Objective = objective;
        enc.Soap.Assessment = assessment;
        enc.Soap.Plan = plan;
        enc.Soap.UpdatedAt = DateTime.UtcNow;
        enc.Soap.UpdatedById = userId;
        enc.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<EncounterDiagnosis> AddDiagnosisAsync(int encounterId, string icdCode, string description, DiagnosisStatus status, bool isPrimary, string? userId, CancellationToken ct = default)
    {
        if (isPrimary)
        {
            var existing = await _db.EncounterDiagnoses
                .Where(d => d.EncounterId == encounterId && d.IsPrimary)
                .ToListAsync(ct);
            foreach (var e in existing) e.IsPrimary = false;
        }

        var dx = new EncounterDiagnosis
        {
            EncounterId = encounterId,
            IcdCode = icdCode.Trim(),
            Description = description.Trim(),
            Status = status,
            IsPrimary = isPrimary,
            CreatedById = userId
        };
        _db.EncounterDiagnoses.Add(dx);
        await _db.SaveChangesAsync(ct);
        return dx;
    }

    public async Task RemoveDiagnosisAsync(int diagnosisId, CancellationToken ct = default)
    {
        var dx = await _db.EncounterDiagnoses.FindAsync(new object[] { diagnosisId }, ct);
        if (dx is null) return;
        _db.EncounterDiagnoses.Remove(dx);
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> SignOffAsync(int encounterId, string clinicianId, CancellationToken ct = default)
    {
        var enc = await _db.Encounters
            .Include(e => e.Diagnoses)
            .FirstAsync(e => e.Id == encounterId, ct);
        if (enc.Status != EncounterStatus.InProgress) return true;
        if (!enc.Diagnoses.Any()) return false;

        enc.Status = EncounterStatus.Signed;
        enc.SignedAt = DateTime.UtcNow;
        enc.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);

        if (enc.QueueEntryId.HasValue)
            await _queue.CompleteAsync(enc.QueueEntryId.Value, clinicianId, ct);

        return true;
    }

    private static EncounterType MapClinicToType(ClinicSpecialty s) => s switch
    {
        ClinicSpecialty.Antenatal => EncounterType.AntenatalVisit,
        ClinicSpecialty.Telemedicine => EncounterType.Telemedicine,
        _ => EncounterType.OutpatientOpd
    };
}
