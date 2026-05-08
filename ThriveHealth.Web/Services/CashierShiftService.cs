using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Billing;

namespace ThriveHealth.Web.Services;

public record ShiftSummary(int Id, string ShiftNumber, decimal Cash, decimal Pos, decimal Transfer, decimal MobileMoney, decimal Cheque, decimal Total, int Receipts);

public interface ICashierShiftService
{
    Task<int> OpenAsync(int facilityId, string cashierId, decimal openingFloat, CancellationToken ct = default);
    Task<int?> GetCurrentShiftIdAsync(string cashierId, CancellationToken ct = default);
    Task<ShiftSummary> SummariseAsync(int shiftId, CancellationToken ct = default);
    Task<bool> CloseAsync(int shiftId, decimal countedCash, string? notes, CancellationToken ct = default);
}

public class CashierShiftService : ICashierShiftService
{
    private readonly ApplicationDbContext _db;
    private readonly IBillingService _billing;
    public CashierShiftService(ApplicationDbContext db, IBillingService billing)
    {
        _db = db;
        _billing = billing;
    }

    public async Task<int> OpenAsync(int facilityId, string cashierId, decimal openingFloat, CancellationToken ct = default)
    {
        var existing = await _db.CashierShifts.FirstOrDefaultAsync(
            s => s.CashierId == cashierId && s.Status == CashierShiftStatus.Open, ct);
        if (existing is not null) return existing.Id;

        var shift = new CashierShift
        {
            FacilityId = facilityId,
            CashierId = cashierId,
            ShiftNumber = await _billing.GenerateShiftNumberAsync(ct),
            OpeningFloat = openingFloat,
            OpenedAt = DateTime.UtcNow,
            Status = CashierShiftStatus.Open
        };
        _db.CashierShifts.Add(shift);
        await _db.SaveChangesAsync(ct);
        return shift.Id;
    }

    public Task<int?> GetCurrentShiftIdAsync(string cashierId, CancellationToken ct = default) =>
        _db.CashierShifts
            .Where(s => s.CashierId == cashierId && s.Status == CashierShiftStatus.Open)
            .Select(s => (int?)s.Id)
            .FirstOrDefaultAsync(ct);

    public async Task<ShiftSummary> SummariseAsync(int shiftId, CancellationToken ct = default)
    {
        var shift = await _db.CashierShifts
            .Include(s => s.Payments)
            .FirstAsync(s => s.Id == shiftId, ct);

        var posted = shift.Payments.Where(p => p.Status == PaymentStatus.Recorded).ToList();
        decimal sum(PaymentMethod m) => posted.Where(p => p.Method == m).Sum(p => p.Amount);

        var cash = sum(PaymentMethod.Cash);
        var pos = sum(PaymentMethod.Pos);
        var transfer = sum(PaymentMethod.BankTransfer);
        var mobile = sum(PaymentMethod.MobileMoney);
        var cheque = sum(PaymentMethod.Cheque);
        return new ShiftSummary(shift.Id, shift.ShiftNumber, cash, pos, transfer, mobile, cheque,
            cash + pos + transfer + mobile + cheque, posted.Count);
    }

    public async Task<bool> CloseAsync(int shiftId, decimal countedCash, string? notes, CancellationToken ct = default)
    {
        var shift = await _db.CashierShifts
            .Include(s => s.Payments)
            .FirstOrDefaultAsync(s => s.Id == shiftId, ct);
        if (shift is null) return false;
        if (shift.Status == CashierShiftStatus.Closed) return false;

        var summary = await SummariseAsync(shiftId, ct);
        var expectedCash = summary.Cash + shift.OpeningFloat;

        shift.CountedCash = countedCash;
        shift.Variance = countedCash - expectedCash;
        shift.Notes = notes;
        shift.ClosedAt = DateTime.UtcNow;
        shift.Status = CashierShiftStatus.Closed;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
