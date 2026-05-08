using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchCFacilityFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AiAncRiskEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AiEcgInterpretEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AiIdsrOutbreakEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AiPaedsDoseEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiAncRiskEnabled",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "AiEcgInterpretEnabled",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "AiIdsrOutbreakEnabled",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "AiPaedsDoseEnabled",
                table: "Facilities");
        }
    }
}
