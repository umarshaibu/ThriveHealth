using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;

namespace ThriveHealth.Web.Services;

public interface IHospitalNumberGenerator
{
    Task<string> NextAsync(int facilityId, CancellationToken ct = default);
}

public class HospitalNumberGenerator : IHospitalNumberGenerator
{
    private readonly ApplicationDbContext _db;

    public HospitalNumberGenerator(ApplicationDbContext db) => _db = db;

    public async Task<string> NextAsync(int facilityId, CancellationToken ct = default)
    {
        var facility = await _db.Facilities.AsNoTracking()
            .FirstOrDefaultAsync(f => f.Id == facilityId, ct)
            ?? throw new InvalidOperationException("Facility not found.");

        var prefix = string.IsNullOrWhiteSpace(facility.HospitalNumberPrefix)
            ? facility.Code
            : facility.HospitalNumberPrefix!;

        var year = DateTime.UtcNow.Year;
        var counter = await _db.HospitalNumberCounters
            .FirstOrDefaultAsync(c => c.FacilityId == facilityId && c.Year == year, ct);

        if (counter is null)
        {
            counter = new HospitalNumberCounter { FacilityId = facilityId, Year = year, LastSequence = 0 };
            _db.HospitalNumberCounters.Add(counter);
        }

        counter.LastSequence += 1;
        await _db.SaveChangesAsync(ct);

        return $"{prefix}/{year}/{counter.LastSequence:D6}";
    }
}
