using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Tenant_ScopeAllEntities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "WebPushSubscriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Wards",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "WardRoundEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Vitals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "TriageAssessments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "TicketCounters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "TheatreSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "TheatreSessionEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Theatres",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "TheatreChecklistItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "TeleSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "TeleSessionParticipants",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "TeleChatMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "TeleChatAttachments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Suppliers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "StockTakes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "StockTakeItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "StockMovements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "SoapNotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "SmsMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "RosterShifts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Rooms",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ResusEvents",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ResusBays",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ReminderJobs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Referrals",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "QueueEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PurchaseOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PurchaseOrderItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ProcedureOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Problems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Prescriptions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PrescriptionItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PostnatalVisits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PortalSymptomIntakes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PortalAccounts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PharmacyStores",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PaymentTransactions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Payments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Patients",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PatientPayers",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PatientNextOfKin",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "PatientDocuments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "NursingNotes",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "NhmisReports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Newborns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "MpiPotentialMatches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "MpiMergeAudits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "MortuaryEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Medications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "MedicalCertificates",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "MarSlots",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "LeaveRequests",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "LabResultValues",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "LabResults",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "LabOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "InventoryStocks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "InventoryStockMovements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "InventoryItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "InpatientMedications",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ImmunizationDoses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ImagingReports",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ImagingOrders",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "IdsrCases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "IcuChartEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "HrProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "HospitalNumberCounters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "GrowthMeasurements",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Grns",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "GrnItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "FluidEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Encounters",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "EncounterDiagnoses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "EmailMessages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "DrugStocks",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "DotPhrases",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Dispenses",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "DispenseItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "DialysisSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Deliveries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Clinics",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ClinicianTimeOffs",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ClinicianAvailabilities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Claims",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ClaimItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ChildProfiles",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "ChatPackages",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "CashierShifts",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "BloodUnits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "BloodDonors",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "BloodCrossMatches",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Bills",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "BillItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Beds",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "BedAllocations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Authorizations",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "AuditEntries",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Appointments",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "AnteNatalVisits",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "AnteNatalRecords",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "AlliedSessions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Allergies",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "AiSuggestions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Admissions",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            // -----------------------------------------------------------------------
            // Backfill TenantId for every row before foreign keys are added below.
            // Every newly-added column was created with default 0, which would violate
            // the FK to Tenants if left unchanged. Resolution order:
            //   1. Tables with FacilityId join Facilities to copy its TenantId.
            //   2. Child tables (Patient/Encounter/Bill/Admission/etc.) inherit from
            //      their parent — running after step 1 so the parent already has its
            //      TenantId set.
            //   3. Grandchildren run after their parents.
            //   4. Anything still at 0 (e.g. orphaned legacy rows or tables with no
            //      facility link such as Suppliers/InventoryItems) falls back to the
            //      lowest tenant id — safe in single-tenant dev installs and easy to
            //      audit afterwards. Production deployments with multi-tenant legacy
            //      data should hand-edit this fallback.
            // -----------------------------------------------------------------------
            migrationBuilder.Sql(@"
                -- Round 1: tables with FacilityId
                UPDATE ""Admissions"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""AiSuggestions"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""AlliedSessions"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""AnteNatalRecords"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Appointments"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""AuditEntries"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Bills"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""BloodCrossMatches"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""BloodDonors"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""BloodUnits"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""CashierShifts"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""ChatPackages"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Claims"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Clinics"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Deliveries"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""DialysisSessions"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Dispenses"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""EmailMessages"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Encounters"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""GrowthMeasurements"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Grns"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""HospitalNumberCounters"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""IcuChartEntries"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""IdsrCases"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""ImmunizationDoses"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""MedicalCertificates"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""MortuaryEntries"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""NhmisReports"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Patients"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""PaymentTransactions"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""PharmacyStores"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""PurchaseOrders"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""QueueEntries"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Referrals"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""ResusBays"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Rooms"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""RosterShifts"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""SmsMessages"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""StockTakes"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""TeleSessions"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""TheatreSessions"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Theatres"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""TicketCounters"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Wards"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""WebPushSubscriptions"" t SET ""TenantId"" = f.""TenantId"" FROM ""Facilities"" f WHERE t.""FacilityId"" = f.""Id"" AND t.""TenantId"" = 0;

                -- Round 2: children of Patient / Encounter / Bill / Admission / etc.
                UPDATE ""Allergies"" t SET ""TenantId"" = p.""TenantId"" FROM ""Patients"" p WHERE t.""PatientId"" = p.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Medications"" t SET ""TenantId"" = p.""TenantId"" FROM ""Patients"" p WHERE t.""PatientId"" = p.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Vitals"" t SET ""TenantId"" = p.""TenantId"" FROM ""Patients"" p WHERE t.""PatientId"" = p.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""PatientDocuments"" t SET ""TenantId"" = p.""TenantId"" FROM ""Patients"" p WHERE t.""PatientId"" = p.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""PatientNextOfKin"" t SET ""TenantId"" = p.""TenantId"" FROM ""Patients"" p WHERE t.""PatientId"" = p.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""PatientPayers"" t SET ""TenantId"" = p.""TenantId"" FROM ""Patients"" p WHERE t.""PatientId"" = p.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Problems"" t SET ""TenantId"" = p.""TenantId"" FROM ""Patients"" p WHERE t.""PatientId"" = p.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""ChildProfiles"" t SET ""TenantId"" = p.""TenantId"" FROM ""Patients"" p WHERE t.""PatientId"" = p.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""PortalAccounts"" t SET ""TenantId"" = p.""TenantId"" FROM ""Patients"" p WHERE t.""PatientId"" = p.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""PortalSymptomIntakes"" t SET ""TenantId"" = p.""TenantId"" FROM ""Patients"" p WHERE t.""PatientId"" = p.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""MpiPotentialMatches"" t SET ""TenantId"" = p.""TenantId"" FROM ""Patients"" p WHERE t.""PatientAId"" = p.""Id"" AND t.""TenantId"" = 0;

                UPDATE ""BillItems"" t SET ""TenantId"" = b.""TenantId"" FROM ""Bills"" b WHERE t.""BillId"" = b.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Payments"" t SET ""TenantId"" = b.""TenantId"" FROM ""Bills"" b WHERE t.""BillId"" = b.""Id"" AND t.""TenantId"" = 0;

                UPDATE ""SoapNotes"" t SET ""TenantId"" = e.""TenantId"" FROM ""Encounters"" e WHERE t.""EncounterId"" = e.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""EncounterDiagnoses"" t SET ""TenantId"" = e.""TenantId"" FROM ""Encounters"" e WHERE t.""EncounterId"" = e.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""ImagingOrders"" t SET ""TenantId"" = e.""TenantId"" FROM ""Encounters"" e WHERE t.""EncounterId"" = e.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""LabOrders"" t SET ""TenantId"" = e.""TenantId"" FROM ""Encounters"" e WHERE t.""EncounterId"" = e.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Prescriptions"" t SET ""TenantId"" = e.""TenantId"" FROM ""Encounters"" e WHERE t.""EncounterId"" = e.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""ProcedureOrders"" t SET ""TenantId"" = e.""TenantId"" FROM ""Encounters"" e WHERE t.""EncounterId"" = e.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""ResusEvents"" t SET ""TenantId"" = e.""TenantId"" FROM ""Encounters"" e WHERE t.""EncounterId"" = e.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""TriageAssessments"" t SET ""TenantId"" = e.""TenantId"" FROM ""Encounters"" e WHERE t.""EncounterId"" = e.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Authorizations"" t SET ""TenantId"" = e.""TenantId"" FROM ""Encounters"" e WHERE t.""EncounterId"" = e.""Id"" AND t.""TenantId"" = 0;

                UPDATE ""BedAllocations"" t SET ""TenantId"" = a.""TenantId"" FROM ""Admissions"" a WHERE t.""AdmissionId"" = a.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""FluidEntries"" t SET ""TenantId"" = a.""TenantId"" FROM ""Admissions"" a WHERE t.""AdmissionId"" = a.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""InpatientMedications"" t SET ""TenantId"" = a.""TenantId"" FROM ""Admissions"" a WHERE t.""AdmissionId"" = a.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""NursingNotes"" t SET ""TenantId"" = a.""TenantId"" FROM ""Admissions"" a WHERE t.""AdmissionId"" = a.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""WardRoundEntries"" t SET ""TenantId"" = a.""TenantId"" FROM ""Admissions"" a WHERE t.""AdmissionId"" = a.""Id"" AND t.""TenantId"" = 0;

                UPDATE ""Beds"" t SET ""TenantId"" = w.""TenantId"" FROM ""Wards"" w WHERE t.""WardId"" = w.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""ClinicianAvailabilities"" t SET ""TenantId"" = c.""TenantId"" FROM ""Clinics"" c WHERE t.""ClinicId"" = c.""Id"" AND t.""TenantId"" = 0;

                UPDATE ""DrugStocks"" t SET ""TenantId"" = s.""TenantId"" FROM ""PharmacyStores"" s WHERE t.""StoreId"" = s.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""StockMovements"" t SET ""TenantId"" = s.""TenantId"" FROM ""PharmacyStores"" s WHERE t.""StoreId"" = s.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""DispenseItems"" t SET ""TenantId"" = d.""TenantId"" FROM ""Dispenses"" d WHERE t.""DispenseId"" = d.""Id"" AND t.""TenantId"" = 0;

                UPDATE ""TheatreChecklistItems"" t SET ""TenantId"" = ts.""TenantId"" FROM ""TheatreSessions"" ts WHERE t.""TheatreSessionId"" = ts.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""TheatreSessionEvents"" t SET ""TenantId"" = ts.""TenantId"" FROM ""TheatreSessions"" ts WHERE t.""TheatreSessionId"" = ts.""Id"" AND t.""TenantId"" = 0;

                UPDATE ""TeleChatMessages"" t SET ""TenantId"" = ts.""TenantId"" FROM ""TeleSessions"" ts WHERE t.""TeleSessionId"" = ts.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""TeleSessionParticipants"" t SET ""TenantId"" = ts.""TenantId"" FROM ""TeleSessions"" ts WHERE t.""TeleSessionId"" = ts.""Id"" AND t.""TenantId"" = 0;

                UPDATE ""AnteNatalVisits"" t SET ""TenantId"" = a.""TenantId"" FROM ""AnteNatalRecords"" a WHERE t.""AnteNatalRecordId"" = a.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""PostnatalVisits"" t SET ""TenantId"" = a.""TenantId"" FROM ""AnteNatalRecords"" a WHERE t.""AnteNatalRecordId"" = a.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""Newborns"" t SET ""TenantId"" = d.""TenantId"" FROM ""Deliveries"" d WHERE t.""DeliveryId"" = d.""Id"" AND t.""TenantId"" = 0;

                UPDATE ""PurchaseOrderItems"" t SET ""TenantId"" = po.""TenantId"" FROM ""PurchaseOrders"" po WHERE t.""PurchaseOrderId"" = po.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""GrnItems"" t SET ""TenantId"" = g.""TenantId"" FROM ""Grns"" g WHERE t.""GrnId"" = g.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""StockTakeItems"" t SET ""TenantId"" = s.""TenantId"" FROM ""StockTakes"" s WHERE t.""StockTakeId"" = s.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""ClaimItems"" t SET ""TenantId"" = c.""TenantId"" FROM ""Claims"" c WHERE t.""ClaimId"" = c.""Id"" AND t.""TenantId"" = 0;

                UPDATE ""LabResults"" t SET ""TenantId"" = lo.""TenantId"" FROM ""LabOrders"" lo WHERE t.""LabOrderId"" = lo.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""ImagingReports"" t SET ""TenantId"" = io.""TenantId"" FROM ""ImagingOrders"" io WHERE t.""ImagingOrderId"" = io.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""PrescriptionItems"" t SET ""TenantId"" = p.""TenantId"" FROM ""Prescriptions"" p WHERE t.""PrescriptionId"" = p.""Id"" AND t.""TenantId"" = 0;

                -- Round 3: grandchildren
                UPDATE ""LabResultValues"" t SET ""TenantId"" = lr.""TenantId"" FROM ""LabResults"" lr WHERE t.""LabResultId"" = lr.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""TeleChatAttachments"" t SET ""TenantId"" = m.""TenantId"" FROM ""TeleChatMessages"" m WHERE t.""MessageId"" = m.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""MarSlots"" t SET ""TenantId"" = im.""TenantId"" FROM ""InpatientMedications"" im WHERE t.""InpatientMedicationId"" = im.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""MpiMergeAudits"" t SET ""TenantId"" = p.""TenantId"" FROM ""Patients"" p WHERE t.""MasterPatientId"" = p.""Id"" AND t.""TenantId"" = 0;
                UPDATE ""ReminderJobs"" t SET ""TenantId"" = a.""TenantId"" FROM ""Appointments"" a WHERE t.""AppointmentId"" = a.""Id"" AND t.""TenantId"" = 0;

                -- User-rooted (HrProfile etc. — staff records)
                UPDATE ""HrProfiles"" t SET ""TenantId"" = u.""TenantId"" FROM ""AspNetUsers"" u WHERE t.""UserId"" = u.""Id"" AND t.""TenantId"" = 0 AND u.""TenantId"" IS NOT NULL;
                UPDATE ""DotPhrases"" t SET ""TenantId"" = u.""TenantId"" FROM ""AspNetUsers"" u WHERE t.""OwnerId"" = u.""Id"" AND t.""TenantId"" = 0 AND u.""TenantId"" IS NOT NULL;
                UPDATE ""LeaveRequests"" t SET ""TenantId"" = u.""TenantId"" FROM ""AspNetUsers"" u WHERE t.""StaffId"" = u.""Id"" AND t.""TenantId"" = 0 AND u.""TenantId"" IS NOT NULL;
                UPDATE ""ClinicianTimeOffs"" t SET ""TenantId"" = u.""TenantId"" FROM ""AspNetUsers"" u WHERE t.""ClinicianId"" = u.""Id"" AND t.""TenantId"" = 0 AND u.""TenantId"" IS NOT NULL;

                -- Round 4 (final fallback): anything still 0 falls back to the lowest tenant id.
                -- Targets tables with no facility/patient/encounter link (Suppliers, InventoryItems,
                -- InventoryStocks, InventoryStockMovements) and any orphan rows in other tables.
                DO $$
                DECLARE
                    fallback_tenant_id integer;
                    r record;
                BEGIN
                    SELECT MIN(""Id"") INTO fallback_tenant_id FROM ""Tenants"";
                    IF fallback_tenant_id IS NULL THEN
                        RAISE EXCEPTION 'No Tenants exist — cannot backfill TenantId. Run Tenant_Foundation first.';
                    END IF;

                    FOR r IN
                        SELECT table_name
                        FROM information_schema.columns
                        WHERE table_schema = 'public' AND column_name = 'TenantId'
                          AND table_name NOT IN ('Tenants','Plans','TenantSubscriptions','TenantPayments','Facilities','AspNetUsers')
                    LOOP
                        EXECUTE format('UPDATE %I SET ""TenantId"" = %L WHERE ""TenantId"" = 0', r.table_name, fallback_tenant_id);
                    END LOOP;
                END $$;
            ");

            migrationBuilder.CreateIndex(
                name: "IX_WebPushSubscriptions_TenantId",
                table: "WebPushSubscriptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Wards_TenantId",
                table: "Wards",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_WardRoundEntries_TenantId",
                table: "WardRoundEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Vitals_TenantId",
                table: "Vitals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TriageAssessments_TenantId",
                table: "TriageAssessments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TicketCounters_TenantId",
                table: "TicketCounters",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TheatreSessions_TenantId",
                table: "TheatreSessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TheatreSessionEvents_TenantId",
                table: "TheatreSessionEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Theatres_TenantId",
                table: "Theatres",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TheatreChecklistItems_TenantId",
                table: "TheatreChecklistItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TeleSessions_TenantId",
                table: "TeleSessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TeleSessionParticipants_TenantId",
                table: "TeleSessionParticipants",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TeleChatMessages_TenantId",
                table: "TeleChatMessages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_TeleChatAttachments_TenantId",
                table: "TeleChatAttachments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Suppliers_TenantId",
                table: "Suppliers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakes_TenantId",
                table: "StockTakes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StockTakeItems_TenantId",
                table: "StockTakeItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_TenantId",
                table: "StockMovements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SoapNotes_TenantId",
                table: "SoapNotes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_SmsMessages_TenantId",
                table: "SmsMessages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_RosterShifts_TenantId",
                table: "RosterShifts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Rooms_TenantId",
                table: "Rooms",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResusEvents_TenantId",
                table: "ResusEvents",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ResusBays_TenantId",
                table: "ResusBays",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ReminderJobs_TenantId",
                table: "ReminderJobs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_TenantId",
                table: "Referrals",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_QueueEntries_TenantId",
                table: "QueueEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrders_TenantId",
                table: "PurchaseOrders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PurchaseOrderItems_TenantId",
                table: "PurchaseOrderItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureOrders_TenantId",
                table: "ProcedureOrders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Problems_TenantId",
                table: "Problems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_TenantId",
                table: "Prescriptions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItems_TenantId",
                table: "PrescriptionItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PostnatalVisits_TenantId",
                table: "PostnatalVisits",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalSymptomIntakes_TenantId",
                table: "PortalSymptomIntakes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PortalAccounts_TenantId",
                table: "PortalAccounts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyStores_TenantId",
                table: "PharmacyStores",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PaymentTransactions_TenantId",
                table: "PaymentTransactions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_TenantId",
                table: "Payments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_TenantId",
                table: "Patients",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientPayers_TenantId",
                table: "PatientPayers",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientNextOfKin_TenantId",
                table: "PatientNextOfKin",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientDocuments_TenantId",
                table: "PatientDocuments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NursingNotes_TenantId",
                table: "NursingNotes",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_NhmisReports_TenantId",
                table: "NhmisReports",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Newborns_TenantId",
                table: "Newborns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MpiPotentialMatches_TenantId",
                table: "MpiPotentialMatches",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MpiMergeAudits_TenantId",
                table: "MpiMergeAudits",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MortuaryEntries_TenantId",
                table: "MortuaryEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Medications_TenantId",
                table: "Medications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalCertificates_TenantId",
                table: "MedicalCertificates",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_MarSlots_TenantId",
                table: "MarSlots",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_TenantId",
                table: "LeaveRequests",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LabResultValues_TenantId",
                table: "LabResultValues",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_TenantId",
                table: "LabResults",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_TenantId",
                table: "LabOrders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryStocks_TenantId",
                table: "InventoryStocks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryStockMovements_TenantId",
                table: "InventoryStockMovements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InventoryItems_TenantId",
                table: "InventoryItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_InpatientMedications_TenantId",
                table: "InpatientMedications",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ImmunizationDoses_TenantId",
                table: "ImmunizationDoses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ImagingReports_TenantId",
                table: "ImagingReports",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ImagingOrders_TenantId",
                table: "ImagingOrders",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_IdsrCases_TenantId",
                table: "IdsrCases",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_IcuChartEntries_TenantId",
                table: "IcuChartEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_HrProfiles_TenantId",
                table: "HrProfiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_HospitalNumberCounters_TenantId",
                table: "HospitalNumberCounters",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GrowthMeasurements_TenantId",
                table: "GrowthMeasurements",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Grns_TenantId",
                table: "Grns",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_GrnItems_TenantId",
                table: "GrnItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_FluidEntries_TenantId",
                table: "FluidEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_TenantId",
                table: "Encounters",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EncounterDiagnoses_TenantId",
                table: "EncounterDiagnoses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_EmailMessages_TenantId",
                table: "EmailMessages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DrugStocks_TenantId",
                table: "DrugStocks",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DotPhrases_TenantId",
                table: "DotPhrases",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Dispenses_TenantId",
                table: "Dispenses",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DispenseItems_TenantId",
                table: "DispenseItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_DialysisSessions_TenantId",
                table: "DialysisSessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_TenantId",
                table: "Deliveries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Clinics_TenantId",
                table: "Clinics",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicianTimeOffs_TenantId",
                table: "ClinicianTimeOffs",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ClinicianAvailabilities_TenantId",
                table: "ClinicianAvailabilities",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_TenantId",
                table: "Claims",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimItems_TenantId",
                table: "ClaimItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildProfiles_TenantId",
                table: "ChildProfiles",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatPackages_TenantId",
                table: "ChatPackages",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_TenantId",
                table: "CashierShifts",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodUnits_TenantId",
                table: "BloodUnits",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodDonors_TenantId",
                table: "BloodDonors",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodCrossMatches_TenantId",
                table: "BloodCrossMatches",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_TenantId",
                table: "Bills",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BillItems_TenantId",
                table: "BillItems",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Beds_TenantId",
                table: "Beds",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_BedAllocations_TenantId",
                table: "BedAllocations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Authorizations_TenantId",
                table: "Authorizations",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AuditEntries_TenantId",
                table: "AuditEntries",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Appointments_TenantId",
                table: "Appointments",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AnteNatalVisits_TenantId",
                table: "AnteNatalVisits",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AnteNatalRecords_TenantId",
                table: "AnteNatalRecords",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AlliedSessions_TenantId",
                table: "AlliedSessions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Allergies_TenantId",
                table: "Allergies",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_AiSuggestions_TenantId",
                table: "AiSuggestions",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Admissions_TenantId",
                table: "Admissions",
                column: "TenantId");

            migrationBuilder.AddForeignKey(
                name: "FK_Admissions_Tenants_TenantId",
                table: "Admissions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AiSuggestions_Tenants_TenantId",
                table: "AiSuggestions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Allergies_Tenants_TenantId",
                table: "Allergies",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AlliedSessions_Tenants_TenantId",
                table: "AlliedSessions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnteNatalRecords_Tenants_TenantId",
                table: "AnteNatalRecords",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AnteNatalVisits_Tenants_TenantId",
                table: "AnteNatalVisits",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Appointments_Tenants_TenantId",
                table: "Appointments",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_AuditEntries_Tenants_TenantId",
                table: "AuditEntries",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Authorizations_Tenants_TenantId",
                table: "Authorizations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BedAllocations_Tenants_TenantId",
                table: "BedAllocations",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Beds_Tenants_TenantId",
                table: "Beds",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BillItems_Tenants_TenantId",
                table: "BillItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Bills_Tenants_TenantId",
                table: "Bills",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BloodCrossMatches_Tenants_TenantId",
                table: "BloodCrossMatches",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BloodDonors_Tenants_TenantId",
                table: "BloodDonors",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_BloodUnits_Tenants_TenantId",
                table: "BloodUnits",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_CashierShifts_Tenants_TenantId",
                table: "CashierShifts",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChatPackages_Tenants_TenantId",
                table: "ChatPackages",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ChildProfiles_Tenants_TenantId",
                table: "ChildProfiles",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClaimItems_Tenants_TenantId",
                table: "ClaimItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Claims_Tenants_TenantId",
                table: "Claims",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicianAvailabilities_Tenants_TenantId",
                table: "ClinicianAvailabilities",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ClinicianTimeOffs_Tenants_TenantId",
                table: "ClinicianTimeOffs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Clinics_Tenants_TenantId",
                table: "Clinics",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Deliveries_Tenants_TenantId",
                table: "Deliveries",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DialysisSessions_Tenants_TenantId",
                table: "DialysisSessions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DispenseItems_Tenants_TenantId",
                table: "DispenseItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Dispenses_Tenants_TenantId",
                table: "Dispenses",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DotPhrases_Tenants_TenantId",
                table: "DotPhrases",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_DrugStocks_Tenants_TenantId",
                table: "DrugStocks",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EmailMessages_Tenants_TenantId",
                table: "EmailMessages",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_EncounterDiagnoses_Tenants_TenantId",
                table: "EncounterDiagnoses",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Encounters_Tenants_TenantId",
                table: "Encounters",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_FluidEntries_Tenants_TenantId",
                table: "FluidEntries",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GrnItems_Tenants_TenantId",
                table: "GrnItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Grns_Tenants_TenantId",
                table: "Grns",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_GrowthMeasurements_Tenants_TenantId",
                table: "GrowthMeasurements",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HospitalNumberCounters_Tenants_TenantId",
                table: "HospitalNumberCounters",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_HrProfiles_Tenants_TenantId",
                table: "HrProfiles",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IcuChartEntries_Tenants_TenantId",
                table: "IcuChartEntries",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_IdsrCases_Tenants_TenantId",
                table: "IdsrCases",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ImagingOrders_Tenants_TenantId",
                table: "ImagingOrders",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ImagingReports_Tenants_TenantId",
                table: "ImagingReports",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ImmunizationDoses_Tenants_TenantId",
                table: "ImmunizationDoses",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InpatientMedications_Tenants_TenantId",
                table: "InpatientMedications",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryItems_Tenants_TenantId",
                table: "InventoryItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryStockMovements_Tenants_TenantId",
                table: "InventoryStockMovements",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_InventoryStocks_Tenants_TenantId",
                table: "InventoryStocks",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LabOrders_Tenants_TenantId",
                table: "LabOrders",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LabResults_Tenants_TenantId",
                table: "LabResults",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LabResultValues_Tenants_TenantId",
                table: "LabResultValues",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_LeaveRequests_Tenants_TenantId",
                table: "LeaveRequests",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MarSlots_Tenants_TenantId",
                table: "MarSlots",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MedicalCertificates_Tenants_TenantId",
                table: "MedicalCertificates",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Medications_Tenants_TenantId",
                table: "Medications",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MortuaryEntries_Tenants_TenantId",
                table: "MortuaryEntries",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MpiMergeAudits_Tenants_TenantId",
                table: "MpiMergeAudits",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_MpiPotentialMatches_Tenants_TenantId",
                table: "MpiPotentialMatches",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Newborns_Tenants_TenantId",
                table: "Newborns",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NhmisReports_Tenants_TenantId",
                table: "NhmisReports",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_NursingNotes_Tenants_TenantId",
                table: "NursingNotes",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientDocuments_Tenants_TenantId",
                table: "PatientDocuments",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientNextOfKin_Tenants_TenantId",
                table: "PatientNextOfKin",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientPayers_Tenants_TenantId",
                table: "PatientPayers",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Patients_Tenants_TenantId",
                table: "Patients",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Payments_Tenants_TenantId",
                table: "Payments",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PaymentTransactions_Tenants_TenantId",
                table: "PaymentTransactions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PharmacyStores_Tenants_TenantId",
                table: "PharmacyStores",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PortalAccounts_Tenants_TenantId",
                table: "PortalAccounts",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PortalSymptomIntakes_Tenants_TenantId",
                table: "PortalSymptomIntakes",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PostnatalVisits_Tenants_TenantId",
                table: "PostnatalVisits",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PrescriptionItems_Tenants_TenantId",
                table: "PrescriptionItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Prescriptions_Tenants_TenantId",
                table: "Prescriptions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Problems_Tenants_TenantId",
                table: "Problems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ProcedureOrders_Tenants_TenantId",
                table: "ProcedureOrders",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrderItems_Tenants_TenantId",
                table: "PurchaseOrderItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_PurchaseOrders_Tenants_TenantId",
                table: "PurchaseOrders",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_QueueEntries_Tenants_TenantId",
                table: "QueueEntries",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Referrals_Tenants_TenantId",
                table: "Referrals",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ReminderJobs_Tenants_TenantId",
                table: "ReminderJobs",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ResusBays_Tenants_TenantId",
                table: "ResusBays",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_ResusEvents_Tenants_TenantId",
                table: "ResusEvents",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Rooms_Tenants_TenantId",
                table: "Rooms",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_RosterShifts_Tenants_TenantId",
                table: "RosterShifts",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SmsMessages_Tenants_TenantId",
                table: "SmsMessages",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_SoapNotes_Tenants_TenantId",
                table: "SoapNotes",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockMovements_Tenants_TenantId",
                table: "StockMovements",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockTakeItems_Tenants_TenantId",
                table: "StockTakeItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_StockTakes_Tenants_TenantId",
                table: "StockTakes",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Suppliers_Tenants_TenantId",
                table: "Suppliers",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeleChatAttachments_Tenants_TenantId",
                table: "TeleChatAttachments",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeleChatMessages_Tenants_TenantId",
                table: "TeleChatMessages",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeleSessionParticipants_Tenants_TenantId",
                table: "TeleSessionParticipants",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TeleSessions_Tenants_TenantId",
                table: "TeleSessions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TheatreChecklistItems_Tenants_TenantId",
                table: "TheatreChecklistItems",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Theatres_Tenants_TenantId",
                table: "Theatres",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TheatreSessionEvents_Tenants_TenantId",
                table: "TheatreSessionEvents",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TheatreSessions_Tenants_TenantId",
                table: "TheatreSessions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TicketCounters_Tenants_TenantId",
                table: "TicketCounters",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_TriageAssessments_Tenants_TenantId",
                table: "TriageAssessments",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Vitals_Tenants_TenantId",
                table: "Vitals",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WardRoundEntries_Tenants_TenantId",
                table: "WardRoundEntries",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_Wards_Tenants_TenantId",
                table: "Wards",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);

            migrationBuilder.AddForeignKey(
                name: "FK_WebPushSubscriptions_Tenants_TenantId",
                table: "WebPushSubscriptions",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Admissions_Tenants_TenantId",
                table: "Admissions");

            migrationBuilder.DropForeignKey(
                name: "FK_AiSuggestions_Tenants_TenantId",
                table: "AiSuggestions");

            migrationBuilder.DropForeignKey(
                name: "FK_Allergies_Tenants_TenantId",
                table: "Allergies");

            migrationBuilder.DropForeignKey(
                name: "FK_AlliedSessions_Tenants_TenantId",
                table: "AlliedSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_AnteNatalRecords_Tenants_TenantId",
                table: "AnteNatalRecords");

            migrationBuilder.DropForeignKey(
                name: "FK_AnteNatalVisits_Tenants_TenantId",
                table: "AnteNatalVisits");

            migrationBuilder.DropForeignKey(
                name: "FK_Appointments_Tenants_TenantId",
                table: "Appointments");

            migrationBuilder.DropForeignKey(
                name: "FK_AuditEntries_Tenants_TenantId",
                table: "AuditEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_Authorizations_Tenants_TenantId",
                table: "Authorizations");

            migrationBuilder.DropForeignKey(
                name: "FK_BedAllocations_Tenants_TenantId",
                table: "BedAllocations");

            migrationBuilder.DropForeignKey(
                name: "FK_Beds_Tenants_TenantId",
                table: "Beds");

            migrationBuilder.DropForeignKey(
                name: "FK_BillItems_Tenants_TenantId",
                table: "BillItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Bills_Tenants_TenantId",
                table: "Bills");

            migrationBuilder.DropForeignKey(
                name: "FK_BloodCrossMatches_Tenants_TenantId",
                table: "BloodCrossMatches");

            migrationBuilder.DropForeignKey(
                name: "FK_BloodDonors_Tenants_TenantId",
                table: "BloodDonors");

            migrationBuilder.DropForeignKey(
                name: "FK_BloodUnits_Tenants_TenantId",
                table: "BloodUnits");

            migrationBuilder.DropForeignKey(
                name: "FK_CashierShifts_Tenants_TenantId",
                table: "CashierShifts");

            migrationBuilder.DropForeignKey(
                name: "FK_ChatPackages_Tenants_TenantId",
                table: "ChatPackages");

            migrationBuilder.DropForeignKey(
                name: "FK_ChildProfiles_Tenants_TenantId",
                table: "ChildProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_ClaimItems_Tenants_TenantId",
                table: "ClaimItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Claims_Tenants_TenantId",
                table: "Claims");

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicianAvailabilities_Tenants_TenantId",
                table: "ClinicianAvailabilities");

            migrationBuilder.DropForeignKey(
                name: "FK_ClinicianTimeOffs_Tenants_TenantId",
                table: "ClinicianTimeOffs");

            migrationBuilder.DropForeignKey(
                name: "FK_Clinics_Tenants_TenantId",
                table: "Clinics");

            migrationBuilder.DropForeignKey(
                name: "FK_Deliveries_Tenants_TenantId",
                table: "Deliveries");

            migrationBuilder.DropForeignKey(
                name: "FK_DialysisSessions_Tenants_TenantId",
                table: "DialysisSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_DispenseItems_Tenants_TenantId",
                table: "DispenseItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Dispenses_Tenants_TenantId",
                table: "Dispenses");

            migrationBuilder.DropForeignKey(
                name: "FK_DotPhrases_Tenants_TenantId",
                table: "DotPhrases");

            migrationBuilder.DropForeignKey(
                name: "FK_DrugStocks_Tenants_TenantId",
                table: "DrugStocks");

            migrationBuilder.DropForeignKey(
                name: "FK_EmailMessages_Tenants_TenantId",
                table: "EmailMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_EncounterDiagnoses_Tenants_TenantId",
                table: "EncounterDiagnoses");

            migrationBuilder.DropForeignKey(
                name: "FK_Encounters_Tenants_TenantId",
                table: "Encounters");

            migrationBuilder.DropForeignKey(
                name: "FK_FluidEntries_Tenants_TenantId",
                table: "FluidEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_GrnItems_Tenants_TenantId",
                table: "GrnItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Grns_Tenants_TenantId",
                table: "Grns");

            migrationBuilder.DropForeignKey(
                name: "FK_GrowthMeasurements_Tenants_TenantId",
                table: "GrowthMeasurements");

            migrationBuilder.DropForeignKey(
                name: "FK_HospitalNumberCounters_Tenants_TenantId",
                table: "HospitalNumberCounters");

            migrationBuilder.DropForeignKey(
                name: "FK_HrProfiles_Tenants_TenantId",
                table: "HrProfiles");

            migrationBuilder.DropForeignKey(
                name: "FK_IcuChartEntries_Tenants_TenantId",
                table: "IcuChartEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_IdsrCases_Tenants_TenantId",
                table: "IdsrCases");

            migrationBuilder.DropForeignKey(
                name: "FK_ImagingOrders_Tenants_TenantId",
                table: "ImagingOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_ImagingReports_Tenants_TenantId",
                table: "ImagingReports");

            migrationBuilder.DropForeignKey(
                name: "FK_ImmunizationDoses_Tenants_TenantId",
                table: "ImmunizationDoses");

            migrationBuilder.DropForeignKey(
                name: "FK_InpatientMedications_Tenants_TenantId",
                table: "InpatientMedications");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryItems_Tenants_TenantId",
                table: "InventoryItems");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryStockMovements_Tenants_TenantId",
                table: "InventoryStockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_InventoryStocks_Tenants_TenantId",
                table: "InventoryStocks");

            migrationBuilder.DropForeignKey(
                name: "FK_LabOrders_Tenants_TenantId",
                table: "LabOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_LabResults_Tenants_TenantId",
                table: "LabResults");

            migrationBuilder.DropForeignKey(
                name: "FK_LabResultValues_Tenants_TenantId",
                table: "LabResultValues");

            migrationBuilder.DropForeignKey(
                name: "FK_LeaveRequests_Tenants_TenantId",
                table: "LeaveRequests");

            migrationBuilder.DropForeignKey(
                name: "FK_MarSlots_Tenants_TenantId",
                table: "MarSlots");

            migrationBuilder.DropForeignKey(
                name: "FK_MedicalCertificates_Tenants_TenantId",
                table: "MedicalCertificates");

            migrationBuilder.DropForeignKey(
                name: "FK_Medications_Tenants_TenantId",
                table: "Medications");

            migrationBuilder.DropForeignKey(
                name: "FK_MortuaryEntries_Tenants_TenantId",
                table: "MortuaryEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_MpiMergeAudits_Tenants_TenantId",
                table: "MpiMergeAudits");

            migrationBuilder.DropForeignKey(
                name: "FK_MpiPotentialMatches_Tenants_TenantId",
                table: "MpiPotentialMatches");

            migrationBuilder.DropForeignKey(
                name: "FK_Newborns_Tenants_TenantId",
                table: "Newborns");

            migrationBuilder.DropForeignKey(
                name: "FK_NhmisReports_Tenants_TenantId",
                table: "NhmisReports");

            migrationBuilder.DropForeignKey(
                name: "FK_NursingNotes_Tenants_TenantId",
                table: "NursingNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientDocuments_Tenants_TenantId",
                table: "PatientDocuments");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientNextOfKin_Tenants_TenantId",
                table: "PatientNextOfKin");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientPayers_Tenants_TenantId",
                table: "PatientPayers");

            migrationBuilder.DropForeignKey(
                name: "FK_Patients_Tenants_TenantId",
                table: "Patients");

            migrationBuilder.DropForeignKey(
                name: "FK_Payments_Tenants_TenantId",
                table: "Payments");

            migrationBuilder.DropForeignKey(
                name: "FK_PaymentTransactions_Tenants_TenantId",
                table: "PaymentTransactions");

            migrationBuilder.DropForeignKey(
                name: "FK_PharmacyStores_Tenants_TenantId",
                table: "PharmacyStores");

            migrationBuilder.DropForeignKey(
                name: "FK_PortalAccounts_Tenants_TenantId",
                table: "PortalAccounts");

            migrationBuilder.DropForeignKey(
                name: "FK_PortalSymptomIntakes_Tenants_TenantId",
                table: "PortalSymptomIntakes");

            migrationBuilder.DropForeignKey(
                name: "FK_PostnatalVisits_Tenants_TenantId",
                table: "PostnatalVisits");

            migrationBuilder.DropForeignKey(
                name: "FK_PrescriptionItems_Tenants_TenantId",
                table: "PrescriptionItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Prescriptions_Tenants_TenantId",
                table: "Prescriptions");

            migrationBuilder.DropForeignKey(
                name: "FK_Problems_Tenants_TenantId",
                table: "Problems");

            migrationBuilder.DropForeignKey(
                name: "FK_ProcedureOrders_Tenants_TenantId",
                table: "ProcedureOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrderItems_Tenants_TenantId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropForeignKey(
                name: "FK_PurchaseOrders_Tenants_TenantId",
                table: "PurchaseOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_QueueEntries_Tenants_TenantId",
                table: "QueueEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_Referrals_Tenants_TenantId",
                table: "Referrals");

            migrationBuilder.DropForeignKey(
                name: "FK_ReminderJobs_Tenants_TenantId",
                table: "ReminderJobs");

            migrationBuilder.DropForeignKey(
                name: "FK_ResusBays_Tenants_TenantId",
                table: "ResusBays");

            migrationBuilder.DropForeignKey(
                name: "FK_ResusEvents_Tenants_TenantId",
                table: "ResusEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_Rooms_Tenants_TenantId",
                table: "Rooms");

            migrationBuilder.DropForeignKey(
                name: "FK_RosterShifts_Tenants_TenantId",
                table: "RosterShifts");

            migrationBuilder.DropForeignKey(
                name: "FK_SmsMessages_Tenants_TenantId",
                table: "SmsMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_SoapNotes_Tenants_TenantId",
                table: "SoapNotes");

            migrationBuilder.DropForeignKey(
                name: "FK_StockMovements_Tenants_TenantId",
                table: "StockMovements");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTakeItems_Tenants_TenantId",
                table: "StockTakeItems");

            migrationBuilder.DropForeignKey(
                name: "FK_StockTakes_Tenants_TenantId",
                table: "StockTakes");

            migrationBuilder.DropForeignKey(
                name: "FK_Suppliers_Tenants_TenantId",
                table: "Suppliers");

            migrationBuilder.DropForeignKey(
                name: "FK_TeleChatAttachments_Tenants_TenantId",
                table: "TeleChatAttachments");

            migrationBuilder.DropForeignKey(
                name: "FK_TeleChatMessages_Tenants_TenantId",
                table: "TeleChatMessages");

            migrationBuilder.DropForeignKey(
                name: "FK_TeleSessionParticipants_Tenants_TenantId",
                table: "TeleSessionParticipants");

            migrationBuilder.DropForeignKey(
                name: "FK_TeleSessions_Tenants_TenantId",
                table: "TeleSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_TheatreChecklistItems_Tenants_TenantId",
                table: "TheatreChecklistItems");

            migrationBuilder.DropForeignKey(
                name: "FK_Theatres_Tenants_TenantId",
                table: "Theatres");

            migrationBuilder.DropForeignKey(
                name: "FK_TheatreSessionEvents_Tenants_TenantId",
                table: "TheatreSessionEvents");

            migrationBuilder.DropForeignKey(
                name: "FK_TheatreSessions_Tenants_TenantId",
                table: "TheatreSessions");

            migrationBuilder.DropForeignKey(
                name: "FK_TicketCounters_Tenants_TenantId",
                table: "TicketCounters");

            migrationBuilder.DropForeignKey(
                name: "FK_TriageAssessments_Tenants_TenantId",
                table: "TriageAssessments");

            migrationBuilder.DropForeignKey(
                name: "FK_Vitals_Tenants_TenantId",
                table: "Vitals");

            migrationBuilder.DropForeignKey(
                name: "FK_WardRoundEntries_Tenants_TenantId",
                table: "WardRoundEntries");

            migrationBuilder.DropForeignKey(
                name: "FK_Wards_Tenants_TenantId",
                table: "Wards");

            migrationBuilder.DropForeignKey(
                name: "FK_WebPushSubscriptions_Tenants_TenantId",
                table: "WebPushSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_WebPushSubscriptions_TenantId",
                table: "WebPushSubscriptions");

            migrationBuilder.DropIndex(
                name: "IX_Wards_TenantId",
                table: "Wards");

            migrationBuilder.DropIndex(
                name: "IX_WardRoundEntries_TenantId",
                table: "WardRoundEntries");

            migrationBuilder.DropIndex(
                name: "IX_Vitals_TenantId",
                table: "Vitals");

            migrationBuilder.DropIndex(
                name: "IX_TriageAssessments_TenantId",
                table: "TriageAssessments");

            migrationBuilder.DropIndex(
                name: "IX_TicketCounters_TenantId",
                table: "TicketCounters");

            migrationBuilder.DropIndex(
                name: "IX_TheatreSessions_TenantId",
                table: "TheatreSessions");

            migrationBuilder.DropIndex(
                name: "IX_TheatreSessionEvents_TenantId",
                table: "TheatreSessionEvents");

            migrationBuilder.DropIndex(
                name: "IX_Theatres_TenantId",
                table: "Theatres");

            migrationBuilder.DropIndex(
                name: "IX_TheatreChecklistItems_TenantId",
                table: "TheatreChecklistItems");

            migrationBuilder.DropIndex(
                name: "IX_TeleSessions_TenantId",
                table: "TeleSessions");

            migrationBuilder.DropIndex(
                name: "IX_TeleSessionParticipants_TenantId",
                table: "TeleSessionParticipants");

            migrationBuilder.DropIndex(
                name: "IX_TeleChatMessages_TenantId",
                table: "TeleChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_TeleChatAttachments_TenantId",
                table: "TeleChatAttachments");

            migrationBuilder.DropIndex(
                name: "IX_Suppliers_TenantId",
                table: "Suppliers");

            migrationBuilder.DropIndex(
                name: "IX_StockTakes_TenantId",
                table: "StockTakes");

            migrationBuilder.DropIndex(
                name: "IX_StockTakeItems_TenantId",
                table: "StockTakeItems");

            migrationBuilder.DropIndex(
                name: "IX_StockMovements_TenantId",
                table: "StockMovements");

            migrationBuilder.DropIndex(
                name: "IX_SoapNotes_TenantId",
                table: "SoapNotes");

            migrationBuilder.DropIndex(
                name: "IX_SmsMessages_TenantId",
                table: "SmsMessages");

            migrationBuilder.DropIndex(
                name: "IX_RosterShifts_TenantId",
                table: "RosterShifts");

            migrationBuilder.DropIndex(
                name: "IX_Rooms_TenantId",
                table: "Rooms");

            migrationBuilder.DropIndex(
                name: "IX_ResusEvents_TenantId",
                table: "ResusEvents");

            migrationBuilder.DropIndex(
                name: "IX_ResusBays_TenantId",
                table: "ResusBays");

            migrationBuilder.DropIndex(
                name: "IX_ReminderJobs_TenantId",
                table: "ReminderJobs");

            migrationBuilder.DropIndex(
                name: "IX_Referrals_TenantId",
                table: "Referrals");

            migrationBuilder.DropIndex(
                name: "IX_QueueEntries_TenantId",
                table: "QueueEntries");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrders_TenantId",
                table: "PurchaseOrders");

            migrationBuilder.DropIndex(
                name: "IX_PurchaseOrderItems_TenantId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropIndex(
                name: "IX_ProcedureOrders_TenantId",
                table: "ProcedureOrders");

            migrationBuilder.DropIndex(
                name: "IX_Problems_TenantId",
                table: "Problems");

            migrationBuilder.DropIndex(
                name: "IX_Prescriptions_TenantId",
                table: "Prescriptions");

            migrationBuilder.DropIndex(
                name: "IX_PrescriptionItems_TenantId",
                table: "PrescriptionItems");

            migrationBuilder.DropIndex(
                name: "IX_PostnatalVisits_TenantId",
                table: "PostnatalVisits");

            migrationBuilder.DropIndex(
                name: "IX_PortalSymptomIntakes_TenantId",
                table: "PortalSymptomIntakes");

            migrationBuilder.DropIndex(
                name: "IX_PortalAccounts_TenantId",
                table: "PortalAccounts");

            migrationBuilder.DropIndex(
                name: "IX_PharmacyStores_TenantId",
                table: "PharmacyStores");

            migrationBuilder.DropIndex(
                name: "IX_PaymentTransactions_TenantId",
                table: "PaymentTransactions");

            migrationBuilder.DropIndex(
                name: "IX_Payments_TenantId",
                table: "Payments");

            migrationBuilder.DropIndex(
                name: "IX_Patients_TenantId",
                table: "Patients");

            migrationBuilder.DropIndex(
                name: "IX_PatientPayers_TenantId",
                table: "PatientPayers");

            migrationBuilder.DropIndex(
                name: "IX_PatientNextOfKin_TenantId",
                table: "PatientNextOfKin");

            migrationBuilder.DropIndex(
                name: "IX_PatientDocuments_TenantId",
                table: "PatientDocuments");

            migrationBuilder.DropIndex(
                name: "IX_NursingNotes_TenantId",
                table: "NursingNotes");

            migrationBuilder.DropIndex(
                name: "IX_NhmisReports_TenantId",
                table: "NhmisReports");

            migrationBuilder.DropIndex(
                name: "IX_Newborns_TenantId",
                table: "Newborns");

            migrationBuilder.DropIndex(
                name: "IX_MpiPotentialMatches_TenantId",
                table: "MpiPotentialMatches");

            migrationBuilder.DropIndex(
                name: "IX_MpiMergeAudits_TenantId",
                table: "MpiMergeAudits");

            migrationBuilder.DropIndex(
                name: "IX_MortuaryEntries_TenantId",
                table: "MortuaryEntries");

            migrationBuilder.DropIndex(
                name: "IX_Medications_TenantId",
                table: "Medications");

            migrationBuilder.DropIndex(
                name: "IX_MedicalCertificates_TenantId",
                table: "MedicalCertificates");

            migrationBuilder.DropIndex(
                name: "IX_MarSlots_TenantId",
                table: "MarSlots");

            migrationBuilder.DropIndex(
                name: "IX_LeaveRequests_TenantId",
                table: "LeaveRequests");

            migrationBuilder.DropIndex(
                name: "IX_LabResultValues_TenantId",
                table: "LabResultValues");

            migrationBuilder.DropIndex(
                name: "IX_LabResults_TenantId",
                table: "LabResults");

            migrationBuilder.DropIndex(
                name: "IX_LabOrders_TenantId",
                table: "LabOrders");

            migrationBuilder.DropIndex(
                name: "IX_InventoryStocks_TenantId",
                table: "InventoryStocks");

            migrationBuilder.DropIndex(
                name: "IX_InventoryStockMovements_TenantId",
                table: "InventoryStockMovements");

            migrationBuilder.DropIndex(
                name: "IX_InventoryItems_TenantId",
                table: "InventoryItems");

            migrationBuilder.DropIndex(
                name: "IX_InpatientMedications_TenantId",
                table: "InpatientMedications");

            migrationBuilder.DropIndex(
                name: "IX_ImmunizationDoses_TenantId",
                table: "ImmunizationDoses");

            migrationBuilder.DropIndex(
                name: "IX_ImagingReports_TenantId",
                table: "ImagingReports");

            migrationBuilder.DropIndex(
                name: "IX_ImagingOrders_TenantId",
                table: "ImagingOrders");

            migrationBuilder.DropIndex(
                name: "IX_IdsrCases_TenantId",
                table: "IdsrCases");

            migrationBuilder.DropIndex(
                name: "IX_IcuChartEntries_TenantId",
                table: "IcuChartEntries");

            migrationBuilder.DropIndex(
                name: "IX_HrProfiles_TenantId",
                table: "HrProfiles");

            migrationBuilder.DropIndex(
                name: "IX_HospitalNumberCounters_TenantId",
                table: "HospitalNumberCounters");

            migrationBuilder.DropIndex(
                name: "IX_GrowthMeasurements_TenantId",
                table: "GrowthMeasurements");

            migrationBuilder.DropIndex(
                name: "IX_Grns_TenantId",
                table: "Grns");

            migrationBuilder.DropIndex(
                name: "IX_GrnItems_TenantId",
                table: "GrnItems");

            migrationBuilder.DropIndex(
                name: "IX_FluidEntries_TenantId",
                table: "FluidEntries");

            migrationBuilder.DropIndex(
                name: "IX_Encounters_TenantId",
                table: "Encounters");

            migrationBuilder.DropIndex(
                name: "IX_EncounterDiagnoses_TenantId",
                table: "EncounterDiagnoses");

            migrationBuilder.DropIndex(
                name: "IX_EmailMessages_TenantId",
                table: "EmailMessages");

            migrationBuilder.DropIndex(
                name: "IX_DrugStocks_TenantId",
                table: "DrugStocks");

            migrationBuilder.DropIndex(
                name: "IX_DotPhrases_TenantId",
                table: "DotPhrases");

            migrationBuilder.DropIndex(
                name: "IX_Dispenses_TenantId",
                table: "Dispenses");

            migrationBuilder.DropIndex(
                name: "IX_DispenseItems_TenantId",
                table: "DispenseItems");

            migrationBuilder.DropIndex(
                name: "IX_DialysisSessions_TenantId",
                table: "DialysisSessions");

            migrationBuilder.DropIndex(
                name: "IX_Deliveries_TenantId",
                table: "Deliveries");

            migrationBuilder.DropIndex(
                name: "IX_Clinics_TenantId",
                table: "Clinics");

            migrationBuilder.DropIndex(
                name: "IX_ClinicianTimeOffs_TenantId",
                table: "ClinicianTimeOffs");

            migrationBuilder.DropIndex(
                name: "IX_ClinicianAvailabilities_TenantId",
                table: "ClinicianAvailabilities");

            migrationBuilder.DropIndex(
                name: "IX_Claims_TenantId",
                table: "Claims");

            migrationBuilder.DropIndex(
                name: "IX_ClaimItems_TenantId",
                table: "ClaimItems");

            migrationBuilder.DropIndex(
                name: "IX_ChildProfiles_TenantId",
                table: "ChildProfiles");

            migrationBuilder.DropIndex(
                name: "IX_ChatPackages_TenantId",
                table: "ChatPackages");

            migrationBuilder.DropIndex(
                name: "IX_CashierShifts_TenantId",
                table: "CashierShifts");

            migrationBuilder.DropIndex(
                name: "IX_BloodUnits_TenantId",
                table: "BloodUnits");

            migrationBuilder.DropIndex(
                name: "IX_BloodDonors_TenantId",
                table: "BloodDonors");

            migrationBuilder.DropIndex(
                name: "IX_BloodCrossMatches_TenantId",
                table: "BloodCrossMatches");

            migrationBuilder.DropIndex(
                name: "IX_Bills_TenantId",
                table: "Bills");

            migrationBuilder.DropIndex(
                name: "IX_BillItems_TenantId",
                table: "BillItems");

            migrationBuilder.DropIndex(
                name: "IX_Beds_TenantId",
                table: "Beds");

            migrationBuilder.DropIndex(
                name: "IX_BedAllocations_TenantId",
                table: "BedAllocations");

            migrationBuilder.DropIndex(
                name: "IX_Authorizations_TenantId",
                table: "Authorizations");

            migrationBuilder.DropIndex(
                name: "IX_AuditEntries_TenantId",
                table: "AuditEntries");

            migrationBuilder.DropIndex(
                name: "IX_Appointments_TenantId",
                table: "Appointments");

            migrationBuilder.DropIndex(
                name: "IX_AnteNatalVisits_TenantId",
                table: "AnteNatalVisits");

            migrationBuilder.DropIndex(
                name: "IX_AnteNatalRecords_TenantId",
                table: "AnteNatalRecords");

            migrationBuilder.DropIndex(
                name: "IX_AlliedSessions_TenantId",
                table: "AlliedSessions");

            migrationBuilder.DropIndex(
                name: "IX_Allergies_TenantId",
                table: "Allergies");

            migrationBuilder.DropIndex(
                name: "IX_AiSuggestions_TenantId",
                table: "AiSuggestions");

            migrationBuilder.DropIndex(
                name: "IX_Admissions_TenantId",
                table: "Admissions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "WebPushSubscriptions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Wards");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "WardRoundEntries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Vitals");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TriageAssessments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TicketCounters");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TheatreSessions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TheatreSessionEvents");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Theatres");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TheatreChecklistItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TeleSessions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TeleSessionParticipants");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TeleChatMessages");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "TeleChatAttachments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Suppliers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "StockTakes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "StockTakeItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "StockMovements");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SoapNotes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "SmsMessages");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "RosterShifts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Rooms");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ResusEvents");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ResusBays");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ReminderJobs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Referrals");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "QueueEntries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PurchaseOrders");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PurchaseOrderItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ProcedureOrders");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Problems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Prescriptions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PrescriptionItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PostnatalVisits");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PortalSymptomIntakes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PortalAccounts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PharmacyStores");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PaymentTransactions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Payments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Patients");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PatientPayers");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PatientNextOfKin");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "PatientDocuments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "NursingNotes");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "NhmisReports");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Newborns");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MpiPotentialMatches");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MpiMergeAudits");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MortuaryEntries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Medications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MedicalCertificates");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "MarSlots");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "LeaveRequests");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "LabResultValues");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "LabResults");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "InventoryStocks");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "InventoryStockMovements");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "InventoryItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "InpatientMedications");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ImmunizationDoses");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ImagingReports");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ImagingOrders");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "IdsrCases");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "IcuChartEntries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "HrProfiles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "HospitalNumberCounters");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "GrowthMeasurements");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Grns");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "GrnItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "FluidEntries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "EncounterDiagnoses");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "EmailMessages");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DrugStocks");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DotPhrases");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Dispenses");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DispenseItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "DialysisSessions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Deliveries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Clinics");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ClinicianTimeOffs");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ClinicianAvailabilities");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Claims");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ClaimItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ChildProfiles");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "ChatPackages");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "CashierShifts");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BloodUnits");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BloodDonors");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BloodCrossMatches");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Bills");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BillItems");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Beds");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "BedAllocations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Authorizations");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AuditEntries");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Appointments");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AnteNatalVisits");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AnteNatalRecords");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AlliedSessions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Allergies");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AiSuggestions");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Admissions");
        }
    }
}
