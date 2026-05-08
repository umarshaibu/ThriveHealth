using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Reporting_Analytics_Batch15 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "NhmisReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    Month = table.Column<int>(type: "integer", nullable: false),
                    Period = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    GeneratedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    SubmittedToWhom = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    SubmissionReference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    AggregatesJson = table.Column<string>(type: "text", nullable: false),
                    GeneratedById = table.Column<string>(type: "text", nullable: true),
                    SubmittedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NhmisReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NhmisReports_AspNetUsers_GeneratedById",
                        column: x => x.GeneratedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_NhmisReports_AspNetUsers_SubmittedById",
                        column: x => x.SubmittedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "NotifiableDiseases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    CaseDefinition = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Window = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NotifiableDiseases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "IdsrCases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    CaseNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    NotifiableDiseaseId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: true),
                    PatientName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    AgeYears = table.Column<int>(type: "integer", nullable: true),
                    Sex = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Lga = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    State = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    OnsetDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ReportDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Classification = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Symptoms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Exposure = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Vaccinated = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    LabSampleCollected = table.Column<bool>(type: "boolean", nullable: false),
                    LabSampleDate = table.Column<DateOnly>(type: "date", nullable: true),
                    LabSampleType = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    LabResult = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OutcomeDate = table.Column<DateOnly>(type: "date", nullable: true),
                    NotifiedNcdc = table.Column<bool>(type: "boolean", nullable: false),
                    NotifiedNcdcAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Comments = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReportedById = table.Column<string>(type: "text", nullable: true),
                    ReportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IdsrCases", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IdsrCases_AspNetUsers_ReportedById",
                        column: x => x.ReportedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_IdsrCases_NotifiableDiseases_NotifiableDiseaseId",
                        column: x => x.NotifiableDiseaseId,
                        principalTable: "NotifiableDiseases",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_IdsrCases_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_IdsrCases_CaseNumber",
                table: "IdsrCases",
                column: "CaseNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IdsrCases_FacilityId_Status_OnsetDate",
                table: "IdsrCases",
                columns: new[] { "FacilityId", "Status", "OnsetDate" });

            migrationBuilder.CreateIndex(
                name: "IX_IdsrCases_NotifiableDiseaseId_OnsetDate",
                table: "IdsrCases",
                columns: new[] { "NotifiableDiseaseId", "OnsetDate" });

            migrationBuilder.CreateIndex(
                name: "IX_IdsrCases_PatientId",
                table: "IdsrCases",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_IdsrCases_ReportedById",
                table: "IdsrCases",
                column: "ReportedById");

            migrationBuilder.CreateIndex(
                name: "IX_NhmisReports_FacilityId_Period",
                table: "NhmisReports",
                columns: new[] { "FacilityId", "Period" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_NhmisReports_GeneratedById",
                table: "NhmisReports",
                column: "GeneratedById");

            migrationBuilder.CreateIndex(
                name: "IX_NhmisReports_SubmittedById",
                table: "NhmisReports",
                column: "SubmittedById");

            migrationBuilder.CreateIndex(
                name: "IX_NotifiableDiseases_Code",
                table: "NotifiableDiseases",
                column: "Code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "IdsrCases");

            migrationBuilder.DropTable(
                name: "NhmisReports");

            migrationBuilder.DropTable(
                name: "NotifiableDiseases");
        }
    }
}
