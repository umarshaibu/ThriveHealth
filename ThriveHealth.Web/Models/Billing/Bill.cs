using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.Billing;

public enum BillStatus
{
    Open = 1,
    PartiallyPaid = 2,
    Paid = 3,
    Cancelled = 4,
    WrittenOff = 5
}

public enum BillItemKind
{
    Consultation = 1,
    Lab = 2,
    Imaging = 3,
    Drug = 4,
    Procedure = 5,
    BedDay = 6,
    Theatre = 7,
    Other = 99
}

public class Bill
{
    public int Id { get; set; }
    public int FacilityId { get; set; }
    public Facility? Facility { get; set; }

    [Required, MaxLength(40)] public string BillNumber { get; set; } = string.Empty;

    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public int? EncounterId { get; set; }
    public Encounter? Encounter { get; set; }

    public BillStatus Status { get; set; } = BillStatus.Open;

    public DateOnly ServiceDate { get; set; } = DateOnly.FromDateTime(DateTime.UtcNow);
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public decimal GrossAmount { get; set; }
    public decimal DiscountAmount { get; set; }
    public decimal NetAmount { get; set; }
    public decimal PaidAmount { get; set; }

    public decimal Balance => Math.Max(0, NetAmount - PaidAmount);

    [MaxLength(500)] public string? Notes { get; set; }
    [MaxLength(200)] public string? DiscountReason { get; set; }

    public string? CreatedById { get; set; }
    public ApplicationUser? CreatedBy { get; set; }

    public ICollection<BillItem> Items { get; set; } = new List<BillItem>();
    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}

public class BillItem
{
    public int Id { get; set; }

    public int BillId { get; set; }
    public Bill? Bill { get; set; }

    public BillItemKind Kind { get; set; }
    [Required, MaxLength(200)] public string Description { get; set; } = string.Empty;
    [MaxLength(20)] public string? ServiceCode { get; set; }

    public int Quantity { get; set; } = 1;
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal LineDiscount { get; set; }
    public decimal LineNet { get; set; }

    public int? LabOrderId { get; set; }
    public int? ImagingOrderId { get; set; }
    public int? PrescriptionItemId { get; set; }
    public int? ProcedureOrderId { get; set; }
    public int? DispenseItemId { get; set; }
    public int? TheatreSessionId { get; set; }
}

public enum PaymentMethod { Cash = 1, Pos = 2, BankTransfer = 3, MobileMoney = 4, Cheque = 5, Voucher = 6 }
public enum PaymentStatus { Recorded = 1, Reversed = 2 }

public class Payment
{
    public int Id { get; set; }

    public int BillId { get; set; }
    public Bill? Bill { get; set; }

    public int? CashierShiftId { get; set; }
    public CashierShift? CashierShift { get; set; }

    [Required, MaxLength(40)] public string ReceiptNumber { get; set; } = string.Empty;

    public PaymentMethod Method { get; set; }
    public PaymentStatus Status { get; set; } = PaymentStatus.Recorded;

    public decimal Amount { get; set; }
    [MaxLength(60)] public string? Reference { get; set; }

    public DateTime ReceivedAt { get; set; } = DateTime.UtcNow;
    public string? CashierId { get; set; }
    public ApplicationUser? Cashier { get; set; }

    [MaxLength(300)] public string? Notes { get; set; }
}

public enum CashierShiftStatus { Open = 1, Closed = 2 }

public class CashierShift
{
    public int Id { get; set; }
    public int FacilityId { get; set; }

    public string CashierId { get; set; } = string.Empty;
    public ApplicationUser? Cashier { get; set; }

    [Required, MaxLength(40)] public string ShiftNumber { get; set; } = string.Empty;

    public CashierShiftStatus Status { get; set; } = CashierShiftStatus.Open;

    public DateTime OpenedAt { get; set; } = DateTime.UtcNow;
    public DateTime? ClosedAt { get; set; }

    public decimal OpeningFloat { get; set; }
    public decimal CountedCash { get; set; }
    public decimal Variance { get; set; }

    [MaxLength(500)] public string? Notes { get; set; }

    public ICollection<Payment> Payments { get; set; } = new List<Payment>();
}
