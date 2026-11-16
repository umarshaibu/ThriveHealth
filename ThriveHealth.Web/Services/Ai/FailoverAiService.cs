using Microsoft.Extensions.Options;

namespace ThriveHealth.Web.Services.Ai;

/// <summary>
/// Uses the free OpenRouter models first and falls back to Claude when they fail,
/// including when OpenRouter's daily quota has run out.
/// </summary>
public sealed class FailoverAiService : IAiService
{
    private readonly OpenRouterAiService _primary;
    private readonly ClaudeAiService _claude;
    private readonly AiOptions _options;
    private readonly ILogger<FailoverAiService> _logger;

    public FailoverAiService(OpenRouterAiService primary, ClaudeAiService claude, IOptions<AiOptions> options, ILogger<FailoverAiService> logger)
    {
        _primary = primary;
        _claude = claude;
        _options = options.Value;
        _logger = logger;
    }

    public bool IsConfigured => _primary.IsConfigured || _claude.IsConfigured;
    public string Provider => _primary.Provider;
    public string Model => _primary.Model;

    public async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default)
    {
        if (!_claude.IsConfigured) return await _primary.CompleteAsync(request, ct);
        if (!_primary.IsConfigured) return await _claude.CompleteAsync(request, ct);

        // With Claude behind it, the free models get less time so the patient isn't left waiting.
        using var limit = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limit.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _options.FailoverAfterSeconds)));

        var free = await _primary.CompleteAsync(request, limit.Token);
        if (free.Success || ct.IsCancellationRequested) return free;

        _logger.LogInformation("Free AI models failed ({Error}); falling back to Claude", free.ErrorMessage);
        var claude = await _claude.CompleteAsync(request, ct);
        return claude with { LatencyMs = free.LatencyMs + claude.LatencyMs };
    }
}
