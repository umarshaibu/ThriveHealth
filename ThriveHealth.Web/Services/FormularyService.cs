using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;

namespace ThriveHealth.Web.Services;

public record FormularyCheck(int DrugId, string DrugName, bool IsCovered, decimal CopayPercent, string? Note);

public interface IFormularyService
{
    Task<FormularyCheck?> CheckAsync(int patientId, int drugId, CancellationToken ct = default);
    Task<IReadOnlyList<FormularyCheck>> CheckManyAsync(int patientId, IEnumerable<int> drugIds, CancellationToken ct = default);
}

public class FormularyService : IFormularyService
{
    private readonly ApplicationDbContext _db;
    public FormularyService(ApplicationDbContext db) => _db = db;

    public async Task<FormularyCheck?> CheckAsync(int patientId, int drugId, CancellationToken ct = default)
    {
        var hits = await CheckManyAsync(patientId, new[] { drugId }, ct);
        return hits.FirstOrDefault();
    }

    public async Task<IReadOnlyList<FormularyCheck>> CheckManyAsync(int patientId, IEnumerable<int> drugIds, CancellationToken ct = default)
    {
        var ids = drugIds.Distinct().ToList();
        if (ids.Count == 0) return Array.Empty<FormularyCheck>();

        var primaryPayer = await _db.PatientPayers.AsNoTracking()
            .Include(p => p.PayerPlan)!.ThenInclude(p => p!.Formulary)
            .Where(p => p.PatientId == patientId && p.IsActive && p.IsPrimary)
            .OrderByDescending(p => p.IsPrimary)
            .FirstOrDefaultAsync(ct);

        var drugs = await _db.Drugs.AsNoTracking()
            .Where(d => ids.Contains(d.Id))
            .Select(d => new { d.Id, d.GenericName, d.BrandName, d.Strength, d.IsControlled, d.Schedule })
            .ToListAsync(ct);

        var results = new List<FormularyCheck>();

        if (primaryPayer?.PayerPlan is null)
        {
            foreach (var d in drugs)
            {
                results.Add(new FormularyCheck(d.Id, d.GenericName, true, 100m,
                    "No insurance plan linked — patient pays out-of-pocket."));
            }
            return results;
        }

        var plan = primaryPayer.PayerPlan;
        var formulary = plan.Formulary.ToDictionary(f => f.DrugId);

        foreach (var d in drugs)
        {
            if (formulary.TryGetValue(d.Id, out var f))
            {
                results.Add(new FormularyCheck(d.Id, d.GenericName, f.IsCovered, f.CopayPercent,
                    f.IsCovered ? null : (f.Notes ?? "Drug excluded from this plan's formulary.")));
            }
            else
            {
                results.Add(new FormularyCheck(d.Id, d.GenericName, plan.DefaultFormularyCovered, plan.DefaultCopayPercent,
                    plan.DefaultFormularyCovered ? null : "Drug not on plan's formulary."));
            }
        }
        return results;
    }
}
