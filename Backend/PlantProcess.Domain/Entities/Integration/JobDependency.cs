using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Entities.Integration;

/// <summary>T-106. Chapter 5.3.6 names three edge kinds and no others.</summary>
public enum JobDependencyKind
{
    Data = 1,
    Schedule = 2,
    Resource = 3
}

/// <summary>
/// T-106. ONE EDGE OF THE CANONICAL JOB DEPENDENCY GRAPH.
///
/// The edge states that JobDefinitionId runs AFTER DependsOnJobDefinitionId.
/// The direction is fixed here and nowhere else, because a graph whose direction
/// is decided per caller is two graphs.
///
/// The corrective completes the governed fields Chapter 5.3.6 declares. They are
/// not decoration: is_required decides whether an unsatisfied upstream blocks or
/// is recorded as skipped, depends_on_version decides whether a pinned mismatch
/// blocks, and staleness_tolerance_minutes is the window inside which an
/// upstream result still counts as satisfied.
///
/// There is deliberately NO stale-permission field. The design names the
/// stale_accepted resolution but declares no column that grants it, and
/// inventing one would be manufacturing an authority the product does not have.
/// </summary>
public class JobDependency : BaseEntity
{
    public Guid JobDefinitionId { get; private set; }

    public Guid DependsOnJobDefinitionId { get; private set; }

    public JobDependencyKind DependencyKind { get; private set; } = JobDependencyKind.Data;

    public bool IsRequired { get; private set; } = true;

    /// <summary>Null means the upstream's current published version.</summary>
    public int? DependsOnVersion { get; private set; }

    /// <summary>Null means no tolerance is declared for this edge.</summary>
    public int? StalenessToleranceMinutes { get; private set; }

    private JobDependency()
    {
    }

    public JobDependency(
        Guid jobDefinitionId,
        Guid dependsOnJobDefinitionId,
        JobDependencyKind dependencyKind = JobDependencyKind.Data,
        bool isRequired = true,
        int? dependsOnVersion = null,
        int? stalenessToleranceMinutes = null)
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

        if (dependsOnVersion.HasValue && dependsOnVersion.Value <= 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(dependsOnVersion), "A pinned dependency version must be greater than zero.");
        }

        if (stalenessToleranceMinutes.HasValue && stalenessToleranceMinutes.Value < 0)
        {
            throw new ArgumentOutOfRangeException(
                nameof(stalenessToleranceMinutes), "A staleness tolerance cannot be negative.");
        }

        JobDefinitionId = jobDefinitionId;
        DependsOnJobDefinitionId = dependsOnJobDefinitionId;
        DependencyKind = dependencyKind;
        IsRequired = isRequired;
        DependsOnVersion = dependsOnVersion;
        StalenessToleranceMinutes = stalenessToleranceMinutes;
    }
}