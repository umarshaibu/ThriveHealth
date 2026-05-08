using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Tele_BillLink_PhaseA : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "BillId",
                table: "TeleSessions",
                type: "integer",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeleSessions_BillId",
                table: "TeleSessions",
                column: "BillId");

            migrationBuilder.AddForeignKey(
                name: "FK_TeleSessions_Bills_BillId",
                table: "TeleSessions",
                column: "BillId",
                principalTable: "Bills",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeleSessions_Bills_BillId",
                table: "TeleSessions");

            migrationBuilder.DropIndex(
                name: "IX_TeleSessions_BillId",
                table: "TeleSessions");

            migrationBuilder.DropColumn(
                name: "BillId",
                table: "TeleSessions");
        }
    }
}
