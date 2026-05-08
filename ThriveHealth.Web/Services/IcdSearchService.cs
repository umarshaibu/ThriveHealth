using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Clinical;

namespace ThriveHealth.Web.Services;

public record IcdHit(string Code, string Description, string? Category);

public interface IIcdSearchService
{
    Task<IReadOnlyList<IcdHit>> SearchAsync(string query, int limit = 15, CancellationToken ct = default);
}

public class IcdSearchService : IIcdSearchService
{
    private readonly ApplicationDbContext _db;
    public IcdSearchService(ApplicationDbContext db) => _db = db;

    public async Task<IReadOnlyList<IcdHit>> SearchAsync(string query, int limit = 15, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(query))
        {
            return await _db.IcdCodes.AsNoTracking()
                .Where(c => c.IsCommon)
                .OrderBy(c => c.Code)
                .Take(limit)
                .Select(c => new IcdHit(c.Code, c.Description, c.Category))
                .ToListAsync(ct);
        }

        var q = query.Trim();
        var like = $"%{q}%";
        return await _db.IcdCodes.AsNoTracking()
            .Where(c =>
                EF.Functions.ILike(c.Code, like) ||
                EF.Functions.ILike(c.Description, like) ||
                (c.LocalSynonyms != null && EF.Functions.ILike(c.LocalSynonyms, like)))
            .OrderBy(c => c.Code)
            .Take(limit)
            .Select(c => new IcdHit(c.Code, c.Description, c.Category))
            .ToListAsync(ct);
    }
}
