using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Scheduling;

namespace ThriveHealth.Web.Services;

public record SlotDto(DateTime StartUtc, int DurationMinutes, string ClinicianId, string ClinicianName, int? RoomId);

public interface ISlotService
{
    Task<IReadOnlyList<SlotDto>> GetAvailableSlotsAsync(int clinicId, DateOnly date, CancellationToken ct = default);
}

public class SlotService : ISlotService
{
    private readonly ApplicationDbContext _db;

    public SlotService(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<SlotDto>> GetAvailableSlotsAsync(int clinicId, DateOnly date, CancellationToken ct = default)
    {
        var clinic = await _db.Clinics.AsNoTracking().FirstOrDefaultAsync(c => c.Id == clinicId, ct);
        if (clinic is null) return Array.Empty<SlotDto>();

        var dayOfWeek = date.DayOfWeek;
        var availabilities = await _db.ClinicianAvailabilities
            .Include(a => a.Clinician)
            .Where(a => a.ClinicId == clinicId && a.IsActive && a.DayOfWeek == dayOfWeek
                && (a.ValidFrom == null || a.ValidFrom <= date)
                && (a.ValidTo == null || a.ValidTo >= date))
            .ToListAsync(ct);

        if (availabilities.Count == 0) return Array.Empty<SlotDto>();

        var dayStartUtc = date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc);
        var dayEndUtc = dayStartUtc.AddDays(1);

        var booked = await _db.Appointments
            .Where(a => a.ScheduledStartUtc >= dayStartUtc && a.ScheduledStartUtc < dayEndUtc
                && a.ClinicId == clinicId
                && a.Status != AppointmentStatus.Cancelled
                && a.Status != AppointmentStatus.Rescheduled)
            .Select(a => new { a.ClinicianId, a.ScheduledStartUtc, a.DurationMinutes })
            .ToListAsync(ct);

        var clinicianIds = availabilities.Select(a => a.ClinicianId).Distinct().ToList();
        var timeOffs = await _db.ClinicianTimeOffs
            .Where(t => clinicianIds.Contains(t.ClinicianId)
                && t.StartUtc < dayEndUtc && t.EndUtc > dayStartUtc)
            .ToListAsync(ct);

        var slots = new List<SlotDto>();
        foreach (var avail in availabilities)
        {
            var slotMin = avail.SlotMinutesOverride ?? clinic.DefaultSlotMinutes;
            var startUtc = date.ToDateTime(avail.StartTime, DateTimeKind.Utc);
            var endUtc = date.ToDateTime(avail.EndTime, DateTimeKind.Utc);

            for (var t = startUtc; t.AddMinutes(slotMin) <= endUtc; t = t.AddMinutes(slotMin))
            {
                if (timeOffs.Any(o => o.ClinicianId == avail.ClinicianId && t < o.EndUtc && t.AddMinutes(slotMin) > o.StartUtc))
                    continue;

                if (booked.Any(b => b.ClinicianId == avail.ClinicianId
                    && t < b.ScheduledStartUtc.AddMinutes(b.DurationMinutes)
                    && t.AddMinutes(slotMin) > b.ScheduledStartUtc))
                    continue;

                slots.Add(new SlotDto(
                    t, slotMin,
                    avail.ClinicianId,
                    avail.Clinician?.FullName ?? "Clinician",
                    avail.RoomId));
            }
        }

        return slots
            .OrderBy(s => s.StartUtc)
            .ThenBy(s => s.ClinicianName)
            .ToList();
    }
}
