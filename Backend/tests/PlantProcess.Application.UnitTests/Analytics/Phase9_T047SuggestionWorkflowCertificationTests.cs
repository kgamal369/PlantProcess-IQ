
using System.Security.Cryptography;
using System.Text;
using PlantProcess.Application.Analytics.Suggestions;
using PlantProcess.Application.Provenance;
using Xunit;

namespace PlantProcess.Application.UnitTests.Analytics;

/// <summary>
/// PPIQ_REALIZATION_T047_DETERMINISTIC_SUGGESTION_WORKFLOW.
/// Certifies deterministic suggestion generation, stable IDs, confidence,
/// ranged impact, de-duplication, supersede dismissal, and RBAC state transitions.
/// </summary>
public sealed class Phase9_T047SuggestionWorkflowCertificationTests
{
    private static readonly SuggestionEngine Engine = new();

    private static ApprovedFinding Finding(
        FindingKind kind,
        string findingRef,
        string subject,
        string? defectCode,
        decimal low,
        decimal high,
        double dq = 0.92,
        int sampleSize = 180,
        double stability = 0.87,
        bool synthetic = false)
        => new(
            kind,
            findingRef,
            ProvenanceHandle.Finding(findingRef),
            subject,
            defectCode,
            sampleSize,
            stability,
            dq,
            low,
            high,
            synthetic);

    private static ApprovedFinding Risk(
        string subject,
        decimal low,
        decimal high,
        double dq = 0.92,
        int sampleSize = 180,
        double stability = 0.87,
        bool synthetic = false)
        => Finding(
            FindingKind.Risk,
            $"finding-edge-{subject}",
            subject,
            "EDGE_CRACK",
            low,
            high,
            dq,
            sampleSize,
            stability,
            synthetic);

    private static Guid ExpectedSuggestionId(string suggestionKey)
    {
        using var md5 = MD5.Create();
        return new Guid(md5.ComputeHash(Encoding.UTF8.GetBytes("ppiq-suggestion:" + suggestionKey)));
    }

    [Fact]
    public void T047_Same_Inputs_Produce_Identical_MD5_Stable_Suggestion_Ids()
    {
        var findings = new[]
        {
            Risk("caster-b", 10_000m, 20_000m),
            Risk("caster-a", 28_000m, 56_000m),
            Finding(FindingKind.ValueImpact, "finding-value-lf", "ladle-furnace", "ENERGY", 5_000m, 12_000m)
        };

        var first = Engine.Generate(findings);
        var second = Engine.Generate(findings);

        Assert.Equal(first.Select(x => x.Id), second.Select(x => x.Id));
        Assert.Equal(first.Select(x => x.SuggestionKey), second.Select(x => x.SuggestionKey));

        var expectedKey = "Risk|caster-a|EDGE_CRACK|finding-edge-caster-a";
        var card = Assert.Single(first, x => x.SuggestionKey == expectedKey);

        Assert.Equal(ExpectedSuggestionId(expectedKey), card.Id);
        Assert.Contains("ppiq-suggestion", "ppiq-suggestion:" + expectedKey);
    }

    [Fact]
    public void T047_Input_Order_Does_Not_Change_Output_Order()
    {
        var a = new[]
        {
            Risk("caster-b", 10_000m, 20_000m),
            Risk("caster-a", 28_000m, 56_000m),
            Risk("caster-c", 28_000m, 56_000m, dq: 0.80, sampleSize: 120, stability: 0.75)
        };

        var b = a.Reverse().ToArray();

        var first = Engine.Generate(a);
        var second = Engine.Generate(b);

        Assert.Equal(first.Select(x => x.SuggestionKey), second.Select(x => x.SuggestionKey));
        Assert.Equal("finding-edge-caster-a", first[0].SourceFindingRefs.Single());
    }

    [Fact]
    public void T047_Every_Suggestion_Has_Evidence_Ranged_Impact_Confidence_And_Honesty_Text()
    {
        var cards = Engine.Generate(new[]
        {
            Risk("caster-a", 28_000m, 56_000m),
            Finding(FindingKind.Correlation, "finding-correlation-1", "roughing-mill", "WEDGE", 1_000m, 4_000m)
        });

        Assert.NotEmpty(cards);

        Assert.All(cards, card =>
        {
            Assert.NotEqual(Guid.Empty, card.Id);
            Assert.False(string.IsNullOrWhiteSpace(card.SuggestionKey));
            Assert.False(string.IsNullOrWhiteSpace(card.Title));
            Assert.False(string.IsNullOrWhiteSpace(card.ActionType));
            Assert.NotEmpty(card.EvidenceHandles);
            Assert.All(card.EvidenceHandles, handle => Assert.False(string.IsNullOrWhiteSpace(handle.Id)));
            Assert.NotNull(card.ImpactLow);
            Assert.NotNull(card.ImpactHigh);
            Assert.True(card.ImpactHigh >= card.ImpactLow);
            Assert.InRange(card.Confidence, 0.0, 1.0);
            Assert.Contains("estimated range", card.HonestyText, StringComparison.OrdinalIgnoreCase);
            Assert.Contains("not a promise", card.HonestyText, StringComparison.OrdinalIgnoreCase);
            Assert.Equal(SuggestionStatus.Open, card.Status);
        });
    }

    [Fact]
    public void T047_Confidence_Is_Bounded_And_Monotonic()
    {
        var weak = Engine.Generate(new[]
        {
            Risk("caster-a", 1m, 2m, dq: 0.25, sampleSize: 5, stability: 0.25)
        })[0];

        var strong = Engine.Generate(new[]
        {
            Risk("caster-a", 1m, 2m, dq: 0.95, sampleSize: 300, stability: 0.95)
        })[0];

        Assert.InRange(weak.Confidence, 0.0, 1.0);
        Assert.InRange(strong.Confidence, 0.0, 1.0);
        Assert.True(strong.Confidence > weak.Confidence);
    }

    [Fact]
    public void T047_Findings_Without_Evidence_Or_Synthetic_Seed_Findings_Are_Refused()
    {
        var noEvidence = new ApprovedFinding(
            FindingKind.Risk,
            "finding-no-evidence",
            new ProvenanceHandle(ProvenanceKind.Finding, ""),
            "caster-a",
            "EDGE_CRACK",
            100,
            0.80,
            0.90,
            1m,
            2m);

        var synthetic = Risk("synthetic-seed", 1m, 2m, synthetic: true);

        Assert.Empty(Engine.Generate(new[] { noEvidence }));
        Assert.Empty(Engine.Generate(new[] { synthetic }));
    }

    [Fact]
    public async Task T047_Rerun_Deduplicates_By_SuggestionKey_And_Dismisses_Superseded()
    {
        var store = new InMemorySuggestionStore();
        var tenant = Guid.Parse("99999999-9999-9999-9999-999999999999");

        var firstCards = Engine.Generate(new[]
        {
            Risk("caster-a", 28_000m, 56_000m),
            Risk("caster-b", 10_000m, 20_000m)
        });

        var first = await store.SyncAsync(tenant, firstCards, "corr-1", CancellationToken.None);

        Assert.Equal(2, first.Created);
        Assert.Equal(0, first.Updated);
        Assert.Equal(0, first.Dismissed);

        var rerun = await store.SyncAsync(tenant, firstCards, "corr-2", CancellationToken.None);

        Assert.Equal(0, rerun.Created);
        Assert.Equal(2, rerun.Updated);
        Assert.Equal(0, rerun.Dismissed);

        var activeAfterRerun = await store.ListActiveAsync(tenant, CancellationToken.None);
        Assert.Equal(2, activeAfterRerun.Count);
        Assert.Equal(2, activeAfterRerun.Select(x => x.SuggestionKey).Distinct(StringComparer.Ordinal).Count());

        var reducedCards = Engine.Generate(new[]
        {
            Risk("caster-a", 28_000m, 56_000m)
        });

        var reduced = await store.SyncAsync(tenant, reducedCards, "corr-3", CancellationToken.None);

        Assert.Equal(0, reduced.Created);
        Assert.Equal(1, reduced.Updated);
        Assert.Equal(1, reduced.Dismissed);

        var activeAfterSupersede = await store.ListActiveAsync(tenant, CancellationToken.None);
        Assert.Single(activeAfterSupersede);
        Assert.Equal("Risk|caster-a|EDGE_CRACK|finding-edge-caster-a", activeAfterSupersede[0].SuggestionKey);
    }

    [Fact]
    public async Task T047_Workflow_Allows_Assign_Accept_Close_For_Authorized_Role()
    {
        var store = new InMemorySuggestionStore();
        var tenant = Guid.NewGuid();
        var cards = Engine.Generate(new[] { Risk("caster-a", 28_000m, 56_000m) });

        await store.SyncAsync(tenant, cards, "corr-authorized", CancellationToken.None);
        var card = (await store.ListActiveAsync(tenant, CancellationToken.None)).Single();

        var assigned = await store.TransitionAsync(tenant, card.Id, SuggestionStatus.Assigned, "operator", "operator-1", "acknowledged", CancellationToken.None);
        Assert.True(assigned.Allowed);

        var accepted = await store.TransitionAsync(tenant, card.Id, SuggestionStatus.Accepted, "engineer", "engineer-1", "accepted for action", CancellationToken.None);
        Assert.True(accepted.Allowed);

        var closed = await store.TransitionAsync(tenant, card.Id, SuggestionStatus.Closed, "admin", "admin-1", "closed after review", CancellationToken.None);
        Assert.True(closed.Allowed);

        var final = (await store.ListAllAsync(tenant)).Single();
        Assert.Equal(SuggestionStatus.Closed, final.Status);

        Assert.Equal(new[]
        {
            "Open->Assigned",
            "Assigned->Accepted",
            "Accepted->Closed"
        }, store.Audit.Select(x => x.Transition));
    }

    [Fact]
    public async Task T047_Unauthorized_Role_Cannot_Accept_Or_Close_A_Suggestion()
    {
        var store = new InMemorySuggestionStore();
        var tenant = Guid.NewGuid();
        var cards = Engine.Generate(new[] { Risk("caster-a", 28_000m, 56_000m) });

        await store.SyncAsync(tenant, cards, "corr-unauthorized", CancellationToken.None);
        var card = (await store.ListActiveAsync(tenant, CancellationToken.None)).Single();

        var assigned = await store.TransitionAsync(tenant, card.Id, SuggestionStatus.Assigned, "operator", "operator-1", "acknowledged", CancellationToken.None);
        Assert.True(assigned.Allowed);

        var unauthorizedAccept = await store.TransitionAsync(tenant, card.Id, SuggestionStatus.Accepted, "viewer", "viewer-1", "trying to accept", CancellationToken.None);
        Assert.False(unauthorizedAccept.Allowed);
        Assert.Contains("may acknowledge but not Accepted", unauthorizedAccept.Reason);

        var stillAssigned = (await store.ListAllAsync(tenant)).Single();
        Assert.Equal(SuggestionStatus.Assigned, stillAssigned.Status);

        var unauthorizedClose = await store.TransitionAsync(tenant, card.Id, SuggestionStatus.Closed, "operator", "operator-1", "trying to close", CancellationToken.None);
        Assert.False(unauthorizedClose.Allowed);
        Assert.Equal(SuggestionStatus.Assigned, (await store.ListAllAsync(tenant)).Single().Status);
    }

    [Fact]
    public void T047_Reject_And_Terminal_State_Machine_Rules_Are_Enforced()
    {
        Assert.True(SuggestionWorkflow.CanTransition(SuggestionStatus.Open, SuggestionStatus.Rejected, "engineer").Allowed);
        Assert.False(SuggestionWorkflow.CanTransition(SuggestionStatus.Rejected, SuggestionStatus.Accepted, "engineer").Allowed);
        Assert.False(SuggestionWorkflow.CanTransition(SuggestionStatus.Closed, SuggestionStatus.Assigned, "admin").Allowed);
        Assert.False(SuggestionWorkflow.CanTransition(SuggestionStatus.Dismissed, SuggestionStatus.Assigned, "admin").Allowed);
        Assert.False(SuggestionWorkflow.CanTransition(SuggestionStatus.Open, SuggestionStatus.Closed, "admin").Allowed);
    }

    private sealed class InMemorySuggestionStore : ISuggestionStore
    {
        private readonly Dictionary<Guid, List<SuggestionCard>> _byTenant = new();

        public List<SuggestionAuditEntry> Audit { get; } = new();

        public Task<SuggestionSyncResult> SyncAsync(
            Guid tenantId,
            IReadOnlyList<SuggestionCard> generated,
            string correlationId,
            CancellationToken cancellationToken)
        {
            if (!_byTenant.TryGetValue(tenantId, out var list))
            {
                list = new List<SuggestionCard>();
                _byTenant[tenantId] = list;
            }

            var created = 0;
            var updated = 0;
            var dismissed = 0;

            var generatedKeys = generated
                .Select(x => x.SuggestionKey)
                .ToHashSet(StringComparer.Ordinal);

            foreach (var card in generated)
            {
                var existingIndex = list.FindIndex(x =>
                    x.SuggestionKey == card.SuggestionKey &&
                    x.Status is not SuggestionStatus.Dismissed and not SuggestionStatus.Closed and not SuggestionStatus.Rejected);

                if (existingIndex >= 0)
                {
                    var currentStatus = list[existingIndex].Status;
                    list[existingIndex] = card with { Status = currentStatus };
                    updated++;
                }
                else
                {
                    list.Add(card);
                    created++;
                }
            }

            for (var i = 0; i < list.Count; i++)
            {
                var card = list[i];

                if (card.Status is SuggestionStatus.Dismissed or SuggestionStatus.Closed or SuggestionStatus.Rejected)
                    continue;

                if (generatedKeys.Contains(card.SuggestionKey))
                    continue;

                list[i] = card with { Status = SuggestionStatus.Dismissed };
                Audit.Add(new SuggestionAuditEntry(card.Id, null, SuggestionStatus.Dismissed, "suggestion-job", $"superseded; correlation {correlationId}"));
                dismissed++;
            }

            return Task.FromResult(new SuggestionSyncResult(created, updated, dismissed, correlationId));
        }

        public Task<IReadOnlyList<SuggestionCard>> ListActiveAsync(Guid tenantId, CancellationToken cancellationToken)
        {
            var active = _byTenant.TryGetValue(tenantId, out var list)
                ? list
                    .Where(x => x.Status is not SuggestionStatus.Dismissed and not SuggestionStatus.Closed and not SuggestionStatus.Rejected)
                    .OrderByDescending(x => x.ImpactHigh ?? 0m)
                    .ThenByDescending(x => x.Confidence)
                    .ThenBy(x => x.SuggestionKey, StringComparer.Ordinal)
                    .ToList()
                : new List<SuggestionCard>();

            return Task.FromResult<IReadOnlyList<SuggestionCard>>(active);
        }

        public Task<IReadOnlyList<SuggestionCard>> ListAllAsync(Guid tenantId)
        {
            var all = _byTenant.TryGetValue(tenantId, out var list)
                ? list.ToList()
                : new List<SuggestionCard>();

            return Task.FromResult<IReadOnlyList<SuggestionCard>>(all);
        }

        public Task<TransitionDecision> TransitionAsync(
            Guid tenantId,
            Guid suggestionId,
            SuggestionStatus to,
            string role,
            string actor,
            string? note,
            CancellationToken cancellationToken)
        {
            if (!_byTenant.TryGetValue(tenantId, out var list))
                return Task.FromResult(new TransitionDecision(false, "Suggestion not found for this tenant."));

            var index = list.FindIndex(x => x.Id == suggestionId);
            if (index < 0)
                return Task.FromResult(new TransitionDecision(false, "Suggestion not found for this tenant."));

            var current = list[index];
            var decision = SuggestionWorkflow.CanTransition(current.Status, to, role);

            if (!decision.Allowed)
                return Task.FromResult(decision);

            list[index] = current with { Status = to };
            Audit.Add(new SuggestionAuditEntry(current.Id, current.Status, to, actor, note));

            return Task.FromResult(decision);
        }
    }

    private sealed record SuggestionAuditEntry(
        Guid SuggestionId,
        SuggestionStatus? From,
        SuggestionStatus To,
        string Actor,
        string? Note)
    {
        public string Transition => From is null ? $"->{To}" : $"{From}->{To}";
    }
}
