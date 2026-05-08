using System.ComponentModel.DataAnnotations;
using ThriveHealth.Web.Models.Audit;
using ThriveHealth.Web.Models.Integrations;

namespace ThriveHealth.Web.Models.ViewModels;

public class AuditFilterViewModel
{
    public AuditCategory? Category { get; set; }
    public AuditOutcome? Outcome { get; set; }
    public string? Action { get; set; }
    public string? ActorUserId { get; set; }
    [DataType(DataType.Date)] public DateOnly? Since { get; set; }
    public IReadOnlyList<AuditEntry> Entries { get; set; } = Array.Empty<AuditEntry>();
    public int TotalLast24h { get; set; }
    public int FailuresLast24h { get; set; }
}

public class IntegrationsDashboardViewModel
{
    public string SmsProvider { get; set; } = string.Empty;
    public string EmailProvider { get; set; } = string.Empty;
    public string PaymentProvider { get; set; } = string.Empty;

    public int SmsQueued { get; set; }
    public int SmsSent24h { get; set; }
    public int SmsFailed24h { get; set; }
    public int EmailQueued { get; set; }
    public int EmailSent24h { get; set; }
    public int PaymentInitiated { get; set; }
    public int PaymentSuccessful24h { get; set; }
    public decimal PaymentSuccessfulAmount24h { get; set; }

    public IReadOnlyList<SmsMessage> RecentSms { get; set; } = Array.Empty<SmsMessage>();
    public IReadOnlyList<EmailMessage> RecentEmails { get; set; } = Array.Empty<EmailMessage>();
    public IReadOnlyList<PaymentTransaction> RecentPayments { get; set; } = Array.Empty<PaymentTransaction>();
}

public class SmsTestSendViewModel
{
    [Required, MaxLength(20)] public string ToPhone { get; set; } = string.Empty;
    [Required, MaxLength(640)] public string Body { get; set; } = "ThriveHealth: This is a test message.";
}

public class EmailTestSendViewModel
{
    [Required, EmailAddress, MaxLength(150)] public string ToEmail { get; set; } = string.Empty;
    [Required, MaxLength(200)] public string Subject { get; set; } = "ThriveHealth integration test";
    [Required] public string BodyHtml { get; set; } = "<p>This is a test email from ThriveHealth.</p>";
}

public class HealthCheckViewModel
{
    public bool DbReachable { get; set; }
    public string? DbVersion { get; set; }
    public string AppVersion { get; set; } = "16.0.0";
    public int MigrationsApplied { get; set; }
    public DateTime ServerTimeUtc { get; set; }
    public IReadOnlyDictionary<string, string> SecurityHeaders { get; set; } = new Dictionary<string, string>();
    public string SmsProvider { get; set; } = string.Empty;
    public string EmailProvider { get; set; } = string.Empty;
    public string PaymentProvider { get; set; } = string.Empty;
    public bool RateLimitingEnabled { get; set; } = true;
    public bool HstsEnabled { get; set; } = true;
}
