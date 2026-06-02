using System.Text.RegularExpressions;

namespace PlantProcess.Application.Analytics.Value;

public sealed record HonestyLintResult(bool IsClean, IReadOnlyList<string> Violations);

/// <summary>
/// T-043: rejects forbidden value-facing phrasing per §2. Casing- and spacing-insensitive, so
/// "Guaranteed", "GUARANTEED", and "will   save" are all caught.
/// </summary>
public static class ValueHonestyLint
{
    private static readonly string[] Forbidden = { "guaranteed", "will save" };

    public static HonestyLintResult Validate(string? text)
    {
        var violations = new List<string>();
        if (string.IsNullOrWhiteSpace(text)) return new HonestyLintResult(true, violations);

        var normalized = Regex.Replace(text, "\\s+", " ").Trim().ToLowerInvariant();
        foreach (var phrase in Forbidden)
            if (normalized.Contains(phrase)) violations.Add(phrase);

        return new HonestyLintResult(violations.Count == 0, violations);
    }
}