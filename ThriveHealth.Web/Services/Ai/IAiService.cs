namespace ThriveHealth.Web.Services.Ai;

public sealed record AiCompletionRequest(
    string SystemPrompt,
    string UserPrompt,
    int MaxOutputTokens = 1024,
    double Temperature = 0.2);

public sealed record AiCompletionResult(
    bool Success,
    string? Text,
    int InputTokens,
    int OutputTokens,
    int LatencyMs,
    string Model,
    string Provider,
    string? ErrorMessage);

public interface IAiService
{
    /// <summary>True when a provider is configured and the global kill-switch is on.</summary>
    bool IsConfigured { get; }

    string Provider { get; }
    string Model { get; }

    Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default);
}
