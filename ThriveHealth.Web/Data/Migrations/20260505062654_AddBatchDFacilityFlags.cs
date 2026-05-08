using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class AddBatchDFacilityFlags : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<bool>(
                name: "AiMortuaryDraftEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AiPatientSummaryEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AiReferralDraftEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);

            migrationBuilder.AddColumn<bool>(
                name: "AiSoapStructureEnabled",
                table: "Facilities",
                type: "boolean",
                nullable: false,
                defaultValue: false);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "AiMortuaryDraftEnabled",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "AiPatientSummaryEnabled",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "AiReferralDraftEnabled",
                table: "Facilities");

            migrationBuilder.DropColumn(
                name: "AiSoapStructureEnabled",
                table: "Facilities");
        }
    }
}
