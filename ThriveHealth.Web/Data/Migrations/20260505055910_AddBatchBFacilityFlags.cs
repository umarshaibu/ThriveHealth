using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchBFacilityFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AiBillAnomalyEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AiClaimsRiskEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AiInventoryForecastEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AiNlSearchEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AiSchedulingAssistEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiBillAnomalyEnabled",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "AiClaimsRiskEnabled",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "AiInventoryForecastEnabled",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "AiNlSearchEnabled",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "AiSchedulingAssistEnabled",
                table: "Facilities");
        }
    }
}
