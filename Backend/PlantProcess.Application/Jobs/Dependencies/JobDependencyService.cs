using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Jobs.Dependencies;

/// <summary>
/// T-106. The one canonical dependency graph, read and written in one place.
/// There is no second scheduler and no second registry: this reads the same
/// job_definitions rows the orchestrator runs.
/// </summary>
public interface IJobDependencyService
{
    Task<ApplicationResult> AddDependencyAsync(
        Guid jobDefinitionId,
        Guid dependsOnJobDefinitionId,
        CancellationToken cancellationToken);

    Task<ApplicationResult> RemoveDependencyAsync(
        Guid jobDefinitionId,
        Guid dependsOnJobDefinitionId,
        CancellationToken cancellationToken);

    Task<ApplicationResult<IReadOnlyList<JobDependencyEdge>>> ListEdgesAsync(
        CancellationToken cancellationToken);

    /// <summary>
    /// The jobs that must run, predecessors first, for the given job to run
    /// lawfully. The given job is the last element on success.
    /// </summary>
    Task<ApplicationResult<IReadOnlyList<Guid>>> ResolveExecutionOrderAsync(
        Guid jobDefinitionId,
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
        CancellationToken cancellationToken)
    {
        ApplicationResult dependent = await AssertJobExistsAsync(jobDefinitionId, cancellationToken);
        if (dependent.IsFailure)
        {
            return dependent;
        }

        ApplicationResult predecessor = await AssertJobExistsAsync(dependsOnJobDefinitionId, cancellationToken);
        if (predecessor.IsFailure)
        {
            return predecessor;
        }

        IReadOnlyList<JobDependencyEdge> existing = await LoadEdgesAsync(cancellationToken);

        var candidate = new JobDependencyEdge(jobDefinitionId, dependsOnJobDefinitionId);

        // Refused HERE, at save time. The database trigger says the same thing
        // for a writer that is not this application; neither is the other's
        // fallback, and both refuse before anything is scheduled.
        ApplicationResult lawful = JobDependencyGraph.ValidateNewEdge(candidate, existing);
        if (lawful.IsFailure)
        {
            return lawful;
        }

        _dbContext.JobDependencies.Add(new JobDependency(jobDefinitionId, dependsOnJobDefinitionId));
        await _dbContext.SaveChangesAsync(cancellationToken);

        return ApplicationResult.Success();
    }

    public async Task<ApplicationResult> RemoveDependencyAsync(
        Guid jobDefinitionId,
        Guid dependsOnJobDefinitionId,
        CancellationToken cancellationToken)
    {
        JobDependency? row = await _dbContext.JobDependencies
            .FirstOrDefaultAsync(
                x => !x.IsDeleted
                     && x.JobDefinitionId == jobDefinitionId
                     && x.DependsOnJobDefinitionId == dependsOnJobDefinitionId,
                cancellationToken);

        if (row is null)
        {
            return ApplicationResult.Failure(
                ApplicationError.NotFound(
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
        Guid jobDefinitionId,
        CancellationToken cancellationToken)
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

    private async Task<IReadOnlyList<JobDependencyEdge>> LoadEdgesAsync(CancellationToken cancellationToken)
    {
        List<JobDependencyEdge> edges = await _dbContext.JobDependencies
            .AsNoTracking()
            .Where(x => !x.IsDeleted)
            .OrderBy(x => x.JobDefinitionId)
            .ThenBy(x => x.DependsOnJobDefinitionId)
            .Select(x => new JobDependencyEdge(x.JobDefinitionId, x.DependsOnJobDefinitionId))
            .ToListAsync(cancellationToken);

        return edges;
    }

    private async Task<ApplicationResult> AssertJobExistsAsync(
        Guid jobDefinitionId,
        CancellationToken cancellationToken)
    {
        bool present = await _dbContext.JobDefinitions
            .AsNoTracking()
            .AnyAsync(x => !x.IsDeleted && x.Id == jobDefinitionId, cancellationToken);

        if (!present)
        {
            return ApplicationResult.Failure(JobDependencyErrors.UnknownJobInDependency(jobDefinitionId));
        }

        return ApplicationResult.Success();
    }
}