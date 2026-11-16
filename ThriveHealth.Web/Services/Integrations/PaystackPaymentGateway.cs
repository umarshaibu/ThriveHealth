using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Billing;
using ThriveHealth.Web.Models.Integrations;

namespace ThriveHealth.Web.Services.Integrations;

public class PaystackOptions
{
    public const string SectionName = "Payments:Paystack";
    public string SecretKey { get; set; } = string.Empty;
    public string PublicKey { get; set; } = string.Empty;
    public string BaseUrl { get; set; } = "https://api.paystack.co";
    /// <summary>Where Paystack redirects the customer after checkout (a deep link back into the app).</summary>
    public string? CallbackUrl { get; set; }
}

/// <summary>Server-side verification of a completed checkout. Implemented by gateways that have a
/// hosted-checkout return step (Paystack); the no-op stub does not implement it.</summary>
public interface IPaymentVerifier
{
    /// <summary>Verifies a transaction with the provider and, if paid, posts it to the bill. Returns true if settled.</summary>
    Task<bool> VerifyAndSettleAsync(string reference, CancellationToken ct = default);
}

/// <summary>
/// Paystack integration. <c>InitiateAsync</c> calls /transaction/initialize and returns the hosted
/// checkout URL; <c>VerifyAndSettleAsync</c> calls /transaction/verify and posts the payment to the
/// bill on success. Selected when <c>Payments:Provider=paystack</c> and a secret key is configured.
/// </summary>
public class PaystackPaymentGateway : IPaymentGateway, IPaymentVerifier
{
    public string ProviderName => "Paystack";

    private readonly ApplicationDbContext _db;
    private readonly IBillingService _billing;
    private readonly IHttpClientFactory _httpFactory;
    private readonly PaystackOptions _opts;
    private readonly ILogger<PaystackPaymentGateway> _log;

    public PaystackPaymentGateway(ApplicationDbContext db, IBillingService billing, IHttpClientFactory httpFactory,
        IOptions<PaystackOptions> opts, ILogger<PaystackPaymentGateway> log)
    {
        _db = db; _billing = billing; _httpFactory = httpFactory; _opts = opts.Value; _log = log;
    }

    private HttpClient Client()
    {
        var c = _httpFactory.CreateClient("paystack");
        c.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", _opts.SecretKey);
        return c;
    }

    public async Task<PaymentInitiateResult> InitiateAsync(PaymentInitiateRequest req, CancellationToken ct = default)
    {
        var reference = $"TH-PSK-{DateTime.UtcNow:yyyyMMddHHmmss}-{Random.Shared.Next(1000, 9999)}";
        var initiatedBy = req.InitiatedById;
        if (string.IsNullOrEmpty(initiatedBy) || initiatedBy.StartsWith("portal-") || initiatedBy == "system") initiatedBy = null;

        var tx = new PaymentTransaction
        {
            FacilityId = req.FacilityId, Reference = reference, Provider = ProviderName,
            BillId = req.BillId, PatientId = req.PatientId, Amount = req.Amount, Currency = "NGN",
            CustomerEmail = req.CustomerEmail, CustomerPhone = req.CustomerPhone,
            Status = PaymentTransactionStatus.Initiated, InitiatedById = initiatedBy
        };
        _db.PaymentTransactions.Add(tx);
        await _db.SaveChangesAsync(ct);

        var payload = new
        {
            email = string.IsNullOrWhiteSpace(req.CustomerEmail) ? "patient@thrivehealth.ng" : req.CustomerEmail,
            amount = (long)Math.Round(req.Amount * 100m),  // Paystack expects the minor unit (kobo)
            reference,
            callback_url = req.CallbackUrl ?? _opts.CallbackUrl
        };

        try
        {
            using var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");
            var res = await Client().PostAsync($"{_opts.BaseUrl}/transaction/initialize", content, ct);
            var bodyText = await res.Content.ReadAsStringAsync(ct);
            if (res.IsSuccessStatusCode)
            {
                using var doc = JsonDocument.Parse(bodyText);
                var data = doc.RootElement.GetProperty("data");
                var url = data.GetProperty("authorization_url").GetString() ?? "";
                tx.Status = PaymentTransactionStatus.Pending;
                await _db.SaveChangesAsync(ct);
                return new PaymentInitiateResult(tx.Id, reference, url);
            }
            _log.LogWarning("[Paystack] initialize failed ({Status}): {Body}", (int)res.StatusCode, bodyText);
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[Paystack] initialize threw");
        }
        // On failure, return with an empty URL — the controller surfaces a clean error.
        return new PaymentInitiateResult(tx.Id, reference, "");
    }

    public async Task<bool> VerifyAndSettleAsync(string reference, CancellationToken ct = default)
    {
        try
        {
            var res = await Client().GetAsync($"{_opts.BaseUrl}/transaction/verify/{Uri.EscapeDataString(reference)}", ct);
            var bodyText = await res.Content.ReadAsStringAsync(ct);
            if (!res.IsSuccessStatusCode) { _log.LogWarning("[Paystack] verify failed: {Body}", bodyText); return false; }

            using var doc = JsonDocument.Parse(bodyText);
            var data = doc.RootElement.GetProperty("data");
            var status = data.GetProperty("status").GetString();
            var txId = await TxIdForReferenceAsync(reference, ct);

            if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
            {
                // Never settle for less than the bill: the charged amount must match what we initiated.
                var expectedKobo = await _db.PaymentTransactions.AsNoTracking().Where(t => t.Id == txId)
                    .Select(t => (long)Math.Round(t.Amount * 100m)).FirstOrDefaultAsync(ct);
                var paidKobo = data.GetProperty("amount").GetInt64();
                if (paidKobo != expectedKobo)
                {
                    _log.LogError("[Paystack] amount mismatch for {Ref}: paid {Paid}, expected {Expected}", reference, paidKobo, expectedKobo);
                    await MarkFailedAsync(txId, $"Amount mismatch: paid {paidKobo} kobo, expected {expectedKobo}", ct);
                    return false;
                }
                return await MarkSuccessfulAsync(txId, reference, "Paystack verified", ct);
            }

            // "ongoing", "pending", "processing" and "queued" can still succeed, for example bank transfers.
            if (status is "failed" or "abandoned" or "reversed")
                await MarkFailedAsync(txId, $"Paystack status: {status}", ct);
            return false;
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[Paystack] verify threw for {Ref}", reference);
            return false;
        }
    }

    private async Task<long> TxIdForReferenceAsync(string reference, CancellationToken ct) =>
        await _db.PaymentTransactions.Where(t => t.Reference == reference).Select(t => t.Id).FirstOrDefaultAsync(ct);

    // Shared with LoggingPaymentGateway by behaviour: post a Payment to the bill on success.
    public async Task<bool> MarkSuccessfulAsync(long transactionId, string? providerReference, string? providerResponse, CancellationToken ct = default)
    {
        // The app's confirm call and Paystack's webhook can arrive together. Claiming the row
        // atomically means exactly one of them posts the payment to the bill.
        var previous = await _db.PaymentTransactions.AsNoTracking()
            .Where(t => t.Id == transactionId).Select(t => (PaymentTransactionStatus?)t.Status).FirstOrDefaultAsync(ct);
        if (previous is null) return false;
        var claimed = await _db.PaymentTransactions
            .Where(t => t.Id == transactionId && t.Status != PaymentTransactionStatus.Successful)
            .ExecuteUpdateAsync(u => u.SetProperty(t => t.Status, PaymentTransactionStatus.Successful), ct);
        if (claimed == 0) return true;

        var tx = await _db.PaymentTransactions.Include(t => t.Bill).FirstAsync(t => t.Id == transactionId, ct);
        tx.ProviderReference = providerReference ?? tx.Reference;
        tx.ProviderResponse = providerResponse ?? "Marked successful";
        tx.CompletedAt = DateTime.UtcNow;

        var inputs = new List<PaymentInput>
        {
            new(PaymentMethod.MobileMoney, tx.Amount, tx.ProviderReference, $"Online via {tx.Provider}")
        };
        try
        {
            await _billing.RecordPaymentsAsync(tx.BillId!.Value, inputs, null, tx.InitiatedById ?? "");
        }
        catch
        {
            // Release the claim so the payment can be retried rather than lost.
            _db.ChangeTracker.Clear();
            await _db.PaymentTransactions.Where(t => t.Id == transactionId)
                .ExecuteUpdateAsync(u => u.SetProperty(t => t.Status, previous.Value), CancellationToken.None);
            throw;
        }

        var latest = await _db.Payments
            .Where(p => p.BillId == tx.BillId && p.Reference == tx.ProviderReference)
            .OrderByDescending(p => p.Id).FirstOrDefaultAsync(ct);
        if (latest != null) tx.PaymentId = latest.Id;

        await _db.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> MarkFailedAsync(long transactionId, string reason, CancellationToken ct = default)
    {
        var tx = await _db.PaymentTransactions.FirstOrDefaultAsync(t => t.Id == transactionId, ct);
        if (tx is null) return false;
        if (tx.Status == PaymentTransactionStatus.Successful) return false; // A settled payment is never undone here.
        tx.Status = PaymentTransactionStatus.Failed;
        tx.ProviderResponse = reason;
        tx.CompletedAt = DateTime.UtcNow;
        await _db.SaveChangesAsync(ct);
        return true;
    }
}
