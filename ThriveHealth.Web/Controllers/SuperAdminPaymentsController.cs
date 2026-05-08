using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Tenancy;

namespace ThriveHealth.Web.Controllers;

/// <summary>
/// Bank-transfer review queue. Tenants submit a receipt via the tenant-side billing console;
/// super-admins approve or reject here. Approval flips the tenant to Active and rolls the
/// subscription period forward. Mounted at <c>/superadmin/payments</c>.
/// </summary>
[Authorize(Roles = Roles.SuperAdmin), Route("superadmin/payments")]
public class SuperAdminPaymentsController : Controller
{
    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _users;
    public SuperAdminPaymentsController(ApplicationDbContext db, UserManager<ApplicationUser> users)
    { _db = db; _users = users; }

    [HttpGet("")]
    public async Task<IActionResult> Index(PaymentReceiptStatus? status)
    {
        var s = status ?? PaymentReceiptStatus.Pending;
        // Tenant payments are tenant-scoped; bypass the filter here because super-admins
        // need to see receipts from every tenant in the queue.
        var rows = await _db.TenantPayments.AsNoTracking().IgnoreQueryFilters()
            .Include(p => p.Tenant)
            .Include(p => p.Subscription).ThenInclude(s => s!.Plan)
            .Where(p => p.Status == s)
            .OrderByDescending(p => p.SubmittedAt).Take(200).ToListAsync();
        ViewBag.Status = s;
        return View(rows);
    }

    [HttpPost("approve"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Approve(int id, string? notes)
    {
        var payment = await _db.TenantPayments.IgnoreQueryFilters()
            .Include(p => p.Tenant)
            .Include(p => p.Subscription)
            .FirstOrDefaultAsync(p => p.Id == id);
        if (payment is null) return NotFound();
        if (payment.Status != PaymentReceiptStatus.Pending)
        {
            TempData["Error"] = "Already reviewed.";
            return RedirectToAction(nameof(Index));
        }

        payment.Status = PaymentReceiptStatus.Approved;
        payment.ReviewedAt = DateTime.UtcNow;
        payment.ReviewedById = (await _users.GetUserAsync(User))?.Id;
        payment.ReviewNotes = notes;

        if (payment.Tenant is not null)
        {
            payment.Tenant.Status = TenantStatus.Active;
            payment.Tenant.UpdatedAt = DateTime.UtcNow;
        }
        if (payment.Subscription is not null)
        {
            var period = payment.Subscription.Cycle == BillingCycle.Annual
                ? DateTime.UtcNow.AddYears(1)
                : DateTime.UtcNow.AddMonths(1);
            payment.Subscription.CurrentPeriodStart = DateTime.UtcNow;
            payment.Subscription.CurrentPeriodEnd = period;
            payment.Subscription.IsActive = true;
        }
        await _db.SaveChangesAsync();
        TempData["Success"] = "Payment approved — tenant is now active.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("reject"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Reject(int id, string reason)
    {
        var payment = await _db.TenantPayments.IgnoreQueryFilters().FirstOrDefaultAsync(p => p.Id == id);
        if (payment is null) return NotFound();
        if (payment.Status != PaymentReceiptStatus.Pending)
        {
            TempData["Error"] = "Already reviewed.";
            return RedirectToAction(nameof(Index));
        }
        payment.Status = PaymentReceiptStatus.Rejected;
        payment.ReviewedAt = DateTime.UtcNow;
        payment.ReviewedById = (await _users.GetUserAsync(User))?.Id;
        payment.ReviewNotes = reason;
        await _db.SaveChangesAsync();
        TempData["Success"] = "Payment rejected.";
        return RedirectToAction(nameof(Index));
    }
}
