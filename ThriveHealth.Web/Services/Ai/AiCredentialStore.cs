using System.Security.Cryptography;
using Microsoft.AspNetCore.DataProtection;
using Microsoft.EntityFrameworkCore;
using ThriveHealth.Web.Data;
using ThriveHealth.Web.Models.Ai;

namespace ThriveHealth.Web.Services.Ai;

public sealed record ClaudeCredential(string ApiKey, string Model);

public sealed record ClaudeSettingsStatus(
    bool Enabled,
    bool HasKey,
    bool KeyReadable,
    string? KeyHint,
    string Model,
    DateTime? UpdatedAtUtc,
    string? UpdatedBy);

/// <summary>
/// Stores the Claude API key encrypted in the database. A singleton with a short cache, because
/// the key is needed on every AI request and saves on another server should still arrive soon.
/// </summary>
public sealed class AiCredentialStore
{
    public const string DefaultClaudeModel = "claude-opus-5-5";
    public static readonly IReadOnlyList<string> ClaudeModels = new[]
    {
        "claude-opus-5-5",
        "claude-sonnet-5-5",
        "claude-haiku-5-5",
    };

    private const string ClaudeProvider = "anthropic";
    private static readonly TimeSpan CacheFor = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopes;
    private readonly IDataProtector _protector;
    private readonly ILogger<AiCredentialStore> _logger;
    private volatile CacheEntry? _cache;
    private int _version;

    private sealed record CacheEntry(ClaudeCredential? Credential, DateTime ExpiresUtc);

    public AiCredentialStore(IServiceScopeFactory scopes, IDataProtectionProvider protection, ILogger<AiCredentialStore> logger)
    {
        _scopes = scopes;
        _protector = protection.CreateProtector("ThriveHealth.Ai.ProviderKey.v1");
        _logger = logger;
    }

    /// <summary>The usable credential, or null when Claude is off, has no key, or the key can't be read.</summary>
    public ClaudeCredential? GetClaude()
    {
        var cached = _cache;
        if (cached is not null && cached.ExpiresUtc > DateTime.UtcNow) return cached.Credential;

        var version = Volatile.Read(ref _version);
        ClaudeCredential? credential = null;
        try
        {
            using var scope = _scopes.CreateScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var row = db.AiProviderCredentials.AsNoTracking().FirstOrDefault(c => c.Provider == ClaudeProvider);
            if (row is { Enabled: true, EncryptedApiKey: not null } && TryDecrypt(row.EncryptedApiKey, out var key))
                credential = new ClaudeCredential(key, row.Model);
        }
        catch (Exception ex)
        {
            // Not cached, so a brief database problem doesn't turn Claude off for the whole cache period.
            _logger.LogError(ex, "Could not load the Claude settings");
            return null;
        }

        // Skip caching if a save happened during the read, so a removed key can't be cached again.
        if (version == Volatile.Read(ref _version))
            _cache = new CacheEntry(credential, DateTime.UtcNow + CacheFor);
        return credential;
    }

    public async Task<ClaudeSettingsStatus> GetStatusAsync(CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.AiProviderCredentials.AsNoTracking().FirstOrDefaultAsync(c => c.Provider == ClaudeProvider, ct);
        if (row is null) return new ClaudeSettingsStatus(false, false, false, null, DefaultClaudeModel, null, null);

        var hasKey = row.EncryptedApiKey is not null;
        var readable = hasKey && TryDecrypt(row.EncryptedApiKey!, out _);
        return new ClaudeSettingsStatus(row.Enabled, hasKey, readable, row.KeyHint, row.Model, row.UpdatedAtUtc, row.UpdatedBy);
    }

    /// <summary>Saves the settings. A blank <paramref name="newApiKey"/> keeps the current key.</summary>
    public async Task SaveClaudeAsync(bool enabled, string model, string? newApiKey, string? updatedBy, CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.AiProviderCredentials.FirstOrDefaultAsync(c => c.Provider == ClaudeProvider, ct);
        if (row is null)
        {
            row = new AiProviderCredential { Provider = ClaudeProvider };
            db.AiProviderCredentials.Add(row);
        }

        if (!string.IsNullOrWhiteSpace(newApiKey))
        {
            row.EncryptedApiKey = _protector.Protect(newApiKey);
            row.KeyHint = newApiKey[^4..];
        }
        row.Enabled = enabled;
        row.Model = model;
        row.UpdatedAtUtc = DateTime.UtcNow;
        row.UpdatedBy = updatedBy;

        await db.SaveChangesAsync(ct);
        Invalidate();
    }

    public async Task RemoveClaudeKeyAsync(string? updatedBy, CancellationToken ct = default)
    {
        using var scope = _scopes.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
        var row = await db.AiProviderCredentials.FirstOrDefaultAsync(c => c.Provider == ClaudeProvider, ct);
        if (row is not null)
        {
            row.EncryptedApiKey = null;
            row.KeyHint = null;
            row.Enabled = false;
            row.UpdatedAtUtc = DateTime.UtcNow;
            row.UpdatedBy = updatedBy;
            await db.SaveChangesAsync(ct);
        }
        Invalidate();
    }

    private void Invalidate()
    {
        Interlocked.Increment(ref _version);
        _cache = null;
    }

    private bool TryDecrypt(string encrypted, out string key)
    {
        try
        {
            key = _protector.Unprotect(encrypted);
            return true;
        }
        catch (CryptographicException ex)
        {
            // Happens if the server's Data Protection keys changed; the key must be entered again.
            _logger.LogError(ex, "The saved Claude API key could not be decrypted");
            key = string.Empty;
            return false;
        }
    }
}
