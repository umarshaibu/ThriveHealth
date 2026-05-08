using ThriveHealth.Web.Models.Inpatient;

namespace ThriveHealth.Web.Services;

public interface IMarSlotGenerator
{
    IEnumerable<DateTime> GenerateSlots(InpatientMedicationKind kind, string? frequency, DateTime startUtc, DateTime endUtc);
}

public class MarSlotGenerator : IMarSlotGenerator
{
    public IEnumerable<DateTime> GenerateSlots(InpatientMedicationKind kind, string? frequency, DateTime startUtc, DateTime endUtc)
    {
        if (kind == InpatientMedicationKind.Stat || kind == InpatientMedicationKind.Once)
        {
            yield return startUtc;
            yield break;
        }
        if (kind == InpatientMedicationKind.Prn) yield break;

        var times = ParseFrequency(frequency);
        if (times.Count == 0) yield break;

        var date = DateTime.SpecifyKind(startUtc.Date, DateTimeKind.Utc);
        var lastDate = DateTime.SpecifyKind(endUtc.Date, DateTimeKind.Utc);
        while (date <= lastDate)
        {
            foreach (var t in times)
            {
                var slot = date.AddHours(t.Hour).AddMinutes(t.Minute);
                if (slot >= startUtc && slot <= endUtc) yield return slot;
            }
            date = date.AddDays(1);
        }
    }

    private static List<TimeOnly> ParseFrequency(string? freq)
    {
        if (string.IsNullOrWhiteSpace(freq)) return new();
        var f = freq.Trim().ToUpperInvariant();

        if (f.Contains("OD") || f.Contains("ONCE DAILY") || f.StartsWith("DAILY"))
            return new() { new(8, 0) };
        if (f.Contains("BD") || f.Contains("BID") || f.Contains("TWICE"))
            return new() { new(8, 0), new(20, 0) };
        if (f.Contains("TDS") || f.Contains("TID") || f.Contains("THREE"))
            return new() { new(8, 0), new(14, 0), new(20, 0) };
        if (f.Contains("QDS") || f.Contains("QID") || f.Contains("FOUR"))
            return new() { new(6, 0), new(12, 0), new(18, 0), new(0, 0) };
        if (f.Contains("Q4H") || f.Contains("4 HOURLY"))
            return Enumerable.Range(0, 6).Select(i => new TimeOnly(i * 4, 0)).ToList();
        if (f.Contains("Q6H") || f.Contains("6 HOURLY"))
            return new() { new(0, 0), new(6, 0), new(12, 0), new(18, 0) };
        if (f.Contains("Q8H") || f.Contains("8 HOURLY"))
            return new() { new(8, 0), new(16, 0), new(0, 0) };
        if (f.Contains("Q12H") || f.Contains("12 HOURLY"))
            return new() { new(8, 0), new(20, 0) };

        return new() { new(8, 0) };
    }
}
