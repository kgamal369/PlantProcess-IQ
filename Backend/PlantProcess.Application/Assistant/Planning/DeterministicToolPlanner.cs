using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;

namespace PlantProcess.Application.Assistant.Planning;

/// <summary>
/// T-179. THE DETERMINISTIC ASSISTANT TOOL PLANNER.
///
/// THE ORDER OF THE DECISION, AND WHY IT IS THIS ORDER.
///
///     permission  ->  ambiguity  ->  capability match  ->  canonical ordering
///
/// Permission is first because a tool the caller may not use must never appear in a
/// plan at all. Selecting it and trusting a later layer to strip it out would make the
/// plan itself a statement of what exists, which is a disclosure whether or not the
/// tool is ever run.
///
/// Ambiguity is second because a plan built on a guessed entity is worse than no plan.
/// If the resolver could not decide which unit the caller meant, the planner says so
/// and stops; it never takes the first candidate because it happened to sort first.
///
/// Capability match is exact, never generous. An intent requiring an exact value is
/// served only by an exact structured tool, and a Layer B intent is served only by a
/// tool declaring precisely the claim the intent requires. That is what prevents an
/// association being planned as a cause, a prediction as an observed fact, a similarity
/// search as an explanation, or a remediation candidate as an instruction.
///
/// Canonical ordering is last so that a registry declared in a different order produces
/// the identical plan.
///
/// THERE IS NO MODEL IN HERE. No prompt, no gateway, no embedding, no client of any
/// kind. Tool selection is a pure function of the declared inputs, which is what makes
/// it reproducible and auditable. An architecture test proves the absence rather than
/// this comment asserting it.
/// </summary>
public static class DeterministicToolPlanner
{
    /// <summary>Produce the plan. Pure: same request, same plan, always.</summary>
    public static ToolPlan Plan(PlanningRequest request)
    {
        ArgumentNullException.ThrowIfNull(request);

        var entities = request.Entities
            .OrderBy(e => e.Role, StringComparer.Ordinal)
            .ToImmutableArray();

        var clarifications = RequiredClarifications(request.Intent, entities);
        if (clarifications.Length > 0)
        {
            return new ToolPlan(
                PlanningOutcome.ClarificationRequired,
                ImmutableArray<string>.Empty,
                request.Intent,
                entities,
                request.Permission.TenantId,
                request.Permission.CallerRole,
                ImmutableArray<ToolDecision>.Empty,
                clarifications,
                "The request cannot be planned until "
                    + string.Join(", ", clarifications.Select(c => c.Role))
                    + " is resolved. No tool is planned on a guessed entity.");
        }

        var boundRoles = entities
            .Where(e => e.IsBound)
            .Select(e => e.Role)
            .ToImmutableHashSet(StringComparer.Ordinal);

        var decisions = new List<ToolDecision>();
        var selected = new List<DeclaredTool>();
        var selectedEquivalence = new HashSet<string>(StringComparer.Ordinal);

        foreach (var tool in request.Registry.Canonical())
        {
            // Permission first, and unconditionally. A tool the caller may not use is
            // not evaluated for fit, because even the reason for omitting it would
            // otherwise describe a capability the caller is not entitled to know about.
            if (!request.Permission.Permits(tool.ToolId))
            {
                decisions.Add(new ToolDecision(
                    tool.ToolId,
                    ToolDecisionCode.OmittedNotPermitted,
                    "The caller's tenant and role do not permit this tool, so it is not "
                        + "planned whatever its capability."));
                continue;
            }

            if (tool.ProvidesClaim != request.Intent.RequiredClaim)
            {
                decisions.Add(new ToolDecision(
                    tool.ToolId,
                    ToolDecisionCode.OmittedClaimMismatch,
                    $"The tool provides {tool.ProvidesClaim} and the resolved intent "
                        + $"requires {request.Intent.RequiredClaim}. A plan that used it "
                        + "would answer a different question from the one asked."));
                continue;
            }

            if (request.Intent.RequiresExactValue && tool.Exactness != ToolExactness.Exact)
            {
                decisions.Add(new ToolDecision(
                    tool.ToolId,
                    ToolDecisionCode.OmittedApproximateForExactValue,
                    "The intent requires an exact value and this tool estimates. An exact "
                        + "question answered by an estimate is wrong even when the estimate "
                        + "is close."));
                continue;
            }

            var missing = tool.RequiredEntityRoles
                .Where(role => !boundRoles.Contains(role))
                .ToArray();

            if (missing.Length > 0)
            {
                decisions.Add(new ToolDecision(
                    tool.ToolId,
                    ToolDecisionCode.OmittedMissingRequiredEntity,
                    "The tool needs entity role(s) the request does not bind: "
                        + string.Join(", ", missing) + "."));
                continue;
            }

            var equivalence = tool.EquivalenceKey();
            if (!selectedEquivalence.Add(equivalence))
            {
                decisions.Add(new ToolDecision(
                    tool.ToolId,
                    ToolDecisionCode.OmittedEquivalentAlreadySelected,
                    "An equivalent tool is already planned. Running the same work twice "
                        + "under two identifiers would read as corroboration."));
                continue;
            }

            selected.Add(tool);
            decisions.Add(new ToolDecision(
                tool.ToolId,
                ToolDecisionCode.Selected,
                $"The tool provides {tool.ProvidesClaim} in {tool.Layer} at "
                    + $"{tool.Exactness} exactness, which is what the resolved intent "
                    + "requires, and the caller is permitted to use it."));
        }

        var orderedDecisions = decisions
            .OrderBy(d => d.ToolId, StringComparer.Ordinal)
            .ToImmutableArray();

        if (selected.Count == 0)
        {
            return new ToolPlan(
                PlanningOutcome.Unsupported,
                ImmutableArray<string>.Empty,
                request.Intent,
                entities,
                request.Permission.TenantId,
                request.Permission.CallerRole,
                orderedDecisions,
                ImmutableArray<ClarificationRequirement>.Empty,
                $"No permitted declared tool supports intent '{request.Intent.IntentCode}'. "
                    + "No generic fallback is planned, because a tool that does not "
                    + "support the request would produce an answer to a different one.");
        }

        var orderedSelection = selected
            .OrderBy(t => StageOrdinal(t), Comparer<int>.Default)
            .ThenBy(t => t.ToolId, StringComparer.Ordinal)
            .Select(t => t.ToolId)
            .ToImmutableArray();

        return new ToolPlan(
            PlanningOutcome.Planned,
            orderedSelection,
            request.Intent,
            entities,
            request.Permission.TenantId,
            request.Permission.CallerRole,
            orderedDecisions,
            ImmutableArray<ClarificationRequirement>.Empty,
            $"{orderedSelection.Length} permitted tool(s) match intent "
                + $"'{request.Intent.IntentCode}' exactly.");
    }

    /// <summary>
    /// Exact structured tools are planned before intelligence tools.
    ///
    /// Where both are permitted and both match, the caller sees the value before the
    /// estimate about it. This is an ordering rule and not a filter: it does not remove
    /// anything, and the exactness filter above is what refuses an estimate for an
    /// exact question.
    /// </summary>
    private static int StageOrdinal(DeclaredTool tool) =>
        tool switch
        {
            { Layer: ToolLayer.LayerA, Exactness: ToolExactness.Exact } => 0,
            { Layer: ToolLayer.LayerA } => 1,
            _ => 2
        };

    /// <summary>
    /// Which required roles the caller must disambiguate.
    ///
    /// A role the intent requires is a clarification when it is missing, and a
    /// clarification when the resolver offered several candidates. Both are reported
    /// with the candidates, so the caller is asked a question they can answer.
    /// </summary>
    private static ImmutableArray<ClarificationRequirement> RequiredClarifications(
        ResolvedIntent intent,
        ImmutableArray<ResolvedEntity> entities)
    {
        var byRole = entities.ToDictionary(e => e.Role, e => e, StringComparer.Ordinal);
        var required = new List<ClarificationRequirement>();

        foreach (var role in intent.RequiredEntityRoles)
        {
            if (!byRole.TryGetValue(role, out var entity) || entity is null)
            {
                required.Add(new ClarificationRequirement(
                    role,
                    ImmutableArray<string>.Empty,
                    $"The intent requires the role '{role}' and the request binds no "
                        + "entity to it."));
                continue;
            }

            if (entity.IsBound)
            {
                continue;
            }

            required.Add(new ClarificationRequirement(
                role,
                entity.Candidates,
                entity.Candidates.Length > 1
                    ? $"The role '{role}' resolved to {entity.Candidates.Length} candidates "
                        + "and the planner does not choose between them."
                    : $"The role '{role}' could not be resolved to a canonical entity."));
        }

        return required
            .OrderBy(c => c.Role, StringComparer.Ordinal)
            .ToImmutableArray();
    }
}
