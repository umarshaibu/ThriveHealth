using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Tenancy;

namespace ThriveHealth.Web.Controllers;

/// <summary>
/// Platform event log — a unified, chronological view of everything that happened across the
/// estate. Synthesised from existing rows (no new event-store table) so it's always consistent
/// with the underlying data:
///   • tenant signups (<see cref="Tenant.CreatedAt"/>)
///   • bank transfer submissions / approvals / rejections (<see cref="TenantPayment"/>)
///   • status transitions (suspension, reactivation captured via timestamps + status)
///   • custom-domain verifications (<see cref="Tenant.CustomDomainVerifiedAt"/>)
///   • plan changes (subscription <see cref="TenantSubscription.StartedAt"/>)
/// </summary>
[Authorize(Roles = Roles.SuperAdmin), Route("superadmin/activity")]
public class SuperAdminActivityController : Controller
{
    private readonly ApplicationDbContext _db;
    public SuperAdminActivityController(ApplicationDbContext db) { _db = db; }

    public record ActivityRow(DateTime At, string Kind, string Tenant, string? TenantSlug, int? TenantId,
        string Title, string? Detail, string Tone);

    [HttpGet("")]
    public async Task<IActionResult> Index(string? kind, int days = 30)
    {
        days = Math.Clamp(days, 1, 365);
        var cutoff = DateTime.UtcNow.AddDays(-days);

        var tenants = await _db.Tenants.IgnoreQueryFilters().AsNoTracking().ToListAsync();
        var payments = await _db.TenantPayments.IgnoreQueryFilters().AsNoTracking()
            .Include(p => p.Tenant)
            .Where(p => p.SubmittedAt >= cutoff || (p.ReviewedAt.HasValue && p.ReviewedAt >= cutoff))
            .ToListAsync();
        var subs = await _db.TenantSubscriptions.IgnoreQueryFilters().AsNoTracking()
            .Include(s => s.Tenant).Include(s => s.Plan)
            .Where(s => s.StartedAt >= cutoff)
            .ToListAsync();

        var rows = new List<ActivityRow>();

        foreach (var t in tenants.Where(x => x.CreatedAt >= cutoff))
            rows.Add(new(t.CreatedAt, "signup", t.LegalName, t.Slug, t.Id,
                "Tenant signed up", $"slug: {t.Slug} · {t.CountryCode} · {t.OwnerEmail}", "info"));

        foreach (var p in payments.Where(x => x.SubmittedAt >= cutoff))
            rows.Add(new(p.SubmittedAt, "payment-submitted", p.Tenant?.LegalName ?? "—", p.Tenant?.Slug, p.TenantId,
                $"Submitted {p.Currency} {p.Amount:N0} via {p.Method}",
                $"ref {p.Reference} · bank: {p.BankAccountUsed}", "warning"));

        foreach (var p in payments.Where(x => x.ReviewedAt.HasValue && x.ReviewedAt >= cutoff))
        {
            var tone = p.Status == PaymentReceiptStatus.Approved ? "success" : "danger";
            var verb = p.Status == PaymentReceiptStatus.Approved ? "approved" : "rejected";
            rows.Add(new(p.ReviewedAt!.Value, $"payment-{verb}", p.Tenant?.LegalName ?? "—", p.Tenant?.Slug, p.TenantId,
                $"Payment {verb} ({p.Currency} {p.Amount:N0})",
                p.ReviewNotes, tone));
        }

        foreach (var t in tenants.Where(x => x.SuspendedAt.HasValue && x.SuspendedAt >= cutoff))
            rows.Add(new(t.SuspendedAt!.Value, "suspended", t.LegalName, t.Slug, t.Id,
                "Tenant suspended", null, "danger"));

        foreach (var t in tenants.Where(x => x.CustomDomainVerifiedAt.HasValue && x.CustomDomainVerifiedAt >= cutoff))
            rows.Add(new(t.CustomDomainVerifiedAt!.Value, "domain-verified", t.LegalName, t.Slug, t.Id,
                "Custom domain verified", t.CustomDomain, "success"));

        foreach (var s in subs.Where(x => x.StartedAt >= cutoff))
            rows.Add(new(s.StartedAt, "plan-change", s.Tenant?.LegalName ?? "—", s.Tenant?.Slug, s.TenantId,
                $"Subscribed to {s.Plan?.Name} ({s.Cycle})",
                $"{s.PriceCurrency} {s.PriceAmount:N0}", "info"));

        var filtered = string.IsNullOrEmpty(kind) ? rows : rows.Where(r => r.Kind == kind).ToList();
        var ordered = filtered.OrderByDescending(r => r.At).Take(500).ToList();

        ViewBag.Kind = kind;
        ViewBag.Days = days;
        ViewBag.TotalsByKind = rows.GroupBy(r => r.Kind).ToDictionary(g => g.Key, g => g.Count());
        return View(ordered);
    }
}
