using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Integrations;
using ThriveHealth.Web.Models.Telemedicine;
using ThriveHealth.Web.Services.Integrations;

namespace ThriveHealth.Web.Services;

/// <summary>
/// Sends email + SMS notifications at the lifecycle moments of a tele-session — billed, scheduled,
/// clinician-joined, completed, refunded, no-show. Wraps the existing gateways so the call sites
/// don't have to know about templates or contact lookup.
/// </summary>
public interface ITeleNotifier
{
    Task NotifyBillRaisedAsync(int sessionId, CancellationToken ct = default);
    Task NotifySessionScheduledAsync(int sessionId, CancellationToken ct = default);
    Task NotifyClinicianReadyAsync(int sessionId, CancellationToken ct = default);
    Task NotifySessionCompletedAsync(int sessionId, CancellationToken ct = default);
    Task NotifySessionCancelledAsync(int sessionId, decimal refundAmount, string reason, CancellationToken ct = default);
    Task NotifyNoShowAsync(int sessionId, bool patientNoShow, CancellationToken ct = default);
}

public class TeleNotifier : ITeleNotifier
{
    private readonly ApplicationDbContext _db;
    private readonly IEmailGateway _email;
    private readonly ISmsGateway _sms;
    private readonly ILogger<TeleNotifier> _log;

    public TeleNotifier(ApplicationDbContext db, IEmailGateway email, ISmsGateway sms, ILogger<TeleNotifier> log)
    {
        _db = db; _email = email; _sms = sms; _log = log;
    }

    private async Task<(TeleSession? s, string? email, string? phone, string? patientName, string? clinicianName)> LoadAsync(int sessionId, CancellationToken ct)
    {
        var s = await _db.TeleSessions.AsNoTracking()
            .Include(x => x.Patient).Include(x => x.Clinician).Include(x => x.Bill)
            .FirstOrDefaultAsync(x => x.Id == sessionId, ct);
        if (s is null) return (null, null, null, null, null);
        var portalEmail = await _db.PortalAccounts.AsNoTracking()
            .Where(a => a.PatientId == s.PatientId && a.IsActive).Select(a => a.Email).FirstOrDefaultAsync(ct);
        return (s, portalEmail, s.Patient?.Phone, s.Patient?.FullName, s.Clinician?.FullName);
    }

    public async Task NotifyBillRaisedAsync(int sessionId, CancellationToken ct = default)
    {
        var (s, email, phone, name, _) = await LoadAsync(sessionId, ct);
        if (s is null) return;

        var amount = s.Bill?.NetAmount ?? 0m;
        var subject = $"Settle your tele-consult bill — {s.SessionNumber}";
        var body = $"<p>Dear {name},</p><p>Your tele-consultation request <strong>{s.SessionNumber}</strong> has been received. " +
                   $"A bill for <strong>NGN {amount:N2}</strong> has been issued (#{s.Bill?.BillNumber}). " +
                   $"<a href=\"/Portal/Pay?teleSessionId={s.Id}\">Settle the bill</a> to schedule your call.</p>";
        await SafeSendAsync(s, email, phone, subject, body,
            $"ThriveHealth: bill {s.Bill?.BillNumber} for {amount:N0} NGN. Pay online to schedule your tele-consult.",
            MessagePurpose.BillReceipt, ct);
    }

    public async Task NotifySessionScheduledAsync(int sessionId, CancellationToken ct = default)
    {
        var (s, email, phone, name, _) = await LoadAsync(sessionId, ct);
        if (s is null) return;
        var when = s.ScheduledStartUtc.ToLocalTime().ToString("dd MMM yyyy 'at' HH:mm");
        var subject = $"Tele-consult scheduled — {s.SessionNumber}";
        var body = $"<p>Hi {name},</p><p>Payment received. Your tele-consultation <strong>{s.SessionNumber}</strong> is scheduled for <strong>{when}</strong>. " +
                   "We'll notify you again when a clinician picks up the call.</p>";
        await SafeSendAsync(s, email, phone, subject, body,
            $"ThriveHealth: payment confirmed. Your tele-consult {s.SessionNumber} is scheduled for {when}.",
            MessagePurpose.PaymentReceipt, ct);
    }

    public async Task NotifyClinicianReadyAsync(int sessionId, CancellationToken ct = default)
    {
        var (s, email, phone, name, doc) = await LoadAsync(sessionId, ct);
        if (s is null) return;
        var subject = $"Your doctor is ready — {s.SessionNumber}";
        var body = $"<p>Hello {name},</p><p>Dr {doc ?? "the clinician"} is in the tele-consult room and waiting for you. " +
                   $"<a href=\"/Portal/Telemed\">Join the call now</a>.</p>";
        await SafeSendAsync(s, email, phone, subject, body,
            $"ThriveHealth: Dr {doc ?? "the clinician"} is ready for your tele-consult. Open the portal to join.",
            MessagePurpose.TeleSessionReady, ct);
    }

    public async Task NotifySessionCompletedAsync(int sessionId, CancellationToken ct = default)
    {
        var (s, email, phone, name, doc) = await LoadAsync(sessionId, ct);
        if (s is null) return;
        var subject = $"Consultation completed — {s.SessionNumber}";
        var body = $"<p>Hi {name},</p><p>Your tele-consult with Dr {doc ?? "your clinician"} is now complete. " +
                   "You can review the notes, prescriptions and any follow-up bookings in your portal.</p>" +
                   "<p><a href=\"/Portal/Telemed\">Open my consult history</a></p>";
        await SafeSendAsync(s, email, phone, subject, body,
            $"ThriveHealth: consult {s.SessionNumber} complete. Visit your portal for notes & prescriptions.",
            MessagePurpose.AdHoc, ct);
    }

    public async Task NotifySessionCancelledAsync(int sessionId, decimal refundAmount, string reason, CancellationToken ct = default)
    {
        var (s, email, phone, name, _) = await LoadAsync(sessionId, ct);
        if (s is null) return;
        var subject = $"Tele-consult cancelled — {s.SessionNumber}";
        var refundLine = refundAmount > 0
            ? $"A refund of <strong>NGN {refundAmount:N2}</strong> will be processed to your account within 3–5 working days."
            : "No refund applies under our cancellation policy.";
        var body = $"<p>Hi {name},</p><p>Your tele-consultation <strong>{s.SessionNumber}</strong> has been cancelled ({reason}). {refundLine}</p>";
        await SafeSendAsync(s, email, phone, subject, body,
            $"ThriveHealth: tele-consult {s.SessionNumber} cancelled. " + (refundAmount > 0 ? $"Refund {refundAmount:N0} NGN in 3-5 days." : "No refund per policy."),
            MessagePurpose.AdHoc, ct);
    }

    public async Task NotifyNoShowAsync(int sessionId, bool patientNoShow, CancellationToken ct = default)
    {
        var (s, email, phone, name, doc) = await LoadAsync(sessionId, ct);
        if (s is null) return;
        if (patientNoShow)
        {
            await SafeSendAsync(s, email, phone,
                $"You missed your tele-consult — {s.SessionNumber}",
                $"<p>Hi {name},</p><p>Dr {doc ?? "the clinician"} waited but you didn't join the consultation. " +
                "Please <a href=\"/Portal/RequestTelemed\">request a new consult</a> if you'd like to reschedule.</p>",
                $"ThriveHealth: tele-consult {s.SessionNumber} marked no-show. Reschedule via the portal if needed.",
                MessagePurpose.AdHoc, ct);
        }
        else
        {
            await SafeSendAsync(s, email, phone,
                $"Your tele-consult could not start — {s.SessionNumber}",
                $"<p>Hi {name},</p><p>The clinician was unable to join your tele-consult. We're sorry for the inconvenience. " +
                "A full refund will be processed and you can <a href=\"/Portal/RequestTelemed\">book a new consult</a> at no extra charge.</p>",
                $"ThriveHealth: clinician unavailable for {s.SessionNumber}. Full refund and free re-booking.",
                MessagePurpose.AdHoc, ct);
        }
    }

    private async Task SafeSendAsync(TeleSession s, string? email, string? phone, string subject, string bodyHtml, string smsBody, MessagePurpose purpose, CancellationToken ct)
    {
        try
        {
            if (!string.IsNullOrEmpty(email))
                await _email.EnqueueAsync(new EmailSendRequest(s.FacilityId, email, s.Patient?.FullName, subject, bodyHtml, purpose, s.PatientId), ct);
            if (!string.IsNullOrEmpty(phone))
                await _sms.EnqueueAsync(new SmsSendRequest(s.FacilityId, phone, smsBody, purpose, s.PatientId), ct);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "Failed to enqueue tele notification for session {SessionId}", s.Id);
        }
    }
}
