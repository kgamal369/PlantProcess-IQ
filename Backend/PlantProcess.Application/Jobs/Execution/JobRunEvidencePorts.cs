using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Jobs.Execution;

/// <summary>
/// THE ONE STORE FOR PER-BLOCK RUNTIME EVIDENCE.
///
/// JobRunHistory owns the run verdict; this owns the child grain, keyed by the genuine
/// run identity and the stable authored block id. It is not a second run history.
/// </summary>
public interface IJobRunBlockEvidenceStore
{
    Task AddAsync(IReadOnlyList<JobRunBlockEvidence> evidence, CancellationToken cancellationToken);

    Task SaveAsync(CancellationToken cancellationToken);

    /// <summary>
    /// Discards changes to one evidence row that were never persisted, restoring it to
    /// what the database actually holds.
    ///
    /// IN-MEMORY STATUS IS NOT DURABLE TRUTH. A block marked Succeeded whose save then
    /// failed has not succeeded, and the entity refuses to leave a terminal state, so the
    /// only honest way to record the real outcome is to go back to the persisted row
    /// first and mark the failure against that.
    /// </summary>
    Task DiscardAsync(JobRunBlockEvidence evidence, CancellationToken cancellationToken);

    Task<IReadOnlyList<JobRunBlockEvidence>> ListAsync(Guid jobRunHistoryId, CancellationToken cancellationToken);
}

/// <summary>
/// Whether an operator has asked a run to stop. The executor asks this at every governed
/// boundary; the answer is read from the persisted request, not from a tracked copy that
/// another request could have made stale.
/// </summary>
public interface IJobRunCancellationProbe
{
    Task<bool> IsRequestedAsync(Guid jobRunHistoryId, CancellationToken cancellationToken);
}

public sealed class JobRunCancellationProbe : IJobRunCancellationProbe
{
    private readonly IPlantProcessDbContext _dbContext;

    public JobRunCancellationProbe(IPlantProcessDbContext dbContext)
    {
        _dbContext = dbContext;
    }

    public async Task<bool> IsRequestedAsync(Guid jobRunHistoryId, CancellationToken cancellationToken)
    {
        DateTime? requestedAtUtc = await _dbContext.JobRunHistories
            .AsNoTracking()
            .Where(x => x.Id == jobRunHistoryId)
            .Select(x => x.CancellationRequestedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return requestedAtUtc.HasValue;
    }
}
