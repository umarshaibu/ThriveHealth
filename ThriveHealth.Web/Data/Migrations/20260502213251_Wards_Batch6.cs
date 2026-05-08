using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Wards_Batch6 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Wards",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    ColorHex = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Wards", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Wards_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Beds",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    WardId = table.Column<int>(type: "integer", nullable: false),
                    BedNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Restriction = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CurrentAdmissionId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Beds", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Beds_Wards_WardId",
                        column: x => x.WardId,
                        principalTable: "Wards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Admissions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    WardId = table.Column<int>(type: "integer", nullable: false),
                    BedId = table.Column<int>(type: "integer", nullable: false),
                    AdmittingDoctorId = table.Column<string>(type: "text", nullable: false),
                    SourceEncounterId = table.Column<int>(type: "integer", nullable: true),
                    AdmissionEncounterId = table.Column<int>(type: "integer", nullable: true),
                    ReasonForAdmission = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    WorkingDiagnosis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AdmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DischargedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DischargeDisposition = table.Column<int>(type: "integer", nullable: true),
                    DischargeDiagnosis = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DischargeSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    FollowUpPlan = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    DischargedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Admissions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Admissions_AspNetUsers_AdmittingDoctorId",
                        column: x => x.AdmittingDoctorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Admissions_AspNetUsers_DischargedById",
                        column: x => x.DischargedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Admissions_Beds_BedId",
                        column: x => x.BedId,
                        principalTable: "Beds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Admissions_Encounters_AdmissionEncounterId",
                        column: x => x.AdmissionEncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Admissions_Encounters_SourceEncounterId",
                        column: x => x.SourceEncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Admissions_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Admissions_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Admissions_Wards_WardId",
                        column: x => x.WardId,
                        principalTable: "Wards",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BedAllocations",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionId = table.Column<int>(type: "integer", nullable: false),
                    BedId = table.Column<int>(type: "integer", nullable: false),
                    FromUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ToUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    AllocatedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BedAllocations", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BedAllocations_Admissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "Admissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_BedAllocations_Beds_BedId",
                        column: x => x.BedId,
                        principalTable: "Beds",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "FluidEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    VolumeMl = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    RecordedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_FluidEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_FluidEntries_Admissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "Admissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_FluidEntries_AspNetUsers_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "InpatientMedications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionId = table.Column<int>(type: "integer", nullable: false),
                    DrugId = table.Column<int>(type: "integer", nullable: true),
                    DrugName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Strength = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Dose = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Route = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Frequency = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Instructions = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsControlled = table.Column<bool>(type: "boolean", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PrescribedById = table.Column<string>(type: "text", nullable: true),
                    PrescribedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StopReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_InpatientMedications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_InpatientMedications_Admissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "Admissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_InpatientMedications_AspNetUsers_PrescribedById",
                        column: x => x.PrescribedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_InpatientMedications_Drugs_DrugId",
                        column: x => x.DrugId,
                        principalTable: "Drugs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "NursingNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionId = table.Column<int>(type: "integer", nullable: false),
                    Shift = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    Handover = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    RecordedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_NursingNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_NursingNotes_Admissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "Admissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_NursingNotes_AspNetUsers_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "WardRoundEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AdmissionId = table.Column<int>(type: "integer", nullable: false),
                    Body = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: false),
                    PlanChanges = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RecordedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_WardRoundEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_WardRoundEntries_Admissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "Admissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_WardRoundEntries_AspNetUsers_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MarSlots",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    InpatientMedicationId = table.Column<int>(type: "integer", nullable: false),
                    ScheduledUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    AdministeredUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AdministeredById = table.Column<string>(type: "text", nullable: true),
                    ActualDose = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Route = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    BatchNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MarSlots", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MarSlots_AspNetUsers_AdministeredById",
                        column: x => x.AdministeredById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MarSlots_InpatientMedications_InpatientMedicationId",
                        column: x => x.InpatientMedicationId,
                        principalTable: "InpatientMedications",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Admissions_AdmissionEncounterId",
                table: "Admissions",
                column: "AdmissionEncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_Admissions_AdmittingDoctorId",
                table: "Admissions",
                column: "AdmittingDoctorId");

            migrationBuilder.CreateIndex(
                name: "IX_Admissions_BedId",
                table: "Admissions",
                column: "BedId");

            migrationBuilder.CreateIndex(
                name: "IX_Admissions_DischargedById",
                table: "Admissions",
                column: "DischargedById");

            migrationBuilder.CreateIndex(
                name: "IX_Admissions_FacilityId_Status",
                table: "Admissions",
                columns: new[] { "FacilityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Admissions_PatientId_AdmittedAt",
                table: "Admissions",
                columns: new[] { "PatientId", "AdmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Admissions_SourceEncounterId",
                table: "Admissions",
                column: "SourceEncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_Admissions_WardId",
                table: "Admissions",
                column: "WardId");

            migrationBuilder.CreateIndex(
                name: "IX_BedAllocations_AdmissionId_FromUtc",
                table: "BedAllocations",
                columns: new[] { "AdmissionId", "FromUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_BedAllocations_BedId",
                table: "BedAllocations",
                column: "BedId");

            migrationBuilder.CreateIndex(
                name: "IX_Beds_Status",
                table: "Beds",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_Beds_WardId_BedNumber",
                table: "Beds",
                columns: new[] { "WardId", "BedNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_FluidEntries_AdmissionId_RecordedUtc",
                table: "FluidEntries",
                columns: new[] { "AdmissionId", "RecordedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_FluidEntries_RecordedById",
                table: "FluidEntries",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_InpatientMedications_AdmissionId_Status",
                table: "InpatientMedications",
                columns: new[] { "AdmissionId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_InpatientMedications_DrugId",
                table: "InpatientMedications",
                column: "DrugId");

            migrationBuilder.CreateIndex(
                name: "IX_InpatientMedications_PrescribedById",
                table: "InpatientMedications",
                column: "PrescribedById");

            migrationBuilder.CreateIndex(
                name: "IX_MarSlots_AdministeredById",
                table: "MarSlots",
                column: "AdministeredById");

            migrationBuilder.CreateIndex(
                name: "IX_MarSlots_InpatientMedicationId_ScheduledUtc",
                table: "MarSlots",
                columns: new[] { "InpatientMedicationId", "ScheduledUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_MarSlots_Status",
                table: "MarSlots",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_NursingNotes_AdmissionId",
                table: "NursingNotes",
                column: "AdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_NursingNotes_RecordedById",
                table: "NursingNotes",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_WardRoundEntries_AdmissionId",
                table: "WardRoundEntries",
                column: "AdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_WardRoundEntries_RecordedById",
                table: "WardRoundEntries",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_Wards_FacilityId_Code",
                table: "Wards",
                columns: new[] { "FacilityId", "Code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BedAllocations");

            migrationBuilder.DropTable(
                name: "FluidEntries");

            migrationBuilder.DropTable(
                name: "MarSlots");

            migrationBuilder.DropTable(
                name: "NursingNotes");

            migrationBuilder.DropTable(
                name: "WardRoundEntries");

            migrationBuilder.DropTable(
                name: "InpatientMedications");

            migrationBuilder.DropTable(
                name: "Admissions");

            migrationBuilder.DropTable(
                name: "Beds");

            migrationBuilder.DropTable(
                name: "Wards");
        }
    }
}
