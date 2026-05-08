using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Billing;
using ThriveHealth.Web.Models.Integrations;

namespace ThriveHealth.Web.Services.Integrations;

public record PaymentInitiateRequest(int FacilityId, int BillId, int PatientId, decimal Amount,
    string? CustomerEmail, string? CustomerPhone, string? InitiatedById);

public record PaymentInitiateResult(long TransactionId, string Reference, string AuthorizationUrl);

public interface IPaymentGateway
{
    string ProviderName { get; }
    Task<PaymentInitiateResult> InitiateAsync(PaymentInitiateRequest req, CancellationToken ct = default);
    Task<bool> MarkSuccessfulAsync(long transactionId, string? providerReference, string? providerResponse, CancellationToken ct = default);
    Task<bool> MarkFailedAsync(long transactionId, string reason, CancellationToken ct = default);
}

public class LoggingPaymentGateway : IPaymentGateway
{
    public string ProviderName => "Logging (no-op)";
    private readonly ApplicationDbContext _db;
    private readonly IBillingService _billing;
    private readonly ILogger<LoggingPaymentGateway> _log;

    public LoggingPaymentGateway(ApplicationDbContext db, IBillingService billing, ILogger<LoggingPaymentGateway> log)
    {
        _db = db; _billing = billing; _log = log;
    }

    public async Task<PaymentInitiateResult> InitiateAsync(PaymentInitiateRequest req, CancellationToken ct = default)
    {
        var reference = $"TH-PAY-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
        // Synthetic ids (portal-N, system) aren't valid AspNetUsers FK targets; store NULL for those.
        var initiatedBy = req.InitiatedById;
        if (string.IsNullOrEmpty(initiatedBy) || initiatedBy.StartsWith("portal-") || initiatedBy == "system") initiatedBy = null;
        var tx = new PaymentTransaction
        {
            FacilityId = req.FacilityId,
            Reference = reference,
            Provider = ProviderName,
            BillId = req.BillId,
            PatientId = req.PatientId,
            Amount = req.Amount,
            Currency = "NGN",
            CustomerEmail = req.CustomerEmail,
            CustomerPhone = req.CustomerPhone,
            Status = PaymentTransactionStatus.Initiated,
            InitiatedById = initiatedBy
        };
        _db.PaymentTransactions.Add(tx);
        await _db.SaveChangesAsync(ct);

        // A real Paystack/Flutterwave integration would call /transaction/initialize here and return the redirect URL.
        // For now we return an in-app callback URL the admin uses to mark the transaction as paid.
        var url = $"/Admin/Integrations/PaymentCallback?txId={tx.Id}&status=success";
        _log.LogInformation("[Payment:noop] Initiated tx {Ref} for bill {BillId} amount {Amt}", reference, req.BillId, req.Amount);
        return new PaymentInitiateResult(tx.Id, reference, url);
    }

    public async Task<bool> MarkSuccessfulAsync(long transactionId, string? providerReference, string? providerResponse, CancellationToken ct = default)
    {
        var tx = await _db.PaymentTransactions.Include(t => t.Bill).FirstOrDefaultAsync(t => t.Id == transactionId, ct);
        if (tx is null) return false;
        if (tx.Status == PaymentTransactionStatus.Successful) return true;

        tx.Status = PaymentTransactionStatus.Successful;
        tx.ProviderReference = providerReference ?? tx.Reference;
        tx.ProviderResponse = providerResponse ?? "Marked successful";
        tx.CompletedAt = DateTime.UtcNow;

        // Materialise into a real Payment via the billing service
        var inputs = new List<PaymentInput>
        {
            new(PaymentMethod.MobileMoney, tx.Amount, tx.ProviderReference, $"Online via {tx.Provider}")
        };
        await _billing.RecordPaymentsAsync(tx.BillId!.Value, inputs, null, tx.InitiatedById ?? "");

        // Link the most recent payment for this bill
        var latest = await _db.Payments
            .Where(p => p.BillId == tx.BillId && p.Reference == tx.ProviderReference)
            .OrderByDescending(p => p.Id).FirstOrDefaultAsync(ct);
        if (latest != null) tx.PaymentId = latest.Id;

        await _db.SaveChangesAsync(ct);
        _log.LogInformation("[Payment:noop] Confirmed tx {TxId} ref {Ref}", tx.Id, tx.ProviderReference);
        return true;
    }

    public async Task<bool> MarkFailedAsync(long transactionId, string reason, CancellationToken ct = default)
    {
        var tx = await _db.PaymentTransactions.FirstOrDefaultAsync(t => t.Id == transactionId, ct);
        if (tx is null) return false;
        tx.Status = PaymentTransactionStatus.Failed;
        tx.ProviderResponse = reason;
        tx.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
