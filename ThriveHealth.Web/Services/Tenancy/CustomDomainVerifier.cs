using System.Security.Cryptography;
using System.Text.RegularExpressions;
using DnsClient;

namespace ThriveHealth.Web.Services.Tenancy;

/// <summary>
/// Generates per-tenant verification tokens and confirms the tenant published the token in a
/// TXT record at <c>_thrivehealth.{customDomain}</c>. We use TXT (not CNAME) because verification
/// must work even before the tenant has finished pointing live traffic at us — we just need proof
/// they control the DNS zone, not that the site is already live.
/// </summary>
public interface ICustomDomainVerifier
{
    string GenerateToken();
    Task<DomainVerificationResult> VerifyAsync(string customDomain, string expectedToken, CancellationToken ct = default);
    bool IsHostnameValid(string hostname);
}

public record DomainVerificationResult(bool Verified, string? FailureReason, IReadOnlyList<string>? FoundRecords);

public sealed class CustomDomainVerifier : ICustomDomainVerifier
{
    private const string TxtPrefix = "_thrivehealth.";
    private static readonly Regex HostnameRegex = new(
        @"^(?=.{1,253}$)(?!-)(?:[a-z0-9-]{1,63}(?<!-)\.)+[a-z]{2,63}$",
        RegexOptions.Compiled);

    private static readonly string[] ReservedSuffixes =
        { "thrivehealth.ng", "thrivehealth.local", "localhost" };

    private readonly ILookupClient _dns;
    private readonly ILogger<CustomDomainVerifier> _log;

    public CustomDomainVerifier(ILogger<CustomDomainVerifier> log)
    {
        _log = log;
        // Default DNS resolver options are fine: respects /etc/resolv.conf on Linux,
        // 5s timeout per query, retries enabled.
        _dns = new LookupClient(new LookupClientOptions
        {
            UseCache = false,        // verification must read live DNS, not cached state
            Timeout = TimeSpan.FromSeconds(5),
            Retries = 2
        });
    }

    public string GenerateToken()
    {
        // 24 url-safe bytes ≈ 192 bits — well above brute-force range, fits in TXT record cleanly.
        var bytes = RandomNumberGenerator.GetBytes(24);
        return "th-verify-" + Convert.ToHexString(bytes).ToLowerInvariant();
    }

    public bool IsHostnameValid(string hostname)
    {
        if (string.IsNullOrWhiteSpace(hostname)) return false;
        var h = hostname.Trim().ToLowerInvariant();
        if (!HostnameRegex.IsMatch(h)) return false;
        // Don't let tenants claim *.thrivehealth.ng — those are owned by us and would shadow
        // the platform's own subdomain routing.
        return !ReservedSuffixes.Any(suffix => h == suffix || h.EndsWith("." + suffix));
    }

    public async Task<DomainVerificationResult> VerifyAsync(string customDomain, string expectedToken, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(customDomain) || string.IsNullOrWhiteSpace(expectedToken))
            return new(false, "Domain or token missing.", null);

        var lookup = TxtPrefix + customDomain.Trim().ToLowerInvariant();
        try
        {
            var result = await _dns.QueryAsync(lookup, QueryType.TXT, cancellationToken: ct);
            if (result.HasError)
                return new(false, $"DNS lookup failed: {result.ErrorMessage}", null);

            // Each TXT record can be made up of multiple strings; flatten them all so a record
            // split across <quote> chunks still matches.
            var records = result.Answers.TxtRecords()
                .Select(r => string.Concat(r.Text))
                .ToList();

            if (records.Count == 0)
                return new(false, $"No TXT records found at {lookup}.", records);

            var match = records.Any(r => string.Equals(r.Trim(), expectedToken, StringComparison.OrdinalIgnoreCase));
            return match
                ? new(true, null, records)
                : new(false, $"TXT record at {lookup} does not contain the expected token.", records);
        }
        catch (Exception ex)
        {
            _log.LogWarning(ex, "DNS verification failed for {Domain}", customDomain);
            return new(false, $"DNS lookup failed: {ex.Message}", null);
        }
    }
}
