using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Hubs;
using ThriveHealth.Web.Models.Scheduling;

namespace ThriveHealth.Web.Services;

public interface IQueueService
{
    Task<QueueEntry> CheckInAsync(int facilityId, int patientId, int clinicId, string? clinicianId,
        AppointmentPriority priority, string? complaint, int? appointmentId, string? userId, CancellationToken ct = default);

    Task TriageAsync(int queueEntryId, AppointmentPriority priority, int? mews, string? notes, string? userId, CancellationToken ct = default);
    Task CallAsync(int queueEntryId, string? userId, CancellationToken ct = default);
    Task StartConsultationAsync(int queueEntryId, string? userId, CancellationToken ct = default);
    Task CompleteAsync(int queueEntryId, string? userId, CancellationToken ct = default);
    Task SkipAsync(int queueEntryId, string? userId, CancellationToken ct = default);
    Task<string> NextTicketNumberAsync(int facilityId, int clinicId, string clinicCode, CancellationToken ct = default);
}

public class QueueService : IQueueService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<QueueHub> _hub;

    public QueueService(ApplicationDbContext db, IHubContext<QueueHub> hub)
    {
        _db = db;
        _hub = hub;
    }

    public async Task<string> NextTicketNumberAsync(int facilityId, int clinicId, string clinicCode, CancellationToken ct = default)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var counter = await _db.TicketCounters
            .FirstOrDefaultAsync(c => c.FacilityId == facilityId && c.ClinicId == clinicId && c.Date == today, ct);
        if (counter is null)
        {
            counter = new TicketCounter { FacilityId = facilityId, ClinicId = clinicId, Date = today, LastSequence = 0 };
            _db.TicketCounters.Add(counter);
        }
        counter.LastSequence += 1;
        await _db.SaveChangesAsync(ct);
        return $"{clinicCode}-{counter.LastSequence:D3}";
    }

    public async Task<QueueEntry> CheckInAsync(
        int facilityId, int patientId, int clinicId, string? clinicianId,
        AppointmentPriority priority, string? complaint, int? appointmentId, string? userId, CancellationToken ct = default)
    {
        var clinic = await _db.Clinics.AsNoTracking().FirstAsync(c => c.Id == clinicId, ct);
        var ticket = await NextTicketNumberAsync(facilityId, clinicId, clinic.Code, ct);

        var entry = new QueueEntry
        {
            FacilityId = facilityId,
            PatientId = patientId,
            ClinicId = clinicId,
            ClinicianId = clinicianId,
            AppointmentId = appointmentId,
            TicketNumber = ticket,
            TicketDate = DateOnly.FromDateTime(DateTime.UtcNow),
            Priority = priority,
            Status = QueueStatus.Waiting,
            CheckedInAt = DateTime.UtcNow,
            CheckedInById = userId,
            Complaint = complaint
        };
        _db.QueueEntries.Add(entry);

        if (appointmentId.HasValue)
        {
            var appt = await _db.Appointments.FindAsync(new object[] { appointmentId.Value }, ct);
            if (appt is not null)
            {
                appt.Status = AppointmentStatus.CheckedIn;
                appt.CheckedInAt = DateTime.UtcNow;
                appt.UpdatedAt = DateTime.UtcNow;
            }
        }
        await _db.SaveChangesAsync(ct);
        await PublishUpdate(facilityId, clinicId);
        return entry;
    }

    public async Task TriageAsync(int queueEntryId, AppointmentPriority priority, int? mews, string? notes, string? userId, CancellationToken ct = default)
    {
        var entry = await _db.QueueEntries.FindAsync(new object[] { queueEntryId }, ct);
        if (entry is null) return;
        entry.Priority = priority;
        entry.TriageMews = mews;
        entry.TriageNotes = notes;
        entry.TriagedById = userId;
        entry.TriagedAt = DateTime.UtcNow;
        entry.Status = QueueStatus.Triaged;
        await _db.SaveChangesAsync(ct);
        await PublishUpdate(entry.FacilityId, entry.ClinicId);
    }

    public async Task CallAsync(int queueEntryId, string? userId, CancellationToken ct = default)
    {
        var entry = await _db.QueueEntries.FindAsync(new object[] { queueEntryId }, ct);
        if (entry is null) return;
        entry.Status = QueueStatus.Called;
        entry.CalledAt = DateTime.UtcNow;
        if (userId is not null) entry.ClinicianId = userId;
        await _db.SaveChangesAsync(ct);
        await PublishUpdate(entry.FacilityId, entry.ClinicId);
    }

    public async Task StartConsultationAsync(int queueEntryId, string? userId, CancellationToken ct = default)
    {
        var entry = await _db.QueueEntries
            .Include(e => e.Appointment)
            .FirstOrDefaultAsync(e => e.Id == queueEntryId, ct);
        if (entry is null) return;
        entry.Status = QueueStatus.InConsultation;
        entry.ConsultStartedAt = DateTime.UtcNow;
        if (userId is not null) entry.ClinicianId = userId;
        if (entry.Appointment is not null)
        {
            entry.Appointment.Status = AppointmentStatus.InProgress;
            entry.Appointment.StartedAt = DateTime.UtcNow;
            entry.Appointment.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        await PublishUpdate(entry.FacilityId, entry.ClinicId);
    }

    public async Task CompleteAsync(int queueEntryId, string? userId, CancellationToken ct = default)
    {
        var entry = await _db.QueueEntries
            .Include(e => e.Appointment)
            .FirstOrDefaultAsync(e => e.Id == queueEntryId, ct);
        if (entry is null) return;
        entry.Status = QueueStatus.Completed;
        entry.CompletedAt = DateTime.UtcNow;
        if (entry.Appointment is not null)
        {
            entry.Appointment.Status = AppointmentStatus.Completed;
            entry.Appointment.CompletedAt = DateTime.UtcNow;
            entry.Appointment.UpdatedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
        await PublishUpdate(entry.FacilityId, entry.ClinicId);
    }

    public async Task SkipAsync(int queueEntryId, string? userId, CancellationToken ct = default)
    {
        var entry = await _db.QueueEntries.FindAsync(new object[] { queueEntryId }, ct);
        if (entry is null) return;
        entry.Status = QueueStatus.Skipped;
        await _db.SaveChangesAsync(ct);
        await PublishUpdate(entry.FacilityId, entry.ClinicId);
    }

    private Task PublishUpdate(int facilityId, int clinicId) =>
        _hub.Clients.Group($"queue-{facilityId}-{clinicId}").SendAsync("queueUpdated", new { facilityId, clinicId });
}
