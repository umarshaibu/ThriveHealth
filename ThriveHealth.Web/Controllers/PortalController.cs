using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.EntityFrameworkCore;
using System.Globalization;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Audit;
using ThriveHealth.Web.Models.Billing;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Integrations;
using ThriveHealth.Web.Models.Portal;
using ThriveHealth.Web.Models.Telemedicine;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;
using ThriveHealth.Web.Services.Ai;
using ThriveHealth.Web.Services.Integrations;

namespace ThriveHealth.Web.Controllers;

[Authorize(AuthenticationSchemes = PortalAuth.Scheme)]
public class PortalController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly IPortalAuthService _auth;
    private readonly ITelemedicineService _tele;
    private readonly IAuditService _audit;
    private readonly IEmailGateway _email;
    private readonly IPaymentGateway _payment;
    private readonly IClinicalAiService _ai;
    private readonly ILiveKitTokenService _liveKit;
    private readonly ITeleNotifier _notifier;
    private readonly ITeleChatService _chat;
    private readonly IBillingService _billing;

    public PortalController(ApplicationDbContext db, IPortalAuthService auth, ITelemedicineService tele,
        IAuditService audit, IEmailGateway email, IPaymentGateway payment, IClinicalAiService ai,
        ILiveKitTokenService liveKit, ITeleNotifier notifier, ITeleChatService chat, IBillingService billing)
    {
        _db = db; _auth = auth; _tele = tele; _audit = audit; _email = email; _payment = payment; _ai = ai;
        _liveKit = liveKit; _notifier = notifier; _chat = chat; _billing = billing;
    }

    private int? CurrentPatientId()
    {
        var raw = User.FindFirst(PortalAuth.ClaimPatientId)?.Value;
        return int.TryParse(raw, out var n) ? n : null;
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Login(string? returnUrl) => View(new PortalLoginViewModel { ReturnUrl = returnUrl });

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("portal-auth")]
    public async Task<IActionResult> Login(PortalLoginViewModel m)
    {
        if (!ModelState.IsValid) return View(m);
        var account = await _auth.FindByEmailAsync(m.Email);
        if (account is null || !await _auth.ValidateAsync(account, m.Password))
        {
            await _audit.LogAsync("portal.login", AuditCategory.Authentication, AuditOutcome.Failure,
                summary: $"Bad portal login for {m.Email}", actorOverride: m.Email);
            ModelState.AddModelError(string.Empty, "Email or password is incorrect.");
            return View(m);
        }
        await _auth.SignInAsync(HttpContext, account, m.RememberMe);
        await _audit.LogAsync("portal.login", AuditCategory.Authentication, AuditOutcome.Success,
            entityType: "PortalAccount", entityKey: account.Id.ToString(),
            summary: $"Patient {account.Patient?.FullName ?? account.Email} signed in",
            facilityId: account.Patient?.FacilityId, actorOverride: account.Email);
        if (!string.IsNullOrEmpty(m.ReturnUrl) && Url.IsLocalUrl(m.ReturnUrl)) return Redirect(m.ReturnUrl);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet, AllowAnonymous]
    public IActionResult Register() => View(new PortalRegisterViewModel());

    [HttpPost, AllowAnonymous, ValidateAntiForgeryToken, EnableRateLimiting("portal-auth")]
    public async Task<IActionResult> Register(PortalRegisterViewModel m)
    {
        if (!ModelState.IsValid) return View(m);

        if (!DateOnly.TryParseExact(m.DateOfBirth, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var dob))
        {
            ModelState.AddModelError(nameof(m.DateOfBirth), "Use yyyy-MM-dd format.");
            return View(m);
        }

        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p =>
            p.HospitalNumber == m.HospitalNumber.Trim()
            && p.LastName.ToLower() == m.LastName.Trim().ToLower()
            && p.DateOfBirth == dob
            && !p.IsMergedAlias);

        if (patient is null)
        {
            await _audit.LogAsync("portal.register", AuditCategory.SecurityEvent, AuditOutcome.Failure,
                summary: $"Patient verification failed for {m.HospitalNumber} / {m.LastName}", actorOverride: m.Email);
            ModelState.AddModelError(string.Empty, "Patient not found. Verify hospital number, surname and date of birth match your registration.");
            return View(m);
        }

        var account = await _auth.RegisterAsync(patient.Id, m.Email, m.Phone, m.Password);
        if (account is null)
        {
            ModelState.AddModelError(string.Empty, "Email already in use, or this patient already has a portal account.");
            return View(m);
        }

        await _auth.SignInAsync(HttpContext, account, true);
        await _audit.LogAsync("portal.register", AuditCategory.Authentication, AuditOutcome.Success,
            entityType: "PortalAccount", entityKey: account.Id.ToString(),
            summary: $"Portal account created for {patient.FullName}", facilityId: patient.FacilityId,
            actorOverride: account.Email);

        await _email.EnqueueAsync(new EmailSendRequest(
            patient.FacilityId, m.Email, patient.FullName,
            "Welcome to ThriveHealth Portal",
            $"<p>Hi {patient.FirstName},</p><p>Your patient portal account has been created. You can now book appointments, view results and request tele-consultations at any time.</p><p>If you didn't create this account, please contact the hospital immediately.</p>",
            MessagePurpose.PortalVerification, patient.Id));

        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout()
    {
        var pid = CurrentPatientId();
        await _auth.SignOutAsync(HttpContext);
        if (pid.HasValue)
        {
            await _audit.LogAsync("portal.logout", AuditCategory.Authentication, AuditOutcome.Success,
                entityType: "Patient", entityKey: pid.Value.ToString());
        }
        return RedirectToAction(nameof(Login));
    }

    [HttpGet]
    public async Task<IActionResult> Index()
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid);
        if (patient is null) return RedirectToAction(nameof(Login));

        var todayUtc = DateTime.UtcNow.Date;
        var upcoming = await _db.Appointments.CountAsync(a => a.PatientId == pid && a.ScheduledStartUtc >= todayUtc && (int)a.Status < 4);
        var bills = await _db.Bills.AsNoTracking()
            .Where(b => b.PatientId == pid && (b.Status == BillStatus.Open || b.Status == BillStatus.PartiallyPaid))
            .Select(b => new { b.NetAmount, b.PaidAmount }).ToListAsync();
        var pending = await _db.LabOrders.CountAsync(o => o.PatientId == pid && o.Status != OrderStatus.Completed && o.Status != OrderStatus.Cancelled);
        var teles = await _db.TeleSessions.CountAsync(t => t.PatientId == pid && (t.Status == TeleSessionStatus.Requested || t.Status == TeleSessionStatus.Scheduled || t.Status == TeleSessionStatus.PatientWaiting || t.Status == TeleSessionStatus.InCall));

        var visits = await _db.Encounters.AsNoTracking()
            .Where(e => e.PatientId == pid)
            .OrderByDescending(e => e.StartedAt)
            .Select(e => new { e.Id, e.StartedAt, e.Status, e.ChiefComplaint })
            .Take(5).ToListAsync();

        return View(new PortalDashboardViewModel
        {
            Patient = patient,
            UpcomingAppointments = upcoming,
            OpenBills = bills.Count,
            OpenBalance = bills.Sum(b => b.NetAmount - b.PaidAmount),
            PendingResults = pending,
            ActiveTeleSessions = teles,
            RecentVisits = visits.Cast<object>().ToList()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Visits()
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var visits = await _db.Encounters.AsNoTracking()
            .Include(e => e.Clinic)
            .Include(e => e.Clinician)
            .Where(e => e.PatientId == pid)
            .OrderByDescending(e => e.StartedAt).Take(50).ToListAsync();
        return View(visits);
    }

    [HttpGet]
    public async Task<IActionResult> Visit(int id)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var visit = await _db.Encounters.AsNoTracking()
            .Include(e => e.Clinic)
            .Include(e => e.Clinician)
            .Include(e => e.Soap)
            .Include(e => e.Diagnoses)
            .Include(e => e.LabOrders).ThenInclude(o => o.LabTest)
            .Include(e => e.LabOrders).ThenInclude(o => o.Result)
            .Include(e => e.ImagingOrders)
            .Include(e => e.ProcedureOrders)
            .Include(e => e.Prescriptions).ThenInclude(p => p.Items)
            .FirstOrDefaultAsync(e => e.Id == id && e.PatientId == pid);
        if (visit is null) return NotFound();
        ViewBag.Certificates = await _db.MedicalCertificates.AsNoTracking()
            .Where(c => c.EncounterId == id).OrderByDescending(c => c.IssuedAt).ToListAsync();
        ViewBag.Referrals = await _db.Referrals.AsNoTracking().Include(r => r.ReferredToClinician)
            .Where(r => r.EncounterId == id).OrderByDescending(r => r.CreatedAt).ToListAsync();
        ViewBag.TeleSession = await _db.TeleSessions.AsNoTracking()
            .FirstOrDefaultAsync(t => t.EncounterId == id);
        return View(visit);
    }

    [HttpGet]
    public async Task<IActionResult> Appointments()
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var appts = await _db.Appointments.AsNoTracking()
            .Include(a => a.Clinic).Include(a => a.Clinician)
            .Where(a => a.PatientId == pid)
            .OrderByDescending(a => a.ScheduledStartUtc).Take(50).ToListAsync();
        return View(appts);
    }

    [HttpGet]
    public async Task<IActionResult> Results()
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var labs = await _db.LabOrders.AsNoTracking()
            .Include(o => o.LabTest)
            .Include(o => o.Result)
            .Where(o => o.PatientId == pid)
            .OrderByDescending(o => o.OrderedAt).Take(50).ToListAsync();
        var imaging = await _db.ImagingOrders.AsNoTracking()
            .Include(o => o.Report)
            .Where(o => o.PatientId == pid)
            .OrderByDescending(o => o.OrderedAt).Take(50).ToListAsync();
        ViewBag.Imaging = imaging;
        return View(labs);
    }

    [HttpGet]
    public async Task<IActionResult> LabResult(int orderId)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var order = await _db.LabOrders.AsNoTracking()
            .Include(o => o.LabTest).ThenInclude(t => t!.Analytes)
            .Include(o => o.OrderedBy)
            .Include(o => o.Encounter)
            .Include(o => o.Result).ThenInclude(r => r!.Values).ThenInclude(v => v.LabAnalyte)
            .Include(o => o.Result).ThenInclude(r => r!.AuthorizedBy)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.PatientId == pid);
        if (order is null) return NotFound();
        return View(order);
    }

    [HttpGet]
    public async Task<IActionResult> ImagingReport(int orderId)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var order = await _db.ImagingOrders.AsNoTracking()
            .Include(o => o.OrderedBy)
            .Include(o => o.Encounter)
            .Include(o => o.Report).ThenInclude(r => r!.AuthorizedBy)
            .Include(o => o.Report).ThenInclude(r => r!.ReportedBy)
            .FirstOrDefaultAsync(o => o.Id == orderId && o.PatientId == pid);
        if (order is null) return NotFound();
        return View(order);
    }

    [HttpGet]
    public async Task<IActionResult> Bills()
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var bills = await _db.Bills.AsNoTracking()
            .Include(b => b.Items).Include(b => b.Payments)
            .Where(b => b.PatientId == pid)
            .OrderByDescending(b => b.CreatedAt).Take(50).ToListAsync();
        return View(bills);
    }

    [HttpGet]
    public async Task<IActionResult> Telemed()
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var sessions = await _db.TeleSessions.AsNoTracking()
            .Include(s => s.Clinician)
            .Include(s => s.Bill)
            .Where(s => s.PatientId == pid)
            .OrderByDescending(s => s.ScheduledStartUtc).Take(50).ToListAsync();
        return View(sessions);
    }

    [HttpGet]
    public async Task<IActionResult> RequestTelemed()
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid);
        var gate = PatientRegistrationGate.Check(patient);
        if (!gate.IsComplete)
        {
            TempData["Info"] = "Complete your hospital profile before requesting a tele-consult.";
            return RedirectToAction(nameof(Profile), new { redirectAfter = Url.Action(nameof(RequestTelemed)) });
        }
        return View(new PortalRequestTeleViewModel
        {
            VideoFee = _tele.Fees.Video,
            AudioFee = _tele.Fees.Audio,
            ChatFee = _tele.Fees.Chat,
            Currency = _tele.Fees.Currency
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestTelemed(PortalRequestTeleViewModel m)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        if (!ModelState.IsValid)
        {
            m.VideoFee = _tele.Fees.Video; m.AudioFee = _tele.Fees.Audio; m.ChatFee = _tele.Fees.Chat; m.Currency = _tele.Fees.Currency;
            return View(m);
        }

        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid);
        if (patient is null) return RedirectToAction(nameof(Login));
        var gate = PatientRegistrationGate.Check(patient);
        if (!gate.IsComplete)
        {
            TempData["Info"] = "Complete your hospital profile before requesting a tele-consult.";
            return RedirectToAction(nameof(Profile), new { redirectAfter = Url.Action(nameof(RequestTelemed)) });
        }

        var sessionId = await _tele.RequestSessionAsync(patient.FacilityId, pid.Value, m.ConsultationReason, m.Mode, m.ScheduledStartUtc);

        // Always capture an intake — even minimal — so the clinician sees context before the call.
        _db.PortalSymptomIntakes.Add(new PortalSymptomIntake
        {
            PatientId = pid.Value,
            TeleSessionId = sessionId,
            ChiefComplaint = m.ConsultationReason,
            Symptoms = m.Symptoms ?? string.Empty
        });
        await _db.SaveChangesAsync();

        TempData["Success"] = "Tele-consult requested. Settle the bill to schedule the call.";
        return RedirectToAction(nameof(Pay), new { teleSessionId = sessionId });
    }

    [HttpGet]
    public async Task<IActionResult> Profile(string? redirectAfter)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid);
        if (patient is null) return RedirectToAction(nameof(Login));
        var gate = PatientRegistrationGate.Check(patient);
        return View(new PortalProfileViewModel
        {
            HospitalNumber = patient.HospitalNumber,
            FirstName = patient.FirstName,
            LastName = patient.LastName,
            DateOfBirth = patient.DateOfBirth,
            Sex = patient.Sex,
            Phone = patient.Phone ?? string.Empty,
            StreetAddress = patient.StreetAddress,
            Lga = patient.Lga,
            State = patient.State,
            MissingFields = gate.MissingFields,
            RedirectAfter = redirectAfter
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Profile(PortalProfileViewModel m)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        if (!ModelState.IsValid) return View(m);

        var patient = await _db.Patients.FirstOrDefaultAsync(p => p.Id == pid);
        if (patient is null) return RedirectToAction(nameof(Login));

        patient.FirstName = m.FirstName.Trim();
        patient.LastName = m.LastName.Trim();
        patient.DateOfBirth = m.DateOfBirth;
        patient.Sex = m.Sex;
        patient.Phone = m.Phone.Trim();
        patient.StreetAddress = m.StreetAddress?.Trim();
        patient.Lga = m.Lga?.Trim();
        patient.State = m.State?.Trim();
        await _db.SaveChangesAsync();

        TempData["Success"] = "Profile updated.";
        if (!string.IsNullOrEmpty(m.RedirectAfter) && Url.IsLocalUrl(m.RedirectAfter)) return Redirect(m.RedirectAfter);
        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Pay(int teleSessionId)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var session = await _db.TeleSessions.AsNoTracking()
            .Include(s => s.Bill)
            .FirstOrDefaultAsync(s => s.Id == teleSessionId && s.PatientId == pid);
        if (session is null) return NotFound();
        if (session.Bill is null) { TempData["Error"] = "No bill on this consult yet."; return RedirectToAction(nameof(Telemed)); }
        if (session.Bill.Status == BillStatus.Paid)
        {
            TempData["Success"] = "Bill already settled — your call is scheduled.";
            return RedirectToAction(nameof(Telemed));
        }
        return View(new PortalPayViewModel
        {
            TeleSessionId = session.Id,
            BillId = session.Bill.Id,
            SessionNumber = session.SessionNumber,
            BillNumber = session.Bill.BillNumber,
            Mode = session.Mode,
            Amount = session.Bill.Balance,
            Currency = _tele.Fees.Currency,
            ConsultationReason = session.ConsultationReason,
            ScheduledStartUtc = session.ScheduledStartUtc
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Pay(int teleSessionId, int billId)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var session = await _db.TeleSessions.Include(s => s.Bill).Include(s => s.Patient)
            .FirstOrDefaultAsync(s => s.Id == teleSessionId && s.PatientId == pid && s.BillId == billId);
        if (session is null || session.Bill is null) return NotFound();
        if (session.Bill.Status == BillStatus.Paid) return RedirectToAction(nameof(PayResult), new { teleSessionId });

        var account = await _db.PortalAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.PatientId == pid);
        var initiated = await _payment.InitiateAsync(new PaymentInitiateRequest(
            FacilityId: session.FacilityId,
            BillId: session.Bill.Id,
            PatientId: pid.Value,
            Amount: session.Bill.Balance,
            CustomerEmail: account?.Email,
            CustomerPhone: session.Patient?.Phone,
            InitiatedById: $"portal-{pid.Value}"));

        // Real gateways redirect to a hosted checkout page that returns the user to our PayCallback when
        // the customer completes the payment. The stub gateway has no checkout, so for now we route the
        // patient straight to PayCallback as if the checkout already succeeded — flipping bill → Paid
        // and session → Scheduled. Wiring up Paystack/Flutterwave just changes this single line.
        return Redirect(Url.Action(nameof(PayCallback), new { txId = initiated.TransactionId, status = "success", teleSessionId })!);
    }

    /// <summary>Portal-facing payment callback the stub gateway redirects to after the fake checkout.
    /// Real Paystack/Flutterwave will hit a webhook endpoint server-side instead, but the same shape works.</summary>
    [HttpGet]
    public async Task<IActionResult> PayCallback(long txId, string status, int teleSessionId)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
        {
            await _payment.MarkSuccessfulAsync(txId, providerReference: null, providerResponse: "Portal callback");
            await _audit.LogAsync("portal.payment.success", AuditCategory.BusinessAction, AuditOutcome.Success,
                entityType: "PaymentTransaction", entityKey: txId.ToString(),
                summary: $"Tele-consult bill paid (session {teleSessionId})", actorOverride: $"patient-{pid}");
            await _notifier.NotifySessionScheduledAsync(teleSessionId);
            TempData["Success"] = "Payment confirmed. Your tele-consult is scheduled.";
        }
        else
        {
            await _payment.MarkFailedAsync(txId, "Cancelled at provider.");
            TempData["Error"] = "Payment was not completed.";
        }
        return RedirectToAction(nameof(PayResult), new { teleSessionId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelTele(int teleSessionId, string? reason)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var s = await _db.TeleSessions.AsNoTracking().FirstOrDefaultAsync(x => x.Id == teleSessionId && x.PatientId == pid);
        if (s is null) return NotFound();
        if (s.Status is TeleSessionStatus.Completed or TeleSessionStatus.Cancelled
            or TeleSessionStatus.NoShowPatient or TeleSessionStatus.NoShowClinician
            or TeleSessionStatus.InCall)
        {
            TempData["Error"] = "This consult cannot be cancelled at this stage.";
            return RedirectToAction(nameof(Telemed));
        }
        var (ok, refund) = await _tele.CancelAsync(teleSessionId, reason ?? "Cancelled by patient", patientInitiated: true);
        TempData[ok ? "Success" : "Error"] = ok
            ? (refund > 0 ? $"Cancelled. Refund of NGN {refund:N2} will be processed." : "Cancelled. No refund applies under our policy.")
            : "Could not cancel.";
        return RedirectToAction(nameof(Telemed));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> RateTele(int teleSessionId, int rating, string? feedback)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        if (rating < 1 || rating > 5) { TempData["Error"] = "Pick a rating between 1 and 5."; return RedirectToAction(nameof(Telemed)); }
        var s = await _db.TeleSessions.FirstOrDefaultAsync(x => x.Id == teleSessionId && x.PatientId == pid);
        if (s is null) return NotFound();
        if (s.Status != TeleSessionStatus.Completed) { TempData["Error"] = "Only completed consults can be rated."; return RedirectToAction(nameof(Telemed)); }
        s.PatientRating = rating;
        s.PatientFeedback = string.IsNullOrWhiteSpace(feedback) ? null : feedback.Trim();
        await _db.SaveChangesAsync();
        TempData["Success"] = "Thanks for the feedback!";
        return RedirectToAction(nameof(Telemed));
    }

    [HttpGet]
    public async Task<IActionResult> Receipt(int paymentId)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var payment = await _db.Payments.AsNoTracking()
            .Include(p => p.Bill).ThenInclude(b => b!.Items)
            .Include(p => p.Bill).ThenInclude(b => b!.Patient)
            .Include(p => p.Bill).ThenInclude(b => b!.Facility)
            .FirstOrDefaultAsync(p => p.Id == paymentId);
        if (payment is null || payment.Bill?.PatientId != pid) return NotFound();
        return View("Receipt", payment);
    }

    [HttpGet]
    public async Task<IActionResult> PayResult(int teleSessionId)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var session = await _db.TeleSessions.AsNoTracking()
            .Include(s => s.Bill).ThenInclude(b => b!.Payments)
            .FirstOrDefaultAsync(s => s.Id == teleSessionId && s.PatientId == pid);
        if (session is null) return NotFound();
        ViewBag.Paid = session.Bill?.Status == BillStatus.Paid;
        ViewBag.LatestPaymentId = session.Bill?.Payments
            .Where(p => p.Status == PaymentStatus.Recorded)
            .OrderByDescending(p => p.ReceivedAt)
            .FirstOrDefault()?.Id;
        return View(session);
    }

    [HttpGet]
    public async Task<IActionResult> Room(string token)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var s = await _db.TeleSessions
            .Include(x => x.Patient)
            .Include(x => x.Clinician)
            .Include(x => x.Bill)
            .FirstOrDefaultAsync(x => x.RoomToken == token && x.PatientId == pid);
        if (s is null) return NotFound();

        // Pay-before-join: block if the bill is outstanding.
        if (s.Bill is not null && s.Bill.Status != BillStatus.Paid)
        {
            TempData["Error"] = $"Settle bill {s.Bill.BillNumber} before joining the call.";
            return RedirectToAction(nameof(Pay), new { teleSessionId = s.Id });
        }

        // Chat-mode tele-sessions don't need a LiveKit Room — send the patient straight to the
        // persistent chat thread.
        if (s.Mode == TeleSessionMode.Chat) return RedirectToAction(nameof(Chat));

        // Don't flip the session to PatientWaiting / InCall yet — that happens only when the patient
        // accepts the consent overlay AND LiveKit reports a successful peer connection.
        ViewBag.Role = "Patient";
        ViewBag.LiveKitConfigured = _liveKit.IsConfigured;
        if (_liveKit.IsConfigured)
        {
            var displayName = s.Patient?.FullName ?? "Patient";
            ViewBag.LiveKitUrl = _liveKit.ServerUrl;
            ViewBag.LiveKitToken = _liveKit.IssueAccessToken(s, $"patient-{pid.Value}", displayName, canPublish: true);
            ViewBag.LiveKitRoom = _liveKit.RoomName(s);
        }
        return View("~/Views/Telemedicine/Room.cshtml", s);
    }

    // ─── Chat threads + 24-hour packages ───────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Chats()
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var threads = await _db.TeleSessions.AsNoTracking()
            .Include(s => s.Clinician)
            .Where(s => s.PatientId == pid && s.Mode == TeleSessionMode.Chat)
            .OrderByDescending(s => s.CreatedAt).ToListAsync();
        // For each thread fetch the latest message + count unread by patient.
        var ids = threads.Select(t => t.Id).ToList();
        var lastMessages = await _db.TeleChatMessages.AsNoTracking()
            .Where(m => ids.Contains(m.TeleSessionId))
            .GroupBy(m => m.TeleSessionId)
            .Select(g => new {
                SessionId = g.Key,
                LastBody = g.OrderByDescending(x => x.SentAt).Select(x => x.Body).FirstOrDefault(),
                LastAt = g.Max(x => (DateTime?)x.SentAt),
                LastRole = g.OrderByDescending(x => x.SentAt).Select(x => x.SenderRole).FirstOrDefault(),
                UnreadByPatient = g.Count(x => x.SenderRole == ChatSenderRole.Clinician && x.ReadByPatientAt == null)
            })
            .ToDictionaryAsync(x => x.SessionId);
        ViewBag.LastMessages = lastMessages;
        ViewBag.Package = await _chat.GetActivePackageAsync(pid.Value);
        return View(threads);
    }

    [HttpGet]
    public async Task<IActionResult> Chat(int? sessionId)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid);
        if (patient is null) return RedirectToAction(nameof(Login));

        TeleSession? session;
        if (sessionId.HasValue)
        {
            // Open a specific thread (history view for completed/cancelled, live for active).
            session = await _db.TeleSessions.AsNoTracking().Include(s => s.Clinician)
                .FirstOrDefaultAsync(s => s.Id == sessionId.Value && s.PatientId == pid && s.Mode == TeleSessionMode.Chat);
            if (session is null) return NotFound();
        }
        else
        {
            // Default: open the current active thread, auto-creating one if a chat package is active.
            session = await _chat.GetOrCreateActiveChatSessionAsync(patient.FacilityId, pid.Value);
        }
        var pkg = await _chat.GetActivePackageAsync(pid.Value);
        ViewBag.Package = pkg;
        ViewBag.Session = session;
        ViewBag.PatientId = pid.Value;
        IReadOnlyList<TeleChatMessage> msgs = session != null
            ? await _chat.ListMessagesAsync(session.Id)
            : Array.Empty<TeleChatMessage>();
        if (session != null) await _chat.MarkReadAsync(session.Id, ChatSenderRole.Patient);
        ViewBag.Messages = msgs;
        ViewBag.Attachments = await _chat.ListAttachmentsAsync(msgs.Select(m => m.Id));
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EndChat(int sessionId)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var session = await _db.TeleSessions.FirstOrDefaultAsync(s => s.Id == sessionId && s.PatientId == pid && s.Mode == TeleSessionMode.Chat);
        if (session is null) return NotFound();
        if (session.Status is TeleSessionStatus.Completed or TeleSessionStatus.Cancelled)
        {
            TempData["Info"] = "That chat is already closed.";
            return RedirectToAction(nameof(Chats));
        }
        await _tele.EndSessionAsync(session.Id, null);
        TempData["Success"] = "Chat closed.";
        return RedirectToAction(nameof(Chats));
    }

    public record PostChatMessageRequest(string Body, long? RepliesToMessageId);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PostChatMessage([FromBody] PostChatMessageRequest req)
    {
        var pid = CurrentPatientId();
        if (pid is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(req.Body)) return BadRequest(new { error = "Empty message." });

        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid);
        if (patient is null) return NotFound();
        var session = await _chat.GetOrCreateActiveChatSessionAsync(patient.FacilityId, pid.Value);
        if (session is null)
            return BadRequest(new { error = "Buy a chat package or request a chat consult to start a conversation." });
        var msg = await _chat.AddMessageAsync(session.Id, ChatSenderRole.Patient, null, req.Body, req.RepliesToMessageId);
        return Ok(new { id = msg.Id, sentAt = msg.SentAt });
    }

    [HttpGet]
    public async Task<IActionResult> ChatMessages(long? sinceId, int? sessionId)
    {
        var pid = CurrentPatientId();
        if (pid is null) return Unauthorized();
        TeleSession? session;
        if (sessionId.HasValue)
        {
            session = await _db.TeleSessions.AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == sessionId.Value && s.PatientId == pid && s.Mode == TeleSessionMode.Chat);
        }
        else
        {
            session = await _db.TeleSessions.AsNoTracking()
                .Where(s => s.PatientId == pid && s.Mode == TeleSessionMode.Chat
                    && s.Status != TeleSessionStatus.Completed && s.Status != TeleSessionStatus.Cancelled)
                .OrderByDescending(s => s.CreatedAt).FirstOrDefaultAsync();
        }
        if (session is null) return Ok(new { messages = Array.Empty<object>() });

        await _chat.MarkReadAsync(session.Id, ChatSenderRole.Patient);
        var msgs = await _db.TeleChatMessages.AsNoTracking()
            .Include(m => m.SenderUser)
            .Include(m => m.RepliesToMessage).ThenInclude(rm => rm!.SenderUser)
            .Where(m => m.TeleSessionId == session.Id && (sinceId == null || m.Id > sinceId))
            .OrderBy(m => m.SentAt).ToListAsync();
        var attsByMsg = await _chat.ListAttachmentsAsync(msgs.Select(m => m.Id));
        return Ok(new { messages = msgs.Select(m => new {
            id = m.Id,
            role = m.SenderRole.ToString(),
            who = m.SenderRole == ChatSenderRole.Clinician ? (m.SenderUser != null ? "Dr " + m.SenderUser.FullName : "Clinician") : "You",
            body = m.Body,
            sentAt = m.SentAt,
            // Read receipt: was THIS message read by the OTHER party? Only meaningful for messages
            // I (the patient) sent; for incoming messages it's irrelevant.
            readByOther = m.SenderRole == ChatSenderRole.Patient && m.ReadByClinicianAt.HasValue,
            replyTo = m.RepliesToMessage == null ? null : new {
                id = m.RepliesToMessage.Id,
                role = m.RepliesToMessage.SenderRole.ToString(),
                who = m.RepliesToMessage.SenderRole == ChatSenderRole.Clinician ? (m.RepliesToMessage.SenderUser != null ? "Dr " + m.RepliesToMessage.SenderUser.FullName : "Clinician") : "You",
                snippet = m.RepliesToMessage.Body.Length > 120 ? m.RepliesToMessage.Body.Substring(0, 120) + "…" : m.RepliesToMessage.Body
            },
            attachments = (attsByMsg.TryGetValue(m.Id, out var atts) ? atts : new()).Select(a => new { id = a.Id, fileName = a.FileName, contentType = a.ContentType, sizeBytes = a.SizeBytes, url = a.Url })
        }) });
    }

    public record PushSubscribeRequest(string Endpoint, string P256dh, string Auth);

    [HttpGet]
    public IActionResult PushKey([FromServices] IWebPushService push) =>
        Ok(new { publicKey = push.PublicKey, configured = push.IsConfigured });

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PushSubscribe([FromBody] PushSubscribeRequest req, [FromServices] IWebPushService push)
    {
        var pid = CurrentPatientId();
        if (pid is null) return Unauthorized();
        if (string.IsNullOrEmpty(req.Endpoint)) return BadRequest(new { error = "Missing endpoint." });
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid);
        await push.SubscribeAsync(Models.Integrations.PushOwnerType.Patient, pid.Value.ToString(),
            req.Endpoint, req.P256dh, req.Auth, Request.Headers.UserAgent.ToString(), patient?.FacilityId);
        return Ok(new { ok = true });
    }

    public record PushUnsubscribeRequest(string Endpoint);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PushUnsubscribe([FromBody] PushUnsubscribeRequest req, [FromServices] IWebPushService push)
    {
        if (CurrentPatientId() is null) return Unauthorized();
        await push.UnsubscribeAsync(req.Endpoint);
        return Ok(new { ok = true });
    }

    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadChatAttachment(IFormFile file, [FromServices] IWebHostEnvironment env)
    {
        var pid = CurrentPatientId();
        if (pid is null) return Unauthorized();
        if (file is null || file.Length == 0) return BadRequest(new { error = "No file." });
        if (file.Length > 5 * 1024 * 1024) return BadRequest(new { error = "Maximum 5 MB per attachment." });
        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/gif", "application/pdf" };
        if (!allowed.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only images (JPG/PNG/WEBP/GIF) and PDFs are accepted." });

        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid);
        if (patient is null) return NotFound();
        var session = await _chat.GetOrCreateActiveChatSessionAsync(patient.FacilityId, pid.Value);
        if (session is null) return BadRequest(new { error = "Buy a chat package or request a chat consult first." });

        var ext = Path.GetExtension(file.FileName);
        var name = $"{Guid.NewGuid():N}{ext}";
        var subdir = Path.Combine("uploads", "chat", DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
        var dir = Path.Combine(env.WebRootPath, subdir);
        Directory.CreateDirectory(dir);
        var fullPath = Path.Combine(dir, name);
        await using (var stream = System.IO.File.Create(fullPath))
            await file.CopyToAsync(stream);
        var url = "/" + Path.Combine(subdir, name).Replace('\\', '/');

        var msg = await _chat.AddMessageAsync(session.Id, ChatSenderRole.Patient, null, $"📎 {file.FileName}");
        await _chat.AttachToMessageAsync(msg.Id, file.FileName, file.ContentType, file.Length, url);
        return Ok(new { id = msg.Id, url, fileName = file.FileName, contentType = file.ContentType });
    }

    [HttpGet]
    public async Task<IActionResult> BuyChatPackage()
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var pkg = await _chat.GetActivePackageAsync(pid.Value);
        if (pkg.IsActive)
        {
            TempData["Info"] = "You already have an active chat package.";
            return RedirectToAction(nameof(Chat));
        }
        ViewBag.Price = pkg.Price;
        return View();
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> BuyChatPackage(string? confirm)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == pid);
        if (patient is null) return RedirectToAction(nameof(Login));

        var pkg = await _chat.GetActivePackageAsync(pid.Value);
        var billId = await _chat.CreateChatPackageBillAsync(patient.FacilityId, pid.Value, pkg.Price, _billing);
        var account = await _db.PortalAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.PatientId == pid);
        var bill = await _db.Bills.AsNoTracking().FirstOrDefaultAsync(b => b.Id == billId);
        var initiated = await _payment.InitiateAsync(new PaymentInitiateRequest(
            FacilityId: patient.FacilityId,
            BillId: billId,
            PatientId: pid.Value,
            Amount: bill?.NetAmount ?? pkg.Price,
            CustomerEmail: account?.Email,
            CustomerPhone: patient.Phone,
            InitiatedById: $"portal-{pid.Value}"));
        return Redirect(Url.Action(nameof(ChatPackageCallback), new { txId = initiated.TransactionId, status = "success", billId })!);
    }

    [HttpGet]
    public async Task<IActionResult> ChatPackageCallback(long txId, string status, int billId)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        if (string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
        {
            await _payment.MarkSuccessfulAsync(txId, providerReference: null, providerResponse: "Chat package callback");
            await _chat.ActivatePackageForBillAsync(billId);
            await _audit.LogAsync("portal.chat.package.activated", AuditCategory.BusinessAction, AuditOutcome.Success,
                entityType: "ChatPackage", entityKey: billId.ToString(),
                summary: $"24h chat package activated", actorOverride: $"patient-{pid}");
            TempData["Success"] = "Chat package activated. You have 24 hours of unlimited chat.";
        }
        else
        {
            await _payment.MarkFailedAsync(txId, "Cancelled at provider.");
            TempData["Error"] = "Payment was not completed.";
        }
        return RedirectToAction(nameof(Chat));
    }

    /// <summary>Called by the room JS once the patient accepts consent and LiveKit connects.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmJoin(int teleSessionId)
    {
        var pid = CurrentPatientId();
        if (pid is null) return Unauthorized();
        var owns = await _db.TeleSessions.AsNoTracking().AnyAsync(s => s.Id == teleSessionId && s.PatientId == pid);
        if (!owns) return Unauthorized();
        var ok = await _tele.PatientJoinAsync(teleSessionId);
        return ok ? Ok(new { joinedAt = DateTime.UtcNow }) : BadRequest(new { error = "Could not mark joined." });
    }

    [HttpGet]
    public async Task<IActionResult> Intake(int? teleSessionId)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        return View(new PortalIntakeViewModel { TeleSessionId = teleSessionId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Intake(PortalIntakeViewModel m)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));
        if (!ModelState.IsValid) return View(m);
        _db.PortalSymptomIntakes.Add(new PortalSymptomIntake
        {
            PatientId = pid.Value,
            TeleSessionId = m.TeleSessionId,
            ChiefComplaint = m.ChiefComplaint,
            Symptoms = m.Symptoms,
            DurationDays = m.DurationDays,
            Severity = m.Severity,
            CurrentMedications = m.CurrentMedications,
            KnownAllergies = m.KnownAllergies,
            Notes = m.Notes
        });
        await _db.SaveChangesAsync();
        TempData["Success"] = "Symptom intake submitted.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PayBill(int billId)
    {
        var pid = CurrentPatientId();
        if (pid is null) return RedirectToAction(nameof(Login));

        var bill = await _db.Bills.Include(b => b.Patient)
            .FirstOrDefaultAsync(b => b.Id == billId && b.PatientId == pid);
        if (bill is null) return NotFound();
        if (bill.Status is BillStatus.Paid or BillStatus.Cancelled or BillStatus.WrittenOff)
        {
            TempData["Error"] = "This bill is not payable.";
            return RedirectToAction(nameof(Bills));
        }

        var account = await _db.PortalAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.PatientId == pid);
        var init = await _payment.InitiateAsync(new PaymentInitiateRequest(
            bill.FacilityId, bill.Id, pid.Value, bill.Balance,
            account?.Email, bill.Patient?.Phone, null));

        await _audit.LogAsync("portal.payment.initiated", AuditCategory.BusinessAction, AuditOutcome.Success,
            entityType: "PaymentTransaction", entityKey: init.TransactionId.ToString(),
            summary: $"Patient initiated online payment {init.Reference} for {bill.BillNumber} (₦{bill.Balance:N2})",
            facilityId: bill.FacilityId);

        return Redirect(init.AuthorizationUrl);
    }

    [HttpGet]
    public async Task<IActionResult> SymptomChecker()
    {
        var pid = CurrentPatientId();
        if (pid is null) return Challenge();
        var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pid.Value);
        if (p is null) return NotFound();
        ViewBag.Patient = p;
        return View();
    }

    public class PortalSymptomDto { public string? Question { get; set; } public List<string>? History { get; set; } }

    [HttpPost]
    public async Task<IActionResult> SymptomCheckerAsk([FromBody] PortalSymptomDto dto)
    {
        var pid = CurrentPatientId();
        if (pid is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.Question))
            return Json(new { ok = false, error = "Type your question first." });

        var p = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(x => x.Id == pid.Value);
        if (p is null) return NotFound();
        var account = await _db.PortalAccounts.AsNoTracking().FirstOrDefaultAsync(a => a.PatientId == pid.Value);

        var ageSex = p.DateOfBirth.HasValue
            ? $"{(int)((DateTime.UtcNow - p.DateOfBirth.Value.ToDateTime(TimeOnly.MinValue)).TotalDays / 365)}y {p.Sex}"
            : p.Sex.ToString();

        var input = new SymptomCheckerInput(p.FacilityId, account?.Id, ageSex, dto.Question, dto.History ?? new());
        var outcome = await _ai.SymptomCheckAsync(input, requestedById: null);
        return Json(new { ok = outcome.Ok, text = outcome.Text, error = outcome.Error });
    }
}
