using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ThriveHealth.Web.Services.Ai;

/// <summary>
/// OpenRouter provider — single OpenAI-compatible gateway to dozens of models including
/// free tiers (DeepSeek, Llama 3.3 70B, Qwen, Gemini Flash) and paid (Claude, GPT-4).
/// Switch model with the <see cref="AiOptions.Model"/> string; no other code changes needed.
/// </summary>
public sealed class OpenRouterAiService : IAiService
{
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
        ? "meta-llama/llama-3.3-70b-instruct:free"
        : _options.Model;

    public async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured)
            return new AiCompletionResult(false, null, 0, 0, 0, Model, Provider, "AI provider not configured.");

        var sw = Stopwatch.StartNew();
        try
        {
            var payload = new ChatRequest(
                Model,
                new[]
                {
                    new ChatMessage("system", request.SystemPrompt),
                    new ChatMessage("user", request.UserPrompt)
                },
                request.MaxOutputTokens,
                request.Temperature);

            var resp = await _http.PostAsJsonAsync("/api/v1/chat/completions", payload, ct);
            sw.Stop();

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("OpenRouter API error {Status}: {Body}", resp.StatusCode, body);
                return new AiCompletionResult(false, null, 0, 0, (int)sw.ElapsedMilliseconds, Model, Provider, $"API {(int)resp.StatusCode}");
            }

            var parsed = await resp.Content.ReadFromJsonAsync<ChatResponse>(cancellationToken: ct);
            var text = parsed?.Choices?.FirstOrDefault()?.Message?.Content ?? string.Empty;
            var inputTokens = parsed?.Usage?.PromptTokens ?? 0;
            var outputTokens = parsed?.Usage?.CompletionTokens ?? 0;

            if (string.IsNullOrEmpty(text))
            {
                var finish = parsed?.Choices?.FirstOrDefault()?.FinishReason ?? "no_text";
                return new AiCompletionResult(false, null, inputTokens, outputTokens, (int)sw.ElapsedMilliseconds, Model, Provider, $"Empty response ({finish})");
            }

            return new AiCompletionResult(true, text, inputTokens, outputTokens, (int)sw.ElapsedMilliseconds, Model, Provider, null);
        }
        catch (TaskCanceledException)
        {
            return new AiCompletionResult(false, null, 0, 0, (int)sw.ElapsedMilliseconds, Model, Provider, "Request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "OpenRouter call failed");
            return new AiCompletionResult(false, null, 0, 0, (int)sw.ElapsedMilliseconds, Model, Provider, ex.Message);
        }
    }

    private sealed record ChatRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("messages")] ChatMessage[] Messages,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("temperature")] double Temperature);

    private sealed record ChatMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed class ChatResponse
    {
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
