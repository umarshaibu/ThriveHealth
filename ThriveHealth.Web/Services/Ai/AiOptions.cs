namespace ThriveHealth.Web.Services.Ai;

public sealed class AiOptions
{
    public const string SectionName = "Ai";

    /// <summary>Master kill-switch. When false, IAiService.IsConfigured is false regardless of provider.</summary>
    public bool Enabled { get; set; } = false;

    /// <summary>"openrouter" | "anthropic" | "stub". Default stub when no key.</summary>
    public string Provider { get; set; } = "stub";

    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Provider-specific model. Examples:
    /// openrouter="google/gemma-4-31b-it:free"; anthropic="claude-sonnet-4-6".</summary>
    public string Model { get; set; } = "openrouter/free";

    /// <summary>OpenRouter only: models tried in order after <see cref="Model"/> fails. Free models
    /// are retired and rate-limited often, so keep this list current. Deliberately empty here:
    /// the configuration binder appends to an initialised list rather than replacing it.</summary>
    public List<string> FallbackModels { get; set; } = new();

    /// <summary>Anthropic base URL (only used when Provider=anthropic).</summary>
    public string ApiBaseUrl { get; set; } = "https://api.anthropic.com";
    public string AnthropicVersion { get; set; } = "2023-06-01";

    /// <summary>OpenRouter base URL.</summary>
    public string OpenRouterBaseUrl { get; set; } = "https://openrouter.ai";

    /// <summary>Optional: site URL OpenRouter shows in its dashboard / leaderboards.</summary>
    public string OpenRouterReferer { get; set; } = "https://thrivehealth.local";

    /// <summary>Optional: app name OpenRouter shows in its dashboard.</summary>
    public string OpenRouterAppName { get; set; } = "ThriveHealth";

    /// <summary>Per-request timeout in seconds.</summary>
    public int TimeoutSeconds { get; set; } = 30;

    /// <summary>OpenRouter only: time budget in seconds for one completion across all fallback models.</summary>
    public int TotalTimeoutSeconds { get; set; } = 60;

    /// <summary>When Claude failover is on: seconds the free models get before falling back to it.</summary>
    public int FailoverAfterSeconds { get; set; } = 20;
}
