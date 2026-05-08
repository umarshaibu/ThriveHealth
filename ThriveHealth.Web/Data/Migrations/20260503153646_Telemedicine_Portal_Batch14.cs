using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Telemedicine_Portal_Batch14 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "PortalAccounts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PasswordHash = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    EmailVerified = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LastLoginAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    LastLoginIp = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalAccounts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortalAccounts_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeleSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    SessionNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    ClinicianId = table.Column<string>(type: "text", nullable: true),
                    EncounterId = table.Column<int>(type: "integer", nullable: true),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ScheduledStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PatientJoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ClinicianJoinedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    StartedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EndedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RoomToken = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    JoinUrl = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ConsultationReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClinicianNotes = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PatientSymptoms = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PatientRating = table.Column<int>(type: "integer", nullable: true),
                    PatientFeedback = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeleSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeleSessions_AspNetUsers_ClinicianId",
                        column: x => x.ClinicianId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TeleSessions_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TeleSessions_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PortalSymptomIntakes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    TeleSessionId = table.Column<int>(type: "integer", nullable: true),
                    ChiefComplaint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Symptoms = table.Column<string>(type: "character varying(1500)", maxLength: 1500, nullable: true),
                    DurationDays = table.Column<int>(type: "integer", nullable: true),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    CurrentMedications = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    KnownAllergies = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    SubmittedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PortalSymptomIntakes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PortalSymptomIntakes_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PortalSymptomIntakes_TeleSessions_TeleSessionId",
                        column: x => x.TeleSessionId,
                        principalTable: "TeleSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PortalAccounts_Email",
                table: "PortalAccounts",
                column: "Email",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortalAccounts_PatientId",
                table: "PortalAccounts",
                column: "PatientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_PortalSymptomIntakes_PatientId_SubmittedAt",
                table: "PortalSymptomIntakes",
                columns: new[] { "PatientId", "SubmittedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_PortalSymptomIntakes_TeleSessionId",
                table: "PortalSymptomIntakes",
                column: "TeleSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TeleSessions_ClinicianId",
                table: "TeleSessions",
                column: "ClinicianId");

            migrationBuilder.CreateIndex(
                name: "IX_TeleSessions_EncounterId",
                table: "TeleSessions",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_TeleSessions_FacilityId_Status_ScheduledStartUtc",
                table: "TeleSessions",
                columns: new[] { "FacilityId", "Status", "ScheduledStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TeleSessions_PatientId",
                table: "TeleSessions",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_TeleSessions_SessionNumber",
                table: "TeleSessions",
                column: "SessionNumber",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "PortalAccounts");

            migrationBuilder.DropTable(
                name: "PortalSymptomIntakes");

            migrationBuilder.DropTable(
                name: "TeleSessions");
        }
    }
}
