using ThriveHealth.Web.Models.Identity;

namespace ThriveHealth.Web.Models.Patients;

public class VitalsRecord
{
    public int Id { get; set; }
    public int PatientId { get; set; }
    public Patient? Patient { get; set; }

    public DateTime RecordedAt { get; set; } = DateTime.UtcNow;
    public string? RecordedById { get; set; }
    public ApplicationUser? RecordedBy { get; set; }

    public int? SystolicBp { get; set; }
    public int? DiastolicBp { get; set; }
    public int? HeartRate { get; set; }
    public int? RespiratoryRate { get; set; }
    public decimal? TemperatureCelsius { get; set; }
    public int? SpO2 { get; set; }
    public decimal? WeightKg { get; set; }
    public decimal? HeightCm { get; set; }
    public int? PainScore { get; set; }
    public int? GcsTotal { get; set; }

    public string? Notes { get; set; }

    public decimal? Bmi
    {
        get
        {
            if (WeightKg is null || HeightCm is null || HeightCm.Value == 0) return null;
            var m = HeightCm.Value / 100m;
            return Math.Round(WeightKg.Value / (m * m), 1);
        }
    }

    public int? Mews
    {
        get
        {
            int? score = null;
            void Add(int v) { score = (score ?? 0) + v; }

            if (SystolicBp.HasValue)
            {
                var s = SystolicBp.Value;
                if (s <= 70) Add(3);
                else if (s <= 80) Add(2);
                else if (s <= 100) Add(1);
                else if (s >= 200) Add(2);
            }
            if (HeartRate.HasValue)
            {
                var h = HeartRate.Value;
                if (h < 40) Add(2);
                else if (h <= 50) Add(1);
                else if (h <= 100) Add(0);
                else if (h <= 110) Add(1);
                else if (h <= 129) Add(2);
                else Add(3);
            }
            if (RespiratoryRate.HasValue)
            {
                var r = RespiratoryRate.Value;
                if (r < 9) Add(2);
                else if (r <= 14) Add(0);
                else if (r <= 20) Add(1);
                else if (r <= 29) Add(2);
                else Add(3);
            }
            if (TemperatureCelsius.HasValue)
            {
                var t = TemperatureCelsius.Value;
                if (t < 35) Add(2);
                else if (t <= 38.4m) Add(0);
                else Add(2);
            }
            return score;
        }
    }
}
