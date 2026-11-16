using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.ViewModels;
using AdmissionStatus = ThriveHealth.Web.Models.Inpatient.AdmissionStatus;
using BedStatus = ThriveHealth.Web.Models.Inpatient.BedStatus;
using ClaimStatus = ThriveHealth.Web.Models.Insurance.ClaimStatus;
using DoseStatus = ThriveHealth.Web.Models.Immunization.DoseStatus;
using IdsrCaseStatus = ThriveHealth.Web.Models.Reporting.IdsrCaseStatus;
using LeaveStatus = ThriveHealth.Web.Models.Hr.LeaveStatus;
using NotificationWindow = ThriveHealth.Web.Models.Reporting.NotificationWindow;
using OrderStatus = ThriveHealth.Web.Models.Clinical.OrderStatus;
using PaymentStatus = ThriveHealth.Web.Models.Billing.PaymentStatus;
using PrescriptionStatus = ThriveHealth.Web.Models.Clinical.PrescriptionStatus;
using PurchaseOrderStatus = ThriveHealth.Web.Models.Inventory.PurchaseOrderStatus;
using QueueStatus = ThriveHealth.Web.Models.Scheduling.QueueStatus;
using SessionStatus = ThriveHealth.Web.Models.Allied.SessionStatus;

namespace ThriveHealth.Web.Controllers;

/// <summary>
/// Role-specific content for the dashboard grid: hero, trend chart, gauge and spotlight.
/// Every value comes from the facility's data. Patient names appear only on personas whose
/// existing boards already show them; HR, Procurement and the default persona show none.
/// Days are UTC, matching the stat tiles.
/// </summary>
public partial class DashboardController
{
    private const int BoardQueueCap = 20;
    private const int BoardWardListCap = 15;

    private sealed record Buckets(DateTime Start, int Count, bool Monthly, string[] Labels)
    {
        public double[] Sum(IEnumerable<(DateTime At, double Value)> rows)
        {
            var totals = new double[Count];
            foreach (var (at, value) in rows)
            {
                var i = Monthly
                    ? (at.Year - Start.Year) * 12 + at.Month - Start.Month
                    : (at.Date - Start.Date).Days;
                if (i >= 0 && i < Count) totals[i] += value;
            }
            return totals;
        }

        public double[] Tally(IEnumerable<DateTime> rows) => Sum(rows.Select(at => (at, 1d)));
    }

    private static Buckets LastDays()
    {
        const int days = 14;
        var start = DateTime.UtcNow.Date.AddDays(-(days - 1));
        return new Buckets(start, days, false,
            Enumerable.Range(0, days).Select(i => start.AddDays(i).ToString("d MMM")).ToArray());
    }

    private static Buckets LastMonths()
    {
        const int months = 12;
        var now = DateTime.UtcNow;
        var start = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(-(months - 1));
        return new Buckets(start, months, true,
            Enumerable.Range(0, months).Select(i => start.AddMonths(i).ToString("MMM")).ToArray());
    }

    private static string Naira(decimal amount) => $"₦{amount:N0}";
    private static string Naira(double amount) => $"₦{amount:N0}";
    private static string Num(double value) => value.ToString("N0");
    private static string Capped(int count, int cap) => count >= cap ? $"{cap}+" : count.ToString();
    private static string At(DateTime utc) => utc.ToLocalTime().ToString("HH:mm");
    private static string Pct(int part, int whole) => whole == 0 ? "0%" : $"{Math.Round(100.0 * part / whole):0}%";

    private static bool IsUpcoming(string appointmentStatus) =>
        appointmentStatus is not ("Cancelled" or "Completed" or "NoShow");

    private static string RelativeDay(DateTime utc)
    {
        var days = (DateTime.UtcNow.Date - utc.Date).Days;
        return days switch { 0 => "Today", 1 => "Yesterday", -1 => "Tomorrow", _ => utc.ToLocalTime().ToString("d MMM") };
    }

    private static string RelativeDay(DateOnly date) => RelativeDay(date.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc));

    private CanvasLink To(string action, string controller, string? permission = null, object? route = null)
        => new(Url.Action(action, controller, route) ?? "#", permission);

    private TrendPanel Trend(string title, Buckets b, string nameA, double[] a, string nameB, double[] b2, Func<double, string>? format = null)
    {
        var show = format ?? Num;
        return new TrendPanel
        {
            Title = title,
            Subtitle = b.Monthly ? "Last 12 months" : "Last 14 days",
            SeriesA = nameA, SeriesB = nameB, A = a, B = b2, Labels = b.Labels,
            TotalA = show(a.Sum()), TotalB = show(b2.Sum())
        };
    }

    private static GaugePanel Gauge(string title, string subtitle, string value, string caption, string labelA, int a, string labelB, int b)
        => new() { Title = title, Subtitle = subtitle, Value = value, Caption = caption, LabelA = labelA, A = a, LabelB = labelB, B = b };

    private async Task<DashboardCanvasVm> BuildCanvasAsync(Persona p, int fid, string userId, DashboardViewModel vm)
    {
        var canvas = p switch
        {
            Persona.Executive => await ExecutiveCanvasAsync(fid, (ExecutiveBoardVm)vm.Board!),
            Persona.Clinician => await ClinicianCanvasAsync(fid, userId, (ClinicianBoardVm)vm.Board!),
            Persona.Nursing => await NursingCanvasAsync(fid, (NursingBoardVm)vm.Board!),
            Persona.FrontOffice => await FrontOfficeCanvasAsync(fid, (FrontOfficeBoardVm)vm.Board!),
            Persona.Lab => await LabCanvasAsync(fid, (LabBoardVm)vm.Board!),
            Persona.Imaging => await ImagingCanvasAsync(fid),
            Persona.Pharmacy => await PharmacyCanvasAsync(fid, (PharmacyBoardVm)vm.Board!),
            Persona.Finance => await FinanceCanvasAsync(fid, (FinanceBoardVm)vm.Board!),
            Persona.Claims => await ClaimsCanvasAsync(fid),
            Persona.Hr => await HrCanvasAsync(fid),
            Persona.Procurement => await ProcurementCanvasAsync(fid),
            Persona.PublicHealth => await PublicHealthCanvasAsync(fid),
            Persona.Specialty => await SpecialtyCanvasAsync(fid),
            _ => await OverviewCanvasAsync(fid)
        };
        canvas.Hero.Subtitle = $"{vm.FacilityName} · {DateTime.Now:dddd, d MMMM}";
        return canvas;
    }

    private const string HiddenForRole = "Patient details aren't available for your role";

    /// <summary>
    /// Applies the user's real permissions, since roles can be edited per tenant. Without
    /// patients.read, nothing on the grid names a patient. Activity links the user can't open
    /// become plain text rather than leading to an access-denied page.
    /// </summary>
    private static void ApplyPermissions(Persona p, DashboardViewModel vm, HashSet<string> perms)
    {
        foreach (var item in vm.RecentActivity)
        {
            var needed = item.LinkController switch
            {
                "Encounters" => Permissions.EncountersWrite,
                "Lab" => Permissions.LabRead,
                "Imaging" => Permissions.ImagingRead,
                "Pharmacy" => Permissions.PharmacyDispense,
                "Cashier" => Permissions.BillsRead,
                "Admissions" => Permissions.AdmissionsManage,
                _ => null
            };
            if (needed is not null && !perms.Contains(needed))
            {
                item.LinkController = null;
                item.LinkAction = null;
            }
        }

        if (perms.Contains(Permissions.PatientsRead) || vm.Canvas is null) return;

        vm.RecentActivity = new List<DashboardActivityItem>();
        vm.Board = null;
        vm.BoardKey = null;
        if (p is not (Persona.Hr or Persona.Procurement or Persona.Default))
        {
            vm.Canvas.Hero.Next = null;
            vm.Canvas.Hero.NextEmpty = HiddenForRole;
        }
        if (p is Persona.Clinician or Persona.Lab or Persona.Imaging or Persona.Finance
            or Persona.Claims or Persona.PublicHealth or Persona.Specialty)
        {
            var hidden = vm.Canvas.Spotlight;
            hidden.Name = null;
            hidden.Detail = null;
            hidden.Badge = null;
            hidden.Chips = new List<string>();
            hidden.Link = null;
            hidden.Empty = HiddenForRole;
        }
    }

    // ====================================================================================
    // Stat tiles: a line of context and a link for each number
    // ====================================================================================
    private async Task<List<DashboardStat>> CompleteStatsAsync(Persona p, int fid, List<DashboardStat> stats)
    {
        var tiles = stats;
        if (p == Persona.Specialty)
        {
            var todayStart = DateTime.UtcNow.Date;
            var weekStart = todayStart.AddDays(-6);
            var completed = await _db.AlliedSessions.AsNoTracking()
                .CountAsync(s => s.FacilityId == fid && s.Status == SessionStatus.Completed && s.CompletedUtc >= weekStart);
            var inProgress = await _db.AlliedSessions.AsNoTracking()
                .CountAsync(s => s.FacilityId == fid && s.Status == SessionStatus.InProgress);
            tiles = stats.Concat(new[]
            {
                new DashboardStat { Label = "In progress", Value = inProgress.ToString(), Icon = "bi-activity", Tone = "warning" },
                new DashboardStat { Label = "Completed this week", Value = completed.ToString(), Icon = "bi-check2-circle", Tone = "success" }
            }).ToList();
        }
        else if (p == Persona.Default)
        {
            var patients = await _db.Patients.AsNoTracking().CountAsync(x => x.FacilityId == fid && !x.IsMergedAlias);
            var admissions = await _db.Admissions.AsNoTracking().CountAsync(a => a.FacilityId == fid && a.Status == AdmissionStatus.Active);
            var bedsFree = await _db.Beds.AsNoTracking().CountAsync(b => b.Ward!.FacilityId == fid && b.Status == BedStatus.Free);
            var clinics = await _db.Clinics.AsNoTracking().CountAsync(c => c.FacilityId == fid);
            tiles = new List<DashboardStat>
            {
                new() { Label = "Patients on record", Value = patients.ToString("N0"), Icon = "bi-people", Tone = "primary" },
                new() { Label = "Active admissions", Value = admissions.ToString(), Icon = "bi-hospital", Tone = "info" },
                new() { Label = "Beds free", Value = bedsFree.ToString(), Icon = "bi-grid-3x3", Tone = "success" },
                new() { Label = "Clinics", Value = clinics.ToString(), Icon = "bi-buildings", Tone = "info" }
            };
        }

        var meta = StatMeta(p);
        foreach (var (stat, i) in tiles.Select((s, i) => (s, i)))
        {
            if (i >= meta.Length) break;
            stat.Note = meta[i].Note;
            stat.Link = meta[i].Link;
        }
        return tiles;
    }

    private (string Note, CanvasLink? Link)[] StatMeta(Persona p) => p switch
    {
        Persona.Executive => new (string, CanvasLink?)[]
        {
            ("Registered at this facility", To("Index", "Patients", Permissions.PatientsRead)),
            ("Currently on the wards", To("Index", "Admissions", Permissions.AdmissionsManage)),
            ("Open emergency encounters", To("Index", "Emergency", Permissions.EmergencyBoardRead)),
            ("Payments recorded today", To("Bills", "Cashier", Permissions.BillsRead))
        },
        Persona.Clinician => new (string, CanvasLink?)[]
        {
            ("Patients still to see today", To("MyQueue", "Queue", Permissions.QueueServe)),
            ("Inpatients under your care", To("Index", "Admissions", Permissions.AdmissionsManage)),
            ("Authorised, waiting for you", To("Worklist", "Lab", Permissions.LabPerform)),
            ("Started by you today", null)
        },
        Persona.Nursing => new (string, CanvasLink?)[]
        {
            ("Emergency patients not in a bay", To("Index", "Emergency", Permissions.EmergencyBoardRead)),
            ("Includes any overdue doses", To("Index", "Admissions", Permissions.AdmissionsManage)),
            ("Across all wards", To("Board", "Wards", Permissions.WardsManage)),
            ("Antenatal clinic", To("Index", "Maternity", Permissions.AncManage))
        },
        Persona.FrontOffice => new (string, CanvasLink?)[]
        {
            ("Booked across all clinics", To("Index", "Appointments", Permissions.AppointmentsRead)),
            ("Checked in and not finished", To("Index", "Queue", Permissions.QueueRead)),
            ("Added at this facility today", To("Index", "Patients", Permissions.PatientsRead)),
            ("Patients with no verified NIN", To("Index", "Patients", Permissions.PatientsRead))
        },
        Persona.Lab => new (string, CanvasLink?)[]
        {
            ("Ordered and not completed", To("Worklist", "Lab", Permissions.LabPerform)),
            ("Ready for sign-off", To("Worklist", "Lab", Permissions.LabPerform)),
            ("Ordering clinician not yet told", To("Worklist", "Lab", Permissions.LabPerform)),
            ("In the blood bank", To("Index", "BloodBank", Permissions.BloodBankManage))
        },
        Persona.Imaging => new (string, CanvasLink?)[]
        {
            ("Ordered and not completed", To("Worklist", "Imaging", Permissions.ImagingPerform)),
            ("Reported, not yet signed off", To("Worklist", "Imaging", Permissions.ImagingPerform)),
            ("Reports with a critical finding", To("Worklist", "Imaging", Permissions.ImagingPerform)),
            ("Studies carried out today", To("Worklist", "Imaging", Permissions.ImagingPerform))
        },
        Persona.Pharmacy => new (string, CanvasLink?)[]
        {
            ("Issued and not yet dispensed", To("Worklist", "Pharmacy", Permissions.PharmacyDispense)),
            ("At or below reorder level", To("Stock", "Pharmacy", Permissions.PharmacyStock)),
            ("Batches to use or remove", To("Stock", "Pharmacy", Permissions.PharmacyStock)),
            ("Dispenses recorded today", To("Worklist", "Pharmacy", Permissions.PharmacyDispense))
        },
        Persona.Finance => new (string, CanvasLink?)[]
        {
            ("All payment methods", To("Bills", "Cashier", Permissions.BillsRead)),
            ("Cash payments today", To("Shift", "Cashier", Permissions.CashierShiftManage)),
            ("Open and part-paid bills", To("Bills", "Cashier", Permissions.BillsRead)),
            ("Awaiting payer settlement", To("Index", "Claims", Permissions.ClaimsManage))
        },
        Persona.Claims => new (string, CanvasLink?)[]
        {
            ("Not yet sent to a payer", To("Index", "Claims", Permissions.ClaimsManage)),
            ("Submitted, no reply yet", To("Index", "Claims", Permissions.ClaimsManage)),
            ("May be worth disputing", To("Index", "Claims", Permissions.ClaimsManage)),
            ("Received from payers", To("Index", "Payers", Permissions.PayersManage))
        },
        Persona.Hr => new (string, CanvasLink?)[]
        {
            ("At this facility", To("Index", "Hr", Permissions.HrRead)),
            ("Waiting for a decision", To("Leave", "Hr", Permissions.LeaveRequest)),
            ("Practising licence out of date", To("Index", "Hr", Permissions.HrRead)),
            ("Licences due for renewal", To("Index", "Hr", Permissions.HrRead))
        },
        Persona.Procurement => new (string, CanvasLink?)[]
        {
            ("Issued, not fully received", To("Index", "PurchaseOrders", Permissions.PurchaseOrderManage)),
            ("Deliveries booked in today", To("Index", "Grn", Permissions.GrnReceive)),
            ("At or below reorder level", To("Stock", "Inventory", Permissions.InventoryRead)),
            ("Batches to use or remove", To("Stock", "Inventory", Permissions.InventoryRead))
        },
        Persona.PublicHealth => new (string, CanvasLink?)[]
        {
            ("Under investigation", To("Idsr", "Reporting", Permissions.IdsrReport)),
            ("NCDC must be told at once", To("Idsr", "Reporting", Permissions.IdsrReport)),
            ("Due today or earlier", To("Worklist", "Immunization", Permissions.ImmunizationAdminister)),
            ("Monthly return to the state", To("Nhmis", "Reporting", Permissions.NhmisGenerate))
        },
        Persona.Specialty => new (string, CanvasLink?)[]
        {
            ("Booked for today", To("Index", "Allied", Permissions.AlliedSession)),
            ("Scheduled and not started", To("Index", "Allied", Permissions.AlliedSession)),
            ("Being treated now", To("Index", "Allied", Permissions.AlliedSession)),
            ("In the last 7 days", To("Index", "Allied", Permissions.AlliedSession))
        },
        _ => new (string, CanvasLink?)[]
        {
            ("Registered at this facility", To("Index", "Patients", Permissions.PatientsRead)),
            ("Currently on the wards", null),
            ("Available across all wards", null),
            ("Set up at this facility", null)
        }
    };

    // ====================================================================================
    // Persona canvases
    // ====================================================================================
    private async Task<DashboardCanvasVm> ExecutiveCanvasAsync(int fid, ExecutiveBoardVm e)
    {
        var months = LastMonths();
        var collected = await _db.Payments.AsNoTracking()
            .Where(x => x.Bill!.FacilityId == fid && x.Status == PaymentStatus.Recorded && x.ReceivedAt >= months.Start)
            .Select(x => new { x.ReceivedAt, x.Amount }).ToListAsync();
        var billed = await _db.Bills.AsNoTracking()
            .Where(x => x.FacilityId == fid && x.CreatedAt >= months.Start)
            .Select(x => new { x.CreatedAt, x.NetAmount }).ToListAsync();

        var todayStart = DateTime.UtcNow.Date;
        var top = await _db.Encounters.AsNoTracking()
            .Where(x => x.FacilityId == fid && x.StartedAt >= todayStart)
            .GroupBy(x => x.ClinicianId)
            .Select(g => new { ClinicianId = g.Key, Seen = g.Count() })
            .OrderByDescending(x => x.Seen).FirstOrDefaultAsync();
        var clinician = top?.ClinicianId is null ? null : await _db.Users.AsNoTracking().FirstOrDefaultAsync(u => u.Id == top.ClinicianId);
        var latest = e.RecentAdmissions.FirstOrDefault();

        return new DashboardCanvasVm
        {
            Hero = new HeroPanel
            {
                Title = "Hospital today", Icon = "bi-hospital", ClinicalArt = true,
                Link = To("Index", "Analytics", Permissions.AnalyticsView),
                NextLabel = "Latest admission", NextEmpty = "No active admissions",
                Next = latest is null ? null : new HeroNextCard(latest.PatientName, $"{latest.Ward} · Bed {latest.Bed}", RelativeDay(latest.AdmittedAt), $"Admitted at {At(latest.AdmittedAt)}"),
                MetricsTitle = "Across the hospital",
                Metrics = new()
                {
                    new("Visits today", e.VisitsToday.ToString("N0")),
                    new("Visits this month", e.VisitsMonth.ToString("N0")),
                    new("Revenue this month", Naira(e.RevenueMonth))
                }
            },
            Trend = Trend("Hospital revenue", months,
                "Collected", months.Sum(collected.Select(x => (x.ReceivedAt, (double)x.Amount))),
                "Billed", months.Sum(billed.Select(x => (x.CreatedAt, (double)x.NetAmount))), Naira),
            Gauge = Gauge("Bed occupancy", "All wards", Pct(e.BedsOccupied, e.BedsTotal), $"{e.BedsOccupied} of {e.BedsTotal} beds in use",
                "Occupied", e.BedsOccupied, "Free", Math.Max(0, e.BedsTotal - e.BedsOccupied)),
            Spotlight = new SpotlightPanel
            {
                Title = "Busiest clinician today", Icon = "bi-person-badge", Empty = "No consultations started yet today",
                Name = clinician?.FullName, Detail = clinician?.Designation ?? clinician?.Department,
                Badge = clinician is null ? null : "Active", BadgeTone = "success",
                Chips = top is null ? new() : new() { $"{top.Seen} {(top.Seen == 1 ? "patient" : "patients")} seen" }
            }
        };
    }

    private async Task<DashboardCanvasVm> ClinicianCanvasAsync(int fid, string userId, ClinicianBoardVm c)
    {
        var days = LastDays();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;
        var consults = await _db.Encounters.AsNoTracking()
            .Where(x => x.FacilityId == fid && x.ClinicianId == userId && x.StartedAt >= days.Start)
            .Select(x => x.StartedAt).ToListAsync();
        var labs = await _db.LabOrders.AsNoTracking()
            .Where(x => x.Patient!.FacilityId == fid && x.OrderedById == userId && x.OrderedAt >= days.Start)
            .Select(x => x.OrderedAt).ToListAsync();
        var total = await _db.QueueEntries.AsNoTracking().CountAsync(q => q.FacilityId == fid && q.TicketDate == today && q.ClinicianId == userId
            && q.Status != QueueStatus.Skipped && q.Status != QueueStatus.LeftWithoutBeingSeen);
        var done = await _db.QueueEntries.AsNoTracking().CountAsync(q => q.FacilityId == fid && q.TicketDate == today && q.ClinicianId == userId && q.Status == QueueStatus.Completed);

        var appointment = c.TodaysAppointments.FirstOrDefault(a => a.ScheduledAt >= now && IsUpcoming(a.Status));
        var waiting = c.WaitingList.FirstOrDefault();
        var urgent = c.WaitingList.Count(w => w.Priority is "Stat" or "Urgent");

        return new DashboardCanvasVm
        {
            Hero = new HeroPanel
            {
                Title = "My clinic", Icon = "bi-heart-pulse", ClinicalArt = true,
                Link = To("MyQueue", "Queue", Permissions.QueueServe),
                NextLabel = "Next appointment", NextEmpty = "No more appointments today",
                Next = appointment is null ? null : new HeroNextCard(appointment.PatientName, appointment.Clinic ?? "Clinic", RelativeDay(appointment.ScheduledAt), $"{At(appointment.ScheduledAt)} · {appointment.Status}"),
                MetricsTitle = "My queue now",
                Metrics = new()
                {
                    new("Longest wait", c.WaitingList.Count == 0 ? "None" : $"{c.WaitingList.Max(w => w.WaitingMinutes)} min"),
                    new("Urgent patients", urgent.ToString()),
                    new("Results to review", Capped(c.LabsToReview.Count, 10))
                }
            },
            Trend = Trend("My activity", days, "Consultations", days.Tally(consults), "Lab requests", days.Tally(labs)),
            Gauge = Gauge("Today's patients", "My queue", total.ToString("N0"), "Patients today", "Seen", done, "Still to see", total - done),
            Spotlight = new SpotlightPanel
            {
                Title = "Next patient", Icon = "bi-person-lines-fill", Empty = "No one is waiting for you right now",
                Name = waiting?.PatientName, Detail = waiting?.Complaint ?? (waiting is null ? null : "No complaint recorded"),
                Badge = waiting?.Priority, BadgeTone = waiting?.Priority is "Stat" or "Urgent" ? "danger" : "info",
                Chips = waiting is null ? new() : new() { $"Ticket {waiting.TicketNo}", $"Waiting {waiting.WaitingMinutes} min" },
                Link = waiting is null ? null : To("OpenFromQueue", "Encounters", Permissions.EncountersWrite, new { queueEntryId = waiting.QueueEntryId })
            }
        };
    }

    private async Task<DashboardCanvasVm> NursingCanvasAsync(int fid, NursingBoardVm n)
    {
        var days = LastDays();
        var admitted = await _db.Admissions.AsNoTracking()
            .Where(x => x.FacilityId == fid && x.AdmittedAt >= days.Start).Select(x => x.AdmittedAt).ToListAsync();
        var discharged = await _db.Admissions.AsNoTracking()
            .Where(x => x.FacilityId == fid && x.DischargedAt >= days.Start).Select(x => x.DischargedAt!.Value).ToListAsync();

        var dose = n.MedicationDue.FirstOrDefault();
        var occupied = n.Wards.Sum(w => w.Occupied);
        var beds = n.Wards.Sum(w => w.Total);
        var fullest = n.Wards.Where(w => w.Total > 0).OrderByDescending(w => (double)w.Occupied / w.Total).FirstOrDefault();

        return new DashboardCanvasVm
        {
            Hero = new HeroPanel
            {
                Title = "Ward care", Icon = "bi-clipboard2-pulse", ClinicalArt = true,
                Link = To("Board", "Wards", Permissions.WardsManage),
                NextLabel = "Next dose", NextEmpty = "No doses due in the next 2 hours",
                Next = dose is null ? null : new HeroNextCard(dose.PatientName, $"Bed {dose.Bed} · {dose.Drug} {dose.Dose} {dose.Route}", dose.Overdue ? "Overdue" : "Due soon", $"Scheduled {At(dose.ScheduledUtc)}"),
                MetricsTitle = "Needs attention",
                Metrics = new()
                {
                    new("Overdue doses", Capped(n.MedicationDue.Count(m => m.Overdue), BoardQueueCap)),
                    new("Vitals overdue", Capped(n.VitalsDue.Count, BoardWardListCap)),
                    new("Awaiting triage", Capped(n.TriageQueue.Count, BoardWardListCap))
                }
            },
            Trend = Trend("Ward flow", days, "Admissions", days.Tally(admitted), "Discharges", days.Tally(discharged)),
            Gauge = Gauge("Bed occupancy", "All wards", Pct(occupied, beds), $"{occupied} of {beds} beds in use", "Occupied", occupied, "Free", Math.Max(0, beds - occupied)),
            Spotlight = new SpotlightPanel
            {
                Title = "Fullest ward", Icon = "bi-hospital", Empty = "No wards set up yet",
                Name = fullest?.WardName, Detail = fullest is null ? null : $"{fullest.Occupied} of {fullest.Total} beds occupied",
                Badge = fullest is null ? null : fullest.Free == 0 ? "Full" : $"{fullest.Free} free",
                BadgeTone = fullest?.Free == 0 ? "danger" : "success",
                Chips = fullest is null ? new() : new() { Pct(fullest.Occupied, fullest.Total) + " occupied" },
                Link = To("Board", "Wards", Permissions.WardsManage)
            }
        };
    }

    private async Task<DashboardCanvasVm> FrontOfficeCanvasAsync(int fid, FrontOfficeBoardVm f)
    {
        var days = LastDays();
        var now = DateTime.UtcNow;
        var registered = await _db.Patients.AsNoTracking()
            .Where(x => x.FacilityId == fid && x.CreatedAt >= days.Start && !x.IsMergedAlias).Select(x => x.CreatedAt).ToListAsync();
        var booked = await _db.Appointments.AsNoTracking()
            .Where(x => x.FacilityId == fid && x.ScheduledStartUtc >= days.Start && x.ScheduledStartUtc < now.Date.AddDays(1))
            .Select(x => x.ScheduledStartUtc).ToListAsync();

        var next = f.TodaysAppointments.FirstOrDefault(a => a.ScheduledAt >= now && IsUpcoming(a.Status));
        var waiting = f.QueueByClinic.Sum(c => c.Waiting);
        var inRoom = f.QueueByClinic.Sum(c => c.InRoom);
        var done = f.QueueByClinic.Sum(c => c.Done);
        var busiest = f.QueueByClinic.OrderByDescending(c => c.Waiting + c.InRoom).FirstOrDefault(c => c.Waiting + c.InRoom > 0);

        return new DashboardCanvasVm
        {
            Hero = new HeroPanel
            {
                Title = "Front desk", Icon = "bi-door-open",
                Link = To("Index", "Queue", Permissions.QueueRead),
                NextLabel = "Next arrival", NextEmpty = "No more appointments today",
                Next = next is null ? null : new HeroNextCard(next.PatientName, next.Clinic ?? "Clinic", RelativeDay(next.ScheduledAt), $"{At(next.ScheduledAt)} · {next.Status}"),
                MetricsTitle = "Patient flow",
                Metrics = new() { new("Waiting", waiting.ToString()), new("With a clinician", inRoom.ToString()), new("Finished", done.ToString()) }
            },
            Trend = Trend("Patient traffic", days, "Appointments", days.Tally(booked), "Registrations", days.Tally(registered)),
            Gauge = Gauge("Today's queue", "All clinics", (waiting + inRoom + done).ToString("N0"), "Patients checked in", "Finished", done, "Still in clinic", waiting + inRoom),
            Spotlight = new SpotlightPanel
            {
                Title = "Busiest clinic", Icon = "bi-buildings", Empty = "No patients in any clinic right now",
                Name = busiest?.ClinicName, Detail = busiest is null ? null : $"{busiest.Done} finished today",
                Badge = busiest is null ? null : "Open", BadgeTone = "success",
                Chips = busiest is null ? new() : new() { $"{busiest.Waiting} waiting", $"{busiest.InRoom} with clinician" },
                Link = To("Index", "Queue", Permissions.QueueRead)
            }
        };
    }

    private async Task<DashboardCanvasVm> LabCanvasAsync(int fid, LabBoardVm l)
    {
        var days = LastDays();
        var todayStart = DateTime.UtcNow.Date;
        var orders = await _db.LabOrders.AsNoTracking()
            .Where(x => x.Patient!.FacilityId == fid && x.OrderedAt >= days.Start).Select(x => new { x.OrderedAt, x.Status }).ToListAsync();
        var authorised = await _db.LabResults.AsNoTracking()
            .Where(x => x.LabOrder!.Patient!.FacilityId == fid && x.AuthorizedAt >= days.Start).Select(x => x.AuthorizedAt!.Value).ToListAsync();

        var todays = orders.Where(x => x.OrderedAt >= todayStart && x.Status != OrderStatus.Cancelled).ToList();
        var done = todays.Count(x => x.Status == OrderStatus.Completed);
        var oldest = l.OpenSamples.FirstOrDefault();
        var critical = l.CriticalUnnotified.FirstOrDefault();
        var waiting = l.AwaitingAuthorisation.FirstOrDefault();

        return new DashboardCanvasVm
        {
            Hero = new HeroPanel
            {
                Title = "Laboratory", Icon = "bi-droplet-half",
                Link = To("Worklist", "Lab", Permissions.LabPerform),
                NextLabel = "Oldest open request", NextEmpty = "No open requests",
                Next = oldest is null ? null : new HeroNextCard(oldest.PatientName, oldest.Tests, RelativeDay(oldest.OrderedAt), $"{At(oldest.OrderedAt)} · {oldest.Status}"),
                MetricsTitle = "Bench status",
                Metrics = new()
                {
                    new("Requested today", todays.Count.ToString()),
                    new("Of those, completed", done.ToString()),
                    new("Critical to notify", Capped(l.CriticalUnnotified.Count, BoardWardListCap))
                }
            },
            Trend = Trend("Lab throughput", days, "Requests", days.Tally(orders.Select(x => x.OrderedAt)), "Authorised", days.Tally(authorised)),
            Gauge = Gauge("Today's requests", "Excludes cancelled", todays.Count.ToString("N0"), "Requests today", "Completed", done, "In progress", todays.Count - done),
            Spotlight = critical is not null
                ? new SpotlightPanel
                {
                    Title = "Critical result", Icon = "bi-exclamation-octagon",
                    Name = critical.PatientName, Detail = critical.Test, Badge = "Critical", BadgeTone = "danger",
                    Chips = new() { "Clinician not told", RelativeDay(critical.FlaggedAt) },
                    Link = To("View", "Lab", Permissions.LabRead, new { id = critical.LabOrderId })
                }
                : new SpotlightPanel
                {
                    Title = "Next to authorise", Icon = "bi-shield-check", Empty = "Nothing waiting for sign-off",
                    Name = waiting?.PatientName, Detail = waiting?.Tests, Badge = waiting is null ? null : "Ready", BadgeTone = "info",
                    Chips = waiting is null ? new() : new() { RelativeDay(waiting.OrderedAt) },
                    Link = waiting is null ? null : To("View", "Lab", Permissions.LabRead, new { id = waiting.LabOrderId })
                }
        };
    }

    private async Task<DashboardCanvasVm> ImagingCanvasAsync(int fid)
    {
        var days = LastDays();
        var todayStart = DateTime.UtcNow.Date;
        var orders = await _db.ImagingOrders.AsNoTracking()
            .Where(x => x.Patient!.FacilityId == fid && x.OrderedAt >= days.Start).Select(x => new { x.OrderedAt, x.Status }).ToListAsync();
        var reported = await _db.ImagingReports.AsNoTracking()
            .Where(x => x.ImagingOrder!.Patient!.FacilityId == fid && x.ReportedAt >= days.Start).Select(x => x.ReportedAt!.Value).ToListAsync();
        var pending = _db.ImagingOrders.AsNoTracking().Include(x => x.Patient)
            .Where(x => x.Patient!.FacilityId == fid && x.Status != OrderStatus.Completed && x.Status != OrderStatus.Cancelled);
        var oldest = await pending.OrderBy(x => x.OrderedAt).FirstOrDefaultAsync();
        var urgent = await pending.OrderByDescending(x => x.Urgency).ThenBy(x => x.OrderedAt).FirstOrDefaultAsync();

        var todays = orders.Where(x => x.OrderedAt >= todayStart && x.Status != OrderStatus.Cancelled).ToList();
        var done = todays.Count(x => x.Status == OrderStatus.Completed);

        return new DashboardCanvasVm
        {
            Hero = new HeroPanel
            {
                Title = "Imaging", Icon = "bi-radioactive",
                Link = To("Worklist", "Imaging", Permissions.ImagingPerform),
                NextLabel = "Oldest open study", NextEmpty = "No studies pending",
                Next = oldest is null ? null : new HeroNextCard(oldest.Patient?.FullName ?? "Unknown", $"{oldest.Modality} · {oldest.StudyDescription}", RelativeDay(oldest.OrderedAt), $"Ordered {At(oldest.OrderedAt)}"),
                MetricsTitle = "Today",
                Metrics = new() { new("Requested", todays.Count.ToString()), new("Of those, completed", done.ToString()), new("Reported (14 days)", reported.Count.ToString()) }
            },
            Trend = Trend("Imaging throughput", days, "Requests", days.Tally(orders.Select(x => x.OrderedAt)), "Reported", days.Tally(reported)),
            Gauge = Gauge("Today's studies", "Excludes cancelled", todays.Count.ToString("N0"), "Studies today", "Completed", done, "In progress", todays.Count - done),
            Spotlight = new SpotlightPanel
            {
                Title = "Most urgent study", Icon = "bi-camera", Empty = "No studies pending",
                Name = urgent?.Patient?.FullName, Detail = urgent is null ? null : $"{urgent.Modality} · {urgent.StudyDescription}",
                Badge = urgent?.Urgency.ToString(), BadgeTone = urgent?.Urgency.ToString() is "Stat" or "Urgent" ? "danger" : "info",
                Chips = urgent is null ? new() : new() { RelativeDay(urgent.OrderedAt), $"Ordered {At(urgent.OrderedAt)}" },
                Link = urgent is null ? null : To("View", "Imaging", Permissions.ImagingRead, new { id = urgent.Id })
            }
        };
    }

    private async Task<DashboardCanvasVm> PharmacyCanvasAsync(int fid, PharmacyBoardVm p)
    {
        var days = LastDays();
        var todayStart = DateTime.UtcNow.Date;
        var issued = await _db.Prescriptions.AsNoTracking()
            .Where(x => x.Patient!.FacilityId == fid && x.IssuedAt >= days.Start).Select(x => new { x.IssuedAt, x.Status }).ToListAsync();
        var dispensed = await _db.Dispenses.AsNoTracking()
            .Where(x => x.FacilityId == fid && x.DispensedAt >= days.Start).Select(x => x.DispensedAt).ToListAsync();

        var todays = issued.Where(x => x.IssuedAt >= todayStart && x.Status != PrescriptionStatus.Cancelled).ToList();
        var done = todays.Count(x => x.Status == PrescriptionStatus.Dispensed);
        var oldest = p.PendingPrescriptions.FirstOrDefault();
        var low = p.LowStock.FirstOrDefault();

        return new DashboardCanvasVm
        {
            Hero = new HeroPanel
            {
                Title = "Pharmacy", Icon = "bi-capsule",
                Link = To("Worklist", "Pharmacy", Permissions.PharmacyDispense),
                NextLabel = "Longest waiting", NextEmpty = "No prescriptions waiting",
                Next = oldest is null ? null : new HeroNextCard(oldest.PatientName, $"{oldest.ItemCount} item(s) · {oldest.Prescriber ?? "Prescriber not recorded"}", RelativeDay(oldest.PrescribedAt), $"Issued {At(oldest.PrescribedAt)}"),
                MetricsTitle = "Today",
                Metrics = new()
                {
                    new("Issued", todays.Count.ToString()),
                    new("Items to dispense", p.PendingPrescriptions.Sum(x => x.ItemCount) + (p.PendingPrescriptions.Count >= BoardQueueCap ? "+" : "")),
                    new("Items dispensed", p.TodaysDispenses.Sum(x => x.Items) + (p.TodaysDispenses.Count >= 10 ? "+" : ""))
                }
            },
            Trend = Trend("Dispensing", days, "Prescriptions", days.Tally(issued.Select(x => x.IssuedAt)), "Dispensed", days.Tally(dispensed)),
            Gauge = Gauge("Today's prescriptions", "Excludes cancelled", todays.Count.ToString("N0"), "Prescriptions today", "Dispensed", done, "Waiting", todays.Count - done),
            Spotlight = new SpotlightPanel
            {
                Title = "Lowest stock", Icon = "bi-box-seam", Empty = "All drugs are above reorder level",
                Name = low?.DrugName, Detail = low?.StoreName, Badge = low is null ? null : low.Quantity == 0 ? "Out of stock" : "Reorder",
                BadgeTone = low?.Quantity == 0 ? "danger" : "warning",
                Chips = low is null ? new() : new() { $"{low.Quantity} on hand", $"Reorder at {low.ReorderPoint}" },
                Link = To("Stock", "Pharmacy", Permissions.PharmacyStock)
            }
        };
    }

    private async Task<DashboardCanvasVm> FinanceCanvasAsync(int fid, FinanceBoardVm f)
    {
        var months = LastMonths();
        var monthStart = months.Start.AddMonths(months.Count - 1);
        var collected = await _db.Payments.AsNoTracking()
            .Where(x => x.Bill!.FacilityId == fid && x.Status == PaymentStatus.Recorded && x.ReceivedAt >= months.Start)
            .Select(x => new { x.ReceivedAt, x.Amount }).ToListAsync();
        var billed = await _db.Bills.AsNoTracking()
            .Where(x => x.FacilityId == fid && x.CreatedAt >= months.Start)
            .Select(x => new { x.CreatedAt, x.NetAmount }).ToListAsync();

        var collectedMonth = collected.Where(x => x.ReceivedAt >= monthStart).Sum(x => x.Amount);
        var billedMonth = billed.Where(x => x.CreatedAt >= monthStart).Sum(x => x.NetAmount);
        var uncollected = Math.Max(0, billedMonth - collectedMonth);
        var largest = f.OpenBills.OrderByDescending(b => b.Outstanding).FirstOrDefault();
        var latest = f.RecentPayments.FirstOrDefault();
        var metrics = f.CollectedByMethod.OrderByDescending(m => m.Value).Take(3).Select(m => new CanvasMetric(m.Key, Naira(m.Value))).ToList();
        if (metrics.Count == 0) metrics.Add(new CanvasMetric("Collected", Naira(0m)));

        return new DashboardCanvasVm
        {
            Hero = new HeroPanel
            {
                Title = "Cash office", Icon = "bi-cash-stack",
                Link = To("Bills", "Cashier", Permissions.BillsRead),
                NextLabel = "Largest recent balance", NextEmpty = "No open bills",
                Next = largest is null ? null : new HeroNextCard(largest.PatientName, $"{Naira(largest.Outstanding)} of {Naira(largest.Total)}", RelativeDay(largest.OpenedAt), "Open bill"),
                MetricsTitle = "Collected today",
                Metrics = metrics
            },
            Trend = Trend("Revenue", months,
                "Collected", months.Sum(collected.Select(x => (x.ReceivedAt, (double)x.Amount))),
                "Billed", months.Sum(billed.Select(x => (x.CreatedAt, (double)x.NetAmount))), Naira),
            Gauge = Gauge("Collection rate", "This month", billedMonth == 0 ? "0%" : $"{Math.Min(100, Math.Round(100 * collectedMonth / billedMonth)):0}%", $"{Naira(collectedMonth)} collected",
                "Collected", (int)Math.Min(int.MaxValue, Math.Min(collectedMonth, billedMonth)), "Uncollected", (int)Math.Min(int.MaxValue, uncollected)),
            Spotlight = new SpotlightPanel
            {
                Title = "Latest payment", Icon = "bi-cash-coin", Empty = "No payments recorded today",
                Name = latest?.PatientName, Detail = latest is null ? null : Naira(latest.Amount),
                Badge = latest is null ? null : "Received", BadgeTone = "success",
                Chips = latest is null ? new() : new() { latest.Method, At(latest.At) },
                Link = To("Bills", "Cashier", Permissions.BillsRead)
            }
        };
    }

    private async Task<DashboardCanvasVm> ClaimsCanvasAsync(int fid)
    {
        var days = LastDays();
        var quarter = DateTime.UtcNow.AddDays(-90);
        var submitted = await _db.Claims.AsNoTracking().Where(c => c.FacilityId == fid && c.SubmittedAt >= days.Start).Select(c => c.SubmittedAt!.Value).ToListAsync();
        var responded = await _db.Claims.AsNoTracking().Where(c => c.FacilityId == fid && c.RespondedAt >= days.Start).Select(c => c.RespondedAt!.Value).ToListAsync();
        var paid = await _db.Claims.AsNoTracking().CountAsync(c => c.FacilityId == fid && c.RespondedAt >= quarter && (c.Status == ClaimStatus.Paid || c.Status == ClaimStatus.PartiallyPaid));
        var denied = await _db.Claims.AsNoTracking().CountAsync(c => c.FacilityId == fid && c.RespondedAt >= quarter && c.Status == ClaimStatus.Denied);
        var draftValue = await _db.Claims.AsNoTracking().Where(c => c.FacilityId == fid && c.Status == ClaimStatus.Draft).SumAsync(c => (decimal?)c.ClaimableAmount) ?? 0m;
        var awaiting = _db.Claims.AsNoTracking().Include(c => c.Patient).Include(c => c.Payer)
            .Where(c => c.FacilityId == fid && (c.Status == ClaimStatus.Submitted || c.Status == ClaimStatus.Acknowledged));
        var awaitingValue = await awaiting.SumAsync(c => (decimal?)c.ClaimableAmount) ?? 0m;
        var deniedValue = await _db.Claims.AsNoTracking().Where(c => c.FacilityId == fid && c.Status == ClaimStatus.Denied).SumAsync(c => (decimal?)c.ClaimableAmount) ?? 0m;
        var draft = await _db.Claims.AsNoTracking().Include(c => c.Patient).Include(c => c.Payer)
            .Where(c => c.FacilityId == fid && c.Status == ClaimStatus.Draft).OrderBy(c => c.CreatedAt).FirstOrDefaultAsync();
        var largest = await awaiting.OrderByDescending(c => c.ClaimableAmount).FirstOrDefaultAsync();

        return new DashboardCanvasVm
        {
            Hero = new HeroPanel
            {
                Title = "Claims desk", Icon = "bi-file-earmark-medical",
                Link = To("Index", "Claims", Permissions.ClaimsManage),
                NextLabel = "Oldest draft", NextEmpty = "No draft claims",
                Next = draft is null ? null : new HeroNextCard(draft.Patient?.FullName ?? "Unknown", $"{draft.Payer?.Name ?? "Payer"} · {Naira(draft.ClaimableAmount)}", RelativeDay(draft.CreatedAt), "Not yet submitted"),
                MetricsTitle = "Claim value",
                Metrics = new() { new("In draft", Naira(draftValue)), new("Awaiting payer", Naira(awaitingValue)), new("Denied", Naira(deniedValue)) }
            },
            Trend = Trend("Claims movement", days, "Submitted", days.Tally(submitted), "Payer responses", days.Tally(responded)),
            Gauge = Gauge("Payment rate", "Last 90 days", Pct(paid, paid + denied), "Of answered claims paid", "Paid", paid, "Denied", denied),
            Spotlight = new SpotlightPanel
            {
                Title = "Largest awaiting payer", Icon = "bi-hourglass-split", Empty = "No claims awaiting a payer",
                Name = largest?.Patient?.FullName, Detail = largest?.Payer?.Name, Badge = largest?.Status.ToString(), BadgeTone = "info",
                Chips = largest is null ? new() : new() { Naira(largest.ClaimableAmount), largest.SubmittedAt is null ? "Not sent" : $"Sent {RelativeDay(largest.SubmittedAt.Value)}" },
                Link = To("Index", "Claims", Permissions.ClaimsManage)
            }
        };
    }

    private async Task<DashboardCanvasVm> HrCanvasAsync(int fid)
    {
        var days = LastDays();
        var now = DateTime.UtcNow;
        var today = DateOnly.FromDateTime(now);
        var renewalCutoff = now.AddDays(60);
        var leave = await _db.LeaveRequests.AsNoTracking().Where(l => l.Staff!.FacilityId == fid && l.CreatedAt >= days.Start).Select(l => l.CreatedAt).ToListAsync();
        var joined = await _db.Users.AsNoTracking().Where(u => u.FacilityId == fid && u.CreatedAt >= days.Start).Select(u => u.CreatedAt).ToListAsync();
        var onLeave = await _db.LeaveRequests.AsNoTracking().CountAsync(l => l.Staff!.FacilityId == fid && l.Status == LeaveStatus.Approved && l.StartDate <= today && l.EndDate >= today);
        var upcoming = await _db.LeaveRequests.AsNoTracking().CountAsync(l => l.Staff!.FacilityId == fid && l.Status == LeaveStatus.Approved && l.StartDate > today && l.StartDate <= today.AddDays(30));
        var licensed = await _db.Users.AsNoTracking().CountAsync(u => u.FacilityId == fid && u.IsActive && u.LicenseExpiry.HasValue);
        var attention = await _db.Users.AsNoTracking().CountAsync(u => u.FacilityId == fid && u.IsActive && u.LicenseExpiry.HasValue && u.LicenseExpiry <= renewalCutoff);
        var request = await _db.LeaveRequests.AsNoTracking().Include(l => l.Staff)
            .Where(l => l.Staff!.FacilityId == fid && l.Status == LeaveStatus.Submitted).OrderBy(l => l.StartDate).FirstOrDefaultAsync();
        var expiring = await _db.Users.AsNoTracking()
            .Where(u => u.FacilityId == fid && u.IsActive && u.LicenseExpiry.HasValue).OrderBy(u => u.LicenseExpiry).FirstOrDefaultAsync();
        var expired = expiring?.LicenseExpiry < now;

        return new DashboardCanvasVm
        {
            Hero = new HeroPanel
            {
                Title = "People", Icon = "bi-person-vcard",
                Link = To("Index", "Hr", Permissions.HrRead),
                NextLabel = "Next leave decision", NextEmpty = "No leave requests waiting",
                Next = request is null ? null : new HeroNextCard(request.Staff?.FullName ?? "Staff member", $"{request.Type} · {request.Days} day(s)", RelativeDay(request.StartDate), "Requested start date"),
                MetricsTitle = "Staffing",
                Metrics = new() { new("On leave today", onLeave.ToString()), new("Leave in next 30 days", upcoming.ToString()), new("Joined (14 days)", joined.Count.ToString()) }
            },
            Trend = Trend("Staff movement", days, "Leave requests", days.Tally(leave), "New staff", days.Tally(joined)),
            Gauge = Gauge("Licence compliance", "Staff with a licence on file", Pct(licensed - attention, licensed), "Valid beyond 60 days", "Valid", licensed - attention, "Expired or due", attention),
            Spotlight = new SpotlightPanel
            {
                Title = "Next licence to renew", Icon = "bi-shield-exclamation", Empty = "No licence dates on file",
                Name = expiring?.FullName, Detail = expiring?.Designation ?? expiring?.Department,
                Badge = expiring is null ? null : expired ? "Expired" : expiring.LicenseExpiry <= renewalCutoff ? "Renew soon" : "Valid",
                BadgeTone = expiring is null ? "info" : expired ? "danger" : expiring.LicenseExpiry <= renewalCutoff ? "warning" : "success",
                Chips = expiring is null ? new() : new() { $"{expiring.LicenseBody ?? "Licence"}", expiring.LicenseExpiry!.Value.ToString("d MMM yyyy") },
                Link = To("Index", "Hr", Permissions.HrRead)
            }
        };
    }

    private async Task<DashboardCanvasVm> ProcurementCanvasAsync(int fid)
    {
        var days = LastDays();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var quarter = DateTime.UtcNow.AddDays(-90);
        var raised = await _db.PurchaseOrders.AsNoTracking().Where(o => o.FacilityId == fid && o.CreatedAt >= days.Start).Select(o => o.CreatedAt).ToListAsync();
        var received = await _db.Grns.AsNoTracking().Where(g => g.FacilityId == fid && g.ReceivedAt >= days.Start).Select(g => g.ReceivedAt).ToListAsync();
        var open = _db.PurchaseOrders.AsNoTracking().Include(o => o.Supplier)
            .Where(o => o.FacilityId == fid && (o.Status == PurchaseOrderStatus.Issued || o.Status == PurchaseOrderStatus.PartiallyReceived));
        var openValue = await open.SumAsync(o => (decimal?)o.TotalAmount) ?? 0m;
        var overdue = await open.CountAsync(o => o.ExpectedDate < today);
        var drafts = await _db.PurchaseOrders.AsNoTracking().CountAsync(o => o.FacilityId == fid && o.Status == PurchaseOrderStatus.Draft);
        var oldest = await open.OrderBy(o => o.ExpectedDate).ThenBy(o => o.CreatedAt).FirstOrDefaultAsync();
        var completed = await _db.PurchaseOrders.AsNoTracking().CountAsync(o => o.FacilityId == fid && o.CreatedAt >= quarter && (o.Status == PurchaseOrderStatus.Received || o.Status == PurchaseOrderStatus.Closed));
        var outstanding = await open.CountAsync(o => o.CreatedAt >= quarter);
        var low = await _db.InventoryStocks.AsNoTracking().Include(s => s.InventoryItem).Include(s => s.Store)
            .Where(s => s.Store!.FacilityId == fid && s.InventoryItem!.ReorderLevel.HasValue && s.QuantityOnHand <= s.InventoryItem.ReorderLevel)
            .OrderBy(s => s.QuantityOnHand).FirstOrDefaultAsync();

        return new DashboardCanvasVm
        {
            Hero = new HeroPanel
            {
                Title = "Supply chain", Icon = "bi-truck",
                Link = To("Index", "PurchaseOrders", Permissions.PurchaseOrderManage),
                NextLabel = "Next delivery due", NextEmpty = "No open purchase orders",
                Next = oldest is null ? null : new HeroNextCard(oldest.Supplier?.Name ?? "Supplier", oldest.PoNumber, oldest.ExpectedDate.HasValue ? RelativeDay(oldest.ExpectedDate.Value) : "No date", Naira(oldest.TotalAmount)),
                MetricsTitle = "Purchase orders",
                Metrics = new() { new("Open value", Naira(openValue)), new("Overdue", overdue.ToString()), new("Drafts", drafts.ToString()) }
            },
            Trend = Trend("Orders and deliveries", days, "Orders raised", days.Tally(raised), "Goods received", days.Tally(received)),
            Gauge = Gauge("Order fulfilment", "Last 90 days", Pct(completed, completed + outstanding), "Of issued orders received", "Received", completed, "Outstanding", outstanding),
            Spotlight = new SpotlightPanel
            {
                Title = "Lowest stock", Icon = "bi-box-seam", Empty = "All items are above reorder level",
                Name = low?.InventoryItem?.Name, Detail = low?.Store?.Name, Badge = low is null ? null : low.QuantityOnHand == 0 ? "Out of stock" : "Reorder",
                BadgeTone = low?.QuantityOnHand == 0 ? "danger" : "warning",
                Chips = low is null ? new() : new() { $"{low.QuantityOnHand} on hand", $"Reorder at {low.InventoryItem?.ReorderLevel}" },
                Link = To("Stock", "Inventory", Permissions.InventoryRead)
            }
        };
    }

    private async Task<DashboardCanvasVm> PublicHealthCanvasAsync(int fid)
    {
        var days = LastDays();
        var now = DateTime.UtcNow;
        var monthStart = new DateTime(now.Year, now.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var quarter = now.AddDays(-90);
        var cases = await _db.IdsrCases.AsNoTracking().Where(c => c.FacilityId == fid && c.ReportedAt >= days.Start).Select(c => c.ReportedAt).ToListAsync();
        var doses = await _db.ImmunizationDoses.AsNoTracking()
            .Where(d => d.FacilityId == fid && d.Status == DoseStatus.Administered && d.AdministeredAt >= days.Start).Select(d => d.AdministeredAt!.Value).ToListAsync();
        var thisMonth = await _db.IdsrCases.AsNoTracking().CountAsync(c => c.FacilityId == fid && c.ReportedAt >= monthStart);
        var closed = await _db.IdsrCases.AsNoTracking().CountAsync(c => c.FacilityId == fid && c.ReportedAt >= monthStart && c.Status == IdsrCaseStatus.Closed);
        var firstOfMonth = DateOnly.FromDateTime(monthStart);
        var missed = await _db.ImmunizationDoses.AsNoTracking().CountAsync(d => d.FacilityId == fid && d.Status == DoseStatus.Missed && d.DueDate >= firstOfMonth);
        var notified = await _db.IdsrCases.AsNoTracking().CountAsync(c => c.FacilityId == fid && c.ReportedAt >= quarter && c.NotifiedNcdc);
        var notNotified = await _db.IdsrCases.AsNoTracking().CountAsync(c => c.FacilityId == fid && c.ReportedAt >= quarter && !c.NotifiedNcdc);
        var latest = await _db.IdsrCases.AsNoTracking().Include(c => c.NotifiableDisease)
            .Where(c => c.FacilityId == fid && c.Status == IdsrCaseStatus.Open).OrderByDescending(c => c.ReportedAt).FirstOrDefaultAsync();
        var urgent = await _db.IdsrCases.AsNoTracking().Include(c => c.NotifiableDisease)
            .Where(c => c.FacilityId == fid && !c.NotifiedNcdc && c.NotifiableDisease!.Window == NotificationWindow.Immediate)
            .OrderBy(c => c.ReportedAt).FirstOrDefaultAsync();

        return new DashboardCanvasVm
        {
            Hero = new HeroPanel
            {
                Title = "Public health", Icon = "bi-shield-shaded",
                Link = To("Idsr", "Reporting", Permissions.IdsrReport),
                NextLabel = "Latest open case", NextEmpty = "No open cases",
                Next = latest is null ? null : new HeroNextCard(latest.PatientName, latest.NotifiableDisease?.Name ?? "Notifiable disease", RelativeDay(latest.ReportedAt), latest.CaseNumber),
                MetricsTitle = "This month",
                Metrics = new() { new("Cases reported", thisMonth.ToString()), new("Cases closed", closed.ToString()), new("Vaccine doses missed", missed.ToString()) }
            },
            Trend = Trend("Surveillance and vaccines", days, "Cases reported", days.Tally(cases), "Doses given", days.Tally(doses)),
            Gauge = Gauge("NCDC notification", "Last 90 days", Pct(notified, notified + notNotified), "Of cases notified", "Notified", notified, "Not notified", notNotified),
            Spotlight = new SpotlightPanel
            {
                Title = "Oldest urgent notification", Icon = "bi-broadcast", Empty = "No immediate notifications outstanding",
                Name = urgent?.PatientName, Detail = urgent?.NotifiableDisease?.Name,
                Badge = urgent is null ? null : "Notify now", BadgeTone = "danger",
                Chips = urgent is null ? new() : new() { urgent.CaseNumber, RelativeDay(urgent.ReportedAt) },
                Link = To("Idsr", "Reporting", Permissions.IdsrReport)
            }
        };
    }

    private async Task<DashboardCanvasVm> SpecialtyCanvasAsync(int fid)
    {
        var days = LastDays();
        var now = DateTime.UtcNow;
        var todayStart = now.Date;
        var tomorrow = todayStart.AddDays(1);
        var scheduled = await _db.AlliedSessions.AsNoTracking()
            .Where(s => s.FacilityId == fid && s.ScheduledUtc >= days.Start && s.ScheduledUtc < tomorrow).Select(s => s.ScheduledUtc).ToListAsync();
        var completed = await _db.AlliedSessions.AsNoTracking()
            .Where(s => s.FacilityId == fid && s.CompletedUtc >= days.Start).Select(s => s.CompletedUtc!.Value).ToListAsync();
        var todays = await _db.AlliedSessions.AsNoTracking().Include(s => s.Patient)
            .Where(s => s.FacilityId == fid && s.ScheduledUtc >= todayStart && s.ScheduledUtc < tomorrow && s.Status != SessionStatus.Cancelled)
            .OrderBy(s => s.ScheduledUtc).ToListAsync();
        var done = todays.Count(s => s.Status == SessionStatus.Completed);
        var noShow = todays.Count(s => s.Status == SessionStatus.NoShow);
        var next = todays.FirstOrDefault(s => s.Status == SessionStatus.Scheduled && s.ScheduledUtc >= now);
        var active = todays.FirstOrDefault(s => s.Status == SessionStatus.InProgress) ?? next;

        return new DashboardCanvasVm
        {
            Hero = new HeroPanel
            {
                Title = "Therapy sessions", Icon = "bi-clipboard2-pulse", ClinicalArt = true,
                Link = To("Index", "Allied", Permissions.AlliedSession),
                NextLabel = "Next session", NextEmpty = "No more sessions today",
                Next = next is null ? null : new HeroNextCard(next.Patient?.FullName ?? "Unknown", next.ServiceLine.ToString(), RelativeDay(next.ScheduledUtc), At(next.ScheduledUtc)),
                MetricsTitle = "Today",
                Metrics = new() { new("Booked", todays.Count.ToString()), new("Completed", done.ToString()), new("No-shows", noShow.ToString()) }
            },
            Trend = Trend("Session activity", days, "Scheduled", days.Tally(scheduled), "Completed", days.Tally(completed)),
            Gauge = Gauge("Today's sessions", "Excludes cancelled", todays.Count.ToString("N0"), "Sessions today", "Completed", done, "Remaining", todays.Count - done),
            Spotlight = new SpotlightPanel
            {
                Title = active?.Status == SessionStatus.InProgress ? "In session now" : "Next patient", Icon = "bi-person-lines-fill", Empty = "No sessions waiting",
                Name = active?.Patient?.FullName, Detail = active?.ChiefComplaint ?? active?.ServiceLine.ToString(),
                Badge = active?.Status.ToString(), BadgeTone = "info",
                Chips = active is null ? new() : new() { active.SessionNumber, At(active.ScheduledUtc) },
                Link = To("Index", "Allied", Permissions.AlliedSession)
            }
        };
    }

    /// <summary>Roles with no dedicated dashboard. Facility totals only, no patient names.</summary>
    private async Task<DashboardCanvasVm> OverviewCanvasAsync(int fid)
    {
        var days = LastDays();
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var visits = await _db.Encounters.AsNoTracking().Where(e => e.FacilityId == fid && e.StartedAt >= days.Start).Select(e => e.StartedAt).ToListAsync();
        var admissions = await _db.Admissions.AsNoTracking().Where(a => a.FacilityId == fid && a.AdmittedAt >= days.Start).Select(a => a.AdmittedAt).ToListAsync();
        var bedsTotal = await _db.Beds.AsNoTracking().CountAsync(b => b.Ward!.FacilityId == fid);
        var occupied = await _db.Beds.AsNoTracking().CountAsync(b => b.Ward!.FacilityId == fid && b.Status == BedStatus.Occupied);
        var queue = await _db.QueueEntries.AsNoTracking().CountAsync(q => q.FacilityId == fid && q.TicketDate == today);
        var wards = await _db.Wards.AsNoTracking().CountAsync(w => w.FacilityId == fid);
        var staff = await _db.Users.AsNoTracking().CountAsync(u => u.FacilityId == fid && u.IsActive);
        var busiest = await _db.Wards.AsNoTracking().Where(w => w.FacilityId == fid)
            .Select(w => new { w.Name, Total = w.Beds!.Count(), Occupied = w.Beds!.Count(b => b.Status == BedStatus.Occupied) })
            .OrderByDescending(w => w.Occupied).FirstOrDefaultAsync();

        return new DashboardCanvasVm
        {
            Hero = new HeroPanel
            {
                Title = "Hospital overview", Icon = "bi-hospital", ClinicalArt = true, NextLabel = "Clinics today",
                NextEmpty = "No patients checked in yet",
                Next = queue == 0 ? null : new HeroNextCard($"{queue} {(queue == 1 ? "patient" : "patients")}", "Checked in across all clinics", "Today", "Live queue count"),
                MetricsTitle = "This facility",
                Metrics = new() { new("Visits (14 days)", visits.Count.ToString("N0")), new("Wards", wards.ToString()), new("Active staff", staff.ToString()) }
            },
            Trend = Trend("Hospital activity", days, "Visits", days.Tally(visits), "Admissions", days.Tally(admissions)),
            Gauge = Gauge("Bed occupancy", "All wards", Pct(occupied, bedsTotal), $"{occupied} of {bedsTotal} beds in use", "Occupied", occupied, "Free", Math.Max(0, bedsTotal - occupied)),
            Spotlight = new SpotlightPanel
            {
                Title = "Busiest ward", Icon = "bi-hospital", Empty = "No wards set up yet",
                Name = busiest?.Name, Detail = busiest is null ? null : $"{busiest.Occupied} of {busiest.Total} beds occupied",
                Chips = busiest is null ? new() : new() { Pct(busiest.Occupied, busiest.Total) + " occupied" }
            }
        };
    }
}
