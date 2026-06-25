using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Application.Integration.Contracts.Dtos;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Application.Integration.Protection;
using PlantProcess.Domain.Entities.Integration;
using Xunit;

namespace PlantProcess.Application.UnitTests.Integration;

public sealed class SourceLoadThrottlingDataSourceReaderTests
{
    private sealed class RecordingReader : IDataSourceReader
    {
        public bool ReadCalled { get; private set; }
        public string ProviderType => "fake";

        public Task<IReadOnlyList<DataSourceRow>> ReadRowsAsync(
            ConnectionProfile connectionProfile,
            SourceDatasetDefinition datasetDefinition,
            DataSourceReadRequest request,
            CancellationToken cancellationToken)
        {
            ReadCalled = true;
            return Task.FromResult<IReadOnlyList<DataSourceRow>>(Array.Empty<DataSourceRow>());
        }

        public Task<IReadOnlyList<DataSourceRow>> ReadRowsSinceKeyAsync(
            ConnectionProfile connectionProfile,
            SourceDatasetDefinition datasetDefinition,
            DataSourceIncrementalReadRequest request,
            CancellationToken cancellationToken)
        {
            return Task.FromResult<IReadOnlyList<DataSourceRow>>(Array.Empty<DataSourceRow>());
        }
    }

    private sealed class FixedBudget : ISourceLoadBudgetProvider
    {
        private readonly SourceLoadBudget _budget;
        public FixedBudget(SourceLoadBudget budget) => _budget = budget;
        public SourceLoadBudget GetBudget(ConnectionProfile? connectionProfile, SourceDatasetDefinition? datasetDefinition) => _budget;
    }

    private static DataSourceReadRequest Request(Guid profileId, int limit) =>
        new(profileId, null, "orders", null, limit, null);

    [Fact]
    public async Task Over_cap_live_read_is_throttled_and_inner_not_called()
    {
        var inner = new RecordingReader();
        var reader = new ThrottlingDataSourceReader(
            inner, new FixedBudget(new SourceLoadBudget(100, 30, 60)), new SlidingWindowSourceQueryRateLimiter());

        var ex = await Assert.ThrowsAsync<SourceLoadRejectedException>(() =>
            reader.ReadRowsAsync(null!, null!, Request(Guid.NewGuid(), 1000), CancellationToken.None));

        Assert.Equal(SourceLoadRejectionReason.RowCapExceeded, ex.Reason);
        Assert.False(inner.ReadCalled);
    }

    [Fact]
    public async Task Unbounded_live_read_is_rejected()
    {
        var inner = new RecordingReader();
        var reader = new ThrottlingDataSourceReader(
            inner, new FixedBudget(new SourceLoadBudget(100, 30, 60)), new SlidingWindowSourceQueryRateLimiter());

        var ex = await Assert.ThrowsAsync<SourceLoadRejectedException>(() =>
            reader.ReadRowsAsync(null!, null!, Request(Guid.NewGuid(), 0), CancellationToken.None));

        Assert.Equal(SourceLoadRejectionReason.NoRowLimit, ex.Reason);
        Assert.False(inner.ReadCalled);
    }

    [Fact]
    public async Task Within_budget_live_read_passes_through()
    {
        var inner = new RecordingReader();
        var reader = new ThrottlingDataSourceReader(
            inner, new FixedBudget(new SourceLoadBudget(100, 30, 60)), new SlidingWindowSourceQueryRateLimiter());

        var rows = await reader.ReadRowsAsync(null!, null!, Request(Guid.NewGuid(), 50), CancellationToken.None);

        Assert.NotNull(rows);
        Assert.True(inner.ReadCalled);
    }

    [Fact]
    public async Task Rate_limit_throttles_after_budget_exhausted()
    {
        var inner = new RecordingReader();
        var reader = new ThrottlingDataSourceReader(
            inner, new FixedBudget(new SourceLoadBudget(100, 30, 3)), new SlidingWindowSourceQueryRateLimiter());
        var profileId = Guid.NewGuid();

        await reader.ReadRowsAsync(null!, null!, Request(profileId, 10), CancellationToken.None);
        await reader.ReadRowsAsync(null!, null!, Request(profileId, 10), CancellationToken.None);
        await reader.ReadRowsAsync(null!, null!, Request(profileId, 10), CancellationToken.None);

        var ex = await Assert.ThrowsAsync<SourceLoadRejectedException>(() =>
            reader.ReadRowsAsync(null!, null!, Request(profileId, 10), CancellationToken.None));

        Assert.Equal(SourceLoadRejectionReason.RateLimitExceeded, ex.Reason);
    }
}