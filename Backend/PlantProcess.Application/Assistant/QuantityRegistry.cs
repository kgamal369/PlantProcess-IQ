using System.Globalization;
using System.Text.RegularExpressions;

namespace PlantProcess.Application.Assistant;

/// <summary>
/// T-074. One row of the parameter registry, which is the ONLY authority for a
/// quantity's type, unit, sign and range. There is deliberately no second
/// dictionary anywhere in assistant code.
/// </summary>
public sealed record RegistryQuantity(
    string ParameterCode,
    string ParameterName,
    string ValueType,
    string? UnitOfMeasure,
    decimal? ExpectedMinValue,
    decimal? ExpectedMaxValue,
    bool IsSynthetic);

/// <summary>
/// Reads the registry. NOTE: parameter_definitions carries no tenant column -
/// the entity has no TenantId - so this signature does not pretend to take one.
/// Deleted rows are excluded by the implementation, never by the caller.
/// </summary>
public interface IParameterQuantityRegistry
{
    Task<IReadOnlyList<RegistryQuantity>> GetActiveAsync(CancellationToken cancellationToken);
}

/// <summary>
/// Three outcomes, not two. The middle one is the whole point: registry
/// vocabulary can match a question while no approved authority exists for it,
/// and that is neither a match nor an absence.
/// </summary>
public enum QuantityResolutionOutcome
{
    /// <summary>No registry vocabulary appears in the question. Leave everything alone.</summary>
    NoMatch,

    /// <summary>Exactly one active, non-deleted, non-synthetic definition wins.</summary>
    Resolved,

    /// <summary>
    /// Registry vocabulary DOES match, but no unique approved authority exists -
    /// synthetic-only, tied, or conflicting. A numeric answer must fail closed.
    /// </summary>
    KnownButUntrustedOrAmbiguous
}

public sealed record QuantityResolution(
    QuantityResolutionOutcome Outcome,
    RegistryQuantity? Quantity,
    string? Reason)
{
    public static QuantityResolution NoMatch() => new(QuantityResolutionOutcome.NoMatch, null, null);

    public static QuantityResolution Resolved(RegistryQuantity quantity)
        => new(QuantityResolutionOutcome.Resolved, quantity, null);

    public static QuantityResolution Untrusted(string reason)
        => new(QuantityResolutionOutcome.KnownButUntrustedOrAmbiguous, null, reason);
}

/// <summary>
/// Resolves a question to a registry quantity using ONLY the registry's own
/// vocabulary. No unit, quantity or industry word is written here: every phrase
/// compared against the question comes out of parameter_code and parameter_name.
/// </summary>
public static class QuantityResolver
{
    private static readonly Regex Whitespace = new("\\s+", RegexOptions.Compiled);

    /// <summary>Mechanical only: trim, lowercase, dot/underscore/hyphen to space, collapse.</summary>
    public static string Normalise(string? value)
    {
        if (string.IsNullOrWhiteSpace(value)) return string.Empty;

        var spaced = value.Trim().ToLowerInvariant()
            .Replace('.', ' ')
            .Replace('_', ' ')
            .Replace('-', ' ');

        return Whitespace.Replace(spaced, " ").Trim();
    }

    public static QuantityResolution Resolve(string? question, IReadOnlyList<RegistryQuantity>? registry)
    {
        var normalisedQuestion = Normalise(question);
        if (normalisedQuestion.Length == 0 || registry is null || registry.Count == 0)
        {
            return QuantityResolution.NoMatch();
        }

        var matches = new List<(RegistryQuantity Quantity, int Length)>();
        foreach (var candidate in registry)
        {
            var length = Math.Max(
                PhraseLength(normalisedQuestion, candidate.ParameterCode),
                PhraseLength(normalisedQuestion, candidate.ParameterName));

            if (length > 0) matches.Add((candidate, length));
        }

        if (matches.Count == 0) return QuantityResolution.NoMatch();

        // A synthetic definition must never win over a configured one, so the
        // approved rows are considered ALONE rather than ranked alongside.
        var approved = matches.Where(m => !m.Quantity.IsSynthetic).ToList();

        if (approved.Count == 0)
        {
            return QuantityResolution.Untrusted(
                "registry vocabulary matches, but every matching definition is synthetic");
        }

        var best = approved.Max(m => m.Length);
        var winners = approved.Where(m => m.Length == best).ToList();

        if (winners.Count > 1)
        {
            return QuantityResolution.Untrusted(
                "more than one approved definition matches the question equally");
        }

        return QuantityResolution.Resolved(winners[0].Quantity);
    }

    private static int PhraseLength(string normalisedQuestion, string? phrase)
    {
        var normalised = Normalise(phrase);
        if (normalised.Length == 0) return 0;

        return normalisedQuestion.Contains(normalised, StringComparison.Ordinal) ? normalised.Length : 0;
    }

    internal static bool TryValue(string token, out decimal value)
        => decimal.TryParse(
            token.Replace(",", string.Empty).TrimEnd('.'),
            NumberStyles.Number,
            CultureInfo.InvariantCulture,
            out value);
}