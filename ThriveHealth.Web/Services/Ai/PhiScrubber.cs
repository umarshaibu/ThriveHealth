using System.Text.RegularExpressions;

namespace ThriveHealth.Web.Services.Ai;

/// <summary>
/// Best-effort PHI removal before sending text to external AI providers.
/// Removes/redacts names, hospital numbers, NIN, phones, emails, dates of birth,
/// and addresses. Not a substitute for a BAA — defensive layer only.
/// </summary>
public static class PhiScrubber
{
    private static readonly Regex PhoneRx = new(@"\+?\d[\d\s\-]{8,14}\d", RegexOptions.Compiled);
    private static readonly Regex EmailRx = new(@"[\w\.-]+@[\w\.-]+\.\w{2,}", RegexOptions.Compiled);
    private static readonly Regex NinRx = new(@"\b\d{11}\b", RegexOptions.Compiled);
    private static readonly Regex HospitalNumberRx = new(@"\b[A-Z]{2,4}/\d{4}/\d{4,6}\b", RegexOptions.Compiled);
    private static readonly Regex DobRx = new(@"\b\d{1,2}[/\-]\d{1,2}[/\-](19|20)\d{2}\b", RegexOptions.Compiled);

    public static string Scrub(string? input)
    {
        if (string.IsNullOrWhiteSpace(input)) return string.Empty;
        var s = input;
        s = HospitalNumberRx.Replace(s, "[HN]");
        s = NinRx.Replace(s, "[NIN]");
        s = PhoneRx.Replace(s, "[PHONE]");
        s = EmailRx.Replace(s, "[EMAIL]");
        s = DobRx.Replace(s, "[DOB]");
        return s;
    }

    /// <summary>Scrub a multi-line block, keeping line structure.</summary>
    public static string ScrubBlock(string? input)
        => string.IsNullOrEmpty(input) ? string.Empty : Scrub(input);

    /// <summary>Replace a person's name token with [PATIENT].</summary>
    public static string RedactName(string text, string? firstName, string? lastName)
    {
        if (string.IsNullOrEmpty(text)) return text;
        if (!string.IsNullOrWhiteSpace(firstName))
            text = Regex.Replace(text, Regex.Escape(firstName), "[PATIENT]", RegexOptions.IgnoreCase);
        if (!string.IsNullOrWhiteSpace(lastName))
            text = Regex.Replace(text, Regex.Escape(lastName), "[PATIENT]", RegexOptions.IgnoreCase);
        return text;
    }
}
