using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Encounters_Batch4 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "DotPhrases",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    OwnerId = table.Column<string>(type: "text", nullable: false),
                    Trigger = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Expansion = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DotPhrases", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Encounters",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    AppointmentId = table.Column<int>(type: "integer", nullable: true),
                    QueueEntryId = table.Column<int>(type: "integer", nullable: true),
                    ClinicId = table.Column<int>(type: "integer", nullable: false),
                    ClinicianId = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    SignedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChiefComplaint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Encounters", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Encounters_Appointments_AppointmentId",
                        column: x => x.AppointmentId,
                        principalTable: "Appointments",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Encounters_AspNetUsers_ClinicianId",
                        column: x => x.ClinicianId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Encounters_Clinics_ClinicId",
                        column: x => x.ClinicId,
                        principalTable: "Clinics",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Encounters_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Encounters_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Encounters_QueueEntries_QueueEntryId",
                        column: x => x.QueueEntryId,
                        principalTable: "QueueEntries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "IcdCodes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Version = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Category = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    LocalSynonyms = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsCommon = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IcdCodes", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "EncounterDiagnoses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EncounterId = table.Column<int>(type: "integer", nullable: false),
                    IcdCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_EncounterDiagnoses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_EncounterDiagnoses_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "ImagingOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EncounterId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    Modality = table.Column<int>(type: "integer", nullable: false),
                    StudyDescription = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    ClinicalIndication = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Urgency = table.Column<int>(type: "integer", nullable: false),
                    OrderedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderedById = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImagingOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImagingOrders_AspNetUsers_OrderedById",
                        column: x => x.OrderedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ImagingOrders_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImagingOrders_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EncounterId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    TestName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    LoincCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Specimen = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Urgency = table.Column<int>(type: "integer", nullable: false),
                    ClinicalIndication = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ResultSummary = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OrderedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderedById = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabOrders_AspNetUsers_OrderedById",
                        column: x => x.OrderedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LabOrders_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LabOrders_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Prescriptions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EncounterId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PrescribedById = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Prescriptions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Prescriptions_AspNetUsers_PrescribedById",
                        column: x => x.PrescribedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Prescriptions_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Prescriptions_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ProcedureOrders",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EncounterId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    ProcedureName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CptCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Urgency = table.Column<int>(type: "integer", nullable: false),
                    OrderedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    OrderedById = table.Column<string>(type: "text", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ProcedureOrders", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ProcedureOrders_AspNetUsers_OrderedById",
                        column: x => x.OrderedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ProcedureOrders_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ProcedureOrders_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "SoapNotes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EncounterId = table.Column<int>(type: "integer", nullable: false),
                    Subjective = table.Column<string>(type: "text", nullable: true),
                    Objective = table.Column<string>(type: "text", nullable: true),
                    Assessment = table.Column<string>(type: "text", nullable: true),
                    Plan = table.Column<string>(type: "text", nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_SoapNotes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_SoapNotes_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PrescriptionItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PrescriptionId = table.Column<int>(type: "integer", nullable: false),
                    DrugName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    NafdacNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Dose = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Route = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Frequency = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Duration = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: true),
                    Instructions = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IsControlled = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PrescriptionItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PrescriptionItems_Prescriptions_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalTable: "Prescriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_DotPhrases_OwnerId_Trigger",
                table: "DotPhrases",
                columns: new[] { "OwnerId", "Trigger" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_EncounterDiagnoses_EncounterId_IcdCode",
                table: "EncounterDiagnoses",
                columns: new[] { "EncounterId", "IcdCode" });

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_AppointmentId",
                table: "Encounters",
                column: "AppointmentId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_ClinicianId_StartedAt",
                table: "Encounters",
                columns: new[] { "ClinicianId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_ClinicId",
                table: "Encounters",
                column: "ClinicId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_FacilityId",
                table: "Encounters",
                column: "FacilityId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_PatientId_StartedAt",
                table: "Encounters",
                columns: new[] { "PatientId", "StartedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_QueueEntryId",
                table: "Encounters",
                column: "QueueEntryId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_Status",
                table: "Encounters",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_IcdCodes_Code",
                table: "IcdCodes",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IcdCodes_IsCommon",
                table: "IcdCodes",
                column: "IsCommon");

            migrationBuilder.CreateIndex(
                name: "IX_ImagingOrders_EncounterId",
                table: "ImagingOrders",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_ImagingOrders_OrderedById",
                table: "ImagingOrders",
                column: "OrderedById");

            migrationBuilder.CreateIndex(
                name: "IX_ImagingOrders_PatientId_OrderedAt",
                table: "ImagingOrders",
                columns: new[] { "PatientId", "OrderedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_ImagingOrders_Status",
                table: "ImagingOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_EncounterId",
                table: "LabOrders",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_OrderedById",
                table: "LabOrders",
                column: "OrderedById");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_PatientId_OrderedAt",
                table: "LabOrders",
                columns: new[] { "PatientId", "OrderedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_Status",
                table: "LabOrders",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItems_PrescriptionId",
                table: "PrescriptionItems",
                column: "PrescriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_EncounterId",
                table: "Prescriptions",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_PatientId_IssuedAt",
                table: "Prescriptions",
                columns: new[] { "PatientId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Prescriptions_PrescribedById",
                table: "Prescriptions",
                column: "PrescribedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureOrders_EncounterId",
                table: "ProcedureOrders",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureOrders_OrderedById",
                table: "ProcedureOrders",
                column: "OrderedById");

            migrationBuilder.CreateIndex(
                name: "IX_ProcedureOrders_PatientId",
                table: "ProcedureOrders",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_SoapNotes_EncounterId",
                table: "SoapNotes",
                column: "EncounterId",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "DotPhrases");

            migrationBuilder.DropTable(
                name: "EncounterDiagnoses");

            migrationBuilder.DropTable(
                name: "IcdCodes");

            migrationBuilder.DropTable(
                name: "ImagingOrders");

            migrationBuilder.DropTable(
                name: "LabOrders");

            migrationBuilder.DropTable(
                name: "PrescriptionItems");

            migrationBuilder.DropTable(
                name: "ProcedureOrders");

            migrationBuilder.DropTable(
                name: "SoapNotes");

            migrationBuilder.DropTable(
                name: "Prescriptions");

            migrationBuilder.DropTable(
                name: "Encounters");
        }
    }
}
