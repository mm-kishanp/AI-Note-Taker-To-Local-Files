using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Chroniq.Domain.Enums;

namespace Chroniq.Domain.Rules;

public static partial class TextNormalizer
{
    [GeneratedRegex(@"\s+")]
    private static partial Regex Whitespace();

    /// <summary>Case-insensitive, whitespace-collapsed, Unicode-normalized form used for name matching.</summary>
    public static string Name(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return "";
        var s = value.Normalize(NormalizationForm.FormC).Trim();
        return Whitespace().Replace(s, " ").ToLower(CultureInfo.InvariantCulture);
    }

    public static string Email(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "" : value.Trim().ToLowerInvariant();

    /// <summary>Accepts "acme.com" or "@acme.com" and returns "@acme.com".</summary>
    public static string Domain(string? value)
    {
        var s = Email(value);
        if (s.Length == 0) return "";
        var at = s.LastIndexOf('@');
        if (at > 0) s = s[at..];
        return s.StartsWith('@') ? s : "@" + s;
    }

    public static string ForRule(RuleKind kind, string value) => kind switch
    {
        RuleKind.Email => Email(value),
        RuleKind.EmailDomain => Domain(value),
        _ => Name(value),
    };

    public static bool IsValidEmail(string value)
    {
        var s = Email(value);
        var at = s.IndexOf('@');
        return at > 0 && at == s.LastIndexOf('@') && s.IndexOf('.', at) > at + 1 && !s.EndsWith('.');
    }
}
