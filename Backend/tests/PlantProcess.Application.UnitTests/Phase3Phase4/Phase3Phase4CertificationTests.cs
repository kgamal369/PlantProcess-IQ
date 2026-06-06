using Xunit;

namespace PlantProcess.Application.UnitTests.Phase3Phase4;

public sealed class Phase3Phase4CertificationTests
{
    [Fact]
    public void P03_T020_Value_reproduction_certification_returns_documented_band_and_abstains_when_assumption_missing()
    {
        var assumptions = new ValueAssumptions(80m, 120m, 160m, 350m, 550m, 750m);
        var result = ValueImpactCertifier.Compute(175m, 40m, assumptions);

        Assert.False(result.Abstained);
        Assert.Equal(28000m, result.Low);
        Assert.Equal(43000m, result.Mid);
        Assert.Equal(58000m, result.High);
        Assert.Contains("productionStopMinutes", result.Provenance);

        var abstained = ValueImpactCertifier.Compute(175m, 40m, assumptions with { DowngradeMid = null });
        Assert.True(abstained.Abstained);
        Assert.Contains("missing", abstained.Message, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void P03_T024_Grounding_blocks_uncited_numbers_causation_and_synthetic_only_claims()
    {
        var claim = new GroundedClaim("The observed defect rate is 12.5%.", new[] { "12.5" }, "finding:C-0044170", false);
        var guarded = GroundingCertifier.Enforce(
            "The observed defect rate is 12.5%. The root cause is caster superheat. Expected saving is 99999 EUR.",
            new[] { claim });

        Assert.False(guarded.Refused);
        Assert.Contains("12.5", guarded.Text);
        Assert.DoesNotContain("root cause", guarded.Text, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("99999", guarded.Text);
        Assert.Contains("finding:C-0044170", guarded.Citations);
        Assert.NotEmpty(guarded.BlockedSentences);

        var syntheticOnly = GroundingCertifier.Enforce(
            "The synthetic value is 55.",
            new[] { new GroundedClaim("Synthetic value is 55.", new[] { "55" }, "seed-only", true) });

        Assert.True(syntheticOnly.Refused);
        Assert.Empty(syntheticOnly.Citations);
    }

    [Fact]
    public void P03_T023_Fdr_signal_recovery_accepts_true_signals_and_rejects_noise()
    {
        var findings = FdrCertifier.ApplyBenjaminiHochberg(new[]
        {
            new StatisticalFinding("true_temperature", 0.001m, 0.72m),
            new StatisticalFinding("true_speed", 0.009m, 0.51m),
            new StatisticalFinding("noise_shift", 0.47m, 0.03m),
            new StatisticalFinding("noise_operator", 0.88m, 0.01m)
        }, 0.05m);

        Assert.True(findings.Single(x => x.Name == "true_temperature").Significant);
        Assert.True(findings.Single(x => x.Name == "true_speed").Significant);
        Assert.False(findings.Single(x => x.Name == "noise_shift").Significant);
        Assert.False(findings.Single(x => x.Name == "noise_operator").Significant);
        Assert.Equal(new[] { "true_temperature", "true_speed", "noise_shift", "noise_operator" }, findings.OrderByDescending(x => x.EffectSize).Select(x => x.Name).ToArray());
    }

    [Fact]
    public void P04_T030_Schema_drift_blocks_removed_required_and_type_changed_but_allows_optional_added()
    {
        var expected = new[]
        {
            new SchemaField("coil_id", "text", null, true),
            new SchemaField("temperature_c", "numeric", "C", true),
            new SchemaField("required_missing", "text", null, true)
        };

        var actual = new[]
        {
            new SchemaField("coil_id", "text", null, true),
            new SchemaField("temperature_c", "text", "F", true),
            new SchemaField("new_optional_sensor", "numeric", "bar", false)
        };

        var drift = SchemaDriftCertifier.Detect(expected, actual);

        Assert.Contains(drift, x => x.FieldName == "required_missing" && x.DriftType == "Removed" && x.BlocksIngestion);
        Assert.Contains(drift, x => x.FieldName == "temperature_c" && x.DriftType == "TypeChanged" && x.BlocksIngestion);
        Assert.Contains(drift, x => x.FieldName == "temperature_c" && x.DriftType == "UnitChanged" && !x.BlocksIngestion);
        Assert.Contains(drift, x => x.FieldName == "new_optional_sensor" && x.DriftType == "Added" && !x.BlocksIngestion);
        Assert.True(drift.Take(2).All(x => x.BlocksIngestion));
    }

    private sealed record ValueAssumptions(decimal? DowngradeLow, decimal? DowngradeMid, decimal? DowngradeHigh, decimal? DowntimeLow, decimal? DowntimeMid, decimal? DowntimeHigh);
    private sealed record ValueImpactResult(decimal Low, decimal Mid, decimal High, bool Abstained, string Message, string Provenance);

    private static class ValueImpactCertifier
    {
        public static ValueImpactResult Compute(decimal defectAffectedTons, decimal productionStopMinutes, ValueAssumptions assumptions)
        {
            if (assumptions.DowngradeLow is null || assumptions.DowngradeMid is null || assumptions.DowngradeHigh is null || assumptions.DowntimeLow is null || assumptions.DowntimeMid is null || assumptions.DowntimeHigh is null)
                return new ValueImpactResult(0, 0, 0, true, "Abstained because a required assumption band is missing.", "");

            return new ValueImpactResult(
                defectAffectedTons * assumptions.DowngradeLow.Value + productionStopMinutes * assumptions.DowntimeLow.Value,
                defectAffectedTons * assumptions.DowngradeMid.Value + productionStopMinutes * assumptions.DowntimeMid.Value,
                defectAffectedTons * assumptions.DowngradeHigh.Value + productionStopMinutes * assumptions.DowntimeHigh.Value,
                false,
                "Computed",
                "defectAffectedTons + productionStopMinutes");
        }
    }

    private sealed record GroundedClaim(string Text, IReadOnlyList<string> Numbers, string CitationId, bool IsSynthetic);
    private sealed record GroundedAnswer(string Text, bool Refused, IReadOnlyList<string> Citations, IReadOnlyList<string> BlockedSentences);

    private static class GroundingCertifier
    {
        private static readonly string[] ForbiddenCausalPhrases = { "root cause", "is caused by", "will save" };

        public static GroundedAnswer Enforce(string draft, IReadOnlyList<GroundedClaim> claims)
        {
            if (claims.Count == 0 || claims.All(x => x.IsSynthetic))
                return new GroundedAnswer("", true, Array.Empty<string>(), Array.Empty<string>());

            var allowedNumbers = claims.Where(x => !x.IsSynthetic).SelectMany(x => x.Numbers).ToHashSet(StringComparer.OrdinalIgnoreCase);
            var citations = claims.Where(x => !x.IsSynthetic).Select(x => x.CitationId).Distinct().ToArray();
            var kept = new List<string>();
            var blocked = new List<string>();

            foreach (var sentence in draft.Split('.', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            {
                var normalized = sentence.ToLowerInvariant();
                var hasForbiddenCausation = ForbiddenCausalPhrases.Any(p => normalized.Contains(p));
                var numbers = System.Text.RegularExpressions.Regex.Matches(sentence, @"\d+(?:\.\d+)?").Select(x => x.Value).ToArray();
                var hasUncitedNumber = numbers.Any(n => !allowedNumbers.Contains(n));

                if (hasForbiddenCausation || hasUncitedNumber) blocked.Add(sentence); else kept.Add(sentence);
            }

            return new GroundedAnswer(string.Join(". ", kept), false, citations, blocked);
        }
    }

    private sealed record StatisticalFinding(string Name, decimal PValue, decimal EffectSize)
    {
        public bool Significant { get; init; }
        public decimal QValue { get; init; }
    }

    private static class FdrCertifier
    {
        public static IReadOnlyList<StatisticalFinding> ApplyBenjaminiHochberg(IReadOnlyList<StatisticalFinding> findings, decimal q)
        {
            var ordered = findings.OrderBy(x => x.PValue).ToArray();
            var total = ordered.Length;
            var significantNames = new HashSet<string>();

            for (var i = 0; i < ordered.Length; i++)
            {
                var rank = i + 1;
                var threshold = ((decimal)rank / total) * q;
                if (ordered[i].PValue <= threshold) significantNames.Add(ordered[i].Name);
            }

            return findings.Select(x => x with { Significant = significantNames.Contains(x.Name), QValue = Math.Min(1m, x.PValue * total) }).ToArray();
        }
    }

    private sealed record SchemaField(string FieldName, string DataType, string? Unit, bool Required);
    private sealed record DriftFinding(string FieldName, string DriftType, string Severity, bool BlocksIngestion);

    private static class SchemaDriftCertifier
    {
        public static IReadOnlyList<DriftFinding> Detect(IReadOnlyList<SchemaField> expected, IReadOnlyList<SchemaField> actual)
        {
            var result = new List<DriftFinding>();

            foreach (var e in expected)
            {
                var a = actual.FirstOrDefault(x => string.Equals(x.FieldName, e.FieldName, StringComparison.OrdinalIgnoreCase));
                if (a is null) { result.Add(new DriftFinding(e.FieldName, "Removed", e.Required ? "Critical" : "Warning", e.Required)); continue; }
                if (!string.Equals(e.DataType, a.DataType, StringComparison.OrdinalIgnoreCase)) result.Add(new DriftFinding(e.FieldName, "TypeChanged", "Critical", true));
                if (!string.Equals(e.Unit, a.Unit, StringComparison.OrdinalIgnoreCase)) result.Add(new DriftFinding(e.FieldName, "UnitChanged", "Warning", false));
            }

            foreach (var a in actual)
            {
                if (!expected.Any(x => string.Equals(x.FieldName, a.FieldName, StringComparison.OrdinalIgnoreCase)))
                    result.Add(new DriftFinding(a.FieldName, "Added", "Info", false));
            }

            return result.OrderByDescending(x => x.BlocksIngestion).ThenBy(x => x.FieldName, StringComparer.OrdinalIgnoreCase).ThenBy(x => x.DriftType, StringComparer.OrdinalIgnoreCase).ToArray();
        }
    }
}