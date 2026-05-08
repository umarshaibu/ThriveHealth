using System.Diagnostics;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace ThriveHealth.Web.Services.Ai;

public sealed class AnthropicAiService : IAiService
{
    private readonly HttpClient _http;
    private readonly AiOptions _options;
    private readonly ILogger<AnthropicAiService> _logger;

    public AnthropicAiService(HttpClient http, IOptions<AiOptions> options, ILogger<AnthropicAiService> logger)
    {
        _http = http;
        _options = options.Value;
        _logger = logger;

        if (!string.IsNullOrEmpty(_options.ApiBaseUrl))
            _http.BaseAddress = new Uri(_options.ApiBaseUrl);
        _http.Timeout = TimeSpan.FromSeconds(Math.Max(5, _options.TimeoutSeconds));

        if (!string.IsNullOrEmpty(_options.ApiKey))
        {
            _http.DefaultRequestHeaders.Remove("x-api-key");
            _http.DefaultRequestHeaders.Add("x-api-key", _options.ApiKey);
            _http.DefaultRequestHeaders.Remove("anthropic-version");
            _http.DefaultRequestHeaders.Add("anthropic-version", _options.AnthropicVersion);
        }
    }

    public bool IsConfigured => _options.Enabled
        && string.Equals(_options.Provider, "anthropic", StringComparison.OrdinalIgnoreCase)
        && !string.IsNullOrWhiteSpace(_options.ApiKey);

    public string Provider => "anthropic";
    public string Model => _options.Model;

    public async Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return new AiCompletionResult(false, null, 0, 0, 0, _options.Model, Provider, "AI provider not configured.");
        }

        var sw = Stopwatch.StartNew();
        try
        {
            var payload = new AnthropicMessagesRequest(
                _options.Model,
                request.MaxOutputTokens,
                request.Temperature,
                request.SystemPrompt,
                new[] { new AnthropicMessage("user", request.UserPrompt) }
            );

            var resp = await _http.PostAsJsonAsync("/v1/messages", payload, ct);
            sw.Stop();

            if (!resp.IsSuccessStatusCode)
            {
                var body = await resp.Content.ReadAsStringAsync(ct);
                _logger.LogWarning("Anthropic API error {Status}: {Body}", resp.StatusCode, body);
                return new AiCompletionResult(false, null, 0, 0, (int)sw.ElapsedMilliseconds, _options.Model, Provider, $"API {(int)resp.StatusCode}");
            }

            var parsed = await resp.Content.ReadFromJsonAsync<AnthropicMessagesResponse>(cancellationToken: ct);
            var text = parsed?.Content?.FirstOrDefault(c => c.Type == "text")?.Text ?? string.Empty;
            var inputTokens = parsed?.Usage?.InputTokens ?? 0;
            var outputTokens = parsed?.Usage?.OutputTokens ?? 0;

            return new AiCompletionResult(true, text, inputTokens, outputTokens, (int)sw.ElapsedMilliseconds, _options.Model, Provider, null);
        }
        catch (TaskCanceledException)
        {
            return new AiCompletionResult(false, null, 0, 0, (int)sw.ElapsedMilliseconds, _options.Model, Provider, "Request timed out.");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Anthropic call failed");
            return new AiCompletionResult(false, null, 0, 0, (int)sw.ElapsedMilliseconds, _options.Model, Provider, ex.Message);
        }
    }

    private sealed record AnthropicMessagesRequest(
        [property: JsonPropertyName("model")] string Model,
        [property: JsonPropertyName("max_tokens")] int MaxTokens,
        [property: JsonPropertyName("temperature")] double Temperature,
        [property: JsonPropertyName("system")] string System,
        [property: JsonPropertyName("messages")] AnthropicMessage[] Messages);

    private sealed record AnthropicMessage(
        [property: JsonPropertyName("role")] string Role,
        [property: JsonPropertyName("content")] string Content);

    private sealed class AnthropicMessagesResponse
    {
        [JsonPropertyName("content")] public List<AnthropicContentBlock>? Content { get; set; }
        [JsonPropertyName("usage")] public AnthropicUsage? Usage { get; set; }
    }

    private sealed class AnthropicContentBlock
    {
        [JsonPropertyName("type")] public string Type { get; set; } = string.Empty;
        [JsonPropertyName("text")] public string? Text { get; set; }
    }

    private sealed class AnthropicUsage
    {
        [JsonPropertyName("input_tokens")] public int InputTokens { get; set; }
        [JsonPropertyName("output_tokens")] public int OutputTokens { get; set; }
    }
}

public sealed class StubAiService : IAiService
{
    public bool IsConfigured => true;
    public string Provider => "stub";
    public string Model => "stub";

    public Task<AiCompletionResult> CompleteAsync(AiCompletionRequest request, CancellationToken ct = default)
    {
        // Demo-mode stub. Produces a conservative, format-correct response so AI-powered features
        // (symptom checker, lab interpretation, etc.) work end-to-end without real provider keys.
        // Configure Ai:Provider = openrouter | anthropic + Ai:ApiKey to use a real LLM.
        var sys = (request.SystemPrompt ?? "").ToLowerInvariant();
        string text = sys switch
        {
            var s when s.Contains("symptom-triage") || s.Contains("symptom checker") =>
                "ADVICE: Come in today\n" +
                "WHY: Without examining you we can't safely rule out anything serious — better to be seen.\n" +
                "WHAT TO WATCH FOR:\n" +
                " • Symptoms getting worse instead of better\n" +
                " • Difficulty breathing, severe chest pain, fainting\n" +
                " • Fever above 39°C that paracetamol doesn't bring down\n\n" +
                "This is general guidance. Talk to a clinician for diagnosis or treatment.\n\n" +
                "_Demo response — configure Ai:Provider + Ai:ApiKey for live AI._",
            _ =>
                "_Demo response — AI provider is set to 'stub'. Configure Ai:Provider (openrouter | anthropic) " +
                "and Ai:ApiKey in appsettings to enable live AI responses._"
        };
        var prompt = (request.SystemPrompt?.Length ?? 0) + (request.UserPrompt?.Length ?? 0);
        return Task.FromResult(new AiCompletionResult(
            Success: true, Text: text,
            InputTokens: prompt / 4, OutputTokens: text.Length / 4,
            LatencyMs: 5,
            Model: "stub", Provider: "stub",
            ErrorMessage: null));
    }
}
