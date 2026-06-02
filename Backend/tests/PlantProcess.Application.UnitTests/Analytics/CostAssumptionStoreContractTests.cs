using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Application.Analytics.Value;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics;

/// <summary>
/// T-042 contract: versioning, before/after audit capture, and tenant isolation, proven against an
/// in-memory store (no database needed). The Npgsql store mirrors this contract.
/// </summary>
public class CostAssumptionStoreContractTests
{
    private sealed record AuditRow(Guid Tenant, int? From, int To, CostAssumptionSet? Before, CostAssumptionSet After);

    private sealed class InMemoryStore : ICostAssumptionStore
    {
        private readonly Dictionary<Guid, List<CostAssumptionSet>> _byTenant = new();
        public readonly List<AuditRow> Audit = new();

        public Task<CostAssumptionSet?> GetActiveAsync(Guid tenantId, CancellationToken ct)
            => Task.FromResult(_byTenant.TryGetValue(tenantId, out var list) && list.Count > 0
                ? list.OrderByDescending(x => x.Version).First() : null);

        public async Task<int> CreateVersionAsync(Guid tenantId, CostAssumptionSet set, string actor, CancellationToken ct)
        {
            var before = await GetActiveAsync(tenantId, ct);
            var next = (before?.Version ?? 0) + 1;
            var stored = set with { Version = next };
            if (!_byTenant.TryGetValue(tenantId, out var list)) { list = new(); _byTenant[tenantId] = list; }
            list.Add(stored);
            Audit.Add(new AuditRow(tenantId, before?.Version, next, before, stored));
            return next;
        }
    }

    private static CostAssumptionSet Sample(decimal downtimeMid = 150m) => new(
        0, "EUR",
        new CostBand(600, 700, 820), new CostBand(80, 120, 160), new CostBand(240, 300, 360),
        new CostBand(100, downtimeMid, 200), new CostBand(110, 155, 200), new CostBand(60, 85, 120));

    [Fact]
    public async Task Editing_creates_a_new_version_and_a_before_after_audit_row()
    {
        var store = new InMemoryStore();
        var tenant = Guid.NewGuid();

        var v1 = await store.CreateVersionAsync(tenant, Sample(150m), "alice", CancellationToken.None);
        var v2 = await store.CreateVersionAsync(tenant, Sample(175m), "alice", CancellationToken.None);

        Assert.Equal(1, v1);
        Assert.Equal(2, v2);
        Assert.Equal(2, store.Audit.Count);

        var second = store.Audit[1];
        Assert.Equal(1, second.From);
        Assert.Equal(2, second.To);
        Assert.NotNull(second.Before);
        Assert.Equal(150m, second.Before!.DowntimeCostPerMin!.Mid);
        Assert.Equal(175m, second.After.DowntimeCostPerMin!.Mid);
    }

    [Fact]
    public async Task A_value_result_references_the_active_assumption_version()
    {
        var store = new InMemoryStore();
        var tenant = Guid.NewGuid();
        await store.CreateVersionAsync(tenant, Sample(), "alice", CancellationToken.None);
        await store.CreateVersionAsync(tenant, Sample(175m), "alice", CancellationToken.None);

        var active = await store.GetActiveAsync(tenant, CancellationToken.None);
        Assert.NotNull(active);

        var result = new ValueImpactEngine().Compute(
            new ValueImpactInputs("f", "C1", "EDGE_CRACK", 0.02m, 8000m, 90m, 60m), active!);

        Assert.Equal(2, result.AssumptionVersion); // result traces to the exact version used
    }

    [Fact]
    public async Task Tenant_isolation_holds()
    {
        var store = new InMemoryStore();
        var tenantA = Guid.NewGuid();
        var tenantB = Guid.NewGuid();
        await store.CreateVersionAsync(tenantA, Sample(), "alice", CancellationToken.None);

        Assert.NotNull(await store.GetActiveAsync(tenantA, CancellationToken.None));
        Assert.Null(await store.GetActiveAsync(tenantB, CancellationToken.None)); // B never sees A
    }
}