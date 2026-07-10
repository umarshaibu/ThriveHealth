using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Services;
using ThriveHealth.Web.Services.Integrations;

namespace ThriveHealth.Web.Controllers.Api;

/// <summary>
/// Server-to-server payment webhooks. Paystack POSTs here on charge events; we verify the HMAC-SHA512
/// signature, then verify+settle the transaction and run the same post-settlement side effects as the
/// in-app confirm (schedule tele-consult, activate chat package). Always returns 200 so the provider
/// doesn't retry indefinitely once received.
/// </summary>
[ApiController]
[Route("api/v1/payments/webhook")]
[AllowAnonymous]
public class PaymentsWebhookController : ControllerBase
{
    private readonly ApplicationDbContext _db;
    private readonly IPaymentGateway _payment;
    private readonly ITeleNotifier _teleNotifier;
    private readonly ITeleChatService _chat;
    private readonly PaystackOptions _paystack;
    private readonly ILogger<PaymentsWebhookController> _log;

    public PaymentsWebhookController(ApplicationDbContext db, IPaymentGateway payment, ITeleNotifier teleNotifier,
        ITeleChatService chat, IOptions<PaystackOptions> paystack, ILogger<PaymentsWebhookController> log)
    {
        _db = db; _payment = payment; _teleNotifier = teleNotifier; _chat = chat; _paystack = paystack.Value; _log = log;
    }

    [HttpPost("paystack")]
    public async Task<IActionResult> Paystack(CancellationToken ct)
    {
        if (_payment is not IPaymentVerifier verifier || string.IsNullOrWhiteSpace(_paystack.SecretKey))
            return Ok(); // Paystack not the active gateway — ignore.

        using var reader = new StreamReader(Request.Body);
        var body = await reader.ReadToEndAsync(ct);

        // Validate the signature: HMAC-SHA512(body) keyed with the secret, hex, compared to x-paystack-signature.
        var signature = Request.Headers["x-paystack-signature"].ToString();
        var expected = Convert.ToHexString(
            new HMACSHA512(Encoding.UTF8.GetBytes(_paystack.SecretKey)).ComputeHash(Encoding.UTF8.GetBytes(body)))
            .ToLowerInvariant();
        if (string.IsNullOrEmpty(signature) || !CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(signature.ToLowerInvariant()), Encoding.UTF8.GetBytes(expected)))
        {
            _log.LogWarning("[Paystack webhook] invalid signature");
            return Unauthorized();
        }

        try
        {
            using var doc = JsonDocument.Parse(body);
            var evt = doc.RootElement.GetProperty("event").GetString();
            if (evt == "charge.success")
            {
                var reference = doc.RootElement.GetProperty("data").GetProperty("reference").GetString();
                if (!string.IsNullOrEmpty(reference) && await verifier.VerifyAndSettleAsync(reference, ct))
                    await RunPostSettlementAsync(reference, ct);
            }
        }
        catch (Exception ex)
        {
            _log.LogError(ex, "[Paystack webhook] processing failed");
        }
        return Ok();
    }

    private async Task RunPostSettlementAsync(string reference, CancellationToken ct)
    {
        var billId = await _db.PaymentTransactions.AsNoTracking()
            .Where(t => t.Reference == reference).Select(t => t.BillId).FirstOrDefaultAsync(ct);
        if (billId is not int bid) return;

        var teleId = await _db.TeleSessions.AsNoTracking().Where(s => s.BillId == bid).Select(s => (int?)s.Id).FirstOrDefaultAsync(ct);
        if (teleId is int sid) await _teleNotifier.NotifySessionScheduledAsync(sid, ct);
        await _chat.ActivatePackageForBillAsync(bid, ct);
    }
}
