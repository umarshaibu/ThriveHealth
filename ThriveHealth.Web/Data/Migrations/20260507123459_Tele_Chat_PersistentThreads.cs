using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Tele_Chat_PersistentThreads : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "ChatPackages",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    BillId = table.Column<int>(type: "integer", nullable: true),
                    PackageNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Price = table.Column<decimal>(type: "numeric", nullable: false),
                    PurchasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ExpiresAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChatPackages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChatPackages_Bills_BillId",
                        column: x => x.BillId,
                        principalTable: "Bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChatPackages_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeleChatMessages",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeleSessionId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    SenderRole = table.Column<int>(type: "integer", nullable: false),
                    SenderUserId = table.Column<string>(type: "text", nullable: true),
                    Body = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    SentAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReadByPatientAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReadByClinicianAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeleChatMessages", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeleChatMessages_AspNetUsers_SenderUserId",
                        column: x => x.SenderUserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TeleChatMessages_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TeleChatMessages_TeleSessions_TeleSessionId",
                        column: x => x.TeleSessionId,
                        principalTable: "TeleSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_ChatPackages_BillId",
                table: "ChatPackages",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_ChatPackages_PackageNumber",
                table: "ChatPackages",
                column: "PackageNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ChatPackages_PatientId_ExpiresAt",
                table: "ChatPackages",
                columns: new[] { "PatientId", "ExpiresAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TeleChatMessages_PatientId_SentAt",
                table: "TeleChatMessages",
                columns: new[] { "PatientId", "SentAt" });

            migrationBuilder.CreateIndex(
                name: "IX_TeleChatMessages_SenderUserId",
                table: "TeleChatMessages",
                column: "SenderUserId");

            migrationBuilder.CreateIndex(
                name: "IX_TeleChatMessages_TeleSessionId_SentAt",
                table: "TeleChatMessages",
                columns: new[] { "TeleSessionId", "SentAt" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "ChatPackages");

            migrationBuilder.DropTable(
                name: "TeleChatMessages");
        }
    }
}
