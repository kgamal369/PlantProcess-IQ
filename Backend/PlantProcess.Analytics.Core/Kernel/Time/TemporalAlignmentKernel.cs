// Temporal Alignment kernel.
//
// Backlog origin: T-217.
//
// Decides whether evidence instants may be treated as the same moment, under a declared
// tolerance. It computes bounds, not guesses: the separation two instants could have
// ranges between a minimum and a maximum given what is uncertain about each, and only a
// bound that lies wholly on one side of the tolerance decides the question.
//
// Every path out of this file is a decided verdict with its separation bounds, or a
// refusal carrying a code. There is no path that resolves uncertainty by choosing.
using System;
using System.Collections.Generic;
using System.Linq;

namespace PlantProcess.Analytics.Core.Kernel;

public static class TemporalAlignmentKernel
{
    /// <summary>
    /// Align two instants under a declared policy.
    /// </summary>
    public static TemporalAlignmentVerdict Align(
        TemporalAlignmentPolicyRegistry registry,
        string? policyKey,
        TemporalInstant first,
        TemporalInstant second)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        return Align(registry, policyKey, new[] { first, second });
    }

    /// <summary>
    /// Align a set of instants under a declared policy. The set is coincident only if
    /// every pair is; one provably separated pair separates the set, because instants
    /// that cannot all be the same moment are not aligned however close the rest are.
    /// </summary>
    public static TemporalAlignmentVerdict Align(
        TemporalAlignmentPolicyRegistry registry,
        string? policyKey,
        IReadOnlyList<TemporalInstant> instants)
    {
        ArgumentNullException.ThrowIfNull(registry);
        ArgumentNullException.ThrowIfNull(instants);

        if (!registry.TryGetPolicy(policyKey, out var policy) || policy is null)
        {
            return Refuse(TemporalAlignmentCodes.PolicyNotDeclared);
        }

        if (instants.Count < 2 || instants.Any(i => i is null))
        {
            return Refuse(TemporalAlignmentCodes.InsufficientInstants);
        }

        // Aligning an event time against an arrival time is a category error, not a
        // close call. The roles were kept distinct upstream precisely so that this
        // comparison cannot be made by accident.
        if (instants.Select(i => i.Role).Distinct().Count() > 1)
        {
            return Refuse(TemporalAlignmentCodes.IncomparableTimeRoles);
        }

        var worst = new TemporalSeparation(TimeSpan.Zero, TimeSpan.Zero);
        var anySeparated = false;
        var anyIndeterminate = false;

        for (var i = 0; i < instants.Count - 1; i++)
        {
            for (var j = i + 1; j < instants.Count; j++)
            {
                var separation = Separation(instants[i], instants[j]);

                if (separation.Maximum > worst.Maximum) worst = separation;

                if (separation.Minimum > policy.Tolerance) anySeparated = true;
                else if (separation.Maximum > policy.Tolerance) anyIndeterminate = true;
            }
        }

        if (anySeparated) return Decide(TemporalAlignment.Separated, TemporalAlignmentCodes.Separated, worst);
        if (anyIndeterminate) return Decide(TemporalAlignment.Indeterminate, TemporalAlignmentCodes.Indeterminate, worst);

        return Decide(TemporalAlignment.Coincident, TemporalAlignmentCodes.Coincident, worst);
    }

    /// <summary>
    /// The range of separations two instants could have. Minimum is zero when their
    /// uncertainty intervals overlap: they could be the same moment.
    /// </summary>
    public static TemporalSeparation Separation(TemporalInstant first, TemporalInstant second)
    {
        ArgumentNullException.ThrowIfNull(first);
        ArgumentNullException.ThrowIfNull(second);

        var gap = first.Instant - second.Instant;
        if (gap < TimeSpan.Zero) gap = gap.Negate();

        var combined = first.Uncertainty + second.Uncertainty;

        var minimum = gap - combined;
        if (minimum < TimeSpan.Zero) minimum = TimeSpan.Zero;

        return new TemporalSeparation(minimum, gap + combined);
    }

    private static TemporalAlignmentVerdict Decide(
        TemporalAlignment alignment,
        string code,
        TemporalSeparation separation) =>
        new(IsDecided: true,
            alignment,
            separation,
            code,
            // Indeterminate is a reported outcome, not a refusal: the evidence was
            // admissible, and what it supports is "cannot tell from this".
            TerminalState.Finding,
            ExclusionAttribution.None);

    private static TemporalAlignmentVerdict Refuse(string code) =>
        new(IsDecided: false,
            TemporalAlignment.Indeterminate,
            Separation: null,
            code,
            TerminalState.RefusedByGuard,
            ExclusionAttribution.Declaration);
}