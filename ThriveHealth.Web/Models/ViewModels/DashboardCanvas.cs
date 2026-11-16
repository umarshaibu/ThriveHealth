using System.Globalization;
using System.Text;

namespace ThriveHealth.Web.Models.ViewModels;

/// <summary>A link on the dashboard; the view hides it when the user lacks <see cref="Permission"/>.</summary>
public record CanvasLink(string Href, string? Permission = null);

public record CanvasMetric(string Label, string Value);

public record HeroNextCard(string Name, string Detail, string Headline, string Foot);

public class HeroPanel
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-heart-pulse";
    /// <summary>True draws the anatomical illustration; false draws the role's icon.</summary>
    public bool ClinicalArt { get; set; }
    public CanvasLink? Link { get; set; }
    public string NextLabel { get; set; } = "Next up";
    public HeroNextCard? Next { get; set; }
    public string NextEmpty { get; set; } = "Nothing waiting";
    public string MetricsTitle { get; set; } = "Right now";
    public List<CanvasMetric> Metrics { get; set; } = new();
}

public class TrendPanel
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string SeriesA { get; set; } = string.Empty;
    public string SeriesB { get; set; } = string.Empty;
    public double[] A { get; set; } = Array.Empty<double>();
    public double[] B { get; set; } = Array.Empty<double>();
    public string[] Labels { get; set; } = Array.Empty<string>();
    public string TotalA { get; set; } = "0";
    public string TotalB { get; set; } = "0";
    public bool HasData => A.Concat(B).Any(v => v > 0);
}

public class GaugePanel
{
    public string Title { get; set; } = string.Empty;
    public string Subtitle { get; set; } = string.Empty;
    public string Value { get; set; } = "0";
    public string Caption { get; set; } = string.Empty;
    public string LabelA { get; set; } = string.Empty;
    public string LabelB { get; set; } = string.Empty;
    public int A { get; set; }
    public int B { get; set; }
    public double ShareA => A + B == 0 ? 0 : 100.0 * A / (A + B);
    public double ShareB => A + B == 0 ? 0 : 100.0 * B / (A + B);
}

public class SpotlightPanel
{
    public string Title { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-person";
    public string? Name { get; set; }
    public string? Detail { get; set; }
    public string? Badge { get; set; }
    /// <summary>success, warning, danger or info. Status colours are always paired with the badge text.</summary>
    public string BadgeTone { get; set; } = "info";
    public string Empty { get; set; } = "Nothing to show yet";
    public List<string> Chips { get; set; } = new();
    public CanvasLink? Link { get; set; }

    public string Initials => string.Concat((Name ?? string.Empty)
        .Split(' ', StringSplitOptions.RemoveEmptyEntries)
        .Where(w => char.IsLetterOrDigit(w[0]))
        .Take(2)
        .Select(w => char.ToUpperInvariant(w[0])));
}

public class DashboardCanvasVm
{
    public HeroPanel Hero { get; set; } = new();
    public TrendPanel Trend { get; set; } = new();
    public GaugePanel Gauge { get; set; } = new();
    public SpotlightPanel Spotlight { get; set; } = new();
}

/// <summary>Geometry for the server-rendered dashboard chart.</summary>
public static class CanvasSvg
{
    public const int Width = 640;
    public const int Height = 200;
    private const double Inset = 12;

    public static (double X, double Y) Point(double[] values, int index, double max)
    {
        var x = values.Length <= 1 ? 0 : (double)index / (values.Length - 1) * Width;
        var scale = max <= 0 ? 0 : values[index] / max;
        return (x, Height - Inset - scale * (Height - 2 * Inset));
    }

    /// <summary>
    /// Smooth line through every value. Control points stay within each segment's own height,
    /// so the curve never dips below zero or peaks above the real values.
    /// </summary>
    public static string Path(double[] values, double max)
    {
        if (values.Length == 0) return string.Empty;
        string N(double v) => v.ToString("0.#", CultureInfo.InvariantCulture);

        var sb = new StringBuilder();
        var first = Point(values, 0, max);
        sb.Append($"M{N(first.X)} {N(first.Y)}");
        for (var i = 0; i < values.Length - 1; i++)
        {
            var p0 = Point(values, Math.Max(i - 1, 0), max);
            var p1 = Point(values, i, max);
            var p2 = Point(values, i + 1, max);
            var p3 = Point(values, Math.Min(i + 2, values.Length - 1), max);
            double Clamp(double y) => Math.Clamp(y, Math.Min(p1.Y, p2.Y), Math.Max(p1.Y, p2.Y));
            sb.Append($" C{N(p1.X + (p2.X - p0.X) / 6)} {N(Clamp(p1.Y + (p2.Y - p0.Y) / 6))}");
            sb.Append($" {N(p2.X - (p3.X - p1.X) / 6)} {N(Clamp(p2.Y - (p3.Y - p1.Y) / 6))}");
            sb.Append($" {N(p2.X)} {N(p2.Y)}");
        }
        return sb.ToString();
    }

    /// <summary>Seven evenly spaced label positions, always including the first and the latest.</summary>
    public static int[] TickIndexes(int count)
    {
        if (count <= 7) return Enumerable.Range(0, count).ToArray();
        return Enumerable.Range(0, 7).Select(i => (int)Math.Round(i * (count - 1) / 6.0)).ToArray();
    }
}
