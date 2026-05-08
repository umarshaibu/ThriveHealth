using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Diagnostics;

namespace ThriveHealth.Web.Services;

public record ReportInput(
    string? Technique, string? Contrast,
    string? Findings, string? Impression, string? Recommendation,
    string? DicomStudyUid, string? DicomViewerUrl,
    bool HasCriticalFinding);

public interface IImagingService
{
    Task<string> GenerateAccessionAsync(CancellationToken ct = default);
    Task PerformAsync(int imagingOrderId, string userId, CancellationToken ct = default);
    Task SaveReportAsync(int imagingOrderId, ReportInput input, string userId, bool finalize, CancellationToken ct = default);
    Task<bool> AuthorizeAsync(int imagingOrderId, string userId, CancellationToken ct = default);
}

public class ImagingService : IImagingService
{
    private readonly ApplicationDbContext _db;
    public ImagingService(ApplicationDbContext db) => _db = db;

    public async Task<string> GenerateAccessionAsync(CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"IMG-{year}-";
        var existing = await _db.ImagingOrders
            .Where(x => x.AccessionNumber != null && x.AccessionNumber.StartsWith(prefix))
            .Select(x => x.AccessionNumber!).ToListAsync(ct);
        int next = 1;
        foreach (var a in existing)
        {
            var tail = a.Substring(prefix.Length);
            if (int.TryParse(tail, out var n) && n >= next) next = n + 1;
        }
        return $"{prefix}{next:D6}";
    }

    public async Task PerformAsync(int imagingOrderId, string userId, CancellationToken ct = default)
    {
        var o = await _db.ImagingOrders
            .Include(x => x.Report)
            .FirstOrDefaultAsync(x => x.Id == imagingOrderId, ct)
            ?? throw new InvalidOperationException("Imaging order not found");

        if (string.IsNullOrEmpty(o.AccessionNumber))
            o.AccessionNumber = await GenerateAccessionAsync(ct);
        if (o.Status == OrderStatus.Ordered) o.Status = OrderStatus.InProgress;

        o.Report ??= new ImagingReport
        {
            ImagingOrderId = o.Id,
            AccessionNumber = o.AccessionNumber
        };
        if (!o.Report.PerformedAt.HasValue)
        {
            o.Report.PerformedAt = DateTime.UtcNow;
            o.Report.PerformedById = userId;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task SaveReportAsync(int imagingOrderId, ReportInput input, string userId, bool finalize, CancellationToken ct = default)
    {
        var o = await _db.ImagingOrders
            .Include(x => x.Report)
            .FirstOrDefaultAsync(x => x.Id == imagingOrderId, ct)
            ?? throw new InvalidOperationException("Imaging order not found");

        if (string.IsNullOrEmpty(o.AccessionNumber))
            o.AccessionNumber = await GenerateAccessionAsync(ct);

        o.Report ??= new ImagingReport
        {
            ImagingOrderId = o.Id,
            AccessionNumber = o.AccessionNumber
        };

        var r = o.Report;
        r.Technique = input.Technique;
        r.Contrast = input.Contrast;
        r.Findings = input.Findings;
        r.Impression = input.Impression;
        r.Recommendation = input.Recommendation;
        r.DicomStudyUid = input.DicomStudyUid;
        r.DicomViewerUrl = input.DicomViewerUrl;
        r.HasCriticalFinding = input.HasCriticalFinding;
        r.AccessionNumber ??= o.AccessionNumber;

        if (finalize)
        {
            r.ReportedAt = DateTime.UtcNow;
            r.ReportedById = userId;
        }

        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> AuthorizeAsync(int imagingOrderId, string userId, CancellationToken ct = default)
    {
        var o = await _db.ImagingOrders
            .Include(x => x.Report)
            .FirstOrDefaultAsync(x => x.Id == imagingOrderId, ct);
        if (o?.Report is null || !o.Report.ReportedAt.HasValue) return false;
        if (o.Report.AuthorizedAt.HasValue) return true;

        o.Report.AuthorizedAt = DateTime.UtcNow;
        o.Report.AuthorizedById = userId;
        o.Status = OrderStatus.Completed;
        o.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
