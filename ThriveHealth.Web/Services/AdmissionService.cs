using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Hubs;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Inpatient;

namespace ThriveHealth.Web.Services;

public record AdmitRequest(
    int FacilityId,
    int PatientId,
    int WardId,
    int BedId,
    string AdmittingDoctorId,
    string ReasonForAdmission,
    string? WorkingDiagnosis,
    int? SourceEncounterId);

public record DischargeRequest(
    int AdmissionId,
    DischargeDisposition Disposition,
    string? DischargeDiagnosis,
    string? Summary,
    string? FollowUp,
    string DischargingUserId);

public record TransferRequest(
    int AdmissionId,
    int NewBedId,
    string? Reason,
    string AllocatingUserId);

public interface IAdmissionService
{
    Task<Admission> AdmitAsync(AdmitRequest req, CancellationToken ct = default);
    Task<bool> DischargeAsync(DischargeRequest req, CancellationToken ct = default);
    Task<bool> TransferAsync(TransferRequest req, CancellationToken ct = default);
}

public class AdmissionService : IAdmissionService
{
    private readonly ApplicationDbContext _db;
    private readonly IHubContext<BedHub> _bedHub;

    public AdmissionService(ApplicationDbContext db, IHubContext<BedHub> bedHub)
    {
        _db = db;
        _bedHub = bedHub;
    }

    public async Task<Admission> AdmitAsync(AdmitRequest req, CancellationToken ct = default)
    {
        var bed = await _db.Beds.Include(b => b.Ward).FirstAsync(b => b.Id == req.BedId, ct);
        if (bed.Status != BedStatus.Free) throw new InvalidOperationException("Bed is not free.");
        if (bed.WardId != req.WardId) throw new InvalidOperationException("Bed not in ward.");

        var encounter = new Encounter
        {
            FacilityId = req.FacilityId,
            PatientId = req.PatientId,
            ClinicId = await _db.Clinics.Where(c => c.FacilityId == req.FacilityId).Select(c => c.Id).FirstAsync(ct),
            ClinicianId = req.AdmittingDoctorId,
            Type = EncounterType.InpatientAdmission,
            Status = EncounterStatus.InProgress,
            ChiefComplaint = req.ReasonForAdmission,
            Soap = new SoapNote()
        };
        _db.Encounters.Add(encounter);

        var admission = new Admission
        {
            FacilityId = req.FacilityId,
            PatientId = req.PatientId,
            WardId = req.WardId,
            BedId = req.BedId,
            AdmittingDoctorId = req.AdmittingDoctorId,
            SourceEncounterId = req.SourceEncounterId,
            AdmissionEncounter = encounter,
            ReasonForAdmission = req.ReasonForAdmission,
            WorkingDiagnosis = req.WorkingDiagnosis,
            Status = AdmissionStatus.Active,
            AdmittedAt = DateTime.UtcNow
        };
        _db.Admissions.Add(admission);

        bed.Status = BedStatus.Occupied;
        bed.UpdatedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);

        bed.CurrentAdmissionId = admission.Id;
        admission.BedHistory.Add(new BedAllocation
        {
            AdmissionId = admission.Id,
            BedId = bed.Id,
            FromUtc = DateTime.UtcNow,
            AllocatedById = req.AdmittingDoctorId,
            Reason = "Initial admission"
        });

        await _db.SaveChangesAsync(ct);
        await NotifyBedChange(req.FacilityId);
        return admission;
    }

    public async Task<bool> DischargeAsync(DischargeRequest req, CancellationToken ct = default)
    {
        var adm = await _db.Admissions
            .Include(a => a.Bed)
            .Include(a => a.AdmissionEncounter)
            .Include(a => a.BedHistory)
            .FirstOrDefaultAsync(a => a.Id == req.AdmissionId, ct);
        if (adm is null) return false;
        if (adm.Status != AdmissionStatus.Active) return false;

        adm.Status = req.Disposition switch
        {
            DischargeDisposition.Dama => AdmissionStatus.DamaSelfDischarge,
            DischargeDisposition.Absconded => AdmissionStatus.Absconded,
            DischargeDisposition.Deceased => AdmissionStatus.Deceased,
            DischargeDisposition.Transferred => AdmissionStatus.Transferred,
            _ => AdmissionStatus.Discharged
        };
        adm.DischargedAt = DateTime.UtcNow;
        adm.DischargeDisposition = req.Disposition;
        adm.DischargeDiagnosis = req.DischargeDiagnosis;
        adm.DischargeSummary = req.Summary;
        adm.FollowUpPlan = req.FollowUp;
        adm.DischargedById = req.DischargingUserId;

        if (adm.Bed is not null)
        {
            adm.Bed.Status = BedStatus.Cleaning;
            adm.Bed.CurrentAdmissionId = null;
            adm.Bed.UpdatedAt = DateTime.UtcNow;
        }

        var openAlloc = adm.BedHistory.FirstOrDefault(x => x.ToUtc == null);
        if (openAlloc is not null) openAlloc.ToUtc = DateTime.UtcNow;

        if (adm.AdmissionEncounter is not null)
        {
            adm.AdmissionEncounter.Status = EncounterStatus.Signed;
            adm.AdmissionEncounter.SignedAt = DateTime.UtcNow;
            adm.AdmissionEncounter.UpdatedAt = DateTime.UtcNow;
        }

        await _db.SaveChangesAsync(ct);
        await NotifyBedChange(adm.FacilityId);
        return true;
    }

    public async Task<bool> TransferAsync(TransferRequest req, CancellationToken ct = default)
    {
        var adm = await _db.Admissions
            .Include(a => a.Bed)
            .Include(a => a.BedHistory)
            .FirstOrDefaultAsync(a => a.Id == req.AdmissionId, ct);
        if (adm is null || adm.Status != AdmissionStatus.Active) return false;

        var newBed = await _db.Beds.FirstAsync(b => b.Id == req.NewBedId, ct);
        if (newBed.Status != BedStatus.Free) throw new InvalidOperationException("Target bed is not free.");
        if (newBed.Id == adm.BedId) return true;

        var oldBed = adm.Bed!;
        oldBed.Status = BedStatus.Cleaning;
        oldBed.CurrentAdmissionId = null;
        oldBed.UpdatedAt = DateTime.UtcNow;

        var openAlloc = adm.BedHistory.FirstOrDefault(x => x.ToUtc == null);
        if (openAlloc is not null) openAlloc.ToUtc = DateTime.UtcNow;

        newBed.Status = BedStatus.Occupied;
        newBed.CurrentAdmissionId = adm.Id;
        newBed.UpdatedAt = DateTime.UtcNow;

        adm.BedId = newBed.Id;
        adm.WardId = newBed.WardId;
        adm.BedHistory.Add(new BedAllocation
        {
            AdmissionId = adm.Id,
            BedId = newBed.Id,
            FromUtc = DateTime.UtcNow,
            Reason = req.Reason,
            AllocatedById = req.AllocatingUserId
        });

        await _db.SaveChangesAsync(ct);
        await NotifyBedChange(adm.FacilityId);
        return true;
    }

    private Task NotifyBedChange(int facilityId) =>
        _bedHub.Clients.Group($"beds-{facilityId}").SendAsync("bedsUpdated", new { facilityId });
}
