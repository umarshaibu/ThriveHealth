using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Pharmacy_Batch5 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "DrugId",
                table: "PrescriptionItems",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "QuantityDispensed",
                table: "PrescriptionItems",
                type: "integer",
                nullable: false,
                defaultValue: 0);

            migrationBuilder.CreateTable(
                name: "DrugInteractions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DrugAKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    DrugBKey = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    Note = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugInteractions", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "Drugs",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    GenericName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: false),
                    BrandName = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    NafdacNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Strength = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    DoseForm = table.Column<int>(type: "integer", nullable: false),
                    Manufacturer = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    AtcCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Schedule = table.Column<int>(type: "integer", nullable: false),
                    UnitOfIssue = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    ReorderLevel = table.Column<int>(type: "integer", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Drugs", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "PharmacyStores",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PharmacyStores", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PharmacyStores_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Dispenses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    PrescriptionId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    StoreId = table.Column<int>(type: "integer", nullable: false),
                    DispensedById = table.Column<string>(type: "text", nullable: true),
                    DispensedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CounsellingNotes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    TotalAmount = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Dispenses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Dispenses_AspNetUsers_DispensedById",
                        column: x => x.DispensedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Dispenses_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dispenses_PharmacyStores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "PharmacyStores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Dispenses_Prescriptions_PrescriptionId",
                        column: x => x.PrescriptionId,
                        principalTable: "Prescriptions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DrugStocks",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DrugId = table.Column<int>(type: "integer", nullable: false),
                    StoreId = table.Column<int>(type: "integer", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    QuantityOnHand = table.Column<int>(type: "integer", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DrugStocks", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DrugStocks_Drugs_DrugId",
                        column: x => x.DrugId,
                        principalTable: "Drugs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_DrugStocks_PharmacyStores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "PharmacyStores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "StockMovements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DrugId = table.Column<int>(type: "integer", nullable: false),
                    StoreId = table.Column<int>(type: "integer", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    RunningBalance = table.Column<int>(type: "integer", nullable: false),
                    UnitCost = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    Reference = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PerformedById = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_StockMovements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_StockMovements_AspNetUsers_PerformedById",
                        column: x => x.PerformedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_StockMovements_Drugs_DrugId",
                        column: x => x.DrugId,
                        principalTable: "Drugs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_StockMovements_PharmacyStores_StoreId",
                        column: x => x.StoreId,
                        principalTable: "PharmacyStores",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "DispenseItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DispenseId = table.Column<int>(type: "integer", nullable: false),
                    PrescriptionItemId = table.Column<int>(type: "integer", nullable: true),
                    DrugId = table.Column<int>(type: "integer", nullable: true),
                    DrugName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Strength = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NafdacNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    QuantityDispensed = table.Column<int>(type: "integer", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: true),
                    LineTotal = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    IsSubstitution = table.Column<bool>(type: "boolean", nullable: false),
                    SubstitutionReason = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    PatientInstructions = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DispenseItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DispenseItems_Dispenses_DispenseId",
                        column: x => x.DispenseId,
                        principalTable: "Dispenses",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_DispenseItems_Drugs_DrugId",
                        column: x => x.DrugId,
                        principalTable: "Drugs",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DispenseItems_PrescriptionItems_PrescriptionItemId",
                        column: x => x.PrescriptionItemId,
                        principalTable: "PrescriptionItems",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_PrescriptionItems_DrugId",
                table: "PrescriptionItems",
                column: "DrugId");

            migrationBuilder.CreateIndex(
                name: "IX_DispenseItems_DispenseId",
                table: "DispenseItems",
                column: "DispenseId");

            migrationBuilder.CreateIndex(
                name: "IX_DispenseItems_DrugId",
                table: "DispenseItems",
                column: "DrugId");

            migrationBuilder.CreateIndex(
                name: "IX_DispenseItems_PrescriptionItemId",
                table: "DispenseItems",
                column: "PrescriptionItemId");

            migrationBuilder.CreateIndex(
                name: "IX_Dispenses_DispensedById",
                table: "Dispenses",
                column: "DispensedById");

            migrationBuilder.CreateIndex(
                name: "IX_Dispenses_FacilityId_DispensedAt",
                table: "Dispenses",
                columns: new[] { "FacilityId", "DispensedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_Dispenses_PatientId",
                table: "Dispenses",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Dispenses_PrescriptionId",
                table: "Dispenses",
                column: "PrescriptionId");

            migrationBuilder.CreateIndex(
                name: "IX_Dispenses_StoreId",
                table: "Dispenses",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_DrugInteractions_DrugAKey_DrugBKey",
                table: "DrugInteractions",
                columns: new[] { "DrugAKey", "DrugBKey" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Drugs_GenericName",
                table: "Drugs",
                column: "GenericName");

            migrationBuilder.CreateIndex(
                name: "IX_Drugs_NafdacNumber",
                table: "Drugs",
                column: "NafdacNumber");

            migrationBuilder.CreateIndex(
                name: "IX_DrugStocks_DrugId_StoreId_BatchNumber",
                table: "DrugStocks",
                columns: new[] { "DrugId", "StoreId", "BatchNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DrugStocks_StoreId",
                table: "DrugStocks",
                column: "StoreId");

            migrationBuilder.CreateIndex(
                name: "IX_PharmacyStores_FacilityId_Code",
                table: "PharmacyStores",
                columns: new[] { "FacilityId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_DrugId_CreatedAt",
                table: "StockMovements",
                columns: new[] { "DrugId", "CreatedAt" });

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_Kind",
                table: "StockMovements",
                column: "Kind");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_PerformedById",
                table: "StockMovements",
                column: "PerformedById");

            migrationBuilder.CreateIndex(
                name: "IX_StockMovements_StoreId",
                table: "StockMovements",
                column: "StoreId");

            migrationBuilder.AddForeignKey(
                name: "FK_PrescriptionItems_Drugs_DrugId",
                table: "PrescriptionItems",
                column: "DrugId",
                principalTable: "Drugs",
                principalColumn: "Id",
                onDelete: ReferentialAction.SetNull);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "FK_PrescriptionItems_Drugs_DrugId",
                table: "PrescriptionItems");

            migrationBuilder.DropTable(
                name: "DispenseItems");

            migrationBuilder.DropTable(
                name: "DrugInteractions");

            migrationBuilder.DropTable(
                name: "DrugStocks");

            migrationBuilder.DropTable(
                name: "StockMovements");

            migrationBuilder.DropTable(
                name: "Dispenses");

            migrationBuilder.DropTable(
                name: "Drugs");

            migrationBuilder.DropTable(
                name: "PharmacyStores");

            migrationBuilder.DropIndex(
                name: "IX_PrescriptionItems_DrugId",
                table: "PrescriptionItems");

            migrationBuilder.DropColumn(
                name: "DrugId",
                table: "PrescriptionItems");

            migrationBuilder.DropColumn(
                name: "QuantityDispensed",
                table: "PrescriptionItems");
        }
    }
}
