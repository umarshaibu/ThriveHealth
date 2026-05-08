using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Immunization;

namespace ThriveHealth.Web.Services;

public record ImmunizationCardRow(
    int VaccineId,
    string VaccineCode,
    string VaccineName,
    string DoseLabel,
    int RecommendedAgeWeeks,
    DateOnly DueDate,
    DateTime? AdministeredAt,
    DoseStatus Status,
    string? BatchNumber,
    int? ExistingDoseId
);

public interface IImmunizationService
{
    Task<int> EnsureScheduleForPatientAsync(int facilityId, int patientId, DateOnly dateOfBirth, CancellationToken ct = default);
    Task<List<ImmunizationCardRow>> GetCardAsync(int patientId, DateOnly dateOfBirth, CancellationToken ct = default);
    Task<int> AdministerAsync(int doseId, string? batch, DateOnly? expiry, string? site, string? notes, string userId, CancellationToken ct = default);
    Task RefreshDueStatusAsync(int facilityId, CancellationToken ct = default);
}

public class ImmunizationService : IImmunizationService
{
    private readonly ApplicationDbContext _db;
    public ImmunizationService(ApplicationDbContext db) { _db = db; }

    public async Task<int> EnsureScheduleForPatientAsync(int facilityId, int patientId, DateOnly dateOfBirth, CancellationToken ct = default)
    {
        var schedules = await _db.VaccineSchedules
            .Include(s => s.Vaccine)
            .Where(s => s.Vaccine!.IsActive)
            .OrderBy(s => s.Vaccine!.SortOrder).ThenBy(s => s.SortOrder)
            .ToListAsync(ct);

        var existing = await _db.ImmunizationDoses
            .Where(d => d.PatientId == patientId)
            .Select(d => new { d.VaccineId, d.DoseLabel })
            .ToListAsync(ct);
        var seen = existing.Select(x => (x.VaccineId, x.DoseLabel)).ToHashSet();

        var added = 0;
        foreach (var s in schedules)
        {
            if (seen.Contains((s.VaccineId, s.DoseLabel))) continue;
            _db.ImmunizationDoses.Add(new ImmunizationDose
            {
                FacilityId = facilityId,
                PatientId = patientId,
                VaccineId = s.VaccineId,
                VaccineScheduleId = s.Id,
                DoseLabel = s.DoseLabel,
                DueDate = dateOfBirth.AddDays(s.RecommendedAgeWeeks * 7),
                Status = DoseStatus.Due
            });
            added++;
        }
        if (added > 0) await _db.SaveChangesAsync(ct);
        return added;
    }

    public async Task<List<ImmunizationCardRow>> GetCardAsync(int patientId, DateOnly dateOfBirth, CancellationToken ct = default)
    {
        var schedules = await _db.VaccineSchedules
            .Include(s => s.Vaccine)
            .Where(s => s.Vaccine!.IsActive)
            .OrderBy(s => s.Vaccine!.SortOrder).ThenBy(s => s.SortOrder)
            .ToListAsync(ct);

        var doses = await _db.ImmunizationDoses
            .Where(d => d.PatientId == patientId)
            .ToDictionaryAsync(d => (d.VaccineId, d.DoseLabel), ct);

        return schedules.Select(s =>
        {
            doses.TryGetValue((s.VaccineId, s.DoseLabel), out var d);
            return new ImmunizationCardRow(
                s.VaccineId,
                s.Vaccine!.Code,
                s.Vaccine.Name,
                s.DoseLabel,
                s.RecommendedAgeWeeks,
                d?.DueDate ?? dateOfBirth.AddDays(s.RecommendedAgeWeeks * 7),
                d?.AdministeredAt,
                d?.Status ?? DoseStatus.Due,
                d?.BatchNumber,
                d?.Id
            );
        }).ToList();
    }

    public async Task<int> AdministerAsync(int doseId, string? batch, DateOnly? expiry, string? site, string? notes, string userId, CancellationToken ct = default)
    {
        var dose = await _db.ImmunizationDoses.FirstOrDefaultAsync(d => d.Id == doseId, ct);
        if (dose is null) throw new InvalidOperationException("Dose not found.");
        if (dose.Status == DoseStatus.Administered) return dose.Id;

        dose.AdministeredAt = DateTime.UtcNow;
        dose.AdministeredById = userId;
        dose.BatchNumber = batch;
        dose.ExpiryDate = expiry;
        dose.Site = site;
        dose.Notes = notes;
        dose.Status = DoseStatus.Administered;
        await _db.SaveChangesAsync(ct);
        return dose.Id;
    }

    public async Task RefreshDueStatusAsync(int facilityId, CancellationToken ct = default)
    {
        // Mark Due doses past due date as still Due (no auto-miss); reserved for cron job in Batch 16.
        await Task.CompletedTask;
    }
}
