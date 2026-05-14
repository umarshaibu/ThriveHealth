using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Billing;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Pharmacy;

namespace ThriveHealth.Web.Services;

public record PaymentInput(PaymentMethod Method, decimal Amount, string? Reference, string? Notes);

public interface IBillingService
{
    Task<string> GenerateBillNumberAsync(int facilityId, CancellationToken ct = default);
    Task<string> GenerateReceiptNumberAsync(CancellationToken ct = default);
    Task<string> GenerateShiftNumberAsync(CancellationToken ct = default);

    Task<int> BuildBillFromEncounterAsync(int facilityId, int encounterId, string userId, CancellationToken ct = default);
    Task<int> CreateAdHocBillAsync(int facilityId, int patientId, IEnumerable<BillItem> items, string userId, CancellationToken ct = default);
    Task ApplyDiscountAsync(int billId, decimal discountAmount, string? reason, CancellationToken ct = default);
    Task<int> RecordPaymentsAsync(int billId, IEnumerable<PaymentInput> payments, int? cashierShiftId, string userId, CancellationToken ct = default);
}

public class BillingService : IBillingService
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
    public BillingService(ApplicationDbContext db) => _db = db;

    public async Task<string> GenerateBillNumberAsync(int facilityId, CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"BILL-{year}-";
        var existing = await _db.Bills.AsNoTracking()
            .Where(b => b.BillNumber.StartsWith(prefix))
            .Select(b => b.BillNumber).ToListAsync(ct);
        int next = 1;
        foreach (var b in existing)
        {
            var tail = b.Substring(prefix.Length);
            if (int.TryParse(tail, out var n) && n >= next) next = n + 1;
        }
        return $"{prefix}{next:D6}";
    }

    public async Task<string> GenerateReceiptNumberAsync(CancellationToken ct = default)
    {
        var year = DateTime.UtcNow.Year;
        var prefix = $"RCT-{year}-";
        var existing = await _db.Payments.AsNoTracking()
            .Where(r => r.ReceiptNumber.StartsWith(prefix))
            .Select(r => r.ReceiptNumber).ToListAsync(ct);
        int next = 1;
        foreach (var r in existing)
        {
            var tail = r.Substring(prefix.Length);
            if (int.TryParse(tail, out var n) && n >= next) next = n + 1;
        }
        return $"{prefix}{next:D6}";
    }

    public async Task<string> GenerateShiftNumberAsync(CancellationToken ct = default)
    {
        var date = DateTime.UtcNow;
        var prefix = $"SHIFT-{date:yyyyMMdd}-";
        var existing = await _db.CashierShifts.AsNoTracking()
            .Where(s => s.ShiftNumber.StartsWith(prefix))
            .Select(s => s.ShiftNumber).ToListAsync(ct);
        int next = 1;
        foreach (var s in existing)
        {
            var tail = s.Substring(prefix.Length);
            if (int.TryParse(tail, out var n) && n >= next) next = n + 1;
        }
        return $"{prefix}{next:D3}";
    }

    public async Task<int> BuildBillFromEncounterAsync(int facilityId, int encounterId, string userId, CancellationToken ct = default)
    {
        var enc = await _db.Encounters
            .Include(e => e.LabOrders).ThenInclude(o => o.LabTest)
            .Include(e => e.ImagingOrders)
            .Include(e => e.ProcedureOrders)
            .Include(e => e.Prescriptions).ThenInclude(p => p.Items).ThenInclude(i => i.Drug)
            .FirstOrDefaultAsync(e => e.Id == encounterId && e.FacilityId == facilityId, ct)
            ?? throw new InvalidOperationException("Consultation not found.");

        var existing = await _db.Bills.FirstOrDefaultAsync(b => b.EncounterId == encounterId && b.Status != BillStatus.Cancelled, ct);
        if (existing is not null) throw new InvalidOperationException($"A bill already exists for this consultation (#{existing.Id}).");

        var dispenseItems = await _db.DispenseItems.AsNoTracking()
            .Include(di => di.Dispense).ThenInclude(d => d!.Prescription)
            .Where(di => di.Dispense!.Prescription!.EncounterId == encounterId)
            .ToListAsync(ct);

        var bill = new Bill
        {
            FacilityId = facilityId,
            BillNumber = await GenerateBillNumberAsync(facilityId, ct),
            PatientId = enc.PatientId,
            EncounterId = enc.Id,
            ServiceDate = DateOnly.FromDateTime(enc.StartedAt),
            CreatedAt = DateTime.UtcNow,
            CreatedById = userId,
            Status = BillStatus.Open
        };

        BillItem MakeItem(BillItemKind kind, string desc, string? code, decimal unit, int qty)
        {
            var line = unit * qty;
            return new BillItem
            {
                Kind = kind, Description = desc, ServiceCode = code,
                Quantity = qty, UnitPrice = unit, LineTotal = line, LineDiscount = 0, LineNet = line
            };
        }

        if (enc.Type is EncounterType.OutpatientOpd or EncounterType.OutpatientFollowUp
            or EncounterType.SpecialistClinic or EncounterType.Emergency
            or EncounterType.AntenatalVisit or EncounterType.Telemedicine)
        {
            bill.Items.Add(MakeItem(BillItemKind.Consultation,
                enc.Type == EncounterType.Emergency ? "A&E consultation" : "OPD consultation",
                "CONS-001", ConsultationListPrice, 1));
        }

        foreach (var lo in enc.LabOrders.Where(o => o.Status != OrderStatus.Cancelled))
        {
            var price = lo.LabTest?.Price ?? 1_500m;
            var item = MakeItem(BillItemKind.Lab, lo.TestName, lo.LabTest?.Code, price, 1);
            item.LabOrderId = lo.Id;
            bill.Items.Add(item);
        }

        foreach (var io in enc.ImagingOrders.Where(o => o.Status != OrderStatus.Cancelled))
        {
            var price = ImagingListPrice.GetValueOrDefault(io.Modality, 6_000m);
            var item = MakeItem(BillItemKind.Imaging, $"{io.Modality} · {io.StudyDescription}", io.Modality.ToString(), price, 1);
            item.ImagingOrderId = io.Id;
            bill.Items.Add(item);
        }

        foreach (var po in enc.ProcedureOrders.Where(o => o.Status != OrderStatus.Cancelled))
        {
            var item = MakeItem(BillItemKind.Procedure, po.ProcedureName, po.CptCode, ProcedureListPrice, 1);
            item.ProcedureOrderId = po.Id;
            bill.Items.Add(item);
        }

        if (dispenseItems.Any())
        {
            foreach (var di in dispenseItems)
            {
                var unit = di.UnitPrice ?? di.Drug?.UnitPrice ?? 0m;
                if (unit == 0 || di.QuantityDispensed == 0) continue;
                var item = MakeItem(BillItemKind.Drug, di.DrugName, di.NafdacNumber, unit, di.QuantityDispensed);
                item.DispenseItemId = di.Id;
                item.PrescriptionItemId = di.PrescriptionItemId;
                bill.Items.Add(item);
            }
        }
        else
        {
            foreach (var rx in enc.Prescriptions)
            foreach (var i in rx.Items)
            {
                var unit = i.Drug?.UnitPrice ?? 0m;
                if (unit == 0) continue;
                var item = MakeItem(BillItemKind.Drug, i.DrugName, i.NafdacNumber, unit, i.Quantity ?? 1);
                item.PrescriptionItemId = i.Id;
                bill.Items.Add(item);
            }
        }

        bill.GrossAmount = bill.Items.Sum(i => i.LineTotal);
        bill.NetAmount = bill.GrossAmount - bill.DiscountAmount;

        _db.Bills.Add(bill);
        await _db.SaveChangesAsync(ct);
        return bill.Id;
    }

    public async Task<int> CreateAdHocBillAsync(int facilityId, int patientId, IEnumerable<BillItem> items, string userId, CancellationToken ct = default)
    {
        var bill = new Bill
        {
            FacilityId = facilityId,
            BillNumber = await GenerateBillNumberAsync(facilityId, ct),
            PatientId = patientId,
            CreatedAt = DateTime.UtcNow,
            // Empty / synthetic ids (portal-N, system) aren't valid FK targets — store NULL for those.
            CreatedById = string.IsNullOrEmpty(userId) || userId.StartsWith("portal-") || userId == "system" ? null : userId,
            Status = BillStatus.Open
        };
        foreach (var i in items)
        {
            i.LineTotal = i.UnitPrice * i.Quantity;
            i.LineNet = i.LineTotal - i.LineDiscount;
            bill.Items.Add(i);
        }
        bill.GrossAmount = bill.Items.Sum(x => x.LineTotal);
        bill.NetAmount = bill.GrossAmount - bill.DiscountAmount;
        _db.Bills.Add(bill);
        await _db.SaveChangesAsync(ct);
        return bill.Id;
    }

    public async Task ApplyDiscountAsync(int billId, decimal discountAmount, string? reason, CancellationToken ct = default)
    {
        var bill = await _db.Bills.FirstAsync(b => b.Id == billId, ct);
        if (bill.Status == BillStatus.Cancelled || bill.Status == BillStatus.Paid)
            throw new InvalidOperationException("Cannot discount this bill.");
        bill.DiscountAmount = Math.Max(0, Math.Min(discountAmount, bill.GrossAmount));
        bill.DiscountReason = reason;
        bill.NetAmount = bill.GrossAmount - bill.DiscountAmount;
        await _db.SaveChangesAsync(ct);
    }

    public async Task<int> RecordPaymentsAsync(int billId, IEnumerable<PaymentInput> payments, int? cashierShiftId, string userId, CancellationToken ct = default)
    {
        var bill = await _db.Bills.Include(b => b.Payments).FirstAsync(b => b.Id == billId, ct);
        if (bill.Status == BillStatus.Cancelled) throw new InvalidOperationException("Bill is cancelled.");

        int recorded = 0;
        foreach (var p in payments.Where(x => x.Amount > 0))
        {
            var pay = new Payment
            {
                BillId = billId,
                CashierShiftId = cashierShiftId,
                ReceiptNumber = await GenerateReceiptNumberAsync(ct),
                Method = p.Method,
                Amount = p.Amount,
                Reference = p.Reference,
                Notes = p.Notes,
                ReceivedAt = DateTime.UtcNow,
                CashierId = string.IsNullOrEmpty(userId) ? null : userId,
                Status = PaymentStatus.Recorded
            };
            _db.Payments.Add(pay);
            recorded++;
        }

        await _db.SaveChangesAsync(ct);
        bill.PaidAmount = await _db.Payments
            .Where(x => x.BillId == billId && x.Status == PaymentStatus.Recorded)
            .SumAsync(x => x.Amount, ct);
        bill.Status = bill.PaidAmount >= bill.NetAmount && bill.NetAmount > 0
            ? BillStatus.Paid
            : (bill.PaidAmount > 0 ? BillStatus.PartiallyPaid : BillStatus.Open);
        if (bill.Status == BillStatus.Paid) bill.ClosedAt = DateTime.UtcNow;

        await _db.SaveChangesAsync(ct);
        return recorded;
    }
}
