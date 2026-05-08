using ThriveHealth.Web.Models.Ai;

namespace ThriveHealth.Web.Services.Ai;

public sealed record LabInterpretInput(
    int FacilityId,
    int LabOrderId,
    string TestName,
    IEnumerable<LabAnalyteSnapshot> Analytes,
    string? PatientAgeSex,
    string? GeneralComment);

public sealed record LabAnalyteSnapshot(string Name, string Value, string? Unit, string? RefRange, string Flag);

public sealed record DifferentialInput(
    int FacilityId,
    int EncounterId,
    string ChiefComplaint,
    string? HistoryOfPresentIllness,
    string? Vitals,
    IEnumerable<string> Allergies,
    IEnumerable<string> CurrentMedications,
    string? PatientAgeSex);

public sealed record DischargeInput(
    int FacilityId,
    int AdmissionId,
    string? PatientAgeSex,
    string Reason,
    string? WorkingDiagnosis,
    IEnumerable<string> Diagnoses,
    IEnumerable<string> Medications,
    int LengthOfStayDays,
    int InputMl24h,
    int OutputMl24h,
    IEnumerable<string> RecentNotes);

public sealed record ImagingDraftInput(
    int FacilityId,
    int ImagingOrderId,
    string Modality,
    string Study,
    string? ClinicalIndication,
    string? Technique,
    string? Contrast,
    string? Findings,
    string? PatientAgeSex);

public sealed record TriageAssistInput(
    int FacilityId,
    int? PatientId,
    string ChiefComplaint,
    string? Mechanism,
    string? Vitals,
    string? Avpu,
    int? GcsTotal,
    bool IsTrauma,
    bool IsPregnant,
    string? PatientAgeSex);

public sealed record DrugContextCheckInput(
    int FacilityId,
    int? EncounterId,
    IEnumerable<string> Proposed,
    IEnumerable<string> Existing,
    IEnumerable<string> Allergies,
    IEnumerable<string> Conditions,
    string? RenalNote,
    string? PatientAgeSex);

public sealed record IcdCodingInput(
    int FacilityId,
    int EncounterId,
    string DiagnosisText);

public sealed record NlSearchInput(int FacilityId, string Question, string? ContextSummary);

public sealed record SchedulingAssistInput(
    int FacilityId,
    string ChiefComplaint,
    string? PatientAgeSex,
    string? Urgency,
    IEnumerable<ClinicCapacitySnapshot> Clinics);

public sealed record ClinicCapacitySnapshot(int ClinicId, string Name, string? Specialty, int OpenSlotsToday, int OpenSlotsThisWeek);

public sealed record InventoryForecastInput(int FacilityId, IEnumerable<ItemDemandSnapshot> Items, int LookaheadDays);

public sealed record ItemDemandSnapshot(string Name, int OnHand, int? ReorderLevel, int Last30dDispensed, int Last90dDispensed, DateOnly? NearestExpiry);

public sealed record ClaimsRiskInput(int FacilityId, int ClaimId, string Payer, string? Plan, decimal TotalAmount, IEnumerable<string> Items, string? Diagnosis);

public sealed record BillAnomalyInput(int FacilityId, int BillId, string Context, IEnumerable<string> Charges);

public sealed record EcgInterpretInput(int FacilityId, int? PatientId, string? PatientAgeSex, string? ClinicalContext, string EcgFindings);

public sealed record AncRiskInput(
    int FacilityId,
    int AncRecordId,
    string? PatientAgeSex,
    int? Gravida, int? Para,
    int? GestationalWeeks,
    string? PreviousObstetricHistory,
    string? CoMorbidities,
    string? LatestVisitVitals,
    decimal? Bmi,
    string? HivStatus,
    string? Notes);

public sealed record PaedsDoseInput(
    int FacilityId,
    int? PatientId,
    decimal WeightKg,
    int? AgeMonths,
    string Drug,
    string ProposedDose,
    string? Route,
    string? Frequency,
    string? Indication,
    IEnumerable<string> Allergies);

public sealed record IdsrOutbreakInput(
    int FacilityId,
    int WindowDays,
    IEnumerable<DiseaseClusterSnapshot> Clusters);

public sealed record DiseaseClusterSnapshot(string DiseaseCode, string DiseaseName, int CasesInWindow, int CasesPriorWindow, int? DistinctLgas, int? DistinctWards);

public sealed record ReferralDraftInput(
    int FacilityId,
    int? EncounterId,
    int? AdmissionId,
    string? PatientAgeSex,
    string ClinicalContext,
    string? Diagnoses,
    string ReceivingFacility,
    string? Reason,
    IEnumerable<string> KeyFindings,
    IEnumerable<string> Medications);

public sealed record SoapStructureInput(
    int FacilityId,
    int EncounterId,
    string Narrative,
    string? PatientAgeSex);

public sealed record MortuaryDraftInput(
    int FacilityId,
    int MortuaryEntryId,
    string? PatientAgeSex,
    string? CauseOfDeath,
    string? Manner,
    string? PlaceOfDeath,
    DateTime? DateOfDeathUtc,
    string? ReleasedTo,
    string? ReleaseAuthorityRef);

public sealed record PatientSummaryInput(
    int FacilityId,
    int EncounterId,
    string? PatientAgeSex,
    string? ChiefComplaint,
    IEnumerable<string> Diagnoses,
    IEnumerable<string> Prescriptions,
    IEnumerable<string> LabsImaging,
    string? FollowUpPlan,
    string? PreferredLanguage);

public sealed record SymptomCheckerInput(
    int FacilityId,
    int? PortalAccountId,
    string? PatientAgeSex,
    string Question,
    IEnumerable<string> History);

public sealed record AdherenceParseInput(
    int FacilityId,
    string? PatientAgeSex,
    string DrugName,
    string SmsReply);

public sealed record TranslateInput(
    int FacilityId,
    string SourceLanguage,
    string TargetLanguage,
    string Text,
    string? ContextHint);

public sealed record AuditAnomalyInput(
    int FacilityId,
    int WindowHours,
    IEnumerable<AuditLogSnapshot> Entries);

public sealed record AuditLogSnapshot(
    string ActorName,
    string Action,
    string? EntityType,
    string? EntityKey,
    DateTime AtUtc,
    string? IpAddress,
    string? Outcome);

public sealed record DocQualityInput(
    int FacilityId,
    int EncounterId,
    string? ChiefComplaint,
    string? Subjective,
    string? Objective,
    string? Assessment,
    string? Plan,
    IEnumerable<string> Diagnoses,
    IEnumerable<string> Orders);

public sealed record AiOutcome(bool Ok, long? SuggestionId, string? Text, string? Error);

public interface IClinicalAiService
{
    Task<AiOutcome> InterpretLabAsync(LabInterpretInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> SuggestDifferentialAsync(DifferentialInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> DraftDischargeSummaryAsync(DischargeInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> DraftImagingReportAsync(ImagingDraftInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> AssistTriageAsync(TriageAssistInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> CheckDrugContextAsync(DrugContextCheckInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> SuggestIcdCodingAsync(IcdCodingInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> AskNlSearchAsync(NlSearchInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> SuggestSchedulingAsync(SchedulingAssistInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> ForecastInventoryAsync(InventoryForecastInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> AssessClaimRiskAsync(ClaimsRiskInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> DetectBillAnomalyAsync(BillAnomalyInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> InterpretEcgAsync(EcgInterpretInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> AssessAncRiskAsync(AncRiskInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> CheckPaedsDoseAsync(PaedsDoseInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> DetectIdsrOutbreakAsync(IdsrOutbreakInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> DraftReferralLetterAsync(ReferralDraftInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> StructureSoapAsync(SoapStructureInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> DraftMortuaryDocsAsync(MortuaryDraftInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> DraftPatientSummaryAsync(PatientSummaryInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> SymptomCheckAsync(SymptomCheckerInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> ParseAdherenceAsync(AdherenceParseInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> TranslateAsync(TranslateInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> DetectAuditAnomalyAsync(AuditAnomalyInput input, string? requestedById, CancellationToken ct = default);
    Task<AiOutcome> ScoreDocQualityAsync(DocQualityInput input, string? requestedById, CancellationToken ct = default);

    Task<bool> ReviewAsync(long suggestionId, AiSuggestionStatus status, string? editedContent, string reviewerId, CancellationToken ct = default);
}
