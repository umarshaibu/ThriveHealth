using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Theatre;

namespace ThriveHealth.Web.Services;

public interface ITheatreService
{
    Task<int> CreateSessionAsync(int facilityId, int theatreId, int patientId, string leadSurgeonId,
        string? anaesthetistId, string? scrubNurseId, string procedureName, string? cptCode, string? indication,
        CaseUrgency urgency, AnaesthesiaType anaesthesia, DateTime scheduledStartUtc, int estimatedMinutes,
        string userId, CancellationToken ct = default);

    Task<bool> ConfirmChecklistItemAsync(int itemId, bool confirmed, string? notes, string userId, CancellationToken ct = default);
    Task<bool> AdvanceStatusAsync(int sessionId, TheatreSessionStatus newStatus, string userId, CancellationToken ct = default);
    Task LogEventAsync(int sessionId, SessionEventKind kind, string description, string? details, string userId, CancellationToken ct = default);
}

public class TheatreService : ITheatreService
{
    private readonly ApplicationDbContext _db;
    public TheatreService(ApplicationDbContext db) => _db = db;

    private static readonly (ChecklistPhase Phase, string Question)[] WhoChecklist = new[]
    {
        // SIGN IN — before induction of anaesthesia
        (ChecklistPhase.SignIn, "Patient identity confirmed (name, hospital number, DOB)"),
        (ChecklistPhase.SignIn, "Site marked / not applicable"),
        (ChecklistPhase.SignIn, "Anaesthesia safety check completed"),
        (ChecklistPhase.SignIn, "Pulse oximeter on patient and functioning"),
        (ChecklistPhase.SignIn, "Known allergies confirmed"),
        (ChecklistPhase.SignIn, "Difficult airway or aspiration risk identified"),
        (ChecklistPhase.SignIn, "Risk of >500ml blood loss (7ml/kg in children) identified, IVs/fluids planned"),
        (ChecklistPhase.SignIn, "Antibiotic prophylaxis given within last 60 minutes"),

        // TIME OUT — before skin incision
        (ChecklistPhase.TimeOut, "All team members introduced by name and role"),
        (ChecklistPhase.TimeOut, "Surgeon, anaesthetist, nurse confirm patient, site, procedure"),
        (ChecklistPhase.TimeOut, "Critical events anticipated: surgeon's plan / blood loss / specific equipment"),
        (ChecklistPhase.TimeOut, "Critical events anticipated: anaesthesia concerns / patient-specific issues"),
        (ChecklistPhase.TimeOut, "Critical events anticipated: nursing — sterility / equipment / consumables ready"),
        (ChecklistPhase.TimeOut, "Essential imaging displayed / not applicable"),

        // SIGN OUT — before patient leaves theatre
        (ChecklistPhase.SignOut, "Procedure name recorded"),
        (ChecklistPhase.SignOut, "Instrument, swab, and needle counts correct"),
        (ChecklistPhase.SignOut, "Specimen labelling confirmed (with patient name)"),
        (ChecklistPhase.SignOut, "Equipment problems addressed / none"),
        (ChecklistPhase.SignOut, "Surgeon, anaesthetist, nurse review post-op concerns and recovery plan")
    };

    public async Task<int> CreateSessionAsync(int facilityId, int theatreId, int patientId, string leadSurgeonId,
        string? anaesthetistId, string? scrubNurseId, string procedureName, string? cptCode, string? indication,
        CaseUrgency urgency, AnaesthesiaType anaesthesia, DateTime scheduledStartUtc, int estimatedMinutes,
        string userId, CancellationToken ct = default)
    {
        var session = new TheatreSession
        {
            FacilityId = facilityId,
            TheatreId = theatreId,
            PatientId = patientId,
            LeadSurgeonId = leadSurgeonId,
            AnaesthetistId = anaesthetistId,
            ScrubNurseId = scrubNurseId,
            ProcedureName = procedureName,
            CptCode = cptCode,
            Indication = indication,
            Urgency = urgency,
            Anaesthesia = anaesthesia,
            ScheduledStartUtc = DateTime.SpecifyKind(scheduledStartUtc, DateTimeKind.Utc),
            EstimatedMinutes = estimatedMinutes,
            Status = TheatreSessionStatus.Scheduled,
            CreatedById = userId
        };

        int order = 0;
        foreach (var (phase, question) in WhoChecklist)
        {
            session.Checklist.Add(new ChecklistItem
            {
                Phase = phase,
                Question = question,
                SortOrder = order++
            });
        }

        _db.TheatreSessions.Add(session);
        await _db.SaveChangesAsync(ct);
        return session.Id;
    }

    public async Task<bool> ConfirmChecklistItemAsync(int itemId, bool confirmed, string? notes, string userId, CancellationToken ct = default)
    {
        var item = await _db.TheatreChecklistItems.FirstOrDefaultAsync(x => x.Id == itemId, ct);
        if (item is null) return false;
        item.IsConfirmed = confirmed;
        item.Notes = notes;
        item.ConfirmedAt = confirmed ? DateTime.UtcNow : null;
        item.ConfirmedById = confirmed ? userId : null;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> AdvanceStatusAsync(int sessionId, TheatreSessionStatus newStatus, string userId, CancellationToken ct = default)
    {
        var s = await _db.TheatreSessions.FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (s is null) return false;

        var now = DateTime.UtcNow;
        s.Status = newStatus;
        switch (newStatus)
        {
            case TheatreSessionStatus.PreOp: s.PreOpAt ??= now; break;
            case TheatreSessionStatus.InTheatre: s.KnifeOnSkinAt ??= now; break;
            case TheatreSessionStatus.Recovery: s.KnifeOffSkinAt ??= now; s.RecoveryAt ??= now; break;
            case TheatreSessionStatus.Completed: s.CompletedAt ??= now; break;
        }

        _db.TheatreSessionEvents.Add(new SessionEvent
        {
            TheatreSessionId = sessionId,
            Kind = SessionEventKind.Phase,
            Description = $"Phase changed to {newStatus}",
            AtUtc = now,
            RecordedById = userId
        });

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task LogEventAsync(int sessionId, SessionEventKind kind, string description, string? details, string userId, CancellationToken ct = default)
    {
        _db.TheatreSessionEvents.Add(new SessionEvent
        {
            TheatreSessionId = sessionId,
            Kind = kind,
            Description = description,
            Details = details,
            AtUtc = DateTime.UtcNow,
            RecordedById = userId
        });
        await _db.SaveChangesAsync(ct);
    }
}
