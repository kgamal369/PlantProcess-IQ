using Microsoft.EntityFrameworkCore;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Jobs.Execution;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Enums.Integration;

namespace PlantProcess.Application.Jobs.Canvas;

/// <summary>
/// The read side over the existing job and run authorities. Every query is no-tracking
/// and there is no write path here at all: binding goes through the job definition
/// service and execution through the orchestrator.
/// </summary>
public sealed class CanvasJobReadModel : ICanvasJobReadModel
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly IJobRunBlockEvidenceStore _evidence;

    public CanvasJobReadModel(IPlantProcessDbContext dbContext, IJobRunBlockEvidenceStore evidence)
    {
        _dbContext = dbContext;
        _evidence = evidence;
    }

    public async Task<CanvasBoundJob?> FindBoundJobAsync(
        Guid targetDefinitionId,
        JobDefinitionType family,
        CancellationToken cancellationToken)
    {
        JobDefinition? job = await _dbContext.JobDefinitions
            .AsNoTracking()
            .Where(x => !x.IsDeleted && x.JobType == family && x.TargetDefinitionId == targetDefinitionId)
            .OrderBy(x => x.JobCode)
            .FirstOrDefaultAsync(cancellationToken);

        return job is null
            ? null
            : new CanvasBoundJob(
                job.Id,
                job.JobCode,
                job.JobName,
                job.TargetDefinitionKind,
                job.TargetDefinitionId,
                job.TargetDefinitionVersion,
                job.TargetVersionPolicy);
    }

    public async Task<CanvasRunRecord?> FindRunAsync(Guid runId, CancellationToken cancellationToken)
    {
        JobRunHistory? run = await _dbContext.JobRunHistories
            .AsNoTracking()
            .FirstOrDefaultAsync(x => x.Id == runId, cancellationToken);

        return run is null ? null : ToRecord(run);
    }

    public async Task<CanvasRunRecord?> FindRunByCorrelationAsync(
        Guid jobDefinitionId,
        string correlationId,
        CancellationToken cancellationToken)
    {
        JobRunHistory? run = await _dbContext.JobRunHistories
            .AsNoTracking()
            .Where(x => x.JobDefinitionId == jobDefinitionId && x.CorrelationId == correlationId)
            .OrderByDescending(x => x.StartedAtUtc)
            .FirstOrDefaultAsync(cancellationToken);

        return run is null ? null : ToRecord(run);
    }

    public async Task<IReadOnlyList<CanvasRunBlockDto>> ListBlocksAsync(Guid runId, CancellationToken cancellationToken)
    {
        IReadOnlyList<JobRunBlockEvidence> blocks = await _evidence.ListAsync(runId, cancellationToken);

        return blocks
            .OrderBy(b => b.ExecutionOrdinal)
            .Select(b => new CanvasRunBlockDto(
                b.BlockId,
                b.ExecutionOrdinal,
                b.Status.ToString(),
                b.InputRows,
                b.OutputRows,
                b.DiagnosticCode,
                b.DiagnosticDetail,
                b.StartedAtUtc,
                b.FinishedAtUtc))
            .ToArray();
    }

    private static CanvasRunRecord ToRecord(JobRunHistory run) =>
        new(run.Id,
            run.JobDefinitionId,
            run.JobCode,
            run.Status,
            run.StartedAtUtc,
            run.CompletedAtUtc,
            run.CorrelationId,
            run.TargetDefinitionId,
            run.TargetDefinitionVersion,
            run.TargetDefinitionKind,
            run.TargetVersionPolicy,
            run.CancellationRequestedAtUtc,
            run.CancellationAcknowledgedAtUtc,
            run.FailureReason,
            run.RunMessage);
}
