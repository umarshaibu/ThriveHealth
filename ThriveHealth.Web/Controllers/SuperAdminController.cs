using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Tenancy;

namespace ThriveHealth.Web.Controllers;

/// <summary>
/// Platform-level operations console — visible only to ThriveHealth staff with the
/// <see cref="Roles.SuperAdmin"/> role. Mounted at <c>/superadmin</c> on the admin host
/// (<c>admin.thrivehealth.ng</c>) where the tenant resolver sets an admin context, so
/// queries here intentionally bypass tenant scoping.
/// </summary>
[Authorize(Roles = Roles.SuperAdmin), Route("superadmin")]
public class SuperAdminController : Controller
{
    private readonly ApplicationDbContext _db;
    public SuperAdminController(ApplicationDbContext db) { _db = db; }

    public record SuperAdminDashboard(
        int TotalTenants,
        int Trialing,
        int Active,
        int PastDue,
        int Suspended,
        int PendingPayments,
        int PendingDomainVerifications,
        int NewThisMonth,
        int NewLastMonth,
        decimal MrrNgn,
        decimal ArrNgn,
        decimal MrrLastMonth,
        IReadOnlyList<MonthlyPoint> SignupTrend,
        IReadOnlyList<PlanShare> PlanShares,
        IReadOnlyList<ActivityItem> RecentActivity,
        IReadOnlyList<Tenant> RecentTenants,
        IReadOnlyList<Tenant> AtRiskTenants
    );

    public record MonthlyPoint(string Label, int Count);
    public record PlanShare(string Plan, int Count, decimal MrrShare);
    public record ActivityItem(DateTime At, string Kind, string Title, string Detail, string Tone);

    [HttpGet("")]
    public async Task<IActionResult> Index()
    {
        // Tenant scoping is bypassed via IgnoreQueryFilters so the dashboard reads every
        // tenant's data regardless of which host the super-admin happens to hit (admin
        // subdomain in prod, or a tenant subdomain via the dev-override cookie).
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var lastMonthStart = monthStart.AddMonths(-1);

        var tenants = await _db.Tenants.AsNoTracking().IgnoreQueryFilters().ToListAsync();
        var subs = await _db.TenantSubscriptions.AsNoTracking().IgnoreQueryFilters()
            .Include(s => s.Plan).ToListAsync();
        var payments = await _db.TenantPayments.AsNoTracking().IgnoreQueryFilters()
            .Include(p => p.Tenant).ToListAsync();

        var activeSubs = subs.Where(s => s.IsActive).ToList();
        var lastMonthSubs = subs.Where(s => s.IsActive && s.StartedAt < monthStart).ToList();

        // ---- KPIs ----
        var trialing = tenants.Count(t => t.Status == TenantStatus.Trialing);
        var active = tenants.Count(t => t.Status == TenantStatus.Active);
        var pastDue = tenants.Count(t => t.Status == TenantStatus.PastDue);
        var suspended = tenants.Count(t => t.Status == TenantStatus.Suspended);
        var pendingPay = payments.Count(p => p.Status == PaymentReceiptStatus.Pending);
        var pendingDomains = tenants.Count(t => !string.IsNullOrEmpty(t.CustomDomain) && t.CustomDomainVerifiedAt is null);
        var newThisMonth = tenants.Count(t => t.CreatedAt >= monthStart);
        var newLastMonth = tenants.Count(t => t.CreatedAt >= lastMonthStart && t.CreatedAt < monthStart);
        var mrr = MonthlyRecurring(activeSubs);
        var mrrLast = MonthlyRecurring(lastMonthSubs);
        var arr = mrr * 12;

        // ---- 6-month signup trend ----
        var signupTrend = Enumerable.Range(0, 6)
            .Select(i => monthStart.AddMonths(-5 + i))
            .Select(m => new MonthlyPoint(
                m.ToString("MMM"),
                tenants.Count(t => t.CreatedAt >= m && t.CreatedAt < m.AddMonths(1))))
            .ToList();

        // ---- Plan distribution (active subs grouped by plan name) ----
        var planShares = activeSubs
            .Where(s => s.Plan != null)
            .GroupBy(s => s.Plan!.Name)
            .Select(g => new PlanShare(
                g.Key,
                g.Count(),
                g.Sum(s => s.Cycle == BillingCycle.Annual ? s.PriceAmount / 12 : s.PriceAmount)))
            .OrderByDescending(p => p.Count)
            .ToList();

        // ---- Activity feed: synthesize from existing data, newest first ----
        var activity = new List<ActivityItem>();
        foreach (var t in tenants.Where(x => x.CreatedAt >= now.AddDays(-30)))
            activity.Add(new ActivityItem(t.CreatedAt, "signup", $"{t.LegalName} signed up",
                $"slug: {t.Slug} · {t.CountryCode}", "info"));
        foreach (var p in payments.Where(x => x.SubmittedAt >= now.AddDays(-30)))
            activity.Add(new ActivityItem(p.SubmittedAt, "payment-submitted",
                $"{p.Tenant?.LegalName} submitted {p.Currency} {p.Amount:N0}",
                $"ref {p.Reference}", "warning"));
        foreach (var p in payments.Where(x => x.ReviewedAt.HasValue && x.ReviewedAt >= now.AddDays(-30)))
            activity.Add(new ActivityItem(p.ReviewedAt!.Value,
                p.Status == PaymentReceiptStatus.Approved ? "payment-approved" : "payment-rejected",
                $"{p.Tenant?.LegalName} payment {p.Status.ToString().ToLower()}",
                p.ReviewNotes ?? string.Empty,
                p.Status == PaymentReceiptStatus.Approved ? "success" : "danger"));
        foreach (var t in tenants.Where(x => x.SuspendedAt.HasValue && x.SuspendedAt >= now.AddDays(-30)))
            activity.Add(new ActivityItem(t.SuspendedAt!.Value, "suspended",
                $"{t.LegalName} suspended", string.Empty, "danger"));
        foreach (var t in tenants.Where(x => x.CustomDomainVerifiedAt.HasValue && x.CustomDomainVerifiedAt >= now.AddDays(-30)))
            activity.Add(new ActivityItem(t.CustomDomainVerifiedAt!.Value, "domain-verified",
                $"{t.LegalName} verified custom domain",
                t.CustomDomain ?? string.Empty, "success"));

        // ---- At-risk tenants: trial ending in <= 7 days, or past-due ----
        var atRisk = tenants
            .Where(t => (t.Status == TenantStatus.Trialing && t.TrialEndsAt.HasValue && t.TrialEndsAt < now.AddDays(7))
                     || t.Status == TenantStatus.PastDue)
            .OrderBy(t => t.TrialEndsAt ?? t.UpdatedAt)
            .Take(8)
            .ToList();

        var vm = new SuperAdminDashboard(
            TotalTenants: tenants.Count,
            Trialing: trialing,
            Active: active,
            PastDue: pastDue,
            Suspended: suspended,
            PendingPayments: pendingPay,
            PendingDomainVerifications: pendingDomains,
            NewThisMonth: newThisMonth,
            NewLastMonth: newLastMonth,
            MrrNgn: mrr,
            ArrNgn: arr,
            MrrLastMonth: mrrLast,
            SignupTrend: signupTrend,
            PlanShares: planShares,
            RecentActivity: activity.OrderByDescending(a => a.At).Take(15).ToList(),
            RecentTenants: tenants.OrderByDescending(t => t.CreatedAt).Take(8).ToList(),
            AtRiskTenants: atRisk);

        return View(vm);
    }

    private static decimal MonthlyRecurring(IEnumerable<TenantSubscription> activeSubs) =>
        activeSubs.Sum(s => s.Cycle == BillingCycle.Annual ? s.PriceAmount / 12 : s.PriceAmount);
}
