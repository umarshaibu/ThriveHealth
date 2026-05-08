using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Emergency;

namespace ThriveHealth.Web.Services;

public record TriageRequest(
    int FacilityId,
    int PatientId,
    string TriagedById,
    TriageColour Colour,
    ArrivalMode Arrival,
    string ChiefComplaint,
    bool IsTrauma,
    string? MechanismOfInjury,
    AvpuLevel? Avpu,
    int? GcsTotal,
    bool IsPregnant,
    DateTime? LastMealUtc,
    bool IsForensicCase,
    ForensicCategory ForensicCategory,
    string? PoliceReportNumber,
    string? AccompanyingPerson,
    string? KnownAllergies,
    string? CurrentMedications);

public interface ITriageService
{
    Task<Encounter> TriageAsync(TriageRequest req, CancellationToken ct = default);
    Task<bool> AssignToBayAsync(int encounterId, int bayId, string userId, CancellationToken ct = default);
    Task<bool> ReleaseBayAsync(int encounterId, string userId, CancellationToken ct = default);
}

public class TriageService : ITriageService
{
    private readonly ApplicationDbContext _db;
    public TriageService(ApplicationDbContext db) => _db = db;

    public async Task<Encounter> TriageAsync(TriageRequest req, CancellationToken ct = default)
    {
        var aeClinicId = await _db.Clinics
            .Where(c => c.FacilityId == req.FacilityId && c.Code == "AE")
            .Select(c => c.Id)
            .FirstAsync(ct);

        var encounter = new Encounter
        {
            FacilityId = req.FacilityId,
            PatientId = req.PatientId,
            ClinicId = aeClinicId,
            ClinicianId = req.TriagedById,
            Type = EncounterType.Emergency,
            Status = EncounterStatus.InProgress,
            ChiefComplaint = req.ChiefComplaint,
            StartedAt = DateTime.UtcNow,
            Soap = new SoapNote()
        };

        var triage = new TriageAssessment
        {
            Encounter = encounter,
            Colour = req.Colour,
            ArrivalMode = req.Arrival,
            ChiefComplaint = req.ChiefComplaint,
            IsTrauma = req.IsTrauma,
            MechanismOfInjury = req.MechanismOfInjury,
            Avpu = req.Avpu,
            GcsTotal = req.GcsTotal,
            IsPregnant = req.IsPregnant,
            LastMealUtc = req.LastMealUtc,
            IsForensicCase = req.IsForensicCase,
            ForensicCategory = req.ForensicCategory,
            PoliceReportNumber = req.PoliceReportNumber,
            AccompanyingPerson = req.AccompanyingPerson,
            KnownAllergies = req.KnownAllergies,
            CurrentMedications = req.CurrentMedications,
            TriagedAt = DateTime.UtcNow,
            TriagedById = req.TriagedById,
            TargetSeenByUtc = DateTime.UtcNow.AddMinutes(TriageAssessment.TargetMinutesFor(req.Colour))
        };

        _db.Encounters.Add(encounter);
        _db.TriageAssessments.Add(triage);
        await _db.SaveChangesAsync(ct);
        return encounter;
    }

    public async Task<bool> AssignToBayAsync(int encounterId, int bayId, string userId, CancellationToken ct = default)
    {
        var enc = await _db.Encounters.FirstOrDefaultAsync(e => e.Id == encounterId, ct);
        if (enc is null) return false;

        var existingInBay = await _db.Encounters.FirstOrDefaultAsync(
            e => e.ResusBayId == bayId && e.Status == EncounterStatus.InProgress && e.Id != encounterId, ct);
        if (existingInBay is not null) return false;

        enc.ResusBayId = bayId;
        enc.ResusStartedAt ??= DateTime.UtcNow;

        _db.ResusEvents.Add(new ResusEvent
        {
            EncounterId = enc.Id,
            Kind = ResusEventKind.HandoverIn,
            Description = "Patient transferred to resuscitation bay",
            AtUtc = DateTime.UtcNow,
            RecordedById = userId
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ReleaseBayAsync(int encounterId, string userId, CancellationToken ct = default)
    {
        var enc = await _db.Encounters.FirstOrDefaultAsync(e => e.Id == encounterId, ct);
        if (enc is null) return false;
        enc.ResusBayId = null;
        enc.ResusEndedAt = DateTime.UtcNow;
        _db.ResusEvents.Add(new ResusEvent
        {
            EncounterId = enc.Id,
            Kind = ResusEventKind.HandoverOut,
            Description = "Patient released from resuscitation bay",
            AtUtc = DateTime.UtcNow,
            RecordedById = userId
        });
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
