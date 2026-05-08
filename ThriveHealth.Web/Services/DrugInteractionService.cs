using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Pharmacy;

namespace ThriveHealth.Web.Services;

public record InteractionWarning(string DrugA, string DrugB, InteractionSeverity Severity, string Note);

public interface IDrugInteractionService
{
    Task<IReadOnlyList<InteractionWarning>> CheckAsync(
        IEnumerable<string> proposedDrugs,
        IEnumerable<string> existingDrugs,
        CancellationToken ct = default);
}

public class DrugInteractionService : IDrugInteractionService
{
    private readonly ApplicationDbContext _db;
    public DrugInteractionService(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<InteractionWarning>> CheckAsync(
        IEnumerable<string> proposedDrugs,
        IEnumerable<string> existingDrugs,
        CancellationToken ct = default)
    {
        var proposed = proposedDrugs?.Select(NormalizeName).Where(n => n.Length > 0).ToList() ?? new();
        var existing = existingDrugs?.Select(NormalizeName).Where(n => n.Length > 0).ToList() ?? new();
        if (proposed.Count == 0) return Array.Empty<InteractionWarning>();

        var rules = await _db.DrugInteractions.AsNoTracking().ToListAsync(ct);
        var hits = new List<InteractionWarning>();

        foreach (var p in proposed)
        {
            var others = proposed.Where(x => !ReferenceEquals(x, p)).Concat(existing).ToList();
            foreach (var o in others)
            {
                foreach (var r in rules)
                {
                    var matchesP = p.Contains(r.DrugAKey) || p.Contains(r.DrugBKey);
                    var matchesO = o.Contains(r.DrugAKey) || o.Contains(r.DrugBKey);
                    if (matchesP && matchesO &&
                        !(p.Contains(r.DrugAKey) && o.Contains(r.DrugAKey)) &&
                        !(p.Contains(r.DrugBKey) && o.Contains(r.DrugBKey)))
                    {
                        hits.Add(new InteractionWarning(p, o, r.Severity, r.Note));
                    }
                }
            }
        }

        return hits
            .GroupBy(h =>
            {
                var pair = new[] { h.DrugA, h.DrugB };
                Array.Sort(pair, StringComparer.Ordinal);
                return string.Join("|", pair);
            })
            .Select(g => g.OrderByDescending(x => x.Severity).First())
            .OrderByDescending(h => h.Severity)
            .ToList();
    }

    private static string NormalizeName(string s) => (s ?? string.Empty).Trim().ToLowerInvariant();
}
