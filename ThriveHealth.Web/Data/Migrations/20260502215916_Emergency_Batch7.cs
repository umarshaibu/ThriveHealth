using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Emergency_Batch7 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "ResusBayId",
                table: "Encounters",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResusEndedAt",
                table: "Encounters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "ResusStartedAt",
                table: "Encounters",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ResusBays",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    IsTraumaBay = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResusBays", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResusBays_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ResusEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EncounterId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ResusEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ResusEvents_AspNetUsers_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ResusEvents_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TriageAssessments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    EncounterId = table.Column<int>(type: "integer", nullable: false),
                    Colour = table.Column<int>(type: "integer", nullable: false),
                    ArrivalMode = table.Column<int>(type: "integer", nullable: false),
                    ChiefComplaint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    IsTrauma = table.Column<bool>(type: "boolean", nullable: false),
                    MechanismOfInjury = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Avpu = table.Column<int>(type: "integer", nullable: true),
                    GcsTotal = table.Column<int>(type: "integer", nullable: true),
                    IsPregnant = table.Column<bool>(type: "boolean", nullable: false),
                    LastMealUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsForensicCase = table.Column<bool>(type: "boolean", nullable: false),
                    ForensicCategory = table.Column<int>(type: "integer", nullable: false),
                    PoliceReportNumber = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AccompanyingPerson = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    KnownAllergies = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    CurrentMedications = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    TriagedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    TriagedById = table.Column<string>(type: "text", nullable: true),
                    TargetSeenByUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TriageAssessments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TriageAssessments_AspNetUsers_TriagedById",
                        column: x => x.TriagedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TriageAssessments_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_ResusBayId",
                table: "Encounters",
                column: "ResusBayId");

            migrationBuilder.CreateIndex(
                name: "IX_Encounters_Type_Status",
                table: "Encounters",
                columns: new[] { "Type", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_ResusBays_FacilityId_Code",
                table: "ResusBays",
                columns: new[] { "FacilityId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ResusEvents_EncounterId_AtUtc",
                table: "ResusEvents",
                columns: new[] { "EncounterId", "AtUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_ResusEvents_RecordedById",
                table: "ResusEvents",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_TriageAssessments_Colour",
                table: "TriageAssessments",
                column: "Colour");

            migrationBuilder.CreateIndex(
                name: "IX_TriageAssessments_EncounterId",
                table: "TriageAssessments",
                column: "EncounterId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TriageAssessments_IsForensicCase",
                table: "TriageAssessments",
                column: "IsForensicCase");

            migrationBuilder.CreateIndex(
                name: "IX_TriageAssessments_TriagedById",
                table: "TriageAssessments",
                column: "TriagedById");

            migrationBuilder.AddForeignKey(
                name: "FK_Encounters_ResusBays_ResusBayId",
                table: "Encounters",
                column: "ResusBayId",
                principalTable: "ResusBays",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_Encounters_ResusBays_ResusBayId",
                table: "Encounters");

            migrationBuilder.DropTable(
                name: "ResusBays");

            migrationBuilder.DropTable(
                name: "ResusEvents");

            migrationBuilder.DropTable(
                name: "TriageAssessments");

            migrationBuilder.DropIndex(
                name: "IX_Encounters_ResusBayId",
                table: "Encounters");

            migrationBuilder.DropIndex(
                name: "IX_Encounters_Type_Status",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "ResusBayId",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "ResusEndedAt",
                table: "Encounters");

            migrationBuilder.DropColumn(
                name: "ResusStartedAt",
                table: "Encounters");
        }
    }
}
