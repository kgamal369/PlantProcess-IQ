using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Jobs.Execution;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Persistence;

namespace PlantProcess.Infrastructure.Jobs;

/// <summary>
/// Persists per-block runtime evidence through the same context as the run it belongs
/// to. Script 846 owns the table; the entity is excluded from migrations.
/// </summary>
public sealed class JobRunBlockEvidenceStore : IJobRunBlockEvidenceStore
{
    private readonly PlantProcessDbContext _db;

    public JobRunBlockEvidenceStore(PlantProcessDbContext db)
    {
        _db = db;
    }

    public Task AddAsync(IReadOnlyList<JobRunBlockEvidence> evidence, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        _db.JobRunBlockEvidences.AddRange(evidence);
        return Task.CompletedTask;
    }

    public Task SaveAsync(CancellationToken cancellationToken) => _db.SaveChangesAsync(cancellationToken);

    public Task DiscardAsync(JobRunBlockEvidence evidence, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(evidence);
        return _db.JobRunBlockEvidences.Entry(evidence).ReloadAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<JobRunBlockEvidence>> ListAsync(Guid jobRunHistoryId, CancellationToken cancellationToken)
    {
        return await _db.JobRunBlockEvidences
            .AsNoTracking()
            .Where(x => x.JobRunHistoryId == jobRunHistoryId)
            .OrderBy(x => x.ExecutionOrdinal)
            .ToListAsync(cancellationToken);
    }
}
