using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;

namespace ThriveHealth.Web.Services;

public interface IBatch13Numbering
{
    Task<string> NextDialysisAsync(int facilityId, CancellationToken ct = default);
    Task<string> NextDonorAsync(int facilityId, CancellationToken ct = default);
    Task<string> NextBloodUnitAsync(int facilityId, CancellationToken ct = default);
    Task<string> NextCrossMatchAsync(int facilityId, CancellationToken ct = default);
    Task<string> NextMortuaryAsync(int facilityId, CancellationToken ct = default);
    Task<string> NextAlliedAsync(int facilityId, string prefix, CancellationToken ct = default);
}

public class Batch13Numbering : IBatch13Numbering
{
    private readonly ApplicationDbContext _db;
    public Batch13Numbering(ApplicationDbContext db) { _db = db; }

    private static int ParseSeq(string? last, string prefix)
    {
        if (string.IsNullOrEmpty(last)) return 1;
        var tail = last.Substring(prefix.Length);
        return int.TryParse(tail, out var n) ? n + 1 : 1;
    }

    public async Task<string> NextDialysisAsync(int facilityId, CancellationToken ct = default)
    {
        var prefix = $"HD-{DateTime.UtcNow.Year}-";
        var last = await _db.DialysisSessions
            .Where(s => s.FacilityId == facilityId && s.SessionNumber.StartsWith(prefix))
            .OrderByDescending(s => s.SessionNumber).Select(s => s.SessionNumber).FirstOrDefaultAsync(ct);
        return $"{prefix}{ParseSeq(last, prefix):D5}";
    }

    public async Task<string> NextDonorAsync(int facilityId, CancellationToken ct = default)
    {
        var prefix = $"DON-{DateTime.UtcNow.Year}-";
        var last = await _db.BloodDonors
            .Where(s => s.FacilityId == facilityId && s.DonorNumber.StartsWith(prefix))
            .OrderByDescending(s => s.DonorNumber).Select(s => s.DonorNumber).FirstOrDefaultAsync(ct);
        return $"{prefix}{ParseSeq(last, prefix):D5}";
    }

    public async Task<string> NextBloodUnitAsync(int facilityId, CancellationToken ct = default)
    {
        var prefix = $"BU-{DateTime.UtcNow.Year}-";
        var last = await _db.BloodUnits
            .Where(s => s.FacilityId == facilityId && s.UnitNumber.StartsWith(prefix))
            .OrderByDescending(s => s.UnitNumber).Select(s => s.UnitNumber).FirstOrDefaultAsync(ct);
        return $"{prefix}{ParseSeq(last, prefix):D5}";
    }

    public async Task<string> NextCrossMatchAsync(int facilityId, CancellationToken ct = default)
    {
        var prefix = $"XM-{DateTime.UtcNow.Year}-";
        var last = await _db.BloodCrossMatches
            .Where(s => s.FacilityId == facilityId && s.CrossMatchNumber.StartsWith(prefix))
            .OrderByDescending(s => s.CrossMatchNumber).Select(s => s.CrossMatchNumber).FirstOrDefaultAsync(ct);
        return $"{prefix}{ParseSeq(last, prefix):D5}";
    }

    public async Task<string> NextMortuaryAsync(int facilityId, CancellationToken ct = default)
    {
        var prefix = $"MOR-{DateTime.UtcNow.Year}-";
        var last = await _db.MortuaryEntries
            .Where(s => s.FacilityId == facilityId && s.MortuaryNumber.StartsWith(prefix))
            .OrderByDescending(s => s.MortuaryNumber).Select(s => s.MortuaryNumber).FirstOrDefaultAsync(ct);
        return $"{prefix}{ParseSeq(last, prefix):D5}";
    }

    public async Task<string> NextAlliedAsync(int facilityId, string prefix, CancellationToken ct = default)
    {
        var fullPrefix = $"{prefix}-{DateTime.UtcNow.Year}-";
        var last = await _db.AlliedSessions
            .Where(s => s.FacilityId == facilityId && s.SessionNumber.StartsWith(fullPrefix))
            .OrderByDescending(s => s.SessionNumber).Select(s => s.SessionNumber).FirstOrDefaultAsync(ct);
        return $"{fullPrefix}{ParseSeq(last, fullPrefix):D5}";
    }
}
