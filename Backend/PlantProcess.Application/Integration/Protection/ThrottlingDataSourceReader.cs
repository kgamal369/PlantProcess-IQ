using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Application.Integration.Protection;

/// <summary>
/// Enforces SourceLoadProtectionPolicy on the live source-query path. Wraps a real
/// IDataSourceReader: every one-shot ReadRowsAsync is evaluated against the source
/// budget (row cap, rate limit, approved window) and an over-budget read is rejected
/// before it reaches the source. Incremental (backfill) reads pass through unchanged;
/// the backfill worker retains its own cumulative rows/sec throttle.
/// </summary>
public sealed class ThrottlingDataSourceReader : IDataSourceReader
{
    private readonly IDataSourceReader _inner;
    private readonly ISourceLoadBudgetProvider _budgetProvider;
    private readonly ISourceQueryRateLimiter _rateLimiter;

    public ThrottlingDataSourceReader(
        IDataSourceReader inner,
        ISourceLoadBudgetProvider budgetProvider,
        ISourceQueryRateLimiter rateLimiter)
    {
        _inner = inner;
        _budgetProvider = budgetProvider;
        _rateLimiter = rateLimiter;
    }

    public string ProviderType => _inner.ProviderType;

    public async Task<IReadOnlyList<DataSourceRow>> ReadRowsAsync(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        DataSourceReadRequest request,
        CancellationToken cancellationToken)
    {
        var sourceKey = request.ConnectionProfileId.ToString();
        var now = DateTime.UtcNow;
        var budget = _budgetProvider.GetBudget(connectionProfile, datasetDefinition);
        var queriesInLastMinute = _rateLimiter.CountWithinLastMinute(sourceKey, now);

        var decision = SourceLoadProtectionPolicy.Evaluate(
            new SourceQueryRequest(
                HasRowLimit: request.Limit > 0,
                RequestedRowLimit: request.Limit,
                QueriesInLastMinute: queriesInLastMinute,
                NowUtc: TimeOnly.FromDateTime(now)),
            budget);

        if (!decision.Allowed) {
            throw new SourceLoadRejectedException(decision);
        }

        _rateLimiter.Record(sourceKey, now);
        return await _inner.ReadRowsAsync(connectionProfile, datasetDefinition, request, cancellationToken);
    }

    public Task<IReadOnlyList<DataSourceRow>> ReadRowsSinceKeyAsync(
        ConnectionProfile connectionProfile,
        SourceDatasetDefinition datasetDefinition,
        DataSourceIncrementalReadRequest request,
        CancellationToken cancellationToken)
    {
        return _inner.ReadRowsSinceKeyAsync(connectionProfile, datasetDefinition, request, cancellationToken);
    }
}