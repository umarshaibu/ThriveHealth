using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Diagnostics_Batch8 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "AccessionNumber",
                table: "LabOrders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.AddColumn<DateTime>(
                name: "CollectedAt",
                table: "LabOrders",
                type: "timestamp with time zone",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "CollectedById",
                table: "LabOrders",
                type: "text",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "LabTestId",
                table: "LabOrders",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "AccessionNumber",
                table: "ImagingOrders",
                type: "character varying(40)",
                maxLength: 40,
                nullable: true);

            migrationBuilder.CreateTable(
                name: "ImagingReports",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    ImagingOrderId = table.Column<int>(type: "integer", nullable: false),
                    AccessionNumber = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Technique = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Contrast = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    DicomStudyUid = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    DicomViewerUrl = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Findings = table.Column<string>(type: "text", nullable: true),
                    Impression = table.Column<string>(type: "text", nullable: true),
                    Recommendation = table.Column<string>(type: "text", nullable: true),
                    HasCriticalFinding = table.Column<bool>(type: "boolean", nullable: false),
                    PerformedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    PerformedById = table.Column<string>(type: "text", nullable: true),
                    ReportedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReportedById = table.Column<string>(type: "text", nullable: true),
                    AuthorizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuthorizedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImagingReports", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImagingReports_AspNetUsers_AuthorizedById",
                        column: x => x.AuthorizedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ImagingReports_AspNetUsers_PerformedById",
                        column: x => x.PerformedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ImagingReports_AspNetUsers_ReportedById",
                        column: x => x.ReportedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ImagingReports_ImagingOrders_ImagingOrderId",
                        column: x => x.ImagingOrderId,
                        principalTable: "ImagingOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LabTests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Section = table.Column<int>(type: "integer", nullable: false),
                    LoincCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Specimen = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    Container = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    TurnaroundHours = table.Column<int>(type: "integer", nullable: false),
                    Price = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabTests", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "LabAnalytes",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LabTestId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RefLow = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    RefHigh = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    CriticalLow = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    CriticalHigh = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    AgeGroup = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Sex = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabAnalytes", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabAnalytes_LabTests_LabTestId",
                        column: x => x.LabTestId,
                        principalTable: "LabTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LabResults",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LabOrderId = table.Column<int>(type: "integer", nullable: false),
                    LabTestId = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    GeneralComment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Methodology = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EnteredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EnteredById = table.Column<string>(type: "text", nullable: true),
                    AuthorizedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    AuthorizedById = table.Column<string>(type: "text", nullable: true),
                    HasCriticalValue = table.Column<bool>(type: "boolean", nullable: false),
                    CriticalNotified = table.Column<bool>(type: "boolean", nullable: false),
                    CriticalNotifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabResults", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabResults_AspNetUsers_AuthorizedById",
                        column: x => x.AuthorizedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LabResults_AspNetUsers_EnteredById",
                        column: x => x.EnteredById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LabResults_LabOrders_LabOrderId",
                        column: x => x.LabOrderId,
                        principalTable: "LabOrders",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_LabResults_LabTests_LabTestId",
                        column: x => x.LabTestId,
                        principalTable: "LabTests",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "LabResultValues",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    LabResultId = table.Column<int>(type: "integer", nullable: false),
                    LabAnalyteId = table.Column<int>(type: "integer", nullable: false),
                    AnalyteName = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    Unit = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Value = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    NumericValue = table.Column<decimal>(type: "numeric(14,4)", precision: 14, scale: 4, nullable: true),
                    Flag = table.Column<int>(type: "integer", nullable: false),
                    RefRangeDisplay = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Note = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LabResultValues", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LabResultValues_LabAnalytes_LabAnalyteId",
                        column: x => x.LabAnalyteId,
                        principalTable: "LabAnalytes",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_LabResultValues_LabResults_LabResultId",
                        column: x => x.LabResultId,
                        principalTable: "LabResults",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_AccessionNumber",
                table: "LabOrders",
                column: "AccessionNumber");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_CollectedById",
                table: "LabOrders",
                column: "CollectedById");

            migrationBuilder.CreateIndex(
                name: "IX_LabOrders_LabTestId",
                table: "LabOrders",
                column: "LabTestId");

            migrationBuilder.CreateIndex(
                name: "IX_ImagingOrders_AccessionNumber",
                table: "ImagingOrders",
                column: "AccessionNumber");

            migrationBuilder.CreateIndex(
                name: "IX_ImagingReports_AuthorizedById",
                table: "ImagingReports",
                column: "AuthorizedById");

            migrationBuilder.CreateIndex(
                name: "IX_ImagingReports_ImagingOrderId",
                table: "ImagingReports",
                column: "ImagingOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImagingReports_PerformedById",
                table: "ImagingReports",
                column: "PerformedById");

            migrationBuilder.CreateIndex(
                name: "IX_ImagingReports_ReportedById",
                table: "ImagingReports",
                column: "ReportedById");

            migrationBuilder.CreateIndex(
                name: "IX_LabAnalytes_LabTestId_SortOrder",
                table: "LabAnalytes",
                columns: new[] { "LabTestId", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_AuthorizedById",
                table: "LabResults",
                column: "AuthorizedById");

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_EnteredById",
                table: "LabResults",
                column: "EnteredById");

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_HasCriticalValue",
                table: "LabResults",
                column: "HasCriticalValue");

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_LabOrderId",
                table: "LabResults",
                column: "LabOrderId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_LabTestId",
                table: "LabResults",
                column: "LabTestId");

            migrationBuilder.CreateIndex(
                name: "IX_LabResults_Status",
                table: "LabResults",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_LabResultValues_LabAnalyteId",
                table: "LabResultValues",
                column: "LabAnalyteId");

            migrationBuilder.CreateIndex(
                name: "IX_LabResultValues_LabResultId",
                table: "LabResultValues",
                column: "LabResultId");

            migrationBuilder.CreateIndex(
                name: "IX_LabTests_Code",
                table: "LabTests",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LabTests_Section",
                table: "LabTests",
                column: "Section");

            migrationBuilder.AddForeignKey(
                name: "FK_LabOrders_AspNetUsers_CollectedById",
                table: "LabOrders",
                column: "CollectedById",
                principalTable: "AspNetUsers",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);

            migrationBuilder.AddForeignKey(
                name: "FK_LabOrders_LabTests_LabTestId",
                table: "LabOrders",
                column: "LabTestId",
                principalTable: "LabTests",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_LabOrders_AspNetUsers_CollectedById",
                table: "LabOrders");

            migrationBuilder.DropForeignKey(
                name: "FK_LabOrders_LabTests_LabTestId",
                table: "LabOrders");

            migrationBuilder.DropTable(
                name: "ImagingReports");

            migrationBuilder.DropTable(
                name: "LabResultValues");

            migrationBuilder.DropTable(
                name: "LabAnalytes");

            migrationBuilder.DropTable(
                name: "LabResults");

            migrationBuilder.DropTable(
                name: "LabTests");

            migrationBuilder.DropIndex(
                name: "IX_LabOrders_AccessionNumber",
                table: "LabOrders");

            migrationBuilder.DropIndex(
                name: "IX_LabOrders_CollectedById",
                table: "LabOrders");

            migrationBuilder.DropIndex(
                name: "IX_LabOrders_LabTestId",
                table: "LabOrders");

            migrationBuilder.DropIndex(
                name: "IX_ImagingOrders_AccessionNumber",
                table: "ImagingOrders");

            migrationBuilder.DropColumn(
                name: "AccessionNumber",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "CollectedAt",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "CollectedById",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "LabTestId",
                table: "LabOrders");

            migrationBuilder.DropColumn(
                name: "AccessionNumber",
                table: "ImagingOrders");
        }
    }
}
