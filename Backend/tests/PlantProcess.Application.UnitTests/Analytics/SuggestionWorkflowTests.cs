using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using PlantProcess.Application.Analytics.Suggestions;
using PlantProcess.Application.Provenance;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics;

public class SuggestionWorkflowTests
{
    [Theory]
    [InlineData("operator", false)]
    [InlineData("viewer", false)]
    [InlineData("engineer", true)]
    [InlineData("admin", true)]
    public void Only_managers_may_accept(string role, bool allowed)
    {
        var d = SuggestionWorkflow.CanTransition(SuggestionStatus.Assigned, SuggestionStatus.Accepted, role);
        Assert.Equal(allowed, d.Allowed);
    }

    [Fact]
    public void Operator_may_acknowledge_assign()
    {
        Assert.True(SuggestionWorkflow.CanTransition(SuggestionStatus.Open, SuggestionStatus.Assigned, "operator").Allowed);
    }

    [Fact]
    public void Illegal_transition_is_refused()
    {
        // close before accept is not allowed
        Assert.False(SuggestionWorkflow.CanTransition(SuggestionStatus.Open, SuggestionStatus.Closed, "admin").Allowed);
        Assert.False(SuggestionWorkflow.CanTransition(SuggestionStatus.Rejected, SuggestionStatus.Accepted, "admin").Allowed);
    }

    // ---- dedup / reconcile via an in-memory store double (mirrors NpgsqlSuggestionStore behaviour) ----
    private sealed class InMemoryStore : ISuggestionStore
    {
        private readonly Dictionary<Guid, List<SuggestionCard>> _t = new();
        public Task<SuggestionSyncResult> SyncAsync(Guid tenant, IReadOnlyList<SuggestionCard> gen, string corr, CancellationToken ct)
        {
            if (!_t.TryGetValue(tenant, out var list)) { list = new(); _t[tenant] = list; }
            int created = 0, updated = 0, dismissed = 0;
            var genKeys = gen.Select(c => c.SuggestionKey).ToHashSet();
            foreach (var card in gen)
            {
                var existing = list.FindIndex(x => x.SuggestionKey == card.SuggestionKey && x.Status != SuggestionStatus.Dismissed);
                if (existing >= 0) { list[existing] = card with { Status = list[existing].Status }; updated++; }
                else { list.Add(card); created++; }
            }
            for (var i = 0; i < list.Count; i++)
                if (list[i].Status != SuggestionStatus.Dismissed && !genKeys.Contains(list[i].SuggestionKey))
                { list[i] = list[i] with { Status = SuggestionStatus.Dismissed }; dismissed++; }
            return Task.FromResult(new SuggestionSyncResult(created, updated, dismissed, corr));
        }
        public Task<IReadOnlyList<SuggestionCard>> ListActiveAsync(Guid tenant, CancellationToken ct)
            => Task.FromResult((IReadOnlyList<SuggestionCard>)(_t.TryGetValue(tenant, out var l) ? l.Where(x => x.Status != SuggestionStatus.Dismissed).ToList() : new()));
        public Task<TransitionDecision> TransitionAsync(Guid tenant, Guid id, SuggestionStatus to, string role, string actor, string? note, CancellationToken ct)
            => Task.FromResult(new TransitionDecision(true, "ok"));
    }

    private static SuggestionCard Card(string key, decimal high)
        => new(Guid.NewGuid(), key, "t", "InvestigateRisk", new[] { ProvenanceHandle.Finding(key) }, 0, high, 0.8, "honest", new[] { key });

    [Fact]
    public async Task Rerun_on_unchanged_findings_creates_no_duplicates_and_supersede_dismisses()
    {
        var store = new InMemoryStore();
        var tenant = Guid.NewGuid();

        var r1 = await store.SyncAsync(tenant, new[] { Card("k1", 100), Card("k2", 50) }, "c1", CancellationToken.None);
        Assert.Equal(2, r1.Created);

        var r2 = await store.SyncAsync(tenant, new[] { Card("k1", 100), Card("k2", 50) }, "c2", CancellationToken.None);
        Assert.Equal(0, r2.Created);
        Assert.Equal(2, r2.Updated);   // updated in place, no duplicates

        var r3 = await store.SyncAsync(tenant, new[] { Card("k1", 100) }, "c3", CancellationToken.None); // k2 gone
        Assert.Equal(1, r3.Dismissed); // superseded k2 dismissed
        Assert.Single(await store.ListActiveAsync(tenant, CancellationToken.None));
    }

    [Fact]
    public async Task Tenant_isolation_holds_in_sync()
    {
        var store = new InMemoryStore();
        var a = Guid.NewGuid(); var b = Guid.NewGuid();
        await store.SyncAsync(a, new[] { Card("k1", 100) }, "c", CancellationToken.None);
        Assert.Single(await store.ListActiveAsync(a, CancellationToken.None));
        Assert.Empty(await store.ListActiveAsync(b, CancellationToken.None));
    }
}