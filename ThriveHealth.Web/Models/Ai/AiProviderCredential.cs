using System.ComponentModel.DataAnnotations;

namespace ThriveHealth.Web.Models.Ai;

/// <summary>Platform-wide AI provider settings entered in the super-admin console.</summary>
public class AiProviderCredential
{
    public int Id { get; set; }
    [MaxLength(40)] public string Provider { get; set; } = string.Empty;

    /// <summary>Encrypted with ASP.NET Data Protection. Never store or display the plain key.</summary>
    public string? EncryptedApiKey { get; set; }

    /// <summary>Last four characters only, so an admin can tell which key is saved.</summary>
    [MaxLength(4)] public string? KeyHint { get; set; }

    [MaxLength(80)] public string Model { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public DateTime UpdatedAtUtc { get; set; }
    [MaxLength(256)] public string? UpdatedBy { get; set; }
}
