using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Billing;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Patients;
using ThriveHealth.Web.Models.Telemedicine;

namespace ThriveHealth.Web.Services;

public class TelemedicineFees
{
    public decimal Video { get; set; } = 3500m;
    public decimal Audio { get; set; } = 2000m;
    public decimal Chat { get; set; } = 1000m;
    public string Currency { get; set; } = "NGN";

    public decimal For(TeleSessionMode mode) => mode switch
    {
        TeleSessionMode.Audio => Audio,
        TeleSessionMode.Chat => Chat,
        _ => Video
    };
}

public interface ITelemedicineService
{
    TelemedicineFees Fees { get; }
    Task<string> NextSessionNumberAsync(int facilityId, CancellationToken ct = default);
    Task<int> RequestSessionAsync(int facilityId, int patientId, string? reason, TeleSessionMode mode, DateTime scheduledStartUtc, string? requestedById = null, CancellationToken ct = default);
    Task<bool> AssignClinicianAsync(int sessionId, string clinicianId, CancellationToken ct = default);
    Task<bool> PatientJoinAsync(int sessionId, CancellationToken ct = default);
    Task<bool> ClinicianJoinAsync(int sessionId, CancellationToken ct = default);
    Task<bool> EndSessionAsync(int sessionId, string? clinicianNotes, string? clinicianUserId = null, CancellationToken ct = default);
    Task<bool> CancelAsync(int sessionId, CancellationToken ct = default);
    Task<(bool ok, decimal refundAmount)> CancelAsync(int sessionId, string reason, bool patientInitiated = true, CancellationToken ct = default);
    Task<(bool ok, decimal refundAmount)> MarkNoShowAsync(int sessionId, bool patientNoShow, CancellationToken ct = default);
    Task<bool> IsBillSettledAsync(int sessionId, CancellationToken ct = default);
    Task<bool> SaveNotesAsync(int sessionId, string clinicianUserId, string? subjective, string? objective, string? assessment, string? plan, CancellationToken ct = default);
}

public class TelemedicineService : ITelemedicineService
{
    private readonly ApplicationDbContext _db;
    private readonly IBillingService _billing;
    private readonly ITeleNotifier _notifier;
    private readonly IClaimsService _claims;
    private readonly ILogger<TelemedicineService> _log;
    public TelemedicineFees Fees { get; }

    public TelemedicineService(ApplicationDbContext db, IBillingService billing, ITeleNotifier notifier,
        IClaimsService claims, ILogger<TelemedicineService> log, IConfiguration config)
    {
        _db = db;
        _billing = billing;
        _notifier = notifier;
        _claims = claims;
        _log = log;
        Fees = config.GetSection("Telemedicine:Fees").Get<TelemedicineFees>() ?? new TelemedicineFees();
    }

    public async Task<string> NextSessionNumberAsync(int facilityId, CancellationToken ct = default)
    {
        var prefix = $"TM-{DateTime.UtcNow.Year}-";
        var last = await _db.TeleSessions
            .Where(s => s.FacilityId == facilityId && s.SessionNumber.StartsWith(prefix))
            .OrderByDescending(s => s.SessionNumber).Select(s => s.SessionNumber).FirstOrDefaultAsync(ct);
        var next = 1;
        if (!string.IsNullOrEmpty(last))
        {
            var tail = last.Substring(prefix.Length);
            if (int.TryParse(tail, out var n)) next = n + 1;
        }
        return $"{prefix}{next:D5}";
    }

    public async Task<int> RequestSessionAsync(int facilityId, int patientId, string? reason, TeleSessionMode mode, DateTime scheduledStartUtc, string? requestedById = null, CancellationToken ct = default)
    {
        var token = Guid.NewGuid().ToString("N");
        var session = new TeleSession
        {
            FacilityId = facilityId,
            SessionNumber = await NextSessionNumberAsync(facilityId, ct),
            PatientId = patientId,
            Mode = mode,
            ScheduledStartUtc = DateTime.SpecifyKind(scheduledStartUtc, DateTimeKind.Utc),
            Status = TeleSessionStatus.Requested,
            RoomToken = token,
            JoinUrl = $"/Telemedicine/Room/{token}",
            ConsultationReason = reason
        };
        _db.TeleSessions.Add(session);
        await _db.SaveChangesAsync(ct);

        var unitPrice = Fees.For(mode);
        var billItem = new BillItem
        {
            Kind = BillItemKind.Consultation,
            Description = $"Telemedicine consult ({mode}) — {session.SessionNumber}",
            ServiceCode = $"TELE-{mode.ToString().ToUpperInvariant()}",
            Quantity = 1,
            UnitPrice = unitPrice
        };
        var billId = await _billing.CreateAdHocBillAsync(facilityId, patientId, new[] { billItem }, requestedById ?? "system", ct);
        session.BillId = billId;
        await _db.SaveChangesAsync(ct);

        await _notifier.NotifyBillRaisedAsync(session.Id, ct);
        return session.Id;
    }

    public async Task<bool> AssignClinicianAsync(int sessionId, string clinicianId, CancellationToken ct = default)
    {
        var s = await _db.TeleSessions.FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (s is null || s.Status is TeleSessionStatus.Completed or TeleSessionStatus.Cancelled) return false;
        s.ClinicianId = clinicianId;
        if (s.Status == TeleSessionStatus.Requested) s.Status = TeleSessionStatus.Scheduled;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> PatientJoinAsync(int sessionId, CancellationToken ct = default)
    {
        var s = await _db.TeleSessions.FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (s is null) return false;
        s.PatientJoinedAt ??= DateTime.UtcNow;
        if (s.Status is TeleSessionStatus.Scheduled or TeleSessionStatus.Requested)
            s.Status = s.ClinicianJoinedAt.HasValue ? TeleSessionStatus.InCall : TeleSessionStatus.PatientWaiting;
        if (s.ClinicianJoinedAt.HasValue && s.StartedAt is null) s.StartedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> ClinicianJoinAsync(int sessionId, CancellationToken ct = default)
    {
        var s = await _db.TeleSessions.FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (s is null) return false;
        s.ClinicianJoinedAt ??= DateTime.UtcNow;
        if (s.Status is TeleSessionStatus.PatientWaiting or TeleSessionStatus.Scheduled or TeleSessionStatus.Requested)
            s.Status = TeleSessionStatus.InCall;
        if (s.StartedAt is null) s.StartedAt = DateTime.UtcNow;

        // Create the clinical encounter the moment a clinician joins so notes saved during the call
        // land in a real SoapNote (linked to the patient's encounter history) instead of a free-text blob.
        if (s.EncounterId is null && !string.IsNullOrEmpty(s.ClinicianId))
        {
            var clinic = await _db.Clinics.AsNoTracking()
                .Where(c => c.FacilityId == s.FacilityId)
                .OrderBy(c => c.Code == "OPD" ? 0 : 1)
                .FirstOrDefaultAsync(ct);
            if (clinic is not null)
            {
                var enc = new Encounter
                {
                    FacilityId = s.FacilityId,
                    PatientId = s.PatientId,
                    ClinicId = clinic.Id,
                    ClinicianId = s.ClinicianId!,
                    Type = EncounterType.Telemedicine,
                    Status = EncounterStatus.InProgress,
                    StartedAt = s.StartedAt ?? DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ChiefComplaint = s.ConsultationReason,
                    Soap = new SoapNote { UpdatedAt = DateTime.UtcNow, UpdatedById = s.ClinicianId }
                };
                _db.Encounters.Add(enc);
                await _db.SaveChangesAsync(ct);
                s.EncounterId = enc.Id;
            }
        }

        await _db.SaveChangesAsync(ct);
        await _notifier.NotifyClinicianReadyAsync(s.Id, ct);
        return true;
    }

    public async Task<bool> SaveNotesAsync(int sessionId, string clinicianUserId, string? subjective, string? objective, string? assessment, string? plan, CancellationToken ct = default)
    {
        var session = await _db.TeleSessions.Include(s => s.Encounter).ThenInclude(e => e!.Soap)
            .FirstOrDefaultAsync(s => s.Id == sessionId, ct);
        if (session is null || session.Encounter is null) return false;
        if (session.Encounter.ClinicianId != clinicianUserId) return false;

        var soap = session.Encounter.Soap;
        if (soap is null)
        {
            soap = new SoapNote { EncounterId = session.Encounter.Id };
            _db.Set<SoapNote>().Add(soap);
            session.Encounter.Soap = soap;
        }
        soap.Subjective = subjective;
        soap.Objective = objective;
        soap.Assessment = assessment;
        soap.Plan = plan;
        soap.UpdatedAt = DateTime.UtcNow;
        soap.UpdatedById = clinicianUserId;
        session.Encounter.UpdatedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> EndSessionAsync(int sessionId, string? clinicianNotes, string? clinicianUserId = null, CancellationToken ct = default)
    {
        var s = await _db.TeleSessions
            .Include(x => x.Encounter).ThenInclude(e => e!.Soap)
            .FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (s is null) return false;
        s.EndedAt = DateTime.UtcNow;
        s.ClinicianNotes = clinicianNotes ?? s.ClinicianNotes;
        s.Status = TeleSessionStatus.Completed;

        // Sign off the encounter the clinician created on join. If there isn't one (e.g. patient ended
        // the call before the clinician arrived), create a stub so the bill has a clinical anchor.
        if (s.Encounter is null && !string.IsNullOrEmpty(s.ClinicianId))
        {
            var clinic = await _db.Clinics.AsNoTracking()
                .Where(c => c.FacilityId == s.FacilityId)
                .OrderBy(c => c.Code == "OPD" ? 0 : 1)
                .FirstOrDefaultAsync(ct);
            if (clinic is not null)
            {
                s.Encounter = new Encounter
                {
                    FacilityId = s.FacilityId,
                    PatientId = s.PatientId,
                    ClinicId = clinic.Id,
                    ClinicianId = s.ClinicianId!,
                    Type = EncounterType.Telemedicine,
                    Status = EncounterStatus.Signed,
                    StartedAt = s.StartedAt ?? s.ScheduledStartUtc,
                    SignedAt = DateTime.UtcNow,
                    UpdatedAt = DateTime.UtcNow,
                    ChiefComplaint = s.ConsultationReason
                };
                _db.Encounters.Add(s.Encounter);
                await _db.SaveChangesAsync(ct);
                s.EncounterId = s.Encounter.Id;
            }
        }
        if (s.Encounter is not null)
        {
            s.Encounter.Status = EncounterStatus.Signed;
            s.Encounter.SignedAt = DateTime.UtcNow;
            s.Encounter.UpdatedAt = DateTime.UtcNow;
        }

        // Link the bill to the encounter so the consult fee appears under the encounter ledger.
        if (s.EncounterId is not null && s.BillId is not null)
        {
            var bill = await _db.Bills.FirstOrDefaultAsync(b => b.Id == s.BillId, ct);
            if (bill is not null && bill.EncounterId is null) bill.EncounterId = s.EncounterId;
        }

        await _db.SaveChangesAsync(ct);

        // Auto-create an HMO claim if the patient has an active insured payer. Failures are logged
        // but never block the end-call flow — claims can always be built manually later.
        if (s.EncounterId is not null) await TryAutoBuildClaimAsync(s.PatientId, s.EncounterId.Value, s.FacilityId, clinicianUserId, ct);

        await _notifier.NotifySessionCompletedAsync(s.Id, ct);
        return true;
    }

    private async Task TryAutoBuildClaimAsync(int patientId, int encounterId, int facilityId, string? userId, CancellationToken ct)
    {
        try
        {
            var insured = await _db.Set<PatientPayer>().AsNoTracking()
                .Include(p => p.Payer)
                .Where(p => p.PatientId == patientId && p.IsActive && p.IsPrimary
                    && p.Type != PayerType.OutOfPocket && p.PayerId != null)
                .FirstOrDefaultAsync(ct);
            if (insured?.PayerId is null) return;

            var alreadyClaimed = await _db.Claims.AsNoTracking()
                .AnyAsync(c => c.EncounterId == encounterId, ct);
            if (alreadyClaimed) return;

            var claimId = await _claims.BuildFromEncounterAsync(facilityId, encounterId, insured.PayerId.Value, insured.PayerPlanId, userId ?? "system", ct);
            _log.LogInformation("Auto-built claim {ClaimId} for tele-encounter {EncounterId} (payer {Payer})", claimId, encounterId, insured.Payer?.Name);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Auto-build claim failed for encounter {EncounterId} — will need manual creation", encounterId);
        }
    }

    /// <summary>
    /// Cancels a session and applies the refund policy:
    ///   • Patient-initiated more than 2 hours before scheduled start → 100% refund (bill cancelled).
    ///   • Patient-initiated within 2 hours of scheduled start → 50% retained as late-cancel fee.
    ///   • System / clinician-initiated → 100% refund.
    /// Returns the refund amount applied.
    /// </summary>
    public async Task<(bool ok, decimal refundAmount)> CancelAsync(int sessionId, string reason, bool patientInitiated = true, CancellationToken ct = default)
    {
        var s = await _db.TeleSessions.Include(x => x.Bill).FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (s is null || s.Status is TeleSessionStatus.Completed or TeleSessionStatus.Cancelled) return (false, 0m);
        s.Status = TeleSessionStatus.Cancelled;
        s.EndedAt = DateTime.UtcNow;

        decimal refund = 0m;
        if (s.Bill is not null && s.Bill.PaidAmount > 0)
        {
            var hoursUntilStart = (s.ScheduledStartUtc - DateTime.UtcNow).TotalHours;
            var fullRefund = !patientInitiated || hoursUntilStart >= 2.0;
            refund = fullRefund ? s.Bill.PaidAmount : Math.Round(s.Bill.PaidAmount * 0.5m, 2);
            await ApplyRefundAsync(s.Bill, refund, fullRefund, ct);
        }

        await _db.SaveChangesAsync(ct);
        await _notifier.NotifySessionCancelledAsync(s.Id, refund, reason, ct);
        return (true, refund);
    }

    public async Task<bool> CancelAsync(int sessionId, CancellationToken ct = default) =>
        (await CancelAsync(sessionId, "Cancelled by request", patientInitiated: false, ct)).ok;

    /// <summary>Marks a session no-show (patient or clinician) and applies the refund policy.</summary>
    public async Task<(bool ok, decimal refundAmount)> MarkNoShowAsync(int sessionId, bool patientNoShow, CancellationToken ct = default)
    {
        var s = await _db.TeleSessions.Include(x => x.Bill).FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (s is null || s.Status is TeleSessionStatus.Completed or TeleSessionStatus.Cancelled
            or TeleSessionStatus.NoShowPatient or TeleSessionStatus.NoShowClinician) return (false, 0m);

        s.Status = patientNoShow ? TeleSessionStatus.NoShowPatient : TeleSessionStatus.NoShowClinician;
        s.EndedAt = DateTime.UtcNow;

        // Patient no-show → forfeit consult fee (no refund). Clinician no-show → full refund.
        decimal refund = 0m;
        if (!patientNoShow && s.Bill is not null && s.Bill.PaidAmount > 0)
        {
            refund = s.Bill.PaidAmount;
            await ApplyRefundAsync(s.Bill, refund, fullRefund: true, ct);
        }

        await _db.SaveChangesAsync(ct);
        await _notifier.NotifyNoShowAsync(s.Id, patientNoShow, ct);
        return (true, refund);
    }

    private async Task ApplyRefundAsync(ThriveHealth.Web.Models.Billing.Bill bill, decimal refundAmount, bool fullRefund, CancellationToken ct)
    {
        // Bookkeeping: we don't reverse the original cash payment (HMO claims, audit, gateway-locked).
        // Instead we tag the bill with a discount equal to the refund and adjust net so balance shows
        // the patient is no longer owed money. Cashier/finance will issue the actual disbursement.
        bill.DiscountAmount = Math.Min(refundAmount, bill.GrossAmount);
        bill.DiscountReason = fullRefund ? "Tele-consult full refund — cancellation/no-show" : "Tele-consult partial refund — late cancellation";
        bill.NetAmount = bill.GrossAmount - bill.DiscountAmount;
        if (bill.PaidAmount >= bill.NetAmount && bill.NetAmount > 0)
        {
            bill.Status = ThriveHealth.Web.Models.Billing.BillStatus.Paid;
        }
        else if (bill.NetAmount == 0)
        {
            bill.Status = ThriveHealth.Web.Models.Billing.BillStatus.Cancelled;
            bill.ClosedAt = DateTime.UtcNow;
        }
        await _db.SaveChangesAsync(ct);
    }

    public async Task<bool> IsBillSettledAsync(int sessionId, CancellationToken ct = default)
    {
        var s = await _db.TeleSessions.AsNoTracking()
            .Where(x => x.Id == sessionId)
            .Select(x => new { x.BillId })
            .FirstOrDefaultAsync(ct);
        if (s is null) return false;
        if (s.BillId is null) return true; // no bill (legacy / clinician-created)
        var bill = await _db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == s.BillId, ct);
        return bill is not null && bill.Status == BillStatus.Paid;
    }
}
