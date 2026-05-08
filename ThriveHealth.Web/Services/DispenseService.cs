using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Pharmacy;

namespace ThriveHealth.Web.Services;

public record DispenseLineRequest(
    int PrescriptionItemId,
    int? DrugId,
    string DrugName,
    string? Strength,
    string? NafdacNumber,
    int Quantity,
    string? BatchNumber,
    DateOnly? ExpiryDate,
    decimal? UnitPrice,
    bool IsSubstitution,
    string? SubstitutionReason,
    string? PatientInstructions);

public record DispenseResult(bool Ok, int? DispenseId, string? ErrorMessage);

public interface IDispenseService
{
    Task<DispenseResult> DispenseAsync(
        int facilityId,
        int prescriptionId,
        int storeId,
        IEnumerable<DispenseLineRequest> lines,
        string? counsellingNotes,
        string? notes,
        string userId,
        CancellationToken ct = default);
}

public class DispenseService : IDispenseService
{
    private readonly ApplicationDbContext _db;
    public DispenseService(ApplicationDbContext db) => _db = db;

    public async Task<DispenseResult> DispenseAsync(
        int facilityId, int prescriptionId, int storeId,
        IEnumerable<DispenseLineRequest> lines, string? counsellingNotes, string? notes,
        string userId, CancellationToken ct = default)
    {
        var rx = await _db.Prescriptions
            .Include(p => p.Items).ThenInclude(i => i.Drug)
            .FirstOrDefaultAsync(p => p.Id == prescriptionId, ct);
        if (rx is null) return new(false, null, "Prescription not found.");
        if (rx.Status == PrescriptionStatus.Cancelled) return new(false, null, "Prescription is cancelled.");
        if (rx.Status == PrescriptionStatus.Dispensed) return new(false, null, "Prescription already fully dispensed.");

        var lineList = lines.Where(l => l.Quantity > 0).ToList();
        if (lineList.Count == 0) return new(false, null, "No items to dispense.");

        var dispense = new Dispense
        {
            FacilityId = facilityId,
            PrescriptionId = prescriptionId,
            PatientId = rx.PatientId,
            StoreId = storeId,
            DispensedById = userId,
            DispensedAt = DateTime.UtcNow,
            Status = DispenseStatus.Completed,
            CounsellingNotes = counsellingNotes,
            Notes = notes
        };
        _db.Dispenses.Add(dispense);

        decimal total = 0m;

        foreach (var line in lineList)
        {
            var pi = rx.Items.FirstOrDefault(i => i.Id == line.PrescriptionItemId);
            if (pi is null) return new(false, null, $"Prescription item {line.PrescriptionItemId} not on this Rx.");

            var drugId = line.DrugId ?? pi.DrugId;
            DrugStock? stock = null;

            if (drugId.HasValue && !string.IsNullOrEmpty(line.BatchNumber))
            {
                stock = await _db.DrugStocks
                    .FirstOrDefaultAsync(s => s.DrugId == drugId.Value && s.StoreId == storeId && s.BatchNumber == line.BatchNumber, ct);
                if (stock is null) return new(false, null, $"Batch '{line.BatchNumber}' not found for this drug at this store.");
                if (stock.QuantityOnHand < line.Quantity) return new(false, null, $"Insufficient stock of {line.DrugName} batch {line.BatchNumber} (on hand: {stock.QuantityOnHand}, requested: {line.Quantity}).");
            }

            var unitPrice = line.UnitPrice ?? stock?.UnitCost ?? pi.Drug?.UnitPrice ?? 0m;
            var lineTotal = unitPrice * line.Quantity;
            total += lineTotal;

            var di = new DispenseItem
            {
                Dispense = dispense,
                PrescriptionItemId = pi.Id,
                DrugId = drugId,
                DrugName = line.DrugName,
                Strength = line.Strength,
                NafdacNumber = line.NafdacNumber,
                QuantityDispensed = line.Quantity,
                BatchNumber = line.BatchNumber,
                ExpiryDate = line.ExpiryDate,
                UnitPrice = unitPrice,
                LineTotal = lineTotal,
                IsSubstitution = line.IsSubstitution,
                SubstitutionReason = line.SubstitutionReason,
                PatientInstructions = line.PatientInstructions
            };
            dispense.Items.Add(di);

            pi.QuantityDispensed += line.Quantity;

            if (stock is not null)
            {
                stock.QuantityOnHand -= line.Quantity;
                stock.UpdatedAt = DateTime.UtcNow;
                _db.StockMovements.Add(new StockMovement
                {
                    DrugId = stock.DrugId,
                    StoreId = stock.StoreId,
                    BatchNumber = stock.BatchNumber,
                    ExpiryDate = stock.ExpiryDate,
                    Kind = StockMovementKind.Dispense,
                    Quantity = -line.Quantity,
                    RunningBalance = stock.QuantityOnHand,
                    UnitCost = unitPrice,
                    Reference = $"DISP-{rx.Id}",
                    PerformedById = userId,
                    Notes = $"Dispensed for Rx {rx.Id}"
                });
            }
        }

        dispense.TotalAmount = total;

        var allLinesDispensed = rx.Items.All(i =>
            !i.Quantity.HasValue || i.QuantityDispensed >= i.Quantity.Value);
        rx.Status = allLinesDispensed ? PrescriptionStatus.Dispensed : PrescriptionStatus.PartiallyDispensed;

        await _db.SaveChangesAsync(ct);
        return new(true, dispense.Id, null);
    }
}
