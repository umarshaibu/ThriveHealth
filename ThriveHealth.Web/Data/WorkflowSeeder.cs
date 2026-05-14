using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Models.Ai;
using ThriveHealth.Web.Models.Billing;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Diagnostics;
using ThriveHealth.Web.Models.Emergency;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Inpatient;
using ThriveHealth.Web.Models.Patients;
using ThriveHealth.Web.Models.Pharmacy;
using ThriveHealth.Web.Models.Scheduling;

namespace ThriveHealth.Web.Data;

/// <summary>
/// End-to-end demo data: ~15 patients moving through the walk-in→walk-out lifecycle, plus
/// a sample <see cref="AiSuggestion"/> per AI feature (including an <c>ImagingDraft</c>
/// on a chest X-ray order so the imaging-read AI feature has something to render against).
///
/// Idempotent: skipped wholesale if the marker patient <c>TH-WF-001</c> already exists.
/// Tenant scoping is set explicitly on every Added entity before each SaveChanges, because
/// the seeder runs at startup with no <c>ITenantContext</c> populated, so the auto-stamp
/// interceptor short-circuits.
/// </summary>
public static class WorkflowSeeder
{
    private const string MarkerHospitalNumber = "TH-WF-001";

    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();

        if (await db.Patients.AnyAsync(p => p.HospitalNumber == MarkerHospitalNumber))
            return; // already seeded

        var facility = await db.Facilities.AsNoTracking().FirstOrDefaultAsync();
        if (facility is null) return;
        var tenantId = facility.TenantId;

        // Pull commonly-needed references once.
        var refs = await LoadReferencesAsync(db, facility.Id);
        if (refs is null) return; // missing prerequisites; bail without erroring

        var ctx = new SeedCtx(db, facility.Id, tenantId, refs);

        // -------- Patients (15) ---------------------------------------------------------
        var patients = BuildPatients(ctx);
        db.Patients.AddRange(patients);
        Stamp(db, tenantId);
        await db.SaveChangesAsync();

        // -------- Per-scenario branches ------------------------------------------------
        await SeedQueueWaitingAsync(ctx, patients);          // patients 1-3: waiting
        await SeedInConsultationAsync(ctx, patients);         // patients 4-5: doctor seeing them now
        await SeedCompletedVisitsAsync(ctx, patients);        // patients 6-10: full lifecycle done today
        await SeedAEAsync(ctx, patients);                     // patients 11-12: A&E (triaged)
        await SeedAdmissionsAsync(ctx, patients);             // patients 13-14: admitted to ward
        await SeedPendingResultsAsync(ctx, patients);         // patient 15: orders pending in lab/imaging

        // -------- AI demo suggestions across features ----------------------------------
        await SeedAiSuggestionsAsync(ctx, patients);
    }

    // ============================================================================================
    // Shared context + helpers
    // ============================================================================================

    private sealed record SeedCtx(ApplicationDbContext Db, int FacilityId, int TenantId, References Refs);

    private sealed record References(
        Clinic OpdClinic, Clinic AeClinic, Clinic AncClinic,
        Ward GeneralWard, Ward MaternityWard,
        Bed[] FreeBeds,
        PharmacyStore MainPharmacy,
        ApplicationUser Doctor, ApplicationUser Consultant, ApplicationUser Nurse,
        ApplicationUser Cashier, ApplicationUser LabScientist, ApplicationUser Radiographer,
        ApplicationUser Pharmacist,
        LabTest FbcTest, LabTest MalariaTest, LabTest GlucoseTest,
        Drug Paracetamol, Drug Amoxicillin, Drug Artemether);

    private static async Task<References?> LoadReferencesAsync(ApplicationDbContext db, int facilityId)
    {
        var clinics = await db.Clinics.Where(c => c.FacilityId == facilityId && c.IsActive).ToListAsync();
        var wards = await db.Wards.Where(w => w.FacilityId == facilityId && w.IsActive).Include(w => w.Beds).ToListAsync();
        var stores = await db.PharmacyStores.Where(s => s.FacilityId == facilityId && s.IsActive).ToListAsync();
        var drugs = await db.Drugs.ToListAsync();
        var labTests = await db.LabTests.ToListAsync();

        var doc = await db.Users.FirstOrDefaultAsync(u => u.Email == "doc@thrivehealth.ng");
        var consultant = await db.Users.FirstOrDefaultAsync(u => u.Email == "consultant@thrivehealth.ng");
        var nurse = await db.Users.FirstOrDefaultAsync(u => u.Email == "nurse@thrivehealth.ng");
        var cashier = await db.Users.FirstOrDefaultAsync(u => u.Email == "cashier@thrivehealth.ng");
        var lab = await db.Users.FirstOrDefaultAsync(u => u.Email == "lab@thrivehealth.ng");
        var rad = await db.Users.FirstOrDefaultAsync(u => u.Email == "rad@thrivehealth.ng");
        var pharm = await db.Users.FirstOrDefaultAsync(u => u.Email == "pharm@thrivehealth.ng");

        Clinic? opd = clinics.FirstOrDefault(c => c.Specialty == ClinicSpecialty.GeneralOpd) ?? clinics.FirstOrDefault();
        Clinic? ae  = opd; // no separate A&E clinic specialty — share OPD as the routed clinic
        Clinic? anc = clinics.FirstOrDefault(c => c.Specialty == ClinicSpecialty.Antenatal) ?? opd;

        Ward? general = wards.FirstOrDefault(w => w.Type == WardType.GeneralMedical) ?? wards.FirstOrDefault();
        Ward? mat = wards.FirstOrDefault(w => w.Type == WardType.Maternity) ?? general;

        var freeBeds = general?.Beds.Where(b => b.Status == BedStatus.Free).Take(4).ToArray() ?? Array.Empty<Bed>();
        var store = stores.FirstOrDefault();

        Drug? Paracetamol = drugs.FirstOrDefault(d => d.GenericName.Contains("Paracetamol", StringComparison.OrdinalIgnoreCase));
        Drug? Amox = drugs.FirstOrDefault(d => d.GenericName.Contains("Amoxicillin", StringComparison.OrdinalIgnoreCase));
        Drug? Art  = drugs.FirstOrDefault(d => d.GenericName.Contains("Artemether", StringComparison.OrdinalIgnoreCase))
                  ?? drugs.FirstOrDefault(d => d.GenericName.Contains("Artesunate", StringComparison.OrdinalIgnoreCase));

        LabTest? Fbc = labTests.FirstOrDefault(t => t.Code == "FBC") ?? labTests.FirstOrDefault();
        LabTest? Mp  = labTests.FirstOrDefault(t => t.Code.Contains("MAL") || t.Name.Contains("Malaria", StringComparison.OrdinalIgnoreCase)) ?? Fbc;
        LabTest? Glu = labTests.FirstOrDefault(t => t.Code.Contains("GLU") || t.Name.Contains("Glucose", StringComparison.OrdinalIgnoreCase)) ?? Fbc;

        if (opd is null || ae is null || general is null || store is null || doc is null
            || cashier is null || lab is null || pharm is null
            || Paracetamol is null || Fbc is null) return null;

        return new References(opd, ae, anc ?? opd, general, mat ?? general, freeBeds, store,
            doc, consultant ?? doc, nurse ?? doc, cashier, lab, rad ?? lab, pharm,
            Fbc, Mp ?? Fbc, Glu ?? Fbc, Paracetamol, Amox ?? Paracetamol, Art ?? Paracetamol);
    }

    /// <summary>
    /// Stamps <c>TenantId</c> on every newly-tracked entity that exposes the shadow
    /// property — replicating what <c>TenantStampingInterceptor</c> does at request time.
    /// Required here because seeding runs in a background scope with no <c>ITenantContext</c>.
    /// </summary>
    private static void Stamp(ApplicationDbContext db, int tenantId)
    {
        foreach (var entry in db.ChangeTracker.Entries())
        {
            if (entry.State != EntityState.Added) continue;
            var prop = entry.Properties.FirstOrDefault(p => p.Metadata.Name == "TenantId");
            if (prop is null) continue;
            if (prop.CurrentValue is int cur && cur != 0) continue;
            prop.CurrentValue = tenantId;
        }
    }

    // ============================================================================================
    // Patient roster — 15 walk-in patients with Nigerian names + realistic demographics
    // ============================================================================================

    private static Patient[] BuildPatients(SeedCtx ctx)
    {
        // Triple = (FirstName, LastName, Sex, Age, Phone, complaint hint)
        var roster = new (string F, string L, Sex S, int Age, string Phone, string Lga)[]
        {
            ("Adaeze",  "Okafor",     Sex.Female, 28, "+2348031000001", "Ikeja"),
            ("Babatunde","Adeyemi",   Sex.Male,   35, "+2348031000002", "Surulere"),
            ("Chidi",   "Nwosu",      Sex.Male,   41, "+2348031000003", "Yaba"),
            ("Damilola","Akinjide",   Sex.Female, 32, "+2348031000004", "Lekki"),
            ("Emmanuel","Eze",        Sex.Male,   58, "+2348031000005", "Apapa"),
            ("Funke",   "Bakare",     Sex.Female, 24, "+2348031000006", "Ikoyi"),
            ("Garba",   "Mohammed",   Sex.Male,   47, "+2348031000007", "Agege"),
            ("Halima",  "Yusuf",      Sex.Female, 19, "+2348031000008", "Ikeja"),
            ("Ifeoma",  "Obi",        Sex.Female, 33, "+2348031000009", "Surulere"),
            ("Jide",    "Olawale",    Sex.Male,   45, "+2348031000010", "Lekki"),
            ("Kemi",    "Ade",        Sex.Female, 29, "+2348031000011", "Yaba"),
            ("Lawal",   "Sani",       Sex.Male,   62, "+2348031000012", "Apapa"),    // A&E elderly
            ("Mariam",  "Bello",      Sex.Female, 8,  "+2348031000013", "Ikeja"),    // A&E paeds
            ("Ngozi",   "Iwu",        Sex.Female, 36, "+2348031000014", "Lekki"),    // admit
            ("Olumide", "Eze",        Sex.Male,   51, "+2348031000015", "Surulere")  // admit
        };

        var patients = new Patient[roster.Length];
        for (int i = 0; i < roster.Length; i++)
        {
            var r = roster[i];
            patients[i] = new Patient
            {
                HospitalNumber = $"TH-WF-{i + 1:D3}",
                FacilityId = ctx.FacilityId,
                FirstName = r.F, LastName = r.L, Sex = r.S, Phone = r.Phone,
                DateOfBirth = DateOnly.FromDateTime(DateTime.UtcNow.AddYears(-r.Age)),
                State = "Lagos", Lga = r.Lga,
                CreatedAt = DateTime.UtcNow.AddDays(-Random.Shared.Next(0, 90)),
                MaritalStatus = r.Age >= 25 ? MaritalStatus.Married : MaritalStatus.Single,
                IsActive = true
            };
        }
        return patients;
    }

    // ============================================================================================
    // Scenario 1 — three patients are waiting in the OPD queue right now
    // ============================================================================================

    private static async Task SeedQueueWaitingAsync(SeedCtx ctx, Patient[] all)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;

        var entries = new[]
        {
            new QueueEntry { FacilityId = ctx.FacilityId, PatientId = all[0].Id, ClinicId = ctx.Refs.OpdClinic.Id,
                ClinicianId = ctx.Refs.Doctor.Id, TicketNumber = "OPD-001", TicketDate = today,
                Priority = AppointmentPriority.Routine, Status = QueueStatus.Waiting,
                CheckedInAt = now.AddMinutes(-25), TriageNotes = "Headache and mild fever for 2 days." },
            new QueueEntry { FacilityId = ctx.FacilityId, PatientId = all[1].Id, ClinicId = ctx.Refs.OpdClinic.Id,
                ClinicianId = ctx.Refs.Doctor.Id, TicketNumber = "OPD-002", TicketDate = today,
                Priority = AppointmentPriority.Urgent, Status = QueueStatus.Waiting,
                CheckedInAt = now.AddMinutes(-12), TriageNotes = "Severe abdominal pain, vomiting." },
            new QueueEntry { FacilityId = ctx.FacilityId, PatientId = all[2].Id, ClinicId = ctx.Refs.OpdClinic.Id,
                ClinicianId = ctx.Refs.Consultant.Id, TicketNumber = "OPD-003", TicketDate = today,
                Priority = AppointmentPriority.Routine, Status = QueueStatus.Called,
                CheckedInAt = now.AddMinutes(-40), CalledAt = now.AddMinutes(-2),
                TriageNotes = "Follow-up: hypertension review." }
        };
        ctx.Db.QueueEntries.AddRange(entries);
        Stamp(ctx.Db, ctx.TenantId);
        await ctx.Db.SaveChangesAsync();
    }

    // ============================================================================================
    // Scenario 2 — two patients are currently in consultation (encounters InProgress)
    // ============================================================================================

    private static async Task SeedInConsultationAsync(SeedCtx ctx, Patient[] all)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;

        var queueA = new QueueEntry
        {
            FacilityId = ctx.FacilityId, PatientId = all[3].Id, ClinicId = ctx.Refs.OpdClinic.Id,
            ClinicianId = ctx.Refs.Doctor.Id, TicketNumber = "OPD-004", TicketDate = today,
            Priority = AppointmentPriority.Routine, Status = QueueStatus.InConsultation,
            CheckedInAt = now.AddMinutes(-55), ConsultStartedAt = now.AddMinutes(-15),
            TriageNotes = "Sore throat, painful swallow."
        };
        var queueB = new QueueEntry
        {
            FacilityId = ctx.FacilityId, PatientId = all[4].Id, ClinicId = ctx.Refs.OpdClinic.Id,
            ClinicianId = ctx.Refs.Consultant.Id, TicketNumber = "OPD-005", TicketDate = today,
            Priority = AppointmentPriority.Routine, Status = QueueStatus.InConsultation,
            CheckedInAt = now.AddMinutes(-90), ConsultStartedAt = now.AddMinutes(-30),
            TriageNotes = "Recurrent dizziness, recently increased BP."
        };
        ctx.Db.QueueEntries.AddRange(queueA, queueB);
        Stamp(ctx.Db, ctx.TenantId);
        await ctx.Db.SaveChangesAsync();

        var encA = new Encounter
        {
            FacilityId = ctx.FacilityId, PatientId = all[3].Id, ClinicId = ctx.Refs.OpdClinic.Id,
            ClinicianId = ctx.Refs.Doctor.Id, QueueEntryId = queueA.Id,
            Type = EncounterType.OutpatientOpd, Status = EncounterStatus.InProgress,
            StartedAt = now.AddMinutes(-15),
            ChiefComplaint = "Sore throat 3 days, painful swallow, no cough."
        };
        var encB = new Encounter
        {
            FacilityId = ctx.FacilityId, PatientId = all[4].Id, ClinicId = ctx.Refs.OpdClinic.Id,
            ClinicianId = ctx.Refs.Consultant.Id, QueueEntryId = queueB.Id,
            Type = EncounterType.OutpatientOpd, Status = EncounterStatus.InProgress,
            StartedAt = now.AddMinutes(-30),
            ChiefComplaint = "Dizziness on standing, palpitations. Known HTN on amlodipine."
        };
        ctx.Db.Encounters.AddRange(encA, encB);
        Stamp(ctx.Db, ctx.TenantId);
        await ctx.Db.SaveChangesAsync();
    }

    // ============================================================================================
    // Scenario 3 — five fully completed walk-ins today (queue → encounter → orders → bill → paid)
    // ============================================================================================

    private static async Task SeedCompletedVisitsAsync(SeedCtx ctx, Patient[] all)
    {
        var today = DateOnly.FromDateTime(DateTime.UtcNow);
        var now = DateTime.UtcNow;

        for (int i = 0; i < 5; i++)
        {
            var p = all[5 + i];
            var startedAt = now.AddHours(-(6 - i)); // staggered through the morning
            var signedAt = startedAt.AddMinutes(22);

            var queue = new QueueEntry
            {
                FacilityId = ctx.FacilityId, PatientId = p.Id, ClinicId = ctx.Refs.OpdClinic.Id,
                ClinicianId = ctx.Refs.Doctor.Id, TicketNumber = $"OPD-10{i + 1}", TicketDate = today,
                Priority = AppointmentPriority.Routine, Status = QueueStatus.Completed,
                CheckedInAt = startedAt.AddMinutes(-15), ConsultStartedAt = startedAt,
                CompletedAt = signedAt
            };
            ctx.Db.QueueEntries.Add(queue);
            Stamp(ctx.Db, ctx.TenantId);
            await ctx.Db.SaveChangesAsync();

            var enc = new Encounter
            {
                FacilityId = ctx.FacilityId, PatientId = p.Id, ClinicId = ctx.Refs.OpdClinic.Id,
                ClinicianId = ctx.Refs.Doctor.Id, QueueEntryId = queue.Id,
                Type = EncounterType.OutpatientOpd, Status = EncounterStatus.Signed,
                StartedAt = startedAt, SignedAt = signedAt,
                ChiefComplaint = i switch
                {
                    0 => "Fever and joint pains for 4 days.",
                    1 => "Dry cough, no fever.",
                    2 => "Burning urination 3 days.",
                    3 => "Low back pain after lifting.",
                    _ => "Routine BP check."
                }
            };
            ctx.Db.Encounters.Add(enc);
            Stamp(ctx.Db, ctx.TenantId);
            await ctx.Db.SaveChangesAsync();

            // SOAP note
            ctx.Db.SoapNotes.Add(new SoapNote
            {
                EncounterId = enc.Id,
                Subjective = enc.ChiefComplaint + " Denies haematuria, dysuria has progressed.",
                Objective = "BP 128/82  HR 84  Temp 37.6°C  RR 18  SpO2 99% on RA. Chest clear, abdomen soft.",
                Assessment = i switch
                {
                    0 => "Probable uncomplicated malaria.",
                    1 => "Viral URTI.",
                    2 => "Uncomplicated lower UTI.",
                    3 => "Mechanical low back pain.",
                    _ => "Well-controlled HTN."
                },
                Plan = "Symptomatic management, hydration, follow-up if symptoms persist > 72h.",
                UpdatedAt = signedAt,
                UpdatedById = ctx.Refs.Doctor.Id
            });
            ctx.Db.EncounterDiagnoses.Add(new EncounterDiagnosis
            {
                EncounterId = enc.Id, IcdCode = i switch { 0 => "B54", 1 => "J06.9", 2 => "N39.0", 3 => "M54.5", _ => "I10" },
                Description = i switch { 0 => "Unspecified malaria", 1 => "Acute upper respiratory infection",
                    2 => "Urinary tract infection, site not specified", 3 => "Low back pain", _ => "Essential hypertension" },
                Status = DiagnosisStatus.Confirmed, IsPrimary = true,
                CreatedAt = signedAt, CreatedById = ctx.Refs.Doctor.Id
            });

            // Lab order (malaria patient gets actual lab work + result)
            if (i == 0)
            {
                var lab = new LabOrder
                {
                    EncounterId = enc.Id, PatientId = p.Id,
                    LabTestId = ctx.Refs.MalariaTest.Id, TestName = ctx.Refs.MalariaTest.Name,
                    Status = OrderStatus.Completed, Urgency = OrderUrgency.Routine,
                    ClinicalIndication = "?Malaria — 4-day fever",
                    AccessionNumber = $"LAB-{DateTime.UtcNow:yyyyMMdd}-{i + 1:D4}",
                    OrderedAt = startedAt.AddMinutes(2), OrderedById = ctx.Refs.Doctor.Id,
                    CollectedAt = startedAt.AddMinutes(8), CollectedById = ctx.Refs.LabScientist.Id,
                    CompletedAt = startedAt.AddMinutes(35)
                };
                ctx.Db.LabOrders.Add(lab);
                Stamp(ctx.Db, ctx.TenantId);
                await ctx.Db.SaveChangesAsync();

                ctx.Db.LabResults.Add(new LabResult
                {
                    LabOrderId = lab.Id, LabTestId = ctx.Refs.MalariaTest.Id,
                    Status = LabResultStatus.Authorized,
                    EnteredAt = startedAt.AddMinutes(25), EnteredById = ctx.Refs.LabScientist.Id,
                    AuthorizedAt = startedAt.AddMinutes(33), AuthorizedById = ctx.Refs.LabScientist.Id,
                    GeneralComment = "Malaria parasite seen, P. falciparum, parasitaemia +++",
                    HasCriticalValue = false
                });
            }

            // Prescription + dispense
            var rx = new Prescription
            {
                EncounterId = enc.Id, PatientId = p.Id,
                Status = PrescriptionStatus.Dispensed,
                IssuedAt = signedAt, PrescribedById = ctx.Refs.Doctor.Id
            };
            ctx.Db.Prescriptions.Add(rx);
            Stamp(ctx.Db, ctx.TenantId);
            await ctx.Db.SaveChangesAsync();

            var item = new PrescriptionItem
            {
                PrescriptionId = rx.Id,
                DrugId = i == 0 ? ctx.Refs.Artemether.Id : (i == 2 ? ctx.Refs.Amoxicillin.Id : ctx.Refs.Paracetamol.Id),
                DrugName = i == 0 ? ctx.Refs.Artemether.GenericName
                          : i == 2 ? ctx.Refs.Amoxicillin.GenericName
                          : ctx.Refs.Paracetamol.GenericName,
                Dose = "1 tablet", Route = "PO", Frequency = "BD", Duration = "3 days",
                Quantity = 6, QuantityDispensed = 6,
                Instructions = "Take after food."
            };
            ctx.Db.PrescriptionItems.Add(item);
            Stamp(ctx.Db, ctx.TenantId);
            await ctx.Db.SaveChangesAsync();

            var dispense = new Dispense
            {
                FacilityId = ctx.FacilityId, PrescriptionId = rx.Id, PatientId = p.Id,
                StoreId = ctx.Refs.MainPharmacy.Id,
                DispensedById = ctx.Refs.Pharmacist.Id, DispensedAt = signedAt.AddMinutes(8),
                Status = DispenseStatus.Completed,
                CounsellingNotes = "Counselled patient on dose timing and side effects."
            };
            ctx.Db.Dispenses.Add(dispense);
            Stamp(ctx.Db, ctx.TenantId);
            await ctx.Db.SaveChangesAsync();

            ctx.Db.DispenseItems.Add(new DispenseItem
            {
                DispenseId = dispense.Id, PrescriptionItemId = item.Id,
                DrugId = item.DrugId, DrugName = item.DrugName,
                QuantityDispensed = 6, UnitPrice = 250m, LineTotal = 1500m
            });

            // Bill + payment (full settlement)
            var consultFee = i == 4 ? 5000m : 3000m;
            var labFee = i == 0 ? 1500m : 0m;
            var rxFee = 1500m;
            var total = consultFee + labFee + rxFee;

            var bill = new Bill
            {
                FacilityId = ctx.FacilityId, PatientId = p.Id, EncounterId = enc.Id,
                BillNumber = $"BILL-WF-{DateTime.UtcNow:yyyyMMdd}-{i + 1:D3}",
                Status = BillStatus.Paid,
                GrossAmount = total, NetAmount = total, PaidAmount = total,
                CreatedAt = startedAt.AddMinutes(1),
                ClosedAt = signedAt.AddMinutes(12),
                CreatedById = ctx.Refs.Doctor.Id
            };
            ctx.Db.Bills.Add(bill);
            Stamp(ctx.Db, ctx.TenantId);
            await ctx.Db.SaveChangesAsync();

            var billItems = new List<BillItem>
            {
                new() { BillId = bill.Id, Kind = BillItemKind.Consultation,
                    Description = "OPD consultation", Quantity = 1, UnitPrice = consultFee, LineTotal = consultFee, LineNet = consultFee },
                new() { BillId = bill.Id, Kind = BillItemKind.Drug,
                    Description = item.DrugName + " — 6 tabs", Quantity = 1, UnitPrice = rxFee, LineTotal = rxFee, LineNet = rxFee }
            };
            if (i == 0 && labFee > 0)
                billItems.Add(new BillItem { BillId = bill.Id, Kind = BillItemKind.Lab,
                    Description = "Malaria parasite", Quantity = 1, UnitPrice = labFee, LineTotal = labFee, LineNet = labFee });
            ctx.Db.BillItems.AddRange(billItems);

            var pay = new Payment
            {
                BillId = bill.Id, ReceiptNumber = $"RCP-{DateTime.UtcNow:yyyyMMdd}-{bill.Id:D5}",
                Amount = total, Method = i % 2 == 0 ? PaymentMethod.Cash : PaymentMethod.Pos,
                Status = PaymentStatus.Recorded,
                ReceivedAt = signedAt.AddMinutes(10), CashierId = ctx.Refs.Cashier.Id
            };
            ctx.Db.Payments.Add(pay);
            Stamp(ctx.Db, ctx.TenantId);
            await ctx.Db.SaveChangesAsync();
        }
    }

    // ============================================================================================
    // Scenario 4 — two A&E patients, colour-coded
    // ============================================================================================

    private static async Task SeedAEAsync(SeedCtx ctx, Patient[] all)
    {
        var now = DateTime.UtcNow;

        var enc1 = new Encounter
        {
            FacilityId = ctx.FacilityId, PatientId = all[10].Id, ClinicId = ctx.Refs.AeClinic.Id,
            ClinicianId = ctx.Refs.Doctor.Id,
            Type = EncounterType.Emergency, Status = EncounterStatus.InProgress,
            StartedAt = now.AddMinutes(-22),
            ChiefComplaint = "Sudden chest pain radiating to left arm, sweating, nausea."
        };
        var enc2 = new Encounter
        {
            FacilityId = ctx.FacilityId, PatientId = all[11].Id, ClinicId = ctx.Refs.AeClinic.Id,
            ClinicianId = ctx.Refs.Doctor.Id,
            Type = EncounterType.Emergency, Status = EncounterStatus.InProgress,
            StartedAt = now.AddMinutes(-8),
            ChiefComplaint = "High fever 39.6°C, convulsion at home 10 min ago."
        };
        ctx.Db.Encounters.AddRange(enc1, enc2);
        Stamp(ctx.Db, ctx.TenantId);
        await ctx.Db.SaveChangesAsync();

        ctx.Db.TriageAssessments.AddRange(
            new TriageAssessment
            {
                EncounterId = enc1.Id, Colour = TriageColour.Red, ArrivalMode = ArrivalMode.Ambulance,
                ChiefComplaint = enc1.ChiefComplaint!, Avpu = AvpuLevel.Alert,
                TriagedAt = now.AddMinutes(-21), TriagedById = ctx.Refs.Nurse.Id,
                TargetSeenByUtc = now.AddMinutes(-19),
                KnownAllergies = "NKDA", CurrentMedications = "Aspirin daily for years"
            },
            new TriageAssessment
            {
                EncounterId = enc2.Id, Colour = TriageColour.Yellow, ArrivalMode = ArrivalMode.WalkIn,
                ChiefComplaint = enc2.ChiefComplaint!, Avpu = AvpuLevel.Alert,
                TriagedAt = now.AddMinutes(-7), TriagedById = ctx.Refs.Nurse.Id,
                TargetSeenByUtc = now.AddMinutes(53),
                KnownAllergies = "NKDA", CurrentMedications = "None"
            });
        Stamp(ctx.Db, ctx.TenantId);
        await ctx.Db.SaveChangesAsync();
    }

    // ============================================================================================
    // Scenario 5 — two ward admissions with MAR slots due + nursing notes
    // ============================================================================================

    private static async Task SeedAdmissionsAsync(SeedCtx ctx, Patient[] all)
    {
        if (ctx.Refs.FreeBeds.Length < 2) return;
        var now = DateTime.UtcNow;

        var beds = ctx.Refs.FreeBeds.Take(2).ToArray();

        var admissions = new[]
        {
            new Admission
            {
                FacilityId = ctx.FacilityId, PatientId = all[13].Id,
                WardId = ctx.Refs.GeneralWard.Id, BedId = beds[0].Id,
                AdmittingDoctorId = ctx.Refs.Consultant.Id,
                Status = AdmissionStatus.Active, AdmittedAt = now.AddHours(-30),
                ReasonForAdmission = "Pre-eclampsia for BP control and monitoring.",
                WorkingDiagnosis = "Severe pre-eclampsia"
            },
            new Admission
            {
                FacilityId = ctx.FacilityId, PatientId = all[14].Id,
                WardId = ctx.Refs.GeneralWard.Id, BedId = beds[1].Id,
                AdmittingDoctorId = ctx.Refs.Doctor.Id,
                Status = AdmissionStatus.Active, AdmittedAt = now.AddHours(-12),
                ReasonForAdmission = "Severe malaria with vomiting — needs IV therapy.",
                WorkingDiagnosis = "Severe falciparum malaria"
            }
        };
        ctx.Db.Admissions.AddRange(admissions);
        Stamp(ctx.Db, ctx.TenantId);
        await ctx.Db.SaveChangesAsync();

        // Mark the beds occupied
        foreach (var bed in beds) { bed.Status = BedStatus.Occupied; }
        await ctx.Db.SaveChangesAsync();

        // Medications + MAR slots (next few hours)
        foreach (var admission in admissions)
        {
            var med = new InpatientMedication
            {
                AdmissionId = admission.Id,
                DrugId = ctx.Refs.Paracetamol.Id, DrugName = ctx.Refs.Paracetamol.GenericName,
                Dose = "1 g", Route = "IV", Frequency = "QDS",
                Kind = InpatientMedicationKind.Regular, Status = InpatientMedicationStatus.Active,
                StartUtc = admission.AdmittedAt, PrescribedById = admission.AdmittingDoctorId,
                PrescribedAt = admission.AdmittedAt
            };
            ctx.Db.InpatientMedications.Add(med);
            Stamp(ctx.Db, ctx.TenantId);
            await ctx.Db.SaveChangesAsync();

            ctx.Db.MarSlots.AddRange(
                new MarSlot { InpatientMedicationId = med.Id, ScheduledUtc = now.AddMinutes(-30), Status = MarSlotStatus.Scheduled },
                new MarSlot { InpatientMedicationId = med.Id, ScheduledUtc = now.AddMinutes(+45), Status = MarSlotStatus.Scheduled },
                new MarSlot { InpatientMedicationId = med.Id, ScheduledUtc = now.AddHours(+5),    Status = MarSlotStatus.Scheduled });
            Stamp(ctx.Db, ctx.TenantId);
            await ctx.Db.SaveChangesAsync();

            ctx.Db.NursingNotes.Add(new NursingNote
            {
                AdmissionId = admission.Id,
                RecordedUtc = now.AddHours(-2),
                RecordedById = ctx.Refs.Nurse.Id,
                Body = "Patient settled, vitals stable. Pain managed."
            });
            Stamp(ctx.Db, ctx.TenantId);
            await ctx.Db.SaveChangesAsync();
        }
    }

    // ============================================================================================
    // Scenario 6 — pending lab + imaging orders awaiting result/report
    // ============================================================================================

    private static async Task SeedPendingResultsAsync(SeedCtx ctx, Patient[] all)
    {
        var p = all[2]; // re-use the hypertension follow-up patient — they're being called in
        var now = DateTime.UtcNow;

        var enc = new Encounter
        {
            FacilityId = ctx.FacilityId, PatientId = p.Id, ClinicId = ctx.Refs.OpdClinic.Id,
            ClinicianId = ctx.Refs.Consultant.Id,
            Type = EncounterType.OutpatientOpd, Status = EncounterStatus.InProgress,
            StartedAt = now.AddMinutes(-2),
            ChiefComplaint = "HTN follow-up + BP review."
        };
        ctx.Db.Encounters.Add(enc);
        Stamp(ctx.Db, ctx.TenantId);
        await ctx.Db.SaveChangesAsync();

        // Pending lab — sample collected, not yet resulted
        ctx.Db.LabOrders.Add(new LabOrder
        {
            EncounterId = enc.Id, PatientId = p.Id,
            LabTestId = ctx.Refs.FbcTest.Id, TestName = ctx.Refs.FbcTest.Name,
            Status = OrderStatus.InProgress, Urgency = OrderUrgency.Routine,
            ClinicalIndication = "Annual review",
            AccessionNumber = $"LAB-{DateTime.UtcNow:yyyyMMdd}-PEND",
            OrderedAt = now.AddMinutes(-1), OrderedById = ctx.Refs.Consultant.Id,
            CollectedAt = now.AddMinutes(8), CollectedById = ctx.Refs.LabScientist.Id
        });

        // Pending chest X-ray — ordered, awaiting acquisition
        ctx.Db.ImagingOrders.Add(new ImagingOrder
        {
            EncounterId = enc.Id, PatientId = p.Id,
            Modality = ImagingModality.XRay,
            StudyDescription = "Chest X-ray PA",
            ClinicalIndication = "Cough 2 weeks, smoker, rule out CA.",
            Status = OrderStatus.Ordered, Urgency = OrderUrgency.Routine,
            AccessionNumber = $"RAD-{DateTime.UtcNow:yyyyMMdd}-PEND",
            OrderedAt = now, OrderedById = ctx.Refs.Consultant.Id
        });
        Stamp(ctx.Db, ctx.TenantId);
        await ctx.Db.SaveChangesAsync();
    }

    // ============================================================================================
    // AI features — one sample suggestion per major feature, including the X-ray ImagingDraft
    // ============================================================================================

    private static async Task SeedAiSuggestionsAsync(SeedCtx ctx, Patient[] all)
    {
        var now = DateTime.UtcNow;

        // Build a chest X-ray imaging order + (unauthorised) report for the AI feature to attach to.
        var xrayEnc = await ctx.Db.Encounters
            .Where(e => e.FacilityId == ctx.FacilityId && e.PatientId == all[10].Id)
            .FirstOrDefaultAsync();
        if (xrayEnc is not null)
        {
            var xray = new ImagingOrder
            {
                EncounterId = xrayEnc.Id, PatientId = all[10].Id,
                Modality = ImagingModality.XRay,
                StudyDescription = "Chest X-ray (PA + lateral)",
                ClinicalIndication = "62 y/o male, chest pain + sweating — rule out acute CV / pulmonary cause.",
                Status = OrderStatus.Completed, Urgency = OrderUrgency.Stat,
                AccessionNumber = $"RAD-{DateTime.UtcNow:yyyyMMdd}-AI01",
                OrderedAt = now.AddMinutes(-20), OrderedById = ctx.Refs.Doctor.Id,
                CompletedAt = now.AddMinutes(-10)
            };
            ctx.Db.ImagingOrders.Add(xray);
            Stamp(ctx.Db, ctx.TenantId);
            await ctx.Db.SaveChangesAsync();

            ctx.Db.ImagingReports.Add(new ImagingReport
            {
                ImagingOrderId = xray.Id,
                AccessionNumber = xray.AccessionNumber,
                Technique = "PA + lateral, supine",
                DicomStudyUid = "1.2.840.113619.2.55.3.604688119.971.1738156800.123",
                DicomViewerUrl = "https://demo.thrivehealth.ng/dicom/viewer?study=AI01",
                Findings = "AI draft pending clinician review.",
                PerformedAt = now.AddMinutes(-10), PerformedById = ctx.Refs.Radiographer.Id
            });
            Stamp(ctx.Db, ctx.TenantId);
            await ctx.Db.SaveChangesAsync();

            // ImagingDraft AI suggestion — what a model returned for the X-ray read.
            ctx.Db.AiSuggestions.Add(new AiSuggestion
            {
                FacilityId = ctx.FacilityId,
                Feature = AiFeature.ImagingDraft, Status = AiSuggestionStatus.Pending,
                EntityType = "ImagingOrder", EntityKey = xray.Id.ToString(),
                Provider = "stub", Model = "demo-imaging-v1",
                Prompt = "Read this chest X-ray (PA + lateral) for a 62 y/o male presenting with chest pain and diaphoresis. Indication: rule out cardiopulmonary cause.",
                Response = """
                FINDINGS
                • Cardiothoracic ratio mildly enlarged (estimated 0.54).
                • Lung fields clear, no focal consolidation, mass or pleural effusion.
                • Pulmonary vasculature normal in distribution and calibre.
                • No pneumothorax. No rib fracture.
                • Mediastinal contour preserved.

                IMPRESSION
                1. Mild cardiomegaly — correlate with ECG / echo.
                2. No acute pulmonary pathology to account for chest pain.

                RECOMMENDATION
                • 12-lead ECG and troponin to evaluate for acute coronary syndrome.
                • Echocardiogram if cardiomegaly confirmed.
                """,
                InputTokens = 1280, OutputTokens = 220, LatencyMs = 4100,
                CreatedAtUtc = now.AddMinutes(-8), RequestedById = ctx.Refs.Doctor.Id
            });
        }

        // One sample suggestion per other major feature so the UI has something to render.
        var firstCompletedEnc = await ctx.Db.Encounters
            .Where(e => e.FacilityId == ctx.FacilityId && e.Status == EncounterStatus.Signed)
            .OrderByDescending(e => e.SignedAt).FirstOrDefaultAsync();

        if (firstCompletedEnc is not null)
        {
            ctx.Db.AiSuggestions.AddRange(
                new AiSuggestion {
                    FacilityId = ctx.FacilityId,
                    Feature = AiFeature.Differential, Status = AiSuggestionStatus.Accepted,
                    EntityType = "Encounter", EntityKey = firstCompletedEnc.Id.ToString(),
                    Provider = "stub", Model = "demo-clinical-v1",
                    Prompt = "Generate a differential for fever + joint pain in a 28 y/o Nigerian female.",
                    Response = "1. Malaria (P. falciparum) — highest pre-test probability in endemic setting.\n2. Dengue / chikungunya — recent travel?\n3. Typhoid — sustained fever + abdominal symptoms.\n4. Acute viral arthritis.",
                    InputTokens = 240, OutputTokens = 95, LatencyMs = 1800,
                    CreatedAtUtc = now.AddHours(-3), RequestedById = ctx.Refs.Doctor.Id,
                    ReviewedAtUtc = now.AddHours(-3).AddMinutes(2), ReviewedById = ctx.Refs.Doctor.Id
                },
                new AiSuggestion {
                    FacilityId = ctx.FacilityId,
                    Feature = AiFeature.PatientSummary, Status = AiSuggestionStatus.Accepted,
                    EntityType = "Patient", EntityKey = all[5].Id.ToString(),
                    Provider = "stub", Model = "demo-clinical-v1",
                    Prompt = "Summarise this patient's recent visits.",
                    Response = "Adult female with one recent OPD visit today (malaria, treated with artemether). No prior admissions. NKDA. No chronic conditions on file.",
                    InputTokens = 800, OutputTokens = 70, LatencyMs = 1500,
                    CreatedAtUtc = now.AddHours(-2), RequestedById = ctx.Refs.Doctor.Id,
                    ReviewedAtUtc = now.AddHours(-2).AddMinutes(1), ReviewedById = ctx.Refs.Doctor.Id
                },
                new AiSuggestion {
                    FacilityId = ctx.FacilityId,
                    Feature = AiFeature.SoapStructure, Status = AiSuggestionStatus.Edited,
                    EntityType = "Encounter", EntityKey = firstCompletedEnc.Id.ToString(),
                    Provider = "stub", Model = "demo-clinical-v1",
                    Prompt = "Rewrite this scratch note as a SOAP entry.",
                    Response = "S: 4-day fever, joint pains.\nO: T 37.6, BP 128/82, exam unremarkable.\nA: Probable uncomplicated malaria.\nP: Artemether-Lumefantrine, paracetamol PRN, ORS.",
                    EditedContent = "S: 4-day fever and joint pains. No vomiting, no diarrhoea.\nO: T 37.6 °C, BP 128/82, HR 84, RR 18, SpO2 99%. No icterus, no pallor.\nA: Uncomplicated malaria — RDT positive.\nP: Coartem 80/480 BD ×3d, paracetamol PRN, oral fluids. Return if no better in 48h.",
                    InputTokens = 320, OutputTokens = 140, LatencyMs = 1900,
                    CreatedAtUtc = now.AddHours(-3).AddMinutes(15), RequestedById = ctx.Refs.Doctor.Id,
                    ReviewedAtUtc = now.AddHours(-3).AddMinutes(18), ReviewedById = ctx.Refs.Doctor.Id
                },
                new AiSuggestion {
                    FacilityId = ctx.FacilityId,
                    Feature = AiFeature.SymptomChecker, Status = AiSuggestionStatus.Pending,
                    EntityType = "Patient", EntityKey = all[1].Id.ToString(),
                    Provider = "stub", Model = "demo-symptom-v1",
                    Prompt = "Patient (35 M) reports severe abdominal pain + vomiting. What are likely causes?",
                    Response = null,
                    InputTokens = 0, OutputTokens = 0, LatencyMs = 0,
                    CreatedAtUtc = now.AddMinutes(-5), RequestedById = ctx.Refs.Nurse.Id
                });
        }

        Stamp(ctx.Db, ctx.TenantId);
        await ctx.Db.SaveChangesAsync();
    }
}
