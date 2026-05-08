using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchEFacilityFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AiAdherenceParseEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AiAuditAnomalyEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AiDocQualityEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AiSymptomCheckerEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AiTranslateEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiAdherenceParseEnabled",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "AiAuditAnomalyEnabled",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "AiDocQualityEnabled",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "AiSymptomCheckerEnabled",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "AiTranslateEnabled",
                table: "Facilities");
        }
    }
}
