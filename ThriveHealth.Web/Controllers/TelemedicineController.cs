using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Telemedicine;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.TelemedicineClinician)]
public class TelemedicineController : Controller
{
    public const string TeleStaff = Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.Nurse + "," + Roles.ChiefNursingOfficer + "," + Roles.Receptionist + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ITelemedicineService _tele;
    private readonly ILiveKitTokenService _liveKit;
    private readonly ITeleConsultActions _actions;
    private readonly ITeleChatService _chat;

    public TelemedicineController(ApplicationDbContext db, UserManager<ApplicationUser> userManager,
        ITelemedicineService tele, ILiveKitTokenService liveKit, ITeleConsultActions actions, ITeleChatService chat)
    {
        _db = db; _userManager = userManager; _tele = tele; _liveKit = liveKit; _actions = actions; _chat = chat;
    }

    private async Task<(int facilityId, string userId)?> Ctx()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u?.FacilityId is null) return null;
        return (u.FacilityId.Value, u.Id);
    }

    [HttpGet]
    public async Task<IActionResult> Index(TeleSessionStatus? status)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var query = _db.TeleSessions.AsNoTracking()
            .Include(s => s.Patient).Include(s => s.Clinician)
            .Where(s => s.FacilityId == ctx.Value.facilityId);
        if (status.HasValue) query = query.Where(s => s.Status == status.Value);

        var sessions = await query.OrderByDescending(s => s.ScheduledStartUtc).Take(200).ToListAsync();
        var todayUtc = DateTime.UtcNow.Date;
        var rows = sessions.Select(s => new TeleSessionListRow { Session = s, Patient = s.Patient! }).ToList();

        return View(new TeleSessionListViewModel
        {
            Rows = rows,
            FilterStatus = status,
            RequestedCount = rows.Count(r => r.Session.Status == TeleSessionStatus.Requested),
            InCallCount = rows.Count(r => r.Session.Status is TeleSessionStatus.InCall or TeleSessionStatus.PatientWaiting),
            CompletedTodayCount = rows.Count(r => r.Session.Status == TeleSessionStatus.Completed && r.Session.EndedAt.HasValue && r.Session.EndedAt.Value >= todayUtc)
        });
    }

    [HttpGet]
    public IActionResult New() => View("Request", new TeleSessionRequestViewModel());

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> New(TeleSessionRequestViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        if (m.PatientId is null) ModelState.AddModelError(nameof(m.PatientId), "Pick a patient.");
        if (!ModelState.IsValid) return View("Request", m);

        var id = await _tele.RequestSessionAsync(ctx.Value.facilityId, m.PatientId!.Value, m.ConsultationReason, m.Mode, m.ScheduledStartUtc);
        TempData["Success"] = "Tele-session created.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    [HttpGet]
    public async Task<IActionResult> Detail(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var s = await _db.TeleSessions
            .Include(x => x.Patient).Include(x => x.Clinician)
            .FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (s is null) return NotFound();

        var intakes = await _db.PortalSymptomIntakes
            .Where(i => i.PatientId == s.PatientId)
            .OrderByDescending(i => i.SubmittedAt).Take(5).ToListAsync();
        ViewBag.Intakes = intakes;
        return View(s);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Claim(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        await _tele.AssignClinicianAsync(id, ctx.Value.userId);
        TempData["Success"] = "Session assigned to you.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    /// <summary>How many tele-rooms a single clinician can be live in at once. Prevents accidental
    /// double-booking when a clinician taps Join on multiple sessions.</summary>
    private const int MaxConcurrentSessions = 1;

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Join(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var s = await _db.TeleSessions.Include(x => x.Bill)
            .FirstOrDefaultAsync(x => x.Id == id && x.FacilityId == ctx.Value.facilityId);
        if (s is null) return NotFound();
        if (s.Bill is not null && s.Bill.Status != ThriveHealth.Web.Models.Billing.BillStatus.Paid)
        {
            TempData["Error"] = $"Patient hasn't paid yet (bill {s.Bill.BillNumber}, balance {s.Bill.Balance:N2}). The call cannot start until the bill is settled.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        // Concurrent-session guard: don't let a clinician open Room B while still live in Room A.
        var liveElsewhere = await _db.TeleSessions.AsNoTracking()
            .Where(x => x.Id != id && x.ClinicianId == ctx.Value.userId
                && x.ClinicianJoinedAt != null && x.EndedAt == null
                && (x.Status == TeleSessionStatus.InCall || x.Status == TeleSessionStatus.PatientWaiting))
            .Select(x => x.SessionNumber).Take(MaxConcurrentSessions + 1).ToListAsync();
        if (liveElsewhere.Count >= MaxConcurrentSessions)
        {
            TempData["Error"] = $"You're already in another live tele-call ({string.Join(", ", liveElsewhere)}). End that one before starting another.";
            return RedirectToAction(nameof(Detail), new { id });
        }

        // Claim the session for this clinician (so the LiveKit token can be minted),
        // but DO NOT mark the call as joined yet — that happens only after the clinician
        // accepts the consent overlay and the LiveKit peer connection actually establishes.
        if (s.ClinicianId is null) await _tele.AssignClinicianAsync(id, ctx.Value.userId);

        // Chat-mode sessions don't open a video room — they take the clinician to the persistent thread.
        if (s.Mode == TeleSessionMode.Chat)
            return RedirectToAction(nameof(Chat), new { patientId = s.PatientId });

        return RedirectToAction(nameof(Room), new { token = s.RoomToken });
    }

    /// <summary>Called by the room JS once LiveKit connect resolves and the clinician has consented.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> ConfirmJoin(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        var ok = await _tele.ClinicianJoinAsync(id);
        return ok ? Ok(new { joinedAt = DateTime.UtcNow }) : BadRequest(new { error = "Could not mark joined." });
    }

    [HttpGet]
    public async Task<IActionResult> Room(string token)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var s = await _db.TeleSessions
            .Include(x => x.Patient).Include(x => x.Clinician)
            .Include(x => x.Encounter).ThenInclude(e => e!.Soap)
            .Include(x => x.Encounter).ThenInclude(e => e!.LabOrders)
            .Include(x => x.Encounter).ThenInclude(e => e!.ImagingOrders)
            .Include(x => x.Encounter).ThenInclude(e => e!.Prescriptions).ThenInclude(p => p.Items)
            .FirstOrDefaultAsync(x => x.RoomToken == token && x.FacilityId == ctx.Value.facilityId);
        if (s is null) return NotFound();

        // Side-loaded: medical certificates and referrals tied to this encounter.
        if (s.EncounterId.HasValue)
        {
            ViewBag.Certificates = await _db.MedicalCertificates.AsNoTracking()
                .Where(c => c.EncounterId == s.EncounterId.Value).OrderByDescending(c => c.IssuedAt).ToListAsync();
            ViewBag.Referrals = await _db.Referrals.AsNoTracking().Include(r => r.ReferredToClinician)
                .Where(r => r.EncounterId == s.EncounterId.Value).OrderByDescending(r => r.CreatedAt).ToListAsync();
            ViewBag.FollowUps = await _db.Appointments.AsNoTracking().Include(a => a.Clinic)
                .Where(a => a.PatientId == s.PatientId && a.BookedById == ctx.Value.userId && a.CreatedAt >= s.CreatedAt)
                .OrderByDescending(a => a.CreatedAt).ToListAsync();
        }
        ViewBag.Role = "Clinician";
        ViewBag.LiveKitConfigured = _liveKit.IsConfigured;
        if (_liveKit.IsConfigured)
        {
            var clinician = s.Clinician ?? await _userManager.GetUserAsync(User);
            var displayName = clinician?.FullName ?? "Clinician";
            ViewBag.LiveKitUrl = _liveKit.ServerUrl;
            ViewBag.LiveKitToken = _liveKit.IssueAccessToken(s, $"clinician-{ctx.Value.userId}", displayName, canPublish: true);
            ViewBag.LiveKitRoom = _liveKit.RoomName(s);
        }
        return View("Room", s);
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> End(int id, string? notes)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        await _tele.EndSessionAsync(id, notes, ctx.Value.userId);
        TempData["Success"] = "Session ended and signed off.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    public record SaveNotesRequest(string? Subjective, string? Objective, string? Assessment, string? Plan);

    /// <summary>JSON endpoint called by the room view every few seconds while the clinician is typing.</summary>
    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveNotes(int id, [FromBody] SaveNotesRequest req)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        var ok = await _tele.SaveNotesAsync(id, ctx.Value.userId, req.Subjective, req.Objective, req.Assessment, req.Plan);
        return ok ? Ok(new { savedAt = DateTime.UtcNow }) : BadRequest(new { error = "No active encounter — try refreshing the room." });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(int id)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        await _tele.CancelAsync(id);
        TempData["Success"] = "Session cancelled.";
        return RedirectToAction(nameof(Detail), new { id });
    }

    // ========================================================================
    // Phase B — in-room consult actions (XHR endpoints called from Room.cshtml)
    // ========================================================================

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Prescribe(int id, [FromBody] PrescriptionItemInput req)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(req.DrugName)) return BadRequest(new { error = "Drug name is required." });
        var newId = await _actions.AddPrescriptionItemAsync(id, ctx.Value.userId, req);
        if (newId is null) return BadRequest(new { error = "No active encounter or you're not the assigned clinician." });
        return Ok(new { id = newId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> OrderLab(int id, [FromBody] LabOrderInput req)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(req.TestName)) return BadRequest(new { error = "Test name is required." });
        var newId = await _actions.CreateLabOrderAsync(id, ctx.Value.userId, req);
        if (newId is null) return BadRequest(new { error = "No active encounter." });
        return Ok(new { id = newId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> OrderImaging(int id, [FromBody] ImagingOrderInput req)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(req.StudyDescription)) return BadRequest(new { error = "Study description is required." });
        var newId = await _actions.CreateImagingOrderAsync(id, ctx.Value.userId, req);
        if (newId is null) return BadRequest(new { error = "No active encounter." });
        return Ok(new { id = newId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> IssueSickNote(int id, [FromBody] MedicalCertificateInput req)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        if (req.EndDate < req.StartDate) return BadRequest(new { error = "End date cannot be before start date." });
        var newId = await _actions.IssueMedicalCertificateAsync(id, ctx.Value.userId, req);
        if (newId is null) return BadRequest(new { error = "No active encounter." });
        return Ok(new { id = newId, viewUrl = Url.Action(nameof(SickNote), new { certId = newId }) });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Refer(int id, [FromBody] ReferralInput req)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(req.Specialty) && string.IsNullOrWhiteSpace(req.ReferredToClinicianId))
            return BadRequest(new { error = "Pick a specialty or a specific clinician." });
        var newId = await _actions.CreateReferralAsync(id, ctx.Value.userId, req);
        if (newId is null) return BadRequest(new { error = "No active encounter." });
        return Ok(new { id = newId });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> FollowUp(int id, [FromBody] FollowUpInput req)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        var newId = await _actions.BookFollowUpAsync(id, ctx.Value.userId, req);
        if (newId is null) return BadRequest(new { error = "Could not book follow-up — no clinic available." });
        return Ok(new { id = newId });
    }

    /// <summary>Type-ahead search over the facility's drug formulary.</summary>
    [HttpGet]
    public async Task<IActionResult> SearchDrugs(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2) return Ok(Array.Empty<object>());
        var lower = q.Trim().ToLower();
        var results = await _db.Drugs.AsNoTracking()
            .Where(d => d.IsActive && (d.GenericName.ToLower().Contains(lower) || (d.BrandName != null && d.BrandName.ToLower().Contains(lower))))
            .OrderBy(d => d.GenericName).Take(15)
            .Select(d => new { d.Id, label = d.Display, d.GenericName, d.BrandName, d.Strength, d.NafdacNumber, isControlled = d.Schedule != ThriveHealth.Web.Models.Pharmacy.DrugCategory.PrescriptionOnly && d.Schedule != ThriveHealth.Web.Models.Pharmacy.DrugCategory.OverTheCounter })
            .ToListAsync();
        return Ok(results);
    }

    [HttpGet]
    public async Task<IActionResult> SearchLabTests(string q)
    {
        if (string.IsNullOrWhiteSpace(q) || q.Length < 2) return Ok(Array.Empty<object>());
        var lower = q.Trim().ToLower();
        var results = await _db.LabTests.AsNoTracking()
            .Where(t => t.IsActive && (t.Name.ToLower().Contains(lower) || t.Code.ToLower().Contains(lower)))
            .OrderBy(t => t.Name).Take(15)
            .Select(t => new { t.Id, label = $"{t.Name} ({t.Code})", t.Code, t.Name, t.Specimen, section = t.Section.ToString() })
            .ToListAsync();
        return Ok(results);
    }

    [HttpGet]
    public async Task<IActionResult> SearchSpecialists(string? q)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        var query = _userManager.Users.Where(u => u.IsActive && u.FacilityId == ctx.Value.facilityId && u.Id != ctx.Value.userId);
        if (!string.IsNullOrWhiteSpace(q))
        {
            var lower = q.Trim().ToLower();
            query = query.Where(u =>
                u.FirstName.ToLower().Contains(lower) ||
                u.LastName.ToLower().Contains(lower) ||
                (u.Designation != null && u.Designation.ToLower().Contains(lower)) ||
                (u.Department != null && u.Department.ToLower().Contains(lower)));
        }
        var raw = await query.OrderBy(u => u.LastName).Take(15)
            .Select(u => new { u.Id, u.FirstName, u.MiddleName, u.LastName, u.Designation, u.Department })
            .ToListAsync();
        var results = raw.Select(u => new
        {
            u.Id,
            label = $"{(string.IsNullOrWhiteSpace(u.MiddleName) ? $"{u.FirstName} {u.LastName}" : $"{u.FirstName} {u.MiddleName} {u.LastName}")}{(string.IsNullOrEmpty(u.Designation) ? "" : " — " + u.Designation)}",
            specialty = u.Designation ?? u.Department
        });
        return Ok(results);
    }

    // ─── Chat inbox + threads ──────────────────────────────────────────────────────────
    [HttpGet]
    public async Task<IActionResult> Chats(string? scope)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        // Default scope = "mine" so a clinician sees only threads they're in. Toggle to "all" via the tab.
        var mine = string.IsNullOrEmpty(scope) || string.Equals(scope, "mine", StringComparison.OrdinalIgnoreCase);
        var threads = await _chat.ListClinicianInboxAsync(ctx.Value.facilityId, mine ? ctx.Value.userId : null);
        ViewBag.Scope = mine ? "mine" : "all";
        ViewBag.MineCount = mine ? threads.Count : (await _chat.ListClinicianInboxAsync(ctx.Value.facilityId, ctx.Value.userId)).Count;
        ViewBag.AllCount = mine ? (await _chat.ListClinicianInboxAsync(ctx.Value.facilityId)).Count : threads.Count;
        return View(threads);
    }

    [HttpGet]
    public async Task<IActionResult> Chat(int patientId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var patient = await _db.Patients.AsNoTracking().FirstOrDefaultAsync(p => p.Id == patientId && p.FacilityId == ctx.Value.facilityId);
        if (patient is null) return NotFound();

        var session = await _db.TeleSessions
            .Where(s => s.PatientId == patientId && s.Mode == TeleSessionMode.Chat
                && s.Status != TeleSessionStatus.Completed && s.Status != TeleSessionStatus.Cancelled)
            .OrderByDescending(s => s.CreatedAt)
            .FirstOrDefaultAsync();
        if (session != null && session.ClinicianId is null)
        {
            // First clinician to open the thread claims it.
            await _tele.AssignClinicianAsync(session.Id, ctx.Value.userId);
        }
        if (session != null) await _chat.MarkReadAsync(session.Id, ChatSenderRole.Clinician);

        ViewBag.Patient = patient;
        ViewBag.Session = session;
        var msgs = await _chat.ListMessagesByPatientAsync(patientId, take: 200);
        ViewBag.Messages = msgs;
        ViewBag.Attachments = await _chat.ListAttachmentsAsync(msgs.Select(m => m.Id));
        ViewBag.Participants = session is null ? new List<TeleSessionParticipant>() :
            await _db.TeleSessionParticipants.AsNoTracking().Include(p => p.Clinician)
                .Where(p => p.TeleSessionId == session.Id).ToListAsync();
        return View();
    }

    public record PostChatMessageRequest(string Body, long? RepliesToMessageId);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PostChatMessage(int patientId, [FromBody] PostChatMessageRequest req)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(req.Body)) return BadRequest(new { error = "Empty message." });

        var session = await _db.TeleSessions.FirstOrDefaultAsync(s => s.PatientId == patientId
            && s.Mode == TeleSessionMode.Chat
            && s.Status != TeleSessionStatus.Completed && s.Status != TeleSessionStatus.Cancelled);
        if (session is null) return BadRequest(new { error = "No active chat thread for this patient." });
        if (session.ClinicianId is null) await _tele.AssignClinicianAsync(session.Id, ctx.Value.userId);
        var msg = await _chat.AddMessageAsync(session.Id, ChatSenderRole.Clinician, ctx.Value.userId, req.Body, req.RepliesToMessageId);
        return Ok(new { id = msg.Id, sentAt = msg.SentAt });
    }

    [HttpGet]
    public async Task<IActionResult> ChatMessages(int patientId, long? sinceId)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        var session = await _db.TeleSessions.AsNoTracking()
            .Where(s => s.PatientId == patientId && s.FacilityId == ctx.Value.facilityId
                && s.Mode == TeleSessionMode.Chat
                && s.Status != TeleSessionStatus.Completed && s.Status != TeleSessionStatus.Cancelled)
            .OrderByDescending(s => s.CreatedAt).FirstOrDefaultAsync();
        if (session != null) await _chat.MarkReadAsync(session.Id, ChatSenderRole.Clinician);

        var msgs = await _db.TeleChatMessages.AsNoTracking()
            .Include(m => m.SenderUser)
            .Include(m => m.RepliesToMessage).ThenInclude(rm => rm!.SenderUser)
            .Where(m => m.PatientId == patientId && (sinceId == null || m.Id > sinceId))
            .OrderBy(m => m.SentAt).ToListAsync();
        var attsByMsg = await _chat.ListAttachmentsAsync(msgs.Select(m => m.Id));
        return Ok(new { messages = msgs.Select(m => new {
            id = m.Id,
            role = m.SenderRole.ToString(),
            who = m.SenderRole == ChatSenderRole.Clinician ? (m.SenderUser != null ? "Dr " + m.SenderUser.FullName : "Clinician") : "Patient",
            body = m.Body,
            sentAt = m.SentAt,
            readByOther = m.SenderRole == ChatSenderRole.Clinician && m.ReadByPatientAt.HasValue,
            replyTo = m.RepliesToMessage == null ? null : new {
                id = m.RepliesToMessage.Id,
                role = m.RepliesToMessage.SenderRole.ToString(),
                who = m.RepliesToMessage.SenderRole == ChatSenderRole.Clinician ? (m.RepliesToMessage.SenderUser != null ? "Dr " + m.RepliesToMessage.SenderUser.FullName : "Clinician") : "Patient",
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
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        if (string.IsNullOrEmpty(req.Endpoint)) return BadRequest(new { error = "Missing endpoint." });
        await push.SubscribeAsync(Models.Integrations.PushOwnerType.Clinician, ctx.Value.userId,
            req.Endpoint, req.P256dh, req.Auth, Request.Headers.UserAgent.ToString(), ctx.Value.facilityId);
        return Ok(new { ok = true });
    }

    public record PushUnsubscribeRequest(string Endpoint);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> PushUnsubscribe([FromBody] PushUnsubscribeRequest req, [FromServices] IWebPushService push)
    {
        if (await Ctx() is null) return Unauthorized();
        await push.UnsubscribeAsync(req.Endpoint);
        return Ok(new { ok = true });
    }

    /// <summary>Upload an image / PDF attachment to a chat thread. Returns a JSON descriptor that the
    /// composer attaches to the next outgoing message.</summary>
    [HttpPost, ValidateAntiForgeryToken, RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> UploadChatAttachment(int patientId, IFormFile file, [FromServices] IWebHostEnvironment env)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        if (file is null || file.Length == 0) return BadRequest(new { error = "No file." });
        if (file.Length > 5 * 1024 * 1024) return BadRequest(new { error = "Maximum 5 MB per attachment." });
        var allowed = new[] { "image/jpeg", "image/png", "image/webp", "image/gif", "application/pdf" };
        if (!allowed.Contains(file.ContentType, StringComparer.OrdinalIgnoreCase))
            return BadRequest(new { error = "Only images (JPG/PNG/WEBP/GIF) and PDFs are accepted." });

        var session = await _db.TeleSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.PatientId == patientId && s.FacilityId == ctx.Value.facilityId
                && s.Mode == TeleSessionMode.Chat
                && s.Status != TeleSessionStatus.Completed && s.Status != TeleSessionStatus.Cancelled);
        if (session is null) return BadRequest(new { error = "No active chat thread." });

        var ext = Path.GetExtension(file.FileName);
        var name = $"{Guid.NewGuid():N}{ext}";
        var subdir = Path.Combine("uploads", "chat", DateTime.UtcNow.ToString("yyyy"), DateTime.UtcNow.ToString("MM"));
        var dir = Path.Combine(env.WebRootPath, subdir);
        Directory.CreateDirectory(dir);
        var fullPath = Path.Combine(dir, name);
        await using (var stream = System.IO.File.Create(fullPath))
            await file.CopyToAsync(stream);
        var url = "/" + Path.Combine(subdir, name).Replace('\\', '/');

        var msg = await _chat.AddMessageAsync(session.Id, ChatSenderRole.Clinician, ctx.Value.userId, $"📎 {file.FileName}");
        await _chat.AttachToMessageAsync(msg.Id, file.FileName, file.ContentType, file.Length, url);
        return Ok(new { id = msg.Id, url, fileName = file.FileName, contentType = file.ContentType });
    }

    public record AddParticipantRequest(string ClinicianId, string? Role);

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> AddChatParticipant(int patientId, [FromBody] AddParticipantRequest req)
    {
        var ctx = await Ctx();
        if (ctx is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(req.ClinicianId)) return BadRequest(new { error = "Pick a clinician." });

        var session = await _db.TeleSessions.AsNoTracking()
            .FirstOrDefaultAsync(s => s.PatientId == patientId && s.FacilityId == ctx.Value.facilityId
                && s.Mode == TeleSessionMode.Chat
                && s.Status != TeleSessionStatus.Completed && s.Status != TeleSessionStatus.Cancelled);
        if (session is null) return BadRequest(new { error = "No active chat thread." });

        // Don't add the same clinician twice (or the primary clinician).
        if (session.ClinicianId == req.ClinicianId)
            return BadRequest(new { error = "That clinician is already the primary." });
        var exists = await _db.TeleSessionParticipants.AnyAsync(p => p.TeleSessionId == session.Id && p.ClinicianId == req.ClinicianId);
        if (exists) return BadRequest(new { error = "Already in this thread." });

        var participant = new TeleSessionParticipant
        {
            TeleSessionId = session.Id,
            ClinicianId = req.ClinicianId,
            Role = req.Role,
            AddedAt = DateTime.UtcNow,
            AddedById = ctx.Value.userId
        };
        _db.TeleSessionParticipants.Add(participant);
        await _db.SaveChangesAsync();

        // Post a system message so both sides see who joined the thread.
        var name = await _userManager.Users.AsNoTracking().Where(u => u.Id == req.ClinicianId)
            .Select(u => u.FirstName + " " + u.LastName).FirstOrDefaultAsync() ?? "A specialist";
        await _chat.AddMessageAsync(session.Id, ChatSenderRole.System, null, $"Dr {name}{(string.IsNullOrEmpty(req.Role) ? "" : " (" + req.Role + ")")} joined the thread.");
        return Ok(new { id = participant.Id });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> EndChat(int patientId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var session = await _db.TeleSessions.FirstOrDefaultAsync(s => s.PatientId == patientId
            && s.Mode == TeleSessionMode.Chat
            && s.Status != TeleSessionStatus.Completed && s.Status != TeleSessionStatus.Cancelled);
        if (session != null) await _tele.EndSessionAsync(session.Id, null, ctx.Value.userId);
        TempData["Success"] = "Chat thread closed.";
        return RedirectToAction(nameof(Chats));
    }

    /// <summary>Printable sick-note view — opens in a new tab from the room. Patient + employer can print/save.</summary>
    [HttpGet]
    public async Task<IActionResult> SickNote(int certId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var cert = await _db.MedicalCertificates.AsNoTracking()
            .Include(c => c.Patient)
            .Include(c => c.IssuedBy)
            .FirstOrDefaultAsync(c => c.Id == certId && c.FacilityId == ctx.Value.facilityId);
        if (cert is null) return NotFound();
        var facility = await _db.Facilities.AsNoTracking().FirstOrDefaultAsync(f => f.Id == cert.FacilityId);
        ViewBag.Facility = facility;
        return View("SickNote", cert);
    }
}
