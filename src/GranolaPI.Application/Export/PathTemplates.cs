using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;

namespace GranolaPI.Application.Export;

public sealed record TemplateContext(
    string? ProjectName,
    string? ProjectSlug,
    string? ClientName,
    DateTimeOffset MeetingDate,
    string MeetingId,
    string MeetingTitle,
    string? ProjectDirectory = null);

/// <summary>
/// Expands folder/file templates. Every variable value is sanitized into a single path segment, so
/// values can never introduce separators or "..". Literal template text is validated separately.
/// </summary>
public static partial class PathTemplates
{
    public static readonly IReadOnlyList<string> Variables =
    [
        "ProjectName", "ProjectSlug", "ClientName", "MeetingDate", "Year", "Month", "Day",
        "MeetingId", "MeetingTitle", "ProjectDirectory",
    ];

    [GeneratedRegex(@"\{(\w+)\}")]
    private static partial Regex Token();

    public static IReadOnlyList<string> Validate(string? template, string field, bool allowProjectVars = true, bool isFileName = false)
    {
        var errors = new List<string>();
        if (string.IsNullOrWhiteSpace(template))
        {
            errors.Add($"{field} is required.");
            return errors;
        }
        if (Path.IsPathRooted(template) || template.StartsWith('/') || template.StartsWith('\\'))
            errors.Add($"{field} must be relative to its root directory.");
        foreach (Match m in Token().Matches(template))
        {
            var name = m.Groups[1].Value;
            if (!Variables.Contains(name)) errors.Add($"{field}: unknown variable {{{name}}}.");
            else if (!allowProjectVars && name is "ProjectName" or "ProjectSlug" or "ClientName" or "ProjectDirectory")
                errors.Add($"{field}: {{{name}}} is not available here.");
        }
        if (isFileName && (template.Contains('/') || template.Contains('\\')))
            errors.Add($"{field} must not contain folder separators.");
        var literal = Token().Replace(template, "x");
        foreach (var seg in literal.Split('/', '\\'))
        {
            if (seg is ".." or ".") errors.Add($"{field} must not contain '.' or '..' segments.");
            else if (seg.Length == 0) errors.Add($"{field} contains an empty folder segment.");
            else if (seg.IndexOfAny(FileNames.InvalidChars) >= 0) errors.Add($"{field} contains characters that are invalid in file names.");
        }
        return errors.Distinct().ToList();
    }

    /// <summary>Expands a directory template into relative path segments.</summary>
    public static List<string> ExpandDirectory(string template, TemplateContext ctx)
    {
        var segments = new List<string>();
        foreach (var part in template.Split('/', '\\', StringSplitOptions.RemoveEmptyEntries))
        {
            if (part == "{ProjectDirectory}" && ctx.ProjectDirectory is not null)
            {
                segments.AddRange(ctx.ProjectDirectory.Split('/', StringSplitOptions.RemoveEmptyEntries));
                continue;
            }
            segments.Add(FileNames.SanitizeSegment(Token().Replace(part, m => Value(m.Groups[1].Value, ctx, forFileName: false))));
        }
        return segments;
    }

    public static string ExpandFileName(string template, TemplateContext ctx)
    {
        var raw = Token().Replace(template, m => Value(m.Groups[1].Value, ctx, forFileName: true));
        return FileNames.SanitizeFileStem(raw);
    }

    private static string Value(string name, TemplateContext ctx, bool forFileName)
    {
        var d = ctx.MeetingDate;
        var v = name switch
        {
            "ProjectName" => ctx.ProjectName ?? "",
            "ProjectSlug" => ctx.ProjectSlug ?? "",
            "ClientName" => ctx.ClientName ?? "NoClient",
            "MeetingDate" => d.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture),
            "Year" => d.ToString("yyyy", CultureInfo.InvariantCulture),
            "Month" => d.ToString("MM", CultureInfo.InvariantCulture),
            "Day" => d.ToString("dd", CultureInfo.InvariantCulture),
            "MeetingId" => ctx.MeetingId,
            "MeetingTitle" => forFileName ? FileNames.TitleForFileName(ctx.MeetingTitle) : ctx.MeetingTitle,
            "ProjectDirectory" => ctx.ProjectDirectory ?? "",
            _ => "",
        };
        // Values never contribute separators.
        return v.Replace('/', '-').Replace('\\', '-');
    }
}

public static class FileNames
{
    public static readonly char[] InvalidChars = ['<', '>', ':', '"', '|', '?', '*', '\0'];
    private static readonly HashSet<string> Reserved = new(StringComparer.OrdinalIgnoreCase)
    {
        "CON", "PRN", "AUX", "NUL",
        "COM1", "COM2", "COM3", "COM4", "COM5", "COM6", "COM7", "COM8", "COM9",
        "LPT1", "LPT2", "LPT3", "LPT4", "LPT5", "LPT6", "LPT7", "LPT8", "LPT9",
    };

    public const int MaxStemLength = 120;

    /// <summary>"Client: Requirement discussion?" -> "Client-Requirement-discussion". Keeps Unicode letters.</summary>
    public static string TitleForFileName(string? title)
    {
        var sb = new StringBuilder();
        foreach (var ch in (title ?? "").Normalize(NormalizationForm.FormC))
        {
            if (char.IsLetterOrDigit(ch) || char.GetUnicodeCategory(ch) is UnicodeCategory.NonSpacingMark or UnicodeCategory.SpacingCombiningMark)
                sb.Append(ch);
            else if (ch is '_' or '.' or '(' or ')' or '&' or '+')
                sb.Append(ch == '.' ? '-' : ch);
            else
                sb.Append('-');
        }
        var s = CollapseDashes(sb.ToString()).Trim('-');
        return s.Length == 0 ? "Untitled" : s;
    }

    public static string SanitizeSegment(string value)
    {
        var sb = new StringBuilder();
        foreach (var ch in value.Normalize(NormalizationForm.FormC))
        {
            if (ch < 32 || InvalidChars.Contains(ch) || ch is '/' or '\\') sb.Append(' ');
            else sb.Append(ch);
        }
        var s = string.Join(' ', sb.ToString().Split(' ', StringSplitOptions.RemoveEmptyEntries)).TrimEnd('.', ' ').Trim();
        if (s is "" or "." or "..") s = "_";
        if (Reserved.Contains(s) || Reserved.Contains(s.Split('.')[0])) s = "_" + s;
        return s.Length > MaxStemLength ? s[..MaxStemLength].TrimEnd('.', ' ') : s;
    }

    public static string SanitizeFileStem(string value)
    {
        var s = SanitizeSegment(value);
        return s.Length == 0 ? "Untitled" : s;
    }

    private static string CollapseDashes(string s)
    {
        var sb = new StringBuilder(s.Length);
        foreach (var ch in s)
        {
            if (ch == '-' && sb.Length > 0 && sb[^1] == '-') continue;
            sb.Append(ch);
        }
        return sb.ToString();
    }
}

/// <summary>Resolves and guards absolute paths so nothing escapes a configured root.</summary>
public static class SafePath
{
    public static string NormalizeRoot(string root, string baseDirectory)
    {
        if (string.IsNullOrWhiteSpace(root)) throw new ArgumentException("Root directory is required.");
        var full = Path.GetFullPath(Path.IsPathRooted(root) ? root : Path.Combine(baseDirectory, root));
        return Path.TrimEndingDirectorySeparator(full);
    }

    /// <summary>Combines root with a relative path and throws if the result is outside root.</summary>
    public static string Combine(string root, string relative)
    {
        if (Path.IsPathRooted(relative)) throw new InvalidOperationException("Expected a relative path.");
        var full = Path.GetFullPath(Path.Combine(root, relative));
        if (!IsUnder(root, full)) throw new InvalidOperationException($"Path '{relative}' escapes the export root.");
        return full;
    }

    public static bool IsUnder(string root, string fullPath)
    {
        var r = Path.TrimEndingDirectorySeparator(Path.GetFullPath(root)) + Path.DirectorySeparatorChar;
        var f = Path.GetFullPath(fullPath);
        var cmp = OperatingSystem.IsWindows() ? StringComparison.OrdinalIgnoreCase : StringComparison.Ordinal;
        return f.StartsWith(r, cmp) || string.Equals(Path.TrimEndingDirectorySeparator(f) + Path.DirectorySeparatorChar, r, cmp);
    }

    public static string ToPortable(string relative) => relative.Replace('\\', '/');
}
