using System.Text;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Inpatient;
using ThriveHealth.Web.Models.Reporting;
using ThriveHealth.Web.Services.Ai;
using Microsoft.AspNetCore.Authorization;
using ThriveHealth.Web.Models.Audit;

namespace ThriveHealth.Web.Controllers;

[Authorize]
public class AiController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IClinicalAiService _ai;

    public AiController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IClinicalAiService ai)
    {
        _db = db; _userManager = userManager; _ai = ai;
    }

    private async Task<int?> FacilityIdAsync()
    {
        var u = await _userManager.GetUserAsync(User);
        return u?.FacilityId;
    }

    [HttpGet, HasPermission(Permissions.AiNlSearch)]
    public IActionResult Search() => View();

    public class AskDto { public string? Question { get; set; } }

    [HttpPost, HasPermission(Permissions.AiNlSearch)]
    public async Task<IActionResult> Ask([FromBody] AskDto dto)
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.Question))
            return Json(new { ok = false, error = "Type a question." });

        var ctx = await BuildContextSummaryAsync(fid.Value, dto.Question, HttpContext.RequestAborted);
        var u = await _userManager.GetUserAsync(User);
        var outcome = await _ai.AskNlSearchAsync(new NlSearchInput(fid.Value, dto.Question, ctx), u?.Id);
        return Json(new { ok = outcome.Ok, text = outcome.Text, error = outcome.Error });
    }

    /// <summary>Pre-aggregate a small slice of useful facts based on the question's keywords.</summary>
    private async Task<string> BuildContextSummaryAsync(int facilityId, string q, CancellationToken ct)
    {
        var sb = new StringBuilder();
        var lq = q.ToLowerInvariant();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekAgo = DateTime.UtcNow.AddDays(-7);

        if (lq.Contains("idsr") || lq.Contains("outbreak") || lq.Contains("disease") || lq.Contains("notifiable"))
        {
            var open = await _db.IdsrCases.AsNoTracking().Where(c => c.FacilityId == facilityId && c.Status == IdsrCaseStatus.Open).CountAsync(ct);
            var weekly = await _db.IdsrCases.AsNoTracking()
                .Include(c => c.NotifiableDisease)
                .Where(c => c.FacilityId == facilityId && c.OnsetDate >= today.AddDays(-7))
                .GroupBy(c => c.NotifiableDisease!.Code)
                .Select(g => new { Code = g.Key, Count = g.Count() })
                .ToListAsync(ct);
            sb.AppendLine($"IDSR open cases (all-time): {open}");
            sb.AppendLine("IDSR last 7 days by disease: " + (weekly.Count == 0 ? "none" : string.Join(", ", weekly.Select(w => $"{w.Code}={w.Count}"))));
        }
        if (lq.Contains("admission") || lq.Contains("admit") || lq.Contains("ward") || lq.Contains("inpatient"))
        {
            var active = await _db.Admissions.AsNoTracking().Where(a => a.FacilityId == facilityId && a.Status == AdmissionStatus.Active).CountAsync(ct);
            var newWk = await _db.Admissions.AsNoTracking().Where(a => a.FacilityId == facilityId && a.AdmittedAt >= weekAgo).CountAsync(ct);
            sb.AppendLine($"Active admissions: {active} · new in last 7d: {newWk}");
        }
        if (lq.Contains("appointment") || lq.Contains("clinic") || lq.Contains("schedule"))
        {
            var todayStart = DateTime.UtcNow.Date;
            var ap = await _db.Appointments.AsNoTracking()
                .Where(a => a.FacilityId == facilityId && a.ScheduledStartUtc >= todayStart && a.ScheduledStartUtc < todayStart.AddDays(1))
                .CountAsync(ct);
            sb.AppendLine($"Appointments today: {ap}");
        }
        if (lq.Contains("expir") || lq.Contains("stock") || lq.Contains("inventory") || lq.Contains("reorder"))
        {
            var soon = today.AddDays(90);
            var expSoon = await _db.InventoryStocks.AsNoTracking()
                .Include(s => s.Store)
                .Where(s => s.Store!.FacilityId == facilityId && s.ExpiryDate.HasValue && s.ExpiryDate >= today && s.ExpiryDate <= soon)
                .CountAsync(ct);
            var lowItems = await _db.InventoryStocks.AsNoTracking()
                .Include(s => s.Store).Include(s => s.InventoryItem)
                .Where(s => s.Store!.FacilityId == facilityId && s.InventoryItem!.ReorderLevel.HasValue && s.QuantityOnHand <= s.InventoryItem.ReorderLevel)
                .Select(s => s.InventoryItem!.Name)
                .Take(10)
                .ToListAsync(ct);
            sb.AppendLine($"Stock expiring within 90 days: {expSoon} batch(es)");
            sb.AppendLine("Low-stock items (top 10): " + (lowItems.Count == 0 ? "none" : string.Join(", ", lowItems)));
        }
        if (lq.Contains("anc") || lq.Contains("matern") || lq.Contains("pregnan"))
        {
            var anc = await _db.AnteNatalRecords.AsNoTracking().Where(a => a.FacilityId == facilityId && a.Status == ThriveHealth.Web.Models.Maternity.AnteNatalStatus.Booked).CountAsync(ct);
            sb.AppendLine($"Active ANC records: {anc}");
        }
        if (lq.Contains("queue") || lq.Contains("waiting"))
        {
            var todayDate = DateOnly.FromDateTime(DateTime.UtcNow);
            var waiting = await _db.QueueEntries.AsNoTracking()
                .Where(e => e.FacilityId == facilityId && e.TicketDate == todayDate
                    && (e.Status == ThriveHealth.Web.Models.Scheduling.QueueStatus.Waiting
                        || e.Status == ThriveHealth.Web.Models.Scheduling.QueueStatus.Triaged))
                .CountAsync(ct);
            sb.AppendLine($"Patients currently waiting: {waiting}");
        }
        if (sb.Length == 0)
            sb.AppendLine("(no pre-aggregated context for this question; answer may be limited)");

        return sb.ToString();
    }

    // ---- Translate ----
    [HttpGet, HasPermission(Permissions.AiTranslate)]
    public IActionResult Translate() => View();

    public class TranslateDto { public string? Source { get; set; } public string? Target { get; set; } public string? Text { get; set; } public string? Context { get; set; } }

    [HttpPost, HasPermission(Permissions.AiTranslate)]
    public async Task<IActionResult> TranslateRun([FromBody] TranslateDto dto)
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.Text)) return Json(new { ok = false, error = "Enter text to translate." });
        var u = await _userManager.GetUserAsync(User);
        var input = new TranslateInput(fid.Value, dto.Source ?? "English", dto.Target ?? "Hausa", dto.Text!, dto.Context);
        var outcome = await _ai.TranslateAsync(input, u?.Id);
        return Json(new { ok = outcome.Ok, text = outcome.Text, error = outcome.Error });
    }

    // ---- Adherence parse ----
    [HttpGet, HasPermission(Permissions.AiAdherenceParse)]
    public IActionResult AdherenceParse() => View();

    public class AdherenceDto { public string? DrugName { get; set; } public string? SmsReply { get; set; } public string? PatientAgeSex { get; set; } }

    [HttpPost, HasPermission(Permissions.AiAdherenceParse)]
    public async Task<IActionResult> AdherenceParseRun([FromBody] AdherenceDto dto)
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return Unauthorized();
        if (string.IsNullOrWhiteSpace(dto.DrugName) || string.IsNullOrWhiteSpace(dto.SmsReply))
            return Json(new { ok = false, error = "Drug name and SMS reply are required." });
        var u = await _userManager.GetUserAsync(User);
        var outcome = await _ai.ParseAdherenceAsync(new AdherenceParseInput(fid.Value, dto.PatientAgeSex, dto.DrugName!, dto.SmsReply!), u?.Id);
        return Json(new { ok = outcome.Ok, text = outcome.Text, error = outcome.Error });
    }

    // ---- Audit anomaly ----
    [HttpPost, HasPermission(Permissions.AiAuditAnomaly)]
    public async Task<IActionResult> AuditAnomalyRun(int hours = 24)
    {
        var fid = await FacilityIdAsync();
        if (fid is null) return Unauthorized();
        hours = Math.Clamp(hours, 1, 168);
        var since = DateTime.UtcNow.AddHours(-hours);

        var entries = await _db.AuditEntries.AsNoTracking()
            .Where(a => a.FacilityId == fid && a.AtUtc >= since)
            .OrderByDescending(a => a.AtUtc)
            .Take(300)
            .Select(a => new AuditLogSnapshot(
                a.ActorName ?? a.ActorUserId ?? "unknown",
                a.Action,
                a.EntityType, a.EntityKey,
                a.AtUtc, a.IpAddress,
                a.Outcome.ToString()))
            .ToListAsync();

        if (entries.Count == 0)
            return Json(new { ok = true, text = "No audit entries in the last " + hours + " hours.", error = (string?)null });

        var u = await _userManager.GetUserAsync(User);
        var outcome = await _ai.DetectAuditAnomalyAsync(new AuditAnomalyInput(fid.Value, hours, entries), u?.Id);
        return Json(new { ok = outcome.Ok, text = outcome.Text, error = outcome.Error });
    }
}
