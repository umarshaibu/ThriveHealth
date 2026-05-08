using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Audit;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Integrations;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;
using ThriveHealth.Web.Services.Integrations;

namespace ThriveHealth.Web.Controllers;

[Authorize]
public class AdminController : Controller
{
    public const string AdminAccess = Roles.SystemAdministrator + "," + Roles.MedicalDirector + "," +
        Roles.ChiefExecutive + "," + Roles.ChiefFinancialOfficer;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ISmsGateway _sms;
    private readonly IEmailGateway _email;
    private readonly IPaymentGateway _payment;
    private readonly IAuditService _audit;

    public AdminController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        ISmsGateway sms, IEmailGateway email, IPaymentGateway payment, IAuditService audit)
    {
        _db = db; _userManager = userManager; _sms = sms; _email = email; _payment = payment; _audit = audit;
    }

    private async Task<int?> FacilityIdAsync()
    {
        var u = await _userManager.GetUserAsync(User);
        return u?.FacilityId;
    }

    [HttpGet, HasPermission(Permissions.AuditView)]
    public async Task<IActionResult> Audit(AuditCategory? category, AuditOutcome? outcome, string? action, DateOnly? since)
    {
        var fid = await FacilityIdAsync();
        var query = _db.AuditEntries.AsNoTracking()
            .Include(e => e.ActorUser)
            .Where(e => e.FacilityId == null || e.FacilityId == fid);

        if (category.HasValue) query = query.Where(e => e.Category == category.Value);
        if (outcome.HasValue) query = query.Where(e => e.Outcome == outcome.Value);
        if (!string.IsNullOrWhiteSpace(action)) query = query.Where(e => e.Action.Contains(action));
        if (since.HasValue) query = query.Where(e => e.AtUtc >= since.Value.ToDateTime(TimeOnly.MinValue));

        var entries = await query.OrderByDescending(e => e.AtUtc).Take(300).ToListAsync();

        var dayAgo = DateTime.UtcNow.AddDays(-1);
        var totals = await _db.AuditEntries.AsNoTracking()
            .Where(e => (e.FacilityId == null || e.FacilityId == fid) && e.AtUtc >= dayAgo)
            .GroupBy(e => e.Outcome)
            .Select(g => new { g.Key, Count = g.Count() }).ToListAsync();

        return View(new AuditFilterViewModel
        {
            Category = category,
            Outcome = outcome,
            Action = action,
            Since = since,
            Entries = entries,
            TotalLast24h = totals.Sum(x => x.Count),
            FailuresLast24h = totals.Where(x => x.Key == AuditOutcome.Failure).Sum(x => x.Count)
        });
    }

    [HttpGet, HasPermission(Permissions.IntegrationsManage)]
    public async Task<IActionResult> Integrations()
    {
        var fid = await FacilityIdAsync() ?? 0;
        var dayAgo = DateTime.UtcNow.AddDays(-1);

        var smsAll = await _db.SmsMessages.AsNoTracking()
            .Where(m => m.FacilityId == fid)
            .OrderByDescending(m => m.CreatedAt).Take(50).ToListAsync();
        var smsQueued = await _db.SmsMessages.CountAsync(m => m.FacilityId == fid && m.Status == MessageStatus.Queued);
        var smsSent = await _db.SmsMessages.CountAsync(m => m.FacilityId == fid && m.Status == MessageStatus.Sent && m.SentAt >= dayAgo);
        var smsFailed = await _db.SmsMessages.CountAsync(m => m.FacilityId == fid && m.Status == MessageStatus.Failed && m.FailedAt >= dayAgo);

        var emailAll = await _db.EmailMessages.AsNoTracking()
            .Where(m => m.FacilityId == fid)
            .OrderByDescending(m => m.CreatedAt).Take(50).ToListAsync();
        var emQueued = await _db.EmailMessages.CountAsync(m => m.FacilityId == fid && m.Status == MessageStatus.Queued);
        var emSent = await _db.EmailMessages.CountAsync(m => m.FacilityId == fid && m.Status == MessageStatus.Sent && m.SentAt >= dayAgo);

        var payAll = await _db.PaymentTransactions.AsNoTracking()
            .Include(t => t.Bill)
            .Where(t => t.FacilityId == fid)
            .OrderByDescending(t => t.CreatedAt).Take(50).ToListAsync();
        var payInit = await _db.PaymentTransactions.CountAsync(t => t.FacilityId == fid &&
            (t.Status == PaymentTransactionStatus.Initiated || t.Status == PaymentTransactionStatus.Pending));
        var paySucc = await _db.PaymentTransactions.CountAsync(t => t.FacilityId == fid &&
            t.Status == PaymentTransactionStatus.Successful && t.CompletedAt >= dayAgo);
        var paySuccAmt = await _db.PaymentTransactions.AsNoTracking()
            .Where(t => t.FacilityId == fid && t.Status == PaymentTransactionStatus.Successful && t.CompletedAt >= dayAgo)
            .SumAsync(t => (decimal?)t.Amount) ?? 0m;

        return View(new IntegrationsDashboardViewModel
        {
            SmsProvider = _sms.ProviderName,
            EmailProvider = _email.ProviderName,
            PaymentProvider = _payment.ProviderName,
            SmsQueued = smsQueued,
            SmsSent24h = smsSent,
            SmsFailed24h = smsFailed,
            EmailQueued = emQueued,
            EmailSent24h = emSent,
            PaymentInitiated = payInit,
            PaymentSuccessful24h = paySucc,
            PaymentSuccessfulAmount24h = paySuccAmt,
            RecentSms = smsAll,
            RecentEmails = emailAll,
            RecentPayments = payAll
        });
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.IntegrationsManage)]
    public async Task<IActionResult> ProcessSmsQueue()
    {
        var sent = await _sms.ProcessQueueAsync();
        TempData["Success"] = $"Processed SMS queue · {sent} message(s) marked as sent.";
        return RedirectToAction(nameof(Integrations));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.IntegrationsManage)]
    public async Task<IActionResult> ProcessEmailQueue()
    {
        var sent = await _email.ProcessQueueAsync();
        TempData["Success"] = $"Processed email queue · {sent} message(s) marked as sent.";
        return RedirectToAction(nameof(Integrations));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.IntegrationsManage)]
    public async Task<IActionResult> SendTestSms(SmsTestSendViewModel m)
    {
        var fid = await FacilityIdAsync() ?? 0;
        var u = await _userManager.GetUserAsync(User);
        await _sms.EnqueueAsync(new SmsSendRequest(fid, m.ToPhone, m.Body, MessagePurpose.AdHoc, null, u?.Id));
        TempData["Success"] = "Test SMS queued.";
        return RedirectToAction(nameof(Integrations));
    }

    [HttpPost, ValidateAntiForgeryToken, HasPermission(Permissions.IntegrationsManage)]
    public async Task<IActionResult> SendTestEmail(EmailTestSendViewModel m)
    {
        var fid = await FacilityIdAsync() ?? 0;
        var u = await _userManager.GetUserAsync(User);
        await _email.EnqueueAsync(new EmailSendRequest(fid, m.ToEmail, null, m.Subject, m.BodyHtml, MessagePurpose.AdHoc, null, u?.Id));
        TempData["Success"] = "Test email queued.";
        return RedirectToAction(nameof(Integrations));
    }

    [HttpGet, HasPermission(Permissions.IntegrationsManage)]
    public async Task<IActionResult> PaymentCallback(long txId, string status)
    {
        // Stand-in for Paystack/Flutterwave webhook callback.
        if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
        {
            await _payment.MarkSuccessfulAsync(txId, providerReference: null, providerResponse: "Marked via callback");
            await _audit.LogAsync("payment.callback.success", AuditCategory.BusinessAction, AuditOutcome.Success,
                entityType: "PaymentTransaction", entityKey: txId.ToString());
            TempData["Success"] = "Payment marked successful and applied to bill.";
        }
        else
        {
            await _payment.MarkFailedAsync(txId, "Cancelled or failed at provider.");
            TempData["Error"] = "Payment marked failed.";
        }
        return RedirectToAction(nameof(Integrations));
    }

    [HttpGet, HasPermission(Permissions.HardeningView)]
    public async Task<IActionResult> Hardening()
    {
        bool dbReachable = false;
        string? dbVersion = null;
        int migrationsApplied = 0;
        try
        {
            dbReachable = await _db.Database.CanConnectAsync();
            if (dbReachable)
            {
                var conn = _db.Database.GetDbConnection();
                if (conn.State != System.Data.ConnectionState.Open) await conn.OpenAsync();
                using var cmd = conn.CreateCommand();
                cmd.CommandText = "SELECT version()";
                dbVersion = (await cmd.ExecuteScalarAsync())?.ToString();
                var migs = await _db.Database.GetAppliedMigrationsAsync();
                migrationsApplied = migs.Count();
            }
        }
        catch { dbReachable = false; }

        var headers = new Dictionary<string, string>
        {
            ["Content-Security-Policy"] = "Configured (default-src self, scripts/styles allow jsdelivr CDN, frame-ancestors self)",
            ["Strict-Transport-Security"] = "Enabled in production via UseHsts()",
            ["X-Content-Type-Options"] = "nosniff",
            ["X-Frame-Options"] = "SAMEORIGIN",
            ["Referrer-Policy"] = "strict-origin-when-cross-origin",
            ["Permissions-Policy"] = "camera=self, microphone=self, geolocation=()"
        };

        return View(new HealthCheckViewModel
        {
            DbReachable = dbReachable,
            DbVersion = dbVersion,
            MigrationsApplied = migrationsApplied,
            ServerTimeUtc = DateTime.UtcNow,
            SecurityHeaders = headers,
            SmsProvider = _sms.ProviderName,
            EmailProvider = _email.ProviderName,
            PaymentProvider = _payment.ProviderName
        });
    }
}
