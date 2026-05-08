using Microsoft.EntityFrameworkCore;
using System.Text.Json;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Billing;
using ThriveHealth.Web.Models.Immunization;
using ThriveHealth.Web.Models.Inpatient;
using ThriveHealth.Web.Models.Maternity;
using ThriveHealth.Web.Models.Reporting;
using ThriveHealth.Web.Models.Scheduling;

namespace ThriveHealth.Web.Services;

public record NhmisAggregates(
    int Year,
    int Month,
    string Period,
    int OutpatientAttendance,
    int OutpatientUnder5,
    int Admissions,
    int Discharges,
    int InpatientDeaths,
    int Deliveries,
    int LiveBirths,
    int StillBirths,
    int CSections,
    int AncFirstVisits,
    int AncTotalVisits,
    int ImmunizationDosesGiven,
    int LabTestsCompleted,
    int ImagingStudies,
    int PrescriptionsIssued,
    decimal RevenueCollectedNgn,
    int IdsrCasesReported,
    Dictionary<string, int> ImmunizationByVaccine,
    Dictionary<string, int> IdsrByDisease
);

public interface INhmisReportService
{
    Task<NhmisAggregates> ComputeAsync(int facilityId, int year, int month, CancellationToken ct = default);
    Task<NhmisReport> GenerateOrUpdateAsync(int facilityId, int year, int month, string userId, CancellationToken ct = default);
    Task<bool> SubmitAsync(int reportId, string toWhom, string? reference, string userId, CancellationToken ct = default);
}

public class NhmisReportService : INhmisReportService
{
    private readonly ApplicationDbContext _db;
    public NhmisReportService(ApplicationDbContext db) { _db = db; }

    public async Task<NhmisAggregates> ComputeAsync(int facilityId, int year, int month, CancellationToken ct = default)
    {
        var start = new DateTime(year, month, 1, 0, 0, 0, DateTimeKind.Utc);
        var end = start.AddMonths(1);
        var startD = DateOnly.FromDateTime(start);
        var endD = DateOnly.FromDateTime(end);

        var outpatient = await _db.Encounters.AsNoTracking()
            .Where(e => e.FacilityId == facilityId && e.StartedAt >= start && e.StartedAt < end)
            .ToListAsync(ct);

        var u5 = 0;
        if (outpatient.Any())
        {
            var pids = outpatient.Select(e => e.PatientId).Distinct().ToList();
            var dobs = await _db.Patients.AsNoTracking().Where(p => pids.Contains(p.Id)).Select(p => new { p.Id, p.DateOfBirth }).ToListAsync(ct);
            var dobMap = dobs.ToDictionary(x => x.Id, x => x.DateOfBirth);
            u5 = outpatient.Count(e => dobMap.GetValueOrDefault(e.PatientId)?.AddYears(5) > DateOnly.FromDateTime(e.StartedAt));
        }

        var admissions = await _db.Admissions.AsNoTracking()
            .CountAsync(a => a.FacilityId == facilityId && a.AdmittedAt >= start && a.AdmittedAt < end, ct);

        var discharges = await _db.Admissions.AsNoTracking()
            .CountAsync(a => a.FacilityId == facilityId && a.DischargedAt.HasValue && a.DischargedAt >= start && a.DischargedAt < end, ct);

        var inpatientDeaths = await _db.Admissions.AsNoTracking()
            .CountAsync(a => a.FacilityId == facilityId && a.DischargedAt.HasValue && a.DischargedAt >= start && a.DischargedAt < end && a.Status == AdmissionStatus.Deceased, ct);

        var deliveriesList = await _db.Deliveries.AsNoTracking()
            .Include(d => d.Newborns)
            .Where(d => d.FacilityId == facilityId && d.DeliveryUtc >= start && d.DeliveryUtc < end)
            .ToListAsync(ct);
        var liveBirths = deliveriesList.SelectMany(d => d.Newborns).Count(n => d_outcome(n) == 1);
        var stillBirths = deliveriesList.Count(d => d.Outcome != LabourOutcome.LiveBorn);
        var cs = deliveriesList.Count(d => d.Mode == DeliveryMode.ElectiveCS || d.Mode == DeliveryMode.EmergencyCS);

        var ancFirst = await _db.AnteNatalRecords.AsNoTracking()
            .CountAsync(r => r.FacilityId == facilityId && r.BookingDate >= startD && r.BookingDate < endD, ct);
        var ancTotal = await _db.AnteNatalVisits.AsNoTracking()
            .Where(v => v.AnteNatalRecord!.FacilityId == facilityId && v.VisitDate >= startD && v.VisitDate < endD)
            .CountAsync(ct);

        var doses = await _db.ImmunizationDoses.AsNoTracking()
            .Include(d => d.Vaccine)
            .Where(d => d.FacilityId == facilityId && d.AdministeredAt.HasValue && d.AdministeredAt >= start && d.AdministeredAt < end && d.Status == DoseStatus.Administered)
            .ToListAsync(ct);
        var byVax = doses.Where(d => d.Vaccine != null)
            .GroupBy(d => d.Vaccine!.Code)
            .ToDictionary(g => g.Key, g => g.Count());

        var labs = await _db.LabOrders.AsNoTracking()
            .CountAsync(o => o.Encounter!.FacilityId == facilityId && o.CompletedAt.HasValue && o.CompletedAt >= start && o.CompletedAt < end, ct);
        var imaging = await _db.ImagingOrders.AsNoTracking()
            .CountAsync(o => o.Encounter!.FacilityId == facilityId && o.CompletedAt.HasValue && o.CompletedAt >= start && o.CompletedAt < end, ct);
        var prescriptions = await _db.Prescriptions.AsNoTracking()
            .CountAsync(p => p.Encounter!.FacilityId == facilityId && p.IssuedAt >= start && p.IssuedAt < end, ct);

        var revenue = await _db.Payments.AsNoTracking()
            .Where(p => p.Bill!.FacilityId == facilityId && p.Status == PaymentStatus.Recorded && p.ReceivedAt >= start && p.ReceivedAt < end)
            .SumAsync(p => (decimal?)p.Amount, ct) ?? 0m;

        var idsrCases = await _db.IdsrCases.AsNoTracking()
            .Include(c => c.NotifiableDisease)
            .Where(c => c.FacilityId == facilityId && c.OnsetDate >= startD && c.OnsetDate < endD)
            .ToListAsync(ct);
        var byDisease = idsrCases.Where(c => c.NotifiableDisease != null)
            .GroupBy(c => c.NotifiableDisease!.Code)
            .ToDictionary(g => g.Key, g => g.Count());

        return new NhmisAggregates(
            year, month, $"{year:D4}-{month:D2}",
            outpatient.Count, u5,
            admissions, discharges, inpatientDeaths,
            deliveriesList.Count, liveBirths, stillBirths, cs,
            ancFirst, ancTotal,
            doses.Count, labs, imaging, prescriptions,
            revenue, idsrCases.Count, byVax, byDisease
        );

        static int d_outcome(Newborn n) => 1; // newborns recorded indicate live birth path; outcome handled at delivery level
    }

    public async Task<NhmisReport> GenerateOrUpdateAsync(int facilityId, int year, int month, string userId, CancellationToken ct = default)
    {
        var period = $"{year:D4}-{month:D2}";
        var aggregates = await ComputeAsync(facilityId, year, month, ct);
        var json = JsonSerializer.Serialize(aggregates);

        var report = await _db.NhmisReports.FirstOrDefaultAsync(r => r.FacilityId == facilityId && r.Period == period, ct);
        if (report is null)
        {
            report = new NhmisReport
            {
                FacilityId = facilityId,
                Year = year,
                Month = month,
                Period = period,
                Status = NhmisReportStatus.Generated,
                AggregatesJson = json,
                GeneratedById = userId,
                GeneratedAt = DateTime.UtcNow
            };
            _db.NhmisReports.Add(report);
        }
        else if (report.Status != NhmisReportStatus.Submitted)
        {
            report.AggregatesJson = json;
            report.Status = NhmisReportStatus.Generated;
            report.GeneratedAt = DateTime.UtcNow;
            report.GeneratedById = userId;
        }
        await _db.SaveChangesAsync(ct);
        return report;
    }

    public async Task<bool> SubmitAsync(int reportId, string toWhom, string? reference, string userId, CancellationToken ct = default)
    {
        var r = await _db.NhmisReports.FirstOrDefaultAsync(x => x.Id == reportId, ct);
        if (r is null) return false;
        r.Status = NhmisReportStatus.Submitted;
        r.SubmittedAt = DateTime.UtcNow;
        r.SubmittedToWhom = toWhom;
        r.SubmissionReference = reference;
        r.SubmittedById = userId;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}

public interface IIdsrService
{
    Task<string> NextCaseNumberAsync(int facilityId, CancellationToken ct = default);
    Task<int> ReportCaseAsync(IdsrCase c, CancellationToken ct = default);
    Task<bool> CloseCaseAsync(int id, CaseOutcome outcome, DateOnly? outcomeDate, string? comments, CancellationToken ct = default);
    Task<bool> NotifyNcdcAsync(int id, CancellationToken ct = default);
}

public class IdsrService : IIdsrService
{
    private readonly ApplicationDbContext _db;
    public IdsrService(ApplicationDbContext db) { _db = db; }

    public async Task<string> NextCaseNumberAsync(int facilityId, CancellationToken ct = default)
    {
        var prefix = $"IDSR-{DateTime.UtcNow.Year}-";
        var last = await _db.IdsrCases
            .Where(c => c.FacilityId == facilityId && c.CaseNumber.StartsWith(prefix))
            .OrderByDescending(c => c.CaseNumber).Select(c => c.CaseNumber).FirstOrDefaultAsync(ct);
        var next = 1;
        if (!string.IsNullOrEmpty(last))
        {
            var tail = last.Substring(prefix.Length);
            if (int.TryParse(tail, out var n)) next = n + 1;
        }
        return $"{prefix}{next:D5}";
    }

    public async Task<int> ReportCaseAsync(IdsrCase c, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(c.CaseNumber))
            c.CaseNumber = await NextCaseNumberAsync(c.FacilityId, ct);
        _db.IdsrCases.Add(c);
        await _db.SaveChangesAsync(ct);
        return c.Id;
    }

    public async Task<bool> CloseCaseAsync(int id, CaseOutcome outcome, DateOnly? outcomeDate, string? comments, CancellationToken ct = default)
    {
        var c = await _db.IdsrCases.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        c.Status = IdsrCaseStatus.Closed;
        c.Outcome = outcome;
        c.OutcomeDate = outcomeDate;
        if (!string.IsNullOrEmpty(comments)) c.Comments = (c.Comments is null ? "" : c.Comments + "\n") + comments;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> NotifyNcdcAsync(int id, CancellationToken ct = default)
    {
        var c = await _db.IdsrCases.FirstOrDefaultAsync(x => x.Id == id, ct);
        if (c is null) return false;
        c.NotifiedNcdc = true;
        c.NotifiedNcdcAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
