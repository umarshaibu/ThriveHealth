using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Insurance;
using ThriveHealth.Web.Models.Pharmacy;

namespace ThriveHealth.Web.Services;

public record ClaimSettlement(decimal ApprovedAmount, decimal PaidAmount, string? PayerReference, string? Notes);
public record DenialDetails(string Reason, string? Notes);

public interface IClaimsService
{
    Task<int> BuildFromEncounterAsync(int facilityId, int encounterId, int payerId, int? payerPlanId, string userId, CancellationToken ct = default);
    Task<bool> SubmitAsync(int claimId, string userId, CancellationToken ct = default);
    Task<bool> SettleAsync(int claimId, ClaimSettlement settlement, string userId, CancellationToken ct = default);
    Task<bool> DenyAsync(int claimId, DenialDetails details, string userId, CancellationToken ct = default);
    Task<string> GenerateReferenceAsync(string payerCode, CancellationToken ct = default);
}

public class ClaimsService : IClaimsService
{
    private const decimal ConsultationListPrice = 5_000m;

    private static readonly IReadOnlyDictionary<ImagingModality, decimal> ImagingListPrice =
        new Dictionary<ImagingModality, decimal>
        {
            [ImagingModality.XRay] = 5_000m,
            [ImagingModality.Ultrasound] = 8_000m,
            [ImagingModality.CT] = 25_000m,
            [ImagingModality.MRI] = 50_000m,
            [ImagingModality.Mammography] = 12_000m,
            [ImagingModality.Fluoroscopy] = 10_000m,
            [ImagingModality.Other] = 6_000m
        };

    private const decimal ProcedureListPrice = 3_000m;

    private readonly ApplicationDbContext _db;
    public ClaimsService(ApplicationDbContext db) => _db = db;

    public async Task<string> GenerateReferenceAsync(string payerCode, CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var month = DateTime.UtcNow.Month;
        var prefix = $"CLM-{payerCode}-{year}{month:D2}-";
        var existing = await _db.Claims.AsNoTracking()
            .Where(c => c.ClaimReference != null && c.ClaimReference.StartsWith(prefix))
            .Select(c => c.ClaimReference!).ToListAsync(ct);
        int next = 1;
        foreach (var r in existing)
        {
            var tail = r.Substring(prefix.Length);
            if (int.TryParse(tail, out var n) && n >= next) next = n + 1;
        }
        return $"{prefix}{next:D5}";
    }

    public async Task<int> BuildFromEncounterAsync(int facilityId, int encounterId, int payerId, int? payerPlanId, string userId, CancellationToken ct = default)
    {
        var enc = await _db.Encounters
            .Include(e => e.LabOrders).ThenInclude(o => o.LabTest)
            .Include(e => e.ImagingOrders)
            .Include(e => e.ProcedureOrders)
            .Include(e => e.Prescriptions).ThenInclude(p => p.Items).ThenInclude(i => i.Drug)
            .FirstOrDefaultAsync(e => e.Id == encounterId && e.FacilityId == facilityId, ct)
            ?? throw new InvalidOperationException("Consultation not found.");

        var payer = await _db.Payers.FirstOrDefaultAsync(p => p.Id == payerId, ct)
            ?? throw new InvalidOperationException("Payer not found.");

        PayerPlan? plan = null;
        if (payerPlanId.HasValue)
            plan = await _db.PayerPlans.Include(p => p.Formulary).FirstOrDefaultAsync(p => p.Id == payerPlanId.Value, ct);
        plan ??= await _db.PayerPlans.Include(p => p.Formulary).FirstOrDefaultAsync(p => p.PayerId == payerId, ct);

        decimal multiplier = plan?.TariffMultiplier ?? 1.0m;
        decimal defaultCopay = plan?.DefaultCopayPercent ?? 0m;
        bool defaultCovered = plan?.DefaultFormularyCovered ?? true;

        var dispenseItems = await _db.DispenseItems.AsNoTracking()
            .Include(di => di.Dispense).ThenInclude(d => d!.Prescription)
            .Where(di => di.Dispense!.Prescription!.EncounterId == encounterId)
            .ToListAsync(ct);

        var claim = new Claim
        {
            FacilityId = facilityId,
            PayerId = payerId,
            PayerPlanId = plan?.Id,
            PatientId = enc.PatientId,
            EncounterId = enc.Id,
            ServiceDate = DateOnly.FromDateTime(enc.StartedAt),
            Status = ClaimStatus.Draft,
            CreatedById = userId,
            ClaimReference = await GenerateReferenceAsync(payer.Code, ct)
        };

        ClaimItem MakeItem(ClaimItemKind kind, string desc, string? code, decimal listPrice,
            int qty = 1, decimal copayPct = 0, bool covered = true, string? denialReason = null)
        {
            var unit = Math.Round(listPrice * multiplier, 2);
            var line = unit * qty;
            var copay = Math.Round(line * copayPct / 100m, 2);
            var claimable = covered ? Math.Max(0, line - copay) : 0;
            return new ClaimItem
            {
                Kind = kind, Description = desc, ServiceCode = code,
                Quantity = qty, UnitPrice = unit, LineTotal = line,
                CopayAmount = copay, ClaimableAmount = claimable,
                IsCovered = covered, DenialReason = covered ? null : denialReason
            };
        }

        if (enc.Type == EncounterType.OutpatientOpd
         || enc.Type == EncounterType.OutpatientFollowUp
         || enc.Type == EncounterType.SpecialistClinic
         || enc.Type == EncounterType.Emergency
         || enc.Type == EncounterType.AntenatalVisit
         || enc.Type == EncounterType.Telemedicine)
        {
            claim.Items.Add(MakeItem(ClaimItemKind.Consultation,
                enc.Type == EncounterType.Emergency ? "A&E consultation" : "OPD consultation",
                "CONS-001", ConsultationListPrice, 1, defaultCopay, true));
        }

        foreach (var lo in enc.LabOrders.Where(o => o.Status != OrderStatus.Cancelled))
        {
            var price = lo.LabTest?.Price ?? 1_500m;
            var item = MakeItem(ClaimItemKind.Lab, lo.TestName, lo.LabTest?.Code, price, 1, defaultCopay, true);
            item.LabOrderId = lo.Id;
            claim.Items.Add(item);
        }

        foreach (var io in enc.ImagingOrders.Where(o => o.Status != OrderStatus.Cancelled))
        {
            var price = ImagingListPrice.GetValueOrDefault(io.Modality, 6_000m);
            var item = MakeItem(ClaimItemKind.Imaging, $"{io.Modality} · {io.StudyDescription}", io.Modality.ToString(), price, 1, defaultCopay, true);
            item.ImagingOrderId = io.Id;
            claim.Items.Add(item);
        }

        foreach (var po in enc.ProcedureOrders.Where(o => o.Status != OrderStatus.Cancelled))
        {
            var item = MakeItem(ClaimItemKind.Procedure, po.ProcedureName, po.CptCode ?? "PROC-GEN", ProcedureListPrice, 1, defaultCopay, true);
            item.ProcedureOrderId = po.Id;
            claim.Items.Add(item);
        }

        var formulary = plan?.Formulary.ToDictionary(f => f.DrugId);
        if (dispenseItems.Any())
        {
            foreach (var di in dispenseItems)
            {
                var unitListPrice = di.UnitPrice ?? di.Drug?.UnitPrice ?? 0m;
                bool covered = defaultCovered;
                decimal copayPct = defaultCopay;
                string? denial = null;
                if (di.DrugId.HasValue && formulary != null && formulary.TryGetValue(di.DrugId.Value, out var f))
                {
                    covered = f.IsCovered;
                    copayPct = f.CopayPercent;
                    if (!f.IsCovered) denial = f.Notes ?? "Drug excluded from formulary.";
                }
                else if (di.DrugId.HasValue && formulary != null && !formulary.ContainsKey(di.DrugId.Value))
                {
                    if (!defaultCovered) denial = "Drug not on plan's formulary.";
                }

                var item = MakeItem(ClaimItemKind.Drug,
                    di.DrugName, di.NafdacNumber, unitListPrice, di.QuantityDispensed, copayPct, covered, denial);
                item.NafdacNumber = di.NafdacNumber;
                item.DispenseItemId = di.Id;
                item.PrescriptionItemId = di.PrescriptionItemId;
                claim.Items.Add(item);
            }
        }
        else
        {
            foreach (var rx in enc.Prescriptions)
            {
                foreach (var i in rx.Items)
                {
                    var unitListPrice = i.Drug?.UnitPrice ?? 0m;
                    if (unitListPrice == 0) continue;
                    var qty = i.Quantity ?? 1;
                    bool covered = defaultCovered;
                    decimal copayPct = defaultCopay;
                    string? denial = null;
                    if (i.DrugId.HasValue && formulary != null && formulary.TryGetValue(i.DrugId.Value, out var f))
                    {
                        covered = f.IsCovered;
                        copayPct = f.CopayPercent;
                        if (!f.IsCovered) denial = f.Notes ?? "Drug excluded from formulary.";
                    }
                    var item = MakeItem(ClaimItemKind.Drug,
                        i.DrugName, i.NafdacNumber, unitListPrice, qty, copayPct, covered, denial);
                    item.NafdacNumber = i.NafdacNumber;
                    item.PrescriptionItemId = i.Id;
                    claim.Items.Add(item);
                }
            }
        }

        claim.GrossAmount = claim.Items.Sum(x => x.LineTotal);
        claim.CopayAmount = claim.Items.Sum(x => x.CopayAmount);
        claim.ClaimableAmount = claim.Items.Sum(x => x.ClaimableAmount);

        _db.Claims.Add(claim);
        await _db.SaveChangesAsync(ct);
        return claim.Id;
    }

    public async Task<bool> SubmitAsync(int claimId, string userId, CancellationToken ct = default)
    {
        var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == claimId, ct);
        if (claim is null) return false;
        if (claim.Status != ClaimStatus.Draft) return false;

        claim.Status = ClaimStatus.Submitted;
        claim.SubmittedAt = DateTime.UtcNow;
        claim.SubmittedById = userId;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> SettleAsync(int claimId, ClaimSettlement settlement, string userId, CancellationToken ct = default)
    {
        var claim = await _db.Claims.Include(c => c.Items).FirstOrDefaultAsync(c => c.Id == claimId, ct);
        if (claim is null) return false;
        if (claim.Status == ClaimStatus.Closed || claim.Status == ClaimStatus.Draft) return false;

        claim.ApprovedAmount = settlement.ApprovedAmount;
        claim.PaidAmount = settlement.PaidAmount;
        claim.PayerReference = settlement.PayerReference;
        claim.Notes = settlement.Notes;
        claim.RespondedAt = DateTime.UtcNow;

        if (settlement.PaidAmount >= claim.ClaimableAmount && claim.ClaimableAmount > 0)
            claim.Status = ClaimStatus.Paid;
        else if (settlement.PaidAmount > 0)
            claim.Status = ClaimStatus.PartiallyPaid;
        else
            claim.Status = ClaimStatus.Acknowledged;

        if (claim.ClaimableAmount > 0 && claim.Items.Any())
        {
            var ratio = settlement.ApprovedAmount / claim.ClaimableAmount;
            foreach (var i in claim.Items.Where(x => x.IsCovered))
                i.ApprovedAmount = Math.Round(i.ClaimableAmount * ratio, 2);
        }

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> DenyAsync(int claimId, DenialDetails details, string userId, CancellationToken ct = default)
    {
        var claim = await _db.Claims.FirstOrDefaultAsync(c => c.Id == claimId, ct);
        if (claim is null) return false;
        if (claim.Status == ClaimStatus.Closed || claim.Status == ClaimStatus.Paid) return false;

        claim.Status = ClaimStatus.Denied;
        claim.DenialReason = details.Reason;
        claim.Notes = details.Notes;
        claim.RespondedAt = DateTime.UtcNow;
        claim.ApprovedAmount = 0;
        claim.PaidAmount = 0;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
