using System.Collections.Concurrent;
using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ThriveHealth.Web.Services.Ai;

/// <summary>
/// OpenRouter provider — single OpenAI-compatible gateway to free and paid models.
/// Tries <see cref="AiOptions.Model"/>, then each of <see cref="AiOptions.FallbackModels"/> in order;
/// change either in configuration, no other code changes needed.
/// </summary>
public sealed class OpenRouterAiService : IAiService
{
    // Shared across requests (this service is transient) so one patient's failed attempt
    // saves the next patient from waiting on the same busy model.
    private static readonly ConcurrentDictionary<string, DateTime> CoolingUntil = new(StringComparer.OrdinalIgnoreCase);
    private static readonly TimeSpan Cooldown = TimeSpan.FromSeconds(90);

    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly ILogger<OpenRouterAiService> _logger;

    public OpenRouterAiService(HttpClient http, IOptions<AiOptions> options, ILogger<OpenRouterAiService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrEmpty(_options.OpenRouterBaseUrl))
            _http.BaseAddress = new Uri(_options.OpenRouterBaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds));

        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _http.DefaultRequestHeaders.Authorization =
                new System.Net.Http.Headers.AuthenticationHeaderValue("Bearer", _options.ApiKey);
        }
        // OpenRouter recommends these for attribution / leaderboards. Optional.
        if (!string.IsNullOrEmpty(_options.OpenRouterReferer))
        {
            _http.DefaultRequestHeaders.Remove("HTTP-Referer");
            _http.DefaultRequestHeaders.Add("HTTP-Referer", _options.OpenRouterReferer);
        }
        if (!string.IsNullOrEmpty(_options.OpenRouterAppName))
        {
            _http.DefaultRequestHeaders.Remove("X-Title");
            _http.DefaultRequestHeaders.Add("X-Title", _options.OpenRouterAppName);
        }
    }

    public bool IsConfigured => _options.Enabled
        && string.Equals(_options.Provider, "openrouter", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(_options.ApiKey);

    public string Provider => "openrouter";
    public string Model => string.IsNullOrWhiteSpace(_options.Model)
        ? "openrouter/free"
        : _options.Model;

    public async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new AiCompletionResult(false, null, 0, 0, 0, Model, Provider, "AI provider not configured.");

        var sw = Stopwatch.StartNew();
        using var budget = CancellationTokenSource.CreateLinkedTokenSource(ct);
        budget.CancelAfter(TimeSpan.FromSeconds(Math.Max(5, _options.TotalTimeoutSeconds)));

        var models = new[] { Model }
            .Concat(_options.FallbackModels.Where(m => !string.IsNullOrWhiteSpace(m)))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToList();

        // Recently failed models go last rather than being dropped, in case they have recovered.
        var now = DateTime.UtcNow;
        models = models.OrderBy(m => CoolingUntil.TryGetValue(m, out var until) && until > now).ToList();

        AiCompletionResult? last = null;
        foreach (var model in models)
        {
            var (result, tryNext, cool) = await TryModelAsync(model, request, sw, budget.Token);
            if (result.Success)
            {
                CoolingUntil.TryRemove(model, out _);
                return result;
            }

            last = result;
            if (!tryNext) break;
            if (cool) CoolingUntil[model] = DateTime.UtcNow + Cooldown;
            _logger.LogInformation("OpenRouter model {Model} failed ({Error}); trying next model", model, result.ErrorMessage);
        }

        return last!;
    }

    // Cool is true only when the model itself is the problem, not this particular request.
    private async Task<(AiCompletionResult Result, bool TryNext, bool Cool)> TryModelAsync(
        string model, AiCompletionRequest request, Stopwatch sw, CancellationToken ct)
    {
        AiCompletionResult Fail(string error, int input = 0, int output = 0) =>
            new(false, null, input, output, (int)sw.ElapsedMilliseconds, model, Provider, error);

        try
        {
            var payload = new ChatRequest(
                model,
                new[]
                {
                    new ChatMessage("system", request.SystemPrompt),
                    new ChatMessage("user", request.UserPrompt)
                },
                request.MaxOutputTokens,
                request.Temperature,
                request.ReasoningEffort is null ? null : new ReasoningOptions(request.ReasoningEffort, true));

            using var resp = await _http.PostAsJsonAsync("/api/v1/chat/completions", payload, ct);

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("OpenRouter API error {Status} for {Model}: {Body}", resp.StatusCode, model, body);
                var busy = resp.StatusCode == System.Net.HttpStatusCode.TooManyRequests || (int)resp.StatusCode >= 500;
                return (Fail($"API {(int)resp.StatusCode}"), !IsAccountWideFailure(resp.StatusCode, body), busy);
            }

            var parsed = await resp.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct);
            var text = parsed?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
            var inputTokens = parsed?.Usage?.PromptTokens ?? 0;
            var outputTokens = parsed?.Usage?.CompletionTokens ?? 0;

            if (string.IsNullOrEmpty(text))
            {
                var finish = parsed?.Choices?.FirstOrDefault()?.FinishReason ?? "no_text";
                return (Fail($"Empty response ({finish})", inputTokens, outputTokens), true, false);
            }

            // Routers such as openrouter/free report the model that really answered; audit that one.
            var answeredBy = string.IsNullOrWhiteSpace(parsed?.Model) ? model : parsed!.Model!;
            return (new AiCompletionResult(true, text, inputTokens, outputTokens, (int)sw.ElapsedMilliseconds, answeredBy, Provider, null), false, false);
        }
        catch (OperationCanceledException) when (ct.IsCancellationRequested)
        {
            return (Fail("Request timed out."), false, false);
        }
        catch (OperationCanceledException)
        {
            // HttpClient's per-request timeout: only this model was slow.
            return (Fail("Request timed out."), true, true);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenRouter call failed for {Model}", model);
            return (Fail(ex.Message), true, true);
        }
    }

    /// <summary>Failures no other model can avoid: a bad key, no credits, or the account's free-tier quota.</summary>
    private static bool IsAccountWideFailure(System.Net.HttpStatusCode status, string body) =>
        status is System.Net.HttpStatusCode.Unauthorized or System.Net.HttpStatusCode.PaymentRequired
        || (status == System.Net.HttpStatusCode.TooManyRequests
            && body.Contains("free-models-per", StringComparison.OrdinalIgnoreCase));

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] ChatMessage[] Messages,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("reasoning"), JsonIgnore(Condition = JsonIgnoreCondition.WhenWritingNull)] ReasoningOptions? Reasoning);

    private sealed record ReasoningOptions(
        [property: JsonPropertyName("effort")] string Effort,
        [property: JsonPropertyName("exclude")] bool Exclude);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed class ChatResponse
    {
        [JsonPropertyName("model")] public string? Model { get; set; }
        [JsonPropertyName("choices")] public List<ChatChoice>? Choices { get; set; }
        [JsonPropertyName("usage")] public ChatUsage? Usage { get; set; }
    }

    private sealed class ChatChoice
    {
        [JsonPropertyName("message")] public ChatMessageResp? Message { get; set; }
        [JsonPropertyName("finish_reason")] public string? FinishReason { get; set; }
    }

    private sealed class ChatMessageResp
    {
        [JsonPropertyName("role")] public string? Role { get; set; }
        [JsonPropertyName("content")] public string? Content { get; set; }
    }

    private sealed class ChatUsage
    {
        [JsonPropertyName("prompt_tokens")] public int PromptTokens { get; set; }
        [JsonPropertyName("completion_tokens")] public int CompletionTokens { get; set; }
        [JsonPropertyName("total_tokens")] public int TotalTokens { get; set; }
    }
}
