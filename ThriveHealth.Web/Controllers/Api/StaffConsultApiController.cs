using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Api;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Telemedicine;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers.Api;

/// <summary>
/// Clinician consult workspace — document a tele-consult: SOAP notes, prescriptions, lab and
/// imaging orders. All actions are scoped to a session assigned to the calling clinician
/// (enforced by ITeleConsultActions / ITelemedicineService.SaveNotesAsync).
/// </summary>
[Route("api/v1/staff/consults")]
public class StaffConsultApiController : StaffApiControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly ITelemedicineService _tele;
    private readonly ITeleConsultActions _actions;

    public StaffConsultApiController(ApplicationDbContext db, ITelemedicineService tele, ITeleConsultActions actions)
    {
        _db = db; _tele = tele; _actions = actions;
    }

    private static OrderUrgency ParseUrgency(string? u) =>
        Enum.TryParse<OrderUrgency>(u, ignoreCase: true, out var v) ? v : OrderUrgency.Routine;

    /// <summary>Ensures the consult (assigned to this clinician) has a clinical encounter so notes and
    /// orders attach to the patient's record — without flipping the call state or notifying the patient.</summary>
    private async Task EnsureEncounterAsync(int sessionId, CancellationToken ct)
    {
        var s = await _db.TeleSessions.FirstOrDefaultAsync(
            x => x.Id == sessionId && x.ClinicianId == UserId && x.FacilityId == FacilityId, ct);
        if (s is null || s.EncounterId is not null) return;

        var clinic = await _db.Clinics.AsNoTracking()
            .Where(c => c.FacilityId == s.FacilityId)
            .OrderBy(c => c.Code == "OPD" ? 0 : 1).FirstOrDefaultAsync(ct);
        if (clinic is null) return;

        var enc = new Encounter
        {
            FacilityId = s.FacilityId,
            PatientId = s.PatientId,
            ClinicId = clinic.Id,
            ClinicianId = s.ClinicianId!,
            Type = EncounterType.Telemedicine,
            Status = EncounterStatus.InProgress,
            StartedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            ChiefComplaint = s.ConsultationReason,
            Soap = new SoapNote { UpdatedAt = DateTime.UtcNow, UpdatedById = s.ClinicianId },
        };
        _db.Encounters.Add(enc);
        await _db.SaveChangesAsync(ct);
        s.EncounterId = enc.Id;
        await _db.SaveChangesAsync(ct);
    }

    private async Task<ApiConsultWorkspace?> BuildWorkspaceAsync(int sessionId, CancellationToken ct)
    {
        var s = await _db.TeleSessions.AsNoTracking()
            .Include(x => x.Patient)
            .Include(x => x.Encounter).ThenInclude(e => e!.Soap)
            .Include(x => x.Encounter).ThenInclude(e => e!.Prescriptions).ThenInclude(p => p.Items)
            .Include(x => x.Encounter).ThenInclude(e => e!.LabOrders).ThenInclude(o => o.LabTest)
            .Include(x => x.Encounter).ThenInclude(e => e!.ImagingOrders)
            .FirstOrDefaultAsync(x => x.Id == sessionId && x.ClinicianId == UserId && x.FacilityId == FacilityId, ct);
        if (s is null) return null;

        var enc = s.Encounter;
        var soap = new ApiSoap(enc?.Soap?.Subjective, enc?.Soap?.Objective, enc?.Soap?.Assessment, enc?.Soap?.Plan);

        var prescriptions = (enc?.Prescriptions ?? new List<Prescription>())
            .Select(p => new ApiPrescription(p.Id, p.Status.ToString(),
                p.Items.Select(i => string.Join(" ", new[] { i.DrugName, i.Dose, i.Frequency, i.Duration }
                    .Where(x => !string.IsNullOrWhiteSpace(x))).Trim()).ToList()))
            .ToList();

        var labs = (enc?.LabOrders ?? new List<LabOrder>())
            .Select(o => new ApiOrderLine(o.Id, o.LabTest != null ? o.LabTest.Name : "Lab test", o.Status.ToString()))
            .ToList();

        var imaging = (enc?.ImagingOrders ?? new List<ImagingOrder>())
            .Select(o => new ApiOrderLine(o.Id, $"{o.Modality} — {o.StudyDescription}", o.Status.ToString()))
            .ToList();

        return new ApiConsultWorkspace(s.Id, s.Patient?.FullName ?? "", s.Patient?.HospitalNumber ?? "",
            s.Mode.ToString(), s.Status.ToString(), soap, prescriptions, labs, imaging);
    }

    [HttpGet("{id:int}")]
    [ProducesResponseType(typeof(ApiConsultWorkspace), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Workspace(int id, CancellationToken ct)
    {
        var ws = await BuildWorkspaceAsync(id, ct);
        return ws is null ? NotFound(new ApiError("Consult not found or not assigned to you.", "not_found")) : Ok(ws);
    }

    [HttpPost("{id:int}/notes")]
    [ProducesResponseType(typeof(ApiConsultWorkspace), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> SaveNotes(int id, [FromBody] ApiSaveNotesRequest req, CancellationToken ct)
    {
        await EnsureEncounterAsync(id, ct);
        var ok = await _tele.SaveNotesAsync(id, UserId, req.Subjective, req.Objective, req.Assessment, req.Plan, ct);
        if (!ok) return BadRequest(new ApiError("Could not save notes for this consult.", "save_failed"));
        return Ok(await BuildWorkspaceAsync(id, ct));
    }

    [HttpPost("{id:int}/prescriptions")]
    [ProducesResponseType(typeof(ApiConsultWorkspace), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Prescribe(int id, [FromBody] ApiPrescribeRequest req, CancellationToken ct)
    {
        if (!ModelState.IsValid || string.IsNullOrWhiteSpace(req.DrugName))
            return BadRequest(new ApiError("Drug name is required.", "bad_request"));
        await EnsureEncounterAsync(id, ct);
        var created = await _actions.AddPrescriptionItemAsync(id, UserId,
            new PrescriptionItemInput(null, req.DrugName.Trim(), req.Dose, req.Route, req.Frequency, req.Duration, req.Quantity, req.Instructions), ct);
        if (created is null) return BadRequest(new ApiError("Could not add prescription (consult not assigned to you).", "failed"));
        return Ok(await BuildWorkspaceAsync(id, ct));
    }

    [HttpPost("{id:int}/lab-orders")]
    [ProducesResponseType(typeof(ApiConsultWorkspace), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> LabOrder(int id, [FromBody] ApiLabOrderRequest req, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(req.TestName)) return BadRequest(new ApiError("Test name is required.", "bad_request"));
        await EnsureEncounterAsync(id, ct);
        var created = await _actions.CreateLabOrderAsync(id, UserId,
            new LabOrderInput(null, req.TestName.Trim(), req.ClinicalIndication, ParseUrgency(req.Urgency)), ct);
        if (created is null) return BadRequest(new ApiError("Could not create lab order.", "failed"));
        return Ok(await BuildWorkspaceAsync(id, ct));
    }

    [HttpPost("{id:int}/imaging-orders")]
    [ProducesResponseType(typeof(ApiConsultWorkspace), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> ImagingOrder(int id, [FromBody] ApiImagingOrderRequest req, CancellationToken ct)
    {
        if (!Enum.TryParse<ImagingModality>(req.Modality, ignoreCase: true, out var modality))
            return BadRequest(new ApiError("Unknown imaging modality.", "bad_modality"));
        if (string.IsNullOrWhiteSpace(req.StudyDescription)) return BadRequest(new ApiError("Study description is required.", "bad_request"));
        await EnsureEncounterAsync(id, ct);
        var created = await _actions.CreateImagingOrderAsync(id, UserId,
            new ImagingOrderInput(modality, req.StudyDescription.Trim(), req.ClinicalIndication, ParseUrgency(req.Urgency)), ct);
        if (created is null) return BadRequest(new ApiError("Could not create imaging order.", "failed"));
        return Ok(await BuildWorkspaceAsync(id, ct));
    }
}
