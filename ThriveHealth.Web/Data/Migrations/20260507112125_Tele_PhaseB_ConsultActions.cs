using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Tele_PhaseB_ConsultActions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "MedicalCertificates",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    CertificateNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EncounterId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    IssuedById = table.Column<string>(type: "text", nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Diagnosis = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    IcdCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Recommendations = table.Column<string>(type: "character varying(800)", maxLength: 800, nullable: true),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MedicalCertificates", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MedicalCertificates_AspNetUsers_IssuedById",
                        column: x => x.IssuedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MedicalCertificates_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_MedicalCertificates_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Referrals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    ReferralNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    EncounterId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    ReferringClinicianId = table.Column<string>(type: "text", nullable: true),
                    ReferredToClinicianId = table.Column<string>(type: "text", nullable: true),
                    Specialty = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ClinicalSummary = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AcknowledgedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Referrals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Referrals_AspNetUsers_ReferredToClinicianId",
                        column: x => x.ReferredToClinicianId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Referrals_AspNetUsers_ReferringClinicianId",
                        column: x => x.ReferringClinicianId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Referrals_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Referrals_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_MedicalCertificates_CertificateNumber",
                table: "MedicalCertificates",
                column: "CertificateNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MedicalCertificates_EncounterId",
                table: "MedicalCertificates",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalCertificates_FacilityId_IssuedAt",
                table: "MedicalCertificates",
                columns: new[] { "FacilityId", "IssuedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_MedicalCertificates_IssuedById",
                table: "MedicalCertificates",
                column: "IssuedById");

            migrationBuilder.CreateIndex(
                name: "IX_MedicalCertificates_PatientId",
                table: "MedicalCertificates",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_EncounterId",
                table: "Referrals",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_FacilityId_Status_CreatedAt",
                table: "Referrals",
                columns: new[] { "FacilityId", "Status", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_PatientId",
                table: "Referrals",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferralNumber",
                table: "Referrals",
                column: "ReferralNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferredToClinicianId",
                table: "Referrals",
                column: "ReferredToClinicianId");

            migrationBuilder.CreateIndex(
                name: "IX_Referrals_ReferringClinicianId",
                table: "Referrals",
                column: "ReferringClinicianId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "MedicalCertificates");

            migrationBuilder.DropTable(
                name: "Referrals");
        }
    }
}
