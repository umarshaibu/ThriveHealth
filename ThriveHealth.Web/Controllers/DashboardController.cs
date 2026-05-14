using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.ViewModels;

namespace ThriveHealth.Web.Controllers;

[Authorize]
public class DashboardController : Controller
{
    private readonly UserManager<ApplicationUser> _userManager;
    private readonly ApplicationDbContext _db;

    public DashboardController(UserManager<ApplicationUser> userManager, ApplicationDbContext db)
    {
        _userManager = userManager;
        _db = db;
    }

    /// <summary>Persona = the dashboard a user sees. Multiple roles can share a persona.</summary>
    private enum Persona { Executive, Clinician, Nursing, FrontOffice, Lab, Imaging, Pharmacy, Finance, Claims, Hr, Procurement, PublicHealth, Specialty, Default }

    /// <summary>Highest-priority persona wins when a user has several roles.</summary>
    private static Persona PersonaForRoles(IList<string> roles)
    {
        if (roles.Contains(Roles.SystemAdministrator) || roles.Contains(Roles.MedicalDirector)
            || roles.Contains(Roles.ChiefExecutive)) return Persona.Executive;
        if (roles.Contains(Roles.ChiefFinancialOfficer)) return Persona.Finance;
        if (roles.Contains(Roles.ChiefNursingOfficer)) return Persona.Nursing;
        if (roles.Contains(Roles.HrOfficer)) return Persona.Hr;
        if (roles.Contains(Roles.Consultant) || roles.Contains(Roles.Doctor) || roles.Contains(Roles.MedicalOfficer)) return Persona.Clinician;
        if (roles.Contains(Roles.Nurse) || roles.Contains(Roles.Midwife)) return Persona.Nursing;
        if (roles.Contains(Roles.LabScientist) || roles.Contains(Roles.LabTechnician)) return Persona.Lab;
        if (roles.Contains(Roles.Radiographer)) return Persona.Imaging;
        if (roles.Contains(Roles.Pharmacist) || roles.Contains(Roles.PharmacyTechnician)) return Persona.Pharmacy;
        if (roles.Contains(Roles.Cashier) || roles.Contains(Roles.Accountant)) return Persona.Finance;
        if (roles.Contains(Roles.ClaimsOfficer)) return Persona.Claims;
        if (roles.Contains(Roles.StoreOfficer) || roles.Contains(Roles.ProcurementOfficer)) return Persona.Procurement;
        if (roles.Contains(Roles.PublicHealthOfficer)) return Persona.PublicHealth;
        if (roles.Contains(Roles.Receptionist) || roles.Contains(Roles.RecordsOfficer) || roles.Contains(Roles.TriageClerk)) return Persona.FrontOffice;
        if (roles.Contains(Roles.Physiotherapist)) return Persona.Specialty;
        return Persona.Default;
    }

    private static string PersonaLabel(Persona p) => p switch
    {
        Persona.Executive => "Executive overview",
        Persona.Clinician => "Clinician dashboard",
        Persona.Nursing => "Nursing dashboard",
        Persona.FrontOffice => "Front-desk dashboard",
        Persona.Lab => "Laboratory dashboard",
        Persona.Imaging => "Imaging dashboard",
        Persona.Pharmacy => "Pharmacy dashboard",
        Persona.Finance => "Finance dashboard",
        Persona.Claims => "Claims dashboard",
        Persona.Hr => "HR dashboard",
        Persona.Procurement => "Procurement dashboard",
        Persona.PublicHealth => "Public-health dashboard",
        Persona.Specialty => "Specialty services",
        _ => "Dashboard"
    };

    public async Task<IActionResult> Index()
    {
        var user = await _userManager.GetUserAsync(User);
        if (user is null) return Challenge();
        var fid = user.FacilityId;
        var roles = (await _userManager.GetRolesAsync(user)).ToList();
        var persona = PersonaForRoles(roles);
        var primary = roles.FirstOrDefault() ?? "Staff";

        var facility = fid.HasValue ? await _db.Facilities.AsNoTracking().FirstOrDefaultAsync(f => f.Id == fid.Value) : null;

        var vm = new DashboardViewModel
        {
            FacilityName = facility?.Name ?? "ThriveHealth",
            GreetingName = user.FirstName,
            PrimaryRole = primary,
            Persona = PersonaLabel(persona),
            PatientCount = fid.HasValue ? await _db.Patients.AsNoTracking().CountAsync(p => p.FacilityId == fid && !p.IsMergedAlias) : 0,
        };

        if (!fid.HasValue) { vm.ActiveAlerts.Add("Your account isn't associated with a facility yet — contact an administrator."); return View(vm); }

        vm.Stats = await BuildStatsAsync(persona, fid.Value, user.Id);
        vm.NextActions = BuildNextActions(persona);
        vm.ActiveAlerts = await BuildAlertsAsync(persona, fid.Value);
        vm.RecentActivity = await BuildRecentActivityAsync(persona, fid.Value, user.Id);

        // Persona-specific working surface (real lists of items to action, not just count tiles).
        // BoardKey selects the partial in Views/Dashboard/Partials/_Board{Key}.cshtml; the
        // strongly-typed payload lives on Board and is cast in the view.
        (vm.BoardKey, vm.Board) = await BuildBoardAsync(persona, fid.Value, user.Id);

        return View(vm);
    }

    /// <summary>
    /// Builds the strongly-typed per-persona payload that the role-specific partial renders.
    /// Returns (null, null) when no specialised partial exists — the view falls back to the
    /// generic Stats + Activity + Quick-actions shell.
    /// </summary>
    private async Task<(string?, object?)> BuildBoardAsync(Persona p, int fid, string userId) => p switch
    {
        Persona.Clinician   => ("Clinician",   await BuildClinicianBoardAsync(fid, userId)),
        Persona.Nursing     => ("Nursing",     await BuildNursingBoardAsync(fid)),
        Persona.Pharmacy    => ("Pharmacy",    await BuildPharmacyBoardAsync(fid)),
        Persona.Lab         => ("Lab",         await BuildLabBoardAsync(fid)),
        Persona.FrontOffice => ("FrontOffice", await BuildFrontOfficeBoardAsync(fid)),
        Persona.Finance     => ("Finance",     await BuildFinanceBoardAsync(fid)),
        Persona.Executive   => ("Executive",   await BuildExecutiveBoardAsync(fid)),
        _ => (null, null)
    };

    private async Task<ClinicianBoardVm> BuildClinicianBoardAsync(int fid, string userId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayStart = DateTime.UtcNow.Date;
        var tomorrow = todayStart.AddDays(1);
        var now = DateTime.UtcNow;

        var waiting = await _db.QueueEntries.AsNoTracking()
            .Include(q => q.Patient)
            .Where(q => q.FacilityId == fid && q.TicketDate == today && q.ClinicianId == userId
                && (q.Status == ThriveHealth.Web.Models.Scheduling.QueueStatus.Waiting
                 || q.Status == ThriveHealth.Web.Models.Scheduling.QueueStatus.InConsultation
                 || q.Status == ThriveHealth.Web.Models.Scheduling.QueueStatus.Called))
            .OrderByDescending(q => q.Priority).ThenBy(q => q.CheckedInAt)
            .Take(20).ToListAsync();

        var appts = await _db.Appointments.AsNoTracking()
            .Include(a => a.Patient).Include(a => a.Clinic)
            .Where(a => a.FacilityId == fid && a.ClinicianId == userId
                && a.ScheduledStartUtc >= todayStart && a.ScheduledStartUtc < tomorrow)
            .OrderBy(a => a.ScheduledStartUtc).Take(20).ToListAsync();

        var labs = await _db.LabResults.AsNoTracking()
            .Include(r => r.LabOrder).ThenInclude(o => o!.Patient)
            .Include(r => r.LabOrder).ThenInclude(o => o!.LabTest)
            .Where(r => r.LabOrder!.Patient!.FacilityId == fid
                && r.LabOrder.OrderedById == userId
                && r.Status == ThriveHealth.Web.Models.Diagnostics.LabResultStatus.Authorized
                && r.LabOrder.Status != ThriveHealth.Web.Models.Clinical.OrderStatus.Completed)
            .OrderByDescending(r => r.AuthorizedAt).Take(10).ToListAsync();

        var recent = await _db.Encounters.AsNoTracking()
            .Include(e => e.Patient)
            .Include(e => e.Diagnoses)
            .Where(e => e.FacilityId == fid && e.ClinicianId == userId
                && e.Status == ThriveHealth.Web.Models.Clinical.EncounterStatus.Signed)
            .OrderByDescending(e => e.SignedAt).Take(5).ToListAsync();

        return new ClinicianBoardVm
        {
            WaitingList = waiting.Select(q => new WaitingPatient(
                q.Id, q.PatientId,
                q.TicketNumber ?? "—",
                q.Patient is null ? "Unknown" : $"{q.Patient.FirstName} {q.Patient.LastName}".Trim(),
                q.TriageNotes,
                q.Priority.ToString(),
                (int)Math.Max(0, (now - q.CheckedInAt).TotalMinutes))).ToList(),

            TodaysAppointments = appts.Select(a => new TodayAppointment(
                a.Id, a.PatientId,
                a.Patient is null ? "Unknown" : $"{a.Patient.FirstName} {a.Patient.LastName}".Trim(),
                a.ScheduledStartUtc, a.Clinic?.Name, a.Status.ToString())).ToList(),

            LabsToReview = labs.Select(r => new LabReviewItem(
                r.LabOrderId, r.LabOrder!.PatientId,
                r.LabOrder.Patient is null ? "Unknown" : $"{r.LabOrder.Patient.FirstName} {r.LabOrder.Patient.LastName}".Trim(),
                r.LabOrder.LabTest?.Name ?? "Test",
                r.HasCriticalValue, r.AuthorizedAt ?? r.EnteredAt)).ToList(),

            RecentConsultations = recent.Select(e => new RecentConsult(
                e.Id, e.PatientId,
                e.Patient is null ? "Unknown" : $"{e.Patient.FirstName} {e.Patient.LastName}".Trim(),
                e.Diagnoses.FirstOrDefault()?.Description ?? e.Diagnoses.FirstOrDefault()?.IcdCode,
                e.SignedAt ?? e.StartedAt)).ToList()
        };
    }

    private async Task<NursingBoardVm> BuildNursingBoardAsync(int fid)
    {
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);

        var mar = await _db.MarSlots.AsNoTracking()
            .Include(s => s.InpatientMedication).ThenInclude(m => m!.Admission).ThenInclude(a => a!.Patient)
            .Include(s => s.InpatientMedication).ThenInclude(m => m!.Admission).ThenInclude(a => a!.Bed)
            .Where(s => s.InpatientMedication!.Admission!.FacilityId == fid
                && s.Status == ThriveHealth.Web.Models.Inpatient.MarSlotStatus.Scheduled
                && s.ScheduledUtc <= now.AddHours(2))
            .OrderBy(s => s.ScheduledUtc).Take(20).ToListAsync();

        var triage = await _db.Encounters.AsNoTracking()
            .Include(e => e.Patient)
            .Where(e => e.FacilityId == fid
                && e.Type == ThriveHealth.Web.Models.Clinical.EncounterType.Emergency
                && e.Status == ThriveHealth.Web.Models.Clinical.EncounterStatus.InProgress
                && e.ResusBayId == null)
            .OrderBy(e => e.StartedAt).Take(15).ToListAsync();

        var wards = await _db.Wards.AsNoTracking()
            .Where(w => w.FacilityId == fid)
            .Select(w => new
            {
                w.Id, w.Name,
                Total = w.Beds!.Count(),
                Occupied = w.Beds!.Count(b => b.Status == ThriveHealth.Web.Models.Inpatient.BedStatus.Occupied)
            }).ToListAsync();

        var vitalsDue = await _db.Admissions.AsNoTracking()
            .Include(a => a.Patient).Include(a => a.Bed)
            .Where(a => a.FacilityId == fid && a.Status == ThriveHealth.Web.Models.Inpatient.AdmissionStatus.Active)
            .Select(a => new
            {
                a.Id, a.PatientId,
                Patient = a.Patient,
                Bed = a.Bed,
                LastVitalsAt = _db.Vitals.Where(v => v.PatientId == a.PatientId)
                    .OrderByDescending(v => v.RecordedAt).Select(v => (DateTime?)v.RecordedAt).FirstOrDefault()
            })
            .Where(x => x.LastVitalsAt == null || x.LastVitalsAt < now.AddHours(-4))
            .Take(15).ToListAsync();

        return new NursingBoardVm
        {
            MedicationDue = mar.Select(s => new DueMedication(
                s.Id, s.InpatientMedication!.AdmissionId,
                s.InpatientMedication.Admission!.PatientId,
                s.InpatientMedication.Admission.Patient is null ? "Unknown"
                    : $"{s.InpatientMedication.Admission.Patient.FirstName} {s.InpatientMedication.Admission.Patient.LastName}".Trim(),
                s.InpatientMedication.Admission.Bed?.BedNumber ?? "—",
                s.InpatientMedication.DrugName, s.InpatientMedication.Dose,
                s.InpatientMedication.Route,
                s.ScheduledUtc, s.ScheduledUtc < now)).ToList(),

            TriageQueue = triage.Select(e => new TriagePatient(
                e.Id, e.PatientId,
                e.Patient is null ? "Unknown" : $"{e.Patient.FirstName} {e.Patient.LastName}".Trim(),
                e.ChiefComplaint, e.Triage == null ? "None" : e.Triage.Colour.ToString(), e.StartedAt)).ToList(),

            Wards = wards.Select(w => new WardSnapshot(w.Id, w.Name, w.Total, w.Occupied, w.Total - w.Occupied)).ToList(),

            VitalsDue = vitalsDue.Select(x => new VitalsDuePatient(
                x.Id, x.PatientId,
                x.Patient is null ? "Unknown" : $"{x.Patient.FirstName} {x.Patient.LastName}".Trim(),
                x.Bed?.BedNumber ?? "—",
                x.LastVitalsAt.HasValue ? (now - x.LastVitalsAt.Value).TotalHours : 999)).ToList()
        };
    }

    private async Task<PharmacyBoardVm> BuildPharmacyBoardAsync(int fid)
    {
        var todayStart = DateTime.UtcNow.Date;

        var pending = await _db.Prescriptions.AsNoTracking()
            .Include(p => p.Patient).Include(p => p.Items).Include(p => p.PrescribedBy)
            .Where(p => p.Patient!.FacilityId == fid
                && p.Status != ThriveHealth.Web.Models.Clinical.PrescriptionStatus.Dispensed
                && p.Status != ThriveHealth.Web.Models.Clinical.PrescriptionStatus.Cancelled)
            .OrderBy(p => p.IssuedAt).Take(20).ToListAsync();

        var lowStock = await _db.DrugStocks.AsNoTracking()
            .Include(s => s.Drug).Include(s => s.Store)
            .Where(s => s.Store!.FacilityId == fid && s.Drug != null
                && s.QuantityOnHand <= (s.Drug.ReorderLevel ?? 0))
            .OrderBy(s => s.QuantityOnHand).Take(15).ToListAsync();

        var dispenses = await _db.Dispenses.AsNoTracking()
            .Include(d => d.Patient).Include(d => d.Items)
            .Where(d => d.FacilityId == fid && d.DispensedAt >= todayStart)
            .OrderByDescending(d => d.DispensedAt).Take(10).ToListAsync();

        return new PharmacyBoardVm
        {
            PendingPrescriptions = pending.Select(p => new PendingPrescription(
                p.Id, p.PatientId,
                p.Patient is null ? "Unknown" : $"{p.Patient.FirstName} {p.Patient.LastName}".Trim(),
                p.Items.Count, p.PrescribedBy is null ? null : $"{p.PrescribedBy.FirstName} {p.PrescribedBy.LastName}".Trim(),
                p.IssuedAt)).ToList(),

            LowStock = lowStock.Select(s => new LowStockDrug(
                s.Id, s.Drug == null ? "Unknown" : s.Drug.Display, s.Store?.Name ?? "—",
                s.QuantityOnHand, s.Drug?.ReorderLevel ?? 0)).ToList(),

            TodaysDispenses = dispenses.Select(d => new TodayDispense(
                d.Id,
                d.Patient is null ? "Unknown" : $"{d.Patient.FirstName} {d.Patient.LastName}".Trim(),
                d.Items.Count, d.DispensedAt)).ToList()
        };
    }

    private async Task<LabBoardVm> BuildLabBoardAsync(int fid)
    {
        var open = await _db.LabOrders.AsNoTracking()
            .Include(o => o.Patient).Include(o => o.LabTest)
            .Where(o => o.Patient!.FacilityId == fid
                && o.Status != ThriveHealth.Web.Models.Clinical.OrderStatus.Completed
                && o.Status != ThriveHealth.Web.Models.Clinical.OrderStatus.Cancelled)
            .OrderBy(o => o.OrderedAt).Take(15).ToListAsync();

        var auth = await _db.LabResults.AsNoTracking()
            .Include(r => r.LabOrder).ThenInclude(o => o!.Patient)
            .Include(r => r.LabOrder).ThenInclude(o => o!.LabTest)
            .Where(r => r.LabOrder!.Patient!.FacilityId == fid
                && r.Status == ThriveHealth.Web.Models.Diagnostics.LabResultStatus.Final)
            .OrderBy(r => r.AuthorizedAt).Take(15).ToListAsync();

        var critical = await _db.LabResults.AsNoTracking()
            .Include(r => r.LabOrder).ThenInclude(o => o!.Patient)
            .Include(r => r.LabOrder).ThenInclude(o => o!.LabTest)
            .Where(r => r.LabOrder!.Patient!.FacilityId == fid && r.HasCriticalValue && !r.CriticalNotified)
            .OrderByDescending(r => r.AuthorizedAt).Take(15).ToListAsync();

        OpenLabSample MapOpen(ThriveHealth.Web.Models.Clinical.LabOrder o) => new(
            o.Id, o.PatientId,
            o.Patient is null ? "Unknown" : $"{o.Patient.FirstName} {o.Patient.LastName}".Trim(),
            o.LabTest?.Name ?? "—", o.OrderedAt, o.Status.ToString());

        return new LabBoardVm
        {
            OpenSamples = open.Select(MapOpen).ToList(),
            AwaitingAuthorisation = auth.Select(r => MapOpen(r.LabOrder!)).ToList(),
            CriticalUnnotified = critical.Select(r => new CriticalValue(
                r.Id, r.LabOrderId, r.LabOrder!.PatientId,
                r.LabOrder.Patient is null ? "Unknown" : $"{r.LabOrder.Patient.FirstName} {r.LabOrder.Patient.LastName}".Trim(),
                r.LabOrder.LabTest?.Name ?? "—",
                r.GeneralComment ?? "see report",
                r.AuthorizedAt ?? r.EnteredAt)).ToList()
        };
    }

    private async Task<FrontOfficeBoardVm> BuildFrontOfficeBoardAsync(int fid)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayStart = DateTime.UtcNow.Date;
        var tomorrow = todayStart.AddDays(1);

        var queueByClinic = await _db.Clinics.AsNoTracking()
            .Where(c => c.FacilityId == fid)
            .Select(c => new
            {
                c.Id, c.Name,
                Waiting = _db.QueueEntries.Count(q => q.ClinicId == c.Id && q.TicketDate == today
                    && q.Status == ThriveHealth.Web.Models.Scheduling.QueueStatus.Waiting),
                InRoom = _db.QueueEntries.Count(q => q.ClinicId == c.Id && q.TicketDate == today
                    && q.Status == ThriveHealth.Web.Models.Scheduling.QueueStatus.InConsultation),
                Done = _db.QueueEntries.Count(q => q.ClinicId == c.Id && q.TicketDate == today
                    && q.Status == ThriveHealth.Web.Models.Scheduling.QueueStatus.Completed)
            }).ToListAsync();

        var appts = await _db.Appointments.AsNoTracking()
            .Include(a => a.Patient).Include(a => a.Clinic)
            .Where(a => a.FacilityId == fid && a.ScheduledStartUtc >= todayStart && a.ScheduledStartUtc < tomorrow)
            .OrderBy(a => a.ScheduledStartUtc).Take(15).ToListAsync();

        var newPatients = await _db.Patients.AsNoTracking()
            .Where(p => p.FacilityId == fid && p.CreatedAt >= todayStart && !p.IsMergedAlias)
            .OrderByDescending(p => p.CreatedAt).Take(10).ToListAsync();

        return new FrontOfficeBoardVm
        {
            QueueByClinic = queueByClinic.Select(c => new ClinicQueueCount(c.Id, c.Name, c.Waiting, c.InRoom, c.Done)).ToList(),
            TodaysAppointments = appts.Select(a => new TodayAppointment(
                a.Id, a.PatientId,
                a.Patient is null ? "Unknown" : $"{a.Patient.FirstName} {a.Patient.LastName}".Trim(),
                a.ScheduledStartUtc, a.Clinic?.Name, a.Status.ToString())).ToList(),
            NewRegistrations = newPatients.Select(p => new TodayRegistration(
                p.Id, $"{p.FirstName} {p.LastName}".Trim(), p.CreatedAt, p.NinVerified)).ToList()
        };
    }

    private async Task<FinanceBoardVm> BuildFinanceBoardAsync(int fid)
    {
        var todayStart = DateTime.UtcNow.Date;

        var openBills = await _db.Bills.AsNoTracking()
            .Include(b => b.Patient)
            .Where(b => b.FacilityId == fid
                && b.Status != ThriveHealth.Web.Models.Billing.BillStatus.Paid
                && b.Status != ThriveHealth.Web.Models.Billing.BillStatus.Cancelled)
            .OrderByDescending(b => b.CreatedAt).Take(20).ToListAsync();

        var payments = await _db.Payments.AsNoTracking()
            .Include(p => p.Bill).ThenInclude(b => b!.Patient)
            .Where(p => p.Bill!.FacilityId == fid
                && p.Status == ThriveHealth.Web.Models.Billing.PaymentStatus.Recorded
                && p.ReceivedAt >= todayStart)
            .OrderByDescending(p => p.ReceivedAt).ToListAsync();

        return new FinanceBoardVm
        {
            OpenBills = openBills.Select(b => new OpenBill(
                b.Id, b.PatientId,
                b.Patient is null ? "Unknown" : $"{b.Patient.FirstName} {b.Patient.LastName}".Trim(),
                b.GrossAmount, b.Balance, b.CreatedAt)).ToList(),
            CollectedToday = payments.Sum(p => p.Amount),
            CollectedByMethod = payments.GroupBy(p => p.Method.ToString()).ToDictionary(g => g.Key, g => g.Sum(p => p.Amount)),
            RecentPayments = payments.Take(10).Select(p => new RecentPayment(
                p.Id,
                p.Bill?.Patient is null ? "Unknown" : $"{p.Bill.Patient.FirstName} {p.Bill.Patient.LastName}".Trim(),
                p.Amount, p.Method.ToString(), p.ReceivedAt)).ToList()
        };
    }

    private async Task<ExecutiveBoardVm> BuildExecutiveBoardAsync(int fid)
    {
        var todayStart = DateTime.UtcNow.Date;
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1, 0, 0, 0, DateTimeKind.Utc);

        var revenueToday = await _db.Payments.AsNoTracking()
            .Where(p => p.Bill!.FacilityId == fid
                && p.Status == ThriveHealth.Web.Models.Billing.PaymentStatus.Recorded
                && p.ReceivedAt >= todayStart)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;
        var revenueMonth = await _db.Payments.AsNoTracking()
            .Where(p => p.Bill!.FacilityId == fid
                && p.Status == ThriveHealth.Web.Models.Billing.PaymentStatus.Recorded
                && p.ReceivedAt >= monthStart)
            .SumAsync(p => (decimal?)p.Amount) ?? 0m;

        var visitsToday = await _db.Encounters.AsNoTracking()
            .CountAsync(e => e.FacilityId == fid && e.StartedAt >= todayStart);
        var visitsMonth = await _db.Encounters.AsNoTracking()
            .CountAsync(e => e.FacilityId == fid && e.StartedAt >= monthStart);

        var activeAdmissions = await _db.Admissions.AsNoTracking()
            .CountAsync(a => a.FacilityId == fid && a.Status == ThriveHealth.Web.Models.Inpatient.AdmissionStatus.Active);

        var ae = await _db.Encounters.AsNoTracking()
            .CountAsync(e => e.FacilityId == fid && e.Type == ThriveHealth.Web.Models.Clinical.EncounterType.Emergency
                && e.Status == ThriveHealth.Web.Models.Clinical.EncounterStatus.InProgress);

        var bedsTotal = await _db.Beds.AsNoTracking().CountAsync(b => b.Ward!.FacilityId == fid);
        var bedsOcc = await _db.Beds.AsNoTracking()
            .CountAsync(b => b.Ward!.FacilityId == fid && b.Status == ThriveHealth.Web.Models.Inpatient.BedStatus.Occupied);

        var recentAdm = await _db.Admissions.AsNoTracking()
            .Include(a => a.Patient).Include(a => a.Ward).Include(a => a.Bed)
            .Where(a => a.FacilityId == fid && a.Status == ThriveHealth.Web.Models.Inpatient.AdmissionStatus.Active)
            .OrderByDescending(a => a.AdmittedAt).Take(8).ToListAsync();

        return new ExecutiveBoardVm
        {
            RevenueToday = revenueToday, RevenueMonth = revenueMonth,
            VisitsToday = visitsToday, VisitsMonth = visitsMonth,
            ActiveAdmissions = activeAdmissions, EmergencyInProgress = ae,
            BedsTotal = bedsTotal, BedsOccupied = bedsOcc,
            RecentAdmissions = recentAdm.Select(a => new AdmissionTile(
                a.Id, a.PatientId,
                a.Patient is null ? "Unknown" : $"{a.Patient.FirstName} {a.Patient.LastName}".Trim(),
                a.Ward?.Name ?? "—", a.Bed?.BedNumber ?? "—", a.AdmittedAt)).ToList()
        };
    }

    // ====================================================================================
    // Stats — one query bundle per persona, real numbers from the DB
    // ====================================================================================
    private async Task<List<DashboardStat>> BuildStatsAsync(Persona p, int fid, string userId)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var todayStart = DateTime.UtcNow.Date;
        var monthStart = new DateTime(DateTime.UtcNow.Year, DateTime.UtcNow.Month, 1);
        var soon = today.AddDays(90);

        DashboardStat S(string label, object value, string icon, string tone)
            => new() { Label = label, Value = value?.ToString() ?? "0", Icon = icon, Tone = tone };

        switch (p)
        {
            case Persona.Executive:
            {
                var patients = await _db.Patients.AsNoTracking().CountAsync(x => x.FacilityId == fid && !x.IsMergedAlias);
                var admissions = await _db.Admissions.AsNoTracking().CountAsync(a => a.FacilityId == fid && a.Status == ThriveHealth.Web.Models.Inpatient.AdmissionStatus.Active);
                var ae = await _db.Encounters.AsNoTracking().CountAsync(e => e.FacilityId == fid && e.Type == ThriveHealth.Web.Models.Clinical.EncounterType.Emergency && e.Status == ThriveHealth.Web.Models.Clinical.EncounterStatus.InProgress);
                var revenueToday = await _db.Payments.AsNoTracking()
                    .Where(p2 => p2.Bill!.FacilityId == fid && p2.Status == ThriveHealth.Web.Models.Billing.PaymentStatus.Recorded && p2.ReceivedAt >= todayStart)
                    .SumAsync(p2 => (decimal?)p2.Amount) ?? 0m;
                return new()
                {
                    S("Total patients", patients.ToString("N0"), "bi-people", "primary"),
                    S("Active admissions", admissions, "bi-hospital", "info"),
                    S("A&E in progress", ae, "bi-bandaid", "warning"),
                    S("Revenue today (₦)", revenueToday.ToString("N0"), "bi-cash-coin", "success")
                };
            }

            case Persona.Clinician:
            {
                var myQueueToday = await _db.QueueEntries.AsNoTracking()
                    .CountAsync(q => q.FacilityId == fid && q.TicketDate == today && q.ClinicianId == userId
                        && q.Status != ThriveHealth.Web.Models.Scheduling.QueueStatus.Completed
                        && q.Status != ThriveHealth.Web.Models.Scheduling.QueueStatus.Skipped
                        && q.Status != ThriveHealth.Web.Models.Scheduling.QueueStatus.LeftWithoutBeingSeen);
                var awaitingReview = await _db.LabResults.AsNoTracking()
                    .CountAsync(r => r.LabOrder!.Patient!.FacilityId == fid && r.LabOrder.OrderedById == userId
                        && r.Status == ThriveHealth.Web.Models.Diagnostics.LabResultStatus.Authorized && r.LabOrder.Status != ThriveHealth.Web.Models.Clinical.OrderStatus.Completed);
                var myAdmits = await _db.Admissions.AsNoTracking()
                    .CountAsync(a => a.FacilityId == fid && a.Status == ThriveHealth.Web.Models.Inpatient.AdmissionStatus.Active && a.AdmittingDoctorId == userId);
                var todayConsults = await _db.Encounters.AsNoTracking()
                    .CountAsync(e => e.FacilityId == fid && e.ClinicianId == userId && e.StartedAt >= todayStart);
                return new()
                {
                    S("My queue", myQueueToday, "bi-person-lines-fill", "primary"),
                    S("My admissions", myAdmits, "bi-clipboard2-heart", "info"),
                    S("Lab results to review", awaitingReview, "bi-droplet-half", "warning"),
                    S("Today's consultations", todayConsults, "bi-calendar2-check", "success")
                };
            }

            case Persona.Nursing:
            {
                var triageQ = await _db.Encounters.AsNoTracking()
                    .CountAsync(e => e.FacilityId == fid && e.Type == ThriveHealth.Web.Models.Clinical.EncounterType.Emergency
                        && e.Status == ThriveHealth.Web.Models.Clinical.EncounterStatus.InProgress && e.ResusBayId == null);
                var marDue = await _db.MarSlots.AsNoTracking()
                    .CountAsync(s => s.InpatientMedication!.Admission!.FacilityId == fid
                        && s.Status == ThriveHealth.Web.Models.Inpatient.MarSlotStatus.Scheduled
                        && s.ScheduledUtc <= DateTime.UtcNow.AddHours(2));
                var bedsTotal = await _db.Beds.AsNoTracking().CountAsync(b => b.Ward!.FacilityId == fid);
                var bedsFree = await _db.Beds.AsNoTracking().CountAsync(b => b.Ward!.FacilityId == fid && b.Status == ThriveHealth.Web.Models.Inpatient.BedStatus.Free);
                var ancToday = await _db.AnteNatalVisits.AsNoTracking()
                    .CountAsync(v => v.AnteNatalRecord!.FacilityId == fid && v.VisitDate == today);
                return new()
                {
                    S("Triage queue", triageQ, "bi-bandaid", "warning"),
                    S("Drugs due (next 2h)", marDue, "bi-capsule", "primary"),
                    S("Beds free", $"{bedsFree}/{bedsTotal}", "bi-hospital", "info"),
                    S("ANC visits today", ancToday, "bi-heart-pulse", "success")
                };
            }

            case Persona.FrontOffice:
            {
                var apptsToday = await _db.Appointments.AsNoTracking()
                    .CountAsync(a => a.FacilityId == fid && a.ScheduledStartUtc >= todayStart && a.ScheduledStartUtc < todayStart.AddDays(1));
                var checkedIn = await _db.QueueEntries.AsNoTracking()
                    .CountAsync(q => q.FacilityId == fid && q.TicketDate == today
                        && q.Status != ThriveHealth.Web.Models.Scheduling.QueueStatus.Completed
                        && q.Status != ThriveHealth.Web.Models.Scheduling.QueueStatus.Skipped);
                var newRegs = await _db.Patients.AsNoTracking().CountAsync(p => p.FacilityId == fid && p.CreatedAt >= todayStart);
                var unverifiedNin = await _db.Patients.AsNoTracking().CountAsync(p => p.FacilityId == fid && !p.IsMergedAlias && (p.Nin == null || !p.NinVerified));
                return new()
                {
                    S("Appointments today", apptsToday, "bi-calendar2", "info"),
                    S("In queue now", checkedIn, "bi-list-check", "primary"),
                    S("New registrations today", newRegs, "bi-person-plus", "success"),
                    S("Unverified IDs", unverifiedNin, "bi-person-badge", "warning")
                };
            }

            case Persona.Lab:
            {
                var open = await _db.LabOrders.AsNoTracking()
                    .CountAsync(o => o.Patient!.FacilityId == fid
                        && o.Status != ThriveHealth.Web.Models.Clinical.OrderStatus.Completed
                        && o.Status != ThriveHealth.Web.Models.Clinical.OrderStatus.Cancelled);
                var awaitingAuth = await _db.LabResults.AsNoTracking()
                    .CountAsync(r => r.LabOrder!.Patient!.FacilityId == fid && r.Status == ThriveHealth.Web.Models.Diagnostics.LabResultStatus.Final);
                var critical = await _db.LabResults.AsNoTracking()
                    .CountAsync(r => r.LabOrder!.Patient!.FacilityId == fid && r.HasCriticalValue && !r.CriticalNotified);
                var bloodAvail = await _db.BloodUnits.AsNoTracking()
                    .CountAsync(u => u.FacilityId == fid && u.Status == ThriveHealth.Web.Models.BloodBank.BloodUnitStatus.Available);
                return new()
                {
                    S("Specimens open", open, "bi-droplet", "primary"),
                    S("Awaiting authorisation", awaitingAuth, "bi-shield-check", "warning"),
                    S("Critical, not notified", critical, "bi-exclamation-octagon", "danger"),
                    S("Blood units available", bloodAvail, "bi-droplet-fill", "success")
                };
            }

            case Persona.Imaging:
            {
                var open = await _db.ImagingOrders.AsNoTracking()
                    .CountAsync(o => o.Patient!.FacilityId == fid
                        && o.Status != ThriveHealth.Web.Models.Clinical.OrderStatus.Completed
                        && o.Status != ThriveHealth.Web.Models.Clinical.OrderStatus.Cancelled);
                var awaitingAuth = await _db.ImagingReports.AsNoTracking()
                    .CountAsync(r => r.ImagingOrder!.Patient!.FacilityId == fid && r.ReportedAt != null && r.AuthorizedAt == null);
                var crit = await _db.ImagingReports.AsNoTracking()
                    .CountAsync(r => r.ImagingOrder!.Patient!.FacilityId == fid && r.HasCriticalFinding);
                var doneToday = await _db.ImagingReports.AsNoTracking()
                    .CountAsync(r => r.ImagingOrder!.Patient!.FacilityId == fid && r.PerformedAt >= todayStart);
                return new()
                {
                    S("Studies pending", open, "bi-radioactive", "primary"),
                    S("Reports for authorisation", awaitingAuth, "bi-shield-check", "warning"),
                    S("Critical findings", crit, "bi-exclamation-octagon", "danger"),
                    S("Performed today", doneToday, "bi-camera-fill", "success")
                };
            }

            case Persona.Pharmacy:
            {
                var rxQueue = await _db.Prescriptions.AsNoTracking()
                    .CountAsync(r => r.Encounter!.FacilityId == fid && r.Status == ThriveHealth.Web.Models.Clinical.PrescriptionStatus.Issued);
                var lowStock = await _db.DrugStocks.AsNoTracking()
                    .CountAsync(s => s.Store!.FacilityId == fid && s.Drug!.ReorderLevel.HasValue && s.QuantityOnHand <= s.Drug.ReorderLevel);
                var expSoon = await _db.DrugStocks.AsNoTracking()
                    .CountAsync(s => s.Store!.FacilityId == fid && s.ExpiryDate >= today && s.ExpiryDate <= soon);
                var dispensedToday = await _db.Dispenses.AsNoTracking()
                    .CountAsync(d => d.Store!.FacilityId == fid && d.DispensedAt >= todayStart);
                return new()
                {
                    S("Prescriptions waiting", rxQueue, "bi-prescription2", "primary"),
                    S("Low stock items", lowStock, "bi-exclamation-triangle", "warning"),
                    S("Expiring within 90d", expSoon, "bi-calendar-x", "danger"),
                    S("Dispensed today", dispensedToday, "bi-bag-check", "success")
                };
            }

            case Persona.Finance:
            {
                var cashToday = await _db.Payments.AsNoTracking()
                    .Where(p => p.Bill!.FacilityId == fid && p.Status == ThriveHealth.Web.Models.Billing.PaymentStatus.Recorded
                        && p.ReceivedAt >= todayStart && p.Method == ThriveHealth.Web.Models.Billing.PaymentMethod.Cash)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0m;
                var allToday = await _db.Payments.AsNoTracking()
                    .Where(p => p.Bill!.FacilityId == fid && p.Status == ThriveHealth.Web.Models.Billing.PaymentStatus.Recorded && p.ReceivedAt >= todayStart)
                    .SumAsync(p => (decimal?)p.Amount) ?? 0m;
                var outstanding = await _db.Bills.AsNoTracking()
                    .Where(b => b.FacilityId == fid && (b.Status == ThriveHealth.Web.Models.Billing.BillStatus.Open || b.Status == ThriveHealth.Web.Models.Billing.BillStatus.PartiallyPaid))
                    .SumAsync(b => (decimal?)(b.NetAmount - b.PaidAmount)) ?? 0m;
                var openClaims = await _db.Claims.AsNoTracking()
                    .CountAsync(c => c.FacilityId == fid && (c.Status == ThriveHealth.Web.Models.Insurance.ClaimStatus.Submitted
                        || c.Status == ThriveHealth.Web.Models.Insurance.ClaimStatus.Acknowledged
                        || c.Status == ThriveHealth.Web.Models.Insurance.ClaimStatus.PartiallyPaid));
                return new()
                {
                    S("Revenue today (₦)", allToday.ToString("N0"), "bi-cash-coin", "success"),
                    S("Cash collected (₦)", cashToday.ToString("N0"), "bi-cash", "primary"),
                    S("Outstanding (₦)", outstanding.ToString("N0"), "bi-receipt", "warning"),
                    S("Open claims", openClaims, "bi-file-earmark-medical", "info")
                };
            }

            case Persona.Claims:
            {
                var draft = await _db.Claims.AsNoTracking().CountAsync(c => c.FacilityId == fid && c.Status == ThriveHealth.Web.Models.Insurance.ClaimStatus.Draft);
                var submitted = await _db.Claims.AsNoTracking().CountAsync(c => c.FacilityId == fid && c.Status == ThriveHealth.Web.Models.Insurance.ClaimStatus.Submitted);
                var denied = await _db.Claims.AsNoTracking().CountAsync(c => c.FacilityId == fid && c.Status == ThriveHealth.Web.Models.Insurance.ClaimStatus.Denied);
                var paid30 = await _db.Claims.AsNoTracking()
                    .Where(c => c.FacilityId == fid && c.Status == ThriveHealth.Web.Models.Insurance.ClaimStatus.Paid && c.RespondedAt >= DateTime.UtcNow.AddDays(-30))
                    .SumAsync(c => (decimal?)c.PaidAmount) ?? 0m;
                return new()
                {
                    S("Drafts to submit", draft, "bi-pencil-square", "warning"),
                    S("Awaiting payer response", submitted, "bi-hourglass-split", "primary"),
                    S("Denied", denied, "bi-x-octagon", "danger"),
                    S("Paid last 30d (₦)", paid30.ToString("N0"), "bi-cash-coin", "success")
                };
            }

            case Persona.Hr:
            {
                var staff = await _db.Users.AsNoTracking().CountAsync(u => u.FacilityId == fid && u.IsActive);
                var pendingLeave = await _db.LeaveRequests.AsNoTracking()
                    .CountAsync(l => l.Staff!.FacilityId == fid && l.Status == ThriveHealth.Web.Models.Hr.LeaveStatus.Submitted);
                var licExpired = await _db.Users.AsNoTracking()
                    .CountAsync(u => u.FacilityId == fid && u.IsActive && u.LicenseExpiry.HasValue && u.LicenseExpiry < DateTime.UtcNow);
                var licSoon = await _db.Users.AsNoTracking()
                    .CountAsync(u => u.FacilityId == fid && u.IsActive && u.LicenseExpiry.HasValue
                        && u.LicenseExpiry >= DateTime.UtcNow && u.LicenseExpiry <= DateTime.UtcNow.AddDays(60));
                return new()
                {
                    S("Active staff", staff, "bi-people", "primary"),
                    S("Leave requests pending", pendingLeave, "bi-calendar-x", "warning"),
                    S("Licences expired", licExpired, "bi-shield-exclamation", "danger"),
                    S("Expiring within 60d", licSoon, "bi-clock-history", "info")
                };
            }

            case Persona.Procurement:
            {
                var openPo = await _db.PurchaseOrders.AsNoTracking()
                    .CountAsync(po => po.FacilityId == fid && (po.Status == ThriveHealth.Web.Models.Inventory.PurchaseOrderStatus.Issued
                        || po.Status == ThriveHealth.Web.Models.Inventory.PurchaseOrderStatus.PartiallyReceived));
                var grnToday = await _db.Grns.AsNoTracking().CountAsync(g => g.FacilityId == fid && g.ReceivedAt >= todayStart);
                var lowInv = await _db.InventoryStocks.AsNoTracking()
                    .CountAsync(s => s.Store!.FacilityId == fid && s.InventoryItem!.ReorderLevel.HasValue && s.QuantityOnHand <= s.InventoryItem.ReorderLevel);
                var expSoonInv = await _db.InventoryStocks.AsNoTracking()
                    .CountAsync(s => s.Store!.FacilityId == fid && s.ExpiryDate >= today && s.ExpiryDate <= soon);
                return new()
                {
                    S("Open POs", openPo, "bi-cart", "primary"),
                    S("GRNs received today", grnToday, "bi-truck", "info"),
                    S("Low stock items", lowInv, "bi-exclamation-triangle", "warning"),
                    S("Expiring within 90d", expSoonInv, "bi-calendar-x", "danger")
                };
            }

            case Persona.PublicHealth:
            {
                var idsrOpen = await _db.IdsrCases.AsNoTracking()
                    .CountAsync(c => c.FacilityId == fid && c.Status == ThriveHealth.Web.Models.Reporting.IdsrCaseStatus.Open);
                var idsrUnnotified = await _db.IdsrCases.AsNoTracking()
                    .CountAsync(c => c.FacilityId == fid && !c.NotifiedNcdc
                        && c.NotifiableDisease!.Window == ThriveHealth.Web.Models.Reporting.NotificationWindow.Immediate);
                var immDue = await _db.ImmunizationDoses.AsNoTracking()
                    .CountAsync(d => d.FacilityId == fid && d.Status == ThriveHealth.Web.Models.Immunization.DoseStatus.Due
                        && d.DueDate <= today);
                var nhmisDone = await _db.NhmisReports.AsNoTracking()
                    .AnyAsync(r => r.FacilityId == fid && r.Year == DateTime.UtcNow.AddMonths(-1).Year && r.Month == DateTime.UtcNow.AddMonths(-1).Month
                        && r.Status == ThriveHealth.Web.Models.Reporting.NhmisReportStatus.Submitted);
                return new()
                {
                    S("IDSR open cases", idsrOpen, "bi-shield-shaded", "primary"),
                    S("Immediate, not notified", idsrUnnotified, "bi-broadcast", "danger"),
                    S("Vaccines due", immDue, "bi-bandaid", "warning"),
                    S("Last month NHMIS", nhmisDone ? "Submitted" : "Pending", "bi-file-earmark-bar-graph", nhmisDone ? "success" : "warning")
                };
            }

            case Persona.Specialty:
            {
                var allied = await _db.AlliedSessions.AsNoTracking()
                    .CountAsync(s => s.FacilityId == fid && s.ScheduledUtc >= todayStart && s.ScheduledUtc < todayStart.AddDays(1));
                var open = await _db.AlliedSessions.AsNoTracking()
                    .CountAsync(s => s.FacilityId == fid && s.Status == ThriveHealth.Web.Models.Allied.SessionStatus.Scheduled);
                return new()
                {
                    S("Sessions today", allied, "bi-calendar2", "primary"),
                    S("Open referrals", open, "bi-people", "info")
                };
            }

            default:
                return new() { S("Welcome", "ThriveHealth", "bi-heart-pulse", "primary") };
        }
    }

    // ====================================================================================
    // Recent activity — small cross-module feed scoped to persona
    // ====================================================================================
    private async Task<List<DashboardActivityItem>> BuildRecentActivityAsync(Persona p, int fid, string userId)
    {
        var items = new List<DashboardActivityItem>();
        var since = DateTime.UtcNow.AddDays(-2);

        switch (p)
        {
            case Persona.Executive:
            case Persona.FrontOffice:
            {
                var encs = await _db.Encounters.AsNoTracking().Include(e => e.Patient).Include(e => e.Clinician)
                    .Where(e => e.FacilityId == fid && e.StartedAt >= since).OrderByDescending(e => e.StartedAt).Take(8).ToListAsync();
                items.AddRange(encs.Select(e => new DashboardActivityItem
                {
                    At = e.StartedAt, Icon = "bi-clipboard2-pulse", Tone = "primary",
                    Title = $"{e.Type} · {e.Patient?.FullName}",
                    Subtitle = $"{e.Clinician?.FullName ?? "—"} · {e.ChiefComplaint}",
                    LinkController = "Encounters", LinkAction = "Summary", LinkId = e.Id
                }));
                break;
            }
            case Persona.Clinician:
            {
                var encs = await _db.Encounters.AsNoTracking().Include(e => e.Patient)
                    .Where(e => e.FacilityId == fid && e.ClinicianId == userId && e.StartedAt >= since.AddDays(-7))
                    .OrderByDescending(e => e.StartedAt).Take(8).ToListAsync();
                items.AddRange(encs.Select(e => new DashboardActivityItem
                {
                    At = e.StartedAt, Icon = "bi-clipboard2-pulse", Tone = "primary",
                    Title = e.Patient?.FullName ?? "—",
                    Subtitle = $"{e.Type} · {e.ChiefComplaint}",
                    LinkController = "Encounters",
                    LinkAction = e.Status == ThriveHealth.Web.Models.Clinical.EncounterStatus.Signed ? "Summary" : "Edit",
                    LinkId = e.Id
                }));
                break;
            }
            case Persona.Lab:
            {
                var orders = await _db.LabOrders.AsNoTracking().Include(o => o.Patient).Include(o => o.LabTest)
                    .Where(o => o.Patient!.FacilityId == fid && o.OrderedAt >= since).OrderByDescending(o => o.OrderedAt).Take(8).ToListAsync();
                items.AddRange(orders.Select(o => new DashboardActivityItem
                {
                    At = o.OrderedAt, Icon = "bi-droplet-half", Tone = o.Status == ThriveHealth.Web.Models.Clinical.OrderStatus.Completed ? "success" : "primary",
                    Title = o.LabTest?.Name ?? o.TestName, Subtitle = o.Patient?.FullName,
                    LinkController = "Lab", LinkAction = "View", LinkId = o.Id
                }));
                break;
            }
            case Persona.Imaging:
            {
                var orders = await _db.ImagingOrders.AsNoTracking().Include(o => o.Patient)
                    .Where(o => o.Patient!.FacilityId == fid && o.OrderedAt >= since).OrderByDescending(o => o.OrderedAt).Take(8).ToListAsync();
                items.AddRange(orders.Select(o => new DashboardActivityItem
                {
                    At = o.OrderedAt, Icon = "bi-radioactive", Tone = "primary",
                    Title = $"{o.Modality} · {o.StudyDescription}", Subtitle = o.Patient?.FullName,
                    LinkController = "Imaging", LinkAction = "View", LinkId = o.Id
                }));
                break;
            }
            case Persona.Pharmacy:
            {
                var rx = await _db.Prescriptions.AsNoTracking().Include(r => r.Patient).Include(r => r.Items)
                    .Where(r => r.Encounter!.FacilityId == fid && r.IssuedAt >= since)
                    .OrderByDescending(r => r.IssuedAt).Take(8).ToListAsync();
                items.AddRange(rx.Select(r => new DashboardActivityItem
                {
                    At = r.IssuedAt, Icon = "bi-prescription2", Tone = r.Status == ThriveHealth.Web.Models.Clinical.PrescriptionStatus.Dispensed ? "success" : "primary",
                    Title = $"{r.Items.Count} item(s) · {r.Patient?.FullName}", Subtitle = r.Status.ToString(),
                    LinkController = "Pharmacy", LinkAction = "Worklist"
                }));
                break;
            }
            case Persona.Finance:
            {
                var pays = await _db.Payments.AsNoTracking().Include(p => p.Bill).ThenInclude(b => b!.Patient)
                    .Where(p => p.Bill!.FacilityId == fid && p.ReceivedAt >= since)
                    .OrderByDescending(p => p.ReceivedAt).Take(8).ToListAsync();
                items.AddRange(pays.Select(p => new DashboardActivityItem
                {
                    At = p.ReceivedAt, Icon = "bi-cash-coin", Tone = "success",
                    Title = $"₦{p.Amount:N0} · {p.Method}", Subtitle = p.Bill?.Patient?.FullName,
                    LinkController = "Cashier", LinkAction = "Bill", LinkId = p.BillId
                }));
                break;
            }
            case Persona.Nursing:
            {
                var notes = await _db.NursingNotes.AsNoTracking().Include(n => n.Admission!).ThenInclude(a => a!.Patient)
                    .Where(n => n.Admission!.FacilityId == fid && n.RecordedUtc >= since)
                    .OrderByDescending(n => n.RecordedUtc).Take(8).ToListAsync();
                items.AddRange(notes.Select(n => new DashboardActivityItem
                {
                    At = n.RecordedUtc, Icon = "bi-clipboard-pulse", Tone = "info",
                    Title = $"{n.Shift} · {n.Admission?.Patient?.FullName}", Subtitle = n.Body[..Math.Min(80, n.Body.Length)],
                    LinkController = "Admissions", LinkAction = "Details", LinkId = n.AdmissionId
                }));
                break;
            }
        }

        return items.OrderByDescending(i => i.At).Take(8).ToList();
    }

    // ====================================================================================
    // Alerts — facility-wide warnings relevant to the persona
    // ====================================================================================
    private async Task<List<string>> BuildAlertsAsync(Persona p, int fid)
    {
        var alerts = new List<string>();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);

        // Universal: critical lab + immediate IDSR
        var criticalLab = await _db.LabResults.AsNoTracking()
            .CountAsync(r => r.LabOrder!.Patient!.FacilityId == fid && r.HasCriticalValue && !r.CriticalNotified);
        if (criticalLab > 0)
            alerts.Add($"{criticalLab} critical lab result(s) not yet notified to ordering clinician.");

        var idsrUrgent = await _db.IdsrCases.AsNoTracking()
            .CountAsync(c => c.FacilityId == fid && !c.NotifiedNcdc
                && c.NotifiableDisease!.Window == ThriveHealth.Web.Models.Reporting.NotificationWindow.Immediate);
        if (idsrUrgent > 0)
            alerts.Add($"{idsrUrgent} immediate-notification IDSR case(s) not yet reported to NCDC.");

        // Persona-specific
        if (p == Persona.Pharmacy || p == Persona.Procurement || p == Persona.Executive)
        {
            var soon = today.AddDays(30);
            var expiring = await _db.DrugStocks.AsNoTracking()
                .CountAsync(s => s.Store!.FacilityId == fid && s.ExpiryDate >= today && s.ExpiryDate <= soon);
            if (expiring > 0) alerts.Add($"{expiring} drug batch(es) expire within 30 days.");
        }
        if (p == Persona.Hr || p == Persona.Executive)
        {
            var licExpired = await _db.Users.AsNoTracking()
                .CountAsync(u => u.FacilityId == fid && u.IsActive && u.LicenseExpiry.HasValue && u.LicenseExpiry < DateTime.UtcNow);
            if (licExpired > 0) alerts.Add($"{licExpired} staff licence(s) have expired.");
        }
        return alerts;
    }

    // ====================================================================================
    // Quick actions — what the persona would click most
    // ====================================================================================
    private static List<DashboardAction> BuildNextActions(Persona p)
    {
        DashboardAction A(string label, string ctrl, string action, string icon, string? perm, object? rv = null)
            => new() { Label = label, Controller = ctrl, Action = action, Icon = icon, Permission = perm, RouteValues = rv };

        var P = typeof(Permissions);
        return p switch
        {
            Persona.Executive => new()
            {
                A("Analytics", "Analytics", "Index", "bi-graph-up-arrow", Permissions.AnalyticsView),
                A("Audit log", "Admin", "Audit", "bi-journal-text", Permissions.AuditView),
                A("Patient register", "Patients", "Index", "bi-people", Permissions.PatientsRead),
                A("Bills overview", "Cashier", "Bills", "bi-receipt", Permissions.BillsRead),
                A("Claims", "Claims", "Index", "bi-file-earmark-medical", Permissions.ClaimsManage)
            },
            Persona.Clinician => new()
            {
                A("My queue", "Queue", "MyQueue", "bi-clipboard2-pulse", Permissions.QueueServe),
                A("Live queues", "Queue", "Index", "bi-list-check", Permissions.QueueRead),
                A("A&E live board", "Emergency", "Index", "bi-bandaid", Permissions.EmergencyBoardRead),
                A("Admissions", "Admissions", "Index", "bi-clipboard2-heart", Permissions.AdmissionsManage),
                A("Theatre schedule", "Theatre", "Schedule", "bi-scissors", Permissions.TheatreSchedule)
            },
            Persona.Nursing => new()
            {
                A("Bed board", "Wards", "Board", "bi-hospital", Permissions.WardsManage),
                A("Live queues", "Queue", "Index", "bi-list-check", Permissions.QueueRead),
                A("New A&E triage", "Emergency", "Triage", "bi-bandaid", Permissions.TriageCreate),
                A("Immunization worklist", "Immunization", "Worklist", "bi-bandaid", Permissions.ImmunizationAdminister),
                A("Antenatal clinic", "Maternity", "Index", "bi-heart-pulse", Permissions.AncManage)
            },
            Persona.FrontOffice => new()
            {
                A("Register a patient", "Patients", "Register", "bi-person-plus", Permissions.PatientsRegister),
                A("Book appointment", "Appointments", "Book", "bi-calendar-plus", Permissions.AppointmentsBook),
                A("Check in walk-in", "Queue", "CheckIn", "bi-door-open", Permissions.QueueCheckIn),
                A("Today's appointments", "Appointments", "Index", "bi-calendar2-week", Permissions.AppointmentsRead),
                A("Live queues", "Queue", "Index", "bi-list-check", Permissions.QueueRead)
            },
            Persona.Lab => new()
            {
                A("Lab worklist", "Lab", "Worklist", "bi-droplet-half", Permissions.LabPerform),
                A("Blood bank", "BloodBank", "Index", "bi-droplet-fill", Permissions.BloodBankManage),
                A("Patient register", "Patients", "Index", "bi-people", Permissions.PatientsRead)
            },
            Persona.Imaging => new()
            {
                A("Imaging worklist", "Imaging", "Worklist", "bi-radioactive", Permissions.ImagingPerform),
                A("Patient register", "Patients", "Index", "bi-people", Permissions.PatientsRead)
            },
            Persona.Pharmacy => new()
            {
                A("Pharmacy worklist", "Pharmacy", "Worklist", "bi-prescription2", Permissions.PharmacyDispense),
                A("Stock on hand", "Pharmacy", "Stock", "bi-box-seam", Permissions.PharmacyStock),
                A("Drug register", "Drugs", "Index", "bi-capsule", Permissions.PharmacyStock)
            },
            Persona.Finance => new()
            {
                A("Bills", "Cashier", "Bills", "bi-receipt", Permissions.BillsRead),
                A("My cashier shift", "Cashier", "Shift", "bi-cash-stack", Permissions.CashierShiftManage),
                A("Claims", "Claims", "Index", "bi-file-earmark-medical", Permissions.ClaimsManage),
                A("Payers", "Payers", "Index", "bi-buildings-fill", Permissions.PayersManage)
            },
            Persona.Claims => new()
            {
                A("Claims worklist", "Claims", "Index", "bi-file-earmark-medical", Permissions.ClaimsManage),
                A("Payers", "Payers", "Index", "bi-buildings-fill", Permissions.PayersManage),
                A("Bills", "Cashier", "Bills", "bi-receipt", Permissions.BillsRead)
            },
            Persona.Hr => new()
            {
                A("Staff & licences", "Hr", "Index", "bi-person-vcard", Permissions.HrRead),
                A("Duty roster", "Hr", "Roster", "bi-calendar2-week", Permissions.RosterManage),
                A("Leave requests", "Hr", "Leave", "bi-calendar-x", Permissions.LeaveRequest),
                A("Patient register", "Patients", "Index", "bi-people", Permissions.PatientsRead)
            },
            Persona.Procurement => new()
            {
                A("Purchase orders", "PurchaseOrders", "Index", "bi-cart", Permissions.PurchaseOrderManage),
                A("Goods received", "Grn", "Index", "bi-truck", Permissions.GrnReceive),
                A("Inventory stock", "Inventory", "Stock", "bi-box-seam", Permissions.InventoryRead),
                A("Stock takes", "StockTake", "Index", "bi-clipboard-check", Permissions.StockTakeManage)
            },
            Persona.PublicHealth => new()
            {
                A("IDSR cases", "Reporting", "Idsr", "bi-shield-shaded", Permissions.IdsrReport),
                A("NHMIS monthly", "Reporting", "Nhmis", "bi-file-earmark-bar-graph", Permissions.NhmisGenerate),
                A("Immunization", "Immunization", "Worklist", "bi-bandaid", Permissions.ImmunizationAdminister),
                A("Analytics", "Analytics", "Index", "bi-graph-up-arrow", Permissions.AnalyticsView)
            },
            Persona.Specialty => new()
            {
                A("Allied sessions", "Allied", "Index", "bi-clipboard2-pulse", Permissions.AlliedSession),
                A("Patient register", "Patients", "Index", "bi-people", Permissions.PatientsRead)
            },
            _ => new() { A("Patient register", "Patients", "Index", "bi-people", Permissions.PatientsRead) }
        };
    }
}
