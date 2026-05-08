using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Tele_Chat_Replies : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<long>(
                name: "RepliesToMessageId",
                table: "TeleChatMessages",
                type: "bigint",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "IX_TeleChatMessages_RepliesToMessageId",
                table: "TeleChatMessages",
                column: "RepliesToMessageId");

            migrationBuilder.AddForeignKey(
                name: "FK_TeleChatMessages_TeleChatMessages_RepliesToMessageId",
                table: "TeleChatMessages",
                column: "RepliesToMessageId",
                principalTable: "TeleChatMessages",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_TeleChatMessages_TeleChatMessages_RepliesToMessageId",
                table: "TeleChatMessages");

            migrationBuilder.DropIndex(
                name: "IX_TeleChatMessages_RepliesToMessageId",
                table: "TeleChatMessages");

            migrationBuilder.DropColumn(
                name: "RepliesToMessageId",
                table: "TeleChatMessages");
        }
    }
}
