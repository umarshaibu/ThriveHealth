using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Tenant_Foundation : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "Facilities",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.AddColumn<int>(
                name: "TenantId",
                table: "AspNetUsers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Plans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Tagline = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    MonthlyNgn = table.Column<decimal>(type: "numeric", nullable: false),
                    AnnualNgn = table.Column<decimal>(type: "numeric", nullable: false),
                    MaxStaff = table.Column<int>(type: "integer", nullable: true),
                    MaxPatientsPerMonth = table.Column<int>(type: "integer", nullable: true),
                    MaxFacilities = table.Column<int>(type: "integer", nullable: true),
                    MaxTeleConsultsPerMonth = table.Column<int>(type: "integer", nullable: true),
                    TelemedicineEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ChatPackagesEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AiEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    MultiFacilityEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    ClaimsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    AnalyticsEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    PrioritySupport = table.Column<bool>(type: "boolean", nullable: false),
                    SsoEnabled = table.Column<bool>(type: "boolean", nullable: false),
                    OnPremiseAvailable = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsCustomQuote = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Plans", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Tenants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Slug = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    LegalName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    BrandName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    LogoUrl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    PrimaryColor = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    CurrencyCode = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    CountryCode = table.Column<string>(type: "character varying(2)", maxLength: 2, nullable: false),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Lga = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OwnerEmail = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    OwnerName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    OwnerPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    EmailVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    TrialEndsAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SuspendedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsTeachingHospital = table.Column<bool>(type: "boolean", nullable: false),
                    TeachingVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Tenants", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "TenantSubscriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    PlanId = table.Column<int>(type: "integer", nullable: false),
                    Cycle = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentPeriodStart = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentPeriodEnd = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CancelledAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PriceAmount = table.Column<decimal>(type: "numeric", nullable: false),
                    PriceCurrency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    PaystackSubscriptionCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantSubscriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantSubscriptions_Plans_PlanId",
                        column: x => x.PlanId,
                        principalTable: "Plans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TenantSubscriptions_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TenantPayments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TenantId = table.Column<int>(type: "integer", nullable: false),
                    SubscriptionId = table.Column<int>(type: "integer", nullable: true),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric", nullable: false),
                    Currency = table.Column<string>(type: "character varying(3)", maxLength: 3, nullable: false),
                    Reference = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    BankAccountUsed = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    PaidAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReceiptUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceiptFileName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReviewedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReviewedById = table.Column<string>(type: "text", nullable: true),
                    ReviewNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TenantPayments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TenantPayments_AspNetUsers_ReviewedById",
                        column: x => x.ReviewedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TenantPayments_TenantSubscriptions_SubscriptionId",
                        column: x => x.SubscriptionId,
                        principalTable: "TenantSubscriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TenantPayments_Tenants_TenantId",
                        column: x => x.TenantId,
                        principalTable: "Tenants",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Facilities_TenantId",
                table: "Facilities",
                column: "TenantId");

            migrationBuilder.CreateIndex(
                name: "IX_Plans_Code",
                table: "Plans",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TenantPayments_ReviewedById",
                table: "TenantPayments",
                column: "ReviewedById");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPayments_SubscriptionId",
                table: "TenantPayments",
                column: "SubscriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantPayments_TenantId_Status",
                table: "TenantPayments",
                columns: new[] { "TenantId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_OwnerEmail",
                table: "Tenants",
                column: "OwnerEmail");

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Slug",
                table: "Tenants",
                column: "Slug",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Tenants_Status",
                table: "Tenants",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubscriptions_PlanId",
                table: "TenantSubscriptions",
                column: "PlanId");

            migrationBuilder.CreateIndex(
                name: "IX_TenantSubscriptions_TenantId_IsActive",
                table: "TenantSubscriptions",
                columns: new[] { "TenantId", "IsActive" });

            // Backfill — every existing facility / user is migrated under a single default tenant.
            // The default tenant inherits whatever is in the first existing facility row so the
            // demo deployment continues to work.
            migrationBuilder.Sql(@"
                INSERT INTO ""Tenants"" (
                    ""Slug"", ""LegalName"", ""BrandName"", ""LogoUrl"", ""PrimaryColor"",
                    ""CurrencyCode"", ""CountryCode"", ""State"", ""Lga"", ""Address"",
                    ""OwnerEmail"", ""OwnerName"", ""OwnerPhone"",
                    ""Status"", ""EmailVerifiedAt"", ""TrialEndsAt"", ""SuspendedAt"",
                    ""IsTeachingHospital"", ""TeachingVerifiedAt"",
                    ""CreatedAt"", ""UpdatedAt""
                )
                SELECT
                    'default',
                    COALESCE(MAX(""Name""), 'Default Hospital'),
                    NULL, NULL, NULL,
                    'NGN', 'NG', MAX(""State""), MAX(""Lga""), MAX(""Address""),
                    'admin@thrivehealth.ng', 'System Administrator', NULL,
                    3, now(), NULL, NULL,
                    false, NULL,
                    now(), now()
                FROM ""Facilities""
                WHERE NOT EXISTS (SELECT 1 FROM ""Tenants"" WHERE ""Slug"" = 'default');

                -- Every existing facility is reassigned to the default tenant
                UPDATE ""Facilities""
                SET ""TenantId"" = (SELECT ""Id"" FROM ""Tenants"" WHERE ""Slug"" = 'default' LIMIT 1)
                WHERE ""TenantId"" = 0 OR ""TenantId"" IS NULL;

                -- Every existing user gets the default tenant via their facility
                UPDATE ""AspNetUsers"" u
                SET ""TenantId"" = f.""TenantId""
                FROM ""Facilities"" f
                WHERE u.""FacilityId"" = f.""Id"" AND u.""TenantId"" IS NULL;
            ");

            migrationBuilder.AddForeignKey(
                name: "FK_Facilities_Tenants_TenantId",
                table: "Facilities",
                column: "TenantId",
                principalTable: "Tenants",
                principalColumn: "Id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Facilities_Tenants_TenantId",
                table: "Facilities");

            migrationBuilder.DropTable(
                name: "TenantPayments");

            migrationBuilder.DropTable(
                name: "TenantSubscriptions");

            migrationBuilder.DropTable(
                name: "Plans");

            migrationBuilder.DropTable(
                name: "Tenants");

            migrationBuilder.DropIndex(
                name: "IX_Facilities_TenantId",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "TenantId",
                table: "AspNetUsers");
        }
    }
}
