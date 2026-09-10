using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Integration;

/// <summary>
/// T-106. ONE EDGE OF THE CANONICAL JOB DEPENDENCY GRAPH.
///
/// The edge states that <see cref="JobDefinitionId"/> runs AFTER
/// <see cref="DependsOnJobDefinitionId"/>. The direction is fixed here and
/// nowhere else, because a graph whose direction is decided per caller is two
/// graphs.
///
/// Only the structural refusal lives on the entity: an edge from a job to
/// itself is not a dependency anybody could satisfy. Whether an edge would
/// close a cycle is a question about the whole graph, not about one row, and
/// belongs to the service and to the database trigger that back it.
/// </summary>
public class JobDependency : BaseEntity
{
    public Guid JobDefinitionId { get; private set; }

    public Guid DependsOnJobDefinitionId { get; private set; }

    private JobDependency()
    {
    }

    public JobDependency(Guid jobDefinitionId, Guid dependsOnJobDefinitionId)
    {
        if (jobDefinitionId == Guid.Empty)
        {
            throw new ArgumentException("A dependency must name the dependent job.", nameof(jobDefinitionId));
        }

        if (dependsOnJobDefinitionId == Guid.Empty)
        {
            throw new ArgumentException("A dependency must name the predecessor job.", nameof(dependsOnJobDefinitionId));
        }

        if (jobDefinitionId == dependsOnJobDefinitionId)
        {
            throw new ArgumentException("A job cannot depend on itself.", nameof(dependsOnJobDefinitionId));
        }

        JobDefinitionId = jobDefinitionId;
        DependsOnJobDefinitionId = dependsOnJobDefinitionId;
    }
}