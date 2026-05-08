namespace ThriveHealth.Web.Models.ViewModels;

public class DashboardStat
{
    public string Label { get; set; } = string.Empty;
    public string Value { get; set; } = string.Empty;
    public string? Icon { get; set; }
    public string? Trend { get; set; }
    public string Tone { get; set; } = "primary";
}

public class DashboardAction
{
    public string Label { get; set; } = string.Empty;
    public string Icon { get; set; } = "bi-arrow-right-short";
    public string Controller { get; set; } = "Dashboard";
    public string Action { get; set; } = "Index";
    public object? RouteValues { get; set; }
    /// <summary>If set, this action is hidden when the current user lacks the permission.</summary>
    public string? Permission { get; set; }
}

public class DashboardViewModel
{
    public string FacilityName { get; set; } = string.Empty;
    public string GreetingName { get; set; } = string.Empty;
    public string PrimaryRole { get; set; } = string.Empty;
    public string Persona { get; set; } = "Default";
    public List<DashboardStat> Stats { get; set; } = new();
    public List<string> ActiveAlerts { get; set; } = new();
    public List<DashboardAction> NextActions { get; set; } = new();
    public List<DashboardActivityItem> RecentActivity { get; set; } = new();
    public int PatientCount { get; set; }
}

public class DashboardActivityItem
{
    public DateTime At { get; set; }
    public string Icon { get; set; } = "bi-circle";
    public string Tone { get; set; } = "primary";
    public string Title { get; set; } = string.Empty;
    public string? Subtitle { get; set; }
    public string? LinkController { get; set; }
    public string? LinkAction { get; set; }
    public int? LinkId { get; set; }
}
