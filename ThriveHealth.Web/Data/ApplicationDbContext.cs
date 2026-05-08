using System.Linq.Expressions;
using Microsoft.AspNetCore.Identity.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Models.Ai;
using ThriveHealth.Web.Models.Allied;
using ThriveHealth.Web.Models.Audit;
using ThriveHealth.Web.Models.Billing;
using ThriveHealth.Web.Models.BloodBank;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Critical;
using ThriveHealth.Web.Models.Diagnostics;
using ThriveHealth.Web.Models.Emergency;
using ThriveHealth.Web.Models.Hr;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Immunization;
using ThriveHealth.Web.Models.Inpatient;
using ThriveHealth.Web.Models.Integrations;
using ThriveHealth.Web.Models.Insurance;
using ThriveHealth.Web.Models.Inventory;
using ThriveHealth.Web.Models.Maternity;
using ThriveHealth.Web.Models.Mortuary;
using ThriveHealth.Web.Models.Paediatrics;
using ThriveHealth.Web.Models.Patients;
using ThriveHealth.Web.Models.Pharmacy;
using ThriveHealth.Web.Models.Portal;
using ThriveHealth.Web.Models.Reporting;
using ThriveHealth.Web.Models.Scheduling;
using ThriveHealth.Web.Models.Telemedicine;
using ThriveHealth.Web.Models.Tenancy;
using ThriveHealth.Web.Models.Theatre;
using ThriveHealth.Web.Services.Tenancy;

namespace ThriveHealth.Web.Data;

public class ApplicationDbContext : IdentityDbContext<ApplicationUser>
{
    /// <summary>
    /// Per-request tenant context. Optional — falls back to <c>null</c> for design-time tooling
    /// (migrations, scaffolding) where DI isn't available. When null, query filters allow all
    /// rows to pass; intended only for tooling, not production paths.
    /// </summary>
    private readonly ITenantContext? _tenantContext;

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options)
        : base(options)
    {
    }

    public ApplicationDbContext(DbContextOptions<ApplicationDbContext> options, ITenantContext tenantContext)
        : base(options)
    {
        _tenantContext = tenantContext;
    }

    /// <summary>
    /// Tenant-id reader used by global query filters. Tracked through a property (not a field)
    /// so EF Core re-evaluates it per query — without this the first query's value would be
    /// baked into the compiled filter and every subsequent request would silently use the wrong
    /// tenant. Returns <c>null</c> in admin / marketing / design-time contexts, which the filter
    /// treats as "see everything".
    /// </summary>
    public int? CurrentTenantIdForFilter => _tenantContext?.CurrentId;

    /// <summary>
    /// Entities deliberately exempt from tenant scoping:
    ///   • <see cref="Tenant"/> / <see cref="Plan"/> — tenancy meta itself.
    ///   • Global reference catalogues (ICD codes, drug master, vaccine schedules, payer
    ///     directory, notifiable disease list, lab catalog) — shared across every hospital
    ///     on the platform.
    ///   • <see cref="RolePermission"/> — platform-wide RBAC matrix.
    /// Identity tables (AspNetUsers, AspNetRoles, etc.) are also unscoped — auto-filtering
    /// them breaks login/role flows in subtle ways. <see cref="ApplicationUser"/> still has
    /// a real <c>TenantId</c> column so the AuthController can validate at sign-in.
    /// </summary>
    private static readonly HashSet<Type> TenantUnscopedTypes = new()
    {
        typeof(Tenant), typeof(Plan),
        typeof(IcdCode),
        typeof(LabTest), typeof(LabAnalyte),
        typeof(Drug), typeof(DrugInteraction),
        typeof(Vaccine), typeof(VaccineSchedule),
        typeof(NotifiableDisease),
        typeof(Payer), typeof(PayerPlan), typeof(PayerFormulary),
        typeof(RolePermission)
    };

    private static bool IsIdentityType(Type t) =>
        t.Namespace?.StartsWith("Microsoft.AspNetCore.Identity") == true;

    public DbSet<Facility> Facilities => Set<Facility>();
    public DbSet<ThriveHealth.Web.Models.Tenancy.Tenant> Tenants => Set<ThriveHealth.Web.Models.Tenancy.Tenant>();
    public DbSet<ThriveHealth.Web.Models.Tenancy.Plan> Plans => Set<ThriveHealth.Web.Models.Tenancy.Plan>();
    public DbSet<ThriveHealth.Web.Models.Tenancy.TenantSubscription> TenantSubscriptions => Set<ThriveHealth.Web.Models.Tenancy.TenantSubscription>();
    public DbSet<ThriveHealth.Web.Models.Tenancy.TenantPayment> TenantPayments => Set<ThriveHealth.Web.Models.Tenancy.TenantPayment>();
    public DbSet<Patient> Patients => Set<Patient>();
    public DbSet<PatientNextOfKin> PatientNextOfKin => Set<PatientNextOfKin>();
    public DbSet<PatientPayer> PatientPayers => Set<PatientPayer>();
    public DbSet<Allergy> Allergies => Set<Allergy>();
    public DbSet<Problem> Problems => Set<Problem>();
    public DbSet<MedicationRecord> Medications => Set<MedicationRecord>();
    public DbSet<VitalsRecord> Vitals => Set<VitalsRecord>();
    public DbSet<PatientDocument> PatientDocuments => Set<PatientDocument>();
    public DbSet<MpiPotentialMatch> MpiPotentialMatches => Set<MpiPotentialMatch>();
    public DbSet<MpiMergeAudit> MpiMergeAudits => Set<MpiMergeAudit>();
    public DbSet<HospitalNumberCounter> HospitalNumberCounters => Set<HospitalNumberCounter>();

    public DbSet<Clinic> Clinics => Set<Clinic>();
    public DbSet<Room> Rooms => Set<Room>();
    public DbSet<ClinicianAvailability> ClinicianAvailabilities => Set<ClinicianAvailability>();
    public DbSet<ClinicianTimeOff> ClinicianTimeOffs => Set<ClinicianTimeOff>();
    public DbSet<Appointment> Appointments => Set<Appointment>();
    public DbSet<QueueEntry> QueueEntries => Set<QueueEntry>();
    public DbSet<TicketCounter> TicketCounters => Set<TicketCounter>();
    public DbSet<ReminderJob> ReminderJobs => Set<ReminderJob>();

    public DbSet<Encounter> Encounters => Set<Encounter>();
    public DbSet<SoapNote> SoapNotes => Set<SoapNote>();
    public DbSet<EncounterDiagnosis> EncounterDiagnoses => Set<EncounterDiagnosis>();
    public DbSet<LabOrder> LabOrders => Set<LabOrder>();
    public DbSet<ImagingOrder> ImagingOrders => Set<ImagingOrder>();
    public DbSet<ProcedureOrder> ProcedureOrders => Set<ProcedureOrder>();
    public DbSet<Prescription> Prescriptions => Set<Prescription>();
    public DbSet<PrescriptionItem> PrescriptionItems => Set<PrescriptionItem>();
    public DbSet<IcdCode> IcdCodes => Set<IcdCode>();
    public DbSet<DotPhrase> DotPhrases => Set<DotPhrase>();

    public DbSet<HrProfile> HrProfiles => Set<HrProfile>();
    public DbSet<RosterShift> RosterShifts => Set<RosterShift>();
    public DbSet<LeaveRequest> LeaveRequests => Set<LeaveRequest>();

    public DbSet<ThriveHealth.Web.Models.Theatre.Theatre> Theatres => Set<ThriveHealth.Web.Models.Theatre.Theatre>();
    public DbSet<TheatreSession> TheatreSessions => Set<TheatreSession>();
    public DbSet<ChecklistItem> TheatreChecklistItems => Set<ChecklistItem>();
    public DbSet<SessionEvent> TheatreSessionEvents => Set<SessionEvent>();

    public DbSet<Bill> Bills => Set<Bill>();
    public DbSet<BillItem> BillItems => Set<BillItem>();
    public DbSet<Payment> Payments => Set<Payment>();
    public DbSet<CashierShift> CashierShifts => Set<CashierShift>();

    public DbSet<Supplier> Suppliers => Set<Supplier>();
    public DbSet<InventoryItem> InventoryItems => Set<InventoryItem>();
    public DbSet<InventoryStock> InventoryStocks => Set<InventoryStock>();
    public DbSet<InventoryStockMovement> InventoryStockMovements => Set<InventoryStockMovement>();
    public DbSet<PurchaseOrder> PurchaseOrders => Set<PurchaseOrder>();
    public DbSet<PurchaseOrderItem> PurchaseOrderItems => Set<PurchaseOrderItem>();
    public DbSet<Grn> Grns => Set<Grn>();
    public DbSet<GrnItem> GrnItems => Set<GrnItem>();
    public DbSet<StockTake> StockTakes => Set<StockTake>();
    public DbSet<StockTakeItem> StockTakeItems => Set<StockTakeItem>();

    public DbSet<Payer> Payers => Set<Payer>();
    public DbSet<PayerPlan> PayerPlans => Set<PayerPlan>();
    public DbSet<PayerFormulary> PayerFormularies => Set<PayerFormulary>();
    public DbSet<Authorization> Authorizations => Set<Authorization>();
    public DbSet<Claim> Claims => Set<Claim>();
    public DbSet<ClaimItem> ClaimItems => Set<ClaimItem>();

    public DbSet<LabTest> LabTests => Set<LabTest>();
    public DbSet<LabAnalyte> LabAnalytes => Set<LabAnalyte>();
    public DbSet<LabResult> LabResults => Set<LabResult>();
    public DbSet<LabResultValue> LabResultValues => Set<LabResultValue>();
    public DbSet<ImagingReport> ImagingReports => Set<ImagingReport>();

    public DbSet<TriageAssessment> TriageAssessments => Set<TriageAssessment>();
    public DbSet<ResusBay> ResusBays => Set<ResusBay>();
    public DbSet<ResusEvent> ResusEvents => Set<ResusEvent>();

    public DbSet<Ward> Wards => Set<Ward>();
    public DbSet<Bed> Beds => Set<Bed>();
    public DbSet<Admission> Admissions => Set<Admission>();
    public DbSet<BedAllocation> BedAllocations => Set<BedAllocation>();
    public DbSet<InpatientMedication> InpatientMedications => Set<InpatientMedication>();
    public DbSet<MarSlot> MarSlots => Set<MarSlot>();
    public DbSet<FluidEntry> FluidEntries => Set<FluidEntry>();
    public DbSet<NursingNote> NursingNotes => Set<NursingNote>();
    public DbSet<WardRoundEntry> WardRoundEntries => Set<WardRoundEntry>();

    public DbSet<Drug> Drugs => Set<Drug>();
    public DbSet<DrugInteraction> DrugInteractions => Set<DrugInteraction>();
    public DbSet<PharmacyStore> PharmacyStores => Set<PharmacyStore>();
    public DbSet<DrugStock> DrugStocks => Set<DrugStock>();
    public DbSet<StockMovement> StockMovements => Set<StockMovement>();
    public DbSet<Dispense> Dispenses => Set<Dispense>();
    public DbSet<DispenseItem> DispenseItems => Set<DispenseItem>();

    public DbSet<AnteNatalRecord> AnteNatalRecords => Set<AnteNatalRecord>();
    public DbSet<AnteNatalVisit> AnteNatalVisits => Set<AnteNatalVisit>();
    public DbSet<Delivery> Deliveries => Set<Delivery>();
    public DbSet<Newborn> Newborns => Set<Newborn>();
    public DbSet<PostnatalVisit> PostnatalVisits => Set<PostnatalVisit>();

    public DbSet<ChildProfile> ChildProfiles => Set<ChildProfile>();
    public DbSet<GrowthMeasurement> GrowthMeasurements => Set<GrowthMeasurement>();

    public DbSet<Vaccine> Vaccines => Set<Vaccine>();
    public DbSet<VaccineSchedule> VaccineSchedules => Set<VaccineSchedule>();
    public DbSet<ImmunizationDose> ImmunizationDoses => Set<ImmunizationDose>();

    public DbSet<IcuChartEntry> IcuChartEntries => Set<IcuChartEntry>();
    public DbSet<DialysisSession> DialysisSessions => Set<DialysisSession>();

    public DbSet<BloodDonor> BloodDonors => Set<BloodDonor>();
    public DbSet<BloodUnit> BloodUnits => Set<BloodUnit>();
    public DbSet<BloodCrossMatch> BloodCrossMatches => Set<BloodCrossMatch>();

    public DbSet<MortuaryEntry> MortuaryEntries => Set<MortuaryEntry>();

    public DbSet<AlliedSession> AlliedSessions => Set<AlliedSession>();

    public DbSet<TeleSession> TeleSessions => Set<TeleSession>();
    public DbSet<TeleChatMessage> TeleChatMessages => Set<TeleChatMessage>();
    public DbSet<TeleChatAttachment> TeleChatAttachments => Set<TeleChatAttachment>();
    public DbSet<TeleSessionParticipant> TeleSessionParticipants => Set<TeleSessionParticipant>();
    public DbSet<ChatPackage> ChatPackages => Set<ChatPackage>();
    public DbSet<ThriveHealth.Web.Models.Integrations.WebPushSubscription> WebPushSubscriptions => Set<ThriveHealth.Web.Models.Integrations.WebPushSubscription>();
    public DbSet<Referral> Referrals => Set<Referral>();
    public DbSet<MedicalCertificate> MedicalCertificates => Set<MedicalCertificate>();
    public DbSet<PortalAccount> PortalAccounts => Set<PortalAccount>();
    public DbSet<PortalSymptomIntake> PortalSymptomIntakes => Set<PortalSymptomIntake>();

    public DbSet<NotifiableDisease> NotifiableDiseases => Set<NotifiableDisease>();
    public DbSet<IdsrCase> IdsrCases => Set<IdsrCase>();
    public DbSet<NhmisReport> NhmisReports => Set<NhmisReport>();

    public DbSet<AuditEntry> AuditEntries => Set<AuditEntry>();
    public DbSet<SmsMessage> SmsMessages => Set<SmsMessage>();
    public DbSet<EmailMessage> EmailMessages => Set<EmailMessage>();
    public DbSet<PaymentTransaction> PaymentTransactions => Set<PaymentTransaction>();

    public DbSet<RolePermission> RolePermissions => Set<RolePermission>();

    public DbSet<AiSuggestion> AiSuggestions => Set<AiSuggestion>();

    protected override void OnModelCreating(ModelBuilder builder)
    {
        base.OnModelCreating(builder);

        builder.HasPostgresExtension("fuzzystrmatch");

        builder.Entity<Facility>(b =>
        {
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => x.TenantId);
            b.HasOne(x => x.Tenant).WithMany(t => t.Facilities).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ThriveHealth.Web.Models.Tenancy.Tenant>(b =>
        {
            b.HasIndex(x => x.Slug).IsUnique();
            b.HasIndex(x => x.OwnerEmail);
            b.HasIndex(x => x.Status);
            // Filtered unique index — empty/NULL custom domain is allowed across many tenants,
            // but a non-null hostname can only ever belong to one tenant at a time.
            b.HasIndex(x => x.CustomDomain).IsUnique().HasFilter("\"CustomDomain\" IS NOT NULL");
        });

        builder.Entity<ThriveHealth.Web.Models.Tenancy.Plan>(b =>
        {
            b.HasIndex(x => x.Code).IsUnique();
        });

        builder.Entity<ThriveHealth.Web.Models.Tenancy.TenantSubscription>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.IsActive });
            b.HasOne(x => x.Tenant).WithMany(t => t.Subscriptions).HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Plan).WithMany().HasForeignKey(x => x.PlanId).OnDelete(DeleteBehavior.Restrict);
        });

        builder.Entity<ThriveHealth.Web.Models.Tenancy.TenantPayment>(b =>
        {
            b.HasIndex(x => new { x.TenantId, x.Status });
            b.HasOne(x => x.Tenant).WithMany().HasForeignKey(x => x.TenantId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Subscription).WithMany().HasForeignKey(x => x.SubscriptionId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.ReviewedBy).WithMany().HasForeignKey(x => x.ReviewedById).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ApplicationUser>(b =>
        {
            b.HasOne(x => x.Facility)
                .WithMany(f => f.Users)
                .HasForeignKey(x => x.FacilityId)
                .OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.StaffNumber);
        });

        builder.Entity<Patient>(b =>
        {
            b.HasIndex(x => new { x.FacilityId, x.HospitalNumber }).IsUnique();
            b.HasIndex(x => x.Phone);
            b.HasIndex(x => x.Nin);
            b.HasIndex(x => new { x.LastName, x.FirstName });
            b.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.MergedIntoPatient).WithMany().HasForeignKey(x => x.MergedIntoPatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<PatientNextOfKin>(b =>
        {
            b.HasOne(x => x.Patient).WithMany(p => p.NextOfKin).HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PatientPayer>(b =>
        {
            b.HasOne(x => x.Patient).WithMany(p => p.Payers).HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Payer).WithMany().HasForeignKey(x => x.PayerId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.PayerPlan).WithMany().HasForeignKey(x => x.PayerPlanId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.PatientId, x.Type });
        });

        builder.Entity<Allergy>(b =>
        {
            b.HasOne(x => x.Patient).WithMany(p => p.Allergies).HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.RecordedBy).WithMany().HasForeignKey(x => x.RecordedById).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Problem>(b =>
        {
            b.HasOne(x => x.Patient).WithMany(p => p.Problems).HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.RecordedBy).WithMany().HasForeignKey(x => x.RecordedById).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<MedicationRecord>(b =>
        {
            b.HasOne(x => x.Patient).WithMany(p => p.Medications).HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.RecordedBy).WithMany().HasForeignKey(x => x.RecordedById).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<VitalsRecord>(b =>
        {
            b.HasOne(x => x.Patient).WithMany(p => p.Vitals).HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.RecordedBy).WithMany().HasForeignKey(x => x.RecordedById).OnDelete(DeleteBehavior.SetNull);
            b.Property(x => x.TemperatureCelsius).HasPrecision(4, 1);
            b.Property(x => x.WeightKg).HasPrecision(6, 2);
            b.Property(x => x.HeightCm).HasPrecision(5, 1);
            b.Ignore(x => x.Bmi);
            b.Ignore(x => x.Mews);
        });

        builder.Entity<PatientDocument>(b =>
        {
            b.HasOne(x => x.Patient).WithMany(p => p.Documents).HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.UploadedBy).WithMany().HasForeignKey(x => x.UploadedById).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<MpiPotentialMatch>(b =>
        {
            b.HasOne(x => x.PatientA).WithMany().HasForeignKey(x => x.PatientAId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.PatientB).WithMany().HasForeignKey(x => x.PatientBId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ResolvedBy).WithMany().HasForeignKey(x => x.ResolvedById).OnDelete(DeleteBehavior.SetNull);
            b.Property(x => x.ConfidenceScore).HasPrecision(5, 2);
            b.HasIndex(x => x.Status);
        });

        builder.Entity<MpiMergeAudit>(b =>
        {
            b.HasOne(x => x.MasterPatient).WithMany().HasForeignKey(x => x.MasterPatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.MergedBy).WithMany().HasForeignKey(x => x.MergedById).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<HospitalNumberCounter>(b =>
        {
            b.HasKey(x => new { x.FacilityId, x.Year });
        });

        builder.Entity<Clinic>(b =>
        {
            b.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.FacilityId, x.Code }).IsUnique();
        });

        builder.Entity<Room>(b =>
        {
            b.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.FacilityId, x.Code }).IsUnique();
        });

        builder.Entity<ClinicianAvailability>(b =>
        {
            b.HasOne(x => x.Clinic).WithMany(c => c.Availability).HasForeignKey(x => x.ClinicId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Clinician).WithMany().HasForeignKey(x => x.ClinicianId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.ClinicianId, x.DayOfWeek });
        });

        builder.Entity<ClinicianTimeOff>(b =>
        {
            b.HasOne(x => x.Clinician).WithMany().HasForeignKey(x => x.ClinicianId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.ClinicianId, x.StartUtc });
        });

        builder.Entity<Appointment>(b =>
        {
            b.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Clinic).WithMany().HasForeignKey(x => x.ClinicId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Clinician).WithMany().HasForeignKey(x => x.ClinicianId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Room).WithMany().HasForeignKey(x => x.RoomId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.BookedBy).WithMany().HasForeignKey(x => x.BookedById).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.RescheduledFrom).WithMany().HasForeignKey(x => x.RescheduledFromId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.FacilityId, x.ScheduledStartUtc });
            b.HasIndex(x => new { x.ClinicId, x.ScheduledStartUtc });
            b.HasIndex(x => new { x.ClinicianId, x.ScheduledStartUtc });
            b.HasIndex(x => new { x.PatientId, x.ScheduledStartUtc });
            b.HasIndex(x => x.Status);
        });

        builder.Entity<QueueEntry>(b =>
        {
            b.HasOne(x => x.Appointment).WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Clinic).WithMany().HasForeignKey(x => x.ClinicId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Clinician).WithMany().HasForeignKey(x => x.ClinicianId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.FacilityId, x.ClinicId, x.TicketDate, x.Status });
            b.Ignore(x => x.IsActive);
        });

        builder.Entity<TicketCounter>(b =>
        {
            b.HasKey(x => new { x.FacilityId, x.ClinicId, x.Date });
        });

        builder.Entity<ReminderJob>(b =>
        {
            b.HasOne(x => x.Appointment).WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.Status, x.ScheduledForUtc });
        });

        builder.Entity<Encounter>(b =>
        {
            b.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Appointment).WithMany().HasForeignKey(x => x.AppointmentId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.QueueEntry).WithMany().HasForeignKey(x => x.QueueEntryId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Clinic).WithMany().HasForeignKey(x => x.ClinicId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Clinician).WithMany().HasForeignKey(x => x.ClinicianId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ResusBay).WithMany().HasForeignKey(x => x.ResusBayId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.PatientId, x.StartedAt });
            b.HasIndex(x => new { x.ClinicianId, x.StartedAt });
            b.HasIndex(x => x.Status);
            b.HasIndex(x => new { x.Type, x.Status });
        });

        builder.Entity<TriageAssessment>(b =>
        {
            b.HasOne(x => x.Encounter).WithOne(e => e.Triage!).HasForeignKey<TriageAssessment>(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.TriagedBy).WithMany().HasForeignKey(x => x.TriagedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.Colour);
            b.HasIndex(x => x.IsForensicCase);
        });

        builder.Entity<ResusBay>(b =>
        {
            b.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.FacilityId, x.Code }).IsUnique();
        });

        builder.Entity<ResusEvent>(b =>
        {
            b.HasOne(x => x.Encounter).WithMany(e => e.ResusEvents).HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.RecordedBy).WithMany().HasForeignKey(x => x.RecordedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.EncounterId, x.AtUtc });
        });

        builder.Entity<SoapNote>(b =>
        {
            b.HasOne(x => x.Encounter).WithOne(e => e.Soap).HasForeignKey<SoapNote>(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<EncounterDiagnosis>(b =>
        {
            b.HasOne(x => x.Encounter).WithMany(e => e.Diagnoses).HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.EncounterId, x.IcdCode });
        });

        builder.Entity<LabOrder>(b =>
        {
            b.HasOne(x => x.Encounter).WithMany(e => e.LabOrders).HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.OrderedBy).WithMany().HasForeignKey(x => x.OrderedById).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.LabTest).WithMany().HasForeignKey(x => x.LabTestId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.CollectedBy).WithMany().HasForeignKey(x => x.CollectedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.PatientId, x.OrderedAt });
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.AccessionNumber);
        });

        builder.Entity<ImagingOrder>(b =>
        {
            b.HasOne(x => x.Encounter).WithMany(e => e.ImagingOrders).HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.OrderedBy).WithMany().HasForeignKey(x => x.OrderedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.PatientId, x.OrderedAt });
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.AccessionNumber);
        });

        builder.Entity<LabTest>(b =>
        {
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => x.Section);
            b.Property(x => x.Price).HasPrecision(12, 2);
        });

        builder.Entity<LabAnalyte>(b =>
        {
            b.HasOne(x => x.LabTest).WithMany(t => t.Analytes).HasForeignKey(x => x.LabTestId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.LabTestId, x.SortOrder });
            b.Property(x => x.RefLow).HasPrecision(14, 4);
            b.Property(x => x.RefHigh).HasPrecision(14, 4);
            b.Property(x => x.CriticalLow).HasPrecision(14, 4);
            b.Property(x => x.CriticalHigh).HasPrecision(14, 4);
        });

        builder.Entity<LabResult>(b =>
        {
            b.HasOne(x => x.LabOrder).WithOne(o => o.Result!).HasForeignKey<LabResult>(x => x.LabOrderId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.LabTest).WithMany().HasForeignKey(x => x.LabTestId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.EnteredBy).WithMany().HasForeignKey(x => x.EnteredById).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.AuthorizedBy).WithMany().HasForeignKey(x => x.AuthorizedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.Status);
            b.HasIndex(x => x.HasCriticalValue);
        });

        builder.Entity<LabResultValue>(b =>
        {
            b.HasOne(x => x.LabResult).WithMany(r => r.Values).HasForeignKey(x => x.LabResultId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.LabAnalyte).WithMany().HasForeignKey(x => x.LabAnalyteId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.NumericValue).HasPrecision(14, 4);
        });

        builder.Entity<ImagingReport>(b =>
        {
            b.HasOne(x => x.ImagingOrder).WithOne(o => o.Report!).HasForeignKey<ImagingReport>(x => x.ImagingOrderId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.PerformedBy).WithMany().HasForeignKey(x => x.PerformedById).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.ReportedBy).WithMany().HasForeignKey(x => x.ReportedById).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.AuthorizedBy).WithMany().HasForeignKey(x => x.AuthorizedById).OnDelete(DeleteBehavior.SetNull);
            b.Ignore(x => x.IsAuthorized);
        });

        builder.Entity<Payer>(b =>
        {
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => x.OrgType);
        });

        builder.Entity<PayerPlan>(b =>
        {
            b.HasOne(x => x.Payer).WithMany(p => p.Plans).HasForeignKey(x => x.PayerId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.PayerId, x.Code }).IsUnique();
            b.Property(x => x.TariffMultiplier).HasPrecision(6, 3);
            b.Property(x => x.CapitationRatePerEnrolleeMonth).HasPrecision(12, 2);
            b.Property(x => x.DefaultCopayPercent).HasPrecision(5, 2);
        });

        builder.Entity<PayerFormulary>(b =>
        {
            b.HasOne(x => x.PayerPlan).WithMany(p => p.Formulary).HasForeignKey(x => x.PayerPlanId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Drug).WithMany().HasForeignKey(x => x.DrugId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.PayerPlanId, x.DrugId }).IsUnique();
            b.Property(x => x.CopayPercent).HasPrecision(5, 2);
        });

        builder.Entity<Authorization>(b =>
        {
            b.HasOne(x => x.PatientPayer).WithMany().HasForeignKey(x => x.PatientPayerId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.AuthorizationCode);
            b.HasIndex(x => x.EncounterId);
            b.Property(x => x.ApprovedAmount).HasPrecision(12, 2);
        });

        builder.Entity<Claim>(b =>
        {
            b.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Payer).WithMany().HasForeignKey(x => x.PayerId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.PayerPlan).WithMany().HasForeignKey(x => x.PayerPlanId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Encounter).WithMany().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.FacilityId, x.PayerId, x.Status });
            b.HasIndex(x => x.ClaimReference);
            b.Property(x => x.GrossAmount).HasPrecision(12, 2);
            b.Property(x => x.CopayAmount).HasPrecision(12, 2);
            b.Property(x => x.ClaimableAmount).HasPrecision(12, 2);
            b.Property(x => x.ApprovedAmount).HasPrecision(12, 2);
            b.Property(x => x.PaidAmount).HasPrecision(12, 2);
        });

        builder.Entity<ClaimItem>(b =>
        {
            b.HasOne(x => x.Claim).WithMany(c => c.Items).HasForeignKey(x => x.ClaimId).OnDelete(DeleteBehavior.Cascade);
            b.Property(x => x.UnitPrice).HasPrecision(12, 2);
            b.Property(x => x.LineTotal).HasPrecision(12, 2);
            b.Property(x => x.CopayAmount).HasPrecision(12, 2);
            b.Property(x => x.ClaimableAmount).HasPrecision(12, 2);
            b.Property(x => x.ApprovedAmount).HasPrecision(12, 2);
        });

        builder.Entity<Supplier>(b =>
        {
            b.HasIndex(x => x.Code).IsUnique();
        });

        builder.Entity<InventoryItem>(b =>
        {
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => x.Name);
            b.Property(x => x.UnitPrice).HasPrecision(12, 2);
        });

        builder.Entity<InventoryStock>(b =>
        {
            b.HasOne(x => x.InventoryItem).WithMany().HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.InventoryItemId, x.StoreId, x.BatchNumber });
            b.Property(x => x.UnitCost).HasPrecision(12, 2);
        });

        builder.Entity<InventoryStockMovement>(b =>
        {
            b.HasOne(x => x.InventoryItem).WithMany().HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.PerformedBy).WithMany().HasForeignKey(x => x.PerformedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.InventoryItemId, x.CreatedAt });
            b.Property(x => x.UnitCost).HasPrecision(12, 2);
        });

        builder.Entity<PurchaseOrder>(b =>
        {
            b.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Supplier).WithMany().HasForeignKey(x => x.SupplierId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.ApprovedBy).WithMany().HasForeignKey(x => x.ApprovedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.PoNumber).IsUnique();
            b.HasIndex(x => new { x.FacilityId, x.Status });
            b.Property(x => x.SubTotal).HasPrecision(14, 2);
            b.Property(x => x.Tax).HasPrecision(14, 2);
            b.Property(x => x.TotalAmount).HasPrecision(14, 2);
        });

        builder.Entity<PurchaseOrderItem>(b =>
        {
            b.HasOne(x => x.PurchaseOrder).WithMany(p => p.Items).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Drug).WithMany().HasForeignKey(x => x.DrugId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.InventoryItem).WithMany().HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.SetNull);
            b.Property(x => x.UnitPrice).HasPrecision(12, 2);
            b.Property(x => x.LineTotal).HasPrecision(14, 2);
            b.Ignore(x => x.IsFullyReceived);
            b.Ignore(x => x.OutstandingQuantity);
        });

        builder.Entity<Grn>(b =>
        {
            b.HasOne(x => x.PurchaseOrder).WithMany(po => po.Grns).HasForeignKey(x => x.PurchaseOrderId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ReceivedBy).WithMany().HasForeignKey(x => x.ReceivedById).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.PostedBy).WithMany().HasForeignKey(x => x.PostedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.GrnNumber).IsUnique();
            b.Property(x => x.TotalReceivedValue).HasPrecision(14, 2);
        });

        builder.Entity<GrnItem>(b =>
        {
            b.HasOne(x => x.Grn).WithMany(g => g.Items).HasForeignKey(x => x.GrnId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.PurchaseOrderItem).WithMany().HasForeignKey(x => x.PurchaseOrderItemId).OnDelete(DeleteBehavior.Restrict);
            b.Property(x => x.UnitCost).HasPrecision(12, 2);
            b.Property(x => x.LineTotal).HasPrecision(14, 2);
        });

        builder.Entity<StockTake>(b =>
        {
            b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.PostedBy).WithMany().HasForeignKey(x => x.PostedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.TakeNumber).IsUnique();
        });

        builder.Entity<StockTakeItem>(b =>
        {
            b.HasOne(x => x.StockTake).WithMany(s => s.Items).HasForeignKey(x => x.StockTakeId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Drug).WithMany().HasForeignKey(x => x.DrugId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.InventoryItem).WithMany().HasForeignKey(x => x.InventoryItemId).OnDelete(DeleteBehavior.SetNull);
            b.Property(x => x.UnitCost).HasPrecision(12, 2);
            b.Property(x => x.VarianceValue).HasPrecision(14, 2);
            b.Ignore(x => x.Variance);
        });

        builder.Entity<HrProfile>(b =>
        {
            b.HasOne(x => x.User).WithOne().HasForeignKey<HrProfile>(x => x.UserId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => x.UserId).IsUnique();
            b.Property(x => x.GrossMonthlySalary).HasPrecision(14, 2);
        });

        builder.Entity<RosterShift>(b =>
        {
            b.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.StaffId, x.Date });
            b.HasIndex(x => new { x.FacilityId, x.Date });
        });

        builder.Entity<LeaveRequest>(b =>
        {
            b.HasOne(x => x.Staff).WithMany().HasForeignKey(x => x.StaffId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.DecidedBy).WithMany().HasForeignKey(x => x.DecidedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.StaffId, x.Status });
        });

        builder.Entity<ThriveHealth.Web.Models.Theatre.Theatre>(b =>
        {
            b.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.FacilityId, x.Code }).IsUnique();
        });

        builder.Entity<TheatreSession>(b =>
        {
            b.HasOne(x => x.Theatre).WithMany().HasForeignKey(x => x.TheatreId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.LeadSurgeon).WithMany().HasForeignKey(x => x.LeadSurgeonId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Anaesthetist).WithMany().HasForeignKey(x => x.AnaesthetistId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.ScrubNurse).WithMany().HasForeignKey(x => x.ScrubNurseId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.FacilityId, x.ScheduledStartUtc });
            b.HasIndex(x => x.Status);
        });

        builder.Entity<ChecklistItem>(b =>
        {
            b.HasOne(x => x.TheatreSession).WithMany(s => s.Checklist).HasForeignKey(x => x.TheatreSessionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.ConfirmedBy).WithMany().HasForeignKey(x => x.ConfirmedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.TheatreSessionId, x.Phase, x.SortOrder });
        });

        builder.Entity<SessionEvent>(b =>
        {
            b.HasOne(x => x.TheatreSession).WithMany(s => s.Events).HasForeignKey(x => x.TheatreSessionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.RecordedBy).WithMany().HasForeignKey(x => x.RecordedById).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Bill>(b =>
        {
            b.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Encounter).WithMany().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.BillNumber).IsUnique();
            b.HasIndex(x => new { x.FacilityId, x.Status });
            b.Property(x => x.GrossAmount).HasPrecision(14, 2);
            b.Property(x => x.DiscountAmount).HasPrecision(14, 2);
            b.Property(x => x.NetAmount).HasPrecision(14, 2);
            b.Property(x => x.PaidAmount).HasPrecision(14, 2);
            b.Ignore(x => x.Balance);
        });

        builder.Entity<BillItem>(b =>
        {
            b.HasOne(x => x.Bill).WithMany(p => p.Items).HasForeignKey(x => x.BillId).OnDelete(DeleteBehavior.Cascade);
            b.Property(x => x.UnitPrice).HasPrecision(12, 2);
            b.Property(x => x.LineTotal).HasPrecision(14, 2);
            b.Property(x => x.LineDiscount).HasPrecision(14, 2);
            b.Property(x => x.LineNet).HasPrecision(14, 2);
        });

        builder.Entity<Payment>(b =>
        {
            b.HasOne(x => x.Bill).WithMany(p => p.Payments).HasForeignKey(x => x.BillId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.CashierShift).WithMany(s => s.Payments).HasForeignKey(x => x.CashierShiftId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Cashier).WithMany().HasForeignKey(x => x.CashierId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.ReceiptNumber).IsUnique();
            b.Property(x => x.Amount).HasPrecision(14, 2);
        });

        builder.Entity<CashierShift>(b =>
        {
            b.HasOne(x => x.Cashier).WithMany().HasForeignKey(x => x.CashierId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => x.ShiftNumber).IsUnique();
            b.HasIndex(x => new { x.FacilityId, x.Status });
            b.Property(x => x.OpeningFloat).HasPrecision(12, 2);
            b.Property(x => x.CountedCash).HasPrecision(14, 2);
            b.Property(x => x.Variance).HasPrecision(12, 2);
        });

        builder.Entity<ProcedureOrder>(b =>
        {
            b.HasOne(x => x.Encounter).WithMany(e => e.ProcedureOrders).HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.OrderedBy).WithMany().HasForeignKey(x => x.OrderedById).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<Prescription>(b =>
        {
            b.HasOne(x => x.Encounter).WithMany(e => e.Prescriptions).HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.PrescribedBy).WithMany().HasForeignKey(x => x.PrescribedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.PatientId, x.IssuedAt });
        });

        builder.Entity<PrescriptionItem>(b =>
        {
            b.HasOne(x => x.Prescription).WithMany(p => p.Items).HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<IcdCode>(b =>
        {
            b.HasIndex(x => x.Code).IsUnique();
            b.HasIndex(x => x.IsCommon);
        });

        builder.Entity<DotPhrase>(b =>
        {
            b.HasIndex(x => new { x.OwnerId, x.Trigger }).IsUnique();
        });

        builder.Entity<PrescriptionItem>(b =>
        {
            b.HasOne(x => x.Drug).WithMany().HasForeignKey(x => x.DrugId).OnDelete(DeleteBehavior.SetNull);
            b.Ignore(x => x.IsFullyDispensed);
        });

        builder.Entity<Drug>(b =>
        {
            b.HasIndex(x => x.GenericName);
            b.HasIndex(x => x.NafdacNumber);
            b.Property(x => x.UnitPrice).HasPrecision(12, 2);
            b.Ignore(x => x.IsControlled);
            b.Ignore(x => x.Display);
        });

        builder.Entity<DrugInteraction>(b =>
        {
            b.HasIndex(x => new { x.DrugAKey, x.DrugBKey }).IsUnique();
        });

        builder.Entity<PharmacyStore>(b =>
        {
            b.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.FacilityId, x.Code }).IsUnique();
        });

        builder.Entity<DrugStock>(b =>
        {
            b.HasOne(x => x.Drug).WithMany().HasForeignKey(x => x.DrugId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.DrugId, x.StoreId, x.BatchNumber }).IsUnique();
            b.Property(x => x.UnitCost).HasPrecision(12, 2);
            b.Ignore(x => x.IsExpired);
            b.Ignore(x => x.ExpiringSoon);
        });

        builder.Entity<StockMovement>(b =>
        {
            b.HasOne(x => x.Drug).WithMany().HasForeignKey(x => x.DrugId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.PerformedBy).WithMany().HasForeignKey(x => x.PerformedById).OnDelete(DeleteBehavior.SetNull);
            b.Property(x => x.UnitCost).HasPrecision(12, 2);
            b.HasIndex(x => new { x.DrugId, x.CreatedAt });
            b.HasIndex(x => x.Kind);
        });

        builder.Entity<Dispense>(b =>
        {
            b.HasOne(x => x.Prescription).WithMany().HasForeignKey(x => x.PrescriptionId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Store).WithMany().HasForeignKey(x => x.StoreId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.DispensedBy).WithMany().HasForeignKey(x => x.DispensedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.FacilityId, x.DispensedAt });
            b.Property(x => x.TotalAmount).HasPrecision(12, 2);
        });

        builder.Entity<DispenseItem>(b =>
        {
            b.HasOne(x => x.Dispense).WithMany(d => d.Items).HasForeignKey(x => x.DispenseId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.PrescriptionItem).WithMany().HasForeignKey(x => x.PrescriptionItemId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Drug).WithMany().HasForeignKey(x => x.DrugId).OnDelete(DeleteBehavior.SetNull);
            b.Property(x => x.UnitPrice).HasPrecision(12, 2);
            b.Property(x => x.LineTotal).HasPrecision(12, 2);
        });

        builder.Entity<Ward>(b =>
        {
            b.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.FacilityId, x.Code }).IsUnique();
        });

        builder.Entity<Bed>(b =>
        {
            b.HasOne(x => x.Ward).WithMany(w => w.Beds).HasForeignKey(x => x.WardId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.WardId, x.BedNumber }).IsUnique();
            b.HasIndex(x => x.Status);
        });

        builder.Entity<Admission>(b =>
        {
            b.HasOne(x => x.Facility).WithMany().HasForeignKey(x => x.FacilityId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Ward).WithMany().HasForeignKey(x => x.WardId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Bed).WithMany().HasForeignKey(x => x.BedId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.AdmittingDoctor).WithMany().HasForeignKey(x => x.AdmittingDoctorId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.SourceEncounter).WithMany().HasForeignKey(x => x.SourceEncounterId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.AdmissionEncounter).WithMany().HasForeignKey(x => x.AdmissionEncounterId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.DischargedBy).WithMany().HasForeignKey(x => x.DischargedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.FacilityId, x.Status });
            b.HasIndex(x => new { x.PatientId, x.AdmittedAt });
            b.HasIndex(x => x.WardId);
            b.Ignore(x => x.LengthOfStay);
        });

        builder.Entity<BedAllocation>(b =>
        {
            b.HasOne(x => x.Admission).WithMany(a => a.BedHistory).HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Bed).WithMany().HasForeignKey(x => x.BedId).OnDelete(DeleteBehavior.Restrict);
            b.HasIndex(x => new { x.AdmissionId, x.FromUtc });
        });

        builder.Entity<InpatientMedication>(b =>
        {
            b.HasOne(x => x.Admission).WithMany(a => a.Medications).HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Drug).WithMany().HasForeignKey(x => x.DrugId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.PrescribedBy).WithMany().HasForeignKey(x => x.PrescribedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.AdmissionId, x.Status });
        });

        builder.Entity<MarSlot>(b =>
        {
            b.HasOne(x => x.InpatientMedication).WithMany(m => m.Slots).HasForeignKey(x => x.InpatientMedicationId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.AdministeredBy).WithMany().HasForeignKey(x => x.AdministeredById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.InpatientMedicationId, x.ScheduledUtc });
            b.HasIndex(x => x.Status);
        });

        builder.Entity<FluidEntry>(b =>
        {
            b.HasOne(x => x.Admission).WithMany(a => a.Fluids).HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.RecordedBy).WithMany().HasForeignKey(x => x.RecordedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.AdmissionId, x.RecordedUtc });
        });

        builder.Entity<NursingNote>(b =>
        {
            b.HasOne(x => x.Admission).WithMany(a => a.NursingNotes).HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.RecordedBy).WithMany().HasForeignKey(x => x.RecordedById).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<WardRoundEntry>(b =>
        {
            b.HasOne(x => x.Admission).WithMany(a => a.WardRounds).HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.RecordedBy).WithMany().HasForeignKey(x => x.RecordedById).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AnteNatalRecord>(b =>
        {
            b.HasIndex(x => x.AncNumber).IsUnique();
            b.HasIndex(x => new { x.FacilityId, x.Status });
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
            b.Property(x => x.HeightCm).HasPrecision(6, 2);
            b.Property(x => x.BookingWeightKg).HasPrecision(6, 2);
            b.Property(x => x.HaemoglobinGdl).HasPrecision(5, 2);
        });

        builder.Entity<AnteNatalVisit>(b =>
        {
            b.HasOne(x => x.AnteNatalRecord).WithMany(r => r.Visits).HasForeignKey(x => x.AnteNatalRecordId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.RecordedBy).WithMany().HasForeignKey(x => x.RecordedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.AnteNatalRecordId, x.VisitDate });
            b.Property(x => x.WeightKg).HasPrecision(6, 2);
            b.Property(x => x.FundalHeightCm).HasPrecision(5, 1);
        });

        builder.Entity<Delivery>(b =>
        {
            b.HasOne(x => x.AnteNatalRecord).WithMany(r => r.Deliveries).HasForeignKey(x => x.AnteNatalRecordId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Accoucheur).WithMany().HasForeignKey(x => x.AccoucheurId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.FacilityId, x.DeliveryUtc });
        });

        builder.Entity<Newborn>(b =>
        {
            b.HasOne(x => x.Delivery).WithMany(d => d.Newborns).HasForeignKey(x => x.DeliveryId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.SetNull);
            b.Property(x => x.LengthCm).HasPrecision(5, 1);
            b.Property(x => x.HeadCircumferenceCm).HasPrecision(5, 1);
        });

        builder.Entity<PostnatalVisit>(b =>
        {
            b.HasOne(x => x.AnteNatalRecord).WithMany(r => r.PostnatalVisits).HasForeignKey(x => x.AnteNatalRecordId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.RecordedBy).WithMany().HasForeignKey(x => x.RecordedById).OnDelete(DeleteBehavior.SetNull);
            b.Property(x => x.MotherTemperatureC).HasPrecision(4, 1);
            b.Property(x => x.BabyWeightKg).HasPrecision(5, 2);
        });

        builder.Entity<ChildProfile>(b =>
        {
            b.HasIndex(x => x.PatientId).IsUnique();
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.MotherPatient).WithMany().HasForeignKey(x => x.MotherPatientId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.FatherPatient).WithMany().HasForeignKey(x => x.FatherPatientId).OnDelete(DeleteBehavior.SetNull);
            b.Property(x => x.BirthLengthCm).HasPrecision(5, 1);
            b.Property(x => x.BirthHeadCircCm).HasPrecision(5, 1);
        });

        builder.Entity<GrowthMeasurement>(b =>
        {
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.RecordedBy).WithMany().HasForeignKey(x => x.RecordedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.PatientId, x.DateOfMeasurement });
            b.Property(x => x.WeightKg).HasPrecision(5, 2);
            b.Property(x => x.HeightCm).HasPrecision(5, 1);
            b.Property(x => x.HeadCircumferenceCm).HasPrecision(5, 1);
            b.Property(x => x.MuacCm).HasPrecision(5, 1);
            b.Property(x => x.BmiKgM2).HasPrecision(5, 2);
        });

        builder.Entity<Vaccine>(b =>
        {
            b.HasIndex(x => x.Code).IsUnique();
        });

        builder.Entity<VaccineSchedule>(b =>
        {
            b.HasOne(x => x.Vaccine).WithMany(v => v.Schedule).HasForeignKey(x => x.VaccineId).OnDelete(DeleteBehavior.Cascade);
            b.HasIndex(x => new { x.VaccineId, x.DoseLabel }).IsUnique();
        });

        builder.Entity<ImmunizationDose>(b =>
        {
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Vaccine).WithMany().HasForeignKey(x => x.VaccineId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.VaccineSchedule).WithMany().HasForeignKey(x => x.VaccineScheduleId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.AdministeredBy).WithMany().HasForeignKey(x => x.AdministeredById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.PatientId, x.VaccineId, x.DoseLabel }).IsUnique();
            b.HasIndex(x => new { x.FacilityId, x.Status, x.DueDate });
        });

        builder.Entity<IcuChartEntry>(b =>
        {
            b.HasOne(x => x.Admission).WithMany().HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.RecordedBy).WithMany().HasForeignKey(x => x.RecordedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.AdmissionId, x.RecordedUtc });
            b.Property(x => x.SpO2).HasPrecision(5, 2);
            b.Property(x => x.TemperatureC).HasPrecision(4, 1);
            b.Property(x => x.FiO2).HasPrecision(4, 2);
            b.Ignore(x => x.GcsTotal);
        });

        builder.Entity<DialysisSession>(b =>
        {
            b.HasIndex(x => x.SessionNumber).IsUnique();
            b.HasOne(x => x.Admission).WithMany().HasForeignKey(x => x.AdmissionId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Operator).WithMany().HasForeignKey(x => x.OperatorId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.FacilityId, x.StartUtc });
            b.Property(x => x.PreWeightKg).HasPrecision(6, 2);
            b.Property(x => x.PostWeightKg).HasPrecision(6, 2);
            b.Property(x => x.BloodFlowMlMin).HasPrecision(7, 2);
            b.Property(x => x.DialysateFlowMlMin).HasPrecision(7, 2);
            b.Property(x => x.HeparinUnits).HasPrecision(8, 2);
        });

        builder.Entity<BloodDonor>(b =>
        {
            b.HasIndex(x => x.DonorNumber).IsUnique();
            b.HasIndex(x => new { x.FacilityId, x.BloodGroup });
        });

        builder.Entity<BloodUnit>(b =>
        {
            b.HasIndex(x => x.UnitNumber).IsUnique();
            b.HasOne(x => x.BloodDonor).WithMany().HasForeignKey(x => x.BloodDonorId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.ReservedForPatient).WithMany().HasForeignKey(x => x.ReservedForPatientId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.CrossMatch).WithMany(c => c.Units).HasForeignKey(x => x.CrossMatchId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.FacilityId, x.Status });
            b.HasIndex(x => new { x.BloodGroup, x.Component, x.Status });
        });

        builder.Entity<BloodCrossMatch>(b =>
        {
            b.HasIndex(x => x.CrossMatchNumber).IsUnique();
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.RequestedBy).WithMany().HasForeignKey(x => x.RequestedById).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.CompatibilityCheckedBy).WithMany().HasForeignKey(x => x.CompatibilityCheckedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.FacilityId, x.Status });
        });

        builder.Entity<MortuaryEntry>(b =>
        {
            b.HasIndex(x => x.MortuaryNumber).IsUnique();
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.ReceivedBy).WithMany().HasForeignKey(x => x.ReceivedById).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.ReleasedBy).WithMany().HasForeignKey(x => x.ReleasedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.FacilityId, x.Status });
            b.Ignore(x => x.LengthOfStayDays);
        });

        builder.Entity<AlliedSession>(b =>
        {
            b.HasIndex(x => x.SessionNumber).IsUnique();
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Provider).WithMany().HasForeignKey(x => x.ProviderId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.FacilityId, x.ServiceLine, x.Status });
            b.HasIndex(x => new { x.FacilityId, x.ScheduledUtc });
        });

        builder.Entity<Referral>(b =>
        {
            b.HasIndex(x => x.ReferralNumber).IsUnique();
            b.HasIndex(x => new { x.FacilityId, x.Status, x.CreatedAt });
            b.HasOne(x => x.Encounter).WithMany().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.ReferringClinician).WithMany().HasForeignKey(x => x.ReferringClinicianId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.ReferredToClinician).WithMany().HasForeignKey(x => x.ReferredToClinicianId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<MedicalCertificate>(b =>
        {
            b.HasIndex(x => x.CertificateNumber).IsUnique();
            b.HasIndex(x => new { x.FacilityId, x.IssuedAt });
            b.HasOne(x => x.Encounter).WithMany().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.IssuedBy).WithMany().HasForeignKey(x => x.IssuedById).OnDelete(DeleteBehavior.SetNull);
            b.Ignore(x => x.DaysOff);
        });

        builder.Entity<TeleChatMessage>(b =>
        {
            b.HasIndex(x => new { x.PatientId, x.SentAt });
            b.HasIndex(x => new { x.TeleSessionId, x.SentAt });
            b.HasOne(x => x.TeleSession).WithMany().HasForeignKey(x => x.TeleSessionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.SenderUser).WithMany().HasForeignKey(x => x.SenderUserId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.RepliesToMessage).WithMany().HasForeignKey(x => x.RepliesToMessageId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<ChatPackage>(b =>
        {
            b.HasIndex(x => x.PackageNumber).IsUnique();
            b.HasIndex(x => new { x.PatientId, x.ExpiresAt });
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Bill).WithMany().HasForeignKey(x => x.BillId).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<TeleChatAttachment>(b =>
        {
            b.HasIndex(x => x.MessageId);
            b.HasOne(x => x.Message).WithMany().HasForeignKey(x => x.MessageId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<TeleSessionParticipant>(b =>
        {
            b.HasIndex(x => new { x.TeleSessionId, x.ClinicianId }).IsUnique();
            b.HasOne(x => x.TeleSession).WithMany().HasForeignKey(x => x.TeleSessionId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.Clinician).WithMany().HasForeignKey(x => x.ClinicianId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<ThriveHealth.Web.Models.Integrations.WebPushSubscription>(b =>
        {
            b.HasIndex(x => new { x.OwnerType, x.OwnerKey });
            b.HasIndex(x => x.Endpoint).IsUnique();
        });

        builder.Entity<TeleSession>(b =>
        {
            b.HasIndex(x => x.SessionNumber).IsUnique();
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Clinician).WithMany().HasForeignKey(x => x.ClinicianId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Encounter).WithMany().HasForeignKey(x => x.EncounterId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Bill).WithMany().HasForeignKey(x => x.BillId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.FacilityId, x.Status, x.ScheduledStartUtc });
            b.Ignore(x => x.DurationMinutes);
        });

        builder.Entity<PortalAccount>(b =>
        {
            b.HasIndex(x => x.Email).IsUnique();
            b.HasIndex(x => x.PatientId).IsUnique();
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
        });

        builder.Entity<PortalSymptomIntake>(b =>
        {
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.Cascade);
            b.HasOne(x => x.TeleSession).WithMany().HasForeignKey(x => x.TeleSessionId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.PatientId, x.SubmittedAt });
        });

        builder.Entity<NotifiableDisease>(b =>
        {
            b.HasIndex(x => x.Code).IsUnique();
        });

        builder.Entity<IdsrCase>(b =>
        {
            b.HasIndex(x => x.CaseNumber).IsUnique();
            b.HasOne(x => x.NotifiableDisease).WithMany().HasForeignKey(x => x.NotifiableDiseaseId).OnDelete(DeleteBehavior.Restrict);
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.ReportedBy).WithMany().HasForeignKey(x => x.ReportedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.FacilityId, x.Status, x.OnsetDate });
            b.HasIndex(x => new { x.NotifiableDiseaseId, x.OnsetDate });
        });

        builder.Entity<NhmisReport>(b =>
        {
            b.HasIndex(x => new { x.FacilityId, x.Period }).IsUnique();
            b.HasOne(x => x.GeneratedBy).WithMany().HasForeignKey(x => x.GeneratedById).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.SubmittedBy).WithMany().HasForeignKey(x => x.SubmittedById).OnDelete(DeleteBehavior.SetNull);
        });

        builder.Entity<AuditEntry>(b =>
        {
            b.HasOne(x => x.ActorUser).WithMany().HasForeignKey(x => x.ActorUserId).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => x.AtUtc);
            b.HasIndex(x => new { x.FacilityId, x.Category, x.AtUtc });
            b.HasIndex(x => new { x.ActorUserId, x.AtUtc });
            b.HasIndex(x => new { x.EntityType, x.EntityKey });
        });

        builder.Entity<SmsMessage>(b =>
        {
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.FacilityId, x.Status, x.CreatedAt });
        });

        builder.Entity<EmailMessage>(b =>
        {
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.CreatedBy).WithMany().HasForeignKey(x => x.CreatedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.FacilityId, x.Status, x.CreatedAt });
        });

        builder.Entity<PaymentTransaction>(b =>
        {
            b.HasIndex(x => x.Reference).IsUnique();
            b.HasOne(x => x.Bill).WithMany().HasForeignKey(x => x.BillId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Patient).WithMany().HasForeignKey(x => x.PatientId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.Payment).WithMany().HasForeignKey(x => x.PaymentId).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.InitiatedBy).WithMany().HasForeignKey(x => x.InitiatedById).OnDelete(DeleteBehavior.SetNull);
            b.HasIndex(x => new { x.FacilityId, x.Status, x.CreatedAt });
            b.Property(x => x.Amount).HasPrecision(14, 2);
        });

        builder.Entity<RolePermission>(b =>
        {
            b.HasIndex(x => new { x.RoleId, x.Permission }).IsUnique();
            b.HasIndex(x => x.Permission);
            b.Property(x => x.RoleId).HasMaxLength(450).IsRequired();
            b.Property(x => x.Permission).HasMaxLength(80).IsRequired();
            b.Property(x => x.GrantedById).HasMaxLength(450);
        });

        builder.Entity<AiSuggestion>(b =>
        {
            b.HasIndex(x => new { x.FacilityId, x.Feature, x.CreatedAtUtc });
            b.HasIndex(x => new { x.EntityType, x.EntityKey });
            b.Property(x => x.RequestedById).HasMaxLength(450);
            b.Property(x => x.ReviewedById).HasMaxLength(450);
            b.HasOne(x => x.RequestedBy).WithMany().HasForeignKey(x => x.RequestedById).OnDelete(DeleteBehavior.SetNull);
            b.HasOne(x => x.ReviewedBy).WithMany().HasForeignKey(x => x.ReviewedById).OnDelete(DeleteBehavior.SetNull);
        });

        ApplyTenantScoping(builder);
    }

    /// <summary>
    /// Walks every mapped entity and stamps it with tenant scoping unless explicitly excluded.
    /// For entities that don't already declare a CLR <c>TenantId</c> property we add a shadow
    /// property + index + FK; everything gets a global query filter that compares
    /// <see cref="CurrentTenantIdForFilter"/> against the row's tenant id, treating a null
    /// context (admin / marketing / migrations) as "no filter".
    /// </summary>
    private void ApplyTenantScoping(ModelBuilder builder)
    {
        foreach (var entityType in builder.Model.GetEntityTypes().ToList())
        {
            var clr = entityType.ClrType;
            if (TenantUnscopedTypes.Contains(clr)) continue;
            if (IsIdentityType(clr)) continue; // covers IdentityRole, IdentityUserRole, etc.
            if (clr == typeof(ApplicationUser)) continue;       // identity user — keeps real TenantId, no auto-filter
            if (clr == typeof(TenantSubscription)) continue;    // already configured; tenant filter applied below
            if (clr == typeof(TenantPayment)) continue;         // ditto

            var entityBuilder = builder.Entity(clr);
            var hasRealTenantId = clr.GetProperty("TenantId") != null;

            if (!hasRealTenantId)
            {
                entityBuilder.Property<int>("TenantId").IsRequired();
                entityBuilder.HasIndex("TenantId");
                // Restrict so we don't cascade-delete a tenant's rows out from under foreign-key
                // children — explicit cleanup runs through the SuperAdmin tenants flow.
                entityBuilder.HasOne(typeof(Tenant)).WithMany().HasForeignKey("TenantId").OnDelete(DeleteBehavior.Restrict);
            }

            entityBuilder.HasQueryFilter(BuildTenantFilter(clr, hasRealTenantId));
        }

        // Tenancy meta — already have real TenantId + FK from earlier config, just add the filter.
        builder.Entity<TenantSubscription>().HasQueryFilter(BuildTenantFilter(typeof(TenantSubscription), useRealProperty: true));
        builder.Entity<TenantPayment>().HasQueryFilter(BuildTenantFilter(typeof(TenantPayment), useRealProperty: true));
    }

    /// <summary>
    /// Builds <c>e =&gt; this.CurrentTenantIdForFilter == null
    ///                    || (int?)e.TenantId == this.CurrentTenantIdForFilter</c>
    /// as an <see cref="Expression"/> tree so it can be passed to
    /// <see cref="EntityTypeBuilder.HasQueryFilter"/> for any entity type, regardless of
    /// whether <c>TenantId</c> is a real CLR property or a shadow property.
    ///
    /// We compare <c>int?</c> to <c>int?</c> rather than dereferencing
    /// <see cref="Nullable{T}.Value"/>: EF Core evaluates both sides of <c>OrElse</c> at
    /// query-translation time, so a <c>.Value</c> access on a null context would throw
    /// before the short-circuit kicks in.
    /// </summary>
    private LambdaExpression BuildTenantFilter(Type clrType, bool useRealProperty)
    {
        var entityParam = Expression.Parameter(clrType, "e");

        // Read through a closure so EF re-queries each time — without this the first
        // query's tenant id would be baked into the compiled filter delegate.
        Expression<Func<int?>> currentIdExpr = () => CurrentTenantIdForFilter;
        var currentId = currentIdExpr.Body; // int?

        Expression tenantIdAccess = useRealProperty
            ? Expression.Property(entityParam, "TenantId")
            : Expression.Call(
                typeof(EF), nameof(EF.Property), new[] { typeof(int) },
                entityParam, Expression.Constant("TenantId"));

        var tenantIdAsNullable = Expression.Convert(tenantIdAccess, typeof(int?));

        var nullCheck = Expression.Equal(currentId, Expression.Constant(null, typeof(int?)));
        var idEq = Expression.Equal(currentId, tenantIdAsNullable);

        var body = Expression.OrElse(nullCheck, idEq);
        return Expression.Lambda(body, entityParam);
    }
}

public class HospitalNumberCounter
{
    public int FacilityId { get; set; }
    public int Year { get; set; }
    public int LastSequence { get; set; }
}
