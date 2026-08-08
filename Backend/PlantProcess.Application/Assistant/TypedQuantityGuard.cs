using System.Text.RegularExpressions;

namespace PlantProcess.Application.Assistant;

/// <summary>What the guard kept, and what it refused to let through.</summary>
public sealed record QuantityGuardResult(string DraftText, IReadOnlyList<string> Blocked);

/// <summary>
/// T-074. Validates QUANTITY SEMANTICS only - unit, sign and range against the
/// registry - and nothing else.
///
/// It owns none of GroundingService's work: uncited numbers, provenance,
/// synthetic-claim filtering and causation wording all remain there, and this
/// runs before it on the same sentence boundaries so the two compose instead of
/// overlapping.
///
/// CANDIDATE IDENTIFICATION is deliberately narrow. For a resolved quantity with
/// a declared unit, only bounded deterministic forms tied to THAT unit count as
/// an answer to the quantity:
///
///     value unit                 1.31 m/min
///     low-high unit              1.20-1.45 m/min
///     low to high unit           1.20 to 1.45 m/min
///
/// Everything else in the sentence is contextual and is never range-checked, so
/// "1.31 m/min on 8 August across 120 observations" is judged on 1.31 alone.
///
/// There is no unit dictionary, no conversion and no vocabulary. A date, a mass
/// or a bare number fails for one reason only: no candidate satisfies the
/// requested quantity's registry contract.
/// </summary>
public static class TypedQuantityGuard
{
    private static readonly Regex SentenceRx = new("(?<=[\\.\\!\\?])\\s+", RegexOptions.Compiled);
    private static readonly Regex NumberRx = new("-?\\d[\\d.,]*", RegexOptions.Compiled);

    public static QuantityGuardResult Apply(string? draftText, QuantityResolution? resolution)
    {
        if (string.IsNullOrWhiteSpace(draftText) ||
            resolution is null ||
            resolution.Outcome == QuantityResolutionOutcome.NoMatch)
        {
            return new QuantityGuardResult(draftText ?? string.Empty, Array.Empty<string>());
        }

        var kept = new List<string>();
        var blocked = new List<string>();

        foreach (var sentence in SentenceRx.Split(draftText))
        {
            if (string.IsNullOrWhiteSpace(sentence)) continue;

            var trimmed = sentence.Trim();

            // A sentence with no numbers cannot present a value as the quantity.
            if (NumberRx.Matches(trimmed).Count == 0)
            {
                kept.Add(trimmed);
                continue;
            }

            if (resolution.Outcome == QuantityResolutionOutcome.KnownButUntrustedOrAmbiguous)
            {
                // The question named a quantity the registry knows and cannot
                // vouch for. No numeric answer may stand in for it.
                blocked.Add(trimmed);
                continue;
            }

            if (IsAcceptable(trimmed, resolution.Quantity!)) kept.Add(trimmed);
            else blocked.Add(trimmed);
        }

        return new QuantityGuardResult(string.Join(" ", kept), blocked);
    }

    private static bool IsAcceptable(string sentence, RegistryQuantity quantity)
    {
        if (!string.IsNullOrWhiteSpace(quantity.UnitOfMeasure))
        {
            var unit = Regex.Escape(quantity.UnitOfMeasure.Trim());

            // The SIGN is part of the candidate. An earlier version started the
            // capture at a digit, so "-1.31 u/min" yielded 1.31, passed the lower
            // bound and survived - the bound was right and the extraction was
            // wrong. The registry range supplies sign authority, but only if the
            // sign reaches it.
            var band = new Regex(
                "(-?\\d[\\d.,]*)\\s*(?:-|\u2013|to)\\s*(-?\\d[\\d.,]*)\\s*" + unit + "(?![A-Za-z0-9])",
                RegexOptions.IgnoreCase);

            var scalar = new Regex(
                "(-?\\d[\\d.,]*)\\s*" + unit + "(?![A-Za-z0-9])",
                RegexOptions.IgnoreCase);

            var values = new List<decimal>();

            foreach (Match match in band.Matches(sentence))
            {
                // Both endpoints of a band are the answer, so both are checked.
                if (QuantityResolver.TryValue(match.Groups[1].Value, out var low)) values.Add(low);
                if (QuantityResolver.TryValue(match.Groups[2].Value, out var high)) values.Add(high);
            }

            if (values.Count == 0)
            {
                foreach (Match match in scalar.Matches(sentence))
                {
                    if (QuantityResolver.TryValue(match.Groups[1].Value, out var value)) values.Add(value);
                }
            }

            // Numeric material offered as the answer with no candidate carrying
            // the registry unit. Fail closed.
            if (values.Count == 0) return false;

            return values.All(value => WithinBounds(value, quantity));
        }

        // No declared unit. Unit validation is unavailable, but bounds still are.
        // Only judge a number that can be identified safely as this quantity.
        var normalisedSentence = QuantityResolver.Normalise(sentence);
        var name = QuantityResolver.Normalise(quantity.ParameterName);
        var code = QuantityResolver.Normalise(quantity.ParameterCode);

        var namesTheQuantity =
            (name.Length > 0 && normalisedSentence.Contains(name, StringComparison.Ordinal)) ||
            (code.Length > 0 && normalisedSentence.Contains(code, StringComparison.Ordinal));

        if (!namesTheQuantity) return true;

        var numbers = NumberRx.Matches(sentence).Select(m => m.Value).ToList();

        // More than one number and no unit to disambiguate: fail closed rather
        // than range-check an arbitrary one.
        if (numbers.Count != 1) return false;

        return QuantityResolver.TryValue(numbers[0], out var single) && WithinBounds(single, quantity);
    }

    private static bool WithinBounds(decimal value, RegistryQuantity quantity)
    {
        if (quantity.ExpectedMinValue.HasValue && value < quantity.ExpectedMinValue.Value) return false;
        if (quantity.ExpectedMaxValue.HasValue && value > quantity.ExpectedMaxValue.Value) return false;
        return true;
    }
}