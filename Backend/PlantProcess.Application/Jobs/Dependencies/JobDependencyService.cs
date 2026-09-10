using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Jobs.Dependencies;

/// <summary>T-106. What one job's run produced, for edges that point at it.</summary>
public sealed record JobRunSnapshot(Guid RunId, JobRunStatus Status, int? TargetDefinitionVersion);

public interface IJobDependencyService
{
    Task<ApplicationResult> AddDependencyAsync(
        Guid jobDefinitionId,
        Guid dependsOnJobDefinitionId,
        JobDependencyKind dependencyKind,
        bool isRequired,
        int? dependsOnVersion,
        int? stalenessToleranceMinutes,
        CancellationToken cancellationToken);

    Task<ApplicationResult> RemoveDependencyAsync(
        Guid jobDefinitionId,
        Guid dependsOnJobDefinitionId,
        CancellationToken cancellationToken);

    Task<ApplicationResult<IReadOnlyList<JobDependencyEdge>>> ListEdgesAsync(
        CancellationToken cancellationToken);

    Task<ApplicationResult<IReadOnlyList<Guid>>> ResolveExecutionOrderAsync(
        Guid jobDefinitionId,
        CancellationToken cancellationToken);

    /// <summary>
    /// T-106. Resolves every edge pointing out of one job, using the runs this
    /// chain has already produced first and the persisted history otherwise.
    /// </summary>
    Task<ApplicationResult<IReadOnlyList<JobDependencyOutcome>>> EvaluateAsync(
        Guid jobDefinitionId,
        IReadOnlyDictionary<Guid, JobRunSnapshot> chainRuns,
        CancellationToken cancellationToken);

    /// <summary>Persists the evidence against a REAL downstream run identity.</summary>
    Task<ApplicationResult> RecordResolutionsAsync(
        Guid runId,
        Guid jobDefinitionId,
        IReadOnlyList<JobDependencyOutcome> outcomes,
        CancellationToken cancellationToken);
}

public sealed class JobDependencyService : IJobDependencyService
{
    private readonly IPlantProcessDbContext _dbContext;

    public JobDependencyService(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<ApplicationResult> AddDependencyAsync(
        Guid jobDefinitionId,
        Guid dependsOnJobDefinitionId,
        JobDependencyKind dependencyKind,
        bool isRequired,
        int? dependsOnVersion,
        int? stalenessToleranceMinutes,
        CancellationToken cancellationToken)
    {
        ApplicationResult dependent = await AssertJobExistsAsync(jobDefinitionId, cancellationToken);
        if (dependent.IsFailure) { return dependent; }

        ApplicationResult predecessor = await AssertJobExistsAsync(dependsOnJobDefinitionId, cancellationToken);
        if (predecessor.IsFailure) { return predecessor; }

        IReadOnlyList<JobDependencyEdge> existing = await LoadEdgesAsync(cancellationToken);
        var candidate = new JobDependencyEdge(jobDefinitionId, dependsOnJobDefinitionId);

        ApplicationResult lawful = JobDependencyGraph.ValidateNewEdge(candidate, existing);
        if (lawful.IsFailure) { return lawful; }

        _dbContext.JobDependencies.Add(new JobDependency(
            jobDefinitionId, dependsOnJobDefinitionId, dependencyKind,
            isRequired, dependsOnVersion, stalenessToleranceMinutes));

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult> RemoveDependencyAsync(
        Guid jobDefinitionId, Guid dependsOnJobDefinitionId, CancellationToken cancellationToken)
    {
        JobDependency? row = await _dbContext.JobDependencies
            .FirstOrDefaultAsync(
                x => !x.IsDeleted
                     && x.JobDefinitionId == jobDefinitionId
                     && x.DependsOnJobDefinitionId == dependsOnJobDefinitionId,
                cancellationToken);

        if (row is null)
        {
            return ApplicationResult.Failure(ApplicationError.NotFound(
                "Job " + jobDefinitionId + " does not depend on job " + dependsOnJobDefinitionId + "."));
        }

        row.SoftDelete("Dependency removed.");
        await _dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult<IReadOnlyList<JobDependencyEdge>>> ListEdgesAsync(
        CancellationToken cancellationToken)
    {
        IReadOnlyList<JobDependencyEdge> edges = await LoadEdgesAsync(cancellationToken);
        return ApplicationResult<IReadOnlyList<JobDependencyEdge>>.Success(edges);
    }

    public async Task<ApplicationResult<IReadOnlyList<Guid>>> ResolveExecutionOrderAsync(
        Guid jobDefinitionId, CancellationToken cancellationToken)
    {
        ApplicationResult exists = await AssertJobExistsAsync(jobDefinitionId, cancellationToken);
        if (exists.IsFailure)
        {
            return ApplicationResult<IReadOnlyList<Guid>>.Failure(exists.Error!);
        }

        IReadOnlyList<JobDependencyEdge> edges = await LoadEdgesAsync(cancellationToken);
        IReadOnlyList<Guid> closure = JobDependencyGraph.DependencyClosure(jobDefinitionId, edges);

        return JobDependencyGraph.TopologicalOrder(closure, edges);
    }

    public async Task<ApplicationResult<IReadOnlyList<JobDependencyOutcome>>> EvaluateAsync(
        Guid jobDefinitionId,
        IReadOnlyDictionary<Guid, JobRunSnapshot> chainRuns,
        CancellationToken cancellationToken)
    {
        List<JobDependency> edges = await _dbContext.JobDependencies
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.JobDefinitionId == jobDefinitionId)
            .OrderBy(x => x.DependsOnJobDefinitionId)
            .ToListAsync(cancellationToken);

        var outcomes = new List<JobDependencyOutcome>();

        foreach (JobDependency edge in edges)
        {
            JobRunSnapshot? upstream = null;

            if (chainRuns.TryGetValue(edge.DependsOnJobDefinitionId, out JobRunSnapshot? fromChain))
            {
                // The action DTO intentionally does not duplicate the resolved target
                // version. Recover it from the real run-history identity rather than
                // filling the snapshot with null and accidentally turning every pinned
                // dependency into a mismatch.
                int? actualVersion = await _dbContext.JobRunHistories
                    .AsNoTracking()
                    .Where(x => !x.IsDeleted && x.Id == fromChain.RunId)
                    .Select(x => x.TargetDefinitionVersion)
                    .FirstOrDefaultAsync(cancellationToken);

                upstream = fromChain with { TargetDefinitionVersion = actualVersion };
            }
            else
            {
                upstream = await LatestRunAsync(edge.DependsOnJobDefinitionId, cancellationToken);
            }

            outcomes.Add(JobDependencyEvaluator.Evaluate(
                edge.DependsOnJobDefinitionId,
                edge.IsRequired,
                edge.DependsOnVersion,
                upstream?.RunId,
                upstream?.Status,
                upstream?.TargetDefinitionVersion));
        }

        return ApplicationResult<IReadOnlyList<JobDependencyOutcome>>.Success(outcomes);
    }

    public async Task<ApplicationResult> RecordResolutionsAsync(
        Guid runId,
        Guid jobDefinitionId,
        IReadOnlyList<JobDependencyOutcome> outcomes,
        CancellationToken cancellationToken)
    {
        if (runId == Guid.Empty)
        {
            return ApplicationResult.Failure(ApplicationError.Validation(
                "Dependency evidence requires a real downstream run identity."));
        }

        foreach (JobDependencyOutcome outcome in outcomes)
        {
            _dbContext.JobRunDependencies.Add(new JobRunDependency(
                runId,
                outcome.DependsOnRunId,
                jobDefinitionId,
                outcome.DependsOnJobDefinitionId,
                outcome.Resolution,
                outcome.ExpectedVersion,
                outcome.ActualVersion,
                outcome.Reason));
        }

        await _dbContext.SaveChangesAsync(cancellationToken);
        return ApplicationResult.Success();
    }

    private async Task<JobRunSnapshot?> LatestRunAsync(Guid jobDefinitionId, CancellationToken cancellationToken)
    {
        JobRunHistory? latest = await _dbContext.JobRunHistories
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.JobDefinitionId == jobDefinitionId)
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return latest is null
            ? null
            : new JobRunSnapshot(latest.Id, latest.Status, latest.TargetDefinitionVersion);
    }

    private async Task<IReadOnlyList<JobDependencyEdge>> LoadEdgesAsync(CancellationToken cancellationToken)
    {
        return await _dbContext.JobDependencies
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.JobDefinitionId)
            .ThenBy(x => x.DependsOnJobDefinitionId)
            .Select(x => new JobDependencyEdge(x.JobDefinitionId, x.DependsOnJobDefinitionId))
            .ToListAsync(cancellationToken);
    }

    private async Task<ApplicationResult> AssertJobExistsAsync(Guid jobDefinitionId, CancellationToken cancellationToken)
    {
        bool present = await _dbContext.JobDefinitions
            .AsNoTracking()
            .AnyAsync(x => !x.IsDeleted && x.Id == jobDefinitionId, cancellationToken);

        return present
            ? ApplicationResult.Success()
            : ApplicationResult.Failure(JobDependencyErrors.UnknownJobInDependency(jobDefinitionId));
    }
}