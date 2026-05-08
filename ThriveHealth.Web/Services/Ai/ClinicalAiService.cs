using System.Text;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Ai;
using ThriveHealth.Web.Models.Audit;

namespace ThriveHealth.Web.Services.Ai;

public sealed class ClinicalAiService : IClinicalAiService
{
    private readonly ApplicationDbContext _db;
    private readonly IAiService _ai;
    private readonly IAuditService _audit;
    private readonly ILogger<ClinicalAiService> _logger;

    private const string SafetyFooter =
        "\n\nCRITICAL RULES:\n" +
        "- This output is DRAFT only and must be reviewed by a licensed clinician before becoming part of the medical record.\n" +
        "- Do not include patient names or identifiers in your output.\n" +
        "- If clinical context is insufficient, say so explicitly rather than guessing.\n" +
        "- Use SI units common in Nigerian practice. Reference Nigerian/WHO guidelines where relevant.";

    public ClinicalAiService(ApplicationDbContext db, IAiService ai, IAuditService audit, ILogger<ClinicalAiService> logger)
    {
        _db = db;
        _ai = ai;
        _audit = audit;
        _logger = logger;
    }

    public async Task<AiOutcome> InterpretLabAsync(LabInterpretInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiLabInterpretEnabled, ct))
            return new AiOutcome(false, null, null, "Lab interpretation is disabled for this facility.");

        var system =
            "You are a clinical decision-support assistant helping clinicians interpret laboratory results " +
            "in a Nigerian secondary/tertiary hospital. Output a concise interpretation (max 6 short bullets), " +
            "then a 'Suggested follow-up:' line listing 1-3 next steps. Reference adult/paediatric ranges based on context. " +
            SafetyFooter;

        var sb = new StringBuilder();
        sb.AppendLine($"Test: {PhiScrubber.Scrub(input.TestName)}");
        if (!string.IsNullOrWhiteSpace(input.PatientAgeSex))
            sb.AppendLine($"Patient: {PhiScrubber.Scrub(input.PatientAgeSex)}");
        sb.AppendLine();
        sb.AppendLine("Analytes:");
        foreach (var a in input.Analytes)
        {
            sb.Append("- ").Append(a.Name).Append(": ").Append(a.Value);
            if (!string.IsNullOrEmpty(a.Unit)) sb.Append(' ').Append(a.Unit);
            if (!string.IsNullOrEmpty(a.RefRange)) sb.Append(" [ref ").Append(a.RefRange).Append(']');
            if (!string.Equals(a.Flag, "Normal", StringComparison.OrdinalIgnoreCase))
                sb.Append(" (").Append(a.Flag).Append(')');
            sb.AppendLine();
        }
        if (!string.IsNullOrWhiteSpace(input.GeneralComment))
            sb.AppendLine().AppendLine("Lab comment: " + PhiScrubber.Scrub(input.GeneralComment));

        return await DispatchAsync(
            AiFeature.LabInterpret,
            facilityId: input.FacilityId,
            entityType: "LabOrder",
            entityKey: input.LabOrderId.ToString(),
            system: system,
            user: sb.ToString(),
            maxTokens: 700,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> SuggestDifferentialAsync(DifferentialInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiDifferentialEnabled, ct))
            return new AiOutcome(false, null, null, "Differential diagnosis is disabled for this facility.");

        var system =
            "You are a clinical decision-support assistant in a Nigerian hospital. Given the chief complaint, " +
            "history, vitals, allergies, and meds, list 3-6 differential diagnoses ranked by likelihood with " +
            "one-line reasoning each. Then list a short 'Suggested workup:' (labs/imaging) and 'Red flags:' " +
            "(conditions to escalate immediately). Keep total under 250 words. " +
            SafetyFooter;

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(input.PatientAgeSex)) sb.AppendLine($"Patient: {PhiScrubber.Scrub(input.PatientAgeSex)}");
        sb.AppendLine($"Chief complaint: {PhiScrubber.Scrub(input.ChiefComplaint)}");
        if (!string.IsNullOrWhiteSpace(input.HistoryOfPresentIllness)) sb.AppendLine($"HPI: {PhiScrubber.Scrub(input.HistoryOfPresentIllness)}");
        if (!string.IsNullOrWhiteSpace(input.Vitals)) sb.AppendLine($"Vitals: {input.Vitals}");
        var allergies = input.Allergies.ToList();
        if (allergies.Count > 0) sb.AppendLine("Allergies: " + string.Join(", ", allergies));
        var meds = input.CurrentMedications.ToList();
        if (meds.Count > 0) sb.AppendLine("Current meds: " + string.Join(", ", meds));

        return await DispatchAsync(
            AiFeature.Differential,
            facilityId: input.FacilityId,
            entityType: "Encounter",
            entityKey: input.EncounterId.ToString(),
            system: system,
            user: sb.ToString(),
            maxTokens: 800,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> DraftDischargeSummaryAsync(DischargeInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiDischargeDraftEnabled, ct))
            return new AiOutcome(false, null, null, "Discharge draft is disabled for this facility.");

        var system =
            "You are a medical writer assistant helping draft a hospital discharge summary in a Nigerian hospital. " +
            "Output the following sections, each on its own line with the heading: " +
            "1) Reason for admission, 2) Hospital course (3-6 sentences), 3) Final diagnoses, " +
            "4) Discharge medications (list only — confirm doses with prescriber), 5) Follow-up plan, 6) Patient education points (plain-language). " +
            "Be concise and factual. Do not invent values; if data is missing for a section, write 'Not documented'. " +
            SafetyFooter;

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(input.PatientAgeSex)) sb.AppendLine($"Patient: {PhiScrubber.Scrub(input.PatientAgeSex)}");
        sb.AppendLine($"Reason for admission: {PhiScrubber.Scrub(input.Reason)}");
        if (!string.IsNullOrWhiteSpace(input.WorkingDiagnosis)) sb.AppendLine($"Working diagnosis: {PhiScrubber.Scrub(input.WorkingDiagnosis)}");
        sb.AppendLine($"Length of stay: {input.LengthOfStayDays} day(s)");
        sb.AppendLine($"Last 24h fluid balance: in {input.InputMl24h} mL / out {input.OutputMl24h} mL");

        var diagnoses = input.Diagnoses.ToList();
        if (diagnoses.Count > 0) { sb.AppendLine("Diagnoses:"); foreach (var d in diagnoses) sb.AppendLine("- " + PhiScrubber.Scrub(d)); }

        var meds = input.Medications.ToList();
        if (meds.Count > 0) { sb.AppendLine("Inpatient medications:"); foreach (var m in meds) sb.AppendLine("- " + PhiScrubber.Scrub(m)); }

        var notes = input.RecentNotes.ToList();
        if (notes.Count > 0)
        {
            sb.AppendLine("Recent ward notes (chronological):");
            foreach (var n in notes.Take(8)) sb.AppendLine("- " + PhiScrubber.Scrub(n));
        }

        return await DispatchAsync(
            AiFeature.DischargeSummary,
            facilityId: input.FacilityId,
            entityType: "Admission",
            entityKey: input.AdmissionId.ToString(),
            system: system,
            user: sb.ToString(),
            maxTokens: 1200,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> DraftImagingReportAsync(ImagingDraftInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiImagingDraftEnabled, ct))
            return new AiOutcome(false, null, null, "Imaging draft is disabled for this facility.");

        var system =
            "You are a radiology drafting assistant in a Nigerian hospital. Given the modality, study, indication, " +
            "and the radiographer's free-text findings, draft three sections in this exact format:\n" +
            "FINDINGS: <organised paragraph or bullets>\nIMPRESSION: <numbered list, 1-4 items>\nRECOMMENDATION: <1-3 short items>\n" +
            "If the findings text is too sparse to draft, write 'Not sufficient — radiographer to expand findings.' " +
            "Flag any feature suggestive of acute critical pathology with a leading 'CRITICAL:' tag in the impression. " +
            SafetyFooter;

        var sb = new StringBuilder();
        sb.AppendLine($"Modality: {input.Modality}");
        sb.AppendLine($"Study: {PhiScrubber.Scrub(input.Study)}");
        if (!string.IsNullOrWhiteSpace(input.PatientAgeSex)) sb.AppendLine($"Patient: {PhiScrubber.Scrub(input.PatientAgeSex)}");
        if (!string.IsNullOrWhiteSpace(input.ClinicalIndication)) sb.AppendLine($"Indication: {PhiScrubber.Scrub(input.ClinicalIndication)}");
        if (!string.IsNullOrWhiteSpace(input.Technique)) sb.AppendLine($"Technique: {input.Technique}");
        if (!string.IsNullOrWhiteSpace(input.Contrast)) sb.AppendLine($"Contrast: {input.Contrast}");
        sb.AppendLine();
        sb.AppendLine("Radiographer findings:");
        sb.AppendLine(PhiScrubber.Scrub(input.Findings) ?? "(none yet)");

        return await DispatchAsync(
            AiFeature.ImagingDraft,
            facilityId: input.FacilityId,
            entityType: "ImagingOrder",
            entityKey: input.ImagingOrderId.ToString(),
            system: system,
            user: sb.ToString(),
            maxTokens: 900,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> AssistTriageAsync(TriageAssistInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiTriageAssistEnabled, ct))
            return new AiOutcome(false, null, null, "Triage assist is disabled for this facility.");

        var system =
            "You are an A&E triage assistant in a Nigerian hospital using Manchester-style triage colours " +
            "(Red=immediate, Orange=very urgent, Yellow=urgent, Green=standard, Blue=non-urgent). " +
            "Given the chief complaint, vitals, and clinical context, output ONLY this format:\n" +
            "SUGGESTED COLOUR: <Red|Orange|Yellow|Green|Blue>\nREASONING: <one sentence>\nRED FLAGS: <comma-list of features that would escalate>\n" +
            "Be conservative — when in doubt prefer the more urgent colour. " +
            SafetyFooter;

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(input.PatientAgeSex)) sb.AppendLine($"Patient: {PhiScrubber.Scrub(input.PatientAgeSex)}");
        sb.AppendLine($"Chief complaint: {PhiScrubber.Scrub(input.ChiefComplaint)}");
        if (input.IsTrauma) sb.AppendLine("Trauma case: yes");
        if (input.IsPregnant) sb.AppendLine("Pregnant: yes");
        if (!string.IsNullOrWhiteSpace(input.Mechanism)) sb.AppendLine($"Mechanism: {PhiScrubber.Scrub(input.Mechanism)}");
        if (!string.IsNullOrWhiteSpace(input.Vitals)) sb.AppendLine($"Vitals: {input.Vitals}");
        if (!string.IsNullOrWhiteSpace(input.Avpu)) sb.AppendLine($"AVPU: {input.Avpu}");
        if (input.GcsTotal.HasValue) sb.AppendLine($"GCS: {input.GcsTotal}");

        return await DispatchAsync(
            AiFeature.TriageAssist,
            facilityId: input.FacilityId,
            entityType: "Patient",
            entityKey: input.PatientId?.ToString() ?? "0",
            system: system,
            user: sb.ToString(),
            maxTokens: 350,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> CheckDrugContextAsync(DrugContextCheckInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiDrugCheckEnabled, ct))
            return new AiOutcome(false, null, null, "Drug check is disabled for this facility.");

        var system =
            "You are a clinical pharmacist assistant in a Nigerian hospital. Review the proposed prescription " +
            "in the context of the patient's existing medications, allergies, conditions, and renal function. " +
            "Output:\n" +
            "INTERACTIONS: <bullets — high-risk interactions only; cite the specific drug pair>\n" +
            "ALLERGY/CONTRAINDICATION: <bullets — explicit conflicts only>\n" +
            "DOSE/RENAL CONCERNS: <bullets — adjust if present>\n" +
            "ALTERNATIVES: <if a major issue, suggest 1-2 alternatives>\n" +
            "If no concerns: write 'No significant concerns identified.' " +
            SafetyFooter;

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(input.PatientAgeSex)) sb.AppendLine($"Patient: {PhiScrubber.Scrub(input.PatientAgeSex)}");
        if (!string.IsNullOrWhiteSpace(input.RenalNote)) sb.AppendLine($"Renal: {input.RenalNote}");
        var conditions = input.Conditions.ToList();
        if (conditions.Count > 0) sb.AppendLine("Active conditions: " + string.Join(", ", conditions));
        var allergies = input.Allergies.ToList();
        if (allergies.Count > 0) sb.AppendLine("Allergies: " + string.Join(", ", allergies));
        var existing = input.Existing.ToList();
        if (existing.Count > 0) sb.AppendLine("Existing meds: " + string.Join(", ", existing));
        var proposed = input.Proposed.ToList();
        sb.AppendLine("Proposed: " + (proposed.Count == 0 ? "(none)" : string.Join(", ", proposed)));

        return await DispatchAsync(
            AiFeature.DrugCheck,
            facilityId: input.FacilityId,
            entityType: "Encounter",
            entityKey: input.EncounterId?.ToString() ?? "0",
            system: system,
            user: sb.ToString(),
            maxTokens: 600,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> SuggestIcdCodingAsync(IcdCodingInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiIcdCodingEnabled, ct))
            return new AiOutcome(false, null, null, "ICD coding suggestion is disabled for this facility.");

        var system =
            "You are a clinical coding assistant. Given a free-text clinical diagnosis from a Nigerian hospital " +
            "encounter, return up to 3 candidate ICD-10 codes ranked by likelihood. Output ONLY this format, one line per candidate:\n" +
            "CODE | TITLE | CONFIDENCE(High|Medium|Low) | NOTE(short)\n" +
            "Use the WHO ICD-10 system. Do NOT invent codes — if uncertain, mark Low confidence and explain in the note. " +
            SafetyFooter;

        var user = $"Diagnosis text: {PhiScrubber.Scrub(input.DiagnosisText)}";

        return await DispatchAsync(
            AiFeature.IcdCoding,
            facilityId: input.FacilityId,
            entityType: "Encounter",
            entityKey: input.EncounterId.ToString(),
            system: system,
            user: user,
            maxTokens: 350,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> AskNlSearchAsync(NlSearchInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiNlSearchEnabled, ct))
            return new AiOutcome(false, null, null, "Natural-language search is disabled for this facility.");

        var system =
            "You are an operational search assistant for a Nigerian hospital management system. The user asks a question " +
            "about hospital data. You will receive a CONTEXT SUMMARY of pre-aggregated facts. Answer using ONLY the data in the " +
            "context. If the context does not contain enough data to answer, suggest where the user should look — name the page " +
            "(e.g. 'Reporting > IDSR', 'Inventory > Stock', 'Claims', 'Analytics'). Be brief (under 120 words). " +
            SafetyFooter;

        var sb = new StringBuilder();
        sb.AppendLine("Question: " + input.Question);
        if (!string.IsNullOrWhiteSpace(input.ContextSummary))
        {
            sb.AppendLine();
            sb.AppendLine("Context summary:");
            sb.AppendLine(input.ContextSummary);
        }

        return await DispatchAsync(
            AiFeature.NlSearch,
            facilityId: input.FacilityId,
            entityType: "Search",
            entityKey: "0",
            system: system,
            user: sb.ToString(),
            maxTokens: 500,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> SuggestSchedulingAsync(SchedulingAssistInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiSchedulingAssistEnabled, ct))
            return new AiOutcome(false, null, null, "Scheduling assist is disabled for this facility.");

        var system =
            "You are an appointment scheduling assistant. Given a chief complaint and a list of clinics with their specialties " +
            "and current open-slot counts, suggest the best 1-2 clinics. Output exactly:\n" +
            "RECOMMENDED CLINIC: <name>\nWHEN: <today | this week | next available>\nREASONING: <one sentence>\nALTERNATE: <name or 'none'>\n" +
            SafetyFooter;

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(input.PatientAgeSex)) sb.AppendLine($"Patient: {PhiScrubber.Scrub(input.PatientAgeSex)}");
        sb.AppendLine($"Chief complaint: {PhiScrubber.Scrub(input.ChiefComplaint)}");
        if (!string.IsNullOrWhiteSpace(input.Urgency)) sb.AppendLine($"Urgency: {input.Urgency}");
        sb.AppendLine();
        sb.AppendLine("Available clinics:");
        foreach (var c in input.Clinics)
            sb.AppendLine($"- {c.Name} ({c.Specialty ?? "general"}) · today {c.OpenSlotsToday} · this week {c.OpenSlotsThisWeek}");

        return await DispatchAsync(
            AiFeature.SchedulingAssist,
            facilityId: input.FacilityId,
            entityType: "Appointment",
            entityKey: "0",
            system: system,
            user: sb.ToString(),
            maxTokens: 250,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> ForecastInventoryAsync(InventoryForecastInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiInventoryForecastEnabled, ct))
            return new AiOutcome(false, null, null, "Inventory forecast is disabled for this facility.");

        var system =
            "You are a hospital inventory forecasting assistant. For each item, decide if a reorder is needed within the lookahead " +
            "window based on recent dispense rate, stock-on-hand, reorder level, and nearest expiry. Output ONLY this format, one line per item flagged:\n" +
            "ITEM: <name> | ACTION: <REORDER|REVIEW|EXPIRY_RISK|OVERSTOCK> | QTY: <number or '-'> | REASON: <short>\n" +
            "If nothing needs action, write 'No items require action.' Skip items without enough data. " +
            SafetyFooter;

        var sb = new StringBuilder();
        sb.AppendLine($"Lookahead window: {input.LookaheadDays} days");
        sb.AppendLine();
        sb.AppendLine("Items (name | on-hand | reorder | last30d | last90d | nearest expiry):");
        foreach (var i in input.Items)
            sb.AppendLine($"- {i.Name} | {i.OnHand} | {i.ReorderLevel?.ToString() ?? "-"} | {i.Last30dDispensed} | {i.Last90dDispensed} | {(i.NearestExpiry?.ToString("yyyy-MM-dd") ?? "-")}");

        return await DispatchAsync(
            AiFeature.InventoryForecast,
            facilityId: input.FacilityId,
            entityType: "Inventory",
            entityKey: "0",
            system: system,
            user: sb.ToString(),
            maxTokens: 1000,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> AssessClaimRiskAsync(ClaimsRiskInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiClaimsRiskEnabled, ct))
            return new AiOutcome(false, null, null, "Claims risk assessment is disabled for this facility.");

        var system =
            "You are a Nigerian health insurance claims auditor. Given a claim's payer, plan, items, total, and primary diagnosis, " +
            "assess denial risk. Output exactly:\n" +
            "DENIAL RISK: <Low|Medium|High>\nLIKELY ISSUES: <bullets>\nFIXES BEFORE SUBMISSION: <bullets>\n" +
            "Common denial reasons: missing pre-authorisation, ICD-procedure mismatch, formulary exclusions, dose/frequency outside payer caps, missing documentation. " +
            SafetyFooter;

        var sb = new StringBuilder();
        sb.AppendLine($"Payer: {input.Payer}");
        if (!string.IsNullOrWhiteSpace(input.Plan)) sb.AppendLine($"Plan: {input.Plan}");
        sb.AppendLine($"Total: ₦{input.TotalAmount:N2}");
        if (!string.IsNullOrWhiteSpace(input.Diagnosis)) sb.AppendLine($"Diagnosis: {PhiScrubber.Scrub(input.Diagnosis)}");
        sb.AppendLine("Items:");
        foreach (var i in input.Items) sb.AppendLine("- " + PhiScrubber.Scrub(i));

        return await DispatchAsync(
            AiFeature.ClaimsRisk,
            facilityId: input.FacilityId,
            entityType: "Claim",
            entityKey: input.ClaimId.ToString(),
            system: system,
            user: sb.ToString(),
            maxTokens: 600,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> DetectBillAnomalyAsync(BillAnomalyInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiBillAnomalyEnabled, ct))
            return new AiOutcome(false, null, null, "Bill anomaly check is disabled for this facility.");

        var system =
            "You are a hospital revenue-integrity assistant. Given the clinical context for a bill and its current charges, " +
            "flag MISSING charges that are typically billed for this context, DUPLICATES, or implausible quantities. Output ONLY this format:\n" +
            "MISSING: <bullets — items that should be billed but are not, or 'none'>\n" +
            "DUPLICATES: <bullets — duplicated charges, or 'none'>\n" +
            "IMPLAUSIBLE: <bullets — quantity/price oddities, or 'none'>\n" +
            "Be specific (e.g. 'No bed-day charges for a 4-day admission'). " +
            SafetyFooter;

        var sb = new StringBuilder();
        sb.AppendLine("Context:");
        sb.AppendLine(PhiScrubber.Scrub(input.Context));
        sb.AppendLine();
        sb.AppendLine("Charges on bill:");
        foreach (var c in input.Charges) sb.AppendLine("- " + PhiScrubber.Scrub(c));

        return await DispatchAsync(
            AiFeature.BillAnomaly,
            facilityId: input.FacilityId,
            entityType: "Bill",
            entityKey: input.BillId.ToString(),
            system: system,
            user: sb.ToString(),
            maxTokens: 500,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> InterpretEcgAsync(EcgInterpretInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiEcgInterpretEnabled, ct))
            return new AiOutcome(false, null, null, "ECG interpretation is disabled for this facility.");

        var system =
            "You are a clinical decision-support assistant for ECG interpretation in a Nigerian hospital. " +
            "The clinician will paste rate, rhythm, intervals, axis, and any abnormalities they noticed. Output exactly:\n" +
            "RHYTHM: <one line>\nRATE/INTERVALS: <one line — note PR, QRS, QTc if mentioned>\nFINDINGS: <bullets>\n" +
            "INTERPRETATION: <one paragraph>\nCRITICAL FLAGS: <STEMI / complete heart block / VT / hyperkalaemic patterns / etc., or 'none'>\n" +
            SafetyFooter;

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(input.PatientAgeSex)) sb.AppendLine($"Patient: {PhiScrubber.Scrub(input.PatientAgeSex)}");
        if (!string.IsNullOrWhiteSpace(input.ClinicalContext)) sb.AppendLine($"Clinical context: {PhiScrubber.Scrub(input.ClinicalContext)}");
        sb.AppendLine();
        sb.AppendLine("ECG findings (clinician):");
        sb.AppendLine(PhiScrubber.Scrub(input.EcgFindings));

        return await DispatchAsync(
            AiFeature.EcgInterpret,
            facilityId: input.FacilityId,
            entityType: "Patient",
            entityKey: input.PatientId?.ToString() ?? "0",
            system: system,
            user: sb.ToString(),
            maxTokens: 600,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> AssessAncRiskAsync(AncRiskInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiAncRiskEnabled, ct))
            return new AiOutcome(false, null, null, "ANC risk assessment is disabled for this facility.");

        var system =
            "You are an antenatal-care risk-stratification assistant in a Nigerian hospital. Based on the ANC record provided, " +
            "assess risk for the most consequential conditions and output exactly:\n" +
            "OVERALL RISK: <Low|Moderate|High>\n" +
            "PRE-ECLAMPSIA RISK: <Low|Moderate|High> · <reasoning>\n" +
            "GESTATIONAL DIABETES RISK: <Low|Moderate|High> · <reasoning>\n" +
            "FETAL GROWTH CONCERN: <Low|Moderate|High> · <reasoning>\n" +
            "PPH/HAEMORRHAGE RISK: <Low|Moderate|High> · <reasoning>\n" +
            "RECOMMENDED ACTIONS: <bullets — labs, follow-up cadence, referral if needed>\n" +
            SafetyFooter;

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(input.PatientAgeSex)) sb.AppendLine($"Patient: {PhiScrubber.Scrub(input.PatientAgeSex)}");
        if (input.Gravida.HasValue || input.Para.HasValue) sb.AppendLine($"G{input.Gravida}P{input.Para}");
        if (input.GestationalWeeks.HasValue) sb.AppendLine($"Gestational age: {input.GestationalWeeks} weeks");
        if (input.Bmi.HasValue) sb.AppendLine($"BMI: {input.Bmi:F1}");
        if (!string.IsNullOrWhiteSpace(input.HivStatus)) sb.AppendLine($"HIV status: {input.HivStatus}");
        if (!string.IsNullOrWhiteSpace(input.PreviousObstetricHistory)) sb.AppendLine($"Past obstetric: {PhiScrubber.Scrub(input.PreviousObstetricHistory)}");
        if (!string.IsNullOrWhiteSpace(input.CoMorbidities)) sb.AppendLine($"Co-morbidities: {PhiScrubber.Scrub(input.CoMorbidities)}");
        if (!string.IsNullOrWhiteSpace(input.LatestVisitVitals)) sb.AppendLine($"Latest visit vitals: {input.LatestVisitVitals}");
        if (!string.IsNullOrWhiteSpace(input.Notes)) sb.AppendLine($"Notes: {PhiScrubber.Scrub(input.Notes)}");

        return await DispatchAsync(
            AiFeature.AncRisk,
            facilityId: input.FacilityId,
            entityType: "AnteNatalRecord",
            entityKey: input.AncRecordId.ToString(),
            system: system,
            user: sb.ToString(),
            maxTokens: 700,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> CheckPaedsDoseAsync(PaedsDoseInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiPaedsDoseEnabled, ct))
            return new AiOutcome(false, null, null, "Paediatric dose check is disabled for this facility.");

        var system =
            "You are a paediatric pharmacology assistant in a Nigerian hospital. Given a child's weight, age, drug, and proposed dose, " +
            "compute the recommended weight-based dose range, compare with the proposed dose, and flag any safety issues. Output exactly:\n" +
            "RECOMMENDED DOSE RANGE: <e.g. 10-15 mg/kg/dose, 6-8 hourly>\n" +
            "PROPOSED MG/KG: <number with unit>\n" +
            "VERDICT: <UNDERDOSE | WITHIN_RANGE | OVERDOSE | NEEDS_REVIEW>\n" +
            "SAFETY FLAGS: <bullets — max single/daily dose breaches, age contraindications, allergy conflicts, or 'none'>\n" +
            "SUGGESTED CORRECTION: <if not WITHIN_RANGE, exact corrected dose; else 'none'>\n" +
            SafetyFooter;

        var sb = new StringBuilder();
        sb.AppendLine($"Weight: {input.WeightKg} kg");
        if (input.AgeMonths.HasValue) sb.AppendLine($"Age: {input.AgeMonths} month(s)");
        sb.AppendLine($"Drug: {PhiScrubber.Scrub(input.Drug)}");
        sb.AppendLine($"Proposed dose: {input.ProposedDose}");
        if (!string.IsNullOrWhiteSpace(input.Route)) sb.AppendLine($"Route: {input.Route}");
        if (!string.IsNullOrWhiteSpace(input.Frequency)) sb.AppendLine($"Frequency: {input.Frequency}");
        if (!string.IsNullOrWhiteSpace(input.Indication)) sb.AppendLine($"Indication: {PhiScrubber.Scrub(input.Indication)}");
        var allergies = input.Allergies.ToList();
        if (allergies.Count > 0) sb.AppendLine("Allergies: " + string.Join(", ", allergies));

        return await DispatchAsync(
            AiFeature.PaedsDose,
            facilityId: input.FacilityId,
            entityType: "Patient",
            entityKey: input.PatientId?.ToString() ?? "0",
            system: system,
            user: sb.ToString(),
            maxTokens: 500,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> DetectIdsrOutbreakAsync(IdsrOutbreakInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiIdsrOutbreakEnabled, ct))
            return new AiOutcome(false, null, null, "IDSR outbreak detection is disabled for this facility.");

        var system =
            "You are a public-health surveillance assistant supporting Nigeria's IDSR (Integrated Disease Surveillance and Response). " +
            "For each notifiable disease cluster reported, assess outbreak likelihood and whether NCDC notification is warranted. " +
            "Use Nigeria's IDSR/NCDC alert thresholds (e.g. one suspected case of cholera/measles/Lassa fever may trigger). Output one block per disease, in this format:\n" +
            "DISEASE: <name>\n" +
            "ALERT LEVEL: <None|Watch|Alert|Outbreak>\n" +
            "NCDC NOTIFICATION: <Required immediately|Required within 24h|Routine weekly|Not required>\n" +
            "REASONING: <one sentence — cite case counts vs prior period and geography>\n" +
            SafetyFooter;

        var sb = new StringBuilder();
        sb.AppendLine($"Window: last {input.WindowDays} days");
        sb.AppendLine();
        sb.AppendLine("Disease clusters (code | name | cases-in-window | cases-prior-window | distinct LGAs | distinct wards):");
        foreach (var c in input.Clusters)
            sb.AppendLine($"- {c.DiseaseCode} | {c.DiseaseName} | {c.CasesInWindow} | {c.CasesPriorWindow} | {(c.DistinctLgas?.ToString() ?? "-")} | {(c.DistinctWards?.ToString() ?? "-")}");

        return await DispatchAsync(
            AiFeature.IdsrOutbreak,
            facilityId: input.FacilityId,
            entityType: "Idsr",
            entityKey: "0",
            system: system,
            user: sb.ToString(),
            maxTokens: 1200,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> DraftReferralLetterAsync(ReferralDraftInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiReferralDraftEnabled, ct))
            return new AiOutcome(false, null, null, "Referral drafting is disabled for this facility.");

        var system =
            "You are a medical writing assistant drafting a Nigerian hospital referral letter. Output a complete letter in plain text with these sections in order:\n" +
            "1) Salutation (e.g. 'Dear Colleague,')\n" +
            "2) Brief patient summary (age/sex, chief reason for referral)\n" +
            "3) Relevant clinical history and findings\n" +
            "4) Current diagnoses\n" +
            "5) Investigations / treatments already performed\n" +
            "6) Specific reason for referral and what is being requested\n" +
            "7) Closing (signed by referring clinician — leave name blank)\n" +
            "Be concise and factual. Do NOT include the patient's full name or hospital number. Use [PATIENT] as placeholder. " +
            SafetyFooter;

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(input.PatientAgeSex)) sb.AppendLine($"Patient: {PhiScrubber.Scrub(input.PatientAgeSex)}");
        sb.AppendLine($"Receiving facility / specialist: {PhiScrubber.Scrub(input.ReceivingFacility)}");
        if (!string.IsNullOrWhiteSpace(input.Reason)) sb.AppendLine($"Reason for referral: {PhiScrubber.Scrub(input.Reason)}");
        sb.AppendLine();
        sb.AppendLine("Clinical context:");
        sb.AppendLine(PhiScrubber.Scrub(input.ClinicalContext));
        if (!string.IsNullOrWhiteSpace(input.Diagnoses)) sb.AppendLine().AppendLine($"Diagnoses: {PhiScrubber.Scrub(input.Diagnoses)}");
        var findings = input.KeyFindings.ToList();
        if (findings.Count > 0) { sb.AppendLine(); sb.AppendLine("Key findings:"); foreach (var f in findings) sb.AppendLine("- " + PhiScrubber.Scrub(f)); }
        var meds = input.Medications.ToList();
        if (meds.Count > 0) { sb.AppendLine(); sb.AppendLine("Current medications:"); foreach (var m in meds) sb.AppendLine("- " + PhiScrubber.Scrub(m)); }

        return await DispatchAsync(
            AiFeature.ReferralDraft,
            facilityId: input.FacilityId,
            entityType: input.AdmissionId.HasValue ? "Admission" : "Encounter",
            entityKey: (input.AdmissionId ?? input.EncounterId ?? 0).ToString(),
            system: system,
            user: sb.ToString(),
            maxTokens: 1100,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> StructureSoapAsync(SoapStructureInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiSoapStructureEnabled, ct))
            return new AiOutcome(false, null, null, "SOAP structuring is disabled for this facility.");

        var system =
            "You are a medical scribe assistant. The clinician dictated a free-text narrative of a consultation in a Nigerian hospital. " +
            "Re-organise the narrative into a structured SOAP note. Output exactly:\n" +
            "SUBJECTIVE:\n<narrative — chief complaint, HPI, ROS, relevant history>\n\n" +
            "OBJECTIVE:\n<vitals, exam findings, results mentioned>\n\n" +
            "ASSESSMENT:\n<diagnoses / impression>\n\n" +
            "PLAN:\n<investigations, treatment, follow-up>\n\n" +
            "Do NOT invent details that aren't in the narrative. If a section has no content, write 'Not documented'. " +
            SafetyFooter;

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(input.PatientAgeSex)) sb.AppendLine($"Patient: {PhiScrubber.Scrub(input.PatientAgeSex)}");
        sb.AppendLine();
        sb.AppendLine("Dictated narrative:");
        sb.AppendLine(PhiScrubber.Scrub(input.Narrative));

        return await DispatchAsync(
            AiFeature.SoapStructure,
            facilityId: input.FacilityId,
            entityType: "Encounter",
            entityKey: input.EncounterId.ToString(),
            system: system,
            user: sb.ToString(),
            maxTokens: 1500,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> DraftMortuaryDocsAsync(MortuaryDraftInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiMortuaryDraftEnabled, ct))
            return new AiOutcome(false, null, null, "Mortuary drafting is disabled for this facility.");

        var system =
            "You are a Nigerian hospital records assistant drafting a release form and a death certificate summary. " +
            "Output two sections:\n\n" +
            "RELEASE FORM:\n<short standard release-of-body wording, naming the receiving party, date and authority reference>\n\n" +
            "DEATH CERTIFICATE SUMMARY:\n" +
            "Cause of death (Part I): <a) immediate cause; b) antecedent cause; c) underlying cause>\n" +
            "Other significant conditions (Part II): <if any, else 'None'>\n" +
            "Manner of death: <Natural | Accident | Homicide | Suicide | Undetermined>\n" +
            "Likely WHO ICD-10 codes for cause: <up to 3 candidate codes with one-line title each>\n" +
            "Use [PATIENT] in place of the deceased's name. " +
            SafetyFooter;

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(input.PatientAgeSex)) sb.AppendLine($"Patient: {PhiScrubber.Scrub(input.PatientAgeSex)}");
        if (input.DateOfDeathUtc.HasValue) sb.AppendLine($"Date of death (UTC): {input.DateOfDeathUtc:yyyy-MM-dd HH:mm}");
        if (!string.IsNullOrWhiteSpace(input.PlaceOfDeath)) sb.AppendLine($"Place of death: {PhiScrubber.Scrub(input.PlaceOfDeath)}");
        if (!string.IsNullOrWhiteSpace(input.CauseOfDeath)) sb.AppendLine($"Cause of death (clinician's note): {PhiScrubber.Scrub(input.CauseOfDeath)}");
        if (!string.IsNullOrWhiteSpace(input.Manner)) sb.AppendLine($"Manner: {input.Manner}");
        if (!string.IsNullOrWhiteSpace(input.ReleasedTo)) sb.AppendLine($"Released to: {PhiScrubber.Scrub(input.ReleasedTo)}");
        if (!string.IsNullOrWhiteSpace(input.ReleaseAuthorityRef)) sb.AppendLine($"Release authority reference: {input.ReleaseAuthorityRef}");

        return await DispatchAsync(
            AiFeature.MortuaryDraft,
            facilityId: input.FacilityId,
            entityType: "MortuaryEntry",
            entityKey: input.MortuaryEntryId.ToString(),
            system: system,
            user: sb.ToString(),
            maxTokens: 800,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> DraftPatientSummaryAsync(PatientSummaryInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiPatientSummaryEnabled, ct))
            return new AiOutcome(false, null, null, "Patient summary drafting is disabled for this facility.");

        var lang = string.IsNullOrWhiteSpace(input.PreferredLanguage) ? "English" : input.PreferredLanguage!;
        var system =
            "You are writing a plain-language visit summary for a patient in a Nigerian hospital. The patient may have low health literacy. " +
            "Use simple words, short sentences, no jargon. Avoid scary-sounding medical terms unless explained in plain language in parentheses. " +
            $"Write in {lang}. Output these sections, each titled clearly:\n" +
            "WHAT WE FOUND:\n<one-paragraph plain summary>\n\n" +
            "YOUR DIAGNOSIS / WHAT IS GOING ON:\n<plain-language>\n\n" +
            "MEDICATIONS YOU WERE GIVEN:\n<list each, with what it's for and how to take it>\n\n" +
            "TESTS / SCANS DONE:\n<list briefly>\n\n" +
            "WHAT TO DO NEXT:\n<follow-up plan, when to come back, when to seek urgent care>\n\n" +
            "WARNING SIGNS — COME BACK IMMEDIATELY IF:\n<bullets — relevant red flags>\n\n" +
            "Do NOT diagnose new things. Stick to what is provided. " +
            SafetyFooter;

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(input.PatientAgeSex)) sb.AppendLine($"Patient: {PhiScrubber.Scrub(input.PatientAgeSex)}");
        if (!string.IsNullOrWhiteSpace(input.ChiefComplaint)) sb.AppendLine($"Chief complaint: {PhiScrubber.Scrub(input.ChiefComplaint)}");
        var dx = input.Diagnoses.ToList();
        if (dx.Count > 0) { sb.AppendLine("Diagnoses:"); foreach (var d in dx) sb.AppendLine("- " + PhiScrubber.Scrub(d)); }
        var rx = input.Prescriptions.ToList();
        if (rx.Count > 0) { sb.AppendLine("Prescriptions:"); foreach (var r in rx) sb.AppendLine("- " + PhiScrubber.Scrub(r)); }
        var orders = input.LabsImaging.ToList();
        if (orders.Count > 0) { sb.AppendLine("Tests/scans:"); foreach (var o in orders) sb.AppendLine("- " + PhiScrubber.Scrub(o)); }
        if (!string.IsNullOrWhiteSpace(input.FollowUpPlan)) sb.AppendLine("Follow-up plan: " + PhiScrubber.Scrub(input.FollowUpPlan));

        return await DispatchAsync(
            AiFeature.PatientSummary,
            facilityId: input.FacilityId,
            entityType: "Encounter",
            entityKey: input.EncounterId.ToString(),
            system: system,
            user: sb.ToString(),
            maxTokens: 1200,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> SymptomCheckAsync(SymptomCheckerInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiSymptomCheckerEnabled, ct))
            return new AiOutcome(false, null, null, "Symptom checker is disabled for this facility.");

        var system =
            "You are a patient-facing symptom-triage assistant for a Nigerian hospital portal. The patient is NOT a clinician. " +
            "Your job is ONLY to give simple advice on whether to: (a) book a routine appointment, (b) come to the hospital today, " +
            "or (c) seek emergency care immediately. You must NOT diagnose. You must NOT prescribe. Use plain English. " +
            "Output exactly:\n" +
            "ADVICE: <Book appointment | Come in today | Go to A&E now | Self-care at home>\n" +
            "WHY: <one short sentence>\n" +
            "WHAT TO WATCH FOR: <2-3 bullets — symptoms that should prompt urgent care>\n" +
            "Be conservative — when in doubt prefer the more urgent option. Always end with: 'This is general guidance. Talk to a clinician for diagnosis or treatment.' " +
            SafetyFooter;

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(input.PatientAgeSex)) sb.AppendLine($"Patient: {PhiScrubber.Scrub(input.PatientAgeSex)}");
        sb.AppendLine();
        sb.AppendLine("Question: " + PhiScrubber.Scrub(input.Question));
        var hist = input.History.ToList();
        if (hist.Count > 0)
        {
            sb.AppendLine();
            sb.AppendLine("Recent conversation:");
            foreach (var h in hist.TakeLast(6)) sb.AppendLine("- " + PhiScrubber.Scrub(h));
        }

        return await DispatchAsync(
            AiFeature.SymptomChecker,
            facilityId: input.FacilityId,
            entityType: "PortalAccount",
            entityKey: input.PortalAccountId?.ToString() ?? "0",
            system: system,
            user: sb.ToString(),
            maxTokens: 400,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> ParseAdherenceAsync(AdherenceParseInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiAdherenceParseEnabled, ct))
            return new AiOutcome(false, null, null, "Adherence parsing is disabled for this facility.");

        var system =
            "You are a medication-adherence assistant. Given a patient's free-text SMS reply about a specific drug, classify their adherence. " +
            "Output exactly:\n" +
            "STATUS: <Taking_as_prescribed | Missed_doses | Stopped | Side_effect | Confused | Unclear | Other>\n" +
            "SIDE_EFFECTS: <bullets if any reported, else 'none'>\n" +
            "ESCALATE: <Yes|No> — yes if side-effect, stopping treatment, or confusion needs clinician follow-up\n" +
            "REPLY_DRAFT: <one short reassuring SMS reply (≤160 chars) suitable to send back; do not give medical advice beyond 'please discuss with your clinician'>\n" +
            SafetyFooter;

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(input.PatientAgeSex)) sb.AppendLine($"Patient: {PhiScrubber.Scrub(input.PatientAgeSex)}");
        sb.AppendLine($"Drug: {PhiScrubber.Scrub(input.DrugName)}");
        sb.AppendLine();
        sb.AppendLine("Patient SMS reply:");
        sb.AppendLine(PhiScrubber.Scrub(input.SmsReply));

        return await DispatchAsync(
            AiFeature.AdherenceParse,
            facilityId: input.FacilityId,
            entityType: "Adherence",
            entityKey: "0",
            system: system,
            user: sb.ToString(),
            maxTokens: 400,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> TranslateAsync(TranslateInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiTranslateEnabled, ct))
            return new AiOutcome(false, null, null, "Translation is disabled for this facility.");

        var system =
            $"You are a careful medical translator. Translate the input from {input.SourceLanguage} to {input.TargetLanguage}. " +
            "Preserve clinical accuracy. Use plain language suitable for a patient unless the context says otherwise. " +
            "Keep dosages, drug names, and numbers exactly as in the source. Do NOT add or remove information. " +
            "Output ONLY the translated text — no preamble, no notes. " +
            SafetyFooter;

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(input.ContextHint)) sb.AppendLine("Context: " + input.ContextHint).AppendLine();
        sb.Append(PhiScrubber.Scrub(input.Text));

        return await DispatchAsync(
            AiFeature.Translate,
            facilityId: input.FacilityId,
            entityType: "Translation",
            entityKey: "0",
            system: system,
            user: sb.ToString(),
            maxTokens: 1200,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> DetectAuditAnomalyAsync(AuditAnomalyInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiAuditAnomalyEnabled, ct))
            return new AiOutcome(false, null, null, "Audit anomaly detection is disabled for this facility.");

        var system =
            "You are a healthcare data-access auditor. Review the audit-log slice provided and flag anomalies that may indicate " +
            "inappropriate access (e.g. after-hours patient lookups, mass record exports, role drift, repeated denials, sensitive data access without business reason). " +
            "Output exactly:\n" +
            "OVERALL: <Normal | Suspicious | Critical>\n" +
            "FLAGGED PATTERNS: <bullets — each names the actor and the suspicious behaviour, or 'none'>\n" +
            "RECOMMENDED ACTIONS: <bullets — investigations the security officer should run, or 'none'>\n" +
            SafetyFooter;

        var sb = new StringBuilder();
        sb.AppendLine($"Window: last {input.WindowHours} hours");
        sb.AppendLine();
        sb.AppendLine("Entries (timestamp · actor · action · entity · ip · outcome):");
        foreach (var e in input.Entries)
            sb.AppendLine($"- {e.AtUtc:yyyy-MM-dd HH:mm} | {e.ActorName} | {e.Action} | {e.EntityType ?? "-"}/{e.EntityKey ?? "-"} | {e.IpAddress ?? "-"} | {e.Outcome ?? "-"}");

        return await DispatchAsync(
            AiFeature.AuditAnomaly,
            facilityId: input.FacilityId,
            entityType: "Audit",
            entityKey: "0",
            system: system,
            user: sb.ToString(),
            maxTokens: 800,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<AiOutcome> ScoreDocQualityAsync(DocQualityInput input, string? requestedById, CancellationToken ct = default)
    {
        if (!await IsFeatureEnabledAsync(input.FacilityId, f => f.AiDocQualityEnabled, ct))
            return new AiOutcome(false, null, null, "Documentation quality scoring is disabled for this facility.");

        var system =
            "You are a clinical-documentation quality reviewer for a Nigerian hospital. Given an encounter's content, score completeness " +
            "and identify what is missing before sign-off. Output exactly:\n" +
            "COMPLETENESS SCORE: <0-100>\n" +
            "STRENGTHS: <bullets — what is well documented, or 'none'>\n" +
            "GAPS: <bullets — specific missing elements (e.g. 'no examination findings', 'no plan', 'no review-of-systems', 'diagnosis not coded')>\n" +
            "MUST-FIX BEFORE SIGN-OFF: <bullets — items that materially affect care or billing>\n" +
            SafetyFooter;

        var sb = new StringBuilder();
        if (!string.IsNullOrWhiteSpace(input.ChiefComplaint)) sb.AppendLine("Chief complaint: " + PhiScrubber.Scrub(input.ChiefComplaint));
        if (!string.IsNullOrWhiteSpace(input.Subjective)) sb.AppendLine("Subjective: " + PhiScrubber.Scrub(input.Subjective));
        if (!string.IsNullOrWhiteSpace(input.Objective)) sb.AppendLine("Objective: " + PhiScrubber.Scrub(input.Objective));
        if (!string.IsNullOrWhiteSpace(input.Assessment)) sb.AppendLine("Assessment: " + PhiScrubber.Scrub(input.Assessment));
        if (!string.IsNullOrWhiteSpace(input.Plan)) sb.AppendLine("Plan: " + PhiScrubber.Scrub(input.Plan));
        var dx = input.Diagnoses.ToList();
        if (dx.Count > 0) { sb.AppendLine("Diagnoses:"); foreach (var d in dx) sb.AppendLine("- " + PhiScrubber.Scrub(d)); }
        var orders = input.Orders.ToList();
        if (orders.Count > 0) { sb.AppendLine("Orders:"); foreach (var o in orders) sb.AppendLine("- " + PhiScrubber.Scrub(o)); }

        return await DispatchAsync(
            AiFeature.DocQuality,
            facilityId: input.FacilityId,
            entityType: "Encounter",
            entityKey: input.EncounterId.ToString(),
            system: system,
            user: sb.ToString(),
            maxTokens: 700,
            requestedById: requestedById,
            ct: ct);
    }

    public async Task<bool> ReviewAsync(long suggestionId, AiSuggestionStatus status, string? editedContent, string reviewerId, CancellationToken ct = default)
    {
        var s = await _db.AiSuggestions.FirstOrDefaultAsync(x => x.Id == suggestionId, ct);
        if (s is null) return false;
        s.Status = status;
        s.EditedContent = editedContent;
        s.ReviewedAtUtc = DateTime.UtcNow;
        s.ReviewedById = reviewerId;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ai.review", AuditCategory.BusinessAction, AuditOutcome.Success,
            entityType: s.EntityType, entityKey: s.EntityKey, summary: $"AI {s.Feature} {status}",
            facilityId: s.FacilityId);
        return true;
    }

    private async Task<bool> IsFeatureEnabledAsync(int facilityId, Func<Models.Identity.Facility, bool> selector, CancellationToken ct)
    {
        if (!_ai.IsConfigured) return false;
        var f = await _db.Facilities.AsNoTracking().FirstOrDefaultAsync(x => x.Id == facilityId, ct);
        return f != null && f.AiEnabled && selector(f);
    }

    private async Task<AiOutcome> DispatchAsync(AiFeature feature, int facilityId, string entityType, string entityKey,
        string system, string user, int maxTokens, string? requestedById, CancellationToken ct)
    {
        var suggestion = new AiSuggestion
        {
            FacilityId = facilityId,
            Feature = feature,
            Status = AiSuggestionStatus.Pending,
            EntityType = entityType,
            EntityKey = entityKey,
            Provider = _ai.Provider,
            Model = _ai.Model,
            Prompt = TruncatePrompt(system + "\n---\n" + user),
            RequestedById = requestedById
        };
        _db.AiSuggestions.Add(suggestion);
        await _db.SaveChangesAsync(ct);

        var result = await _ai.CompleteAsync(new AiCompletionRequest(system, user, maxTokens), ct);
        suggestion.InputTokens = result.InputTokens;
        suggestion.OutputTokens = result.OutputTokens;
        suggestion.LatencyMs = result.LatencyMs;
        suggestion.Provider = result.Provider;
        suggestion.Model = result.Model;

        if (!result.Success)
        {
            suggestion.Status = AiSuggestionStatus.Failed;
            suggestion.ErrorMessage = result.ErrorMessage;
            await _db.SaveChangesAsync(ct);
            await _audit.LogAsync("ai.fail", AuditCategory.System, AuditOutcome.Failure,
                entityType: entityType, entityKey: entityKey, summary: $"AI {feature} failed: {result.ErrorMessage}",
                facilityId: facilityId);
            return new AiOutcome(false, suggestion.Id, null, result.ErrorMessage);
        }

        suggestion.Response = result.Text;
        await _db.SaveChangesAsync(ct);
        await _audit.LogAsync("ai.draft", AuditCategory.BusinessAction, AuditOutcome.Success,
            entityType: entityType, entityKey: entityKey, summary: $"AI {feature} drafted ({result.OutputTokens} tok)",
            facilityId: facilityId);
        return new AiOutcome(true, suggestion.Id, result.Text, null);
    }

    private static string TruncatePrompt(string s, int maxChars = 8000)
        => s.Length <= maxChars ? s : s.Substring(0, maxChars) + "... [truncated]";
}
