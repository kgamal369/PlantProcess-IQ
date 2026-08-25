// Transition-aware pooling guard kernel.
//
// Backlog origin: T-236.
//
// Admits a population for pooling only when every sample provably belongs to the same
// operational regime. It computes no statistic and returns no value: the answer is
// whether the question may be asked of this population at all.
//
// Two ways a population fails. Samples that span regimes cannot be pooled, because the
// result would average two different processes. A single sample whose timestamp
// uncertainty straddles a regime boundary cannot be placed, and placing it by its point
// estimate would smuggle the same defect in one sample at a time.
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Analytics.Core.Kernel;

public static class TransitionAwarePoolingGuard
{
    /// <summary>
    /// Whether these samples may be pooled, under the transitions declared for the scope.
    /// </summary>
    public static PoolingAdmission Admit(
        OperationalTransitionRegistry registry,
        string? scopeKey,
        IReadOnlyList<RegimeScopedSample> samples,
        StabilisationObservation observation)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(observation);

        if (samples.Count == 0 || samples.Any(s => s is null))
        {
            // An empty population is not a population that agrees with itself. Returning
            // an admission here would let a downstream statistic report a confident
            // answer computed from nothing.
            return Refuse(PoolingGuardCodes.EmptyPopulation, 0);
        }

        if (!DeclaredKey.TryNormalise(scopeKey, out var scope))
        {
            return Refuse(OperationalTransitionCodes.ScopeNotDeclared, samples.Count);
        }

        if (samples.Any(s => !string.Equals(Normalise(s.ScopeKey), scope, StringComparison.Ordinal)))
        {
            // Samples from different scopes have different declared transitions, so a
            // single regime verdict would not mean anything.
            return Refuse(PoolingGuardCodes.HeterogeneousScope, samples.Count);
        }

        var regimes = new List<OperationalRegime>();

        foreach (var sample in samples)
        {
            var atPoint = registry.IsScopeDeclared(scope)
                ? OperationalTransitionKernel.ClassifyInstant(registry, scope, sample.Instant.Instant, observation)
                : null;

            if (atPoint is null || !atPoint.IsDecided)
            {
                // The regime classifier's refusal is the guard's refusal. Nothing here
                // second-guesses it.
                return Refuse(atPoint?.Code ?? OperationalTransitionCodes.ScopeNotDeclared, samples.Count);
            }

            var earliest = OperationalTransitionKernel.ClassifyInstant(registry, scope, sample.Instant.EarliestPossible, observation);
            var latest = OperationalTransitionKernel.ClassifyInstant(registry, scope, sample.Instant.LatestPossible, observation);

            if (!earliest.IsDecided) return Refuse(earliest.Code, samples.Count);
            if (!latest.IsDecided) return Refuse(latest.Code, samples.Count);

            if (earliest.Regime != atPoint.Regime || latest.Regime != atPoint.Regime)
            {
                // This sample could have been taken on either side of a changeover. Which
                // side its point estimate happens to land on is not evidence.
                return Refuse(PoolingGuardCodes.SampleRegimeTemporallyUncertain, samples.Count);
            }

            regimes.Add(atPoint.Regime);
        }

        var distinct = regimes.Distinct().ToArray();

        if (distinct.Length > 1)
        {
            return Refuse(PoolingGuardCodes.MixedProcessRegime, samples.Count);
        }

        return new PoolingAdmission(
            IsAdmitted: true,
            distinct[0],
            samples.Count,
            PoolingGuardCodes.PoolingAdmitted,
            TerminalState.Finding,
            ExclusionAttribution.None);
    }

    /// <summary>
    /// The subset of a population belonging to one regime, for a caller that wants to
    /// analyse steady state rather than be refused. Selecting is the caller's decision,
    /// declared by asking for it; the guard still refuses to pool what was not selected.
    /// </summary>
    public static IReadOnlyList<RegimeScopedSample> SelectRegime(
        OperationalTransitionRegistry registry,
        string? scopeKey,
        IReadOnlyList<RegimeScopedSample> samples,
        OperationalRegime regime,
        StabilisationObservation observation)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(samples);
        ArgumentNullException.ThrowIfNull(observation);

        var selected = new List<RegimeScopedSample>();

        foreach (var sample in samples)
        {
            if (sample is null) continue;

            var classification = OperationalTransitionKernel.ClassifyInstant(
                registry, scopeKey, sample.Instant.Instant, observation);

            if (classification.IsDecided && classification.Regime == regime) selected.Add(sample);
        }

        return selected;
    }

    private static string Normalise(string? key) =>
        DeclaredKey.TryNormalise(key, out var normalised) ? normalised : string.Empty;

    private static PoolingAdmission Refuse(string code, int sampleCount) =>
        new(IsAdmitted: false,
            OperationalRegime.Unknown,
            sampleCount,
            code,
            TerminalState.RefusedByGuard,
            ExclusionAttribution.Declaration);
}