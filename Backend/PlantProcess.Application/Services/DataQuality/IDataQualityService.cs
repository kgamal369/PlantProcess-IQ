using PlantProcess.Application.Common.Results;
using PlantProcess.Application.Contracts.DataQuality;

namespace PlantProcess.Application.Services.DataQuality;

/// <summary>
/// Service contract for data quality operations.
/// Covers both manual issue raising and automated full-scan-and-persist.
/// </summary>
public interface IDataQualityService
{
    // ── Manual issue creation ─────────────────────────────────────────────────

    /// <summary>
    /// Manually raises and persists a single data quality issue.
    /// Called by the POST /data-quality/issues endpoint or by downstream processes
    /// that detect a specific issue while processing data.
    /// </summary>
    Task<ApplicationResult<Guid>> RaiseIssueAsync(
        RaiseDataQualityIssueCommand command,
        CancellationToken cancellationToken);

    // ── Automated scan ────────────────────────────────────────────────────────

    /// <summary>
    /// Runs all built-in data quality scan rules across the canonical model
    /// and persists NEW findings to the data_quality_issues table.
    /// Already-persisted issues (matched by IssueType + AffectedEntityName + AffectedEntityId)
    /// are skipped to prevent duplicates.
    /// Safe to call on a schedule — idempotent for existing findings.
    /// </summary>
    /// <param name="maxCandidatesPerRule">
    ///   Upper limit per scan rule (default 500). Prevents runaway scans on large datasets.
    /// </param>
    /// <param name="cancellationToken">Cancellation token.</param>
    /// <returns>
    ///   A result with a <see cref="DataQualityScanSummary"/> describing what was found and persisted.
    /// </returns>
    Task<ApplicationResult<DataQualityScanSummary>> RunFullScanAsync(
        int maxCandidatesPerRule,
        CancellationToken cancellationToken);
}

/// <summary>
/// Summary returned by a full data quality scan run.
/// </summary>
public sealed record DataQualityScanSummary(
    DateTime ScannedAtUtc,
    int CandidatesFound,
    int NewIssuesPersisted,
    int ExistingIssuesSkipped,
    TimeSpan ScanDuration);



