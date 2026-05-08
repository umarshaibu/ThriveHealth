namespace ThriveHealth.Web.Models.Identity;

public static class Roles
{
    // Executive
    public const string MedicalDirector = "Medical Director";
    public const string ChiefExecutive = "Chief Executive";
    public const string ChiefFinancialOfficer = "CFO";

    // Clinical - Doctors
    public const string Consultant = "Consultant";
    public const string Doctor = "Doctor";
    public const string MedicalOfficer = "Medical Officer";

    // Clinical - Nursing
    public const string ChiefNursingOfficer = "Chief Nursing Officer";
    public const string Nurse = "Nurse";
    public const string Midwife = "Midwife";

    // Clinical - Allied
    public const string Pharmacist = "Pharmacist";
    public const string PharmacyTechnician = "Pharmacy Technician";
    public const string LabScientist = "Lab Scientist";
    public const string LabTechnician = "Lab Technician";
    public const string Radiographer = "Radiographer";
    public const string Physiotherapist = "Physiotherapist";

    // Front Office
    public const string Receptionist = "Receptionist";
    public const string RecordsOfficer = "Records Officer";
    public const string TriageClerk = "Triage Clerk";

    // Finance
    public const string Cashier = "Cashier";
    public const string Accountant = "Accountant";
    public const string ClaimsOfficer = "Claims Officer";

    // Administration
    public const string SystemAdministrator = "System Administrator";
    public const string HrOfficer = "HR Officer";
    public const string ProcurementOfficer = "Procurement Officer";
    public const string StoreOfficer = "Store Officer";
    public const string BiomedicalEngineer = "Biomedical Engineer";

    // Public Health
    public const string PublicHealthOfficer = "Public Health Officer";

    // Patient
    public const string Patient = "Patient";

    /// <summary>Platform-level role — access the super-admin console at admin.thrivehealth.ng. NEVER
    /// granted automatically; only seeded for the platform owner. A SuperAdmin operates outside any
    /// tenant context and can suspend / impersonate any tenant.</summary>
    public const string SuperAdmin = "Super Admin";

    public static readonly string[] All =
    {
        MedicalDirector, ChiefExecutive, ChiefFinancialOfficer,
        Consultant, Doctor, MedicalOfficer,
        ChiefNursingOfficer, Nurse, Midwife,
        Pharmacist, PharmacyTechnician, LabScientist, LabTechnician,
        Radiographer, Physiotherapist,
        Receptionist, RecordsOfficer, TriageClerk,
        Cashier, Accountant, ClaimsOfficer,
        SystemAdministrator, HrOfficer, ProcurementOfficer, StoreOfficer, BiomedicalEngineer,
        PublicHealthOfficer,
        Patient
    };

    public static readonly string[] ClinicalDoctors = { Consultant, Doctor, MedicalOfficer };
    public static readonly string[] ClinicalNurses = { ChiefNursingOfficer, Nurse, Midwife };
    public static readonly string[] PharmacyStaff = { Pharmacist, PharmacyTechnician };
    public static readonly string[] LabStaff = { LabScientist, LabTechnician };
    public static readonly string[] FrontOffice = { Receptionist, RecordsOfficer, TriageClerk };
    public static readonly string[] Finance = { Cashier, Accountant, ClaimsOfficer };
    public static readonly string[] Administration = { SystemAdministrator, HrOfficer, ProcurementOfficer, StoreOfficer, BiomedicalEngineer };
    public static readonly string[] Executive = { MedicalDirector, ChiefExecutive, ChiefFinancialOfficer };
}
