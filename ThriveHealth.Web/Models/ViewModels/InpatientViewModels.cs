using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Inpatient;
using ThriveHealth.Web.Models.Patients;

namespace ThriveHealth.Web.Models.ViewModels;

public class WardEditViewModel
{
    public int Id { get; set; }
    [Required, MaxLength(100)] public string Name { get; set; } = string.Empty;
    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    public WardType Type { get; set; } = WardType.GeneralMedical;
    [MaxLength(20)] public string ColorHex { get; set; } = "#1f6feb";
    public bool IsActive { get; set; } = true;
}

public class BedBoardViewModel
{
    public IReadOnlyList<WardWithBeds> Wards { get; set; } = Array.Empty<WardWithBeds>();
    public int TotalBeds { get; set; }
    public int FreeBeds { get; set; }
    public int OccupiedBeds { get; set; }
    public int OutOfServiceBeds { get; set; }
}

public class WardWithBeds
{
    public Ward Ward { get; set; } = null!;
    public IReadOnlyList<BedView> Beds { get; set; } = Array.Empty<BedView>();
}

public class BedView
{
    public Bed Bed { get; set; } = null!;
    public Patient? CurrentPatient { get; set; }
    public Admission? CurrentAdmission { get; set; }
}

public class AdmitViewModel
{
    public int? PatientId { get; set; }
    public string? PatientLabel { get; set; }
    public int? SourceEncounterId { get; set; }

    public int? WardId { get; set; }
    public int? BedId { get; set; }
    public string? AdmittingDoctorId { get; set; }

    [Required, MaxLength(500), Display(Name = "Reason for admission")] public string ReasonForAdmission { get; set; } = string.Empty;
    [MaxLength(500), Display(Name = "Working diagnosis")] public string? WorkingDiagnosis { get; set; }
}

public class AdmissionListRow
{
    public Admission Admission { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
}

public class AdmissionViewModel
{
    public Admission Admission { get; set; } = null!;
    public Patient Patient { get; set; } = null!;
    public IReadOnlyList<InpatientMedication> Medications { get; set; } = Array.Empty<InpatientMedication>();
    public IReadOnlyList<MarSlot> DueSlots { get; set; } = Array.Empty<MarSlot>();
    public IReadOnlyList<VitalsRecord> Vitals { get; set; } = Array.Empty<VitalsRecord>();
    public IReadOnlyList<FluidEntry> Fluids { get; set; } = Array.Empty<FluidEntry>();
    public IReadOnlyList<NursingNote> NursingNotes { get; set; } = Array.Empty<NursingNote>();
    public IReadOnlyList<WardRoundEntry> WardRounds { get; set; } = Array.Empty<WardRoundEntry>();
    public int Input24h { get; set; }
    public int Output24h { get; set; }
    public int Net24h => Input24h - Output24h;
}

public class AddMedicationViewModel
{
    public int AdmissionId { get; set; }
    public int? DrugId { get; set; }
    [Required, MaxLength(200)] public string DrugName { get; set; } = string.Empty;
    [MaxLength(50)] public string? Strength { get; set; }
    [MaxLength(50)] public string? Dose { get; set; }
    [MaxLength(50)] public string? Route { get; set; }
    [MaxLength(80)] public string? Frequency { get; set; } = "BD";
    [MaxLength(300)] public string? Instructions { get; set; }
    public InpatientMedicationKind Kind { get; set; } = InpatientMedicationKind.Regular;

    [DataType(DataType.DateTime), Display(Name = "Start")] public DateTime? StartUtc { get; set; }
    [DataType(DataType.Date), Display(Name = "End")] public DateOnly? EndDate { get; set; }
}

public class AdministerSlotViewModel
{
    public int SlotId { get; set; }
    public MarSlotStatus Status { get; set; } = MarSlotStatus.Given;
    [MaxLength(50)] public string? ActualDose { get; set; }
    [MaxLength(50)] public string? Route { get; set; }
    [MaxLength(40)] public string? BatchNumber { get; set; }
    [MaxLength(500)] public string? Notes { get; set; }
}

public class FluidAddViewModel
{
    public int AdmissionId { get; set; }
    public FluidKind Kind { get; set; } = FluidKind.Input;
    public FluidType Type { get; set; } = FluidType.Oral;
    [Range(1, 5000)] public int VolumeMl { get; set; }
    [MaxLength(100)] public string? Description { get; set; }
}

public class NursingNoteViewModel
{
    public int AdmissionId { get; set; }
    [MaxLength(20)] public string? Shift { get; set; } = "Morning";
    [Required, MaxLength(4000)] public string Body { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Handover { get; set; }
}

public class WardRoundViewModel
{
    public int AdmissionId { get; set; }
    [Required, MaxLength(4000)] public string Body { get; set; } = string.Empty;
    [MaxLength(500)] public string? PlanChanges { get; set; }
}

public class DischargeViewModel
{
    public int AdmissionId { get; set; }
    public DischargeDisposition Disposition { get; set; } = DischargeDisposition.Home;
    [Required, MaxLength(500)] public string DischargeDiagnosis { get; set; } = string.Empty;
    [MaxLength(2000)] public string? Summary { get; set; }
    [MaxLength(1000)] public string? FollowUp { get; set; }
}

public class TransferViewModel
{
    public int AdmissionId { get; set; }
    public int NewBedId { get; set; }
    [MaxLength(300)] public string? Reason { get; set; }
}
