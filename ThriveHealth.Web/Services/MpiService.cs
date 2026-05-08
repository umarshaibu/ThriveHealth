using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Services;

public record MpiCandidate(Patient Patient, decimal Score, IReadOnlyList<string> Reasons);

public interface IMpiService
{
    Task<IReadOnlyList<MpiCandidate>> FindPotentialMatchesAsync(
        int facilityId,
        string firstName,
        string lastName,
        DateOnly? dob,
        string? phone,
        string? nin,
        int? excludePatientId = null,
        CancellationToken ct = default);
}

public class MpiService : IMpiService
{
    private readonly ApplicationDbContext _db;

    public MpiService(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<MpiCandidate>> FindPotentialMatchesAsync(
        int facilityId,
        string firstName,
        string lastName,
        DateOnly? dob,
        string? phone,
        string? nin,
        int? excludePatientId = null,
        CancellationToken ct = default)
    {
        var fn = (firstName ?? string.Empty).Trim().ToLowerInvariant();
        var ln = (lastName ?? string.Empty).Trim().ToLowerInvariant();
        var ph = NormalizePhone(phone);

        var fnPrefix = fn.Length >= 2 ? fn[..2] : fn;
        var lnPrefix = ln.Length >= 2 ? ln[..2] : ln;

        var query = _db.Patients
            .Where(p => p.FacilityId == facilityId && p.IsActive && !p.IsMergedAlias);

        if (excludePatientId.HasValue)
            query = query.Where(p => p.Id != excludePatientId.Value);

        var prefiltered = await query
            .Where(p =>
                (nin != null && p.Nin == nin) ||
                (ph != null && p.Phone != null && p.Phone.EndsWith(ph)) ||
                EF.Functions.ILike(p.LastName, lnPrefix + "%") ||
                EF.Functions.ILike(p.FirstName, fnPrefix + "%"))
            .Take(50)
            .ToListAsync(ct);

        var results = new List<MpiCandidate>();
        foreach (var p in prefiltered)
        {
            var (score, reasons) = Score(p, fn, ln, dob, ph, nin);
            if (score >= 50m)
                results.Add(new MpiCandidate(p, score, reasons));
        }

        return results.OrderByDescending(r => r.Score).Take(10).ToList();
    }

    private static (decimal score, List<string> reasons) Score(
        Patient p, string fn, string ln, DateOnly? dob, string? phone, string? nin)
    {
        decimal score = 0m;
        var reasons = new List<string>();

        if (!string.IsNullOrEmpty(nin) && string.Equals(p.Nin, nin, StringComparison.Ordinal))
        {
            score += 60m;
            reasons.Add("NIN match");
        }

        var pfn = (p.FirstName ?? string.Empty).ToLowerInvariant();
        var pln = (p.LastName ?? string.Empty).ToLowerInvariant();

        var lnSim = JaroWinkler(ln, pln);
        var fnSim = JaroWinkler(fn, pfn);
        score += (decimal)(lnSim * 25);
        score += (decimal)(fnSim * 20);
        if (lnSim >= 0.99) reasons.Add($"Last name match");
        else if (lnSim > 0.85) reasons.Add($"Last name ~ '{p.LastName}'");
        if (fnSim >= 0.99) reasons.Add($"First name match");
        else if (fnSim > 0.85) reasons.Add($"First name ~ '{p.FirstName}'");

        if (dob.HasValue && p.DateOfBirth.HasValue)
        {
            if (p.DateOfBirth == dob)
            {
                score += 30m;
                reasons.Add("DOB exact");
            }
            else
            {
                var diff = Math.Abs(p.DateOfBirth.Value.DayNumber - dob.Value.DayNumber);
                if (diff <= 3) { score += 22m; reasons.Add("DOB within 3 days"); }
                else if (diff <= 30) { score += 12m; reasons.Add("DOB within 30 days"); }
                else if (diff <= 365) { score += 4m; reasons.Add("DOB within 1 year"); }
            }
        }

        if (!string.IsNullOrEmpty(phone))
        {
            var pPh = NormalizePhone(p.Phone);
            if (pPh != null && pPh.EndsWith(phone))
            {
                score += 20m;
                reasons.Add("Phone match");
            }
        }

        return (Math.Min(score, 100m), reasons);
    }

    private static string? NormalizePhone(string? phone)
    {
        if (string.IsNullOrWhiteSpace(phone)) return null;
        var digits = new string(phone.Where(char.IsDigit).ToArray());
        return digits.Length >= 7 ? digits[^Math.Min(10, digits.Length)..] : null;
    }

    private static double JaroWinkler(string a, string b)
    {
        if (a == b) return 1.0;
        if (string.IsNullOrEmpty(a) || string.IsNullOrEmpty(b)) return 0.0;

        var match = Math.Max(a.Length, b.Length) / 2 - 1;
        var aMatched = new bool[a.Length];
        var bMatched = new bool[b.Length];
        var matches = 0;
        for (int i = 0; i < a.Length; i++)
        {
            var lo = Math.Max(0, i - match);
            var hi = Math.Min(b.Length - 1, i + match);
            for (int j = lo; j <= hi; j++)
            {
                if (bMatched[j] || a[i] != b[j]) continue;
                aMatched[i] = true; bMatched[j] = true; matches++; break;
            }
        }
        if (matches == 0) return 0.0;

        double t = 0; int k = 0;
        for (int i = 0; i < a.Length; i++)
        {
            if (!aMatched[i]) continue;
            while (!bMatched[k]) k++;
            if (a[i] != b[k]) t++;
            k++;
        }
        t /= 2;

        double m = matches;
        var jaro = (m / a.Length + m / b.Length + (m - t) / m) / 3.0;

        int prefix = 0;
        for (int i = 0; i < Math.Min(4, Math.Min(a.Length, b.Length)); i++)
        {
            if (a[i] == b[i]) prefix++; else break;
        }
        return jaro + prefix * 0.1 * (1.0 - jaro);
    }
}
