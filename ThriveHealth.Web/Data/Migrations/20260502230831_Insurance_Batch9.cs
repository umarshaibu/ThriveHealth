using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Insurance_Batch9 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "PayerId",
                table: "PatientPayers",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "PayerPlanId",
                table: "PatientPayers",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "Authorizations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientPayerId = table.Column<int>(type: "integer", nullable: false),
                    EncounterId = table.Column<int>(type: "integer", nullable: true),
                    AuthorizationCode = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    AuthorizedFor = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ValidFromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ValidToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ApprovedAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Authorizations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Authorizations_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Authorizations_PatientPayers_PatientPayerId",
                        column: x => x.PatientPayerId,
                        principalTable: "PatientPayers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OrgType = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Address = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    ClaimsDispatchEmail = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    RegulatorRegistrationNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payers", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PayerPlans",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayerId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    TariffMultiplier = table.Column<decimal>(type: "numeric(6,3)", precision: 6, scale: 3, nullable: false),
                    CapitationRatePerEnrolleeMonth = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    RequiresPreAuthorization = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultFormularyCovered = table.Column<bool>(type: "boolean", nullable: false),
                    DefaultCopayPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayerPlans", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayerPlans_Payers_PayerId",
                        column: x => x.PayerId,
                        principalTable: "Payers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Claims",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    PayerId = table.Column<int>(type: "integer", nullable: false),
                    PayerPlanId = table.Column<int>(type: "integer", nullable: true),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    EncounterId = table.Column<int>(type: "integer", nullable: true),
                    ClaimReference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    AuthorizationCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    PayerReference = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RespondedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GrossAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CopayAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ClaimableAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    DenialReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedById = table.Column<string>(type: "text", nullable: true),
                    SubmittedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Claims", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Claims_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Claims_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Claims_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Claims_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Claims_PayerPlans_PayerPlanId",
                        column: x => x.PayerPlanId,
                        principalTable: "PayerPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Claims_Payers_PayerId",
                        column: x => x.PayerId,
                        principalTable: "Payers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PayerFormularies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PayerPlanId = table.Column<int>(type: "integer", nullable: false),
                    DrugId = table.Column<int>(type: "integer", nullable: false),
                    IsCovered = table.Column<bool>(type: "boolean", nullable: false),
                    CopayPercent = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PayerFormularies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PayerFormularies_Drugs_DrugId",
                        column: x => x.DrugId,
                        principalTable: "Drugs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PayerFormularies_PayerPlans_PayerPlanId",
                        column: x => x.PayerPlanId,
                        principalTable: "PayerPlans",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ClaimItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ClaimId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ServiceCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NafdacNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CopayAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ClaimableAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    ApprovedAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    IsCovered = table.Column<bool>(type: "boolean", nullable: false),
                    DenialReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LabOrderId = table.Column<int>(type: "integer", nullable: true),
                    ImagingOrderId = table.Column<int>(type: "integer", nullable: true),
                    PrescriptionItemId = table.Column<int>(type: "integer", nullable: true),
                    ProcedureOrderId = table.Column<int>(type: "integer", nullable: true),
                    DispenseItemId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ClaimItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ClaimItems_Claims_ClaimId",
                        column: x => x.ClaimId,
                        principalTable: "Claims",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PatientPayers_PayerId",
                table: "PatientPayers",
                column: "PayerId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientPayers_PayerPlanId",
                table: "PatientPayers",
                column: "PayerPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_Authorizations_AuthorizationCode",
                table: "Authorizations",
                column: "AuthorizationCode");

            migrationBuilder.CreateIndex(
                name: "IX_Authorizations_CreatedById",
                table: "Authorizations",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Authorizations_EncounterId",
                table: "Authorizations",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_Authorizations_PatientPayerId",
                table: "Authorizations",
                column: "PatientPayerId");

            migrationBuilder.CreateIndex(
                name: "IX_ClaimItems_ClaimId",
                table: "ClaimItems",
                column: "ClaimId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_ClaimReference",
                table: "Claims",
                column: "ClaimReference");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_CreatedById",
                table: "Claims",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_EncounterId",
                table: "Claims",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_FacilityId_PayerId_Status",
                table: "Claims",
                columns: new[] { "FacilityId", "PayerId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Claims_PatientId",
                table: "Claims",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_PayerId",
                table: "Claims",
                column: "PayerId");

            migrationBuilder.CreateIndex(
                name: "IX_Claims_PayerPlanId",
                table: "Claims",
                column: "PayerPlanId");

            migrationBuilder.CreateIndex(
                name: "IX_PayerFormularies_DrugId",
                table: "PayerFormularies",
                column: "DrugId");

            migrationBuilder.CreateIndex(
                name: "IX_PayerFormularies_PayerPlanId_DrugId",
                table: "PayerFormularies",
                columns: new[] { "PayerPlanId", "DrugId" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PayerPlans_PayerId_Code",
                table: "PayerPlans",
                columns: new[] { "PayerId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payers_Code",
                table: "Payers",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Payers_OrgType",
                table: "Payers",
                column: "OrgType");

            migrationBuilder.AddForeignKey(
                name: "FK_PatientPayers_PayerPlans_PayerPlanId",
                table: "PatientPayers",
                column: "PayerPlanId",
                principalTable: "PayerPlans",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_PatientPayers_Payers_PayerId",
                table: "PatientPayers",
                column: "PayerId",
                principalTable: "Payers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PatientPayers_PayerPlans_PayerPlanId",
                table: "PatientPayers");

            migrationBuilder.DropForeignKey(
                name: "FK_PatientPayers_Payers_PayerId",
                table: "PatientPayers");

            migrationBuilder.DropTable(
                name: "Authorizations");

            migrationBuilder.DropTable(
                name: "ClaimItems");

            migrationBuilder.DropTable(
                name: "PayerFormularies");

            migrationBuilder.DropTable(
                name: "Claims");

            migrationBuilder.DropTable(
                name: "PayerPlans");

            migrationBuilder.DropTable(
                name: "Payers");

            migrationBuilder.DropIndex(
                name: "IX_PatientPayers_PayerId",
                table: "PatientPayers");

            migrationBuilder.DropIndex(
                name: "IX_PatientPayers_PayerPlanId",
                table: "PatientPayers");

            migrationBuilder.DropColumn(
                name: "PayerId",
                table: "PatientPayers");

            migrationBuilder.DropColumn(
                name: "PayerPlanId",
                table: "PatientPayers");
        }
    }
}
