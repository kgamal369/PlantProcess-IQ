using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.DataQuality;
using PlantProcess.Domain.Entities.Quality;

namespace PlantProcess.Application.Services.DataQuality;

/// <summary>
/// Full implementation of IDataQualityService.
/// Handles manual issue raising and the automated full-scan-and-persist workflow.
///
/// Scan rules implemented:
///   1.  MissingMaterialAlias              — material has no source alias
///   2.  MissingProcessHistory             — material has no process steps
///   3.  MissingParameterValue             — parameter observation has no numeric/text/boolean value
///   4.  ParameterObservationWithoutStep   — observation not linked to a process step
///   5.  ParameterObservationOutsideWindow — observation timestamp outside step time window
///   6.  DefectEventWithoutCatalog         — defect quality event not linked to a defect catalog
///   7.  HighRiskScoreWithoutContributors  — high risk score (≥0.7) with no contributor JSON
///   8.  SourceSystemNotReadOnly           — source system is not marked read-only
///   9.  ProcessEventWithoutReference      — process event has no material, step, or equipment
///   10. DowntimeEventWithoutReference     — downtime event has no material, step, or equipment
/// </summary>
public sealed class DataQualityService : IDataQualityService
{
    private readonly IPlantProcessDbContext _dbContext;
    private readonly ILogger<DataQualityService> _logger;

    public DataQualityService(
        IPlantProcessDbContext dbContext,
        ILogger<DataQualityService> logger)
    {
        _dbContext = dbContext;
        _logger = logger;
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Manual issue raising
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<ApplicationResult<Guid>> RaiseIssueAsync(
        RaiseDataQualityIssueCommand command,
        CancellationToken cancellationToken)
    {
        _logger.LogDebug(
            "DataQualityService.RaiseIssueAsync called. IssueType={IssueType}, " +
            "Severity={Severity}, MaterialUnitId={MaterialUnitId}, " +
            "AffectedEntityName={AffectedEntityName}, CorrelationId={CorrelationId}",
            command.IssueType,
            command.Severity,
            command.MaterialUnitId,
            command.AffectedEntityName,
            command.Metadata.CorrelationId);

        // ── Validation ─────────────────────────────────────────────────────
        if (string.IsNullOrWhiteSpace(command.IssueType))
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation("Issue type is required."));

        if (string.IsNullOrWhiteSpace(command.Description))
            return ApplicationResult<Guid>.Failure(
                ApplicationError.Validation("Issue description is required."));

        if (command.MaterialUnitId.HasValue)
        {
            var materialExists = await _dbContext.MaterialUnits
                .AnyAsync(x => x.Id == command.MaterialUnitId.Value, cancellationToken);

            if (!materialExists)
                return ApplicationResult<Guid>.Failure(
                    ApplicationError.NotFound("Material unit does not exist."));
        }

        // ── Persist ────────────────────────────────────────────────────────
        var issue = new DataQualityIssue(
            issueType: command.IssueType,
            description: command.Description,
            isSynthetic: command.Metadata.IsSynthetic,
            materialUnitId: command.MaterialUnitId,
            severity: command.Severity ?? "Warning",
            affectedEntityName: command.AffectedEntityName,
            affectedEntityId: command.AffectedEntityId,
            sourceSystem: command.Metadata.SourceSystem,
            sourceRecordId: command.Metadata.SourceRecordId);

        _dbContext.DataQualityIssues.Add(issue);
        await _dbContext.SaveChangesAsync(cancellationToken);

        _logger.LogWarning(
            "Raised data-quality issue manually. " +
            "DataQualityIssueId={DataQualityIssueId}, IssueType={IssueType}, " +
            "Severity={Severity}, MaterialUnitId={MaterialUnitId}, " +
            "AffectedEntityName={AffectedEntityName}, CorrelationId={CorrelationId}",
            issue.Id,
            issue.IssueType,
            issue.Severity,
            issue.MaterialUnitId,
            issue.AffectedEntityName,
            command.Metadata.CorrelationId);

        return ApplicationResult<Guid>.Success(issue.Id);
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Automated full scan
    // ═══════════════════════════════════════════════════════════════════════

    public async Task<ApplicationResult<DataQualityScanSummary>> RunFullScanAsync(
        int maxCandidatesPerRule,
        CancellationToken cancellationToken)
    {
        var scanStart = DateTime.UtcNow;
        var cap = Math.Clamp(maxCandidatesPerRule, 1, 5000);

        _logger.LogInformation(
            "Data quality full scan started. MaxCandidatesPerRule={MaxCandidatesPerRule}, " +
            "ScanStartUtc={ScanStartUtc}",
            cap,
            scanStart);

        try
        {
            // ── Step 1: Collect all candidates from every rule ─────────────
            var candidates = await CollectAllCandidatesAsync(cap, cancellationToken);

            _logger.LogDebug(
                "Data quality scan collected {Count} candidates from all rules.",
                candidates.Count);

            // ── Step 2: Persist only new findings (skip duplicates) ────────
            var newCount = 0;
            var skippedCount = 0;

            foreach (var c in candidates)
            {
                cancellationToken.ThrowIfCancellationRequested();

                // Duplicate check: same IssueType + AffectedEntityName + AffectedEntityId
                var isDuplicate = await _dbContext.DataQualityIssues
                    .AnyAsync(x =>
                        x.IssueType == c.IssueType &&
                        x.AffectedEntityName == c.AffectedEntityName &&
                        x.AffectedEntityId == c.AffectedEntityId,
                        cancellationToken);

                if (isDuplicate)
                {
                    skippedCount++;

                    _logger.LogTrace(
                        "Skipping duplicate data-quality issue. " +
                        "IssueType={IssueType}, AffectedEntityName={AffectedEntityName}, " +
                        "AffectedEntityId={AffectedEntityId}",
                        c.IssueType,
                        c.AffectedEntityName,
                        c.AffectedEntityId);

                    continue;
                }

                _dbContext.DataQualityIssues.Add(new DataQualityIssue(
                    issueType: c.IssueType,
                    description: c.Description,
                    isSynthetic: c.IsSynthetic,
                    materialUnitId: c.MaterialUnitId,
                    severity: c.Severity,
                    affectedEntityName: c.AffectedEntityName,
                    affectedEntityId: c.AffectedEntityId,
                    sourceSystem: c.SourceSystem,
                    sourceRecordId: c.SourceRecordId));

                newCount++;

                _logger.LogWarning(
                    "Persisted new data-quality issue from scan. " +
                    "IssueType={IssueType}, Severity={Severity}, " +
                    "AffectedEntityName={AffectedEntityName}, AffectedEntityId={AffectedEntityId}, " +
                    "MaterialUnitId={MaterialUnitId}",
                    c.IssueType,
                    c.Severity,
                    c.AffectedEntityName,
                    c.AffectedEntityId,
                    c.MaterialUnitId);
            }

            // Batch save all new issues at once for efficiency
            if (newCount > 0)
                await _dbContext.SaveChangesAsync(cancellationToken);

            var scanEnd = DateTime.UtcNow;
            var duration = scanEnd - scanStart;

            var summary = new DataQualityScanSummary(
                ScannedAtUtc: scanEnd,
                CandidatesFound: candidates.Count,
                NewIssuesPersisted: newCount,
                ExistingIssuesSkipped: skippedCount,
                ScanDuration: duration);

            _logger.LogInformation(
                "Data quality full scan completed. " +
                "CandidatesFound={CandidatesFound}, " +
                "NewIssuesPersisted={NewIssuesPersisted}, " +
                "ExistingIssuesSkipped={ExistingIssuesSkipped}, " +
                "DurationMs={DurationMs}",
                summary.CandidatesFound,
                summary.NewIssuesPersisted,
                summary.ExistingIssuesSkipped,
                (long)duration.TotalMilliseconds);

            return ApplicationResult<DataQualityScanSummary>.Success(summary);
        }
        catch (OperationCanceledException)
        {
            _logger.LogWarning(
                "Data quality scan cancelled after {ElapsedMs} ms.",
                (long)(DateTime.UtcNow - scanStart).TotalMilliseconds);
            throw;
        }
        catch (Exception ex)
        {
            _logger.LogError(
                ex,
                "Data quality scan failed after {ElapsedMs} ms.",
                (long)(DateTime.UtcNow - scanStart).TotalMilliseconds);

            return ApplicationResult<DataQualityScanSummary>.Failure(
                ApplicationError.Infrastructure($"Data quality scan failed: {ex.Message}"));
        }
    }

    // ═══════════════════════════════════════════════════════════════════════
    // Private: scan rule implementations
    // ═══════════════════════════════════════════════════════════════════════

    private async Task<List<ScanCandidate>> CollectAllCandidatesAsync(
        int cap,
        CancellationToken cancellationToken)
    {
        var result = new List<ScanCandidate>();

        // ── Rule 1: Materials without aliases ─────────────────────────────
        _logger.LogTrace("DQ Scan: running rule MissingMaterialAlias.");
        var materialsWithoutAliases = await _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(m => !_dbContext.MaterialAliases.Any(a => a.MaterialUnitId == m.Id))
            .OrderBy(x => x.MaterialCode)
            .Take(cap)
            .Select(x => new { x.Id, x.MaterialCode, x.IsSynthetic, x.SourceSystem, x.SourceRecordId })
            .ToListAsync(cancellationToken);

        result.AddRange(materialsWithoutAliases.Select(x => new ScanCandidate(
            IssueType: "MissingMaterialAlias",
            Severity: "Warning",
            Description: $"Material '{x.MaterialCode}' has no source-system alias. Cross-system traceability is reduced.",
            AffectedEntityName: "MaterialUnit",
            AffectedEntityId: x.Id,
            MaterialUnitId: x.Id,
            IsSynthetic: x.IsSynthetic,
            SourceSystem: x.SourceSystem,
            SourceRecordId: x.SourceRecordId)));

        _logger.LogTrace("DQ Scan: MissingMaterialAlias found {Count} candidates.", materialsWithoutAliases.Count);

        // ── Rule 2: Materials without process steps ───────────────────────
        _logger.LogTrace("DQ Scan: running rule MissingProcessHistory.");
        var materialsWithoutSteps = await _dbContext.MaterialUnits
            .AsNoTracking()
            .Where(m => !_dbContext.ProcessStepExecutions.Any(s => s.MaterialUnitId == m.Id))
            .OrderBy(x => x.MaterialCode)
            .Take(cap)
            .Select(x => new { x.Id, x.MaterialCode, x.IsSynthetic, x.SourceSystem, x.SourceRecordId })
            .ToListAsync(cancellationToken);

        result.AddRange(materialsWithoutSteps.Select(x => new ScanCandidate(
            IssueType: "MissingProcessHistory",
            Severity: "Warning",
            Description: $"Material '{x.MaterialCode}' has no process step execution. Investigation timeline will be incomplete.",
            AffectedEntityName: "MaterialUnit",
            AffectedEntityId: x.Id,
            MaterialUnitId: x.Id,
            IsSynthetic: x.IsSynthetic,
            SourceSystem: x.SourceSystem,
            SourceRecordId: x.SourceRecordId)));

        _logger.LogTrace("DQ Scan: MissingProcessHistory found {Count} candidates.", materialsWithoutSteps.Count);

        // ── Rule 3: Parameter observations without any value ─────────────
        _logger.LogTrace("DQ Scan: running rule MissingParameterValue.");
        var observationsWithoutValue = await _dbContext.ParameterObservations
            .AsNoTracking()
            .Where(x => x.NumericValue == null && x.TextValue == null && x.BooleanValue == null)
            .OrderBy(x => x.ObservedAtUtc)
            .Take(cap)
            .Select(x => new { x.Id, x.MaterialUnitId, x.IsSynthetic, x.SourceSystem, x.SourceRecordId })
            .ToListAsync(cancellationToken);

        result.AddRange(observationsWithoutValue.Select(x => new ScanCandidate(
            IssueType: "MissingParameterValue",
            Severity: "Error",
            Description: "Parameter observation has no numeric, text or boolean value. The record is effectively empty.",
            AffectedEntityName: "ParameterObservation",
            AffectedEntityId: x.Id,
            MaterialUnitId: x.MaterialUnitId,
            IsSynthetic: x.IsSynthetic,
            SourceSystem: x.SourceSystem,
            SourceRecordId: x.SourceRecordId)));

        _logger.LogTrace("DQ Scan: MissingParameterValue found {Count} candidates.", observationsWithoutValue.Count);

        // ── Rule 4: Observations not linked to a process step ────────────
        _logger.LogTrace("DQ Scan: running rule ParameterObservationWithoutProcessStep.");
        var observationsWithoutStep = await _dbContext.ParameterObservations
            .AsNoTracking()
            .Where(x => x.ProcessStepExecutionId == null)
            .OrderBy(x => x.ObservedAtUtc)
            .Take(cap)
            .Select(x => new { x.Id, x.MaterialUnitId, x.IsSynthetic, x.SourceSystem, x.SourceRecordId })
            .ToListAsync(cancellationToken);

        result.AddRange(observationsWithoutStep.Select(x => new ScanCandidate(
            IssueType: "ParameterObservationWithoutProcessStep",
            Severity: "Warning",
            Description: "Parameter observation is not linked to a process step. Process-window analytics will be degraded.",
            AffectedEntityName: "ParameterObservation",
            AffectedEntityId: x.Id,
            MaterialUnitId: x.MaterialUnitId,
            IsSynthetic: x.IsSynthetic,
            SourceSystem: x.SourceSystem,
            SourceRecordId: x.SourceRecordId)));

        _logger.LogTrace("DQ Scan: ParameterObservationWithoutProcessStep found {Count} candidates.", observationsWithoutStep.Count);

        // ── Rule 5: Observations outside the process step time window ─────
        _logger.LogTrace("DQ Scan: running rule ParameterObservationOutsideStepWindow.");
        var observationsOutsideWindow = await _dbContext.ParameterObservations
            .AsNoTracking()
            .Join(
                _dbContext.ProcessStepExecutions.AsNoTracking(),
                obs => obs.ProcessStepExecutionId,
                step => step.Id,
                (obs, step) => new { obs, step })
            .Where(x =>
                x.obs.ObservedAtUtc < x.step.StartedAtUtc ||
                (x.step.EndedAtUtc.HasValue && x.obs.ObservedAtUtc > x.step.EndedAtUtc.Value))
            .OrderBy(x => x.obs.ObservedAtUtc)
            .Take(cap)
            .Select(x => new
            {
                x.obs.Id,
                x.obs.MaterialUnitId,
                x.obs.ObservedAtUtc,
                StepStartUtc = x.step.StartedAtUtc,
                StepEndUtc = x.step.EndedAtUtc,
                x.obs.IsSynthetic,
                x.obs.SourceSystem,
                x.obs.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        result.AddRange(observationsOutsideWindow.Select(x => new ScanCandidate(
            IssueType: "ParameterObservationOutsideStepWindow",
            Severity: "Error",
            Description: $"Observation at {x.ObservedAtUtc:o} is outside the linked step window " +
                                 $"[{x.StepStartUtc:o} — {(x.StepEndUtc.HasValue ? x.StepEndUtc.Value.ToString("o") : "open")}].",
            AffectedEntityName: "ParameterObservation",
            AffectedEntityId: x.Id,
            MaterialUnitId: x.MaterialUnitId,
            IsSynthetic: x.IsSynthetic,
            SourceSystem: x.SourceSystem,
            SourceRecordId: x.SourceRecordId)));

        _logger.LogTrace("DQ Scan: ParameterObservationOutsideStepWindow found {Count} candidates.", observationsOutsideWindow.Count);

        // ── Rule 6: Defect events without a defect catalog ────────────────
        _logger.LogTrace("DQ Scan: running rule DefectEventWithoutCatalog.");
        var defectEventsWithoutCatalog = await _dbContext.QualityEvents
            .AsNoTracking()
            .Where(x => x.EventType == "Defect" && x.DefectCatalogId == null)
            .OrderBy(x => x.EventAtUtc)
            .Take(cap)
            .Select(x => new { x.Id, x.MaterialUnitId, x.IsSynthetic, x.SourceSystem, x.SourceRecordId })
            .ToListAsync(cancellationToken);

        result.AddRange(defectEventsWithoutCatalog.Select(x => new ScanCandidate(
            IssueType: "DefectEventWithoutCatalog",
            Severity: "Warning",
            Description: "Quality event is marked as Defect but is not linked to a standardized defect catalog record. Correlation analytics will be degraded.",
            AffectedEntityName: "QualityEvent",
            AffectedEntityId: x.Id,
            MaterialUnitId: x.MaterialUnitId,
            IsSynthetic: x.IsSynthetic,
            SourceSystem: x.SourceSystem,
            SourceRecordId: x.SourceRecordId)));

        _logger.LogTrace("DQ Scan: DefectEventWithoutCatalog found {Count} candidates.", defectEventsWithoutCatalog.Count);

        // ── Rule 7: High risk scores without contributor JSON ─────────────
        _logger.LogTrace("DQ Scan: running rule HighRiskScoreWithoutContributors.");
        var highRiskWithoutContributors = await _dbContext.RiskScores
            .AsNoTracking()
            .Where(x => x.Score >= 0.70m &&
                        (x.MainContributorsJson == null || x.MainContributorsJson == string.Empty))
            .OrderByDescending(x => x.ScoredAtUtc)
            .Take(cap)
            .Select(x => new
            {
                x.Id,
                x.MaterialUnitId,
                x.RiskType,
                x.Score,
                x.IsSynthetic,
                x.SourceSystem,
                x.SourceRecordId
            })
            .ToListAsync(cancellationToken);

        result.AddRange(highRiskWithoutContributors.Select(x => new ScanCandidate(
            IssueType: "HighRiskScoreWithoutContributors",
            Severity: "Warning",
            Description: $"Risk score '{x.RiskType}' is high ({x.Score:F3}) but has no contributor explanation JSON. The 'suspected contributor' promise cannot be fulfilled.",
            AffectedEntityName: "RiskScore",
            AffectedEntityId: x.Id,
            MaterialUnitId: x.MaterialUnitId,
            IsSynthetic: x.IsSynthetic,
            SourceSystem: x.SourceSystem,
            SourceRecordId: x.SourceRecordId)));

        _logger.LogTrace("DQ Scan: HighRiskScoreWithoutContributors found {Count} candidates.", highRiskWithoutContributors.Count);

        // ── Rule 8: Source systems that are not read-only ─────────────────
        _logger.LogTrace("DQ Scan: running rule SourceSystemNotReadOnly.");
        var nonReadOnlySources = await _dbContext.SourceSystemDefinitions
            .AsNoTracking()
            .Where(x => !x.IsReadOnlySource)
            .OrderBy(x => x.SourceSystemCode)
            .Take(cap)
            .Select(x => new { x.Id, x.SourceSystemCode, x.IsSynthetic, x.SourceSystem, x.SourceRecordId })
            .ToListAsync(cancellationToken);

        result.AddRange(nonReadOnlySources.Select(x => new ScanCandidate(
            IssueType: "SourceSystemNotReadOnly",
            Severity: "Warning",
            Description: $"Source system '{x.SourceSystemCode}' is not marked as read-only. MVP/pilot integrations should be read-only to prevent accidental writes.",
            AffectedEntityName: "SourceSystemDefinition",
            AffectedEntityId: x.Id,
            MaterialUnitId: null,
            IsSynthetic: x.IsSynthetic,
            SourceSystem: x.SourceSystem,
            SourceRecordId: x.SourceRecordId)));

        _logger.LogTrace("DQ Scan: SourceSystemNotReadOnly found {Count} candidates.", nonReadOnlySources.Count);

        // ── Rule 9: Process events with no references ─────────────────────
        _logger.LogTrace("DQ Scan: running rule ProcessEventWithoutReference.");
        var orphanProcessEvents = await _dbContext.ProcessEvents
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == null &&
                        x.ProcessStepExecutionId == null &&
                        x.EquipmentId == null)
            .OrderBy(x => x.EventAtUtc)
            .Take(cap)
            .Select(x => new { x.Id, x.IsSynthetic, x.SourceSystem, x.SourceRecordId })
            .ToListAsync(cancellationToken);

        result.AddRange(orphanProcessEvents.Select(x => new ScanCandidate(
            IssueType: "ProcessEventWithoutReference",
            Severity: "Error",
            Description: "Process event has no material, process step or equipment reference. It is effectively orphaned and cannot be linked to any production story.",
            AffectedEntityName: "ProcessEvent",
            AffectedEntityId: x.Id,
            MaterialUnitId: null,
            IsSynthetic: x.IsSynthetic,
            SourceSystem: x.SourceSystem,
            SourceRecordId: x.SourceRecordId)));

        _logger.LogTrace("DQ Scan: ProcessEventWithoutReference found {Count} candidates.", orphanProcessEvents.Count);

        // ── Rule 10: Downtime events with no references ───────────────────
        _logger.LogTrace("DQ Scan: running rule DowntimeEventWithoutReference.");
        var orphanDowntimeEvents = await _dbContext.DowntimeEvents
            .AsNoTracking()
            .Where(x => x.MaterialUnitId == null &&
                        x.ProcessStepExecutionId == null &&
                        x.EquipmentId == null)
            .OrderBy(x => x.StartedAtUtc)
            .Take(cap)
            .Select(x => new { x.Id, x.IsSynthetic, x.SourceSystem, x.SourceRecordId })
            .ToListAsync(cancellationToken);

        result.AddRange(orphanDowntimeEvents.Select(x => new ScanCandidate(
            IssueType: "DowntimeEventWithoutReference",
            Severity: "Error",
            Description: "Downtime event has no material, process step or equipment reference. It cannot be linked to any production story.",
            AffectedEntityName: "DowntimeEvent",
            AffectedEntityId: x.Id,
            MaterialUnitId: null,
            IsSynthetic: x.IsSynthetic,
            SourceSystem: x.SourceSystem,
            SourceRecordId: x.SourceRecordId)));

        _logger.LogTrace("DQ Scan: DowntimeEventWithoutReference found {Count} candidates.", orphanDowntimeEvents.Count);

        return result;
    }

    // ── Internal record for scan rule output ──────────────────────────────────
    private sealed record ScanCandidate(
        string IssueType,
        string Severity,
        string Description,
        string AffectedEntityName,
        Guid AffectedEntityId,
        Guid? MaterialUnitId,
        bool IsSynthetic,
        string? SourceSystem,
        string? SourceRecordId);
}


