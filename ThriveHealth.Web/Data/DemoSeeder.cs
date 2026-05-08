using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Diagnostics;
using ThriveHealth.Web.Models.Hr;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Models.Patients;
using ThriveHealth.Web.Models.Insurance;
using ThriveHealth.Web.Models.Pharmacy;
using ThriveHealth.Web.Models.Telemedicine;

namespace ThriveHealth.Web.Data;

/// <summary>
/// Seeds realistic demo operational data: ~40 patients with end-to-end journeys touching every
/// major workflow. Idempotent — only runs if no patients exist for the demo facility.
/// Gated by config flag <c>Seed:Demo</c> (default true in dev). Run order: after <see cref="DbSeeder"/>.
/// </summary>
public static class DemoSeeder
{
    public static async Task SeedAsync(IServiceProvider services)
    {
        var db = services.GetRequiredService<ApplicationDbContext>();
        var um = services.GetRequiredService<UserManager<ApplicationUser>>();
        var config = services.GetRequiredService<IConfiguration>();

        if (!(config.GetValue<bool?>("Seed:Demo") ?? true)) return;

        var fac = await db.Facilities.AsNoTracking().FirstOrDefaultAsync();
        if (fac is null) return;
        var fid = fac.Id;

        // Allow the telemed demo set to be refreshed on existing demo DBs without rewiping the rest.
        // Runs whether or not the main idempotency check below skips the full seed.
        await TopUpTelemedAsync(db, um, fid);

        // Idempotency marker: a recognisable demo patient. Pre-existing ad-hoc test patients should
        // not block the demo seed.
        if (await db.Patients.AnyAsync(p => p.FacilityId == fid && p.FirstName == "Adekunle" && p.LastName == "Adesanya")) return;

        var rnd = new Random(42);
        var now = DateTime.UtcNow;

        // ---- Cache reference data ----
        var ctx = await BuildContextAsync(db, um, fid);

        // ---- Phase 1: patients + vitals + allergies + problems + meds + NOK + payers ----
        var patients = await CreatePatientsAsync(db, fid, ctx, rnd, now);

        // ---- Phase 2: clinical journeys (encounters, labs, imaging, prescriptions) ----
        await SeedEncountersAsync(db, fid, patients, ctx, rnd, now);

        // ---- Phase 3: finance (bills + payments + claims + cashier shift) ----
        await SeedFinanceAsync(db, fid, patients, ctx, rnd, now);

        // ---- Phase 4: scheduling (appointments + queue + roster + leave) ----
        await SeedSchedulingAsync(db, fid, patients, ctx, rnd, now);

        // ---- Phase 5: inpatient (admissions + MAR + fluids + nursing notes + ward rounds) ----
        await SeedInpatientAsync(db, fid, patients, ctx, rnd, now);

        // ---- Phase 6: theatre + ICU + dialysis ----
        await SeedTheatreIcuAsync(db, fid, patients, ctx, rnd, now);

        // ---- Phase 7: ANC + delivery + paeds + immunization ----
        await SeedMaternityPaedsAsync(db, fid, patients, ctx, rnd, now);

        // ---- Phase 8: blood bank + mortuary + allied + telemed ----
        await SeedSpecialtyAsync(db, fid, patients, ctx, rnd, now);

        // ---- Phase 9: IDSR cases + NHMIS report + portal accounts ----
        await SeedReportingPortalAsync(db, fid, patients, ctx, rnd, now);
    }

    // ====================================================================================
    // Context: cache reference data + staff lookups
    // ====================================================================================
    private sealed class SeedContext
    {
        public required ApplicationUser Admin { get; init; }
        public required ApplicationUser Doctor { get; init; }
        public required ApplicationUser Consultant { get; init; }
        public required ApplicationUser Mo { get; init; }
        public required ApplicationUser Nurse { get; init; }
        public required ApplicationUser Midwife { get; init; }
        public required ApplicationUser Cno { get; init; }
        public required ApplicationUser LabSci { get; init; }
        public required ApplicationUser LabTech { get; init; }
        public required ApplicationUser Radiographer { get; init; }
        public required ApplicationUser Pharmacist { get; init; }
        public required ApplicationUser Cashier { get; init; }
        public required ApplicationUser Receptionist { get; init; }
        public required ApplicationUser ClaimsOfficer { get; init; }
        public required ApplicationUser StoreOfficer { get; init; }

        public required List<ThriveHealth.Web.Models.Scheduling.Clinic> Clinics { get; init; }
        public required List<ThriveHealth.Web.Models.Diagnostics.LabTest> LabTests { get; init; }
        public required List<ThriveHealth.Web.Models.Pharmacy.Drug> Drugs { get; init; }
        public required List<ThriveHealth.Web.Models.Insurance.Payer> Payers { get; init; }
        public required List<ThriveHealth.Web.Models.Insurance.PayerPlan> PayerPlans { get; init; }
        public required List<ThriveHealth.Web.Models.Inpatient.Ward> Wards { get; init; }
        public required List<ThriveHealth.Web.Models.Theatre.Theatre> Theatres { get; init; }
        public required List<ThriveHealth.Web.Models.Emergency.ResusBay> ResusBays { get; init; }
        public required List<ThriveHealth.Web.Models.Reporting.NotifiableDisease> Diseases { get; init; }
        public required List<ThriveHealth.Web.Models.Immunization.Vaccine> Vaccines { get; init; }
        public required List<ThriveHealth.Web.Models.Pharmacy.PharmacyStore> PharmacyStores { get; init; }
        public required ThriveHealth.Web.Models.Scheduling.Clinic OpdClinic { get; init; }
        public required ThriveHealth.Web.Models.Scheduling.Clinic AeClinic { get; init; }
        public required ThriveHealth.Web.Models.Scheduling.Clinic AncClinic { get; init; }
        public required ThriveHealth.Web.Models.Scheduling.Clinic PaedClinic { get; init; }
    }

    private static async Task<SeedContext> BuildContextAsync(ApplicationDbContext db, UserManager<ApplicationUser> um, int fid)
    {
        async Task<ApplicationUser> Find(string email) =>
            (await um.FindByEmailAsync(email)) ?? throw new InvalidOperationException($"Demo seeder: user {email} missing — DbSeeder.SeedAsync must run first.");

        var clinics = await db.Clinics.Where(c => c.FacilityId == fid).ToListAsync();
        return new SeedContext
        {
            Admin = await Find("admin@thrivehealth.ng"),
            Doctor = await Find("doc@thrivehealth.ng"),
            Consultant = await Find("consultant@thrivehealth.ng"),
            Mo = await Find("mo@thrivehealth.ng"),
            Nurse = await Find("nurse@thrivehealth.ng"),
            Midwife = await Find("midwife@thrivehealth.ng"),
            Cno = await Find("cno@thrivehealth.ng"),
            LabSci = await Find("lab@thrivehealth.ng"),
            LabTech = await Find("labtech@thrivehealth.ng"),
            Radiographer = await Find("rad@thrivehealth.ng"),
            Pharmacist = await Find("pharm@thrivehealth.ng"),
            Cashier = await Find("cashier@thrivehealth.ng"),
            Receptionist = await Find("recep@thrivehealth.ng"),
            ClaimsOfficer = await Find("claims@thrivehealth.ng"),
            StoreOfficer = await Find("store@thrivehealth.ng"),

            Clinics = clinics,
            LabTests = await db.LabTests.AsNoTracking().Include(t => t.Analytes).ToListAsync(),
            Drugs = await db.Drugs.AsNoTracking().ToListAsync(),
            Payers = await db.Payers.AsNoTracking().ToListAsync(),
            PayerPlans = await db.PayerPlans.AsNoTracking().ToListAsync(),
            Wards = await db.Wards.AsNoTracking().Include(w => w.Beds).Where(w => w.FacilityId == fid).ToListAsync(),
            Theatres = await db.Theatres.AsNoTracking().Where(t => t.FacilityId == fid).ToListAsync(),
            ResusBays = await db.ResusBays.AsNoTracking().Where(r => r.FacilityId == fid).ToListAsync(),
            Diseases = await db.NotifiableDiseases.AsNoTracking().ToListAsync(),
            Vaccines = await db.Vaccines.AsNoTracking().ToListAsync(),
            PharmacyStores = await db.PharmacyStores.AsNoTracking().Where(s => s.FacilityId == fid).ToListAsync(),
            OpdClinic = clinics.First(c => c.Code == "OPD"),
            AeClinic = clinics.First(c => c.Code == "AE"),
            AncClinic = clinics.First(c => c.Code == "ANC"),
            PaedClinic = clinics.First(c => c.Code == "PAED"),
        };
    }

    // ====================================================================================
    // Phase 1: patients + vitals + allergies + problems + medications + NOK + payer
    // ====================================================================================

    /// <summary>Fixed-shape patient template that keeps each demo patient deterministic and recognisable.</summary>
    private record PatientTemplate(
        string FirstName, string LastName, string? MiddleName, Sex Sex,
        int AgeYears, string Phone, string Lga, string State,
        string Tribe, string? Religion, MaritalStatus? Marital,
        string Occupation, string? Nin,
        string? Allergy, AllergyCategory AllergyCat, AllergySeverity AllergySev,
        string? Problem, string? ProblemIcd, ProblemStatus ProblemStatus,
        string? Medication, string? MedDose, string? MedFreq,
        string? PrimaryPayer, string? PrimaryPlan,
        JourneyType Journey);

    private enum JourneyType
    {
        OpdHypertension, OpdDiabetes, OpdMalaria, OpdUrti, OpdGastritis, OpdAsthma, OpdLowBack, OpdEczema,
        OpdAnaemia, OpdBph, OpdAnxiety, OpdOtitis,
        AeRta, AeMiSuspected, AeSepsis, AeAsthmaExacerbation, AeStroke, AeChildSeizure, AeGsw, AeBurns,
        AncFirstVisit, AncMidTerm, AncLateTerm, AncDelivered, AncHighRisk,
        PaedsImmunization, PaedsGrowth, PaedsAcute, PaedsMalnutrition,
        InpatientPneumonia, InpatientCellulitis, InpatientGastroenteritis,
        SurgicalAppendectomy, SurgicalCS, SurgicalHernia,
        IcuPostOp, DialysisCkd,
        DeceasedAdmission
    }

    private static IReadOnlyList<PatientTemplate> Templates() => new PatientTemplate[]
    {
        new("Adekunle", "Adesanya", "Olumide", Sex.Male, 58, "+2348012345001", "Ikeja", "Lagos", "Yoruba", "Christian", MaritalStatus.Married, "Civil servant", "11122233301",
            null, AllergyCategory.Drug, AllergySeverity.Mild, "Hypertension", "I10", ProblemStatus.Chronic, "Amlodipine", "10 mg", "OD", "NHIS", "NHIS Standard", JourneyType.OpdHypertension),
        new("Ngozi", "Eze", null, Sex.Female, 52, "+2348012345002", "Owerri Municipal", "Imo", "Igbo", "Christian", MaritalStatus.Married, "Trader", "11122233302",
            "Penicillin", AllergyCategory.Drug, AllergySeverity.Severe, "Type 2 Diabetes Mellitus", "E11", ProblemStatus.Chronic, "Metformin", "1 g", "BD", "Hygeia HMO", "Hygeia Bronze", JourneyType.OpdDiabetes),
        new("Bashir", "Yusuf", "Aminu", Sex.Male, 34, "+2348012345003", "Kano Municipal", "Kano", "Hausa", "Muslim", MaritalStatus.Married, "Driver", "11122233303",
            null, AllergyCategory.Drug, AllergySeverity.Mild, null, null, ProblemStatus.Active, null, null, null, null, null, JourneyType.OpdMalaria),
        new("Chiamaka", "Okafor", null, Sex.Female, 27, "+2348012345004", "Enugu North", "Enugu", "Igbo", "Christian", MaritalStatus.Single, "Teacher", "11122233304",
            null, AllergyCategory.Drug, AllergySeverity.Mild, null, null, ProblemStatus.Active, null, null, null, "AXA Mansard", "AXA Health Plus", JourneyType.OpdUrti),
        new("Olumide", "Bello", null, Sex.Male, 41, "+2348012345005", "Surulere", "Lagos", "Yoruba", "Christian", MaritalStatus.Married, "Banker", "11122233305",
            null, AllergyCategory.Drug, AllergySeverity.Mild, "Gastritis", "K29", ProblemStatus.Active, "Omeprazole", "20 mg", "OD", null, null, JourneyType.OpdGastritis),
        new("Aisha", "Ibrahim", null, Sex.Female, 23, "+2348012345006", "Maiduguri", "Borno", "Hausa", "Muslim", MaritalStatus.Single, "Student", "11122233306",
            "Aspirin", AllergyCategory.Drug, AllergySeverity.Moderate, "Asthma", "J45", ProblemStatus.Chronic, "Salbutamol inhaler", "100 mcg", "PRN", null, null, JourneyType.OpdAsthma),
        new("Tunde", "Lawal", "Akin", Sex.Male, 36, "+2348012345007", "Ibadan North", "Oyo", "Yoruba", "Muslim", MaritalStatus.Married, "Mechanic", "11122233307",
            null, AllergyCategory.Drug, AllergySeverity.Mild, "Mechanical low back pain", "M54", ProblemStatus.Active, null, null, null, null, null, JourneyType.OpdLowBack),
        new("Funmi", "Olawale", null, Sex.Female, 19, "+2348012345008", "Ado-Ekiti", "Ekiti", "Yoruba", "Christian", MaritalStatus.Single, "Student", "11122233308",
            null, AllergyCategory.Drug, AllergySeverity.Mild, "Atopic eczema", "L20", ProblemStatus.Chronic, null, null, null, null, null, JourneyType.OpdEczema),
        new("Halima", "Mohammed", null, Sex.Female, 45, "+2348012345009", "Kaduna North", "Kaduna", "Hausa", "Muslim", MaritalStatus.Married, "Tailor", "11122233309",
            null, AllergyCategory.Drug, AllergySeverity.Mild, null, null, ProblemStatus.Active, null, null, null, "NHIS", "NHIS Standard", JourneyType.OpdAnaemia),
        new("Sani", "Garba", null, Sex.Male, 67, "+2348012345010", "Sokoto North", "Sokoto", "Hausa", "Muslim", MaritalStatus.Married, "Retired", "11122233310",
            null, AllergyCategory.Drug, AllergySeverity.Mild, "Benign prostatic hyperplasia", "N40", ProblemStatus.Chronic, "Tamsulosin", "0.4 mg", "OD", null, null, JourneyType.OpdBph),
        new("Kemi", "Adeyemi", null, Sex.Female, 31, "+2348012345011", "Ibadan South-East", "Oyo", "Yoruba", "Christian", MaritalStatus.Single, "Marketer", "11122233311",
            null, AllergyCategory.Drug, AllergySeverity.Mild, "Generalised anxiety", "F41.1", ProblemStatus.Chronic, null, null, null, null, null, JourneyType.OpdAnxiety),
        new("Joseph", "Okonkwo", null, Sex.Male, 8, "+2348012345012", "Asaba", "Delta", "Igbo", "Christian", null, "Pupil", "11122233312",
            null, AllergyCategory.Drug, AllergySeverity.Mild, null, null, ProblemStatus.Active, null, null, null, null, null, JourneyType.OpdOtitis),

        // A&E
        new("Emeka", "Nwosu", null, Sex.Male, 28, "+2348012345013", "Onitsha North", "Anambra", "Igbo", "Christian", MaritalStatus.Single, "Driver", "11122233313",
            null, AllergyCategory.Drug, AllergySeverity.Mild, null, null, ProblemStatus.Active, null, null, null, null, null, JourneyType.AeRta),
        new("Yakubu", "Suleiman", null, Sex.Male, 62, "+2348012345014", "Bauchi", "Bauchi", "Hausa", "Muslim", MaritalStatus.Married, "Farmer", "11122233314",
            null, AllergyCategory.Drug, AllergySeverity.Mild, "Hypertension", "I10", ProblemStatus.Chronic, "Lisinopril", "10 mg", "OD", null, null, JourneyType.AeMiSuspected),
        new("Blessing", "Ojo", null, Sex.Female, 47, "+2348012345015", "Akure South", "Ondo", "Yoruba", "Christian", MaritalStatus.Married, "Trader", "11122233315",
            null, AllergyCategory.Drug, AllergySeverity.Mild, "Diabetes", "E11", ProblemStatus.Chronic, null, null, null, "Hygeia HMO", "Hygeia Bronze", JourneyType.AeSepsis),
        new("Sade", "Bankole", null, Sex.Female, 22, "+2348012345016", "Abeokuta South", "Ogun", "Yoruba", "Christian", MaritalStatus.Single, "Hairstylist", "11122233316",
            "Pollen", AllergyCategory.Environmental, AllergySeverity.Moderate, "Asthma", "J45", ProblemStatus.Chronic, "Salbutamol inhaler", "100 mcg", "PRN", null, null, JourneyType.AeAsthmaExacerbation),
        new("Musa", "Adamu", null, Sex.Male, 71, "+2348012345017", "Jos North", "Plateau", "Hausa", "Muslim", MaritalStatus.Married, "Retired", "11122233317",
            null, AllergyCategory.Drug, AllergySeverity.Mild, "Hypertension", "I10", ProblemStatus.Chronic, "Amlodipine", "10 mg", "OD", null, null, JourneyType.AeStroke),
        new("Daniel", "Adekoya", null, Sex.Male, 4, "+2348012345018", "Ikorodu", "Lagos", "Yoruba", "Christian", null, "Child", "11122233318",
            null, AllergyCategory.Drug, AllergySeverity.Mild, null, null, ProblemStatus.Active, null, null, null, null, null, JourneyType.AeChildSeizure),
        new("Ibrahim", "Aliyu", null, Sex.Male, 33, "+2348012345019", "Zaria", "Kaduna", "Hausa", "Muslim", MaritalStatus.Single, "Trader", "11122233319",
            null, AllergyCategory.Drug, AllergySeverity.Mild, null, null, ProblemStatus.Active, null, null, null, null, null, JourneyType.AeGsw),
        new("Bisi", "Ogundipe", null, Sex.Female, 38, "+2348012345020", "Ilorin West", "Kwara", "Yoruba", "Muslim", MaritalStatus.Married, "Cook", "11122233320",
            null, AllergyCategory.Drug, AllergySeverity.Mild, null, null, ProblemStatus.Active, null, null, null, null, null, JourneyType.AeBurns),

        // ANC / maternity
        new("Amina", "Sani", null, Sex.Female, 25, "+2348012345021", "Kano South", "Kano", "Hausa", "Muslim", MaritalStatus.Married, "Housewife", "11122233321",
            null, AllergyCategory.Drug, AllergySeverity.Mild, null, null, ProblemStatus.Active, null, null, null, null, null, JourneyType.AncFirstVisit),
        new("Patience", "Eze", null, Sex.Female, 29, "+2348012345022", "Aba South", "Abia", "Igbo", "Christian", MaritalStatus.Married, "Trader", "11122233322",
            null, AllergyCategory.Drug, AllergySeverity.Mild, null, null, ProblemStatus.Active, null, null, null, "NHIS", "NHIS Standard", JourneyType.AncMidTerm),
        new("Folake", "Adebayo", null, Sex.Female, 33, "+2348012345023", "Ikeja", "Lagos", "Yoruba", "Christian", MaritalStatus.Married, "Banker", "11122233323",
            null, AllergyCategory.Drug, AllergySeverity.Mild, null, null, ProblemStatus.Active, null, null, null, "AXA Mansard", "AXA Health Plus", JourneyType.AncLateTerm),
        new("Hauwa", "Bello", null, Sex.Female, 27, "+2348012345024", "Yola", "Adamawa", "Hausa", "Muslim", MaritalStatus.Married, "Teacher", "11122233324",
            null, AllergyCategory.Drug, AllergySeverity.Mild, null, null, ProblemStatus.Active, null, null, null, null, null, JourneyType.AncDelivered),
        new("Esther", "Akpan", null, Sex.Female, 35, "+2348012345025", "Uyo", "Akwa Ibom", "Ibibio", "Christian", MaritalStatus.Married, "Civil servant", "11122233325",
            null, AllergyCategory.Drug, AllergySeverity.Mild, "Hypertension", "I10", ProblemStatus.Chronic, "Methyldopa", "250 mg", "TDS", null, null, JourneyType.AncHighRisk),

        // Paeds
        new("Tope", "Adesanya", null, Sex.Female, 0, "+2348012345026", "Ikeja", "Lagos", "Yoruba", "Christian", null, "Infant", null,
            null, AllergyCategory.Drug, AllergySeverity.Mild, null, null, ProblemStatus.Active, null, null, null, null, null, JourneyType.PaedsImmunization),
        new("Kelechi", "Okeke", null, Sex.Male, 2, "+2348012345027", "Aba", "Abia", "Igbo", "Christian", null, "Child", null,
            null, AllergyCategory.Drug, AllergySeverity.Mild, null, null, ProblemStatus.Active, null, null, null, null, null, JourneyType.PaedsGrowth),
        new("Hassan", "Yusuf", null, Sex.Male, 5, "+2348012345028", "Kaduna", "Kaduna", "Hausa", "Muslim", null, "Pupil", null,
            null, AllergyCategory.Drug, AllergySeverity.Mild, null, null, ProblemStatus.Active, null, null, null, null, null, JourneyType.PaedsAcute),
        new("Mary", "Eze", null, Sex.Female, 3, "+2348012345029", "Owerri", "Imo", "Igbo", "Christian", null, "Child", null,
            null, AllergyCategory.Drug, AllergySeverity.Mild, "Severe acute malnutrition", "E43", ProblemStatus.Active, null, null, null, null, null, JourneyType.PaedsMalnutrition),

        // Inpatient (medical)
        new("Adebayo", "Akande", null, Sex.Male, 55, "+2348012345030", "Ibadan North", "Oyo", "Yoruba", "Christian", MaritalStatus.Married, "Lecturer", "11122233330",
            null, AllergyCategory.Drug, AllergySeverity.Mild, null, null, ProblemStatus.Active, null, null, null, "NHIS", "NHIS Standard", JourneyType.InpatientPneumonia),
        new("Grace", "Onuoha", null, Sex.Female, 44, "+2348012345031", "Port Harcourt", "Rivers", "Igbo", "Christian", MaritalStatus.Married, "Trader", "11122233331",
            null, AllergyCategory.Drug, AllergySeverity.Mild, "Diabetes", "E11", ProblemStatus.Chronic, "Metformin", "1 g", "BD", null, null, JourneyType.InpatientCellulitis),
        new("Olu", "Falade", null, Sex.Male, 19, "+2348012345032", "Lekki", "Lagos", "Yoruba", "Christian", MaritalStatus.Single, "Student", "11122233332",
            null, AllergyCategory.Drug, AllergySeverity.Mild, null, null, ProblemStatus.Active, null, null, null, null, null, JourneyType.InpatientGastroenteritis),

        // Surgical
        new("Chinedu", "Obi", null, Sex.Male, 24, "+2348012345033", "Awka", "Anambra", "Igbo", "Christian", MaritalStatus.Single, "Mechanic", "11122233333",
            null, AllergyCategory.Drug, AllergySeverity.Mild, null, null, ProblemStatus.Active, null, null, null, null, null, JourneyType.SurgicalAppendectomy),
        new("Khadija", "Umar", null, Sex.Female, 28, "+2348012345034", "Kano", "Kano", "Hausa", "Muslim", MaritalStatus.Married, "Housewife", "11122233334",
            null, AllergyCategory.Drug, AllergySeverity.Mild, null, null, ProblemStatus.Active, null, null, null, "Hygeia HMO", "Hygeia Bronze", JourneyType.SurgicalCS),
        new("Solomon", "Alabi", null, Sex.Male, 42, "+2348012345035", "Osogbo", "Osun", "Yoruba", "Christian", MaritalStatus.Married, "Builder", "11122233335",
            null, AllergyCategory.Drug, AllergySeverity.Mild, "Inguinal hernia", "K40", ProblemStatus.Chronic, null, null, null, null, null, JourneyType.SurgicalHernia),

        // ICU + dialysis
        new("Maria", "Achebe", null, Sex.Female, 68, "+2348012345036", "Enugu", "Enugu", "Igbo", "Christian", MaritalStatus.Widowed, "Retired", "11122233336",
            null, AllergyCategory.Drug, AllergySeverity.Mild, "Hypertension", "I10", ProblemStatus.Chronic, "Lisinopril", "20 mg", "OD", null, null, JourneyType.IcuPostOp),
        new("Ahmed", "Tanko", null, Sex.Male, 59, "+2348012345037", "Kaduna South", "Kaduna", "Hausa", "Muslim", MaritalStatus.Married, "Civil servant", "11122233337",
            null, AllergyCategory.Drug, AllergySeverity.Mild, "Chronic kidney disease stage 5", "N18.5", ProblemStatus.Chronic, "Erythropoietin", "4000 IU", "Weekly", "NHIS", "NHIS Standard", JourneyType.DialysisCkd),

        // Deceased
        new("Samuel", "Ojo", null, Sex.Male, 78, "+2348012345038", "Akure", "Ondo", "Yoruba", "Christian", MaritalStatus.Married, "Retired", "11122233338",
            null, AllergyCategory.Drug, AllergySeverity.Mild, "Heart failure", "I50", ProblemStatus.Chronic, "Furosemide", "40 mg", "OD", null, null, JourneyType.DeceasedAdmission),
    };

    private static async Task<List<Patient>> CreatePatientsAsync(ApplicationDbContext db, int fid, SeedContext ctx, Random rnd, DateTime now)
    {
        var templates = Templates();
        var patients = new List<Patient>();
        var year = now.Year;

        // Allocate hospital number sequence in one shot (avoid per-patient saves).
        var counter = await db.HospitalNumberCounters.FirstOrDefaultAsync(c => c.FacilityId == fid && c.Year == year);
        int seq;
        if (counter is null)
        {
            counter = new HospitalNumberCounter { FacilityId = fid, Year = year, LastSequence = templates.Count };
            db.HospitalNumberCounters.Add(counter);
            seq = 0;
        }
        else
        {
            seq = counter.LastSequence;
            counter.LastSequence += templates.Count;
        }

        var prefix = (await db.Facilities.AsNoTracking().FirstAsync(f => f.Id == fid)).HospitalNumberPrefix ?? "TH";

        for (var i = 0; i < templates.Count; i++)
        {
            var t = templates[i];
            seq++;
            var dob = DateOnly.FromDateTime(now.AddYears(-t.AgeYears).AddDays(-rnd.Next(0, 365)));

            var p = new Patient
            {
                FacilityId = fid,
                HospitalNumber = $"{prefix}/{year}/{seq:D6}",
                FirstName = t.FirstName,
                LastName = t.LastName,
                MiddleName = t.MiddleName,
                Sex = t.Sex,
                DateOfBirth = dob,
                Phone = t.Phone,
                Lga = t.Lga,
                State = t.State,
                StateOfOrigin = t.State,
                EthnicGroup = t.Tribe,
                Religion = t.Religion,
                MaritalStatus = t.Marital,
                Occupation = t.Occupation,
                Nin = t.Nin,
                NinVerified = t.Nin != null,
                NinVerifiedAt = t.Nin != null ? now.AddDays(-rnd.Next(10, 365)) : null,
                StreetAddress = $"{rnd.Next(1, 99)}, {t.Lga} Street",
                CreatedAt = now.AddDays(-rnd.Next(1, 365)),
                CreatedById = ctx.Receptionist.Id
            };

            // Next of kin
            p.NextOfKin.Add(new PatientNextOfKin
            {
                Name = NokName(t),
                Relationship = NokRelationship(t),
                Phone = "+234801234" + (5100 + i).ToString(),
                Address = p.StreetAddress,
                IsPrimary = true
            });

            // Allergy
            if (!string.IsNullOrEmpty(t.Allergy))
            {
                p.Allergies.Add(new Allergy
                {
                    Category = t.AllergyCat,
                    Substance = t.Allergy,
                    Reaction = t.AllergySev >= AllergySeverity.Severe ? "Anaphylaxis-like" : "Rash, itching",
                    Severity = t.AllergySev,
                    OnsetDate = DateOnly.FromDateTime(now.AddYears(-rnd.Next(1, 10))),
                    IsActive = true,
                    RecordedById = ctx.Doctor.Id,
                    RecordedAt = now.AddDays(-rnd.Next(30, 720))
                });
            }

            // Problem
            if (!string.IsNullOrEmpty(t.Problem))
            {
                p.Problems.Add(new Problem
                {
                    Description = t.Problem,
                    IcdCode = t.ProblemIcd,
                    Status = t.ProblemStatus,
                    OnsetDate = DateOnly.FromDateTime(now.AddYears(-rnd.Next(1, 10))),
                    RecordedById = ctx.Doctor.Id,
                    RecordedAt = now.AddDays(-rnd.Next(30, 720))
                });
            }

            // Existing medication
            if (!string.IsNullOrEmpty(t.Medication))
            {
                p.Medications.Add(new MedicationRecord
                {
                    DrugName = t.Medication,
                    Dose = t.MedDose,
                    Route = "PO",
                    Frequency = t.MedFreq,
                    StartDate = DateOnly.FromDateTime(now.AddDays(-rnd.Next(60, 720))),
                    IsCurrent = true,
                    Source = MedicationSource.External,
                    RecordedById = ctx.Doctor.Id,
                    RecordedAt = now.AddDays(-rnd.Next(30, 360))
                });
            }

            // Vitals — at least one recent set
            p.Vitals.Add(NewVitals(rnd, now.AddDays(-rnd.Next(0, 14)), t, ctx.Nurse.Id));

            patients.Add(p);
        }

        db.Patients.AddRange(patients);
        await db.SaveChangesAsync();

        // Payer associations (some patients only)
        for (var i = 0; i < templates.Count; i++)
        {
            var t = templates[i];
            if (string.IsNullOrEmpty(t.PrimaryPayer)) continue;
            var payer = ctx.Payers.FirstOrDefault(x => x.Name.Contains(t.PrimaryPayer.Split(' ')[0]));
            if (payer is null) continue;
            var plan = ctx.PayerPlans.FirstOrDefault(p => p.PayerId == payer.Id);
            db.PatientPayers.Add(new PatientPayer
            {
                PatientId = patients[i].Id,
                Type = PayerType.Hmo,
                PayerId = payer.Id,
                PayerPlanId = plan?.Id,
                Name = payer.Name,
                PlanName = plan?.Name,
                MembershipNumber = $"M{1000 + i:D6}",
                IsPrimary = true,
                IsActive = true,
                ValidFrom = DateOnly.FromDateTime(now.AddYears(-1)),
                ValidTo = DateOnly.FromDateTime(now.AddYears(1)),
                CreatedAt = now.AddDays(-rnd.Next(60, 720))
            });
        }
        await db.SaveChangesAsync();

        return patients;
    }

    private static VitalsRecord NewVitals(Random rnd, DateTime at, PatientTemplate t, string nurseId)
    {
        // Tweak vitals by journey type so the data is plausible.
        int sysBp = 110 + rnd.Next(0, 25);
        int diaBp = 70 + rnd.Next(0, 15);
        decimal temp = 36.5m + (decimal)rnd.NextDouble();
        int hr = 70 + rnd.Next(0, 25);
        int rr = 16 + rnd.Next(0, 6);
        int spo2 = 96 + rnd.Next(0, 4);

        switch (t.Journey)
        {
            case JourneyType.OpdHypertension:
            case JourneyType.AeStroke:
                sysBp = 160 + rnd.Next(0, 30); diaBp = 100 + rnd.Next(0, 15); break;
            case JourneyType.AeMiSuspected:
                sysBp = 95 + rnd.Next(-10, 15); hr = 110 + rnd.Next(0, 20); spo2 = 92 + rnd.Next(0, 4); break;
            case JourneyType.AeSepsis:
                sysBp = 85 + rnd.Next(0, 15); hr = 120 + rnd.Next(0, 20); temp = 39m + (decimal)rnd.NextDouble(); rr = 24 + rnd.Next(0, 6); break;
            case JourneyType.AeAsthmaExacerbation:
                rr = 28 + rnd.Next(0, 6); spo2 = 90 + rnd.Next(0, 4); hr = 110 + rnd.Next(0, 20); break;
            case JourneyType.OpdMalaria:
                temp = 38.5m + (decimal)rnd.NextDouble(); hr = 95 + rnd.Next(0, 20); break;
            case JourneyType.OpdAnaemia:
                hr = 100 + rnd.Next(0, 15); break;
            case JourneyType.AeChildSeizure:
                temp = 39m + (decimal)rnd.NextDouble(); hr = 130 + rnd.Next(0, 20); break;
            case JourneyType.IcuPostOp:
                sysBp = 100 + rnd.Next(0, 20); hr = 95 + rnd.Next(0, 15); spo2 = 95 + rnd.Next(0, 4); break;
        }

        decimal? weight = t.AgeYears < 1 ? 7m + (decimal)rnd.NextDouble() :
                          t.AgeYears < 12 ? 15m + t.AgeYears * 2.5m :
                          50m + rnd.Next(0, 40);
        decimal? height = t.AgeYears < 1 ? 65m + (decimal)rnd.NextDouble() * 5m :
                          t.AgeYears < 12 ? 85m + t.AgeYears * 6m :
                          155m + rnd.Next(0, 30);

        return new VitalsRecord
        {
            RecordedAt = at,
            RecordedById = nurseId,
            SystolicBp = sysBp,
            DiastolicBp = diaBp,
            HeartRate = hr,
            RespiratoryRate = rr,
            TemperatureCelsius = Math.Round(temp, 1),
            SpO2 = spo2,
            WeightKg = weight is null ? null : Math.Round(weight.Value, 1),
            HeightCm = height is null ? null : Math.Round(height.Value, 1),
            Notes = "Routine"
        };
    }

    private static string NokName(PatientTemplate t)
    {
        if (t.AgeYears < 14) return $"Father of {t.FirstName} {t.LastName}";
        if (t.Sex == Sex.Female && t.Marital == MaritalStatus.Married) return $"Husband of {t.FirstName}";
        if (t.Sex == Sex.Male && t.Marital == MaritalStatus.Married) return $"Wife of {t.FirstName}";
        return $"Sibling of {t.FirstName}";
    }

    private static string NokRelationship(PatientTemplate t)
    {
        if (t.AgeYears < 14) return "Parent";
        if (t.Marital == MaritalStatus.Married) return "Spouse";
        return "Sibling";
    }

    // ====================================================================================
    // Phase 2-9: stubs — implemented in subsequent edits.
    // ====================================================================================

    // ====================================================================================
    // Phase 2: encounters — SOAP, diagnoses, lab/imaging orders, prescriptions
    // ====================================================================================
    private record EncSpec(
        EncounterType Type,
        int ClinicId,
        string Clinician,
        string CC,
        string Subj, string Obj, string Asses, string Plan,
        (string icd, string desc, bool primary)[] Dx,
        string[] LabCodes,
        (ImagingModality mod, string study, string indication)[] Imaging,
        (string drug, string strength, string dose, string route, string freq, string duration, int qty, string? instr)[] Rx,
        bool LabResults,
        bool ImagingReported,
        int DaysAgo);

    private static EncSpec? BuildEncounter(PatientTemplate t, SeedContext ctx, Random rnd)
    {
        string Doc() => ctx.Doctor.Id;
        string Cons() => ctx.Consultant.Id;
        string Mo() => ctx.Mo.Id;

        return t.Journey switch
        {
            JourneyType.OpdHypertension => new(EncounterType.OutpatientFollowUp, ctx.OpdClinic.Id, Doc(),
                "BP review", "BP medication compliance, mild headache 2/52", "BP 158/96, HR 78, no oedema",
                "Hypertension — uncontrolled on monotherapy",
                "Add hydrochlorothiazide; recheck BP in 4 weeks; lifestyle counselling",
                new[] { ("I10", "Essential hypertension", true) },
                new[] { "FBC", "UE", "GLUCOSE", "LIPID" }, new (ImagingModality, string, string)[] { },
                new[] { ("Hydrochlorothiazide", "25 mg", "12.5 mg", "PO", "OD", "30 days", 30, (string?)"Take in the morning") },
                true, false, rnd.Next(1, 30)),

            JourneyType.OpdDiabetes => new(EncounterType.OutpatientFollowUp, ctx.OpdClinic.Id, Cons(),
                "Diabetes review", "Polyuria, mild fatigue, missed metformin doses",
                "BP 130/82, BMI 31, no foot ulcers", "T2DM — suboptimal control",
                "Continue metformin; add gliclazide; foot care advice; FBS log",
                new[] { ("E11", "Type 2 diabetes mellitus", true) },
                new[] { "GLUCOSE", "HBA1C", "UE", "URINALYSIS" }, new (ImagingModality, string, string)[] { },
                new[] { ("Gliclazide", "80 mg", "40 mg", "PO", "OD", "30 days", 30, (string?)null) },
                true, false, rnd.Next(1, 21)),

            JourneyType.OpdMalaria => new(EncounterType.OutpatientOpd, ctx.OpdClinic.Id, Mo(),
                "Fever, body pains 3/7", "High-grade fever, chills, headache, myalgia",
                "T 38.7°C, HR 96, no rash, mild splenomegaly", "Uncomplicated malaria",
                "ACT (artemether-lumefantrine) 6-dose; paracetamol PRN; safety net advice",
                new[] { ("B54", "Unspecified malaria", true) },
                new[] { "MP_RDT", "FBC" }, new (ImagingModality, string, string)[] { },
                new[]
                {
                    ("Artemether/Lumefantrine", "20/120 mg", "4 tabs", "PO", "BD", "3 days", 24, (string?)"Take after fatty meal"),
                    ("Paracetamol", "500 mg", "1 g", "PO", "QDS", "3 days", 12, (string?)null)
                },
                true, false, rnd.Next(1, 14)),

            JourneyType.OpdUrti => new(EncounterType.OutpatientOpd, ctx.OpdClinic.Id, Mo(),
                "Sore throat, cough 2/7", "Sore throat, dry cough, mild rhinorrhoea, no fever",
                "T 36.9°C, oropharynx mildly injected, chest clear", "Acute upper respiratory tract infection",
                "Symptomatic; antipyretics; reassurance; return if worsening",
                new[] { ("J06", "Acute upper respiratory infection", true) },
                Array.Empty<string>(), new (ImagingModality, string, string)[] { },
                new[] { ("Paracetamol", "500 mg", "1 g", "PO", "QDS", "5 days", 20, (string?)null) },
                false, false, rnd.Next(1, 14)),

            JourneyType.OpdGastritis => new(EncounterType.OutpatientFollowUp, ctx.OpdClinic.Id, Doc(),
                "Epigastric pain", "Burning epigastric pain post-meals, no haematemesis",
                "Epigastric tenderness, no peritonism", "Functional dyspepsia / chronic gastritis",
                "PPI 4 weeks; H. pylori test; lifestyle advice",
                new[] { ("K29", "Gastritis and duodenitis", true) },
                new[] { "FBC", "URINALYSIS" }, new (ImagingModality, string, string)[] { },
                new[] { ("Omeprazole", "20 mg", "20 mg", "PO", "OD", "28 days", 28, (string?)"Take 30 min before breakfast") },
                false, false, rnd.Next(1, 30)),

            JourneyType.OpdAsthma => new(EncounterType.OutpatientFollowUp, ctx.OpdClinic.Id, Doc(),
                "Asthma review", "Increased night cough, salbutamol use 4×/week",
                "Wheeze on auscultation, no respiratory distress", "Asthma — partly controlled",
                "Step up therapy: ICS+LABA combination; review in 4 weeks; PEFR monitoring",
                new[] { ("J45", "Asthma", true) },
                Array.Empty<string>(),
                new[] { (ImagingModality.XRay, "Chest X-ray PA", "Asthma assessment") },
                new[]
                {
                    ("Beclomethasone+Salmeterol inhaler", "50/25 mcg", "2 puffs", "Inh", "BD", "30 days", 1, (string?)"Rinse mouth after use"),
                    ("Salbutamol inhaler", "100 mcg", "2 puffs", "Inh", "PRN", "30 days", 1, (string?)null)
                },
                false, true, rnd.Next(1, 30)),

            JourneyType.OpdLowBack => new(EncounterType.OutpatientOpd, ctx.OpdClinic.Id, Mo(),
                "Low back pain", "Mechanical low back pain after lifting; no neurological deficit",
                "Paraspinal tenderness, SLR negative", "Mechanical low back pain",
                "Analgesia; physiotherapy referral; activity modification",
                new[] { ("M54", "Dorsalgia", true) },
                Array.Empty<string>(), new (ImagingModality, string, string)[] { },
                new[]
                {
                    ("Diclofenac", "50 mg", "50 mg", "PO", "BD", "5 days", 10, (string?)"Take with food"),
                    ("Paracetamol", "500 mg", "1 g", "PO", "QDS", "5 days", 20, (string?)null)
                },
                false, false, rnd.Next(1, 21)),

            JourneyType.OpdEczema => new(EncounterType.OutpatientFollowUp, ctx.OpdClinic.Id, Doc(),
                "Eczema flare", "Pruritic rash on flexures, worsened in dry season",
                "Lichenified plaques on antecubital fossae", "Atopic dermatitis — flare",
                "Topical steroid; emollient; trigger advice",
                new[] { ("L20", "Atopic dermatitis", true) },
                Array.Empty<string>(), new (ImagingModality, string, string)[] { },
                new[]
                {
                    ("Hydrocortisone cream 1%", "1%", "Apply thin layer", "Topical", "BD", "14 days", 1, (string?)null),
                    ("Cetirizine", "10 mg", "10 mg", "PO", "OD", "14 days", 14, (string?)null)
                },
                false, false, rnd.Next(1, 30)),

            JourneyType.OpdAnaemia => new(EncounterType.OutpatientOpd, ctx.OpdClinic.Id, Doc(),
                "Tiredness, dizziness", "Fatigue, easy tiredness, mild dyspnoea on exertion",
                "Pale conjunctiva, HR 102, no oedema", "Anaemia — likely iron-deficiency",
                "FBC + ferritin; iron supplementation; review in 4 weeks",
                new[] { ("D50", "Iron deficiency anaemia", true) },
                new[] { "FBC" }, new (ImagingModality, string, string)[] { },
                new[] { ("Ferrous sulphate", "200 mg", "200 mg", "PO", "TDS", "30 days", 90, (string?)"Take with vitamin C") },
                true, false, rnd.Next(1, 30)),

            JourneyType.OpdBph => new(EncounterType.OutpatientFollowUp, ctx.OpdClinic.Id, Cons(),
                "Urinary symptoms", "Nocturia ×3, hesitancy, weak stream",
                "DRE: enlarged smooth prostate, no nodules", "Benign prostatic hyperplasia",
                "Continue tamsulosin; PSA; review symptom score in 4 weeks",
                new[] { ("N40", "Hyperplasia of prostate", true) },
                new[] { "UE", "URINALYSIS" },
                new[] { (ImagingModality.Ultrasound, "Renal + bladder USS (PVR)", "BPH assessment") },
                Array.Empty<(string, string, string, string, string, string, int, string?)>(),
                true, true, rnd.Next(1, 30)),

            JourneyType.OpdAnxiety => new(EncounterType.OutpatientFollowUp, ctx.OpdClinic.Id, Doc(),
                "Worry, palpitations", "Persistent worry about work and family, sleep impaired",
                "BP 122/78, HR 92, no thyroid enlargement", "Generalised anxiety disorder",
                "Reassurance; SSRI commenced; counselling referral",
                new[] { ("F41.1", "Generalised anxiety disorder", true) },
                new[] { "FBC", "UE" }, new (ImagingModality, string, string)[] { },
                new[] { ("Sertraline", "50 mg", "50 mg", "PO", "OD", "30 days", 30, (string?)"Build up over 4 weeks; review side-effects") },
                false, false, rnd.Next(1, 30)),

            JourneyType.OpdOtitis => new(EncounterType.OutpatientOpd, ctx.PaedClinic.Id, Mo(),
                "Right ear pain", "Right ear pain ×2 days, mild fever, irritability",
                "T 37.8°C, right tympanic membrane red and bulging", "Acute otitis media (right)",
                "Amoxicillin 5 days; analgesia; review if no improvement in 48h",
                new[] { ("H66", "Suppurative otitis media", true) },
                Array.Empty<string>(), new (ImagingModality, string, string)[] { },
                new[]
                {
                    ("Amoxicillin", "250 mg/5 ml", "250 mg", "PO", "TDS", "5 days", 1, (string?)"Shake well; complete the course"),
                    ("Paracetamol", "120 mg/5 ml", "120 mg", "PO", "QDS", "3 days", 1, (string?)null)
                },
                false, false, rnd.Next(1, 14)),

            JourneyType.AeRta => new(EncounterType.Emergency, ctx.AeClinic.Id, Doc(),
                "RTA — multiple injuries", "Pedestrian hit by car; head laceration; chest pain",
                "GCS 14, BP 100/60, HR 110, head laceration 4 cm, chest tender, no respiratory distress",
                "Multiple trauma — head injury, chest contusion",
                "ATLS protocol; head CT; chest X-ray; admit for observation",
                new[] { ("S00.83", "Open wound of scalp", true), ("S22.3", "Rib fracture suspected", false) },
                new[] { "FBC", "UE", "GLUCOSE" },
                new[] { (ImagingModality.CT, "Head CT non-contrast", "Trauma — assess for haemorrhage"), (ImagingModality.XRay, "Chest X-ray PA", "Rib fracture / pneumothorax") },
                new[] { ("Tetanus toxoid", "0.5 ml", "0.5 ml", "IM", "STAT", "1 dose", 1, (string?)null) },
                true, true, rnd.Next(1, 21)),

            JourneyType.AeMiSuspected => new(EncounterType.Emergency, ctx.AeClinic.Id, Cons(),
                "Chest pain — central, crushing, 2 hours", "Crushing central chest pain, sweating, nausea",
                "BP 90/60, HR 115, sweating, lung bases clear", "Acute coronary syndrome — STEMI rule out",
                "MONA-A; ECG; serial troponin; admit CCU; cardiology consult",
                new[] { ("I21", "Acute myocardial infarction", true) },
                new[] { "FBC", "UE", "GLUCOSE" },
                new[] { (ImagingModality.XRay, "Chest X-ray PA", "ACS workup") },
                new[]
                {
                    ("Aspirin", "300 mg", "300 mg", "PO", "STAT", "1 dose", 1, (string?)"Chewed"),
                    ("Clopidogrel", "300 mg", "300 mg", "PO", "STAT", "1 dose", 1, (string?)null)
                },
                true, true, rnd.Next(1, 14)),

            JourneyType.AeSepsis => new(EncounterType.Emergency, ctx.AeClinic.Id, Doc(),
                "Fever, confusion", "Fever 4 days, confusion, reduced oral intake, dysuria",
                "T 39.2°C, BP 88/55, HR 122, RR 26, GCS 13, suprapubic tenderness",
                "Sepsis — likely urinary source", "Sepsis-six; broad-spectrum IV; admit; ICU consult",
                new[] { ("A41", "Sepsis", true), ("N39.0", "Urinary tract infection", false) },
                new[] { "FBC", "UE", "GLUCOSE", "URINALYSIS", "BLOOD_CULTURE" },
                new[] { (ImagingModality.XRay, "Chest X-ray PA", "Sepsis workup") },
                new[]
                {
                    ("Ceftriaxone", "1 g", "2 g", "IV", "OD", "5 days", 5, (string?)null),
                    ("Paracetamol", "1 g", "1 g", "IV", "QDS", "3 days", 12, (string?)null)
                },
                true, true, rnd.Next(1, 14)),

            JourneyType.AeAsthmaExacerbation => new(EncounterType.Emergency, ctx.AeClinic.Id, Doc(),
                "Acute breathlessness", "Acute SOB, audible wheeze, salbutamol used heavily at home",
                "RR 30, SpO2 90%, widespread wheeze, accessory muscle use",
                "Acute severe asthma exacerbation",
                "O2; nebulised salbutamol+ipratropium; oral steroid; observe 4h",
                new[] { ("J45.901", "Acute exacerbation of asthma", true) },
                new[] { "FBC" }, new (ImagingModality, string, string)[] { },
                new[]
                {
                    ("Salbutamol nebuliser", "2.5 mg", "5 mg", "Neb", "Q20min ×3 then PRN", "Day 1", 6, (string?)null),
                    ("Prednisolone", "5 mg", "40 mg", "PO", "OD", "5 days", 5, (string?)null)
                },
                false, false, rnd.Next(1, 14)),

            JourneyType.AeStroke => new(EncounterType.Emergency, ctx.AeClinic.Id, Cons(),
                "Sudden weakness", "Sudden right-sided weakness and slurred speech 90 min ago",
                "BP 200/110, HR 88, GCS 14, right hemiparesis 2/5, NIHSS 8",
                "Acute ischaemic stroke", "Activate stroke pathway; CT head; thrombolysis assessment; admit",
                new[] { ("I63", "Cerebral infarction", true), ("I10", "Hypertensive crisis", false) },
                new[] { "FBC", "UE", "GLUCOSE" },
                new[] { (ImagingModality.CT, "Head CT non-contrast", "Acute stroke") },
                new[] { ("Aspirin", "300 mg", "300 mg", "PO", "STAT", "1 dose", 1, (string?)null) },
                true, true, rnd.Next(1, 21)),

            JourneyType.AeChildSeizure => new(EncounterType.Emergency, ctx.AeClinic.Id, Mo(),
                "Febrile convulsion", "Generalised tonic-clonic seizure 3 minutes; resolved before arrival",
                "T 39.4°C, GCS 14, no focal deficit, neck supple", "Febrile seizure — simple",
                "Antipyretics; reassurance; safety advice; observe 4h",
                new[] { ("R56.0", "Febrile convulsion", true) },
                new[] { "FBC", "MP_RDT" }, new (ImagingModality, string, string)[] { },
                new[] { ("Paracetamol", "120 mg/5 ml", "15 mg/kg", "PO", "QDS PRN", "3 days", 1, (string?)null) },
                true, false, rnd.Next(1, 14)),

            JourneyType.AeGsw => new(EncounterType.Emergency, ctx.AeClinic.Id, Cons(),
                "Gunshot wound — left thigh", "Single gunshot wound to left thigh; conscious, talking",
                "BP 110/70, HR 100, single entry wound mid-thigh, no exit, distal pulses present",
                "Gunshot wound — left thigh, neurovascular intact",
                "ATLS; tetanus; orthopaedics + vascular consult; femur X-ray; theatre",
                new[] { ("S71.001A", "Open wound of thigh", true) },
                new[] { "FBC", "UE", "GLUCOSE" },
                new[] { (ImagingModality.XRay, "Left femur X-ray AP+lat", "GSW") },
                new[]
                {
                    ("Tetanus toxoid", "0.5 ml", "0.5 ml", "IM", "STAT", "1 dose", 1, (string?)null),
                    ("Cefazolin", "1 g", "1 g", "IV", "Q8H", "3 days", 9, (string?)null)
                },
                true, true, rnd.Next(1, 21)),

            JourneyType.AeBurns => new(EncounterType.Emergency, ctx.AeClinic.Id, Doc(),
                "Hot oil burn — right arm and torso", "Hot cooking oil spill on right upper limb and anterior torso",
                "Burn ~12% TBSA, mostly partial thickness, no airway involvement",
                "Burns 12% TBSA partial thickness",
                "Parkland fluid resuscitation; analgesia; tetanus; admit burns",
                new[] { ("T31.1", "Burns 12% TBSA", true) },
                new[] { "FBC", "UE" }, new (ImagingModality, string, string)[] { },
                new[] { ("Morphine", "10 mg/ml", "5 mg", "IV", "Q4H PRN", "2 days", 12, (string?)null) },
                true, false, rnd.Next(1, 21)),

            JourneyType.InpatientPneumonia => new(EncounterType.InpatientAdmission, ctx.OpdClinic.Id, Doc(),
                "Cough, fever, breathlessness", "Productive cough, fever 5 days, dyspnoea on exertion",
                "T 38.6°C, RR 28, SpO2 92%, crackles right base", "Community-acquired pneumonia (right lower lobe)",
                "Admit medical ward; IV antibiotics; oxygen therapy; chest physio",
                new[] { ("J18.9", "Pneumonia unspecified", true) },
                new[] { "FBC", "UE", "GLUCOSE" },
                new[] { (ImagingModality.XRay, "Chest X-ray PA", "Pneumonia confirmation") },
                new[] { ("Ceftriaxone", "1 g", "1 g", "IV", "OD", "7 days", 7, (string?)null) },
                true, true, rnd.Next(2, 14)),

            JourneyType.InpatientCellulitis => new(EncounterType.InpatientAdmission, ctx.OpdClinic.Id, Doc(),
                "Painful red leg", "Right lower leg redness and swelling 3 days, fever",
                "T 38.4°C, right calf erythema, warm, tender, no crepitus",
                "Cellulitis — right lower leg",
                "IV antibiotics; elevation; analgesia; mark erythema",
                new[] { ("L03.115", "Cellulitis of right lower limb", true) },
                new[] { "FBC", "UE", "GLUCOSE" }, new (ImagingModality, string, string)[] { },
                new[]
                {
                    ("Cloxacillin", "500 mg", "500 mg", "IV", "Q6H", "5 days", 20, (string?)null),
                    ("Paracetamol", "1 g", "1 g", "PO", "QDS", "5 days", 20, (string?)null)
                },
                true, false, rnd.Next(2, 14)),

            JourneyType.InpatientGastroenteritis => new(EncounterType.InpatientAdmission, ctx.OpdClinic.Id, Mo(),
                "Diarrhoea, vomiting", "Bloody diarrhoea ×8, vomiting, fever, food at street stall",
                "T 38.7°C, dry mucosae, BP 105/65, HR 110, abdomen tender", "Acute gastroenteritis — moderate dehydration",
                "IV fluids; ORS; metronidazole; stool culture; admit",
                new[] { ("A09", "Infectious gastroenteritis", true) },
                new[] { "FBC", "UE", "BLOOD_CULTURE" }, new (ImagingModality, string, string)[] { },
                new[] { ("Metronidazole", "500 mg", "500 mg", "IV", "Q8H", "5 days", 15, (string?)null) },
                true, false, rnd.Next(2, 14)),

            JourneyType.SurgicalAppendectomy => new(EncounterType.Emergency, ctx.AeClinic.Id, Cons(),
                "RIF pain ×24h", "Right iliac fossa pain, anorexia, low-grade fever",
                "T 37.8°C, McBurney's tenderness +ve, Rovsing +ve", "Acute appendicitis",
                "NPO; IV fluids; analgesia; theatre — appendectomy",
                new[] { ("K35.80", "Acute appendicitis", true) },
                new[] { "FBC", "UE", "URINALYSIS" },
                new[] { (ImagingModality.Ultrasound, "Abdominal USS", "Appendicitis") },
                new[] { ("Ceftriaxone", "1 g", "1 g", "IV", "OD", "3 days", 3, (string?)null) },
                true, true, rnd.Next(7, 30)),

            JourneyType.SurgicalCS => new(EncounterType.AntenatalVisit, ctx.AncClinic.Id, Cons(),
                "Term pregnancy — booked CS", "Booked elective CS — previous CS",
                "Term, cephalic, FHR 140, no labour", "Term pregnancy — elective lower-segment CS",
                "Pre-op workup; theatre; counselling",
                new[] { ("O82", "Encounter for caesarean delivery", true) },
                new[] { "FBC", "UE", "GLUCOSE" }, new (ImagingModality, string, string)[] { },
                new[] { ("Cefazolin", "2 g", "2 g", "IV", "STAT", "Pre-op", 1, (string?)"30 min before incision") },
                true, false, rnd.Next(1, 30)),

            JourneyType.SurgicalHernia => new(EncounterType.OutpatientFollowUp, ctx.OpdClinic.Id, Cons(),
                "Right groin swelling", "Right inguinal swelling, reducible, mild discomfort on lifting",
                "Right inguinal hernia, easily reducible, no signs of strangulation",
                "Right inguinal hernia — elective repair",
                "List for elective herniorrhaphy; pre-op workup",
                new[] { ("K40.90", "Inguinal hernia", true) },
                new[] { "FBC", "UE", "GLUCOSE" }, new (ImagingModality, string, string)[] { },
                Array.Empty<(string, string, string, string, string, string, int, string?)>(),
                true, false, rnd.Next(1, 60)),

            JourneyType.IcuPostOp => new(EncounterType.InpatientAdmission, ctx.OpdClinic.Id, Cons(),
                "Post-op ICU", "Post-op laparotomy for perforated peptic ulcer",
                "Sedated, ventilated, BP 100/60, HR 100, SpO2 96% on FiO2 0.4",
                "Post-op ICU care", "Sedation, vasopressor wean, antibiotics",
                new[] { ("K27.5", "Perforated peptic ulcer", true) },
                new[] { "FBC", "UE", "GLUCOSE" },
                new[] { (ImagingModality.XRay, "Chest X-ray AP supine", "ETT and line check") },
                new[]
                {
                    ("Piperacillin/Tazobactam", "4.5 g", "4.5 g", "IV", "Q8H", "7 days", 21, (string?)null),
                    ("Morphine infusion", "1 mg/ml", "5 mg/h", "IV", "Continuous", "48h", 1, (string?)null)
                },
                true, true, rnd.Next(2, 21)),

            JourneyType.DialysisCkd => new(EncounterType.Procedure, ctx.OpdClinic.Id, Cons(),
                "Routine haemodialysis session", "ESRD on regular haemodialysis ×2/week",
                "Stable, BP 140/85, dry weight 70 kg", "ESRD — maintenance haemodialysis",
                "4-hour session; UF 3 L; review post-dialysis",
                new[] { ("N18.5", "Chronic kidney disease stage 5", true) },
                new[] { "UE", "FBC" }, new (ImagingModality, string, string)[] { },
                Array.Empty<(string, string, string, string, string, string, int, string?)>(),
                true, false, rnd.Next(1, 14)),

            JourneyType.DeceasedAdmission => new(EncounterType.Emergency, ctx.AeClinic.Id, Cons(),
                "Acute decompensated heart failure", "Severe SOB, orthopnoea, leg swelling",
                "RR 32, SpO2 84%, JVP raised, bilateral basal crackles, peripheral oedema",
                "Acute decompensated heart failure",
                "O2; IV furosemide; admit; cardiology consult",
                new[] { ("I50.9", "Heart failure unspecified", true) },
                new[] { "FBC", "UE", "GLUCOSE" },
                new[] { (ImagingModality.XRay, "Chest X-ray PA", "Heart failure") },
                new[] { ("Furosemide", "20 mg/2 ml", "80 mg", "IV", "STAT then BD", "5 days", 10, (string?)null) },
                true, true, rnd.Next(2, 21)),

            // ANC, paeds, mortuary etc. handled in their own phases — no generic encounter here.
            _ => null
        };
    }

    private static async Task SeedEncountersAsync(ApplicationDbContext db, int fid, List<Patient> patients, SeedContext ctx, Random rnd, DateTime now)
    {
        var templates = Templates();
        var byTest = ctx.LabTests.ToDictionary(t => t.Code, t => t);

        for (var i = 0; i < templates.Count; i++)
        {
            var t = templates[i];
            var spec = BuildEncounter(t, ctx, rnd);
            if (spec is null) continue;

            var p = patients[i];
            var startedAt = now.AddDays(-spec.DaysAgo).AddHours(-rnd.Next(0, 8));

            var enc = new Encounter
            {
                FacilityId = fid,
                PatientId = p.Id,
                ClinicId = spec.ClinicId,
                ClinicianId = spec.Clinician,
                Type = spec.Type,
                Status = EncounterStatus.Signed,
                StartedAt = startedAt,
                SignedAt = startedAt.AddMinutes(15 + rnd.Next(0, 60)),
                ChiefComplaint = spec.CC,
                Soap = new SoapNote
                {
                    Subjective = spec.Subj,
                    Objective = spec.Obj,
                    Assessment = spec.Asses,
                    Plan = spec.Plan,
                    UpdatedAt = startedAt.AddMinutes(20),
                    UpdatedById = spec.Clinician
                }
            };
            foreach (var (icd, desc, primary) in spec.Dx)
                enc.Diagnoses.Add(new EncounterDiagnosis { IcdCode = icd, Description = desc, Status = DiagnosisStatus.Confirmed, IsPrimary = primary, CreatedById = spec.Clinician, CreatedAt = startedAt.AddMinutes(15) });

            // Lab orders
            foreach (var code in spec.LabCodes)
            {
                var test = byTest.GetValueOrDefault(code);
                var lab = new LabOrder
                {
                    EncounterId = 0, // set after encounter saved
                    PatientId = p.Id,
                    LabTestId = test?.Id,
                    TestName = test?.Name ?? code,
                    Specimen = test?.Specimen,
                    Status = spec.LabResults ? OrderStatus.Completed : OrderStatus.InProgress,
                    Urgency = spec.Type == EncounterType.Emergency ? OrderUrgency.Stat : OrderUrgency.Routine,
                    ClinicalIndication = spec.Asses,
                    OrderedAt = startedAt.AddMinutes(10),
                    OrderedById = spec.Clinician,
                    CollectedAt = spec.LabResults ? startedAt.AddMinutes(30) : null,
                    CollectedById = spec.LabResults ? ctx.LabTech.Id : null,
                    AccessionNumber = spec.LabResults ? $"LAB-{startedAt:yyMMdd}-{rnd.Next(1000, 9999)}" : null,
                    CompletedAt = spec.LabResults ? startedAt.AddHours(2) : null
                };

                if (spec.LabResults && test != null && test.Analytes.Any())
                {
                    var result = new LabResult
                    {
                        LabTestId = test.Id,
                        Status = LabResultStatus.Authorized,
                        EnteredAt = startedAt.AddHours(1),
                        EnteredById = ctx.LabTech.Id,
                        AuthorizedAt = startedAt.AddHours(1.5),
                        AuthorizedById = ctx.LabSci.Id,
                        Methodology = "Automated analyser"
                    };
                    foreach (var a in test.Analytes)
                    {
                        var (val, flag) = SimulateAnalyte(a, t, rnd);
                        result.Values.Add(new LabResultValue
                        {
                            LabAnalyteId = a.Id,
                            AnalyteName = a.Name,
                            Unit = a.Unit,
                            Value = val,
                            NumericValue = decimal.TryParse(val, out var n) ? n : null,
                            Flag = flag,
                            RefRangeDisplay = (a.RefLow.HasValue || a.RefHigh.HasValue) ? $"{a.RefLow}-{a.RefHigh}" : null
                        });
                    }
                    result.HasCriticalValue = result.Values.Any(v => v.Flag == AnalyteFlag.CriticalLow || v.Flag == AnalyteFlag.CriticalHigh);
                    lab.Result = result;
                }

                enc.LabOrders.Add(lab);
            }

            // Imaging orders
            foreach (var (mod, study, indication) in spec.Imaging)
            {
                var img = new ImagingOrder
                {
                    PatientId = p.Id,
                    Modality = mod,
                    StudyDescription = study,
                    ClinicalIndication = indication,
                    Status = spec.ImagingReported ? OrderStatus.Completed : OrderStatus.InProgress,
                    Urgency = spec.Type == EncounterType.Emergency ? OrderUrgency.Stat : OrderUrgency.Routine,
                    OrderedAt = startedAt.AddMinutes(15),
                    OrderedById = spec.Clinician,
                    AccessionNumber = spec.ImagingReported ? $"IMG-{startedAt:yyMMdd}-{rnd.Next(1000, 9999)}" : null,
                    CompletedAt = spec.ImagingReported ? startedAt.AddHours(3) : null
                };
                if (spec.ImagingReported)
                {
                    img.Report = new ImagingReport
                    {
                        Technique = "Standard",
                        Findings = $"{study} reviewed. No acute fracture. Mild bibasal opacities noted." ,
                        Impression = $"Findings consistent with {indication}",
                        Recommendation = "Clinical correlation",
                        PerformedAt = startedAt.AddHours(1),
                        PerformedById = ctx.Radiographer.Id,
                        ReportedAt = startedAt.AddHours(2),
                        ReportedById = ctx.Radiographer.Id,
                        AuthorizedAt = startedAt.AddHours(3),
                        AuthorizedById = ctx.Consultant.Id,
                        AccessionNumber = img.AccessionNumber
                    };
                }
                enc.ImagingOrders.Add(img);
            }

            // Prescription
            if (spec.Rx.Length > 0)
            {
                var rx = new Prescription
                {
                    PatientId = p.Id,
                    Status = PrescriptionStatus.Issued,
                    IssuedAt = startedAt.AddMinutes(20),
                    PrescribedById = spec.Clinician
                };
                foreach (var (drug, strength, dose, route, freq, duration, qty, instr) in spec.Rx)
                {
                    var dEnt = ctx.Drugs.FirstOrDefault(d => d.GenericName.StartsWith(drug.Split(' ')[0], StringComparison.OrdinalIgnoreCase));
                    rx.Items.Add(new PrescriptionItem
                    {
                        DrugId = dEnt?.Id,
                        DrugName = drug,
                        NafdacNumber = dEnt?.NafdacNumber,
                        Dose = dose,
                        Route = route,
                        Frequency = freq,
                        Duration = duration,
                        Quantity = qty,
                        Instructions = instr,
                        IsControlled = dEnt?.IsControlled ?? false
                    });
                }
                enc.Prescriptions.Add(rx);
            }

            db.Encounters.Add(enc);
        }
        await db.SaveChangesAsync();
    }

    /// <summary>Generate a plausible analyte result. Deterministic per template by Random seed.</summary>
    private static (string value, AnalyteFlag flag) SimulateAnalyte(LabAnalyte a, PatientTemplate t, Random rnd)
    {
        if (!a.RefLow.HasValue && !a.RefHigh.HasValue)
            return ("WNR", AnalyteFlag.Normal);
        var lo = (double)(a.RefLow ?? 0m);
        var hi = a.RefHigh.HasValue ? (double)a.RefHigh.Value : lo + 5;
        var span = hi - lo;
        var roll = rnd.NextDouble();
        // Skew abnormal for journeys that should be abnormal
        bool wantHigh = false, wantLow = false;
        switch (a.Name.ToLowerInvariant())
        {
            case var n when n.Contains("haemoglobin") || n.Contains("hb"):
                if (t.Journey == JourneyType.OpdAnaemia || t.Journey == JourneyType.AeSepsis) wantLow = true; break;
            case var n when n.Contains("glucose") || n.Contains("fbs"):
                if (t.Journey == JourneyType.OpdDiabetes || t.Journey == JourneyType.InpatientCellulitis) wantHigh = true; break;
            case var n when n.Contains("urea") || n.Contains("creatinine"):
                if (t.Journey == JourneyType.DialysisCkd || t.Journey == JourneyType.AeSepsis) wantHigh = true; break;
            case var n when n.Contains("wbc") || n.Contains("white"):
                if (t.Journey == JourneyType.AeSepsis || t.Journey == JourneyType.InpatientCellulitis || t.Journey == JourneyType.InpatientPneumonia) wantHigh = true; break;
            case var n when n.Contains("hba1c"):
                if (t.Journey == JourneyType.OpdDiabetes) wantHigh = true; break;
        }
        double v;
        AnalyteFlag flag = AnalyteFlag.Normal;
        if (wantHigh)
        {
            v = hi + span * (0.2 + rnd.NextDouble() * 0.6);
            flag = a.CriticalHigh.HasValue && v >= (double)a.CriticalHigh.Value ? AnalyteFlag.CriticalHigh : AnalyteFlag.High;
        }
        else if (wantLow)
        {
            v = Math.Max(0, lo - span * (0.1 + rnd.NextDouble() * 0.4));
            flag = a.CriticalLow.HasValue && v <= (double)a.CriticalLow.Value ? AnalyteFlag.CriticalLow : AnalyteFlag.Low;
        }
        else
        {
            v = lo + span * (0.2 + rnd.NextDouble() * 0.6);
        }
        return (v.ToString("F1"), flag);
    }
    // ====================================================================================
    // Phase 3: bills + payments + claims + cashier shift
    // ====================================================================================
    private static async Task SeedFinanceAsync(ApplicationDbContext db, int fid, List<Patient> patients, SeedContext ctx, Random rnd, DateTime now)
    {
        var todayShift = new ThriveHealth.Web.Models.Billing.CashierShift
        {
            FacilityId = fid,
            CashierId = ctx.Cashier.Id,
            ShiftNumber = $"SHIFT-{now:yyyyMMdd}-001",
            Status = ThriveHealth.Web.Models.Billing.CashierShiftStatus.Open,
            OpeningFloat = 50_000m,
            OpenedAt = now.Date.AddHours(8)
        };
        var yesterdayShift = new ThriveHealth.Web.Models.Billing.CashierShift
        {
            FacilityId = fid,
            CashierId = ctx.Cashier.Id,
            ShiftNumber = $"SHIFT-{now.AddDays(-1):yyyyMMdd}-001",
            Status = ThriveHealth.Web.Models.Billing.CashierShiftStatus.Closed,
            OpeningFloat = 50_000m,
            CountedCash = 215_000m,
            Variance = 0m,
            OpenedAt = now.Date.AddDays(-1).AddHours(8),
            ClosedAt = now.Date.AddDays(-1).AddHours(17)
        };
        db.CashierShifts.AddRange(todayShift, yesterdayShift);
        await db.SaveChangesAsync();

        var encounters = await db.Encounters.AsNoTracking()
            .Where(e => e.FacilityId == fid)
            .Include(e => e.LabOrders)
            .Include(e => e.ImagingOrders)
            .Include(e => e.Prescriptions).ThenInclude(p => p.Items)
            .ToListAsync();

        var billCounter = 1;
        var receiptCounter = 1;
        var claimCounter = 1;

        var insured = await db.PatientPayers.AsNoTracking().ToListAsync();

        foreach (var enc in encounters)
        {
            var bill = new ThriveHealth.Web.Models.Billing.Bill
            {
                FacilityId = fid,
                BillNumber = $"BILL-{enc.StartedAt:yyyyMM}-{billCounter++:D5}",
                PatientId = enc.PatientId,
                EncounterId = enc.Id,
                ServiceDate = DateOnly.FromDateTime(enc.StartedAt),
                CreatedAt = enc.StartedAt.AddMinutes(40),
                Status = ThriveHealth.Web.Models.Billing.BillStatus.Open,
                CreatedById = ctx.Receptionist.Id
            };

            decimal consultPrice = enc.Type == EncounterType.Emergency ? 7_500m : enc.Type == EncounterType.Telemedicine ? 3_500m : 5_000m;
            bill.Items.Add(BItem(ThriveHealth.Web.Models.Billing.BillItemKind.Consultation, $"Consultation — {enc.Type}", consultPrice, 1));

            foreach (var lab in enc.LabOrders)
            {
                var labTest = ctx.LabTests.FirstOrDefault(t => t.Id == lab.LabTestId);
                var price = labTest?.Price ?? 1_500m;
                bill.Items.Add(BItem(ThriveHealth.Web.Models.Billing.BillItemKind.Lab, "Lab: " + lab.TestName, price, 1, labOrderId: lab.Id));
            }

            foreach (var img in enc.ImagingOrders)
            {
                decimal price = img.Modality switch
                {
                    ThriveHealth.Web.Models.Clinical.ImagingModality.XRay => 6_000m,
                    ThriveHealth.Web.Models.Clinical.ImagingModality.Ultrasound => 8_000m,
                    ThriveHealth.Web.Models.Clinical.ImagingModality.CT => 35_000m,
                    ThriveHealth.Web.Models.Clinical.ImagingModality.MRI => 65_000m,
                    _ => 5_000m
                };
                bill.Items.Add(BItem(ThriveHealth.Web.Models.Billing.BillItemKind.Imaging, $"{img.Modality}: {img.StudyDescription}", price, 1, imagingOrderId: img.Id));
            }

            foreach (var rx in enc.Prescriptions)
            foreach (var i in rx.Items)
            {
                var drug = ctx.Drugs.FirstOrDefault(d => d.Id == i.DrugId);
                var unit = drug?.UnitPrice ?? 50m;
                bill.Items.Add(BItem(ThriveHealth.Web.Models.Billing.BillItemKind.Drug, $"{i.DrugName} {i.Dose}", unit, i.Quantity ?? 1, prescriptionItemId: i.Id));
            }

            foreach (var it in bill.Items)
            {
                it.LineTotal = it.UnitPrice * it.Quantity;
                it.LineNet = it.LineTotal - it.LineDiscount;
            }
            bill.GrossAmount = bill.Items.Sum(i => i.LineTotal);
            bill.DiscountAmount = bill.Items.Sum(i => i.LineDiscount);
            bill.NetAmount = bill.GrossAmount - bill.DiscountAmount;

            var insurance = insured.FirstOrDefault(ip => ip.PatientId == enc.PatientId && ip.IsPrimary);
            var daysAgo = (now - enc.StartedAt).TotalDays;

            decimal paid;
            ThriveHealth.Web.Models.Billing.BillStatus status;
            if (daysAgo > 7) { paid = bill.NetAmount; status = ThriveHealth.Web.Models.Billing.BillStatus.Paid; }
            else if (daysAgo > 2) { paid = Math.Round(bill.NetAmount * (decimal)(0.4 + rnd.NextDouble() * 0.5), 0); status = paid >= bill.NetAmount ? ThriveHealth.Web.Models.Billing.BillStatus.Paid : ThriveHealth.Web.Models.Billing.BillStatus.PartiallyPaid; }
            else { paid = 0; status = ThriveHealth.Web.Models.Billing.BillStatus.Open; }

            bill.PaidAmount = paid;
            bill.Status = status;
            if (status == ThriveHealth.Web.Models.Billing.BillStatus.Paid)
                bill.ClosedAt = enc.StartedAt.AddMinutes(80);

            if (paid > 0)
            {
                var method = (ThriveHealth.Web.Models.Billing.PaymentMethod)(rnd.Next(1, 5));
                bill.Payments.Add(new ThriveHealth.Web.Models.Billing.Payment
                {
                    ReceiptNumber = $"RC-{enc.StartedAt:yyyyMMdd}-{receiptCounter++:D5}",
                    Method = method,
                    Status = ThriveHealth.Web.Models.Billing.PaymentStatus.Recorded,
                    Amount = paid,
                    Reference = method != ThriveHealth.Web.Models.Billing.PaymentMethod.Cash ? $"REF{rnd.Next(100000, 999999)}" : null,
                    ReceivedAt = enc.StartedAt.AddMinutes(60),
                    CashierId = ctx.Cashier.Id,
                    CashierShiftId = daysAgo < 1 ? todayShift.Id : (daysAgo < 2 ? yesterdayShift.Id : (int?)null)
                });
            }

            db.Bills.Add(bill);

            if (insurance != null && status != ThriveHealth.Web.Models.Billing.BillStatus.Open && bill.NetAmount > 0)
            {
                var claimStatus = daysAgo switch
                {
                    < 3 => ClaimStatus.Draft,
                    < 7 => ClaimStatus.Submitted,
                    < 21 => ClaimStatus.PartiallyPaid,
                    _ => ClaimStatus.Paid
                };

                var copay = Math.Round(bill.NetAmount * 0.1m, 0);
                var claim = new Claim
                {
                    FacilityId = fid,
                    PayerId = insurance.PayerId!.Value,
                    PayerPlanId = insurance.PayerPlanId,
                    PatientId = enc.PatientId,
                    EncounterId = enc.Id,
                    ClaimReference = $"CLM-{enc.StartedAt:yyyyMM}-{claimCounter++:D4}",
                    ServiceDate = bill.ServiceDate,
                    Status = claimStatus,
                    CreatedAt = enc.StartedAt.AddDays(1),
                    CreatedById = ctx.ClaimsOfficer.Id,
                    GrossAmount = bill.GrossAmount,
                    CopayAmount = copay,
                    ClaimableAmount = bill.NetAmount - copay,
                    ApprovedAmount = claimStatus >= ClaimStatus.PartiallyPaid ? bill.NetAmount - copay : 0,
                    PaidAmount = claimStatus == ClaimStatus.Paid ? bill.NetAmount - copay : (claimStatus == ClaimStatus.PartiallyPaid ? Math.Round((bill.NetAmount - copay) * 0.5m, 0) : 0),
                    SubmittedAt = claimStatus >= ClaimStatus.Submitted ? enc.StartedAt.AddDays(2) : (DateTime?)null,
                    SubmittedById = claimStatus >= ClaimStatus.Submitted ? ctx.ClaimsOfficer.Id : null,
                    RespondedAt = claimStatus >= ClaimStatus.PartiallyPaid ? enc.StartedAt.AddDays(7) : (DateTime?)null,
                    PayerReference = claimStatus >= ClaimStatus.Submitted ? $"PR{rnd.Next(100000, 999999)}" : null,
                    AuthorizationCode = insurance.AuthorizationCode
                };

                foreach (var bi in bill.Items)
                {
                    claim.Items.Add(new ClaimItem
                    {
                        Kind = (ClaimItemKind)bi.Kind,
                        Description = bi.Description,
                        Quantity = bi.Quantity,
                        UnitPrice = bi.UnitPrice,
                        LineTotal = bi.LineTotal,
                        CopayAmount = Math.Round(bi.LineNet * 0.1m, 0),
                        ClaimableAmount = bi.LineNet - Math.Round(bi.LineNet * 0.1m, 0),
                        ApprovedAmount = claimStatus >= ClaimStatus.PartiallyPaid ? bi.LineNet - Math.Round(bi.LineNet * 0.1m, 0) : 0
                    });
                }

                db.Claims.Add(claim);
            }
        }

        await db.SaveChangesAsync();
    }

    private static ThriveHealth.Web.Models.Billing.BillItem BItem(
        ThriveHealth.Web.Models.Billing.BillItemKind kind, string desc, decimal unit, int qty,
        int? labOrderId = null, int? imagingOrderId = null, int? prescriptionItemId = null) =>
        new()
        {
            Kind = kind,
            Description = desc,
            Quantity = qty,
            UnitPrice = unit,
            LineTotal = unit * qty,
            LineNet = unit * qty,
            LabOrderId = labOrderId,
            ImagingOrderId = imagingOrderId,
            PrescriptionItemId = prescriptionItemId
        };
    // ====================================================================================
    // Phase 4: appointments + queue + roster + leave requests + ticket counter
    // ====================================================================================
    private static async Task SeedSchedulingAsync(ApplicationDbContext db, int fid, List<Patient> patients, SeedContext ctx, Random rnd, DateTime now)
    {
        var today = DateOnly.FromDateTime(now);
        var todayStart = now.Date;

        // Today's appointments — 12 across the day, mostly OPD + some specialty
        var apptCounter = 1;
        for (var i = 0; i < 12; i++)
        {
            var p = patients[i % patients.Count];
            var hour = 8 + i;
            var clinic = i % 3 == 0 ? ctx.AncClinic : i % 4 == 0 ? ctx.PaedClinic : ctx.OpdClinic;
            var status = i < 3 ? ThriveHealth.Web.Models.Scheduling.AppointmentStatus.Completed
                       : i < 6 ? ThriveHealth.Web.Models.Scheduling.AppointmentStatus.CheckedIn
                       : ThriveHealth.Web.Models.Scheduling.AppointmentStatus.Scheduled;
            db.Appointments.Add(new ThriveHealth.Web.Models.Scheduling.Appointment
            {
                FacilityId = fid,
                PatientId = p.Id,
                ClinicId = clinic.Id,
                ClinicianId = i % 2 == 0 ? ctx.Doctor.Id : ctx.Consultant.Id,
                Type = ThriveHealth.Web.Models.Scheduling.AppointmentType.NewOpd,
                Status = status,
                Priority = i == 0 ? ThriveHealth.Web.Models.Scheduling.AppointmentPriority.Urgent : ThriveHealth.Web.Models.Scheduling.AppointmentPriority.Routine,
                Channel = ThriveHealth.Web.Models.Scheduling.BookingChannel.FrontDesk,
                ScheduledStartUtc = todayStart.AddHours(hour),
                DurationMinutes = 15,
                ReasonForVisit = "Routine review",
                BookedById = ctx.Receptionist.Id,
                CheckedInAt = status >= ThriveHealth.Web.Models.Scheduling.AppointmentStatus.CheckedIn ? todayStart.AddHours(hour).AddMinutes(-10) : null,
                StartedAt = status == ThriveHealth.Web.Models.Scheduling.AppointmentStatus.Completed ? todayStart.AddHours(hour) : null,
                CompletedAt = status == ThriveHealth.Web.Models.Scheduling.AppointmentStatus.Completed ? todayStart.AddHours(hour).AddMinutes(20) : null,
                CreatedAt = todayStart.AddDays(-2)
            });
            apptCounter++;
        }
        // A few next-week appointments
        for (var i = 0; i < 6; i++)
        {
            var p = patients[(i + 12) % patients.Count];
            db.Appointments.Add(new ThriveHealth.Web.Models.Scheduling.Appointment
            {
                FacilityId = fid,
                PatientId = p.Id,
                ClinicId = ctx.OpdClinic.Id,
                ClinicianId = ctx.Doctor.Id,
                Type = ThriveHealth.Web.Models.Scheduling.AppointmentType.FollowUp,
                Status = ThriveHealth.Web.Models.Scheduling.AppointmentStatus.Scheduled,
                Priority = ThriveHealth.Web.Models.Scheduling.AppointmentPriority.Routine,
                Channel = ThriveHealth.Web.Models.Scheduling.BookingChannel.FrontDesk,
                ScheduledStartUtc = todayStart.AddDays(rnd.Next(2, 8)).AddHours(9 + rnd.Next(0, 5)),
                DurationMinutes = 15,
                ReasonForVisit = "Follow-up",
                BookedById = ctx.Receptionist.Id,
                CreatedAt = todayStart.AddDays(-1)
            });
        }

        // Queue (today) — a handful in various states
        var ticketSeq = 0;
        var statuses = new[]
        {
            ThriveHealth.Web.Models.Scheduling.QueueStatus.Waiting,
            ThriveHealth.Web.Models.Scheduling.QueueStatus.Triaged,
            ThriveHealth.Web.Models.Scheduling.QueueStatus.Called,
            ThriveHealth.Web.Models.Scheduling.QueueStatus.InConsultation,
            ThriveHealth.Web.Models.Scheduling.QueueStatus.Completed
        };
        for (var i = 0; i < 8; i++)
        {
            ticketSeq++;
            var p = patients[i + 4];
            var s = statuses[i % statuses.Length];
            db.QueueEntries.Add(new ThriveHealth.Web.Models.Scheduling.QueueEntry
            {
                FacilityId = fid,
                PatientId = p.Id,
                ClinicId = ctx.OpdClinic.Id,
                ClinicianId = i % 2 == 0 ? ctx.Doctor.Id : ctx.Consultant.Id,
                TicketNumber = $"OPD-{today:yyyyMMdd}-{ticketSeq:D3}",
                TicketDate = today,
                Priority = i == 0 ? ThriveHealth.Web.Models.Scheduling.AppointmentPriority.Urgent : ThriveHealth.Web.Models.Scheduling.AppointmentPriority.Routine,
                Status = s,
                CheckedInAt = todayStart.AddHours(9).AddMinutes(i * 7),
                TriagedAt = s >= ThriveHealth.Web.Models.Scheduling.QueueStatus.Triaged ? todayStart.AddHours(9).AddMinutes(i * 7 + 5) : null,
                CalledAt = s >= ThriveHealth.Web.Models.Scheduling.QueueStatus.Called ? todayStart.AddHours(9).AddMinutes(i * 7 + 15) : null,
                ConsultStartedAt = s >= ThriveHealth.Web.Models.Scheduling.QueueStatus.InConsultation ? todayStart.AddHours(9).AddMinutes(i * 7 + 20) : null,
                CompletedAt = s == ThriveHealth.Web.Models.Scheduling.QueueStatus.Completed ? todayStart.AddHours(9).AddMinutes(i * 7 + 40) : null,
                CheckedInById = ctx.Receptionist.Id,
                TriagedById = s >= ThriveHealth.Web.Models.Scheduling.QueueStatus.Triaged ? ctx.Nurse.Id : null,
                TriageMews = s >= ThriveHealth.Web.Models.Scheduling.QueueStatus.Triaged ? rnd.Next(0, 4) : null,
                Complaint = "OPD walk-in"
            });
        }

        // Ticket counter row
        db.TicketCounters.Add(new ThriveHealth.Web.Models.Scheduling.TicketCounter
        {
            FacilityId = fid,
            ClinicId = ctx.OpdClinic.Id,
            Date = today,
            LastSequence = ticketSeq
        });

        // Roster — this week's shifts for clinical staff
        var roster = new[]
        {
            (ctx.Doctor, ShiftType.Morning, ctx.OpdClinic.Id, (int?)null),
            (ctx.Consultant, ShiftType.Morning, ctx.OpdClinic.Id, (int?)null),
            (ctx.Mo, ShiftType.Afternoon, ctx.AeClinic.Id, (int?)null),
            (ctx.Nurse, ShiftType.Morning, (int?)null, (int?)ctx.Wards.First().Id),
            (ctx.Nurse, ShiftType.Night, (int?)null, (int?)ctx.Wards.First().Id),
            (ctx.Midwife, ShiftType.Morning, ctx.AncClinic.Id, (int?)null),
            (ctx.LabSci, ShiftType.Morning, (int?)null, (int?)null),
            (ctx.Radiographer, ShiftType.Morning, (int?)null, (int?)null),
            (ctx.Pharmacist, ShiftType.Morning, (int?)null, (int?)null),
        };
        for (var d = 0; d < 7; d++)
        {
            var date = today.AddDays(d);
            foreach (var (staff, shiftType, clinicId, wardId) in roster)
            {
                if ((d + (int)shiftType) % 3 == 0) continue; // sparser distribution
                db.RosterShifts.Add(new RosterShift
                {
                    FacilityId = fid,
                    StaffId = staff.Id,
                    Date = date,
                    ShiftType = shiftType,
                    ClinicId = clinicId,
                    WardId = wardId,
                    Assignment = clinicId.HasValue ? "Clinic duty" : wardId.HasValue ? "Ward duty" : "On-call",
                    CreatedById = ctx.Cno.Id,
                    CreatedAt = todayStart.AddDays(-7)
                });
            }
        }

        // Leave requests
        db.LeaveRequests.AddRange(
            new LeaveRequest
            {
                StaffId = ctx.Nurse.Id, Type = LeaveType.Annual, Status = LeaveStatus.Approved,
                StartDate = today.AddDays(14), EndDate = today.AddDays(20), Days = 7,
                Reason = "Annual leave — family travel",
                CreatedAt = todayStart.AddDays(-10),
                DecidedAt = todayStart.AddDays(-7),
                DecidedById = ctx.Cno.Id,
                DecisionNotes = "Approved subject to ward cover"
            },
            new LeaveRequest
            {
                StaffId = ctx.Mo.Id, Type = LeaveType.Sick, Status = LeaveStatus.Approved,
                StartDate = today.AddDays(-3), EndDate = today.AddDays(-1), Days = 3,
                Reason = "Acute viral illness", CreatedAt = todayStart.AddDays(-3),
                DecidedAt = todayStart.AddDays(-3), DecidedById = ctx.Cno.Id
            },
            new LeaveRequest
            {
                StaffId = ctx.LabTech.Id, Type = LeaveType.Annual, Status = LeaveStatus.Submitted,
                StartDate = today.AddDays(28), EndDate = today.AddDays(35), Days = 8,
                Reason = "Annual leave", CreatedAt = todayStart.AddDays(-1)
            }
        );

        await db.SaveChangesAsync();
    }
    // ====================================================================================
    // Phase 5: admissions + MAR slots + fluids + nursing notes + ward rounds + bed allocation
    // ====================================================================================
    private static async Task SeedInpatientAsync(ApplicationDbContext db, int fid, List<Patient> patients, SeedContext ctx, Random rnd, DateTime now)
    {
        var templates = Templates();
        // Match against Code or Name — wards are coded MMW/FMW/PAED/MAT/ICU
        bool MatchWard(ThriveHealth.Web.Models.Inpatient.Ward w, params string[] keys) =>
            keys.Any(k => w.Code.Contains(k, StringComparison.OrdinalIgnoreCase) || w.Name.Contains(k, StringComparison.OrdinalIgnoreCase));

        var maleWard = ctx.Wards.FirstOrDefault(w => MatchWard(w, "Male", "MMW")) ?? ctx.Wards.First();
        var femaleWard = ctx.Wards.FirstOrDefault(w => MatchWard(w, "Female", "FMW")) ?? maleWard;
        var paedsWard = ctx.Wards.FirstOrDefault(w => MatchWard(w, "Paed", "PAED")) ?? maleWard;
        var matWard = ctx.Wards.FirstOrDefault(w => MatchWard(w, "Matern", "MAT")) ?? femaleWard;
        var icuWard = ctx.Wards.FirstOrDefault(w => MatchWard(w, "ICU", "Intensive")) ?? maleWard;

        var encByPatient = await db.Encounters.AsNoTracking()
            .Where(e => e.FacilityId == fid)
            .GroupBy(e => e.PatientId)
            .Select(g => new { PatientId = g.Key, Id = g.OrderByDescending(x => x.StartedAt).First().Id })
            .ToDictionaryAsync(g => g.PatientId, g => g.Id);

        ThriveHealth.Web.Models.Inpatient.Ward PickWard(PatientTemplate t)
        {
            return t.Journey switch
            {
                JourneyType.AeChildSeizure or JourneyType.PaedsAcute or JourneyType.PaedsMalnutrition => paedsWard,
                JourneyType.AncDelivered or JourneyType.SurgicalCS => matWard,
                JourneyType.IcuPostOp => icuWard,
                _ => t.Sex == Sex.Male ? maleWard : femaleWard
            };
        }

        var bedsUsed = new HashSet<int>();
        ThriveHealth.Web.Models.Inpatient.Bed? PickBed(ThriveHealth.Web.Models.Inpatient.Ward w)
        {
            var b = w.Beds.FirstOrDefault(b => !bedsUsed.Contains(b.Id) && b.Status == ThriveHealth.Web.Models.Inpatient.BedStatus.Free);
            if (b != null) bedsUsed.Add(b.Id);
            return b;
        }

        // Active admissions: 5 patients still admitted; 2 already discharged; 1 deceased
        var inpatientJourneys = new[]
        {
            (JourneyType.InpatientPneumonia, 4, false, ThriveHealth.Web.Models.Inpatient.AdmissionStatus.Active, (ThriveHealth.Web.Models.Inpatient.DischargeDisposition?)null),
            (JourneyType.InpatientCellulitis, 3, false, ThriveHealth.Web.Models.Inpatient.AdmissionStatus.Active, (ThriveHealth.Web.Models.Inpatient.DischargeDisposition?)null),
            (JourneyType.InpatientGastroenteritis, 5, true,  ThriveHealth.Web.Models.Inpatient.AdmissionStatus.Discharged, (ThriveHealth.Web.Models.Inpatient.DischargeDisposition?)ThriveHealth.Web.Models.Inpatient.DischargeDisposition.Home),
            (JourneyType.AeRta,                   2, false, ThriveHealth.Web.Models.Inpatient.AdmissionStatus.Active, (ThriveHealth.Web.Models.Inpatient.DischargeDisposition?)null),
            (JourneyType.AeMiSuspected,           6, false, ThriveHealth.Web.Models.Inpatient.AdmissionStatus.Active, (ThriveHealth.Web.Models.Inpatient.DischargeDisposition?)null),
            (JourneyType.AeSepsis,                4, false, ThriveHealth.Web.Models.Inpatient.AdmissionStatus.Active, (ThriveHealth.Web.Models.Inpatient.DischargeDisposition?)null),
            (JourneyType.AeStroke,                7, true,  ThriveHealth.Web.Models.Inpatient.AdmissionStatus.Discharged, (ThriveHealth.Web.Models.Inpatient.DischargeDisposition?)ThriveHealth.Web.Models.Inpatient.DischargeDisposition.Home),
            (JourneyType.IcuPostOp,               3, false, ThriveHealth.Web.Models.Inpatient.AdmissionStatus.Active, (ThriveHealth.Web.Models.Inpatient.DischargeDisposition?)null),
            (JourneyType.DeceasedAdmission,       2, true,  ThriveHealth.Web.Models.Inpatient.AdmissionStatus.Deceased, (ThriveHealth.Web.Models.Inpatient.DischargeDisposition?)ThriveHealth.Web.Models.Inpatient.DischargeDisposition.Deceased),
        };

        var bedsToUpdate = new List<ThriveHealth.Web.Models.Inpatient.Bed>();

        foreach (var (journey, daysAgo, discharged, status, disp) in inpatientJourneys)
        {
            var idx = templates.ToList().FindIndex(t => t.Journey == journey);
            if (idx < 0) continue;
            var p = patients[idx];
            var t = templates[idx];
            var ward = PickWard(t);
            var bed = PickBed(ward);
            if (bed is null) continue;

            var admittedAt = now.AddDays(-daysAgo);
            var dischargedAt = discharged ? now.AddDays(-rnd.Next(0, daysAgo - 1)) : (DateTime?)null;

            var adm = new ThriveHealth.Web.Models.Inpatient.Admission
            {
                FacilityId = fid,
                PatientId = p.Id,
                WardId = ward.Id,
                BedId = bed.Id,
                AdmittingDoctorId = ctx.Consultant.Id,
                SourceEncounterId = encByPatient.GetValueOrDefault(p.Id),
                AdmissionEncounterId = encByPatient.GetValueOrDefault(p.Id),
                ReasonForAdmission = t.Journey.ToString(),
                WorkingDiagnosis = "See encounter",
                Status = status,
                AdmittedAt = admittedAt,
                DischargedAt = dischargedAt,
                DischargeDisposition = disp,
                DischargeDiagnosis = discharged ? "Improved" : null,
                DischargeSummary = discharged ? "Patient improved on treatment. Discharged stable." : null,
                FollowUpPlan = discharged ? "GOPD review in 2 weeks" : null,
                DischargedById = discharged ? ctx.Consultant.Id : null
            };

            adm.BedHistory.Add(new ThriveHealth.Web.Models.Inpatient.BedAllocation
            {
                BedId = bed.Id,
                FromUtc = admittedAt,
                ToUtc = dischargedAt,
                Reason = "Initial admission",
                AllocatedById = ctx.Nurse.Id
            });

            // Inpatient meds with MAR slots
            var med = new ThriveHealth.Web.Models.Inpatient.InpatientMedication
            {
                DrugName = "Ceftriaxone",
                Strength = "1 g",
                Dose = "1 g",
                Route = "IV",
                Frequency = "OD",
                Kind = ThriveHealth.Web.Models.Inpatient.InpatientMedicationKind.Regular,
                Status = ThriveHealth.Web.Models.Inpatient.InpatientMedicationStatus.Active,
                StartUtc = admittedAt.AddHours(2),
                EndUtc = admittedAt.AddDays(7),
                PrescribedById = ctx.Consultant.Id,
                PrescribedAt = admittedAt.AddHours(1)
            };
            // Generate MAR slots — daily for stay duration so far
            var stayDays = (int)((dischargedAt ?? now) - admittedAt).TotalDays;
            for (var d = 0; d < Math.Min(stayDays, 7); d++)
            {
                var slotTime = admittedAt.AddDays(d).AddHours(8);
                med.Slots.Add(new ThriveHealth.Web.Models.Inpatient.MarSlot
                {
                    ScheduledUtc = slotTime,
                    Status = slotTime <= now.AddHours(-1) ? ThriveHealth.Web.Models.Inpatient.MarSlotStatus.Given : ThriveHealth.Web.Models.Inpatient.MarSlotStatus.Scheduled,
                    AdministeredUtc = slotTime <= now.AddHours(-1) ? slotTime.AddMinutes(rnd.Next(-15, 15)) : null,
                    AdministeredById = slotTime <= now.AddHours(-1) ? ctx.Nurse.Id : null,
                    ActualDose = slotTime <= now.AddHours(-1) ? "1 g" : null,
                    Route = "IV"
                });
            }
            adm.Medications.Add(med);

            // Fluids — last 24h
            var fluidStart = (now.AddHours(-24) > admittedAt) ? now.AddHours(-24) : admittedAt;
            for (var h = 0; h < 6 && fluidStart.AddHours(h * 4) < (dischargedAt ?? now); h++)
            {
                adm.Fluids.Add(new ThriveHealth.Web.Models.Inpatient.FluidEntry
                {
                    Kind = ThriveHealth.Web.Models.Inpatient.FluidKind.Input,
                    Type = ThriveHealth.Web.Models.Inpatient.FluidType.IvCrystalloid,
                    VolumeMl = 250 + rnd.Next(0, 150),
                    Description = "Normal saline",
                    RecordedUtc = fluidStart.AddHours(h * 4),
                    RecordedById = ctx.Nurse.Id
                });
                adm.Fluids.Add(new ThriveHealth.Web.Models.Inpatient.FluidEntry
                {
                    Kind = ThriveHealth.Web.Models.Inpatient.FluidKind.Output,
                    Type = ThriveHealth.Web.Models.Inpatient.FluidType.Urine,
                    VolumeMl = 200 + rnd.Next(0, 200),
                    Description = "Urine",
                    RecordedUtc = fluidStart.AddHours(h * 4 + 1),
                    RecordedById = ctx.Nurse.Id
                });
            }

            // Nursing notes — daily
            for (var d = 0; d < Math.Min(stayDays, 5); d++)
            {
                adm.NursingNotes.Add(new ThriveHealth.Web.Models.Inpatient.NursingNote
                {
                    Shift = d % 2 == 0 ? "Morning" : "Night",
                    Body = d == 0 ? "Patient admitted, vitals stable, IV access established. Plan reviewed with patient and family."
                          : d < 2 ? "Patient comfortable. Tolerating oral feeds. Vitals stable."
                          : "Steady improvement. Mobilising with assistance. Awaiting consultant review.",
                    Handover = d % 2 == 1 ? "Continue current treatment, monitor temp and BP." : null,
                    RecordedUtc = admittedAt.AddDays(d).AddHours(d % 2 == 0 ? 7 : 19),
                    RecordedById = ctx.Nurse.Id
                });
            }

            // Ward rounds
            for (var d = 0; d < Math.Min(stayDays, 4); d++)
            {
                adm.WardRounds.Add(new ThriveHealth.Web.Models.Inpatient.WardRoundEntry
                {
                    Body = d == 0 ? "Initial assessment. Working diagnosis confirmed. Antibiotics commenced."
                          : "Improving. Continue current management. Reassess in 24h.",
                    PlanChanges = d == 1 ? "Switch IV to oral antibiotics tomorrow if stable." : null,
                    RecordedUtc = admittedAt.AddDays(d).AddHours(9),
                    RecordedById = ctx.Consultant.Id
                });
            }

            db.Admissions.Add(adm);
            // Mark bed occupied (or back to free if discharged)
            if (status == ThriveHealth.Web.Models.Inpatient.AdmissionStatus.Active)
                bed.Status = ThriveHealth.Web.Models.Inpatient.BedStatus.Occupied;
            bedsToUpdate.Add(bed);
        }

        db.Beds.UpdateRange(bedsToUpdate);
        await db.SaveChangesAsync();
    }
    // ====================================================================================
    // Phase 6: theatre sessions + ICU chart entries + dialysis sessions
    // ====================================================================================
    private static async Task SeedTheatreIcuAsync(ApplicationDbContext db, int fid, List<Patient> patients, SeedContext ctx, Random rnd, DateTime now)
    {
        var templates = Templates();
        var theatreList = ctx.Theatres;
        if (theatreList.Count == 0) return;
        var t1 = theatreList.First();
        var t2 = theatreList.Count > 1 ? theatreList[1] : t1;

        // Theatre sessions: completed (appendectomy), in-progress (CS), scheduled (hernia)
        var appendIdx = templates.ToList().FindIndex(t => t.Journey == JourneyType.SurgicalAppendectomy);
        var csIdx = templates.ToList().FindIndex(t => t.Journey == JourneyType.SurgicalCS);
        var herniaIdx = templates.ToList().FindIndex(t => t.Journey == JourneyType.SurgicalHernia);

        if (appendIdx >= 0)
        {
            var p = patients[appendIdx];
            var startUtc = now.AddDays(-3).AddHours(-2);
            db.TheatreSessions.Add(new ThriveHealth.Web.Models.Theatre.TheatreSession
            {
                FacilityId = fid, TheatreId = t1.Id, PatientId = p.Id,
                LeadSurgeonId = ctx.Consultant.Id,
                AnaesthetistId = ctx.Doctor.Id, ScrubNurseId = ctx.Nurse.Id,
                ProcedureName = "Open appendectomy", CptCode = "44970",
                Urgency = ThriveHealth.Web.Models.Theatre.CaseUrgency.Urgent,
                Anaesthesia = ThriveHealth.Web.Models.Theatre.AnaesthesiaType.GeneralAnaesthesia,
                Status = ThriveHealth.Web.Models.Theatre.TheatreSessionStatus.Completed,
                ScheduledStartUtc = startUtc,
                EstimatedMinutes = 60,
                PreOpAt = startUtc.AddMinutes(-30),
                KnifeOnSkinAt = startUtc, KnifeOffSkinAt = startUtc.AddMinutes(55),
                RecoveryAt = startUtc.AddMinutes(60), CompletedAt = startUtc.AddMinutes(120),
                Indication = "Acute appendicitis", PreOpAssessment = "ASA II, fasted >6h",
                OperativeNote = "Inflamed appendix removed via Lanz incision; layered closure; haemostasis secured.",
                PostOpInstructions = "IV fluids; analgesia; early mobilisation; antibiotics 5/7",
                EstimatedBloodLossMl = 50, CrystalloidGivenMl = 1000, AsaScore = "II",
                CreatedById = ctx.Consultant.Id, CreatedAt = startUtc.AddDays(-1)
            });
        }
        if (csIdx >= 0)
        {
            var p = patients[csIdx];
            var startUtc = now.AddHours(-1);
            db.TheatreSessions.Add(new ThriveHealth.Web.Models.Theatre.TheatreSession
            {
                FacilityId = fid, TheatreId = t2.Id, PatientId = p.Id,
                LeadSurgeonId = ctx.Consultant.Id, AnaesthetistId = ctx.Doctor.Id, ScrubNurseId = ctx.Midwife.Id,
                ProcedureName = "Lower segment Caesarean section", CptCode = "59514",
                Urgency = ThriveHealth.Web.Models.Theatre.CaseUrgency.ScheduledUrgent,
                Anaesthesia = ThriveHealth.Web.Models.Theatre.AnaesthesiaType.Spinal,
                Status = ThriveHealth.Web.Models.Theatre.TheatreSessionStatus.InTheatre,
                ScheduledStartUtc = startUtc.AddMinutes(-15),
                EstimatedMinutes = 75,
                PreOpAt = startUtc.AddMinutes(-30),
                KnifeOnSkinAt = startUtc,
                Indication = "Previous CS — booked elective",
                AsaScore = "II",
                CreatedById = ctx.Consultant.Id, CreatedAt = now.AddDays(-2)
            });
        }
        if (herniaIdx >= 0)
        {
            var p = patients[herniaIdx];
            var startUtc = now.AddDays(7).AddHours(2);
            db.TheatreSessions.Add(new ThriveHealth.Web.Models.Theatre.TheatreSession
            {
                FacilityId = fid, TheatreId = t1.Id, PatientId = p.Id,
                LeadSurgeonId = ctx.Consultant.Id, AnaesthetistId = ctx.Doctor.Id,
                ProcedureName = "Open inguinal hernia repair (Lichtenstein)", CptCode = "49505",
                Urgency = ThriveHealth.Web.Models.Theatre.CaseUrgency.Elective,
                Anaesthesia = ThriveHealth.Web.Models.Theatre.AnaesthesiaType.Spinal,
                Status = ThriveHealth.Web.Models.Theatre.TheatreSessionStatus.Scheduled,
                ScheduledStartUtc = startUtc, EstimatedMinutes = 60,
                Indication = "Right inguinal hernia", AsaScore = "I",
                CreatedById = ctx.Consultant.Id, CreatedAt = now.AddDays(-1)
            });
        }

        // ICU chart entries — for the IcuPostOp admission
        var icuAdm = await db.Admissions.AsNoTracking()
            .Where(a => a.FacilityId == fid && a.Status == ThriveHealth.Web.Models.Inpatient.AdmissionStatus.Active)
            .OrderByDescending(a => a.AdmittedAt).Take(1).FirstOrDefaultAsync();
        if (icuAdm != null)
        {
            for (var h = 0; h < 12; h++)
            {
                db.IcuChartEntries.Add(new ThriveHealth.Web.Models.Critical.IcuChartEntry
                {
                    FacilityId = fid, AdmissionId = icuAdm.Id,
                    RecordedUtc = now.AddHours(-h * 2),
                    HeartRate = 90 + rnd.Next(-10, 15),
                    SystolicBp = 110 + rnd.Next(-10, 15),
                    DiastolicBp = 70 + rnd.Next(-5, 10),
                    MeanArterialPressure = 80 + rnd.Next(-5, 10),
                    RespiratoryRate = 16 + rnd.Next(0, 6),
                    SpO2 = 95m + (decimal)rnd.NextDouble() * 4m,
                    TemperatureC = 36.8m + (decimal)rnd.NextDouble(),
                    GcsEye = 3, GcsVerbal = 4, GcsMotor = 5,
                    PainScore = rnd.Next(0, 4),
                    Sedation = ThriveHealth.Web.Models.Critical.SedationLevel.Awake,
                    Pupils = "Equal and reactive",
                    UrineOutputMl = 50 + rnd.Next(-15, 25),
                    CrystalloidGivenMl = 100,
                    VentMode = h < 4 ? ThriveHealth.Web.Models.Critical.VentilationMode.SpontaneousRoomAir : ThriveHealth.Web.Models.Critical.VentilationMode.NasalCannula,
                    FiO2 = 0.4m, Peep = 5,
                    Notes = h == 0 ? "Stable. Vasopressor weaning continues." : null,
                    RecordedById = ctx.Nurse.Id
                });
            }
        }

        // Dialysis — for the CKD patient
        var ckdIdx = templates.ToList().FindIndex(t => t.Journey == JourneyType.DialysisCkd);
        if (ckdIdx >= 0)
        {
            var p = patients[ckdIdx];
            for (var s = 0; s < 4; s++)
            {
                var sessStart = now.AddDays(-s * 4).AddHours(-3);
                db.DialysisSessions.Add(new ThriveHealth.Web.Models.Critical.DialysisSession
                {
                    FacilityId = fid, PatientId = p.Id,
                    SessionNumber = $"HD-{sessStart:yyyyMMdd}-{s + 1:D2}",
                    Modality = ThriveHealth.Web.Models.Critical.DialysisModality.Haemodialysis,
                    Access = ThriveHealth.Web.Models.Critical.VascularAccess.AvFistula,
                    StartUtc = sessStart,
                    EndUtc = sessStart.AddHours(4),
                    DurationMinutes = 240,
                    PreWeightKg = 73.5m,
                    PostWeightKg = 70.5m,
                    UfTargetMl = 3000,
                    UfAchievedMl = 2900 + rnd.Next(-100, 100),
                    PreSystolicBp = 150, PreDiastolicBp = 90,
                    PostSystolicBp = 130, PostDiastolicBp = 80,
                    BloodFlowMlMin = 300m,
                    DialysateFlowMlMin = 500m,
                    HeparinUnits = 4000m,
                    DialyserType = "F8 high-flux",
                    OperatorId = ctx.Nurse.Id,
                    Notes = s == 0 ? "Tolerated session well" : null
                });
            }
        }

        await db.SaveChangesAsync();
    }
    // ====================================================================================
    // Phase 7: ANC + delivery + paeds + immunization
    // ====================================================================================
    private static async Task SeedMaternityPaedsAsync(ApplicationDbContext db, int fid, List<Patient> patients, SeedContext ctx, Random rnd, DateTime now)
    {
        var templates = Templates();

        var ancMap = new (JourneyType j, int weeks, int visits, bool delivered)[]
        {
            (JourneyType.AncFirstVisit, 12, 1, false),
            (JourneyType.AncMidTerm, 24, 4, false),
            (JourneyType.AncLateTerm, 36, 6, false),
            (JourneyType.AncDelivered, 40, 8, true),
            (JourneyType.AncHighRisk, 30, 5, false)
        };

        var ancCounter = 1;
        foreach (var (journey, weeks, visitCount, delivered) in ancMap)
        {
            var idx = templates.ToList().FindIndex(t => t.Journey == journey);
            if (idx < 0) continue;
            var p = patients[idx];
            var t = templates[idx];

            var lmp = DateOnly.FromDateTime(now.AddDays(-weeks * 7));
            var edd = lmp.AddDays(280);
            var booking = lmp.AddDays(8 * 7);

            var anc = new ThriveHealth.Web.Models.Maternity.AnteNatalRecord
            {
                FacilityId = fid,
                PatientId = p.Id,
                AncNumber = $"ANC-{now.Year}-{ancCounter++:D4}",
                BookingDate = booking,
                Lmp = lmp,
                Edd = edd,
                Gravida = rnd.Next(1, 5),
                Para = rnd.Next(0, 4),
                BloodGroup = ThriveHealth.Web.Models.Maternity.BloodGroup.OPos,
                RhesusPositive = true,
                HeightCm = 162m,
                BookingWeightKg = 65m,
                HaemoglobinGdl = journey == JourneyType.AncHighRisk ? 9.2m : 11.5m,
                HivStatus = ThriveHealth.Web.Models.Maternity.HivStatus.Negative,
                VdrlReactive = false, HepBPositive = false, SicklingPositive = false,
                RiskFactors = journey == JourneyType.AncHighRisk ? "Chronic hypertension; previous pre-eclampsia" : null,
                PreviousObstetricHistory = "Previous SVD",
                MedicalHistory = t.Problem,
                Status = delivered ? ThriveHealth.Web.Models.Maternity.AnteNatalStatus.Delivered : ThriveHealth.Web.Models.Maternity.AnteNatalStatus.Booked,
                CreatedById = ctx.Midwife.Id,
                CreatedAt = booking.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            };

            for (var v = 1; v <= visitCount; v++)
            {
                var ga = (v * weeks) / Math.Max(visitCount, 1);
                anc.Visits.Add(new ThriveHealth.Web.Models.Maternity.AnteNatalVisit
                {
                    VisitDate = lmp.AddDays(ga * 7),
                    VisitNumber = v,
                    GestationalAgeWeeks = ga,
                    WeightKg = 65m + v * 0.7m,
                    SystolicBp = journey == JourneyType.AncHighRisk ? 145 + rnd.Next(0, 15) : 110 + rnd.Next(0, 15),
                    DiastolicBp = journey == JourneyType.AncHighRisk ? 95 + rnd.Next(0, 10) : 70 + rnd.Next(0, 10),
                    FundalHeightCm = ga > 12 ? ga - 1 : null,
                    FetalHeartRate = ga > 16 ? 140 + rnd.Next(0, 15) : null,
                    Presentation = ga > 28 ? ThriveHealth.Web.Models.Maternity.FetalPresentation.Cephalic : null,
                    UrineProtein = journey == JourneyType.AncHighRisk && v == visitCount,
                    Oedema = journey == JourneyType.AncHighRisk && v == visitCount,
                    FetalMovements = ga > 20,
                    Plan = "Continue routine ANC; iron + folate; SP for IPT",
                    RecordedById = ctx.Midwife.Id,
                    RecordedAt = lmp.AddDays(ga * 7).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
                });
            }

            if (delivered)
            {
                var deliveryUtc = edd.AddDays(-rnd.Next(2, 14)).ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(rnd.Next(8, 22));
                var del = new ThriveHealth.Web.Models.Maternity.Delivery
                {
                    FacilityId = fid,
                    PatientId = p.Id,
                    LabourOnsetUtc = deliveryUtc.AddHours(-6),
                    DeliveryUtc = deliveryUtc,
                    LabourMinutes = 360,
                    Mode = ThriveHealth.Web.Models.Maternity.DeliveryMode.SpontaneousVertex,
                    Outcome = ThriveHealth.Web.Models.Maternity.LabourOutcome.LiveBorn,
                    GestationAtDeliveryWeeks = 39,
                    EpisiotomyPerformed = false,
                    EstimatedBloodLossMl = 250,
                    ActiveMgmtThirdStage = true,
                    OxytocinGiven = true,
                    AccoucheurId = ctx.Midwife.Id,
                    Notes = "Uneventful delivery"
                };
                del.Newborns.Add(new ThriveHealth.Web.Models.Maternity.Newborn
                {
                    Sex = rnd.Next(0, 2) == 0 ? ThriveHealth.Web.Models.Maternity.NewbornSex.Male : ThriveHealth.Web.Models.Maternity.NewbornSex.Female,
                    BirthWeightG = 3200 + rnd.Next(-300, 400),
                    LengthCm = 50m,
                    HeadCircumferenceCm = 35m,
                    Apgar1Min = 8, Apgar5Min = 9, Apgar10Min = 10,
                    BreastfedWithin1Hr = true, VitaminKGiven = true,
                    BcgGivenAtBirth = true, OpvGivenAtBirth = true, HepBGivenAtBirth = true
                });
                anc.Deliveries.Add(del);

                anc.PostnatalVisits.Add(new ThriveHealth.Web.Models.Maternity.PostnatalVisit
                {
                    VisitDate = DateOnly.FromDateTime(deliveryUtc.AddDays(1)),
                    Day = ThriveHealth.Web.Models.Maternity.PostnatalDay.Day1,
                    MotherSystolicBp = 120, MotherDiastolicBp = 78,
                    MotherTemperatureC = 36.8m,
                    Lochia = "Rubra, moderate", FundalInvolution = "At umbilicus",
                    BabyWeightKg = 3.1m, BabyJaundice = false, BabyBreastfeeding = true,
                    RecordedById = ctx.Midwife.Id,
                    RecordedAt = deliveryUtc.AddDays(1)
                });
            }

            db.AnteNatalRecords.Add(anc);
        }

        // Paeds: child profiles + growth + immunization
        var vaccinesWithSchedule = await db.Vaccines.Include(v => v.Schedule).ToListAsync();
        for (var i = 0; i < templates.Count; i++)
        {
            var t = templates[i];
            if (t.Journey != JourneyType.PaedsImmunization && t.Journey != JourneyType.PaedsGrowth
                && t.Journey != JourneyType.PaedsAcute && t.Journey != JourneyType.PaedsMalnutrition)
                continue;

            var p = patients[i];
            var profile = new ThriveHealth.Web.Models.Paediatrics.ChildProfile
            {
                PatientId = p.Id,
                BirthWeightG = 3200,
                BirthLengthCm = 50m,
                BirthHeadCircCm = 35m,
                GestationalAgeAtBirthWeeks = 39,
                CurrentFeeding = t.AgeYears < 1 ? ThriveHealth.Web.Models.Paediatrics.FeedingType.ExclusiveBreast
                                : t.AgeYears < 2 ? ThriveHealth.Web.Models.Paediatrics.FeedingType.ComplementaryFeeding
                                : ThriveHealth.Web.Models.Paediatrics.FeedingType.FamilyDiet,
                KnownAllergies = t.Allergy
            };

            for (var m = 0; m < 4; m++)
            {
                var ageM = Math.Max(0, t.AgeYears * 12 - m * 3);
                profile.Measurements.Add(new ThriveHealth.Web.Models.Paediatrics.GrowthMeasurement
                {
                    FacilityId = fid,
                    PatientId = p.Id,
                    DateOfMeasurement = DateOnly.FromDateTime(now.AddMonths(-m * 3)),
                    AgeMonths = ageM,
                    WeightKg = t.Journey == JourneyType.PaedsMalnutrition ? 8m + m * 0.2m : 12m + m * 0.5m,
                    HeightCm = 80m + m * 2m,
                    HeadCircumferenceCm = 47m + m * 0.3m,
                    MuacCm = t.Journey == JourneyType.PaedsMalnutrition ? 11m : 14m,
                    BmiKgM2 = t.Journey == JourneyType.PaedsMalnutrition ? 13m : 16m,
                    NutritionalStatus = t.Journey == JourneyType.PaedsMalnutrition ? "Severe acute malnutrition" : "Normal",
                    RecordedById = ctx.Nurse.Id,
                    RecordedAt = now.AddMonths(-m * 3)
                });
            }

            db.ChildProfiles.Add(profile);

            foreach (var v in vaccinesWithSchedule.Take(6))
            {
                var sched = v.Schedule.FirstOrDefault();
                if (p.DateOfBirth is null) continue;
                var due = p.DateOfBirth.Value.AddDays((sched?.RecommendedAgeWeeks ?? 0) * 7);
                var administered = due <= DateOnly.FromDateTime(now.AddDays(-7));
                db.ImmunizationDoses.Add(new ThriveHealth.Web.Models.Immunization.ImmunizationDose
                {
                    FacilityId = fid,
                    PatientId = p.Id,
                    VaccineId = v.Id,
                    VaccineScheduleId = sched?.Id,
                    DoseLabel = sched?.DoseLabel ?? "Dose 1",
                    DueDate = due,
                    AdministeredAt = administered ? due.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc).AddHours(10) : null,
                    Status = administered ? ThriveHealth.Web.Models.Immunization.DoseStatus.Administered : ThriveHealth.Web.Models.Immunization.DoseStatus.Due,
                    BatchNumber = administered ? $"BATCH-{rnd.Next(10000, 99999)}" : null,
                    Site = v.Site,
                    AdministeredById = administered ? ctx.Nurse.Id : null
                });
            }
        }

        await db.SaveChangesAsync();
    }
    // ====================================================================================
    // Phase 8: blood bank + mortuary + allied + telemed
    // ====================================================================================
    private static async Task SeedSpecialtyAsync(ApplicationDbContext db, int fid, List<Patient> patients, SeedContext ctx, Random rnd, DateTime now)
    {
        var templates = Templates();

        // Blood donors (5)
        var donors = new List<ThriveHealth.Web.Models.BloodBank.BloodDonor>();
        var groups = new[] { ThriveHealth.Web.Models.Maternity.BloodGroup.OPos, ThriveHealth.Web.Models.Maternity.BloodGroup.APos, ThriveHealth.Web.Models.Maternity.BloodGroup.BPos, ThriveHealth.Web.Models.Maternity.BloodGroup.ONeg, ThriveHealth.Web.Models.Maternity.BloodGroup.ABPos };
        var donorNames = new[] { ("Adamu", "Sani"), ("Ifeanyi", "Okoro"), ("Olusola", "Aiyenuro"), ("Ngozi", "Ekwe"), ("Bashir", "Ahmed") };
        for (var i = 0; i < 5; i++)
        {
            donors.Add(new ThriveHealth.Web.Models.BloodBank.BloodDonor
            {
                FacilityId = fid,
                DonorNumber = $"BD-{now.Year}-{i + 1:D4}",
                FullName = $"{donorNames[i].Item1} {donorNames[i].Item2}",
                DateOfBirth = DateOnly.FromDateTime(now.AddYears(-30 - i)),
                Sex = i % 2 == 0 ? "Male" : "Female",
                Phone = $"+234801999000{i}",
                Address = "Lagos",
                BloodGroup = groups[i],
                RhesusPositive = true,
                DonorType = i < 3 ? ThriveHealth.Web.Models.BloodBank.DonorType.Voluntary : ThriveHealth.Web.Models.BloodBank.DonorType.FamilyReplacement,
                Status = ThriveHealth.Web.Models.BloodBank.DonationStatus.Accepted,
                LastDonationDate = DateOnly.FromDateTime(now.AddDays(-rnd.Next(7, 90))),
                TotalDonations = i + 1,
                HivNegative = true, HepBNegative = true, HepCNegative = true, VdrlNegative = true, MalariaNegative = true,
                CreatedAt = now.AddDays(-rnd.Next(7, 90))
            });
        }
        db.BloodDonors.AddRange(donors);
        await db.SaveChangesAsync();

        // Blood units (10)
        for (var i = 0; i < 10; i++)
        {
            var donor = donors[i % donors.Count];
            var collected = DateOnly.FromDateTime(now.AddDays(-rnd.Next(0, 28)));
            db.BloodUnits.Add(new ThriveHealth.Web.Models.BloodBank.BloodUnit
            {
                FacilityId = fid,
                UnitNumber = $"BU-{now.Year}-{i + 1:D4}",
                BloodDonorId = donor.Id,
                Component = i % 4 == 0 ? ThriveHealth.Web.Models.BloodBank.BloodComponent.PackedRedCells : ThriveHealth.Web.Models.BloodBank.BloodComponent.WholeBlood,
                BloodGroup = donor.BloodGroup,
                RhesusPositive = donor.RhesusPositive ?? true,
                CollectionDate = collected,
                ExpiryDate = collected.AddDays(35),
                VolumeMl = 450,
                Status = i < 7 ? ThriveHealth.Web.Models.BloodBank.BloodUnitStatus.Available : ThriveHealth.Web.Models.BloodBank.BloodUnitStatus.Quarantined,
                ScreeningComplete = i < 7,
                HivNegative = true, HepBNegative = true, HepCNegative = true, VdrlNegative = true, MalariaNegative = true,
                CreatedAt = collected.ToDateTime(TimeOnly.MinValue, DateTimeKind.Utc)
            });
        }

        // Crossmatch — for the GSW patient
        var gswIdx = templates.ToList().FindIndex(t => t.Journey == JourneyType.AeGsw);
        if (gswIdx >= 0)
        {
            db.BloodCrossMatches.Add(new ThriveHealth.Web.Models.BloodBank.BloodCrossMatch
            {
                FacilityId = fid,
                CrossMatchNumber = $"XM-{now.Year}-0001",
                PatientId = patients[gswIdx].Id,
                PatientBloodGroup = ThriveHealth.Web.Models.Maternity.BloodGroup.OPos,
                PatientRhesusPositive = true,
                Component = ThriveHealth.Web.Models.BloodBank.BloodComponent.WholeBlood,
                UnitsRequested = 2,
                RequiredBy = DateOnly.FromDateTime(now.AddDays(1)),
                Indication = "GSW with significant blood loss",
                Status = ThriveHealth.Web.Models.BloodBank.CrossMatchStatus.Compatible,
                RequestedById = ctx.Consultant.Id,
                CompatibilityCheckedById = ctx.LabSci.Id,
                CreatedAt = now.AddHours(-3)
            });
        }

        // Mortuary entries — one for the deceased admission patient + one historical
        var deceasedIdx = templates.ToList().FindIndex(t => t.Journey == JourneyType.DeceasedAdmission);
        if (deceasedIdx >= 0)
        {
            var p = patients[deceasedIdx];
            db.MortuaryEntries.Add(new ThriveHealth.Web.Models.Mortuary.MortuaryEntry
            {
                FacilityId = fid,
                MortuaryNumber = $"MORT-{now.Year}-001",
                CabinetCode = "C-01",
                PatientId = p.Id,
                IsUnidentified = false,
                DeceasedName = p.FirstName + " " + p.LastName,
                Sex = p.Sex.ToString(),
                DateOfBirth = p.DateOfBirth,
                AgeYears = templates[deceasedIdx].AgeYears,
                Tribe = templates[deceasedIdx].Tribe,
                AddressOfOrigin = templates[deceasedIdx].State,
                DateOfDeathUtc = now.AddDays(-2),
                PlaceOfDeath = "Hospital — emergency room",
                CauseOfDeath = "Acute decompensated heart failure",
                Manner = ThriveHealth.Web.Models.Mortuary.MannerOfDeath.Natural,
                NextOfKinName = "Mrs Ojo",
                NextOfKinRelationship = "Spouse",
                NextOfKinPhone = "+2348012345999",
                Status = ThriveHealth.Web.Models.Mortuary.MortuaryStatus.AwaitingRelease,
                ReceivedAt = now.AddDays(-2).AddHours(2),
                ReceivedById = ctx.Nurse.Id
            });
        }

        // Historical released entry (unidentified)
        db.MortuaryEntries.Add(new ThriveHealth.Web.Models.Mortuary.MortuaryEntry
        {
            FacilityId = fid,
            MortuaryNumber = $"MORT-{now.Year}-002",
            CabinetCode = "C-02",
            IsUnidentified = true,
            DeceasedName = "Unknown male — RTA",
            Sex = "Male",
            AgeYears = 35,
            DateOfDeathUtc = now.AddDays(-12),
            PlaceOfDeath = "BID — A&E",
            CauseOfDeath = "Multiple injuries — RTA",
            Manner = ThriveHealth.Web.Models.Mortuary.MannerOfDeath.Accident,
            Status = ThriveHealth.Web.Models.Mortuary.MortuaryStatus.Released,
            ReceivedAt = now.AddDays(-12).AddHours(1),
            ReleasedAt = now.AddDays(-2),
            ReleasedTo = "Police — for forensic investigation",
            ReleaseAuthorityRef = "POL/CR/2026/098",
            ReceivedById = ctx.Nurse.Id,
            ReleasedById = ctx.Admin.Id
        });

        // Allied — physio sessions for low back pain + a dental session
        var lowBackIdx = templates.ToList().FindIndex(t => t.Journey == JourneyType.OpdLowBack);
        if (lowBackIdx >= 0)
        {
            var p = patients[lowBackIdx];
            for (var s = 0; s < 3; s++)
            {
                var sessTime = now.AddDays(-(2 - s) * 4);
                db.AlliedSessions.Add(new ThriveHealth.Web.Models.Allied.AlliedSession
                {
                    FacilityId = fid,
                    PatientId = p.Id,
                    SessionNumber = $"PHY-{now.Year}-{s + 1:D4}",
                    ServiceLine = ThriveHealth.Web.Models.Allied.AlliedServiceLine.Physiotherapy,
                    Status = s < 2 ? ThriveHealth.Web.Models.Allied.SessionStatus.Completed : ThriveHealth.Web.Models.Allied.SessionStatus.Scheduled,
                    ScheduledUtc = sessTime,
                    StartedUtc = s < 2 ? sessTime : null,
                    CompletedUtc = s < 2 ? sessTime.AddMinutes(45) : null,
                    ChiefComplaint = "Mechanical low back pain",
                    Examination = "Lumbar paraspinal tenderness, SLR negative",
                    Assessment = "Mechanical LBP",
                    TreatmentGiven = "Soft tissue mobilisation + core strengthening",
                    Plan = "Continue 2x/week for 6 weeks",
                    PhysioModalitiesUsed = "TENS, manual therapy",
                    SessionsCompleted = s + 1, SessionsPlanned = 12,
                    ProviderId = ctx.Admin.Id,
                    CreatedAt = sessTime.AddDays(-1)
                });
            }
        }

        // Telemed — comprehensive demo set: every status + mode, mixed clinicians, realistic timing
        AddDemoTeleSessions(db, fid, patients, templates.ToList(), ctx, now);

        await db.SaveChangesAsync();
    }

    /// <summary>
    /// Adds the comprehensive telemed demo set to the change-tracker. Caller saves.
    /// Covers every <see cref="TeleSessionStatus"/> and every <see cref="TeleSessionMode"/> across
    /// mixed clinicians so the dashboard, calendar and Tele list views all have realistic content.
    /// </summary>
    private static void AddDemoTeleSessions(ApplicationDbContext db, int fid, List<Patient> patients,
        List<PatientTemplate> templatesList, SeedContext ctx, DateTime now)
    {
        int TIdx(JourneyType j) => templatesList.FindIndex(t => t.Journey == j);
        var teleSeq = 0;
        string NextNum() => $"TM-{now.Year}-{++teleSeq:D5}";
        string Tok() => Guid.NewGuid().ToString("N");

        void Add(JourneyType journey, string? clinicianId, TeleSessionMode mode, TeleSessionStatus status,
            DateTime scheduled, DateTime createdAt,
            string reason, string? symptoms = null, string? notes = null,
            DateTime? patientJoined = null, DateTime? clinicianJoined = null,
            DateTime? started = null, DateTime? ended = null,
            int? rating = null, string? feedback = null)
        {
            var i = TIdx(journey);
            if (i < 0 || i >= patients.Count || patients[i] is null) return;
            var tok = Tok();
            db.TeleSessions.Add(new TeleSession
            {
                FacilityId = fid,
                SessionNumber = NextNum(),
                PatientId = patients[i].Id,
                ClinicianId = clinicianId,
                Mode = mode,
                Status = status,
                ScheduledStartUtc = scheduled,
                PatientJoinedAt = patientJoined,
                ClinicianJoinedAt = clinicianJoined,
                StartedAt = started,
                EndedAt = ended,
                RoomToken = tok,
                JoinUrl = $"/Telemedicine/Room/{tok}",
                ConsultationReason = reason,
                PatientSymptoms = symptoms,
                ClinicianNotes = notes,
                PatientRating = rating,
                PatientFeedback = feedback,
                CreatedAt = createdAt
            });
        }

        // ── Requested (no clinician yet) ────────────────────────────────────────────
        Add(JourneyType.OpdHypertension, clinicianId: null, TeleSessionMode.Video, TeleSessionStatus.Requested,
            scheduled: now.AddHours(2), createdAt: now.AddMinutes(-25),
            reason: "BP has been high all week despite my medication.",
            symptoms: "Headache, occasional palpitations, mild blurred vision.");

        Add(JourneyType.OpdMalaria, clinicianId: null, TeleSessionMode.Audio, TeleSessionStatus.Requested,
            scheduled: now.AddHours(4), createdAt: now.AddMinutes(-10),
            reason: "Fever and chills since last night.",
            symptoms: "Fever 38.6, body aches, no rash. Took paracetamol.");

        // ── Scheduled (clinician assigned, future) ──────────────────────────────────
        Add(JourneyType.OpdAnxiety, ctx.Doctor.Id, TeleSessionMode.Video, TeleSessionStatus.Scheduled,
            scheduled: now.AddHours(3), createdAt: now.AddDays(-1),
            reason: "Mental-health follow-up — sertraline review.",
            symptoms: "Sleeping better but still anxious in mornings.");

        Add(JourneyType.OpdDiabetes, ctx.Consultant.Id, TeleSessionMode.Video, TeleSessionStatus.Scheduled,
            scheduled: now.AddDays(1).AddHours(2), createdAt: now.AddDays(-2),
            reason: "Diabetes review — share sugar logbook before visit.",
            symptoms: "Fasting sugars 7-9 mmol/L, no hypoglycaemia.");

        Add(JourneyType.OpdEczema, ctx.Doctor.Id, TeleSessionMode.Chat, TeleSessionStatus.Scheduled,
            scheduled: now.AddHours(6), createdAt: now.AddHours(-2),
            reason: "Skin flare — patient sending photos.",
            symptoms: "Itchy patches behind knees and elbows for 4 days.");

        Add(JourneyType.AncMidTerm, ctx.Midwife.Id, TeleSessionMode.Video, TeleSessionStatus.Scheduled,
            scheduled: now.AddDays(2).AddHours(3), createdAt: now.AddDays(-1),
            reason: "Antenatal counselling — second-trimester checklist.");

        // ── Patient waiting — patient already in the room, clinician hasn't joined ─
        Add(JourneyType.OpdGastritis, ctx.Doctor.Id, TeleSessionMode.Video, TeleSessionStatus.PatientWaiting,
            scheduled: now.AddMinutes(-5), createdAt: now.AddHours(-3),
            reason: "Persistent epigastric pain despite omeprazole.",
            symptoms: "Burning pain after meals, no vomiting.",
            patientJoined: now.AddMinutes(-3));

        // ── In-call right now (clinician + patient both joined) ─────────────────────
        Add(JourneyType.DialysisCkd, ctx.Consultant.Id, TeleSessionMode.Video, TeleSessionStatus.InCall,
            scheduled: now.AddMinutes(-12), createdAt: now.AddDays(-1),
            reason: "Pre-dialysis review — fluid status and fistula.",
            symptoms: "Slight ankle swelling, weight up 1.2 kg since last session.",
            patientJoined: now.AddMinutes(-11),
            clinicianJoined: now.AddMinutes(-10),
            started: now.AddMinutes(-10));

        // ── Completed sessions (recent activity feed) ───────────────────────────────
        Add(JourneyType.OpdAnxiety, ctx.Doctor.Id, TeleSessionMode.Video, TeleSessionStatus.Completed,
            scheduled: now.AddDays(-2).AddHours(10), createdAt: now.AddDays(-3),
            reason: "Mental-health follow-up.",
            symptoms: "Doing better on sertraline.",
            notes: "Mood improved, sleep better. Continue current dose, review in 4 weeks.",
            patientJoined: now.AddDays(-2).AddHours(10).AddMinutes(-1),
            clinicianJoined: now.AddDays(-2).AddHours(10),
            started: now.AddDays(-2).AddHours(10),
            ended: now.AddDays(-2).AddHours(10).AddMinutes(25),
            rating: 5, feedback: "Very helpful consultation, doctor listened carefully.");

        Add(JourneyType.OpdAsthma, ctx.Doctor.Id, TeleSessionMode.Video, TeleSessionStatus.Completed,
            scheduled: now.AddDays(-7).AddHours(11), createdAt: now.AddDays(-8),
            reason: "Asthma control review.",
            symptoms: "Using salbutamol 2-3 times a week, no night cough.",
            notes: "Step up to ICS-LABA. Demonstrated inhaler technique. Peak flow diary.",
            patientJoined: now.AddDays(-7).AddHours(11),
            clinicianJoined: now.AddDays(-7).AddHours(11),
            started: now.AddDays(-7).AddHours(11),
            ended: now.AddDays(-7).AddHours(11).AddMinutes(18),
            rating: 4, feedback: "Quick and easy, prescription came through SMS.");

        Add(JourneyType.OpdBph, ctx.Consultant.Id, TeleSessionMode.Audio, TeleSessionStatus.Completed,
            scheduled: now.AddDays(-10).AddHours(15), createdAt: now.AddDays(-11),
            reason: "BPH follow-up after starting tamsulosin.",
            symptoms: "Stream improved, nocturia down to once.",
            notes: "Good symptomatic response. Continue tamsulosin 0.4 mg OD. Repeat PSA in 3 months.",
            patientJoined: now.AddDays(-10).AddHours(15).AddMinutes(2),
            clinicianJoined: now.AddDays(-10).AddHours(15),
            started: now.AddDays(-10).AddHours(15).AddMinutes(2),
            ended: now.AddDays(-10).AddHours(15).AddMinutes(15),
            rating: 5, feedback: "Convenient — saved a trip into town.");

        Add(JourneyType.OpdLowBack, ctx.Mo.Id, TeleSessionMode.Chat, TeleSessionStatus.Completed,
            scheduled: now.AddDays(-14).AddHours(9), createdAt: now.AddDays(-14).AddHours(9),
            reason: "Low back pain — exercise advice.",
            symptoms: "Better than last week, still stiff in mornings.",
            notes: "Reassured. Continue physiotherapy plan. Red flags discussed and absent.",
            patientJoined: now.AddDays(-14).AddHours(9).AddMinutes(1),
            clinicianJoined: now.AddDays(-14).AddHours(9),
            started: now.AddDays(-14).AddHours(9).AddMinutes(1),
            ended: now.AddDays(-14).AddHours(9).AddMinutes(22),
            rating: 4, feedback: "Chat worked well, didn't need a video call.");

        Add(JourneyType.OpdUrti, ctx.Mo.Id, TeleSessionMode.Video, TeleSessionStatus.Completed,
            scheduled: now.AddDays(-3).AddHours(14), createdAt: now.AddDays(-3).AddHours(13),
            reason: "Cold and sore throat — needs sick note.",
            symptoms: "Sore throat day 3, no fever today.",
            notes: "Self-limiting URTI. Symptomatic care. 2-day sick note issued.",
            patientJoined: now.AddDays(-3).AddHours(14),
            clinicianJoined: now.AddDays(-3).AddHours(14),
            started: now.AddDays(-3).AddHours(14),
            ended: now.AddDays(-3).AddHours(14).AddMinutes(9),
            rating: 5, feedback: "In and out in 10 minutes, great service.");

        // ── Cancelled (patient changed plan) ────────────────────────────────────────
        Add(JourneyType.OpdAnaemia, ctx.Doctor.Id, TeleSessionMode.Video, TeleSessionStatus.Cancelled,
            scheduled: now.AddDays(-1).AddHours(12), createdAt: now.AddDays(-2),
            reason: "Anaemia follow-up after iron supplements.",
            notes: "Patient cancelled — coming in person tomorrow for repeat FBC.");

        // ── No-show patient ─────────────────────────────────────────────────────────
        Add(JourneyType.PaedsGrowth, ctx.Doctor.Id, TeleSessionMode.Video, TeleSessionStatus.NoShowPatient,
            scheduled: now.AddDays(-1).AddHours(10), createdAt: now.AddDays(-3),
            reason: "Growth chart review.",
            notes: "Clinician waited 12 minutes; mother did not join. SMS reminder sent to rebook.",
            clinicianJoined: now.AddDays(-1).AddHours(10));

        // ── No-show clinician (rare — clinician unavailable) ────────────────────────
        Add(JourneyType.AncFirstVisit, ctx.Midwife.Id, TeleSessionMode.Video, TeleSessionStatus.NoShowClinician,
            scheduled: now.AddDays(-1).AddHours(15), createdAt: now.AddDays(-3),
            reason: "Antenatal first-visit counselling.",
            symptoms: "First pregnancy — has questions about diet and folic acid.",
            notes: "Clinician unable to join (system outage). Rescheduled to in-person ANC clinic.",
            patientJoined: now.AddDays(-1).AddHours(15));
    }

    /// <summary>
    /// Re-seeds the telemed demo set on existing demo databases without dropping unrelated data.
    /// Runs even when the main demo seeder has already completed, so teams can pick up updates to
    /// the telemed seed without nuking finance/scheduling/inpatient/etc. demo state.
    /// No-op when no demo patients are present (the main seeder will handle telemed fresh).
    /// </summary>
    private static async Task TopUpTelemedAsync(ApplicationDbContext db, UserManager<ApplicationUser> um, int fid)
    {
        const int ExpectedSessions = 16;
        var existing = await db.TeleSessions.CountAsync(s => s.FacilityId == fid);
        if (existing >= ExpectedSessions) return;

        var anyDemoPatient = await db.Patients.AnyAsync(p => p.FacilityId == fid && p.FirstName == "Adekunle" && p.LastName == "Adesanya");
        if (!anyDemoPatient) return; // fresh DB — let main seeder run

        var templatesList = Templates().ToList();
        var nameKeys = templatesList.Select(t => t.FirstName + "|" + t.LastName).ToHashSet();
        var demoPatients = await db.Patients.AsNoTracking()
            .Where(p => p.FacilityId == fid)
            .ToListAsync();
        var byName = demoPatients
            .GroupBy(p => p.FirstName + "|" + p.LastName)
            .ToDictionary(g => g.Key, g => g.First());
        var patients = templatesList.Select(t => byName.GetValueOrDefault(t.FirstName + "|" + t.LastName)!).ToList();
        if (patients.Count(p => p is not null) < templatesList.Count / 2) return; // demo set too sparse

        var ctx = await BuildContextAsync(db, um, fid);

        var stale = await db.TeleSessions.Where(s => s.FacilityId == fid).ToListAsync();
        if (stale.Count > 0) db.TeleSessions.RemoveRange(stale);

        AddDemoTeleSessions(db, fid, patients, templatesList, ctx, DateTime.UtcNow);

        await db.SaveChangesAsync();
    }

    // ====================================================================================
    // Phase 9: IDSR cases + NHMIS report + portal accounts
    // ====================================================================================
    private static async Task SeedReportingPortalAsync(ApplicationDbContext db, int fid, List<Patient> patients, SeedContext ctx, Random rnd, DateTime now)
    {
        var templates = Templates();

        // IDSR — 3 cases (malaria, measles, cholera suspected)
        var malaria = ctx.Diseases.FirstOrDefault(d => d.Code.Contains("MAL", StringComparison.OrdinalIgnoreCase));
        var measles = ctx.Diseases.FirstOrDefault(d => d.Code.Contains("MEA", StringComparison.OrdinalIgnoreCase));
        var cholera = ctx.Diseases.FirstOrDefault(d => d.Code.Contains("CHO", StringComparison.OrdinalIgnoreCase));

        var paedsIdx = templates.ToList().FindIndex(t => t.Journey == JourneyType.AeChildSeizure);
        var malariaIdx = templates.ToList().FindIndex(t => t.Journey == JourneyType.OpdMalaria);
        var sepsisIdx = templates.ToList().FindIndex(t => t.Journey == JourneyType.AeSepsis);

        var idsrCounter = 1;
        if (malaria != null && malariaIdx >= 0)
        {
            var p = patients[malariaIdx];
            db.IdsrCases.Add(new ThriveHealth.Web.Models.Reporting.IdsrCase
            {
                FacilityId = fid, NotifiableDiseaseId = malaria.Id, PatientId = p.Id,
                CaseNumber = $"IDSR-{now.Year}-{idsrCounter++:D4}",
                PatientName = p.FirstName + " " + p.LastName, AgeYears = templates[malariaIdx].AgeYears, Sex = p.Sex.ToString(),
                Lga = p.Lga, State = p.State,
                OnsetDate = DateOnly.FromDateTime(now.AddDays(-5)),
                ReportDate = DateOnly.FromDateTime(now.AddDays(-4)),
                Classification = ThriveHealth.Web.Models.Reporting.CaseClassification.Confirmed,
                Outcome = ThriveHealth.Web.Models.Reporting.CaseOutcome.Recovered,
                Status = ThriveHealth.Web.Models.Reporting.IdsrCaseStatus.Closed,
                NotifiedNcdc = true, NotifiedNcdcAt = now.AddDays(-3),
                Comments = "Treated with ACT, recovered fully",
                ReportedById = ctx.Doctor.Id,
                ReportedAt = now.AddDays(-4)
            });
        }
        if (measles != null && paedsIdx >= 0)
        {
            var p = patients[paedsIdx];
            db.IdsrCases.Add(new ThriveHealth.Web.Models.Reporting.IdsrCase
            {
                FacilityId = fid, NotifiableDiseaseId = measles.Id, PatientId = p.Id,
                CaseNumber = $"IDSR-{now.Year}-{idsrCounter++:D4}",
                PatientName = p.FirstName + " " + p.LastName, AgeYears = templates[paedsIdx].AgeYears, Sex = p.Sex.ToString(),
                Lga = p.Lga, State = p.State,
                OnsetDate = DateOnly.FromDateTime(now.AddDays(-3)),
                ReportDate = DateOnly.FromDateTime(now.AddDays(-2)),
                Classification = ThriveHealth.Web.Models.Reporting.CaseClassification.Suspected,
                Outcome = ThriveHealth.Web.Models.Reporting.CaseOutcome.Unknown,
                Status = ThriveHealth.Web.Models.Reporting.IdsrCaseStatus.Open,
                NotifiedNcdc = false,
                Comments = "Suspected measles — lab samples sent. Immediate notification due.",
                ReportedById = ctx.Doctor.Id,
                ReportedAt = now.AddDays(-2)
            });
        }
        if (cholera != null && sepsisIdx >= 0)
        {
            var p = patients[sepsisIdx];
            db.IdsrCases.Add(new ThriveHealth.Web.Models.Reporting.IdsrCase
            {
                FacilityId = fid, NotifiableDiseaseId = cholera.Id, PatientId = p.Id,
                CaseNumber = $"IDSR-{now.Year}-{idsrCounter++:D4}",
                PatientName = p.FirstName + " " + p.LastName, AgeYears = templates[sepsisIdx].AgeYears, Sex = p.Sex.ToString(),
                Lga = p.Lga, State = p.State,
                OnsetDate = DateOnly.FromDateTime(now.AddDays(-1)),
                ReportDate = DateOnly.FromDateTime(now),
                Classification = ThriveHealth.Web.Models.Reporting.CaseClassification.Suspected,
                Outcome = ThriveHealth.Web.Models.Reporting.CaseOutcome.Unknown,
                Status = ThriveHealth.Web.Models.Reporting.IdsrCaseStatus.Open,
                NotifiedNcdc = true, NotifiedNcdcAt = now.AddHours(-4),
                Comments = "Suspected cholera — stool culture sent. Notified NCDC.",
                ReportedById = ctx.Consultant.Id,
                ReportedAt = now.AddHours(-6)
            });
        }

        // NHMIS — submitted prior month
        var lastMonthYear = now.AddMonths(-1).Year;
        var lastMonthMonth = now.AddMonths(-1).Month;
        if (!await db.NhmisReports.AnyAsync(r => r.FacilityId == fid && r.Year == lastMonthYear && r.Month == lastMonthMonth))
        {
            db.NhmisReports.Add(new ThriveHealth.Web.Models.Reporting.NhmisReport
            {
                FacilityId = fid,
                Year = lastMonthYear,
                Month = lastMonthMonth,
                Status = ThriveHealth.Web.Models.Reporting.NhmisReportStatus.Submitted,
                GeneratedAt = now.AddDays(-15),
                GeneratedById = ctx.Admin.Id,
                SubmittedAt = now.AddDays(-12),
                SubmittedById = ctx.Admin.Id,
                Period = $"{lastMonthYear}-{lastMonthMonth:D2}", AggregatesJson = "{\"opd_visits\":420,\"admissions\":85,\"deliveries\":18,\"deaths\":3}"
            });
        }

        // Portal accounts — for 3 patients
        var portalPatients = patients.Take(3).ToList();
        var hasher = new PasswordHasher<ThriveHealth.Web.Models.Portal.PortalAccount>();
        for (var i = 0; i < portalPatients.Count; i++)
        {
            var p = portalPatients[i];
            var acc = new ThriveHealth.Web.Models.Portal.PortalAccount
            {
                PatientId = p.Id,
                Email = $"patient{i + 1}@thrivehealth.ng",
                IsActive = true,
                EmailVerified = true,
                CreatedAt = now.AddDays(-30 - i)
            };
            acc.PasswordHash = hasher.HashPassword(acc, "Patient@12345");
            db.PortalAccounts.Add(acc);
        }

        await db.SaveChangesAsync();
    }
}
