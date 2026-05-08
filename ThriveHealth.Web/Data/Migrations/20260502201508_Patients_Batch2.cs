using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Patients_Batch2 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AlterDatabase()
                .Annotation("Npgsql:PostgresExtension:fuzzystrmatch", ",,");

            migrationBuilder.CreateTable(
                name: "HospitalNumberCounters",
                columns: table => new
                {
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    Year = table.Column<int>(type: "integer", nullable: false),
                    LastSequence = table.Column<int>(type: "integer", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_HospitalNumberCounters", x => new { x.FacilityId, x.Year });
                });

            migrationBuilder.CreateTable(
                name: "Patients",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    HospitalNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    FirstName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    LastName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    MiddleName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Title = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    IsDateOfBirthEstimated = table.Column<bool>(type: "boolean", nullable: false),
                    Sex = table.Column<int>(type: "integer", nullable: false),
                    Gender = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    AlternatePhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    WhatsAppOptIn = table.Column<bool>(type: "boolean", nullable: false),
                    Email = table.Column<string>(type: "character varying(150)", maxLength: 150, nullable: true),
                    StreetAddress = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Lga = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    State = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Postcode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    MaritalStatus = table.Column<int>(type: "integer", nullable: true),
                    Occupation = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Religion = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    StateOfOrigin = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    EthnicGroup = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    PreferredLanguage = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Nin = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    NinVerified = table.Column<bool>(type: "boolean", nullable: false),
                    NinVerifiedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Bvn = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DriversLicense = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    VotersCard = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Passport = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    PhotoPath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsDeceased = table.Column<bool>(type: "boolean", nullable: false),
                    DeceasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    IsMergedAlias = table.Column<bool>(type: "boolean", nullable: false),
                    MergedIntoPatientId = table.Column<int>(type: "integer", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    UpdatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    CreatedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Patients", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Patients_AspNetUsers_CreatedById",
                        column: x => x.CreatedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Patients_Facilities_FacilityId",
                        column: x => x.FacilityId,
                        principalTable: "Facilities",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_Patients_Patients_MergedIntoPatientId",
                        column: x => x.MergedIntoPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "Allergies",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    Category = table.Column<int>(type: "integer", nullable: false),
                    Substance = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Reaction = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    Severity = table.Column<int>(type: "integer", nullable: false),
                    OnsetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    RecordedById = table.Column<string>(type: "text", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Allergies", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Allergies_AspNetUsers_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Allergies_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Medications",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    DrugName = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Dose = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    Route = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Frequency = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    StartDate = table.Column<DateOnly>(type: "date", nullable: true),
                    EndDate = table.Column<DateOnly>(type: "date", nullable: true),
                    IsCurrent = table.Column<bool>(type: "boolean", nullable: false),
                    Source = table.Column<int>(type: "integer", nullable: false),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RecordedById = table.Column<string>(type: "text", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Medications", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Medications_AspNetUsers_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Medications_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "MpiMergeAudits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    MasterPatientId = table.Column<int>(type: "integer", nullable: false),
                    MergedFromPatientId = table.Column<int>(type: "integer", nullable: false),
                    MergedById = table.Column<string>(type: "text", nullable: true),
                    MergedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReversedById = table.Column<string>(type: "text", nullable: true),
                    ReversedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    Reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MpiMergeAudits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MpiMergeAudits_AspNetUsers_MergedById",
                        column: x => x.MergedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MpiMergeAudits_Patients_MasterPatientId",
                        column: x => x.MasterPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "MpiPotentialMatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientAId = table.Column<int>(type: "integer", nullable: false),
                    PatientBId = table.Column<int>(type: "integer", nullable: false),
                    ConfidenceScore = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ResolvedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ResolvedById = table.Column<string>(type: "text", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MpiPotentialMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MpiPotentialMatches_AspNetUsers_ResolvedById",
                        column: x => x.ResolvedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MpiPotentialMatches_Patients_PatientAId",
                        column: x => x.PatientAId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "FK_MpiPotentialMatches_Patients_PatientBId",
                        column: x => x.PatientBId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "PatientDocuments",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    Title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    DocumentType = table.Column<int>(type: "integer", nullable: false),
                    FileName = table.Column<string>(type: "character varying(255)", maxLength: 255, nullable: false),
                    ContentType = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    ByteSize = table.Column<long>(type: "bigint", nullable: false),
                    FilePath = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    UploadedById = table.Column<string>(type: "text", nullable: true),
                    UploadedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientDocuments", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientDocuments_AspNetUsers_UploadedById",
                        column: x => x.UploadedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_PatientDocuments_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientNextOfKin",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    Relationship = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientNextOfKin", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientNextOfKin_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "PatientPayers",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    Type = table.Column<int>(type: "integer", nullable: false),
                    Name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    PlanName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    MembershipNumber = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    AuthorizationCode = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    IsPrimary = table.Column<bool>(type: "boolean", nullable: false),
                    IsActive = table.Column<bool>(type: "boolean", nullable: false),
                    ValidFrom = table.Column<DateOnly>(type: "date", nullable: true),
                    ValidTo = table.Column<DateOnly>(type: "date", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_PatientPayers", x => x.Id);
                    table.ForeignKey(
                        name: "FK_PatientPayers_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Problems",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    Description = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    IcdCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    SnomedCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    OnsetDate = table.Column<DateOnly>(type: "date", nullable: true),
                    ResolutionDate = table.Column<DateOnly>(type: "date", nullable: true),
                    Notes = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    RecordedById = table.Column<string>(type: "text", nullable: true),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Problems", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Problems_AspNetUsers_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Problems_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "Vitals",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    RecordedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    RecordedById = table.Column<string>(type: "text", nullable: true),
                    SystolicBp = table.Column<int>(type: "integer", nullable: true),
                    DiastolicBp = table.Column<int>(type: "integer", nullable: true),
                    HeartRate = table.Column<int>(type: "integer", nullable: true),
                    RespiratoryRate = table.Column<int>(type: "integer", nullable: true),
                    TemperatureCelsius = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    SpO2 = table.Column<int>(type: "integer", nullable: true),
                    WeightKg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    HeightCm = table.Column<decimal>(type: "numeric(5,1)", precision: 5, scale: 1, nullable: true),
                    PainScore = table.Column<int>(type: "integer", nullable: true),
                    GcsTotal = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_Vitals", x => x.Id);
                    table.ForeignKey(
                        name: "FK_Vitals_AspNetUsers_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_Vitals_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "IX_Allergies_PatientId",
                table: "Allergies",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Allergies_RecordedById",
                table: "Allergies",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_Medications_PatientId",
                table: "Medications",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Medications_RecordedById",
                table: "Medications",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_MpiMergeAudits_MasterPatientId",
                table: "MpiMergeAudits",
                column: "MasterPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_MpiMergeAudits_MergedById",
                table: "MpiMergeAudits",
                column: "MergedById");

            migrationBuilder.CreateIndex(
                name: "IX_MpiPotentialMatches_PatientAId",
                table: "MpiPotentialMatches",
                column: "PatientAId");

            migrationBuilder.CreateIndex(
                name: "IX_MpiPotentialMatches_PatientBId",
                table: "MpiPotentialMatches",
                column: "PatientBId");

            migrationBuilder.CreateIndex(
                name: "IX_MpiPotentialMatches_ResolvedById",
                table: "MpiPotentialMatches",
                column: "ResolvedById");

            migrationBuilder.CreateIndex(
                name: "IX_MpiPotentialMatches_Status",
                table: "MpiPotentialMatches",
                column: "Status");

            migrationBuilder.CreateIndex(
                name: "IX_PatientDocuments_PatientId",
                table: "PatientDocuments",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientDocuments_UploadedById",
                table: "PatientDocuments",
                column: "UploadedById");

            migrationBuilder.CreateIndex(
                name: "IX_PatientNextOfKin_PatientId",
                table: "PatientNextOfKin",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_PatientPayers_PatientId_Type",
                table: "PatientPayers",
                columns: new[] { "PatientId", "Type" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_CreatedById",
                table: "Patients",
                column: "CreatedById");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_FacilityId_HospitalNumber",
                table: "Patients",
                columns: new[] { "FacilityId", "HospitalNumber" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_Patients_LastName_FirstName",
                table: "Patients",
                columns: new[] { "LastName", "FirstName" });

            migrationBuilder.CreateIndex(
                name: "IX_Patients_MergedIntoPatientId",
                table: "Patients",
                column: "MergedIntoPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_Nin",
                table: "Patients",
                column: "Nin");

            migrationBuilder.CreateIndex(
                name: "IX_Patients_Phone",
                table: "Patients",
                column: "Phone");

            migrationBuilder.CreateIndex(
                name: "IX_Problems_PatientId",
                table: "Problems",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Problems_RecordedById",
                table: "Problems",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_Vitals_PatientId",
                table: "Vitals",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_Vitals_RecordedById",
                table: "Vitals",
                column: "RecordedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "Allergies");

            migrationBuilder.DropTable(
                name: "HospitalNumberCounters");

            migrationBuilder.DropTable(
                name: "Medications");

            migrationBuilder.DropTable(
                name: "MpiMergeAudits");

            migrationBuilder.DropTable(
                name: "MpiPotentialMatches");

            migrationBuilder.DropTable(
                name: "PatientDocuments");

            migrationBuilder.DropTable(
                name: "PatientNextOfKin");

            migrationBuilder.DropTable(
                name: "PatientPayers");

            migrationBuilder.DropTable(
                name: "Problems");

            migrationBuilder.DropTable(
                name: "Vitals");

            migrationBuilder.DropTable(
                name: "Patients");

            migrationBuilder.AlterDatabase()
                .OldAnnotation("Npgsql:PostgresExtension:fuzzystrmatch", ",,");
        }
    }
}
