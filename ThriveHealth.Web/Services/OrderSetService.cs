using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Clinical;
using ThriveHealth.Web.Models.Emergency;

namespace ThriveHealth.Web.Services;

public record OrderSetSummary(string Key, string Name, string Description, string Icon, string Tone, int LabCount, int ImagingCount, int DrugCount, int ProcedureCount);

public record OrderSetApplication(
    int LabsAdded, int ImagingAdded, int DrugsAdded, int ProceduresAdded);

public interface IOrderSetService
{
    IReadOnlyList<OrderSetSummary> List();
    Task<OrderSetApplication> ApplyAsync(string key, int encounterId, string userId, CancellationToken ct = default);
}

public class OrderSetService : IOrderSetService
{
    private readonly ApplicationDbContext _db;
    public OrderSetService(ApplicationDbContext db) => _db = db;

    private record LabSpec(string Test, string? Specimen, OrderUrgency Urgency = OrderUrgency.Stat);
    private record ImagingSpec(ImagingModality Modality, string Study, OrderUrgency Urgency = OrderUrgency.Stat);
    private record DrugSpec(string Drug, string Dose, string Route, string Frequency, string Instructions);
    private record ProcSpec(string Procedure, string? Notes = null);
    private record OrderSetDef(string Key, string Name, string Description, string Icon, string Tone,
        IReadOnlyList<LabSpec> Labs, IReadOnlyList<ImagingSpec> Imaging,
        IReadOnlyList<DrugSpec> Drugs, IReadOnlyList<ProcSpec> Procedures,
        string Indication);

    private static readonly IReadOnlyList<OrderSetDef> Sets = new List<OrderSetDef>
    {
        new("sepsis", "Sepsis Bundle (Hour-1)", "Surviving Sepsis Campaign hour-1 bundle", "bi-bug", "danger",
            Labs: new[]
            {
                new LabSpec("Full Blood Count", "Whole blood (EDTA)"),
                new LabSpec("Urea & Electrolytes", "Serum"),
                new LabSpec("Liver Function Tests", "Serum"),
                new LabSpec("C-Reactive Protein", "Serum"),
                new LabSpec("Lactate", "Whole blood"),
                new LabSpec("Blood Culture x2", "Blood"),
                new LabSpec("Procalcitonin", "Serum")
            },
            Imaging: Array.Empty<ImagingSpec>(),
            Drugs: new[]
            {
                new DrugSpec("Ceftriaxone", "2 g", "IV", "STAT", "Broad-spectrum after cultures drawn"),
                new DrugSpec("Crystalloid (Normal Saline)", "30 ml/kg", "IV", "Over 1 hour", "Initial fluid resuscitation"),
                new DrugSpec("Paracetamol", "1 g", "IV", "STAT", "Antipyretic")
            },
            Procedures: new[]
            {
                new ProcSpec("Insert two large-bore IV cannulae"),
                new ProcSpec("Continuous cardiac monitoring + SpO₂"),
                new ProcSpec("Catheterise + measure hourly urine output")
            },
            Indication: "Suspected sepsis (qSOFA ≥2 or SIRS criteria)"),

        new("trauma", "Major Trauma Bundle", "Initial workup for major trauma activation", "bi-bandaid", "danger",
            Labs: new[]
            {
                new LabSpec("Full Blood Count", "Whole blood (EDTA)"),
                new LabSpec("Group & Save / Crossmatch", "Whole blood (EDTA pink)"),
                new LabSpec("Coagulation screen (PT/INR/APTT)", "Citrated plasma"),
                new LabSpec("Urea & Electrolytes", "Serum"),
                new LabSpec("Lactate", "Whole blood"),
                new LabSpec("Beta-HCG (if female of reproductive age)", "Serum")
            },
            Imaging: new[]
            {
                new ImagingSpec(ImagingModality.XRay, "Chest X-ray (supine AP)"),
                new ImagingSpec(ImagingModality.XRay, "Pelvis X-ray (AP)"),
                new ImagingSpec(ImagingModality.Ultrasound, "FAST scan (Focused Assessment with Sonography for Trauma)"),
                new ImagingSpec(ImagingModality.CT, "CT trauma series (head/C-spine/chest/abdo/pelvis)", OrderUrgency.Urgent)
            },
            Drugs: new[]
            {
                new DrugSpec("Tranexamic acid", "1 g", "IV", "Over 10 minutes", "Within 3 hours of injury"),
                new DrugSpec("Morphine sulphate", "5 mg", "IV", "Titrate", "Analgesia")
            },
            Procedures: new[]
            {
                new ProcSpec("Two large-bore IV cannulae (16G or larger)"),
                new ProcSpec("Cervical collar + spinal precautions"),
                new ProcSpec("Catheterise + measure urine output")
            },
            Indication: "Major trauma activation (high-energy mechanism, abnormal vitals)"),

        new("acs", "Acute Coronary Syndrome", "Initial workup for suspected ACS / MI", "bi-heart-pulse", "danger",
            Labs: new[]
            {
                new LabSpec("Troponin I (high-sensitivity)", "Serum"),
                new LabSpec("Full Blood Count", "Whole blood (EDTA)"),
                new LabSpec("Urea & Electrolytes", "Serum"),
                new LabSpec("Lipid profile", "Serum (fasting if possible)"),
                new LabSpec("Glucose", "Serum")
            },
            Imaging: new[]
            {
                new ImagingSpec(ImagingModality.XRay, "Chest X-ray (PA / supine if unstable)")
            },
            Drugs: new[]
            {
                new DrugSpec("Aspirin", "300 mg", "PO", "STAT", "Chew and swallow"),
                new DrugSpec("Clopidogrel", "300 mg", "PO", "STAT", "Loading dose"),
                new DrugSpec("Glyceryl Trinitrate", "0.4 mg", "Sublingual", "PRN", "Repeat every 5 min × 3 if pain persists"),
                new DrugSpec("Morphine sulphate", "2.5–5 mg", "IV", "Titrate", "Analgesia + anxiolysis")
            },
            Procedures: new[]
            {
                new ProcSpec("12-lead ECG within 10 minutes"),
                new ProcSpec("Continuous cardiac monitoring"),
                new ProcSpec("IV access × 2"),
                new ProcSpec("Oxygen if SpO₂ < 94%")
            },
            Indication: "Suspected ACS (ischaemic chest pain)"),

        new("anaphylaxis", "Anaphylaxis", "Acute anaphylactic reaction protocol", "bi-exclamation-triangle", "warning",
            Labs: new[]
            {
                new LabSpec("Tryptase (mast cell)", "Serum (within 1 hour)"),
                new LabSpec("Full Blood Count", "Whole blood (EDTA)")
            },
            Imaging: Array.Empty<ImagingSpec>(),
            Drugs: new[]
            {
                new DrugSpec("Adrenaline (epinephrine)", "0.5 mg (1:1000)", "IM (anterolateral thigh)", "STAT — repeat 5 min", "First-line; do not delay"),
                new DrugSpec("Hydrocortisone", "200 mg", "IV", "STAT", ""),
                new DrugSpec("Chlorpheniramine", "10 mg", "IV", "STAT", ""),
                new DrugSpec("Salbutamol nebuliser", "5 mg in 5 ml", "Nebulised", "PRN", "If bronchospasm"),
                new DrugSpec("Crystalloid (Normal Saline)", "1000 ml", "IV", "Over 30 min", "If hypotensive")
            },
            Procedures: new[]
            {
                new ProcSpec("Remove suspected trigger"),
                new ProcSpec("High-flow oxygen 15 L/min via non-rebreather"),
                new ProcSpec("Lay flat + raise legs (if hypotensive)")
            },
            Indication: "Anaphylactic reaction (urticaria + airway/circulatory compromise)"),

        new("stroke", "Acute Stroke / TIA", "Initial workup for acute stroke", "bi-activity", "warning",
            Labs: new[]
            {
                new LabSpec("Random Blood Glucose", "Whole blood / capillary"),
                new LabSpec("Full Blood Count", "Whole blood (EDTA)"),
                new LabSpec("Urea & Electrolytes", "Serum"),
                new LabSpec("Coagulation screen (PT/INR/APTT)", "Citrated plasma"),
                new LabSpec("Lipid profile", "Serum")
            },
            Imaging: new[]
            {
                new ImagingSpec(ImagingModality.CT, "CT brain plain (rule out haemorrhage)")
            },
            Drugs: Array.Empty<DrugSpec>(),
            Procedures: new[]
            {
                new ProcSpec("12-lead ECG"),
                new ProcSpec("Two large-bore IV cannulae"),
                new ProcSpec("Continuous cardiac monitoring + SpO₂"),
                new ProcSpec("NIH Stroke Scale assessment"),
                new ProcSpec("Nil by mouth until swallow assessed")
            },
            Indication: "Acute neurological deficit / suspected stroke")
    };

    public IReadOnlyList<OrderSetSummary> List() =>
        Sets.Select(s => new OrderSetSummary(
            s.Key, s.Name, s.Description, s.Icon, s.Tone,
            s.Labs.Count, s.Imaging.Count, s.Drugs.Count, s.Procedures.Count)).ToList();

    public async Task<OrderSetApplication> ApplyAsync(string key, int encounterId, string userId, CancellationToken ct = default)
    {
        var set = Sets.FirstOrDefault(s => s.Key == key) ?? throw new InvalidOperationException("Unknown order set");
        var enc = await _db.Encounters.FirstOrDefaultAsync(e => e.Id == encounterId, ct)
            ?? throw new InvalidOperationException("Encounter not found");

        var labCatalog = await _db.LabTests.AsNoTracking().Where(t => t.IsActive).ToListAsync(ct);
        foreach (var l in set.Labs)
        {
            var match = labCatalog.FirstOrDefault(t =>
                string.Equals(t.Name, l.Test, StringComparison.OrdinalIgnoreCase));
            _db.LabOrders.Add(new LabOrder
            {
                EncounterId = enc.Id, PatientId = enc.PatientId,
                LabTestId = match?.Id,
                TestName = match?.Name ?? l.Test,
                LoincCode = match?.LoincCode,
                Specimen = l.Specimen ?? match?.Specimen,
                Urgency = l.Urgency, Status = OrderStatus.Ordered,
                ClinicalIndication = set.Indication,
                OrderedById = userId
            });
        }

        foreach (var i in set.Imaging)
        {
            _db.ImagingOrders.Add(new ImagingOrder
            {
                EncounterId = enc.Id, PatientId = enc.PatientId,
                Modality = i.Modality, StudyDescription = i.Study,
                Urgency = i.Urgency, Status = OrderStatus.Ordered,
                ClinicalIndication = set.Indication,
                OrderedById = userId
            });
        }

        if (set.Drugs.Count > 0)
        {
            var rx = new Prescription
            {
                EncounterId = enc.Id, PatientId = enc.PatientId,
                Status = PrescriptionStatus.Issued,
                IssuedAt = DateTime.UtcNow,
                PrescribedById = userId,
                Notes = $"Auto-issued by '{set.Name}' order set"
            };
            foreach (var d in set.Drugs)
            {
                rx.Items.Add(new PrescriptionItem
                {
                    DrugName = d.Drug, Dose = d.Dose, Route = d.Route, Frequency = d.Frequency,
                    Instructions = d.Instructions
                });
            }
            _db.Prescriptions.Add(rx);
        }

        foreach (var p in set.Procedures)
        {
            _db.ProcedureOrders.Add(new ProcedureOrder
            {
                EncounterId = enc.Id, PatientId = enc.PatientId,
                ProcedureName = p.Procedure, Notes = p.Notes,
                Urgency = OrderUrgency.Stat, Status = OrderStatus.Ordered,
                OrderedById = userId
            });
        }

        _db.ResusEvents.Add(new ResusEvent
        {
            EncounterId = enc.Id,
            Kind = ResusEventKind.Note,
            Description = $"Order set applied: {set.Name}",
            Details = $"{set.Labs.Count} labs, {set.Imaging.Count} imaging, {set.Drugs.Count} drugs, {set.Procedures.Count} procedures",
            RecordedById = userId
        });

        await _db.SaveChangesAsync(ct);
        return new OrderSetApplication(set.Labs.Count, set.Imaging.Count, set.Drugs.Count, set.Procedures.Count);
    }
}
