using System;
using Microsoft.EntityFrameworkCore.Migrations;
using Npgsql.EntityFrameworkCore.PostgreSQL.Metadata;

#nullable disable

namespace ThriveHealth.Web.Data.Migrations
{
    /// <inheritdoc />
    public partial class Critical_BloodBank_Mortuary_Allied_Batch13 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "AlliedSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    SessionNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    ServiceLine = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ScheduledUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    StartedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    CompletedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ChiefComplaint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Examination = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Assessment = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    TreatmentGiven = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    Plan = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    Modality = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ToothChart = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DentalProcedureCode = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    RightEyeAcuity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    LeftEyeAcuity = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    RightEyeRefraction = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    LeftEyeRefraction = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    SessionsCompleted = table.Column<int>(type: "integer", nullable: true),
                    SessionsPlanned = table.Column<int>(type: "integer", nullable: true),
                    PhysioModalitiesUsed = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    ProviderId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_AlliedSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_AlliedSessions_AspNetUsers_ProviderId",
                        column: x => x.ProviderId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_AlliedSessions_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BloodCrossMatches",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    CrossMatchNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    PatientBloodGroup = table.Column<int>(type: "integer", nullable: false),
                    PatientRhesusPositive = table.Column<bool>(type: "boolean", nullable: false),
                    Component = table.Column<int>(type: "integer", nullable: false),
                    UnitsRequested = table.Column<int>(type: "integer", nullable: false),
                    RequiredBy = table.Column<DateOnly>(type: "date", nullable: false),
                    Indication = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    IssuedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    RequestedById = table.Column<string>(type: "text", nullable: true),
                    CompatibilityCheckedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodCrossMatches", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BloodCrossMatches_AspNetUsers_CompatibilityCheckedById",
                        column: x => x.CompatibilityCheckedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BloodCrossMatches_AspNetUsers_RequestedById",
                        column: x => x.RequestedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BloodCrossMatches_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "BloodDonors",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    DonorNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    FullName = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    Sex = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    Phone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    Address = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    BloodGroup = table.Column<int>(type: "integer", nullable: false),
                    RhesusPositive = table.Column<bool>(type: "boolean", nullable: true),
                    DonorType = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    LastDonationDate = table.Column<DateOnly>(type: "date", nullable: true),
                    TotalDonations = table.Column<int>(type: "integer", nullable: false),
                    HivNegative = table.Column<bool>(type: "boolean", nullable: true),
                    HepBNegative = table.Column<bool>(type: "boolean", nullable: true),
                    HepCNegative = table.Column<bool>(type: "boolean", nullable: true),
                    VdrlNegative = table.Column<bool>(type: "boolean", nullable: true),
                    MalariaNegative = table.Column<bool>(type: "boolean", nullable: true),
                    DeferralReason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodDonors", x => x.Id);
                });

            migrationBuilder.CreateTable(
                name: "DialysisSessions",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    AdmissionId = table.Column<int>(type: "integer", nullable: true),
                    PatientId = table.Column<int>(type: "integer", nullable: false),
                    SessionNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    Modality = table.Column<int>(type: "integer", nullable: false),
                    Access = table.Column<int>(type: "integer", nullable: false),
                    StartUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    EndUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    DurationMinutes = table.Column<int>(type: "integer", nullable: false),
                    PreWeightKg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    PostWeightKg = table.Column<decimal>(type: "numeric(6,2)", precision: 6, scale: 2, nullable: true),
                    UfTargetMl = table.Column<int>(type: "integer", nullable: true),
                    UfAchievedMl = table.Column<int>(type: "integer", nullable: true),
                    PreSystolicBp = table.Column<int>(type: "integer", nullable: true),
                    PreDiastolicBp = table.Column<int>(type: "integer", nullable: true),
                    PostSystolicBp = table.Column<int>(type: "integer", nullable: true),
                    PostDiastolicBp = table.Column<int>(type: "integer", nullable: true),
                    BloodFlowMlMin = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: true),
                    DialysateFlowMlMin = table.Column<decimal>(type: "numeric(7,2)", precision: 7, scale: 2, nullable: true),
                    HeparinUnits = table.Column<decimal>(type: "numeric(8,2)", precision: 8, scale: 2, nullable: true),
                    DialyserType = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Complications = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    OperatorId = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_DialysisSessions", x => x.Id);
                    table.ForeignKey(
                        name: "FK_DialysisSessions_Admissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "Admissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_DialysisSessions_AspNetUsers_OperatorId",
                        column: x => x.OperatorId,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "IcuChartEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    AdmissionId = table.Column<int>(type: "integer", nullable: false),
                    RecordedUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    HeartRate = table.Column<int>(type: "integer", nullable: true),
                    SystolicBp = table.Column<int>(type: "integer", nullable: true),
                    DiastolicBp = table.Column<int>(type: "integer", nullable: true),
                    MeanArterialPressure = table.Column<int>(type: "integer", nullable: true),
                    RespiratoryRate = table.Column<int>(type: "integer", nullable: true),
                    SpO2 = table.Column<decimal>(type: "numeric(5,2)", precision: 5, scale: 2, nullable: true),
                    TemperatureC = table.Column<decimal>(type: "numeric(4,1)", precision: 4, scale: 1, nullable: true),
                    GcsEye = table.Column<int>(type: "integer", nullable: true),
                    GcsVerbal = table.Column<int>(type: "integer", nullable: true),
                    GcsMotor = table.Column<int>(type: "integer", nullable: true),
                    PainScore = table.Column<int>(type: "integer", nullable: true),
                    Sedation = table.Column<int>(type: "integer", nullable: true),
                    Pupils = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    UrineOutputMl = table.Column<int>(type: "integer", nullable: true),
                    CrystalloidGivenMl = table.Column<int>(type: "integer", nullable: true),
                    BloodGivenMl = table.Column<int>(type: "integer", nullable: true),
                    VentMode = table.Column<int>(type: "integer", nullable: true),
                    FiO2 = table.Column<decimal>(type: "numeric(4,2)", precision: 4, scale: 2, nullable: true),
                    Peep = table.Column<int>(type: "integer", nullable: true),
                    TidalVolumeMl = table.Column<int>(type: "integer", nullable: true),
                    VentRate = table.Column<int>(type: "integer", nullable: true),
                    PeakInspiratoryPressure = table.Column<int>(type: "integer", nullable: true),
                    Inotropes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    RecordedById = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_IcuChartEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_IcuChartEntries_Admissions_AdmissionId",
                        column: x => x.AdmissionId,
                        principalTable: "Admissions",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.Cascade);
                    table.ForeignKey(
                        name: "FK_IcuChartEntries_AspNetUsers_RecordedById",
                        column: x => x.RecordedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "MortuaryEntries",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    MortuaryNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    CabinetCode = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    PatientId = table.Column<int>(type: "integer", nullable: true),
                    IsUnidentified = table.Column<bool>(type: "boolean", nullable: false),
                    DeceasedName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    Sex = table.Column<string>(type: "character varying(20)", maxLength: 20, nullable: true),
                    DateOfBirth = table.Column<DateOnly>(type: "date", nullable: true),
                    AgeYears = table.Column<int>(type: "integer", nullable: true),
                    Tribe = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    AddressOfOrigin = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    DateOfDeathUtc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    PlaceOfDeath = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    CauseOfDeath = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    Manner = table.Column<int>(type: "integer", nullable: true),
                    Embalmed = table.Column<bool>(type: "boolean", nullable: false),
                    EmbalmedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    EmbalmedBy = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    PostMortemDone = table.Column<bool>(type: "boolean", nullable: false),
                    PostMortemFinding = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    NextOfKinName = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    NextOfKinRelationship = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    NextOfKinPhone = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    NextOfKinId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    ReceivedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ReleasedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    ReleasedTo = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    ReleasedToId = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: true),
                    ReleaseAuthorityRef = table.Column<string>(type: "character varying(60)", maxLength: 60, nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    ReceivedById = table.Column<string>(type: "text", nullable: true),
                    ReleasedById = table.Column<string>(type: "text", nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_MortuaryEntries", x => x.Id);
                    table.ForeignKey(
                        name: "FK_MortuaryEntries_AspNetUsers_ReceivedById",
                        column: x => x.ReceivedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MortuaryEntries_AspNetUsers_ReleasedById",
                        column: x => x.ReleasedById,
                        principalTable: "AspNetUsers",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_MortuaryEntries_Patients_PatientId",
                        column: x => x.PatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "BloodUnits",
                columns: table => new
                {
                    Id = table.Column<int>(type: "integer", nullable: false)
                        .Annotation("Npgsql:ValueGenerationStrategy", NpgsqlValueGenerationStrategy.IdentityByDefaultColumn),
                    FacilityId = table.Column<int>(type: "integer", nullable: false),
                    UnitNumber = table.Column<string>(type: "character varying(40)", maxLength: 40, nullable: false),
                    BloodDonorId = table.Column<int>(type: "integer", nullable: true),
                    Component = table.Column<int>(type: "integer", nullable: false),
                    BloodGroup = table.Column<int>(type: "integer", nullable: false),
                    RhesusPositive = table.Column<bool>(type: "boolean", nullable: false),
                    CollectionDate = table.Column<DateOnly>(type: "date", nullable: false),
                    ExpiryDate = table.Column<DateOnly>(type: "date", nullable: false),
                    VolumeMl = table.Column<int>(type: "integer", nullable: false),
                    Status = table.Column<int>(type: "integer", nullable: false),
                    HivNegative = table.Column<bool>(type: "boolean", nullable: true),
                    HepBNegative = table.Column<bool>(type: "boolean", nullable: true),
                    HepCNegative = table.Column<bool>(type: "boolean", nullable: true),
                    VdrlNegative = table.Column<bool>(type: "boolean", nullable: true),
                    MalariaNegative = table.Column<bool>(type: "boolean", nullable: true),
                    ScreeningComplete = table.Column<bool>(type: "boolean", nullable: false),
                    ReservedForPatientId = table.Column<int>(type: "integer", nullable: true),
                    CrossMatchId = table.Column<int>(type: "integer", nullable: true),
                    Notes = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    CreatedAt = table.Column<DateTime>(type: "timestamp with time zone", nullable: false)
                },
                constraints: table =>
                {
                    table.PrimaryKey("PK_BloodUnits", x => x.Id);
                    table.ForeignKey(
                        name: "FK_BloodUnits_BloodCrossMatches_CrossMatchId",
                        column: x => x.CrossMatchId,
                        principalTable: "BloodCrossMatches",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BloodUnits_BloodDonors_BloodDonorId",
                        column: x => x.BloodDonorId,
                        principalTable: "BloodDonors",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "FK_BloodUnits_Patients_ReservedForPatientId",
                        column: x => x.ReservedForPatientId,
                        principalTable: "Patients",
                        principalColumn: "Id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "IX_AlliedSessions_FacilityId_ScheduledUtc",
                table: "AlliedSessions",
                columns: new[] { "FacilityId", "ScheduledUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_AlliedSessions_FacilityId_ServiceLine_Status",
                table: "AlliedSessions",
                columns: new[] { "FacilityId", "ServiceLine", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_AlliedSessions_PatientId",
                table: "AlliedSessions",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_AlliedSessions_ProviderId",
                table: "AlliedSessions",
                column: "ProviderId");

            migrationBuilder.CreateIndex(
                name: "IX_AlliedSessions_SessionNumber",
                table: "AlliedSessions",
                column: "SessionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BloodCrossMatches_CompatibilityCheckedById",
                table: "BloodCrossMatches",
                column: "CompatibilityCheckedById");

            migrationBuilder.CreateIndex(
                name: "IX_BloodCrossMatches_CrossMatchNumber",
                table: "BloodCrossMatches",
                column: "CrossMatchNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BloodCrossMatches_FacilityId_Status",
                table: "BloodCrossMatches",
                columns: new[] { "FacilityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BloodCrossMatches_PatientId",
                table: "BloodCrossMatches",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodCrossMatches_RequestedById",
                table: "BloodCrossMatches",
                column: "RequestedById");

            migrationBuilder.CreateIndex(
                name: "IX_BloodDonors_DonorNumber",
                table: "BloodDonors",
                column: "DonorNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_BloodDonors_FacilityId_BloodGroup",
                table: "BloodDonors",
                columns: new[] { "FacilityId", "BloodGroup" });

            migrationBuilder.CreateIndex(
                name: "IX_BloodUnits_BloodDonorId",
                table: "BloodUnits",
                column: "BloodDonorId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodUnits_BloodGroup_Component_Status",
                table: "BloodUnits",
                columns: new[] { "BloodGroup", "Component", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BloodUnits_CrossMatchId",
                table: "BloodUnits",
                column: "CrossMatchId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodUnits_FacilityId_Status",
                table: "BloodUnits",
                columns: new[] { "FacilityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_BloodUnits_ReservedForPatientId",
                table: "BloodUnits",
                column: "ReservedForPatientId");

            migrationBuilder.CreateIndex(
                name: "IX_BloodUnits_UnitNumber",
                table: "BloodUnits",
                column: "UnitNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_DialysisSessions_AdmissionId",
                table: "DialysisSessions",
                column: "AdmissionId");

            migrationBuilder.CreateIndex(
                name: "IX_DialysisSessions_FacilityId_StartUtc",
                table: "DialysisSessions",
                columns: new[] { "FacilityId", "StartUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_DialysisSessions_OperatorId",
                table: "DialysisSessions",
                column: "OperatorId");

            migrationBuilder.CreateIndex(
                name: "IX_DialysisSessions_SessionNumber",
                table: "DialysisSessions",
                column: "SessionNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_IcuChartEntries_AdmissionId_RecordedUtc",
                table: "IcuChartEntries",
                columns: new[] { "AdmissionId", "RecordedUtc" });

            migrationBuilder.CreateIndex(
                name: "IX_IcuChartEntries_RecordedById",
                table: "IcuChartEntries",
                column: "RecordedById");

            migrationBuilder.CreateIndex(
                name: "IX_MortuaryEntries_FacilityId_Status",
                table: "MortuaryEntries",
                columns: new[] { "FacilityId", "Status" });

            migrationBuilder.CreateIndex(
                name: "IX_MortuaryEntries_MortuaryNumber",
                table: "MortuaryEntries",
                column: "MortuaryNumber",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "IX_MortuaryEntries_PatientId",
                table: "MortuaryEntries",
                column: "PatientId");

            migrationBuilder.CreateIndex(
                name: "IX_MortuaryEntries_ReceivedById",
                table: "MortuaryEntries",
                column: "ReceivedById");

            migrationBuilder.CreateIndex(
                name: "IX_MortuaryEntries_ReleasedById",
                table: "MortuaryEntries",
                column: "ReleasedById");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "AlliedSessions");

            migrationBuilder.DropTable(
                name: "BloodUnits");

            migrationBuilder.DropTable(
                name: "DialysisSessions");

            migrationBuilder.DropTable(
                name: "IcuChartEntries");

            migrationBuilder.DropTable(
                name: "MortuaryEntries");

            migrationBuilder.DropTable(
                name: "BloodCrossMatches");

            migrationBuilder.DropTable(
                name: "BloodDonors");
        }
    }
}
