using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Maternity;

namespace ThriveHealth.Web.Services;

public interface IMaternityService
{
    Task<string> GenerateAncNumberAsync(int facilityId, CancellationToken ct = default);
    int? CalculateGestationalAgeWeeks(DateOnly? lmp, DateOnly visitDate);
    DateOnly? CalculateEdd(DateOnly? lmp);
    Task<int> CreateAnteNatalRecordAsync(AnteNatalRecord record, CancellationToken ct = default);
}

public class MaternityService : IMaternityService
{
    private readonly ApplicationDbContext _db;
    public MaternityService(ApplicationDbContext db) { _db = db; }

    public async Task<string> GenerateAncNumberAsync(int facilityId, CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"ANC-{year}-";
        var last = await _db.AnteNatalRecords
            .Where(r => r.FacilityId == facilityId && r.AncNumber.StartsWith(prefix))
            .OrderByDescending(r => r.AncNumber)
            .Select(r => r.AncNumber)
            .FirstOrDefaultAsync(ct);
        var next = 1;
        if (!string.IsNullOrEmpty(last))
        {
            var tail = last.Substring(prefix.Length);
            if (int.TryParse(tail, out var n)) next = n + 1;
        }
        return $"{prefix}{next:D5}";
    }

    public int? CalculateGestationalAgeWeeks(DateOnly? lmp, DateOnly visitDate)
    {
        if (lmp is null) return null;
        var days = visitDate.DayNumber - lmp.Value.DayNumber;
        if (days < 0) return null;
        return days / 7;
    }

    public DateOnly? CalculateEdd(DateOnly? lmp) =>
        lmp?.AddDays(280); // Naegele's rule

    public async Task<int> CreateAnteNatalRecordAsync(AnteNatalRecord record, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(record.AncNumber))
            record.AncNumber = await GenerateAncNumberAsync(record.FacilityId, ct);
        if (record.Edd is null && record.Lmp is not null)
            record.Edd = CalculateEdd(record.Lmp);
        _db.AnteNatalRecords.Add(record);
        await _db.SaveChangesAsync(ct);
        return record.Id;
    }
}
