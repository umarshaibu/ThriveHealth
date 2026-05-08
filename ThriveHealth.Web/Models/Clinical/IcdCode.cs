using System.ComponentModel.DataAnnotations;

namespace ThriveHealth.Web.Models.Clinical;

public class IcdCode
{
    public int Id { get; set; }

    [Required, MaxLength(20)] public string Code { get; set; } = string.Empty;
    [Required, MaxLength(300)] public string Description { get; set; } = string.Empty;

    [MaxLength(20)] public string Version { get; set; } = "ICD-10";
    [MaxLength(60)] public string? Category { get; set; }

    [MaxLength(500)] public string? LocalSynonyms { get; set; }

    public bool IsCommon { get; set; } = true;
}

public class DotPhrase
{
    public int Id { get; set; }
    public string OwnerId { get; set; } = string.Empty;

    [Required, MaxLength(40)] public string Trigger { get; set; } = string.Empty;
    [Required, MaxLength(2000)] public string Expansion { get; set; } = string.Empty;

    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
