using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Maternity_Paeds_Immunization_Batch12 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AnteNatalRecords",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    AncNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BookingDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Lmp = table.Column<DateOnly>(type: "date", nullable: true),
                    Edd = table.Column<DateOnly>(type: "date", nullable: true),
                    Gravida = table.Column<int>(type: "integer", nullable: false),
                    Para = table.Column<int>(type: "integer", nullable: false),
                    Abortions = table.Column<int>(type: "integer", nullable: false),
                    LivingChildren = table.Column<int>(type: "integer", nullable: false),
                    BloodGroup = table.Column<int>(type: "integer", nullable: false),
                    RhesusPositive = table.Column<bool>(type: "boolean", nullable: true),
                    HeightCm = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    BookingWeightKg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    HaemoglobinGdl = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    HivStatus = table.Column<int>(type: "integer", nullable: false),
                    VdrlReactive = table.Column<bool>(type: "boolean", nullable: true),
                    HepBPositive = table.Column<bool>(type: "boolean", nullable: true),
                    SicklingPositive = table.Column<bool>(type: "boolean", nullable: true),
                    RiskFactors = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    PreviousObstetricHistory = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    MedicalHistory = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    StatusNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedById = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnteNatalRecords", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnteNatalRecords_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AnteNatalRecords_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "ChildProfiles",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    MotherPatientId = table.Column<int>(type: "integer", nullable: true),
                    FatherPatientId = table.Column<int>(type: "integer", nullable: true),
                    BirthWeightG = table.Column<int>(type: "integer", nullable: true),
                    BirthLengthCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    BirthHeadCircCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    GestationalAgeAtBirthWeeks = table.Column<int>(type: "integer", nullable: true),
                    CurrentFeeding = table.Column<int>(type: "integer", nullable: false),
                    KnownAllergies = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ChildProfiles", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ChildProfiles_Patients_FatherPatientId",
                        column: x => x.FatherPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChildProfiles_Patients_MotherPatientId",
                        column: x => x.MotherPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ChildProfiles_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vaccines",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    Code = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    Name = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: false),
                    Description = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Route = table.Column<int>(type: "integer", nullable: false),
                    Site = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vaccines", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "AnteNatalVisits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnteNatalRecordId = table.Column<int>(type: "integer", nullable: false),
                    VisitDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VisitNumber = table.Column<int>(type: "integer", nullable: false),
                    GestationalAgeWeeks = table.Column<int>(type: "integer", nullable: true),
                    WeightKg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    SystolicBp = table.Column<int>(type: "integer", nullable: true),
                    DiastolicBp = table.Column<int>(type: "integer", nullable: true),
                    FundalHeightCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    FetalHeartRate = table.Column<int>(type: "integer", nullable: true),
                    Presentation = table.Column<int>(type: "integer", nullable: true),
                    Lie = table.Column<int>(type: "integer", nullable: true),
                    UrineProtein = table.Column<bool>(type: "boolean", nullable: true),
                    UrineSugar = table.Column<bool>(type: "boolean", nullable: true),
                    Oedema = table.Column<bool>(type: "boolean", nullable: true),
                    FetalMovements = table.Column<bool>(type: "boolean", nullable: true),
                    Complaints = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Plan = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RecordedById = table.Column<string>(type: "text", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AnteNatalVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AnteNatalVisits_AnteNatalRecords_AnteNatalRecordId",
                        column: x => x.AnteNatalRecordId,
                        principalTable: "AnteNatalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_AnteNatalVisits_AspNetUsers_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "Deliveries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    AnteNatalRecordId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    LabourOnsetUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    DeliveryUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    LabourMinutes = table.Column<int>(type: "integer", nullable: false),
                    Mode = table.Column<int>(type: "integer", nullable: false),
                    Outcome = table.Column<int>(type: "integer", nullable: false),
                    GestationAtDeliveryWeeks = table.Column<int>(type: "integer", nullable: false),
                    EpisiotomyPerformed = table.Column<bool>(type: "boolean", nullable: false),
                    PerinealTear = table.Column<string>(type: "text", nullable: true),
                    EstimatedBloodLossMl = table.Column<int>(type: "integer", nullable: true),
                    ActiveMgmtThirdStage = table.Column<bool>(type: "boolean", nullable: false),
                    OxytocinGiven = table.Column<bool>(type: "boolean", nullable: false),
                    Complications = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AccoucheurId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Deliveries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Deliveries_AnteNatalRecords_AnteNatalRecordId",
                        column: x => x.AnteNatalRecordId,
                        principalTable: "AnteNatalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Deliveries_AspNetUsers_AccoucheurId",
                        column: x => x.AccoucheurId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Deliveries_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PostnatalVisits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    AnteNatalRecordId = table.Column<int>(type: "integer", nullable: false),
                    VisitDate = table.Column<DateOnly>(type: "date", nullable: false),
                    Day = table.Column<int>(type: "integer", nullable: false),
                    MotherSystolicBp = table.Column<int>(type: "integer", nullable: true),
                    MotherDiastolicBp = table.Column<int>(type: "integer", nullable: true),
                    MotherTemperatureC = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    Lochia = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    FundalInvolution = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    BabyWeightKg = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    BabyJaundice = table.Column<bool>(type: "boolean", nullable: false),
                    BabyBreastfeeding = table.Column<bool>(type: "boolean", nullable: false),
                    CordHealthy = table.Column<bool>(type: "boolean", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RecordedById = table.Column<string>(type: "text", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PostnatalVisits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PostnatalVisits_AnteNatalRecords_AnteNatalRecordId",
                        column: x => x.AnteNatalRecordId,
                        principalTable: "AnteNatalRecords",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_PostnatalVisits_AspNetUsers_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "GrowthMeasurements",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    DateOfMeasurement = table.Column<DateOnly>(type: "date", nullable: false),
                    AgeMonths = table.Column<int>(type: "integer", nullable: false),
                    WeightKg = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    HeightCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    HeadCircumferenceCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    MuacCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    BmiKgM2 = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    NutritionalStatus = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    DevelopmentalMilestoneNote = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RecordedById = table.Column<string>(type: "text", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ChildProfileId = table.Column<int>(type: "integer", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_GrowthMeasurements", x => x.Id);
                    table.ForeignKey(
                        name: "FK_GrowthMeasurements_AspNetUsers_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_GrowthMeasurements_ChildProfiles_ChildProfileId",
                        column: x => x.ChildProfileId,
                        principalTable: "ChildProfiles",
                        principalColumn: "Id");
                    table.ForeignKey(
                        name: "FK_GrowthMeasurements_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "VaccineSchedules",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    VaccineId = table.Column<int>(type: "integer", nullable: false),
                    DoseLabel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    RecommendedAgeWeeks = table.Column<int>(type: "integer", nullable: false),
                    SortOrder = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_VaccineSchedules", x => x.Id);
                    table.ForeignKey(
                        name: "FK_VaccineSchedules_Vaccines_VaccineId",
                        column: x => x.VaccineId,
                        principalTable: "Vaccines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Newborns",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    DeliveryId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: true),
                    Sex = table.Column<int>(type: "integer", nullable: false),
                    BirthWeightG = table.Column<int>(type: "integer", nullable: true),
                    LengthCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    HeadCircumferenceCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    Apgar1Min = table.Column<int>(type: "integer", nullable: true),
                    Apgar5Min = table.Column<int>(type: "integer", nullable: true),
                    Apgar10Min = table.Column<int>(type: "integer", nullable: true),
                    ResuscitationRequired = table.Column<bool>(type: "boolean", nullable: false),
                    BreastfedWithin1Hr = table.Column<bool>(type: "boolean", nullable: false),
                    VitaminKGiven = table.Column<bool>(type: "boolean", nullable: false),
                    BcgGivenAtBirth = table.Column<bool>(type: "boolean", nullable: false),
                    OpvGivenAtBirth = table.Column<bool>(type: "boolean", nullable: false),
                    HepBGivenAtBirth = table.Column<bool>(type: "boolean", nullable: false),
                    Anomalies = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Newborns", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Newborns_Deliveries_DeliveryId",
                        column: x => x.DeliveryId,
                        principalTable: "Deliveries",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_Newborns_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "ImmunizationDoses",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    VaccineId = table.Column<int>(type: "integer", nullable: false),
                    VaccineScheduleId = table.Column<int>(type: "integer", nullable: true),
                    DoseLabel = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: false),
                    DueDate = table.Column<DateOnly>(type: "date", nullable: false),
                    AdministeredAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    BatchNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Site = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    AdverseEventReported = table.Column<bool>(type: "boolean", nullable: false),
                    AdverseEventNotes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    AdministeredById = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_ImmunizationDoses", x => x.Id);
                    table.ForeignKey(
                        name: "FK_ImmunizationDoses_AspNetUsers_AdministeredById",
                        column: x => x.AdministeredById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ImmunizationDoses_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_ImmunizationDoses_VaccineSchedules_VaccineScheduleId",
                        column: x => x.VaccineScheduleId,
                        principalTable: "VaccineSchedules",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_ImmunizationDoses_Vaccines_VaccineId",
                        column: x => x.VaccineId,
                        principalTable: "Vaccines",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AnteNatalRecords_AncNumber",
                table: "AnteNatalRecords",
                column: "AncNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_AnteNatalRecords_CreatedById",
                table: "AnteNatalRecords",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_AnteNatalRecords_FacilityId_Status",
                table: "AnteNatalRecords",
                columns: new[] { "FacilityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AnteNatalRecords_PatientId",
                table: "AnteNatalRecords",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_AnteNatalVisits_AnteNatalRecordId_VisitDate",
                table: "AnteNatalVisits",
                columns: new[] { "AnteNatalRecordId", "VisitDate" });

            migrationBuilder.CreateIndex(
                name: "IX_AnteNatalVisits_RecordedById",
                table: "AnteNatalVisits",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_ChildProfiles_FatherPatientId",
                table: "ChildProfiles",
                column: "FatherPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildProfiles_MotherPatientId",
                table: "ChildProfiles",
                column: "MotherPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_ChildProfiles_PatientId",
                table: "ChildProfiles",
                column: "PatientId",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_AccoucheurId",
                table: "Deliveries",
                column: "AccoucheurId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_AnteNatalRecordId",
                table: "Deliveries",
                column: "AnteNatalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_FacilityId_DeliveryUtc",
                table: "Deliveries",
                columns: new[] { "FacilityId", "DeliveryUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_Deliveries_PatientId",
                table: "Deliveries",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_GrowthMeasurements_ChildProfileId",
                table: "GrowthMeasurements",
                column: "ChildProfileId");

            migrationBuilder.CreateIndex(
                name: "IX_GrowthMeasurements_PatientId_DateOfMeasurement",
                table: "GrowthMeasurements",
                columns: new[] { "PatientId", "DateOfMeasurement" });

            migrationBuilder.CreateIndex(
                name: "IX_GrowthMeasurements_RecordedById",
                table: "GrowthMeasurements",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_ImmunizationDoses_AdministeredById",
                table: "ImmunizationDoses",
                column: "AdministeredById");

            migrationBuilder.CreateIndex(
                name: "IX_ImmunizationDoses_FacilityId_Status_DueDate",
                table: "ImmunizationDoses",
                columns: new[] { "FacilityId", "Status", "DueDate" });

            migrationBuilder.CreateIndex(
                name: "IX_ImmunizationDoses_PatientId_VaccineId_DoseLabel",
                table: "ImmunizationDoses",
                columns: new[] { "PatientId", "VaccineId", "DoseLabel" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_ImmunizationDoses_VaccineId",
                table: "ImmunizationDoses",
                column: "VaccineId");

            migrationBuilder.CreateIndex(
                name: "IX_ImmunizationDoses_VaccineScheduleId",
                table: "ImmunizationDoses",
                column: "VaccineScheduleId");

            migrationBuilder.CreateIndex(
                name: "IX_Newborns_DeliveryId",
                table: "Newborns",
                column: "DeliveryId");

            migrationBuilder.CreateIndex(
                name: "IX_Newborns_PatientId",
                table: "Newborns",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PostnatalVisits_AnteNatalRecordId",
                table: "PostnatalVisits",
                column: "AnteNatalRecordId");

            migrationBuilder.CreateIndex(
                name: "IX_PostnatalVisits_RecordedById",
                table: "PostnatalVisits",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_Vaccines_Code",
                table: "Vaccines",
                column: "Code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_VaccineSchedules_VaccineId_DoseLabel",
                table: "VaccineSchedules",
                columns: new[] { "VaccineId", "DoseLabel" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AnteNatalVisits");

            migrationBuilder.DropTable(
                name: "GrowthMeasurements");

            migrationBuilder.DropTable(
                name: "ImmunizationDoses");

            migrationBuilder.DropTable(
                name: "Newborns");

            migrationBuilder.DropTable(
                name: "PostnatalVisits");

            migrationBuilder.DropTable(
                name: "ChildProfiles");

            migrationBuilder.DropTable(
                name: "VaccineSchedules");

            migrationBuilder.DropTable(
                name: "Deliveries");

            migrationBuilder.DropTable(
                name: "Vaccines");

            migrationBuilder.DropTable(
                name: "AnteNatalRecords");
        }
    }
}
