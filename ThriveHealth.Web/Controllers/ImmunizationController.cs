using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Auth;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Immunization;
using ThriveHealth.Web.Models.ViewModels;
using ThriveHealth.Web.Services;

namespace ThriveHealth.Web.Controllers;

[HasPermission(Permissions.ImmunizationAdminister)]
public class ImmunizationController : Controller
{
    public const string ImmStaff = Roles.Doctor + "," + Roles.Consultant + "," + Roles.MedicalOfficer + "," +
        Roles.Nurse + "," + Roles.Midwife + "," + Roles.ChiefNursingOfficer + "," +
        Roles.SystemAdministrator + "," + Roles.MedicalDirector;

    private readonly ApplicationDbContext _db;
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly IImmunizationService _imm;

    public ImmunizationController(ApplicationDbContext db, UserManager<ApplicationUser> userManager, IImmunizationService imm)
    {
        _db = db; _userManager = userManager; _imm = imm;
    }

    private async Task<(int facilityId, string userId)?> Ctx()
    {
        var u = await _userManager.GetUserAsync(User);
        if (u?.FacilityId is null) return null;
        return (u.FacilityId.Value, u.Id);
    }

    [HttpGet]
    public async Task<IActionResult> Worklist()
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();

        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var weekAhead = today.AddDays(7);
        var weekBack = today.AddDays(-30);

        var doses = await _db.ImmunizationDoses.AsNoTracking()
            .Include(d => d.Patient)
            .Include(d => d.Vaccine)
            .Where(d => d.FacilityId == ctx.Value.facilityId
                     && d.Status == DoseStatus.Due
                     && d.DueDate >= weekBack && d.DueDate <= weekAhead)
            .OrderBy(d => d.DueDate)
            .Take(300)
            .ToListAsync();

        var rows = doses.Select(d => new ImmunizationWorklistRow
        {
            Dose = d,
            Patient = d.Patient!,
            DaysOverdue = today.DayNumber - d.DueDate.DayNumber
        }).ToList();

        var todayUtc = DateTime.UtcNow.Date;
        var administeredToday = await _db.ImmunizationDoses.AsNoTracking()
            .CountAsync(d => d.FacilityId == ctx.Value.facilityId
                          && d.Status == DoseStatus.Administered
                          && d.AdministeredAt >= todayUtc);

        return View(new ImmunizationWorklistViewModel
        {
            Rows = rows,
            DueTodayCount = rows.Count(r => r.Dose.DueDate == today),
            OverdueCount = rows.Count(r => r.DaysOverdue > 0),
            AdministeredTodayCount = administeredToday
        });
    }

    [HttpGet]
    public async Task<IActionResult> Card(int patientId)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        var patient = await _db.Patients.AsNoTracking()
            .FirstOrDefaultAsync(p => p.Id == patientId && p.FacilityId == ctx.Value.facilityId);
        if (patient is null) return NotFound();
        if (patient.DateOfBirth is null)
        {
            TempData["Error"] = "Patient has no date of birth recorded — cannot compute schedule.";
            return RedirectToAction("Profile", "Patients", new { id = patientId });
        }
        var dob = patient.DateOfBirth.Value;

        await _imm.EnsureScheduleForPatientAsync(ctx.Value.facilityId, patientId, dob);
        var rows = await _imm.GetCardAsync(patientId, dob);

        return View(new ImmunizationCardViewModel
        {
            Patient = patient,
            DateOfBirth = dob,
            Rows = rows,
            AdministeredCount = rows.Count(r => r.Status == DoseStatus.Administered),
            DueOrOverdueCount = rows.Count(r => r.Status == DoseStatus.Due)
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Administer(AdministerDoseViewModel m)
    {
        var ctx = await Ctx();
        if (ctx is null) return NotFound();
        try
        {
            await _imm.AdministerAsync(m.DoseId, m.BatchNumber, m.ExpiryDate, m.Site, m.Notes, ctx.Value.userId);
            TempData["Success"] = "Dose administered.";
        }
        catch (InvalidOperationException ex)
        {
            TempData["Error"] = ex.Message;
        }
        return RedirectToAction(nameof(Card), new { patientId = m.PatientId });
    }
}
