using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Relationships;

/// <summary>
/// T-058. The first real consumer of the resolver.
///
/// It takes a resolved path and produces the ON clauses a query compiler would
/// execute. It reaches the relationship model ONLY through the resolver and the
/// relationship service; it has no idea where relationships are stored, and a
/// consumer that reached past them into storage would be the defect this whole
/// vertical exists to prevent.
///
/// A refusal from the resolver is carried through unchanged, code and all. The
/// planner never works around one, and never emits a partial plan: half a join
/// still runs, and still returns numbers that look like an answer.
/// </summary>
public sealed class RelationshipJoinPlanner : IRelationshipJoinPlanner
{
    private readonly IRelationshipResolver _resolver;
    private readonly IRelationshipService _relationships;

    public RelationshipJoinPlanner(IRelationshipResolver resolver, IRelationshipService relationships)
    {
        _resolver = resolver;
        _relationships = relationships;
    }

    public async Task<ApplicationResult<RelationshipJoinPlanDto>> PlanAsync(
        string fromEntity, string toEntity, string purpose, CancellationToken cancellationToken)
    {
        var resolution = await _resolver.ResolveAsync(fromEntity, toEntity, purpose, cancellationToken);
        if (resolution.IsFailure)
            return ApplicationResult<RelationshipJoinPlanDto>.Failure(resolution.Error!);

        var resolved = resolution.Value!;

        if (!resolved.Resolved)
        {
            return Ok(new RelationshipJoinPlanDto(
                resolved.FromEntity, resolved.ToEntity, resolved.Purpose,
                false, Array.Empty<RelationshipJoinStepDto>(), false, false,
                resolved.RefusalCode, resolved.RefusalMessage, resolved.CandidatePaths));
        }

        var steps = new List<RelationshipJoinStepDto>(resolved.Path.Count);

        foreach (var step in resolved.Path)
        {
            var lookup = await _relationships.GetByIdAsync(step.RelationshipId, cancellationToken);
            if (lookup.IsFailure)
            {
                // The relationship was retired between resolving and planning.
                // That is a refusal, not a plan with a hole in it.
                return Ok(new RelationshipJoinPlanDto(
                    resolved.FromEntity, resolved.ToEntity, resolved.Purpose,
                    false, Array.Empty<RelationshipJoinStepDto>(), false, false,
                    RelationshipRefusalCodes.NoPath,
                    $"A relationship on the resolved path is no longer published, so the path from '{resolved.FromEntity}' to '{resolved.ToEntity}' cannot be planned.",
                    resolved.CandidatePaths));
            }

            steps.Add(BuildStep(lookup.Value!, step));
        }

        var crossesGrain = steps.Any(s => s.CrossesGrain);

        return Ok(new RelationshipJoinPlanDto(
            resolved.FromEntity, resolved.ToEntity, resolved.Purpose,
            true, steps, crossesGrain, crossesGrain,
            null, null, resolved.CandidatePaths));
    }

    /// <summary>
    /// Orients the declared key members for the direction this step travels.
    ///
    /// A relationship is declared once, left to right. When the plan traverses
    /// it right to left the columns must swap: keeping the declared order would
    /// compare the left key to the left key and join on nothing meaningful,
    /// while still producing rows.
    /// </summary>
    private static RelationshipJoinStepDto BuildStep(RelationshipDto relationship, RelationshipPathStepDto step)
    {
        var travellingForward = string.Equals(step.FromEntity, relationship.LeftEntity, StringComparison.Ordinal);

        var predicates = relationship.Members
            .OrderBy(m => m.MemberOrder)
            .Select(m => new RelationshipJoinPredicateDto(
                travellingForward ? m.LeftColumn : m.RightColumn,
                travellingForward ? m.RightColumn : m.LeftColumn,
                string.IsNullOrWhiteSpace(m.Comparison) ? "=" : m.Comparison,
                m.MemberOrder))
            .ToList();

        return new RelationshipJoinStepDto(
            relationship.Id,
            relationship.RelationshipCode,
            step.FromEntity,
            step.ToEntity,
            relationship.JoinType,
            relationship.Cardinality,
            relationship.IsGrainConverting,
            relationship.AttributionRule,
            predicates);
    }

    private static ApplicationResult<RelationshipJoinPlanDto> Ok(RelationshipJoinPlanDto dto) =>
        ApplicationResult<RelationshipJoinPlanDto>.Success(dto);
}