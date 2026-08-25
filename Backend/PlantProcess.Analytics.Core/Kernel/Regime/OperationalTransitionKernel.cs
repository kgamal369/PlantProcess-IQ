// Operational Transition and Stabilisation kernel.
//
// Backlog origin: T-234.
//
// Classifies a scope's regime at an instant, or across a window, from declarations
// alone. It never infers a settling period, never treats an undeclared scope as steady,
// and never reports a transition as lost time.
//
// Every path out is a classification or a refusal carrying a code. A window that spans
// more than one regime is reported as Mixed under the code the validation fixture
// already names, so the downstream pooling guard and the fixture agree on one string.
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Analytics.Core.Kernel;

public static class OperationalTransitionKernel
{
    /// <summary>
    /// The regime of a scope at one instant.
    /// </summary>
    public static RegimeClassification ClassifyInstant(
        OperationalTransitionRegistry registry,
        string? scopeKey,
        DateTimeOffset at,
        StabilisationObservation observation)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(observation);

        if (!registry.IsScopeDeclared(scopeKey))
        {
            // Silence about a scope is silence. Answering Stable here would be the
            // inference this contract exists to refuse.
            return Refuse(OperationalTransitionCodes.ScopeNotDeclared);
        }

        var transitions = registry.TransitionsFor(scopeKey);

        var covering = transitions.FirstOrDefault(t => t.CoversInstant(at));

        if (covering is not null)
        {
            return Decide(OperationalRegime.Transition, covering);
        }

        // The most recent transition that has already ended is the only one whose
        // settling period could still be running.
        var previous = transitions
            .Where(t => t.End <= at)
            .OrderByDescending(t => t.End)
            .FirstOrDefault();

        if (previous is null)
        {
            return Decide(OperationalRegime.Stable, null);
        }

        switch (previous.StabilisationBasis)
        {
            case StabilisationBasis.None:
                return Decide(OperationalRegime.Stable, previous);

            case StabilisationBasis.Time:
                return at < previous.End + previous.StabilisationDuration
                    ? Decide(OperationalRegime.Stabilising, previous)
                    : Decide(OperationalRegime.Stable, previous);

            case StabilisationBasis.SubjectCount:
                if (observation.SubjectsCompletedSinceTransitionEnd < 0)
                {
                    return Refuse(OperationalTransitionCodes.StabilisationObservationNotSupplied);
                }

                return observation.SubjectsCompletedSinceTransitionEnd < previous.StabilisationSubjectCount
                    ? Decide(OperationalRegime.Stabilising, previous)
                    : Decide(OperationalRegime.Stable, previous);

            case StabilisationBasis.Condition:
                if (observation.DeclaredConditionSatisfied is null)
                {
                    // Whether the declared condition has been met is an observation the
                    // caller holds. An unsupplied outcome is not a satisfied one.
                    return Refuse(OperationalTransitionCodes.StabilisationObservationNotSupplied);
                }

                return observation.DeclaredConditionSatisfied.Value
                    ? Decide(OperationalRegime.Stable, previous)
                    : Decide(OperationalRegime.Stabilising, previous);

            default:
                return Refuse(OperationalTransitionCodes.StabilisationBasisNotDeclared);
        }
    }

    /// <summary>
    /// The regime across a window. Mixed means the window covers more than one regime,
    /// which is the condition under which pooling samples invalidates the interpretation.
    /// </summary>
    public static RegimeClassification ClassifyWindow(
        OperationalTransitionRegistry registry,
        string? scopeKey,
        DateTimeOffset from,
        DateTimeOffset to,
        StabilisationObservation observation)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(observation);

        if (to <= from) return Refuse(OperationalTransitionCodes.EmptyWindow);

        if (!registry.IsScopeDeclared(scopeKey)) return Refuse(OperationalTransitionCodes.ScopeNotDeclared);

        var boundaries = new SortedSet<DateTimeOffset> { from };

        foreach (var transition in registry.TransitionsFor(scopeKey))
        {
            foreach (var instant in Edges(transition))
            {
                if (instant > from && instant < to) boundaries.Add(instant);
            }
        }

        var regimes = new List<OperationalRegime>();

        foreach (var boundary in boundaries)
        {
            var classification = ClassifyInstant(registry, scopeKey, boundary, observation);

            if (!classification.IsDecided) return classification;

            regimes.Add(classification.Regime);
        }

        var distinct = regimes.Distinct().ToArray();

        if (distinct.Length == 1) return Decide(distinct[0], null);

        return new RegimeClassification(
            IsDecided: true,
            OperationalRegime.Mixed,
            Transition: null,
            OperationalTransitionCodes.MixedProcessRegime,
            TerminalState.Finding,
            ExclusionAttribution.None);
    }

    private static IEnumerable<DateTimeOffset> Edges(TransitionDeclaration transition)
    {
        yield return transition.Start;
        yield return transition.End;

        if (transition.StabilisationBasis == StabilisationBasis.Time)
        {
            yield return transition.End + transition.StabilisationDuration;
        }
    }

    private static RegimeClassification Decide(OperationalRegime regime, TransitionDeclaration? transition) =>
        new(IsDecided: true,
            regime,
            transition,
            OperationalTransitionCodes.RegimeClassified,
            TerminalState.Finding,
            ExclusionAttribution.None);

    private static RegimeClassification Refuse(string code) =>
        new(IsDecided: false,
            OperationalRegime.Unknown,
            Transition: null,
            code,
            TerminalState.RefusedByGuard,
            ExclusionAttribution.Declaration);
}