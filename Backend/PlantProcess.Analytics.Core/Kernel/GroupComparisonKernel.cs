using System.Collections.Generic;
using System.Linq;
using PlantProcess.Analytics.Core.Numerics;

namespace PlantProcess.Analytics.Core.Kernel;

/// <summary>
/// Schema-independent Numeric x Categorical kernel. Assumption-aware one-way ANOVA
/// with a Kruskal-Wallis fallback when the parametric assumptions are not supported.
/// Reads no database, holds no connection and knows no customer vocabulary.
/// </summary>
public static class GroupComparisonKernel
{
    public const int MinimumGroups = 2;
    public const int MinimumGroupSize = 2;

    /// <summary>
    /// Variance-ratio ceiling above which the parametric assumption is treated as
    /// unsupported. Recorded as evidence on every result so the decision is inspectable.
    /// </summary>
    public const double VarianceRatioCeiling = 4.0;

    /// <summary>Levene significance floor. Below this the homogeneity assumption is rejected.</summary>
    public const double LeveneAlpha = 0.05;

    /// <summary>Absolute skewness ceiling beyond which the normality assumption is rejected.</summary>
    public const double AbsoluteSkewnessCeiling = 2.0;

    public static GroupComparisonResult Evaluate(GroupComparisonInput input)
    {
        var groups = input.Groups ?? new List<NumericGroup>();
        var keys = groups.Select(g => g.Key).ToList();
        var sizes = groups.Select(g => g.Values?.Count ?? 0).ToList();
        int population = sizes.Sum();

        if (groups.Count < MinimumGroups)
            return GroupComparisonResult.Refuse(
                KernelTerminalState.InsufficientData,
                KernelExclusionReason.InsufficientGroups,
                ExclusionAttribution.Data,
                $"Group comparison requires at least {MinimumGroups} groups; the aligned population contains {groups.Count}.",
                population, sizes, keys);

        int smallest = sizes.Min();
        if (smallest < MinimumGroupSize)
            return GroupComparisonResult.Refuse(
                KernelTerminalState.InsufficientData,
                KernelExclusionReason.InsufficientSample,
                ExclusionAttribution.Data,
                $"Every group requires at least {MinimumGroupSize} observations; the smallest group contains {smallest}.",
                population, sizes, keys);

        var all = groups.SelectMany(g => g.Values).ToList();
        double pooledVariance = Stats.SampleVariance(all);
        if (pooledVariance <= 0.0)
            return GroupComparisonResult.Refuse(
                KernelTerminalState.NotApplicable,
                KernelExclusionReason.ConstantZeroVariance,
                ExclusionAttribution.Data,
                "The numeric variable is constant across the aligned population; pooled variance is zero, so no group difference can exist.",
                population, sizes, keys);

        var evidence = AssessAssumptions(groups);
        return evidence.ParametricAssumptionsSupported
            ? OneWayAnova(groups, keys, sizes, population, evidence)
            : KruskalWallis(groups, keys, sizes, population, evidence);
    }

    private static AssumptionEvidence AssessAssumptions(IReadOnlyList<NumericGroup> groups)
    {
        var variances = groups.Select(g => Stats.SampleVariance(g.Values)).ToList();
        var sds = variances.Select(Math.Sqrt).ToList();
        var skews = groups.Select(g => Skewness(g.Values)).ToList();

        double maxVar = variances.Max();
        double minVar = variances.Min();
        double ratio = minVar <= 0.0 ? double.PositiveInfinity : maxVar / minVar;

        var levene = LeveneMedianCentered(groups);
        double leveneStat = levene.Item1;
        double levenePValue = levene.Item2;

        bool homogeneous = levenePValue >= LeveneAlpha && ratio <= VarianceRatioCeiling;
        bool symmetric = skews.All(s => Math.Abs(s) <= AbsoluteSkewnessCeiling);
        bool supported = homogeneous && symmetric;

        string rationale = supported
            ? "Group variances are homogeneous and group distributions are not severely skewed; the parametric one-way ANOVA assumptions are supported."
            : !homogeneous && !symmetric
                ? "Group variances are heterogeneous and at least one group is severely skewed; the parametric assumptions are not supported."
                : !homogeneous
                    ? "Group variances are heterogeneous; the parametric assumption of homogeneity of variance is not supported."
                    : "At least one group is severely skewed; the parametric assumption of approximate normality is not supported.";

        return new AssumptionEvidence(leveneStat, levenePValue, sds, ratio, skews, supported, rationale);
    }

    private static GroupComparisonResult OneWayAnova(
        IReadOnlyList<NumericGroup> groups, IReadOnlyList<string> keys,
        IReadOnlyList<int> sizes, int population, AssumptionEvidence evidence)
    {
        var all = groups.SelectMany(g => g.Values).ToList();
        double grandMean = Stats.Mean(all);
        int k = groups.Count;

        double ssBetween = groups.Sum(g => g.Values.Count * Math.Pow(Stats.Mean(g.Values) - grandMean, 2.0));
        double ssWithin = groups.Sum(g => g.Values.Sum(v => Math.Pow(v - Stats.Mean(g.Values), 2.0)));
        double ssTotal = ssBetween + ssWithin;

        int df1 = k - 1;
        int df2 = population - k;

        if (df2 <= 0 || ssWithin <= 0.0)
            return GroupComparisonResult.Refuse(
                KernelTerminalState.NotApplicable,
                KernelExclusionReason.ConstantZeroVariance,
                ExclusionAttribution.Data,
                "Within-group variance is zero; the F ratio is undefined.",
                population, sizes, keys);

        double f = (ssBetween / df1) / (ssWithin / df2);
        double p = SpecialFunctions.FDistributionSurvival(f, df1, df2);
        double etaSquared = ssTotal <= 0.0 ? double.NaN : ssBetween / ssTotal;

        return new GroupComparisonResult(
            KernelTerminalState.Finding, KernelMethod.Anova,
            KernelExclusionReason.None, ExclusionAttribution.None,
            "One-way ANOVA: parametric assumptions supported.",
            population, sizes, keys,
            f, df1, df2, p, "EtaSquared", etaSquared, false, evidence);
    }

    private static GroupComparisonResult KruskalWallis(
        IReadOnlyList<NumericGroup> groups, IReadOnlyList<string> keys,
        IReadOnlyList<int> sizes, int population, AssumptionEvidence evidence)
    {
        var flat = new List<double>(population);
        var owner = new List<int>(population);
        for (int gi = 0; gi < groups.Count; gi++)
            foreach (var v in groups[gi].Values) { flat.Add(v); owner.Add(gi); }

        var ranks = Stats.Ranks(flat);
        int n = population;
        int k = groups.Count;

        double sum = 0.0;
        for (int gi = 0; gi < k; gi++)
        {
            double rankSum = 0.0;
            for (int i = 0; i < n; i++) if (owner[i] == gi) rankSum += ranks[i];
            sum += rankSum * rankSum / groups[gi].Values.Count;
        }
        double hRaw = 12.0 / (n * (n + 1.0)) * sum - 3.0 * (n + 1.0);

        double tieCorrection = TieCorrection(flat, n);
        bool tiesPresent = tieCorrection < 1.0;
        double h = tieCorrection <= 0.0 ? hRaw : hRaw / tieCorrection;

        int df = k - 1;
        double p = SpecialFunctions.ChiSquareSurvival(h, df);
        double epsilonSquared = (n - k) <= 0 ? double.NaN : (h - k + 1.0) / (n - k);

        return new GroupComparisonResult(
            KernelTerminalState.Finding, KernelMethod.KruskalWallis,
            KernelExclusionReason.None, ExclusionAttribution.None,
            "Kruskal-Wallis: " + evidence.Rationale,
            population, sizes, keys,
            h, df, 0, p, "EpsilonSquared", epsilonSquared, tiesPresent, evidence);
    }

    /// <summary>Tie correction 1 - sum(t^3 - t) / (n^3 - n). Industrial data is heavily tied.</summary>
    private static double TieCorrection(IReadOnlyList<double> values, int n)
    {
        if (n < 2) return 1.0;
        var counts = new Dictionary<double, int>();
        foreach (var v in values)
            counts[v] = counts.TryGetValue(v, out var c) ? c + 1 : 1;
        double total = 0.0;
        foreach (var kv in counts)
            if (kv.Value > 1) total += Math.Pow(kv.Value, 3.0) - kv.Value;
        double denominator = Math.Pow(n, 3.0) - n;
        return denominator <= 0.0 ? 1.0 : 1.0 - total / denominator;
    }

    /// <summary>Adjusted Fisher-Pearson standardised moment coefficient.</summary>
    private static double Skewness(IReadOnlyList<double> values)
    {
        int n = values.Count;
        if (n < 3) return 0.0;
        double mean = Stats.Mean(values);
        double sd = Stats.SampleStdDev(values);
        if (sd <= 0.0) return 0.0;
        double m3 = values.Sum(v => Math.Pow((v - mean) / sd, 3.0));
        return m3 * n / ((n - 1.0) * (n - 2.0));
    }

    /// <summary>Levene test with median centering. Recorded as evidence, never as the sole rule.</summary>
    private static Tuple<double, double> LeveneMedianCentered(IReadOnlyList<NumericGroup> groups)
    {
        var z = new List<List<double>>();
        foreach (var g in groups)
        {
            double median = Stats.Median(g.Values);
            z.Add(g.Values.Select(v => Math.Abs(v - median)).ToList());
        }
        int n = z.Sum(s => s.Count);
        int k = z.Count;
        if (n - k <= 0 || k < 2) return Tuple.Create(double.NaN, double.NaN);

        var all = z.SelectMany(s => s).ToList();
        double grand = Stats.Mean(all);
        double numerator = z.Sum(s => s.Count * Math.Pow(Stats.Mean(s) - grand, 2.0)) / (k - 1.0);
        double denominator = z.Sum(s => s.Sum(v => Math.Pow(v - Stats.Mean(s), 2.0))) / (n - k);
        if (denominator <= 0.0) return Tuple.Create(double.PositiveInfinity, 0.0);

        double w = numerator / denominator;
        double p = SpecialFunctions.FDistributionSurvival(w, k - 1, n - k);
        return Tuple.Create(w, p);
    }
}
