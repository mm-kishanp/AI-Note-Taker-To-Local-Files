using System.Globalization;
using System.Text;

namespace GranolaPI.Domain.ValueObjects;

/// <summary>Generates folder-safe project slugs, e.g. "Motor Dealer App" -> "MotorDealerApp".</summary>
public static class Slug
{
    public const int MaxLength = 64;

    public static string FromName(string name)
    {
        var sb = new StringBuilder();
        var upperNext = true;
        foreach (var ch in name.Normalize(NormalizationForm.FormC))
        {
            if (char.IsLetterOrDigit(ch))
            {
                sb.Append(upperNext ? char.ToUpper(ch, CultureInfo.InvariantCulture) : ch);
                upperNext = false;
            }
            else if (ch is '-' or '_')
            {
                sb.Append(ch);
                upperNext = true;
            }
            else
            {
                upperNext = true;
            }
            if (sb.Length >= MaxLength) break;
        }
        return sb.Length == 0 ? "Project" : sb.ToString();
    }

    /// <summary>A slug is a single path segment of letters, digits, '-' and '_' only.</summary>
    public static bool IsValid(string? slug) =>
        !string.IsNullOrWhiteSpace(slug)
        && slug.Length <= MaxLength
        && slug.All(c => char.IsLetterOrDigit(c) || c is '-' or '_')
        && slug is not ("." or "..");
}
