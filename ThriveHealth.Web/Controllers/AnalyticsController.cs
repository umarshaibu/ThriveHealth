using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Billing;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Immunization;
using ThriveHealth.Web.Models.Inpatient;
using ThriveHealth.Web.Models.Maternity;
using ThriveHealth.Web.Models.Reporting;
using ThriveHealth.Web.Models.ViewModels;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.AnalyticsView)]
public class AnalyticsController : Controller
{
    public const string AnalyticsStaff = Roles.SystemAdministrator + "," + Roles.MedicalDirector + "," +
        Roles.ChiefExecutive + "," + Roles.ChiefFinancialOfficer + "," + Roles.ChiefNursingOfficer + "," +
        Roles.HrOfficer + "," + Roles.Accountant;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;

    public AnalyticsController(ApplicationDbContext db, UserManager<ApplicationUser> userManager)
    {
        _db = db; _userManager = userManager;
    }

    [HttpGet]
    public async Task<IActionResult> Index(int? days)
    {
        var u = await _userManager.GetUserAsync(User);
        if (u?.FacilityId is null) return NotFound();
        var fid = u.FacilityId.Value;
        var window = Math.Clamp(days ?? 30, 7, 365);
        var startUtc = DateTime.UtcNow.Date.AddDays(-window + 1);
        var startDate = DateOnly.FromDateTime(startUtc);

        var totalPatients = await _db.Patients.CountAsync(p => p.FacilityId == fid && !p.IsMergedAlias);
        var newRegs = await _db.Patients.CountAsync(p => p.FacilityId == fid && p.CreatedAt >= startUtc);

        var encounters = await _db.Encounters.AsNoTracking()
            .Where(e => e.FacilityId == fid && e.StartedAt >= startUtc)
            .Select(e => new { e.StartedAt, e.ClinicId })
            .ToListAsync();

        var admissions = await _db.Admissions.AsNoTracking()
            .Where(a => a.FacilityId == fid && a.AdmittedAt >= startUtc)
            .Select(a => new { a.AdmittedAt, a.Status, a.DischargedAt })
            .ToListAsync();

        var deliveriesList = await _db.Deliveries.AsNoTracking()
            .Where(d => d.FacilityId == fid && d.DeliveryUtc >= startUtc)
            .Select(d => new { d.Mode })
            .ToListAsync();

        var labs = await _db.LabOrders.AsNoTracking()
            .Where(o => o.Encounter!.FacilityId == fid && o.OrderedAt >= startUtc)
            .CountAsync();

        var imms = await _db.ImmunizationDoses.AsNoTracking()
            .Where(d => d.FacilityId == fid && d.AdministeredAt.HasValue && d.AdministeredAt >= startUtc && d.Status == DoseStatus.Administered)
            .Include(d => d.Vaccine)
            .ToListAsync();

        var payments = await _db.Payments.AsNoTracking()
            .Where(p => p.Bill!.FacilityId == fid && p.Status == PaymentStatus.Recorded && p.ReceivedAt >= startUtc)
            .Select(p => new { p.ReceivedAt, p.Amount, p.Method })
            .ToListAsync();

        var openBalances = await _db.Bills.AsNoTracking()
            .Where(b => b.FacilityId == fid && (b.Status == BillStatus.Open || b.Status == BillStatus.PartiallyPaid))
            .Select(b => new { b.NetAmount, b.PaidAmount }).ToListAsync();

        var idsrOpen = await _db.IdsrCases.AsNoTracking()
            .CountAsync(c => c.FacilityId == fid && c.Status == IdsrCaseStatus.Open);

        var idsrInWindow = await _db.IdsrCases.AsNoTracking()
            .Include(c => c.NotifiableDisease)
            .Where(c => c.FacilityId == fid && c.OnsetDate >= startDate)
            .ToListAsync();

        var clinicList = await _db.Clinics.AsNoTracking()
            .Where(c => c.FacilityId == fid)
            .Select(c => new { c.Id, c.Name }).ToListAsync();
        var clinicNames = clinicList.ToDictionary(c => c.Id, c => c.Name);

        var encDaily = encounters
            .GroupBy(e => DateOnly.FromDateTime(e.StartedAt))
            .OrderBy(g => g.Key)
            .Select(g => new DailySeriesPoint(g.Key, g.Count()))
            .ToList();

        var admDaily = admissions
            .GroupBy(a => DateOnly.FromDateTime(a.AdmittedAt))
            .OrderBy(g => g.Key)
            .Select(g => new DailySeriesPoint(g.Key, g.Count()))
            .ToList();

        var revDaily = payments
            .GroupBy(p => DateOnly.FromDateTime(p.ReceivedAt))
            .OrderBy(g => g.Key)
            .Select(g => new DailySeriesPoint(g.Key, g.Sum(x => x.Amount)))
            .ToList();

        var byMethod = payments
            .GroupBy(p => p.Method.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var byClinic = encounters
            .GroupBy(e => clinicNames.TryGetValue(e.ClinicId, out var name) ? name : "Unassigned")
            .OrderByDescending(g => g.Count()).Take(10)
            .ToDictionary(g => g.Key, g => g.Count());

        var byVax = imms
            .Where(d => d.Vaccine != null)
            .GroupBy(d => d.Vaccine!.Code)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count());

        var byDisease = idsrInWindow
            .Where(c => c.NotifiableDisease != null)
            .GroupBy(c => c.NotifiableDisease!.Code)
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count());

        return View(new AnalyticsSnapshotViewModel
        {
            Days = window,
            TotalPatients = totalPatients,
            NewRegistrationsInWindow = newRegs,
            OutpatientVisitsInWindow = encounters.Count,
            AdmissionsInWindow = admissions.Count,
            DischargesInWindow = admissions.Count(a => a.DischargedAt.HasValue),
            InpatientDeathsInWindow = admissions.Count(a => a.Status == AdmissionStatus.Deceased),
            DeliveriesInWindow = deliveriesList.Count,
            CSectionsInWindow = deliveriesList.Count(d => d.Mode == DeliveryMode.ElectiveCS || d.Mode == DeliveryMode.EmergencyCS),
            LabsInWindow = labs,
            ImmunizationsInWindow = imms.Count,
            RevenueCollectedInWindow = payments.Sum(p => p.Amount),
            OutstandingBalance = openBalances.Sum(b => b.NetAmount - b.PaidAmount),
            OpenIdsrCases = idsrOpen,
            EncountersDaily = encDaily,
            AdmissionsDaily = admDaily,
            RevenueDaily = revDaily,
            PaymentsByMethod = byMethod,
            EncountersByClinic = byClinic,
            ImmunizationByVaccine = byVax,
            IdsrByDisease = byDisease
        });
    }
}
