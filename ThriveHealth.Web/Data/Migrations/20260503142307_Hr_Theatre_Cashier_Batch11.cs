using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Hr_Theatre_Cashier_Batch11 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "Bills",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    BillNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    EncounterId = table.Column<int>(type: "integer", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ServiceDate = table.Column<DateOnly>(type: "date", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    GrossAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    DiscountAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    NetAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    PaidAmount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DiscountReason = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CreatedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Bills", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Bills_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Bills_Encounters_EncounterId",
                        column: x => x.EncounterId,
                        principalTable: "Encounters",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Bills_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Bills_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "CashierShifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    CashierId = table.Column<string>(type: "text", nullable: false),
                    ShiftNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OpenedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ClosedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    OpeningFloat = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    CountedCash = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Variance = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_CashierShifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_CashierShifts_AspNetUsers_CashierId",
                        column: x => x.CashierId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "HrProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    UserId = table.Column<string>(type: "text", nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    HireDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EmploymentEndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EmploymentType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    GradeLevel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Position = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    UnitOrSection = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    GrossMonthlySalary = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: true),
                    PfaPin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NhfNumber = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PayeTin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    BankName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    BankAccountNumber = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    EmergencyContactName = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    EmergencyContactPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HrProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_HrProfiles_AspNetUsers_UserId",
                        column: x => x.UserId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "LeaveRequests",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    StaffId = table.Column<string>(type: "text", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: false),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Days = table.Column<int>(type: "integer", nullable: false),
                    Reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    DecisionNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DecidedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DecidedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_LeaveRequests", x => x.Id);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_AspNetUsers_DecidedById",
                        column: x => x.DecidedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_LeaveRequests_AspNetUsers_StaffId",
                        column: x => x.StaffId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "RosterShifts",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    StaffId = table.Column<string>(type: "text", nullable: false),
                    Date = table.Column<DateOnly>(type: "date", nullable: false),
                    ShiftType = table.Column<int>(type: "integer", nullable: false),
                    WardId = table.Column<int>(type: "integer", nullable: true),
                    ClinicId = table.Column<int>(type: "integer", nullable: true),
                    Assignment = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_RosterShifts", x => x.Id);
                    table.ForeignKey(
                        name: "FK_RosterShifts_AspNetUsers_StaffId",
                        column: x => x.StaffId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Theatres",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Specialty = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    IsEmergencyTheatre = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Theatres", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Theatres_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BillItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BillId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    ServiceCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Quantity = table.Column<int>(type: "integer", nullable: false),
                    UnitPrice = table.Column<decimal>(type: "numeric(12,2)", precision: 12, scale: 2, nullable: false),
                    LineTotal = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    LineDiscount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    LineNet = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    LabOrderId = table.Column<int>(type: "integer", nullable: true),
                    ImagingOrderId = table.Column<int>(type: "integer", nullable: true),
                    PrescriptionItemId = table.Column<int>(type: "integer", nullable: true),
                    ProcedureOrderId = table.Column<int>(type: "integer", nullable: true),
                    DispenseItemId = table.Column<int>(type: "integer", nullable: true),
                    TheatreSessionId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BillItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BillItems_Bills_BillId",
                        column: x => x.BillId,
                        principalTable: "Bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Payments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    BillId = table.Column<int>(type: "integer", nullable: false),
                    CashierShiftId = table.Column<int>(type: "integer", nullable: true),
                    ReceiptNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Method = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    Amount = table.Column<decimal>(type: "numeric(14,2)", precision: 14, scale: 2, nullable: false),
                    Reference = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CashierId = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Payments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Payments_AspNetUsers_CashierId",
                        column: x => x.CashierId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Payments_Bills_BillId",
                        column: x => x.BillId,
                        principalTable: "Bills",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Payments_CashierShifts_CashierShiftId",
                        column: x => x.CashierShiftId,
                        principalTable: "CashierShifts",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "TheatreSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    TheatreId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    LeadSurgeonId = table.Column<string>(type: "text", nullable: false),
                    AnaesthetistId = table.Column<string>(type: "text", nullable: true),
                    ScrubNurseId = table.Column<string>(type: "text", nullable: true),
                    ProcedureName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    CptCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Urgency = table.Column<int>(type: "integer", nullable: false),
                    Anaesthesia = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ScheduledStartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EstimatedMinutes = table.Column<int>(type: "integer", nullable: false),
                    PreOpAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    KnifeOnSkinAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    KnifeOffSkinAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RecoveryAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Indication = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PreOpAssessment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    OperativeNote = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    PostOpInstructions = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    EstimatedBloodLossMl = table.Column<int>(type: "integer", nullable: true),
                    CrystalloidGivenMl = table.Column<int>(type: "integer", nullable: true),
                    ImplantsUsed = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Complications = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AsaScore = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    CreatedById = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TheatreSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TheatreSessions_AspNetUsers_AnaesthetistId",
                        column: x => x.AnaesthetistId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TheatreSessions_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TheatreSessions_AspNetUsers_LeadSurgeonId",
                        column: x => x.LeadSurgeonId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TheatreSessions_AspNetUsers_ScrubNurseId",
                        column: x => x.ScrubNurseId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TheatreSessions_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_TheatreSessions_Theatres_TheatreId",
                        column: x => x.TheatreId,
                        principalTable: "Theatres",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "TheatreChecklistItems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TheatreSessionId = table.Column<int>(type: "integer", nullable: false),
                    Phase = table.Column<int>(type: "integer", nullable: false),
                    Question = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    IsConfirmed = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    ConfirmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ConfirmedById = table.Column<string>(type: "text", nullable: true),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TheatreChecklistItems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TheatreChecklistItems_AspNetUsers_ConfirmedById",
                        column: x => x.ConfirmedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TheatreChecklistItems_TheatreSessions_TheatreSessionId",
                        column: x => x.TheatreSessionId,
                        principalTable: "TheatreSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "TheatreSessionEvents",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    TheatreSessionId = table.Column<int>(type: "integer", nullable: false),
                    Kind = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    Details = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    AtUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_TheatreSessionEvents", x => x.Id);
                    table.ForeignKey(
                        name: "FK_TheatreSessionEvents_AspNetUsers_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_TheatreSessionEvents_TheatreSessions_TheatreSessionId",
                        column: x => x.TheatreSessionId,
                        principalTable: "TheatreSessions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_BillItems_BillId",
                table: "BillItems",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_BillNumber",
                table: "Bills",
                column: "BillNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Bills_CreatedById",
                table: "Bills",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_EncounterId",
                table: "Bills",
                column: "EncounterId");

            migrationBuilder.CreateIndex(
                name: "IX_Bills_FacilityId_Status",
                table: "Bills",
                columns: new[] { "FacilityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Bills_PatientId",
                table: "Bills",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_CashierId",
                table: "CashierShifts",
                column: "CashierId");

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_FacilityId_Status",
                table: "CashierShifts",
                columns: new[] { "FacilityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_CashierShifts_ShiftNumber",
                table: "CashierShifts",
                column: "ShiftNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_HrProfiles_UserId",
                table: "HrProfiles",
                column: "UserId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_DecidedById",
                table: "LeaveRequests",
                column: "DecidedById");

            migrationBuilder.CreateIndex(
                name: "IX_LeaveRequests_StaffId_Status",
                table: "LeaveRequests",
                columns: new[] { "StaffId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_Payments_BillId",
                table: "Payments",
                column: "BillId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CashierId",
                table: "Payments",
                column: "CashierId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_CashierShiftId",
                table: "Payments",
                column: "CashierShiftId");

            migrationBuilder.CreateIndex(
                name: "IX_Payments_ReceiptNumber",
                table: "Payments",
                column: "ReceiptNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_RosterShifts_FacilityId_Date",
                table: "RosterShifts",
                columns: new[] { "FacilityId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_RosterShifts_StaffId_Date",
                table: "RosterShifts",
                columns: new[] { "StaffId", "Date" });

            migrationBuilder.CreateIndex(
                name: "IX_TheatreChecklistItems_ConfirmedById",
                table: "TheatreChecklistItems",
                column: "ConfirmedById");

            migrationBuilder.CreateIndex(
                name: "IX_TheatreChecklistItems_TheatreSessionId_Phase_SortOrder",
                table: "TheatreChecklistItems",
                columns: new[] { "TheatreSessionId", "Phase", "SortOrder" });

            migrationBuilder.CreateIndex(
                name: "IX_Theatres_FacilityId_Code",
                table: "Theatres",
                columns: new[] { "FacilityId", "Code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_TheatreSessionEvents_RecordedById",
                table: "TheatreSessionEvents",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_TheatreSessionEvents_TheatreSessionId",
                table: "TheatreSessionEvents",
                column: "TheatreSessionId");

            migrationBuilder.CreateIndex(
                name: "IX_TheatreSessions_AnaesthetistId",
                table: "TheatreSessions",
                column: "AnaesthetistId");

            migrationBuilder.CreateIndex(
                name: "IX_TheatreSessions_CreatedById",
                table: "TheatreSessions",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_TheatreSessions_FacilityId_ScheduledStartUtc",
                table: "TheatreSessions",
                columns: new[] { "FacilityId", "ScheduledStartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_TheatreSessions_LeadSurgeonId",
                table: "TheatreSessions",
                column: "LeadSurgeonId");

            migrationBuilder.CreateIndex(
                name: "IX_TheatreSessions_PatientId",
                table: "TheatreSessions",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_TheatreSessions_ScrubNurseId",
                table: "TheatreSessions",
                column: "ScrubNurseId");

            migrationBuilder.CreateIndex(
                name: "IX_TheatreSessions_Status",
                table: "TheatreSessions",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_TheatreSessions_TheatreId",
                table: "TheatreSessions",
                column: "TheatreId");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "BillItems");

            migrationBuilder.DropTable(
                name: "HrProfiles");

            migrationBuilder.DropTable(
                name: "LeaveRequests");

            migrationBuilder.DropTable(
                name: "Payments");

            migrationBuilder.DropTable(
                name: "RosterShifts");

            migrationBuilder.DropTable(
                name: "TheatreChecklistItems");

            migrationBuilder.DropTable(
                name: "TheatreSessionEvents");

            migrationBuilder.DropTable(
                name: "Bills");

            migrationBuilder.DropTable(
                name: "CashierShifts");

            migrationBuilder.DropTable(
                name: "TheatreSessions");

            migrationBuilder.DropTable(
                name: "Theatres");
        }
    }
}
