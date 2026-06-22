using System.Text;
using System.Text.RegularExpressions;

namespace TdsCertificatePortal.Services;

public static partial class TextNormalization
{
    public static string NormalizeName(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return string.Empty;
        }

        var prepared = value.Replace('_', ' ').Replace('-', ' ').Trim().ToUpperInvariant();
        return WhitespaceRegex().Replace(prepared, " ");
    }

    public static string NormalizePan(string? value)
    {
        return (value ?? string.Empty).Trim().ToUpperInvariant();
    }

    public static string ToTitleFromFilenameName(string value)
    {
        var normalized = NormalizeName(value).ToLowerInvariant();
        var builder = new StringBuilder(normalized.Length);
        var makeUpper = true;
        foreach (var ch in normalized)
        {
            builder.Append(makeUpper ? char.ToUpperInvariant(ch) : ch);
            makeUpper = char.IsWhiteSpace(ch);
        }

        return builder.ToString();
    }

    public static string AssessmentYearDisplay(string code)
    {
        if (code.Length == 6 && int.TryParse(code[..4], out var firstYear) && int.TryParse(code[4..], out var secondYear))
        {
            return $"{firstYear}-{secondYear:00}";
        }

        return code;
    }

    [GeneratedRegex("\\s+")]
    private static partial Regex WhitespaceRegex();
}
