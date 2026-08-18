using PlantProcess.Application.Common.Results;

namespace PlantProcess.Application.Relationships;

/// <summary>
/// T-058. The single authority that answers "how do these two entities join".
///
/// It reads the published relationship model through the relationship SERVICE.
/// It has no knowledge of where relationships are stored, which is what lets
/// T-095 move them without touching a consumer.
/// </summary>
public interface IRelationshipResolver
{
    Task<ApplicationResult<RelationshipResolutionDto>> ResolveAsync(
        string fromEntity, string toEntity, string purpose, CancellationToken cancellationToken);
}

/// <summary>
/// T-058. The first real consumer of the resolver: it turns a resolved path
/// into something a query compiler can execute, or refuses with the resolver's
/// own reason.
///
/// It never re-derives a join. If the resolver refuses, the plan refuses with
/// the same code, because a consumer that works around a refusal is a consumer
/// that produces an unexplainable number.
/// </summary>
public interface IRelationshipJoinPlanner
{
    Task<ApplicationResult<RelationshipJoinPlanDto>> PlanAsync(
        string fromEntity, string toEntity, string purpose, CancellationToken cancellationToken);
}