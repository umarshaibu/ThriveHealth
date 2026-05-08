namespace ThriveHealth.Web.Models.Identity;

/// <summary>Catalogue of permission codes. Permissions are granted per role via RolePermission.</summary>
public static class Permissions
{
    public const string PrefixSeparator = ".";

    // Patients
    public const string PatientsRead             = "patients.read";
    public const string PatientsRegister         = "patients.register";
    public const string PatientsEdit             = "patients.edit";
    public const string PatientsMerge            = "patients.merge";

    // Scheduling / queue / front-office
    public const string AppointmentsRead         = "appointments.read";
    public const string AppointmentsBook         = "appointments.book";
    public const string AppointmentsCheckIn      = "appointments.checkin";
    public const string QueueRead                = "queue.read";
    public const string QueueCheckIn             = "queue.checkin";
    public const string QueueServe               = "queue.serve";

    // Triage / A&E
    public const string TriageCreate             = "triage.create";
    public const string EmergencyBoardRead       = "emergency.read";

    // Encounters / clinical notes
    public const string EncountersStart          = "encounters.start";
    public const string EncountersWrite          = "encounters.write";
    public const string EncountersSign           = "encounters.sign";
    public const string OrdersLab                = "orders.lab";
    public const string OrdersImaging            = "orders.imaging";
    public const string OrdersProcedure          = "orders.procedure";
    public const string OrdersPrescribe          = "orders.prescribe";

    // Inpatient / theatre / ICU
    public const string AdmissionsManage         = "admissions.manage";
    public const string WardsManage              = "wards.manage";
    public const string TheatreSchedule          = "theatre.schedule";
    public const string TheatreOperate           = "theatre.operate";
    public const string IcuChart                 = "icu.chart";
    public const string DialysisRun              = "dialysis.run";

    // Maternity / paeds / immunization
    public const string AncManage                = "anc.manage";
    public const string DeliveryRecord           = "delivery.record";
    public const string PaedsManage              = "paeds.manage";
    public const string ImmunizationAdminister   = "immunization.administer";

    // Diagnostics
    public const string LabRead                  = "lab.read";       // view results / patient lab history
    public const string LabPerform               = "lab.perform";    // collect specimen + enter results
    public const string LabAuthorize             = "lab.authorize";
    public const string ImagingRead              = "imaging.read";   // view reports / patient imaging history
    public const string ImagingPerform           = "imaging.perform";// perform study + draft report
    public const string ImagingReport            = "imaging.report";

    // Pharmacy
    public const string PharmacyDispense         = "pharmacy.dispense";
    public const string PharmacyControlled       = "pharmacy.controlled";
    public const string PharmacyStock            = "pharmacy.stock";

    // Blood bank / mortuary / allied
    public const string BloodBankManage          = "bloodbank.manage";
    public const string BloodBankCrossMatch      = "bloodbank.crossmatch";
    public const string MortuaryManage           = "mortuary.manage";
    public const string AlliedSession            = "allied.session";

    // Finance
    public const string BillsRead                = "bills.read";
    public const string BillsBuild               = "bills.build";
    public const string BillsDiscount            = "bills.discount";
    public const string BillsPaymentRecord       = "bills.payment_record";
    public const string CashierShiftManage       = "cashier.shift_manage";
    public const string ClaimsManage             = "claims.manage";
    public const string PayersManage             = "payers.manage";

    // Inventory & procurement
    public const string InventoryRead            = "inventory.read";
    public const string PurchaseOrderManage      = "po.manage";
    public const string GrnReceive               = "grn.receive";
    public const string StockTakeManage          = "stocktake.manage";

    // HR
    public const string HrRead                   = "hr.read";
    public const string HrEdit                   = "hr.edit";
    public const string RosterManage             = "roster.manage";
    public const string LeaveRequest             = "leave.request";
    public const string LeaveDecide              = "leave.decide";

    // Reporting / analytics
    public const string IdsrReport               = "idsr.report";
    public const string IdsrNotify               = "idsr.notify";
    public const string NhmisGenerate            = "nhmis.generate";
    public const string NhmisSubmit              = "nhmis.submit";
    public const string AnalyticsView            = "analytics.view";

    // Telemedicine
    public const string TelemedicineClinician    = "telemedicine.clinician";

    // Admin / RBAC / operations
    public const string StaffManage              = "staff.manage";
    public const string ClinicsManage            = "clinics.manage";
    public const string AuditView                = "audit.view";
    public const string IntegrationsManage       = "integrations.manage";
    public const string HardeningView            = "hardening.view";
    public const string RbacManage               = "rbac.manage"; // critical: permission to manage roles & permissions

    // AI assistance
    public const string AiAssist                 = "ai.assist";          // general gate: enables AI panels
    public const string AiLabInterpret           = "ai.lab_interpret";   // lab result interpretation
    public const string AiDifferential           = "ai.differential";    // differential diagnosis on encounter
    public const string AiDischargeDraft         = "ai.discharge_draft"; // discharge summary draft
    public const string AiImagingDraft           = "ai.imaging_draft";   // imaging report draft
    public const string AiTriageAssist           = "ai.triage_assist";   // A&E triage prioritisation assist
    public const string AiDrugCheck              = "ai.drug_check";      // contextual drug-interaction sanity check
    public const string AiIcdCoding              = "ai.icd_coding";      // ICD-10 coding suggestions on diagnoses
    public const string AiNlSearch               = "ai.nl_search";       // natural-language Q&A search
    public const string AiSchedulingAssist       = "ai.scheduling_assist"; // smart appointment scheduling
    public const string AiInventoryForecast      = "ai.inventory_forecast"; // reorder & demand forecasting
    public const string AiClaimsRisk             = "ai.claims_risk";     // claims denial-risk prediction
    public const string AiBillAnomaly            = "ai.bill_anomaly";    // bill anomaly / missing-charge detection
    public const string AiEcgInterpret           = "ai.ecg_interpret";   // ECG interpretation
    public const string AiAncRisk                = "ai.anc_risk";        // ANC / maternity risk scoring
    public const string AiPaedsDose              = "ai.paeds_dose";      // paeds dose check
    public const string AiIdsrOutbreak           = "ai.idsr_outbreak";   // IDSR cluster / outbreak likelihood
    public const string AiReferralDraft          = "ai.referral_draft"; // referral letter drafting
    public const string AiSoapStructure          = "ai.soap_structure"; // voice / dictation -> structured SOAP
    public const string AiMortuaryDraft          = "ai.mortuary_draft";  // death cert / mortuary release drafting
    public const string AiPatientSummary         = "ai.patient_summary"; // patient-facing plain-language visit summary
    public const string AiSymptomChecker         = "ai.symptom_checker"; // portal symptom-checker chatbot (patient-facing; flag-gated only)
    public const string AiAdherenceParse         = "ai.adherence_parse"; // parse SMS replies for medication adherence
    public const string AiTranslate              = "ai.translate";       // multilingual translation utility
    public const string AiAuditAnomaly           = "ai.audit_anomaly";   // audit-log anomaly detection
    public const string AiDocQuality             = "ai.doc_quality";     // encounter documentation completeness scoring
    public const string AiAdmin                  = "ai.admin";           // configure facility AI features, view costs

    public static readonly string[] All =
    {
        PatientsRead, PatientsRegister, PatientsEdit, PatientsMerge,
        AppointmentsRead, AppointmentsBook, AppointmentsCheckIn,
        QueueRead, QueueCheckIn, QueueServe,
        TriageCreate, EmergencyBoardRead,
        EncountersStart, EncountersWrite, EncountersSign,
        OrdersLab, OrdersImaging, OrdersProcedure, OrdersPrescribe,
        AdmissionsManage, WardsManage, TheatreSchedule, TheatreOperate, IcuChart, DialysisRun,
        AncManage, DeliveryRecord, PaedsManage, ImmunizationAdminister,
        LabRead, LabPerform, LabAuthorize, ImagingRead, ImagingPerform, ImagingReport,
        PharmacyDispense, PharmacyControlled, PharmacyStock,
        BloodBankManage, BloodBankCrossMatch, MortuaryManage, AlliedSession,
        BillsRead, BillsBuild, BillsDiscount, BillsPaymentRecord, CashierShiftManage,
        ClaimsManage, PayersManage,
        InventoryRead, PurchaseOrderManage, GrnReceive, StockTakeManage,
        HrRead, HrEdit, RosterManage, LeaveRequest, LeaveDecide,
        IdsrReport, IdsrNotify, NhmisGenerate, NhmisSubmit, AnalyticsView,
        TelemedicineClinician,
        StaffManage, ClinicsManage, AuditView, IntegrationsManage, HardeningView, RbacManage,
        AiAssist, AiLabInterpret, AiDifferential, AiDischargeDraft, AiImagingDraft,
        AiTriageAssist, AiDrugCheck, AiIcdCoding,
        AiNlSearch, AiSchedulingAssist, AiInventoryForecast, AiClaimsRisk, AiBillAnomaly,
        AiEcgInterpret, AiAncRisk, AiPaedsDose, AiIdsrOutbreak,
        AiReferralDraft, AiSoapStructure, AiMortuaryDraft, AiPatientSummary,
        AiSymptomChecker, AiAdherenceParse, AiTranslate, AiAuditAnomaly, AiDocQuality,
        AiAdmin
    };

    public static IDictionary<string, string[]> Grouped()
    {
        return new Dictionary<string, string[]>
        {
            ["Patients"] = new[] { PatientsRead, PatientsRegister, PatientsEdit, PatientsMerge },
            ["Front office & queue"] = new[] { AppointmentsRead, AppointmentsBook, AppointmentsCheckIn, QueueRead, QueueCheckIn, QueueServe, TriageCreate, EmergencyBoardRead },
            ["Clinical encounters"] = new[] { EncountersStart, EncountersWrite, EncountersSign, OrdersLab, OrdersImaging, OrdersProcedure, OrdersPrescribe },
            ["Inpatient · theatre · ICU"] = new[] { AdmissionsManage, WardsManage, TheatreSchedule, TheatreOperate, IcuChart, DialysisRun },
            ["Maternity · paeds · immunization"] = new[] { AncManage, DeliveryRecord, PaedsManage, ImmunizationAdminister },
            ["Diagnostics"] = new[] { LabRead, LabPerform, LabAuthorize, ImagingRead, ImagingPerform, ImagingReport },
            ["Pharmacy"] = new[] { PharmacyDispense, PharmacyControlled, PharmacyStock },
            ["Blood bank · mortuary · allied"] = new[] { BloodBankManage, BloodBankCrossMatch, MortuaryManage, AlliedSession },
            ["Finance"] = new[] { BillsRead, BillsBuild, BillsDiscount, BillsPaymentRecord, CashierShiftManage, ClaimsManage, PayersManage },
            ["Inventory & procurement"] = new[] { InventoryRead, PurchaseOrderManage, GrnReceive, StockTakeManage },
            ["Human resources"] = new[] { HrRead, HrEdit, RosterManage, LeaveRequest, LeaveDecide },
            ["Reporting & analytics"] = new[] { IdsrReport, IdsrNotify, NhmisGenerate, NhmisSubmit, AnalyticsView },
            ["Telemedicine"] = new[] { TelemedicineClinician },
            ["Administration"] = new[] { StaffManage, ClinicsManage, AuditView, IntegrationsManage, HardeningView, RbacManage },
            ["AI assistance"] = new[] { AiAssist, AiLabInterpret, AiDifferential, AiDischargeDraft, AiImagingDraft, AiTriageAssist, AiDrugCheck, AiIcdCoding, AiNlSearch, AiSchedulingAssist, AiInventoryForecast, AiClaimsRisk, AiBillAnomaly, AiEcgInterpret, AiAncRisk, AiPaedsDose, AiIdsrOutbreak, AiReferralDraft, AiSoapStructure, AiMortuaryDraft, AiPatientSummary, AiSymptomChecker, AiAdherenceParse, AiTranslate, AiAuditAnomaly, AiDocQuality, AiAdmin }
        };
    }
}

public class RolePermission
{
    public int Id { get; set; }
    public string RoleId { get; set; } = string.Empty;
    public string Permission { get; set; } = string.Empty;
    public DateTime GrantedAt { get; set; } = DateTime.UtcNow;
    public string? GrantedById { get; set; }
}
