using System.Diagnostics;
using Anthropic;
using Anthropic.Exceptions;
using Anthropic.Models.Beta.Messages;

namespace ThriveHealth.Web.Services.Ai;

/// <summary>Calls Claude with the key saved in the super-admin console.</summary>
public sealed class ClaudeAiService
{
    private const string ProviderName = "anthropic";

    // Thinking counts towards max_tokens, so small limits written for other models would cut replies off.
    private const int MinimumOutputTokens = 2000;
    private static readonly TimeSpan RequestTimeout = TimeSpan.FromSeconds(20);

    private readonly AiCredentialStore _store;
    private readonly ILogger<ClaudeAiService> _logger;
    private volatile ClientEntry? _client;

    private sealed record ClientEntry(string ApiKey, AnthropicClient Client);

    public ClaudeAiService(AiCredentialStore store, ILogger<ClaudeAiService> logger)
    {
        _store = store;
        _logger = logger;
    }

    public bool IsConfigured => _store.GetClaude() is not null;

    public async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default)
    {
        var credential = _store.GetClaude();
        if (credential is null)
            return new AiCompletionResult(false, null, 0, 0, 0, AiCredentialStore.DefaultClaudeModel, ProviderName, "Claude is not configured.");

        var sw = Stopwatch.StartNew();
        AiCompletionResult Fail(string error, int input = 0, int output = 0) =>
            new(false, null, input, output, (int)sw.ElapsedMilliseconds, credential.Model, ProviderName, error);

        try
        {
            var response = await ClientFor(credential.ApiKey).Beta.Messages.Create(new MessageCreateParams
            {
                Model = credential.Model,
                MaxTokens = Math.Max(request.MaxOutputTokens, MinimumOutputTokens),
                System = request.SystemPrompt,
                Messages = [new BetaUserMessageParam { Content = request.UserPrompt }],
                OutputConfig = string.Equals(request.ReasoningEffort, "low", StringComparison.OrdinalIgnoreCase)
                    ? new BetaOutputConfig { Effort = Effort.Low }
                    : null,
                // Safety classifiers can decline medical text; this retries on a substitute model instead.
                Betas = ["server-side-fallback-2026-07-01"],
                Fallbacks = new Default(),
            }, ct);

            var inputTokens = (int)response.Usage.InputTokens;
            var outputTokens = (int)response.Usage.OutputTokens;

            if (response.StopReason == "refusal") return Fail("Claude declined the request.", inputTokens, outputTokens);
            if (response.StopReason == "max_tokens") return Fail("Claude's reply was cut off.", inputTokens, outputTokens);

            var text = string.Concat(response.Content.Select(b => b.Value).OfType<BetaTextBlock>().Select(b => b.Text));
            if (string.IsNullOrWhiteSpace(text)) return Fail("Empty response.", inputTokens, outputTokens);

            return new AiCompletionResult(true, text, inputTokens, outputTokens, (int)sw.ElapsedMilliseconds, response.Model, ProviderName, null);
        }
        catch (OperationCanceledException)
        {
            return Fail("Request timed out.");
        }
        catch (AnthropicUnauthorizedException)
        {
            _logger.LogError("Claude rejected the saved API key");
            return Fail("Claude API key was rejected.");
        }
        catch (AnthropicRateLimitException)
        {
            _logger.LogWarning("Claude rate limit reached");
            return Fail("Claude rate limit reached.");
        }
        catch (AnthropicApiException ex)
        {
            _logger.LogWarning(ex, "Claude API error");
            return Fail("Claude API error.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Claude call failed");
            return Fail("Could not reach Claude.");
        }
    }

    private AnthropicClient ClientFor(string apiKey)
    {
        var entry = _client;
        if (entry is not null && entry.ApiKey == apiKey) return entry.Client;

        var client = new AnthropicClient { ApiKey = apiKey, Timeout = RequestTimeout, MaxRetries = 0 };
        _client = new ClientEntry(apiKey, client);
        return client;
    }
}
