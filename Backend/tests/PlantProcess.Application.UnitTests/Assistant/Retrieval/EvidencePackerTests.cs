using System;
using System.Collections.Generic;
using System.Collections.Immutable;
using System.Linq;
using PlantProcess.Application.Assistant.Planning;
using PlantProcess.Application.Assistant.Retrieval;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant.Retrieval;

/// <summary>
/// T-180. THE FIXED FALSIFICATION SET, EXECUTED.
///
/// One plan, one registry and one candidate producer serve every probe, so a probe
/// that passes does so because of the rule it aims at rather than because its fixture
/// was shaped to suit it.
/// </summary>
public sealed class EvidencePackerTests
{
    private const string PermittedTool = "layer_a.exact_count";
    private const string SecondPermittedTool = "layer_b.association";
    private const string ForbiddenTool = "layer_b.causal_effect";
    private const string Tenant = "tenant_fixture";
    private const string OtherTenant = "tenant_other";

    /// <summary>
    /// Compare ordered identifiers by CONTENT.
    ///
    /// ImmutableArray implements IEquatable and compares its underlying array by
    /// reference, and xunit prefers IEquatable over element-wise comparison. Comparing
    /// arrays takes the element-wise path, which is what these probes mean.
    /// </summary>
    private static void AssertOrdered(IEnumerable<string> expected, IEnumerable<string> actual) =>
        Assert.Equal(expected.ToArray(), actual.ToArray());

    // ------------------------------------------------------------------ fixture

    private static ToolPlan PlannedPlan(params string[] tools)
    {
        var selected = tools.Length > 0 ? tools : new[] { PermittedTool, SecondPermittedTool };
        // The two tools must not be equivalent to each other. T-179 collapses tools
        // with the same layer, exactness, claim and entity roles into one step, and a
        // fixture that ignored that would test a plan the planner never produces.
        var registry = ToolRegistry.Of(
            selected.Select((t, index) => DeclaredTool.Create(
                t,
                index == 0 ? ToolLayer.LayerA : ToolLayer.LayerB,
                index == 0 ? ToolExactness.Exact : ToolExactness.Approximate,
                ClaimClass.ObservedFact,
                "unit_scope"))
                .ToArray());

        var request = new PlanningRequest(
            PermissionContext.Of(Tenant, "process_engineer", selected),
            ResolvedIntent.Create("evidence_probe", ClaimClass.ObservedFact, false, "unit_scope"),
            ImmutableArray.Create(ResolvedEntity.Bound("unit_scope", "unit_scope_0001")),
            registry);

        return DeterministicToolPlanner.Plan(request);
    }

    private static EvidenceCandidate Candidate(
        string handle,
        string toolId = PermittedTool,
        EvidenceClass evidenceClass = EvidenceClass.RetrievedPassage,
        int tokenCost = 10,
        double exact = 0.0,
        double lexical = 0.5,
        double? semantic = 0.5,
        string? contentIdentity = null,
        string tenantId = Tenant,
        IEnumerable<string>? entityScope = null) =>
        EvidenceCandidate.Create(
            handle, tenantId, toolId, evidenceClass,
            contentIdentity ?? handle, "payload of " + handle, tokenCost,
            exact, lexical, semantic, entityScope, "provenance of " + handle);

    private static TokenBudget Budget(int total = 1000, int reserved = 200) =>
        TokenBudget.Of(total, reserved);

    private static EvidencePack Pack(
        IEnumerable<EvidenceCandidate> candidates,
        ToolPlan? plan = null,
        TokenBudget? budget = null,
        IEvidenceReranker? reranker = null,
        bool retrievalAvailable = true) =>
        EvidencePacker.Pack(
            plan ?? PlannedPlan(), candidates, budget ?? Budget(), reranker, retrievalAvailable);

    // ============================================================= P PERMISSION

    [Fact]
    public void P1_ForbiddenHighScoringEvidenceIsAbsent()
    {
        var pack = Pack(new[]
        {
            Candidate("permitted_a", lexical: 0.10, semantic: 0.10),
            Candidate("forbidden_perfect", toolId: ForbiddenTool, lexical: 1.0, semantic: 1.0)
        });

        AssertOrdered(new[] { "permitted_a" }, pack.Items.Select(i => i.EvidenceHandle));
        Assert.DoesNotContain("forbidden_perfect", pack.Omitted.Select(o => o.EvidenceHandle));
    }

    [Fact]
    public void P2_OnlyPermittedCandidatesAreEverConsidered()
    {
        var pack = Pack(new[]
        {
            Candidate("permitted_weak", lexical: 0.01, semantic: 0.01),
            Candidate("forbidden_strong", toolId: ForbiddenTool, lexical: 1.0, semantic: 1.0),
            Candidate("other_tenant", tenantId: OtherTenant, lexical: 1.0, semantic: 1.0)
        });

        Assert.Equal(1, pack.PermittedCandidateCount);
        AssertOrdered(new[] { "permitted_weak" }, pack.Items.Select(i => i.EvidenceHandle));
    }

    [Fact]
    public void P3_ChangingThePermissionSetChangesThePoolBeforeRanking()
    {
        var candidates = new[]
        {
            Candidate("from_first_tool", toolId: PermittedTool),
            Candidate("from_second_tool", toolId: SecondPermittedTool)
        };

        var wide = Pack(candidates, PlannedPlan(PermittedTool, SecondPermittedTool));
        var narrow = Pack(candidates, PlannedPlan(PermittedTool));

        Assert.Equal(2, wide.PermittedCandidateCount);
        Assert.Equal(1, narrow.PermittedCandidateCount);
        AssertOrdered(new[] { "from_first_tool" }, narrow.Items.Select(i => i.EvidenceHandle));
    }

    [Fact]
    public void P4_UnauthorisedEvidenceInfluencesNoCountOrderingOrFingerprint()
    {
        // The whole point. Adding forbidden candidates must change nothing an
        // observer can see, including anything derived from the pool.
        var permittedOnly = new[]
        {
            Candidate("permitted_a", lexical: 0.4),
            Candidate("permitted_b", lexical: 0.9)
        };

        var withForbidden = permittedOnly.Concat(new[]
        {
            Candidate("forbidden_1", toolId: ForbiddenTool, lexical: 1.0, tokenCost: 900),
            Candidate("forbidden_2", toolId: ForbiddenTool, lexical: 0.99, tokenCost: 900),
            Candidate("other_tenant", tenantId: OtherTenant, lexical: 1.0, tokenCost: 900)
        }).ToArray();

        var clean = Pack(permittedOnly);
        var contaminated = Pack(withForbidden);

        Assert.Equal(clean.PackFingerprint(), contaminated.PackFingerprint());
        Assert.Equal(clean.PermittedCandidateCount, contaminated.PermittedCandidateCount);
        Assert.Equal(clean.DistinctPermittedSourceCount, contaminated.DistinctPermittedSourceCount);
        Assert.Equal(clean.TokensUsed, contaminated.TokensUsed);
        Assert.Equal(clean.Truncated, contaminated.Truncated);
        Assert.Equal(clean.Omitted.Length, contaminated.Omitted.Length);
        AssertOrdered(
            clean.Items.Select(i => i.EvidenceHandle),
            contaminated.Items.Select(i => i.EvidenceHandle));
    }

    [Fact]
    public void P4_TheRejectedCountIsNotPublishedOnThePack()
    {
        // A count of what a caller may not see is itself a disclosure, so the pack
        // carries no property that could report it.
        var names = typeof(EvidencePack)
            .GetProperties()
            .Select(p => p.Name.ToLowerInvariant())
            .ToArray();

        Assert.DoesNotContain("rejectedbypermission", names);
        Assert.DoesNotContain("forbiddencount", names);
        Assert.DoesNotContain("filteredcount", names);
    }

    [Fact]
    public void P_ARankerCannotBeHandedAnUnfilteredPool()
    {
        // The guarantee is structural: the only public way to obtain a
        // PermittedCandidateSet is through the permission filter.
        var constructors = typeof(PermittedCandidateSet)
            .GetConstructors(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Instance);

        Assert.Empty(constructors);
    }

    // ============================================================ D DETERMINISM

    [Fact]
    public void D1_TheSameInputsProduceAnIdenticalPack()
    {
        var candidates = new[] { Candidate("a", lexical: 0.7), Candidate("b", lexical: 0.3) };

        var first = Pack(candidates);
        var second = Pack(candidates);

        Assert.Equal(first.PackFingerprint(), second.PackFingerprint());
        AssertOrdered(
            first.Items.Select(i => i.EvidenceHandle),
            second.Items.Select(i => i.EvidenceHandle));
    }

    [Fact]
    public void D2_ShuffledDeclarationOrderProducesTheIdenticalResult()
    {
        var candidates = new[]
        {
            Candidate("alpha", lexical: 0.5),
            Candidate("bravo", lexical: 0.5),
            Candidate("charlie", lexical: 0.5)
        };

        var forward = Pack(candidates);
        var reversed = Pack(candidates.Reverse().ToArray());

        Assert.Equal(forward.PackFingerprint(), reversed.PackFingerprint());
    }

    [Fact]
    public void D3_EqualScoresBreakTiesDeterministicallyByHandle()
    {
        var candidates = new[]
        {
            Candidate("zulu", lexical: 0.5, semantic: 0.5),
            Candidate("alpha", lexical: 0.5, semantic: 0.5),
            Candidate("mike", lexical: 0.5, semantic: 0.5)
        };

        var pack = Pack(candidates);

        AssertOrdered(
            new[] { "alpha", "mike", "zulu" },
            pack.Items.Select(i => i.EvidenceHandle));
    }

    [Fact]
    public void D4_RepeatedRunsProduceAnIdenticalFingerprintAndOrdering()
    {
        var candidates = new[]
        {
            Candidate("a", lexical: 0.9),
            Candidate("b", lexical: 0.9),
            Candidate("c", evidenceClass: EvidenceClass.StructuredToolResult, exact: 0.2)
        };

        var expected = Pack(candidates).PackFingerprint();
        for (var attempt = 0; attempt < 20; attempt++)
        {
            Assert.Equal(expected, Pack(candidates).PackFingerprint());
        }
    }

    // ================================================================ H HYBRID

    [Fact]
    public void H1_StructuredEvidenceOutranksAHigherScoringPassage()
    {
        var pack = Pack(new[]
        {
            Candidate("passage", evidenceClass: EvidenceClass.RetrievedPassage, lexical: 1.0, semantic: 1.0),
            Candidate("structured", evidenceClass: EvidenceClass.StructuredToolResult, exact: 0.1, lexical: 0.0, semantic: 0.0)
        });

        Assert.Equal("structured", pack.Items.First().EvidenceHandle);
    }

    [Fact]
    public void H2_EachDeclaredSignalContributesAndIsRecorded()
    {
        var pack = Pack(new[]
        {
            Candidate("all_three", evidenceClass: EvidenceClass.StructuredToolResult,
                exact: 0.8, lexical: 0.6, semantic: 0.9)
        });

        var item = Assert.Single(pack.Items);
        Assert.Contains(RetrievalSignal.Exact, item.ContributingSignals);
        Assert.Contains(RetrievalSignal.Lexical, item.ContributingSignals);
        Assert.Contains(RetrievalSignal.Semantic, item.ContributingSignals);

        var expected = (0.5 * 0.8 + 0.2 * 0.6 + 0.3 * 0.9) / (0.5 + 0.2 + 0.3);
        Assert.Equal(expected, item.FusedScore, 10);
    }

    [Fact]
    public void H3_AbsentSemanticCapabilityTakesTheDeclaredDegradedPath()
    {
        var pack = Pack(new[]
        {
            Candidate("a", lexical: 0.8, semantic: null),
            Candidate("b", lexical: 0.2, semantic: null)
        });

        Assert.False(pack.SemanticSignalAvailable);
        Assert.NotEmpty(pack.DegradedReasons);
        Assert.Contains("no semantic score was fabricated", pack.DegradedReasons.Single());

        // Renormalised over the signals that contributed, not scaled down silently.
        var top = pack.Items.First();
        Assert.Equal((0.5 * 0.0 + 0.2 * 0.8) / (0.5 + 0.2), top.FusedScore, 10);
        Assert.DoesNotContain(RetrievalSignal.Semantic, top.ContributingSignals);
    }

    [Fact]
    public void H3_UnavailableRetrievalIsNotAnEmptyResult()
    {
        var pack = Pack(new[] { Candidate("a") }, retrievalAvailable: false);

        Assert.Equal(RetrievalOutcome.RetrievalUnavailable, pack.Outcome);
        Assert.Empty(pack.Items);
        Assert.NotEmpty(pack.DegradedReasons);
        Assert.Contains("not the same as finding", pack.Reason);
    }

    [Fact]
    public void H4_ArerankerMayReorderAndMayNotAdmitOrRemoveAnything()
    {
        var candidates = new[]
        {
            Candidate("a", lexical: 0.9),
            Candidate("b", lexical: 0.1)
        };

        var pack = Pack(candidates, reranker: new ReversingReranker());

        Assert.Equal("reversing", pack.RerankerIdentity);
        AssertOrdered(new[] { "b", "a" }, pack.Items.Select(i => i.EvidenceHandle));
    }

    [Fact]
    public void H4_ArerankerCannotSmuggleInForbiddenEvidence()
    {
        var pack = Pack(
            new[] { Candidate("permitted", lexical: 0.5) },
            reranker: new SmugglingReranker(Candidate("forbidden", toolId: ForbiddenTool)));

        AssertOrdered(new[] { "permitted" }, pack.Items.Select(i => i.EvidenceHandle));
        Assert.Equal(1, pack.PermittedCandidateCount);
    }

    [Fact]
    public void H4_ArerankerThatDropsItemsCannotShrinkThePermittedSet()
    {
        var pack = Pack(
            new[] { Candidate("a", lexical: 0.9), Candidate("b", lexical: 0.1) },
            reranker: new DroppingReranker());

        Assert.Equal(2, pack.Items.Length);
    }

    // ================================================================ B BUDGET

    [Fact]
    public void B1_EverythingFitsAndNothingIsTruncated()
    {
        var pack = Pack(new[]
        {
            Candidate("a", tokenCost: 100),
            Candidate("b", tokenCost: 100)
        });

        Assert.False(pack.Truncated);
        Assert.Equal(2, pack.Items.Length);
        Assert.Equal(200, pack.TokensUsed);
        Assert.Equal(0, pack.OmittedForBudgetCount);
    }

    [Fact]
    public void B2_EvidenceThatDoesNotFitIsOmittedAndDisclosed()
    {
        var pack = Pack(
            new[]
            {
                Candidate("a", tokenCost: 60, lexical: 0.9),
                Candidate("b", tokenCost: 60, lexical: 0.5)
            },
            budget: TokenBudget.Of(120, 40));

        Assert.True(pack.Truncated);
        Assert.Equal(1, pack.Items.Length);
        Assert.Equal(1, pack.OmittedForBudgetCount);

        var omitted = pack.Omitted.Single(o => o.Reason == OmissionReason.ExceededRemainingBudget);
        Assert.Equal("b", omitted.EvidenceHandle);
    }

    [Fact]
    public void B2_NoEvidenceAndTruncatedEvidenceAreDistinguishable()
    {
        var nothing = Pack(Array.Empty<EvidenceCandidate>());
        var more = Pack(
            new[] { Candidate("a", tokenCost: 60), Candidate("b", tokenCost: 60) },
            budget: TokenBudget.Of(100, 40));

        Assert.Equal(RetrievalOutcome.NoPermittedEvidence, nothing.Outcome);
        Assert.False(nothing.Truncated);
        Assert.Equal(RetrievalOutcome.EvidencePacked, more.Outcome);
        Assert.True(more.Truncated);
    }

    [Fact]
    public void B3_AnItemLargerThanTheWholeBudgetIsHandledDeterministically()
    {
        var pack = Pack(
            new[]
            {
                Candidate("oversized", tokenCost: 5000, lexical: 1.0),
                Candidate("small", tokenCost: 10, lexical: 0.1)
            },
            budget: TokenBudget.Of(100, 40));

        // The oversized item does not empty the pack. It is skipped with its own
        // reason and packing continues.
        AssertOrdered(new[] { "small" }, pack.Items.Select(i => i.EvidenceHandle));
        Assert.True(pack.Truncated);
        Assert.Equal(
            OmissionReason.ExceedsWholeBudget,
            pack.Omitted.Single(o => o.EvidenceHandle == "oversized").Reason);
    }

    [Fact]
    public void B4_TheExactBoundaryIsDeterministicAndInclusive()
    {
        var exactlyFits = Pack(
            new[] { Candidate("a", tokenCost: 60) },
            budget: TokenBudget.Of(100, 40));

        var oneTooMany = Pack(
            new[] { Candidate("a", tokenCost: 61) },
            budget: TokenBudget.Of(100, 40));

        Assert.Single(exactlyFits.Items);
        Assert.False(exactlyFits.Truncated);
        Assert.Equal(60, exactlyFits.TokensUsed);

        Assert.Empty(oneTooMany.Items);
        Assert.True(oneTooMany.Truncated);
    }

    [Fact]
    public void B_TheReservedAnswerAllowanceIsSubtractedBeforePacking()
    {
        var budget = TokenBudget.Of(1000, 400);
        Assert.Equal(600, budget.AvailableForEvidence);

        var pack = Pack(new[] { Candidate("a", tokenCost: 700) }, budget: budget);
        Assert.Empty(pack.Items);
        Assert.True(pack.Truncated);
    }

    [Fact]
    public void B_AReservedAllowanceLargerThanTheBudgetIsRefused()
    {
        Assert.Throws<ArgumentException>(() => TokenBudget.Of(100, 200));
    }

    [Fact]
    public void B3_StructuredEvidenceSurvivesTruncationAheadOfPassages()
    {
        var pack = Pack(
            new[]
            {
                Candidate("passage", evidenceClass: EvidenceClass.RetrievedPassage, tokenCost: 50, lexical: 1.0),
                Candidate("structured", evidenceClass: EvidenceClass.StructuredToolResult, tokenCost: 50, exact: 0.1)
            },
            budget: TokenBudget.Of(100, 40));

        AssertOrdered(new[] { "structured" }, pack.Items.Select(i => i.EvidenceHandle));
        Assert.True(pack.Truncated);
    }

    // ============================================================== E IDENTITY

    [Fact]
    public void E1_EveryPackedItemCarriesAStableHandle()
    {
        var pack = Pack(new[] { Candidate("a"), Candidate("b") });

        Assert.All(pack.Items, item => Assert.False(string.IsNullOrWhiteSpace(item.EvidenceHandle)));
        Assert.Equal(pack.Items.Length, pack.Items.Select(i => i.EvidenceHandle).Distinct().Count());
    }

    [Fact]
    public void E2_ProvenanceAndScopeSurvivePacking()
    {
        var pack = Pack(new[]
        {
            Candidate("a", entityScope: new[] { "unit_scope_0001" })
        });

        var item = Assert.Single(pack.Items);
        Assert.Equal("provenance of a", item.Provenance);
        Assert.Equal(PermittedTool, item.ToolId);
        AssertOrdered(new[] { "unit_scope_0001" }, item.EntityScope);
        Assert.Equal("payload of a", item.Payload);
    }

    [Fact]
    public void E3_DuplicateEvidenceCollapsesWithoutLosingAnyHandle()
    {
        var pack = Pack(new[]
        {
            Candidate("handle_one", contentIdentity: "shared_content", lexical: 0.9),
            Candidate("handle_two", contentIdentity: "shared_content", lexical: 0.9)
        });

        var item = Assert.Single(pack.Items);
        AssertOrdered(new[] { "handle_one", "handle_two" }, item.MergedHandles);
        Assert.Equal(
            OmissionReason.CollapsedAsDuplicate,
            pack.Omitted.Single(o => o.EvidenceHandle == "handle_two").Reason);
    }

    [Fact]
    public void E4_TheSameEvidenceCannotAppearAsTwoIndependentSources()
    {
        var pack = Pack(new[]
        {
            Candidate("handle_one", contentIdentity: "shared_content"),
            Candidate("handle_two", contentIdentity: "shared_content"),
            Candidate("handle_three", contentIdentity: "different_content")
        });

        Assert.Equal(2, pack.DistinctPermittedSourceCount);
        Assert.Equal(2, pack.Items.Length);
        Assert.All(pack.Items, item => Assert.Equal(1, item.DistinctSourceCount));
    }

    [Fact]
    public void E_CollapsingIsNotCountedAsBudgetTruncation()
    {
        var pack = Pack(new[]
        {
            Candidate("handle_one", contentIdentity: "shared", tokenCost: 10),
            Candidate("handle_two", contentIdentity: "shared", tokenCost: 10)
        });

        Assert.False(pack.Truncated);
        Assert.Equal(0, pack.OmittedForBudgetCount);
        Assert.Single(pack.Omitted);
    }

    // ============================================================== PLAN GATE

    [Fact]
    public void APlanRequiringClarificationIsNotExecuted()
    {
        var registry = ToolRegistry.Of(
            DeclaredTool.Create(PermittedTool, ToolLayer.LayerA, ToolExactness.Exact, ClaimClass.ObservedFact, "unit_scope"));

        var request = new PlanningRequest(
            PermissionContext.Of(Tenant, "process_engineer", PermittedTool),
            ResolvedIntent.Create("evidence_probe", ClaimClass.ObservedFact, false, "unit_scope"),
            ImmutableArray.Create(ResolvedEntity.Ambiguous("unit_scope", "one", "two")),
            registry);

        var plan = DeterministicToolPlanner.Plan(request);
        var pack = Pack(new[] { Candidate("a") }, plan);

        Assert.Equal(PlanningOutcome.ClarificationRequired, plan.Outcome);
        Assert.Equal(RetrievalOutcome.PlanNotExecutable, pack.Outcome);
        Assert.Empty(pack.Items);
        Assert.Contains("never repairs", pack.Reason);
    }

    [Fact]
    public void TheBenchmarkHooksMeasureAndDeclareNothing()
    {
        var candidates = new[] { Candidate("a"), Candidate("b") };
        var plan = PlannedPlan();

        var (pack, measurement) = RetrievalBenchmarkHooks.MeasureRetrieval(
            () => EvidencePacker.Pack(plan, candidates, Budget()));

        Assert.Equal("B-07", measurement.BenchmarkId);
        Assert.Equal(pack.PackFingerprint(), measurement.PackFingerprint);
        Assert.Equal(pack.Items.Length, measurement.PackedItemCount);

        var names = typeof(RetrievalMeasurement).GetProperties().Select(p => p.Name.ToLowerInvariant());
        Assert.DoesNotContain("verdict", names);
        Assert.DoesNotContain("winner", names);
        Assert.DoesNotContain("recommended", names);
    }

    [Fact]
    public void TheSeamIsMeasuredWithAndWithoutAndNeitherIsDeclaredBetter()
    {
        var candidates = new[] { Candidate("a", lexical: 0.9), Candidate("b", lexical: 0.1) };
        var plan = PlannedPlan();

        var measurements = RetrievalBenchmarkHooks.MeasureRerankerSeam(
            reranker => EvidencePacker.Pack(plan, candidates, Budget(), reranker),
            new ReversingReranker());

        Assert.Equal(2, measurements.Length);
        Assert.Equal("none", measurements[0].RerankerIdentity);
        Assert.Equal("reversing", measurements[1].RerankerIdentity);
        Assert.NotEqual(measurements[0].PackFingerprint, measurements[1].PackFingerprint);
    }

    // --------------------------------------------------------------- test seams

    private sealed class ReversingReranker : IEvidenceReranker
    {
        public string RerankerIdentity => "reversing";

        public ImmutableArray<EvidenceCandidate> Rerank(
            ResolvedIntent intent, ImmutableArray<EvidenceCandidate> ordered) =>
            ordered.Reverse().ToImmutableArray();
    }

    private sealed class SmugglingReranker : IEvidenceReranker
    {
        private readonly EvidenceCandidate _smuggled;

        public SmugglingReranker(EvidenceCandidate smuggled) => _smuggled = smuggled;

        public string RerankerIdentity => "smuggling";

        public ImmutableArray<EvidenceCandidate> Rerank(
            ResolvedIntent intent, ImmutableArray<EvidenceCandidate> ordered) =>
            ordered.Insert(0, _smuggled);
    }

    private sealed class DroppingReranker : IEvidenceReranker
    {
        public string RerankerIdentity => "dropping";

        public ImmutableArray<EvidenceCandidate> Rerank(
            ResolvedIntent intent, ImmutableArray<EvidenceCandidate> ordered) =>
            ImmutableArray<EvidenceCandidate>.Empty;
    }
}
