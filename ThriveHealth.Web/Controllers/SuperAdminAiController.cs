using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ThriveHealth.Web.Models.Audit;
using ThriveHealth.Web.Models.Identity;
using ThriveHealth.Web.Services;
using ThriveHealth.Web.Services.Ai;

namespace ThriveHealth.Web.Controllers;

/// <summary>Platform-wide Claude failover settings, including the API key.</summary>
[Authorize(Roles = Roles.SuperAdmin), Route("superadmin/ai")]
public class SuperAdminAiController : Controller
{
    private const int MaxKeyLength = 300;

    private readonly AiCredentialStore _store;
    private readonly ClaudeAiService _claude;
    private readonly IAuditService _audit;

    public SuperAdminAiController(AiCredentialStore store, ClaudeAiService claude, IAuditService audit)
    {
        _store = store;
        _claude = claude;
        _audit = audit;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
        => View(await _store.GetStatusAsync(ct));

    [HttpPost("save"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(bool enabled, string? model, string? apiKey, CancellationToken ct)
    {
        var key = apiKey?.Trim();
        if (!string.IsNullOrEmpty(key) && (!key.StartsWith("sk-ant-", StringComparison.Ordinal) || key.Length > MaxKeyLength || key.Any(char.IsWhiteSpace)))
        {
            TempData["Error"] = "That doesn't look like a Claude API key. Keys start with sk-ant-.";
            return RedirectToAction(nameof(Index));
        }
        if (model is null || !AiCredentialStore.ClaudeModels.Contains(model))
        {
            TempData["Error"] = "Choose one of the listed Claude models.";
            return RedirectToAction(nameof(Index));
        }

        var status = await _store.GetStatusAsync(ct);
        if (enabled && !status.KeyReadable && string.IsNullOrEmpty(key))
        {
            TempData["Error"] = "Enter an API key before turning Claude on.";
            return RedirectToAction(nameof(Index));
        }

        await _store.SaveClaudeAsync(enabled, model, key, User.Identity?.Name, ct);
        await _audit.LogAsync("ai.claude.settings", AuditCategory.SecurityEvent,
            entityType: "AiProviderCredential", entityKey: "anthropic",
            summary: $"Claude failover {(enabled ? "on" : "off")}, model {model}{(string.IsNullOrEmpty(key) ? "" : ", API key replaced")}",
            ct: ct);

        TempData["Success"] = "Claude settings saved.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("test"), ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(CancellationToken ct)
    {
        if (!_claude.IsConfigured)
        {
            TempData["Error"] = "Save an API key and turn Claude on first.";
            return RedirectToAction(nameof(Index));
        }

        var result = await _claude.CompleteAsync(new AiCompletionRequest(
            "You are checking that an API connection works.", "Reply with the single word OK.",
            ReasoningEffort: "low"), ct);

        if (result.Success) TempData["Success"] = $"Connected to Claude ({result.Model}) in {result.LatencyMs / 1000.0:F1}s.";
        else TempData["Error"] = $"Claude connection failed: {result.ErrorMessage}";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("remove-key"), ValidateAntiForgeryToken]
    public async Task<IActionResult> RemoveKey(CancellationToken ct)
    {
        await _store.RemoveClaudeKeyAsync(User.Identity?.Name, ct);
        await _audit.LogAsync("ai.claude.key_removed", AuditCategory.SecurityEvent,
            entityType: "AiProviderCredential", entityKey: "anthropic", summary: "Claude API key removed", ct: ct);
        TempData["Success"] = "Claude API key removed. Claude failover is off.";
        return RedirectToAction(nameof(Index));
    }
}
