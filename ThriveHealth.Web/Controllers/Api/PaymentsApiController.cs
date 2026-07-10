using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Api;
using ThriveHealth.Web.Models.Billing;
using ThriveHealth.Web.Services;
using ThriveHealth.Web.Services.Integrations;

namespace ThriveHealth.Web.Controllers.Api;

/// <summary>
/// Bill payment for the patient app. <c>pay</c> initiates a transaction (returns a hosted-checkout
/// URL when a real gateway like Paystack is configured); <c>confirm</c> verifies/settles it.
/// </summary>
[Route("api/v1")]
public class PaymentsApiController : ApiControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IPaymentGateway _payment;
    private readonly ITeleNotifier _teleNotifier;
    private readonly ITeleChatService _chat;

    public PaymentsApiController(ApplicationDbContext db, IPaymentGateway payment, ITeleNotifier teleNotifier, ITeleChatService chat)
    {
        _db = db; _payment = payment; _teleNotifier = teleNotifier; _chat = chat;
    }

    [HttpPost("bills/{billId:int}/pay")]
    [ProducesResponseType(typeof(ApiPayInitiateResult), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Pay(int billId, CancellationToken ct)
    {
        var pid = PatientId;
        var bill = await _db.Bills.FirstOrDefaultAsync(b => b.Id == billId && b.PatientId == pid, ct);
        if (bill is null) return NotFound(new ApiError("Bill not found.", "not_found"));
        if (bill.Status == BillStatus.Paid) return BadRequest(new ApiError("Bill is already settled.", "already_paid"));
        if (bill.Balance <= 0) return BadRequest(new ApiError("Nothing to pay on this bill.", "zero_balance"));

        var email = await _db.PortalAccounts.AsNoTracking().Where(a => a.PatientId == pid).Select(a => a.Email).FirstOrDefaultAsync(ct);
        var phone = await _db.Patients.AsNoTracking().Where(p => p.Id == pid).Select(p => p.Phone).FirstOrDefaultAsync(ct);

        var result = await _payment.InitiateAsync(new PaymentInitiateRequest(
            FacilityId: bill.FacilityId, BillId: bill.Id, PatientId: pid, Amount: bill.Balance,
            CustomerEmail: email, CustomerPhone: phone, InitiatedById: $"portal-{pid}"), ct);

        var requiresExternal = result.AuthorizationUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase);
        return Ok(new ApiPayInitiateResult(result.TransactionId, result.Reference, result.AuthorizationUrl,
            _payment.ProviderName, requiresExternal));
    }

    /// <summary>
    /// Settles a transaction after checkout. With a real gateway (IPaymentVerifier) this re-verifies
    /// server-side with the provider; with the no-op stub it marks the transaction paid (dev flow).
    /// </summary>
    [HttpPost("payments/{transactionId:long}/confirm")]
    [ProducesResponseType(typeof(ApiPaymentStatus), StatusCodes.Status200OK)]
    [ProducesResponseType(typeof(ApiError), StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Confirm(long transactionId, CancellationToken ct)
    {
        var pid = PatientId;
        var tx = await _db.PaymentTransactions.FirstOrDefaultAsync(t => t.Id == transactionId && t.PatientId == pid, ct);
        if (tx is null) return NotFound(new ApiError("Transaction not found.", "not_found"));

        bool settled;
        if (_payment is IPaymentVerifier verifier)
            settled = await verifier.VerifyAndSettleAsync(tx.Reference, ct);
        else
            settled = await _payment.MarkSuccessfulAsync(tx.Id, providerReference: null, providerResponse: "Confirmed (mobile)", ct);

        // Post-settlement side effects keyed off the bill: schedule a tele-consult (fires its
        // notification), and/or activate a 24-hour chat package. Both are idempotent no-ops otherwise.
        if (settled && tx.BillId is int billId)
        {
            var teleId = await _db.TeleSessions.AsNoTracking().Where(s => s.BillId == billId).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
            if (teleId is int sid) await _teleNotifier.NotifySessionScheduledAsync(sid, ct);
            await _chat.ActivatePackageForBillAsync(billId, ct);
        }

        return await StatusFor(tx.Id, ct);
    }

    [HttpGet("payments/{transactionId:long}")]
    [ProducesResponseType(typeof(ApiPaymentStatus), StatusCodes.Status200OK)]
    public async Task<IActionResult> Status(long transactionId, CancellationToken ct) => await StatusFor(transactionId, ct);

    private async Task<IActionResult> StatusFor(long transactionId, CancellationToken ct)
    {
        var pid = PatientId;
        var tx = await _db.PaymentTransactions.AsNoTracking().Include(t => t.Bill)
            .FirstOrDefaultAsync(t => t.Id == transactionId && t.PatientId == pid, ct);
        if (tx is null) return NotFound(new ApiError("Transaction not found.", "not_found"));
        return Ok(new ApiPaymentStatus(tx.Id, tx.Status.ToString(), tx.Amount, tx.BillId ?? 0,
            tx.Bill?.Status.ToString() ?? "Unknown"));
    }
}
