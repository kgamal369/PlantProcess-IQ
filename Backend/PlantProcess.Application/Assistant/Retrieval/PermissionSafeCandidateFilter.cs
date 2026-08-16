using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using PlantProcess.Application.Assistant.Planning;

namespace PlantProcess.Application.Assistant.Retrieval;

/// <summary>
/// T-180. PERMISSION, BEFORE ANYTHING ELSE.
///
/// This is the only place a <see cref="PermittedCandidateSet"/> can be made, and every
/// ranking, scoring, packing and counting path requires one. A caller cannot rank an
/// unfiltered pool by forgetting to filter it, because there is no method that would
/// accept the pool.
///
/// WHY NOT FILTER AFTERWARDS. It is tempting to retrieve broadly, rank, and drop the
/// forbidden rows before display. Every one of these leaks if you do:
///
///     scores          a normalised score reveals what it was normalised against
///     counts          "showing 3 of 40" reveals 37 rows the caller may not see
///     ordering        a gap in a ranked list is itself information
///     truncation      "more exists" is a claim about the forbidden set
///     fingerprints    a hash over the pool changes when the hidden part changes
///
/// None of those require a forbidden row to be displayed. They require only that it
/// was in the pool.
///
/// THREE CHECKS, ALL OF THEM STRUCTURAL.
///
///     tenant      evidence belonging to another tenant is not this tenant's to rank
///     tool        the plan names which tools this caller may use; nothing else counts
///     entity      evidence outside the resolved entity scope was not asked for
/// </summary>
public static class PermissionSafeCandidateFilter
{
    /// <summary>
    /// Reduce raw candidates to the ones this plan and this caller may see.
    ///
    /// The plan is the authority for which tools were permitted, and it was itself
    /// built permission-first by T-179. This layer does not re-derive permission, add
    /// a tool the plan omitted, or broaden scope because retrieval came back sparse.
    /// </summary>
    public static PermittedCandidateSet Filter(
        ToolPlan plan,
        IEnumerable<EvidenceCandidate> rawCandidates)
    {
        ArgumentNullException.ThrowIfNull(plan);
        ArgumentNullException.ThrowIfNull(rawCandidates);

        var permittedTools = plan.SelectedToolIds.ToImmutableHashSet(StringComparer.Ordinal);
        var entityScope = plan.Entities
            .Where(e => e.IsBound)
            .Select(e => e.CanonicalId!)
            .ToImmutableHashSet(StringComparer.Ordinal);

        var permitted = new List<EvidenceCandidate>();
        var rejected = 0;

        foreach (var candidate in rawCandidates)
        {
            if (!string.Equals(candidate.TenantId, plan.TenantId, StringComparison.Ordinal))
            {
                rejected++;
                continue;
            }

            if (!permittedTools.Contains(candidate.ToolId))
            {
                rejected++;
                continue;
            }

            if (!WithinEntityScope(candidate, entityScope))
            {
                rejected++;
                continue;
            }

            permitted.Add(candidate);
        }

        // Canonical order at the boundary, so nothing downstream can depend on the
        // order the producer happened to yield.
        var ordered = permitted
            .OrderBy(c => c.EvidenceHandle, StringComparer.Ordinal)
            .ToImmutableArray();

        return new PermittedCandidateSet(ordered, rejected);
    }

    /// <summary>
    /// Evidence declaring no entity scope is scope-neutral and passes.
    ///
    /// A structured tool result about the whole request carries no per-entity scope,
    /// and refusing it would make the scope check delete exactly the strongest
    /// evidence. Evidence that does declare a scope must intersect the resolved one.
    /// </summary>
    private static bool WithinEntityScope(
        EvidenceCandidate candidate,
        ImmutableHashSet<string> resolvedScope)
    {
        if (candidate.EntityScope.IsEmpty)
        {
            return true;
        }

        return candidate.EntityScope.Any(resolvedScope.Contains);
    }

    /// <summary>
    /// Whether this layer may execute the plan at all.
    ///
    /// A plan that asked for clarification, refused as unsupported, or selected no
    /// tool is not repaired here. Executing it anyway would mean retrieving evidence
    /// for a question nobody established, which is the failure T-179 exists to
    /// prevent, undone one layer later.
    /// </summary>
    public static bool IsExecutable(ToolPlan plan)
    {
        ArgumentNullException.ThrowIfNull(plan);
        return plan.Outcome == PlanningOutcome.Planned && !plan.SelectedToolIds.IsEmpty;
    }
}
