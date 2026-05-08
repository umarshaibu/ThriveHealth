using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchAFacilityFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AiDrugCheckEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AiIcdCodingEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AiTriageAssistEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiDrugCheckEnabled",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "AiIcdCodingEnabled",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "AiTriageAssistEnabled",
                table: "Facilities");
        }
    }
}
