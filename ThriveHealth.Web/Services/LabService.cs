using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Diagnostics;

namespace ThriveHealth.Web.Services;

public record LabValueInput(int LabAnalyteId, string Value);

public interface ILabService
{
    Task<string> GenerateAccessionAsync(CancellationToken ct = default);
    Task CollectAsync(int labOrderId, string userId, CancellationToken ct = default);
    Task<int> EnterResultsAsync(int labOrderId, IEnumerable<LabValueInput> values, string? generalComment, string userId, bool finalize, CancellationToken ct = default);
    Task<bool> AuthorizeAsync(int labResultId, string userId, CancellationToken ct = default);
}

public class LabService : ILabService
{
    private readonly ApplicationDbContext _db;
    public LabService(ApplicationDbContext db) => _db = db;

    public async Task<string> GenerateAccessionAsync(CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"LAB-{year}-";
        var lastForYear = await _db.LabOrders
            .Where(x => x.AccessionNumber != null && x.AccessionNumber.StartsWith(prefix))
            .Select(x => x.AccessionNumber!)
            .ToListAsync(ct);
        int next = 1;
        foreach (var a in lastForYear)
        {
            var tail = a.Substring(prefix.Length);
            if (int.TryParse(tail, out var n) && n >= next) next = n + 1;
        }
        return $"{prefix}{next:D6}";
    }

    public async Task CollectAsync(int labOrderId, string userId, CancellationToken ct = default)
    {
        var o = await _db.LabOrders.FirstOrDefaultAsync(x => x.Id == labOrderId, ct)
            ?? throw new InvalidOperationException("Lab order not found");

        if (string.IsNullOrEmpty(o.AccessionNumber))
            o.AccessionNumber = await GenerateAccessionAsync(ct);
        o.CollectedAt = DateTime.UtcNow;
        o.CollectedById = userId;
        if (o.Status == OrderStatus.Ordered) o.Status = OrderStatus.InProgress;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> EnterResultsAsync(int labOrderId, IEnumerable<LabValueInput> values, string? generalComment, string userId, bool finalize, CancellationToken ct = default)
    {
        var order = await _db.LabOrders
            .Include(o => o.Result).ThenInclude(r => r!.Values)
            .Include(o => o.LabTest)!.ThenInclude(t => t!.Analytes)
            .FirstOrDefaultAsync(o => o.Id == labOrderId, ct)
            ?? throw new InvalidOperationException("Lab order not found");

        if (order.LabTestId is null) throw new InvalidOperationException("Order has no linked lab test");

        var result = order.Result;
        if (result is null)
        {
            result = new LabResult
            {
                LabOrderId = order.Id,
                LabTestId = order.LabTestId.Value,
                Status = LabResultStatus.Preliminary,
                EnteredById = userId,
                EnteredAt = DateTime.UtcNow,
                GeneralComment = generalComment
            };
            _db.LabResults.Add(result);
        }
        else
        {
            result.GeneralComment = generalComment;
            result.EnteredById = userId;
            result.EnteredAt = DateTime.UtcNow;
            _db.LabResultValues.RemoveRange(result.Values);
            result.Values.Clear();
        }

        var analyteMap = order.LabTest!.Analytes.ToDictionary(a => a.Id);
        bool anyCritical = false;
        int rowsAdded = 0;

        foreach (var v in values)
        {
            if (string.IsNullOrWhiteSpace(v.Value)) continue;
            if (!analyteMap.TryGetValue(v.LabAnalyteId, out var analyte)) continue;

            decimal? num = null;
            if (decimal.TryParse(v.Value, System.Globalization.NumberStyles.Number,
                System.Globalization.CultureInfo.InvariantCulture, out var parsed)) num = parsed;

            var flag = ComputeFlag(num, analyte);
            if (flag == AnalyteFlag.CriticalLow || flag == AnalyteFlag.CriticalHigh) anyCritical = true;

            string? rangeDisplay = (analyte.RefLow.HasValue || analyte.RefHigh.HasValue)
                ? $"{analyte.RefLow?.ToString() ?? ""} – {analyte.RefHigh?.ToString() ?? ""}"
                : null;

            result.Values.Add(new LabResultValue
            {
                LabAnalyteId = analyte.Id,
                AnalyteName = analyte.Name,
                Unit = analyte.Unit,
                Value = v.Value.Trim(),
                NumericValue = num,
                Flag = flag,
                RefRangeDisplay = rangeDisplay
            });
            rowsAdded++;
        }

        result.HasCriticalValue = anyCritical;
        if (finalize) result.Status = LabResultStatus.Final;

        await _db.SaveChangesAsync(ct);
        return rowsAdded;
    }

    public async Task<bool> AuthorizeAsync(int labResultId, string userId, CancellationToken ct = default)
    {
        var result = await _db.LabResults
            .Include(r => r.LabOrder)
            .FirstOrDefaultAsync(r => r.Id == labResultId, ct);
        if (result is null) return false;
        if (result.Status == LabResultStatus.Authorized) return true;
        if (result.Status != LabResultStatus.Final) return false;

        result.Status = LabResultStatus.Authorized;
        result.AuthorizedAt = DateTime.UtcNow;
        result.AuthorizedById = userId;

        if (result.LabOrder is not null)
        {
            result.LabOrder.Status = OrderStatus.Completed;
            result.LabOrder.CompletedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    private static AnalyteFlag ComputeFlag(decimal? value, LabAnalyte analyte)
    {
        if (!value.HasValue) return AnalyteFlag.Normal;
        var v = value.Value;
        if (analyte.CriticalLow.HasValue && v < analyte.CriticalLow.Value) return AnalyteFlag.CriticalLow;
        if (analyte.CriticalHigh.HasValue && v > analyte.CriticalHigh.Value) return AnalyteFlag.CriticalHigh;
        if (analyte.RefLow.HasValue && v < analyte.RefLow.Value) return AnalyteFlag.Low;
        if (analyte.RefHigh.HasValue && v > analyte.RefHigh.Value) return AnalyteFlag.High;
        return AnalyteFlag.Normal;
    }
}
