using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Jobs.Execution;

/// <summary>
/// T-106. THE ONE QUESTION THE ORCHESTRATOR ASKS ABOUT STORED JOBS.
///
/// Kept narrow for the same reason IJobTargetLookup is narrow: an orchestrator
/// that takes the whole DbContext cannot be falsified without a database, and
/// an execution-order guarantee that is never falsified is a comment. The
/// dependency ordering, the admission refusal and the chain-stops-on-failure
/// rule are all statements about orchestration, not about storage.
/// </summary>
public interface IRunnableJobLookup
{
    Task<JobDefinition?> FindAsync(Guid jobDefinitionId, CancellationToken cancellationToken);
}

public sealed class RunnableJobLookup : IRunnableJobLookup
{
    private readonly IPlantProcessDbContext _dbContext;

    public RunnableJobLookup(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<JobDefinition?> FindAsync(Guid jobDefinitionId, CancellationToken cancellationToken)
    {
        return await _dbContext.JobDefinitions
            .AsNoTracking()
            .FirstOrDefaultAsync(x => !x.IsDeleted && x.Id == jobDefinitionId, cancellationToken);
    }
}