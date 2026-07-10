using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Api;
using ThriveHealth.Web.Models.Billing;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Telemedicine;

namespace ThriveHealth.Web.Controllers.Api;

/// <summary>Read endpoints backing the patient mobile app's core screens.</summary>
[Route("api/v1")]
public class PatientApiController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;

    public PatientApiController(ApplicationDbContext db) => _db = db;

    [HttpGet("me")]
    [ProducesResponseType(typeof(ApiPatientProfile), StatusCodes.Status200OK)]
    public async Task<IActionResult> Me(CancellationToken ct)
    {
        var pid = PatientId;
        var data = await _db.Patients.AsNoTracking()
            .Where(p => p.Id == pid)
            .Select(p => new { p.Id, p.HospitalNumber, p.FirstName, p.MiddleName, p.LastName, p.Gender, p.DateOfBirth, p.Phone, p.Email, p.FacilityId })
            .FirstOrDefaultAsync(ct);
        if (data is null) return NotFound(new ApiError("Patient not found.", "not_found"));

        var facility = await _db.Facilities.AsNoTracking()
            .Where(f => f.Id == data.FacilityId).Select(f => new { f.Name, f.TenantId }).FirstOrDefaultAsync(ct);
        var fullName = string.IsNullOrWhiteSpace(data.MiddleName)
            ? $"{data.FirstName} {data.LastName}".Trim()
            : $"{data.FirstName} {data.MiddleName} {data.LastName}".Trim();

        return Ok(new ApiPatientProfile(data.Id, data.HospitalNumber, fullName, data.Gender,
            data.DateOfBirth?.ToString("yyyy-MM-dd"), data.Phone, data.Email,
            data.FacilityId, facility?.Name ?? "", facility?.TenantId ?? 0));
    }

    [HttpGet("dashboard")]
    [ProducesResponseType(typeof(ApiDashboard), StatusCodes.Status200OK)]
    public async Task<IActionResult> Dashboard(CancellationToken ct)
    {
        var pid = PatientId;
        var todayUtc = DateTime.UtcNow.Date;

        var upcoming = await _db.Appointments
            .CountAsync(a => a.PatientId == pid && a.ScheduledStartUtc >= todayUtc && (int)a.Status < 4, ct);
        var bills = await _db.Bills.AsNoTracking()
            .Where(b => b.PatientId == pid && (b.Status == BillStatus.Open || b.Status == BillStatus.PartiallyPaid))
            .Select(b => new { b.NetAmount, b.PaidAmount }).ToListAsync(ct);
        var pending = await _db.LabOrders
            .CountAsync(o => o.PatientId == pid && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled, ct);
        var teles = await _db.TeleSessions.CountAsync(t => t.PatientId == pid &&
            (t.Status == TeleSessionStatus.Requested || t.Status == TeleSessionStatus.Scheduled ||
             t.Status == TeleSessionStatus.PatientWaiting || t.Status == TeleSessionStatus.InCall), ct);

        var recent = await _db.Encounters.AsNoTracking()
            .Include(e => e.Clinic).Include(e => e.Clinician)
            .Where(e => e.PatientId == pid)
            .OrderByDescending(e => e.StartedAt).Take(5)
            .ToListAsync(ct);

        return Ok(new ApiDashboard(
            upcoming, bills.Count, bills.Sum(b => b.NetAmount - b.PaidAmount), pending, teles,
            recent.Select(ToVisitSummary).ToList()));
    }

    [HttpGet("visits")]
    [ProducesResponseType(typeof(IReadOnlyList<ApiVisitSummary>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Visits(CancellationToken ct)
    {
        var pid = PatientId;
        var visits = await _db.Encounters.AsNoTracking()
            .Include(e => e.Clinic).Include(e => e.Clinician)
            .Where(e => e.PatientId == pid)
            .OrderByDescending(e => e.StartedAt).Take(50).ToListAsync(ct);
        return Ok(visits.Select(ToVisitSummary).ToList());
    }

    [HttpGet("visits/{id:int}")]
    [ProducesResponseType(typeof(ApiVisitDetail), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Visit(int id, CancellationToken ct)
    {
        var pid = PatientId;
        var e = await _db.Encounters.AsNoTracking()
            .Include(x => x.Clinic).Include(x => x.Clinician)
            .Include(x => x.Soap)
            .Include(x => x.Diagnoses)
            .Include(x => x.Prescriptions).ThenInclude(p => p.Items)
            .FirstOrDefaultAsync(x => x.Id == id && x.PatientId == pid, ct);
        if (e is null) return NotFound(new ApiError("Visit not found.", "not_found"));

        var soap = e.Soap is null ? null : new ApiSoap(e.Soap.Subjective, e.Soap.Objective, e.Soap.Assessment, e.Soap.Plan);
        var diagnoses = e.Diagnoses.Select(d => new ApiDiagnosis(d.IcdCode, d.Description, d.Status.ToString())).ToList();
        var prescriptions = e.Prescriptions.Select(p => new ApiPrescription(
            p.Id, p.Status.ToString(),
            p.Items.Select(i => string.Join(" ", new[] { i.DrugName, i.Dose, i.Frequency, i.Duration }
                .Where(s => !string.IsNullOrWhiteSpace(s))).Trim()).ToList())).ToList();

        return Ok(new ApiVisitDetail(e.Id, e.StartedAt, e.Status.ToString(), e.Type.ToString(),
            e.ChiefComplaint, e.Clinic?.Name, e.Clinician?.FullName, soap, diagnoses, prescriptions));
    }

    [HttpGet("results")]
    [ProducesResponseType(typeof(ApiResults), StatusCodes.Status200OK)]
    public async Task<IActionResult> Results(CancellationToken ct)
    {
        var pid = PatientId;
        var labs = await _db.LabOrders.AsNoTracking()
            .Include(o => o.LabTest).Include(o => o.Result)
            .Where(o => o.PatientId == pid)
            .OrderByDescending(o => o.OrderedAt).Take(50)
            .Select(o => new ApiLabResult(o.Id, o.LabTest != null ? o.LabTest.Name : "Lab test",
                o.Status.ToString(), o.OrderedAt, o.Result != null))
            .ToListAsync(ct);

        var imaging = await _db.ImagingOrders.AsNoTracking()
            .Include(o => o.Report)
            .Where(o => o.PatientId == pid)
            .OrderByDescending(o => o.OrderedAt).Take(50)
            .Select(o => new ApiImagingResult(o.Id, o.Modality.ToString(), o.Status.ToString(), o.OrderedAt, o.Report != null))
            .ToListAsync(ct);

        return Ok(new ApiResults(labs, imaging));
    }

    [HttpGet("appointments")]
    [ProducesResponseType(typeof(IReadOnlyList<ApiAppointment>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Appointments(CancellationToken ct)
    {
        var pid = PatientId;
        var appts = await _db.Appointments.AsNoTracking()
            .Include(a => a.Clinic).Include(a => a.Clinician)
            .Where(a => a.PatientId == pid)
            .OrderByDescending(a => a.ScheduledStartUtc).Take(50)
            .Select(a => new ApiAppointment(a.Id, a.ScheduledStartUtc, a.DurationMinutes, a.Status.ToString(),
                a.Type.ToString(), a.Clinic != null ? a.Clinic.Name : null,
                a.Clinician != null ? a.Clinician.FullName : null, a.ReasonForVisit))
            .ToListAsync(ct);
        return Ok(appts);
    }

    [HttpGet("bills")]
    [ProducesResponseType(typeof(IReadOnlyList<ApiBill>), StatusCodes.Status200OK)]
    public async Task<IActionResult> Bills(CancellationToken ct)
    {
        var pid = PatientId;
        var bills = await _db.Bills.AsNoTracking()
            .Where(b => b.PatientId == pid)
            .OrderByDescending(b => b.CreatedAt).Take(50)
            .Select(b => new ApiBill(b.Id, b.BillNumber, b.Status.ToString(), b.CreatedAt,
                b.GrossAmount, b.NetAmount, b.PaidAmount, Math.Max(0, b.NetAmount - b.PaidAmount)))
            .ToListAsync(ct);
        return Ok(bills);
    }

    private static ApiVisitSummary ToVisitSummary(Encounter e) => new(
        e.Id, e.StartedAt, e.Status.ToString(), e.Type.ToString(),
        e.ChiefComplaint, e.Clinic?.Name, e.Clinician?.FullName);
}
