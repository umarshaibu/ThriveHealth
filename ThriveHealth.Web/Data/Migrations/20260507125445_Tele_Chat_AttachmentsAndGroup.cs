using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Tele_Chat_AttachmentsAndGroup : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "TeleChatAttachments",
                columns: table => new
                {
                    Id = table.Column<long>(type: "bigint", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MessageId = table.Column<long>(type: "bigint", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    SizeBytes = table.Column<long>(type: "bigint", nullable: false),
                    Url = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeleChatAttachments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeleChatAttachments_TeleChatMessages_MessageId",
                        column: x => x.MessageId,
                        principalTable: "TeleChatMessages",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TeleSessionParticipants",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TeleSessionId = table.Column<int>(type: "integer", nullable: false),
                    ClinicianId = table.Column<string>(type: "text", nullable: false),
                    Role = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AddedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    AddedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TeleSessionParticipants", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TeleSessionParticipants_AspNetUsers_ClinicianId",
                        column: x => x.ClinicianId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_TeleSessionParticipants_TeleSessions_TeleSessionId",
                        column: x => x.TeleSessionId,
                        principalTable: "TeleSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_TeleChatAttachments_MessageId",
                table: "TeleChatAttachments",
                column: "MessageId");

            migrationBuilder.CreateIndex(
                name: "IX_TeleSessionParticipants_ClinicianId",
                table: "TeleSessionParticipants",
                column: "ClinicianId");

            migrationBuilder.CreateIndex(
                name: "IX_TeleSessionParticipants_TeleSessionId_ClinicianId",
                table: "TeleSessionParticipants",
                columns: new[] { "TeleSessionId", "ClinicianId" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "TeleChatAttachments");

            migrationBuilder.DropTable(
                name: "TeleSessionParticipants");
        }
    }
}
