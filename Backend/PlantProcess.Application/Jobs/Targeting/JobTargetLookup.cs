using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;

namespace PlantProcess.Application.Jobs.Targeting;

/// <summary>
/// T-064. The one question the resolver asks about stored jobs, kept narrow on
/// purpose: a resolver that took the whole DbContext could not be falsified
/// without a database, and a JB04 refusal that is never tested is a comment.
/// </summary>
public interface IJobTargetLookup
{
    Task<IReadOnlyList<string>> JobCodesTargetingAsync(
        string targetDefinitionKind,
        Guid definitionId,
        CancellationToken cancellationToken);
}

public sealed class JobTargetLookup : IJobTargetLookup
{
    private readonly IPlantProcessDbContext _dbContext;

    public JobTargetLookup(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<IReadOnlyList<string>> JobCodesTargetingAsync(
        string targetDefinitionKind,
        Guid definitionId,
        CancellationToken cancellationToken)
    {
        return await _dbContext.JobDefinitions
            .AsNoTracking()
            .Where(x => !x.IsDeleted
                        && x.TargetDefinitionId == definitionId
                        && x.TargetDefinitionKind == targetDefinitionKind)
            .Select(x => x.JobCode)
            .OrderBy(x => x)
            .ToListAsync(cancellationToken);
    }
}
