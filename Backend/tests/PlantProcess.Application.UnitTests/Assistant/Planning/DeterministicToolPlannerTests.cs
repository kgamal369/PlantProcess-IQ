using System;
using System.Collections.Immutable;
using System.Linq;
using System.Reflection;
using PlantProcess.Application.Assistant.Planning;
using Xunit;

namespace PlantProcess.Application.UnitTests.Assistant.Planning;

/// <summary>
/// T-179. THE FIXED FALSIFICATION SET, EXECUTED.
///
/// One registry and one permission context serve every probe, so a probe that passes
/// does so because of the rule it is aiming at rather than because its fixture was
/// built to suit it.
/// </summary>
public sealed class DeterministicToolPlannerTests
{
    private const string ExactCountTool = "layer_a.exact_count";
    private const string ExactKpiTool = "layer_a.exact_kpi";
    private const string AssociationTool = "layer_b.association";
    private const string CausalTool = "layer_b.causal_effect";
    private const string PredictionTool = "layer_b.prediction";
    private const string SimilarityTool = "layer_b.similarity";
    private const string RemediationTool = "layer_b.remediation_candidate";
    private const string EquivalentCountTool = "layer_a.exact_count_duplicate";

    /// <summary>
    /// Compare ordered identifiers by CONTENT.
    ///
    /// ImmutableArray&lt;T&gt; implements IEquatable&lt;ImmutableArray&lt;T&gt;&gt; and
    /// compares the underlying array by reference. xunit prefers IEquatable over
    /// element-wise comparison, so Assert.Equal on two immutable arrays with identical
    /// contents fails and prints two identical-looking collections. Comparing arrays
    /// takes the element-wise path, which is what these probes actually mean.
    /// </summary>
    private static void AssertOrdered(IEnumerable<string> expected, IEnumerable<string> actual) =>
        Assert.Equal(expected.ToArray(), actual.ToArray());

    private static void AssertNotOrdered(IEnumerable<string> left, IEnumerable<string> right) =>
        Assert.NotEqual(left.ToArray(), right.ToArray());

    private static ToolRegistry Registry() => ToolRegistry.Of(
        DeclaredTool.Create(ExactCountTool, ToolLayer.LayerA, ToolExactness.Exact, ClaimClass.ObservedFact, "unit_scope", "time_window"),
        DeclaredTool.Create(ExactKpiTool, ToolLayer.LayerA, ToolExactness.Exact, ClaimClass.ObservedFact, "unit_scope", "time_window", "measure"),
        DeclaredTool.Create(AssociationTool, ToolLayer.LayerB, ToolExactness.Approximate, ClaimClass.Association, "unit_scope", "parameter"),
        DeclaredTool.Create(CausalTool, ToolLayer.LayerB, ToolExactness.Approximate, ClaimClass.CausalEffect, "unit_scope", "parameter"),
        DeclaredTool.Create(PredictionTool, ToolLayer.LayerB, ToolExactness.Approximate, ClaimClass.Prediction, "unit_scope"),
        DeclaredTool.Create(SimilarityTool, ToolLayer.LayerB, ToolExactness.Approximate, ClaimClass.Similarity, "unit_scope"),
        DeclaredTool.Create(RemediationTool, ToolLayer.LayerB, ToolExactness.Approximate, ClaimClass.RemediationCandidate, "unit_scope"));

    private static PermissionContext FullyPermitted() => PermissionContext.Of(
        "tenant_fixture",
        "process_engineer",
        ExactCountTool, ExactKpiTool, AssociationTool, CausalTool, PredictionTool,
        SimilarityTool, RemediationTool, EquivalentCountTool);

    private static ImmutableArray<ResolvedEntity> BoundEntities() => ImmutableArray.Create(
        ResolvedEntity.Bound("unit_scope", "unit_scope_0001"),
        ResolvedEntity.Bound("time_window", "window_0001"),
        ResolvedEntity.Bound("measure", "measure_0001"),
        ResolvedEntity.Bound("parameter", "parameter_0001"));

    private static ResolvedIntent ExactCountIntent() => ResolvedIntent.Create(
        "exact_unit_count", ClaimClass.ObservedFact, requiresExactValue: true,
        "unit_scope", "time_window");

    private static ResolvedIntent AssociationIntent() => ResolvedIntent.Create(
        "parameter_outcome_association", ClaimClass.Association, requiresExactValue: false,
        "unit_scope", "parameter");

    private static PlanningRequest Request(
        ResolvedIntent? intent = null,
        PermissionContext? permission = null,
        ImmutableArray<ResolvedEntity>? entities = null,
        ToolRegistry? registry = null) =>
        new(
            permission ?? FullyPermitted(),
            intent ?? ExactCountIntent(),
            entities ?? BoundEntities(),
            registry ?? Registry());

    // ---------------------------------------------------------------- probe A

    [Fact]
    public void ProbeA_EquivalentMeaningProducesAnIdenticalPlan()
    {
        // Two questions a person might type. The planner never sees either: both are
        // resolved upstream to the same intent and entities, and the plan is a function
        // of that resolution alone.
        const string paraphraseOne = "how many units did we run last week";
        const string paraphraseTwo = "give me the unit count for the previous week";

        var first = DeterministicToolPlanner.Plan(Request());
        var second = DeterministicToolPlanner.Plan(Request());

        Assert.NotEqual(paraphraseOne, paraphraseTwo);
        Assert.Equal(first.PlanFingerprint(), second.PlanFingerprint());
        AssertOrdered(first.SelectedToolIds, second.SelectedToolIds);
        Assert.Equal(first.Outcome, second.Outcome);
    }

    [Fact]
    public void ProbeA_ThePlannerCannotSeeTheQuestionText()
    {
        // The strongest form of the guarantee: there is no field through which wording
        // could reach the planner, so it cannot influence a plan.
        var suspicious = typeof(PlanningRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name.ToLowerInvariant())
            .Where(name =>
                name.Contains("text") || name.Contains("question") || name.Contains("prompt")
                || name.Contains("utterance") || name.Contains("raw"))
            .ToArray();

        Assert.Empty(suspicious);
    }

    // ---------------------------------------------------------------- probe B

    [Fact]
    public void ProbeB_ADeniedToolIsAbsentFromThePlan()
    {
        var restricted = PermissionContext.Of("tenant_fixture", "viewer", ExactKpiTool);
        var plan = DeterministicToolPlanner.Plan(Request(permission: restricted));

        Assert.Equal(PlanningOutcome.Planned, plan.Outcome);
        Assert.DoesNotContain(ExactCountTool, plan.SelectedToolIds);
        Assert.Contains(ExactKpiTool, plan.SelectedToolIds);

        var denied = plan.Decisions.Single(d => d.ToolId == ExactCountTool);
        Assert.Equal(ToolDecisionCode.OmittedNotPermitted, denied.Code);
    }

    [Fact]
    public void ProbeB_TheSameQuestionUnderTwoPermissionSetsPlansDifferently()
    {
        var wide = DeterministicToolPlanner.Plan(Request());
        var narrow = DeterministicToolPlanner.Plan(
            Request(permission: PermissionContext.Of("tenant_fixture", "viewer", ExactKpiTool)));

        AssertNotOrdered(wide.SelectedToolIds, narrow.SelectedToolIds);
        Assert.NotEqual(wide.PlanFingerprint(), narrow.PlanFingerprint());
    }

    // ---------------------------------------------------------------- probe C

    [Fact]
    public void ProbeC_AnAmbiguousEntityRequiresClarificationAndPlansNothing()
    {
        var ambiguous = ImmutableArray.Create(
            ResolvedEntity.Ambiguous("unit_scope", "unit_scope_0002", "unit_scope_0001"),
            ResolvedEntity.Bound("time_window", "window_0001"));

        var plan = DeterministicToolPlanner.Plan(Request(entities: ambiguous));

        Assert.Equal(PlanningOutcome.ClarificationRequired, plan.Outcome);
        Assert.Empty(plan.SelectedToolIds);
        Assert.Empty(plan.Decisions);

        var clarification = Assert.Single(plan.Clarifications);
        Assert.Equal("unit_scope", clarification.Role);
        Assert.Equal(2, clarification.Candidates.Length);
    }

    [Fact]
    public void ProbeC_TheFirstCandidateIsNeverChosenSilently()
    {
        var ambiguous = ImmutableArray.Create(
            ResolvedEntity.Ambiguous("unit_scope", "unit_scope_0001", "unit_scope_0002"),
            ResolvedEntity.Bound("time_window", "window_0001"));

        var plan = DeterministicToolPlanner.Plan(Request(entities: ambiguous));

        Assert.Equal(PlanningOutcome.ClarificationRequired, plan.Outcome);
        Assert.All(plan.Entities, e => Assert.True(e.Role != "unit_scope" || !e.IsBound));
        AssertOrdered(
            new[] { "unit_scope_0001", "unit_scope_0002" },
            plan.Clarifications.Single().Candidates);
    }

    [Fact]
    public void ProbeC_AMissingRequiredRoleAlsoRequiresClarification()
    {
        var missing = ImmutableArray.Create(ResolvedEntity.Bound("unit_scope", "unit_scope_0001"));
        var plan = DeterministicToolPlanner.Plan(Request(entities: missing));

        Assert.Equal(PlanningOutcome.ClarificationRequired, plan.Outcome);
        Assert.Equal("time_window", plan.Clarifications.Single().Role);
    }

    // ---------------------------------------------------------------- probe D

    [Fact]
    public void ProbeD_AnUnsupportedIntentIsRefusedRatherThanApproximated()
    {
        var unsupported = ResolvedIntent.Create(
            "novelty_explanation", ClaimClass.Novelty, requiresExactValue: false, "unit_scope");

        var plan = DeterministicToolPlanner.Plan(Request(intent: unsupported));

        Assert.Equal(PlanningOutcome.Unsupported, plan.Outcome);
        Assert.Empty(plan.SelectedToolIds);
        Assert.Contains("No generic fallback is planned", plan.Reason);
        Assert.All(plan.Decisions, d => Assert.NotEqual(ToolDecisionCode.Selected, d.Code));
    }

    // ---------------------------------------------------------------- probe E

    [Fact]
    public void ProbeE_AnExactFactSelectsOnlyExactStructuredTools()
    {
        var plan = DeterministicToolPlanner.Plan(Request());

        Assert.Equal(PlanningOutcome.Planned, plan.Outcome);
        AssertOrdered(new[] { ExactCountTool, ExactKpiTool }, plan.SelectedToolIds);
    }

    [Fact]
    public void ProbeE_AnApproximateToolIsNeverPlannedForAnExactValue()
    {
        var registry = ToolRegistry.Of(
            DeclaredTool.Create(ExactCountTool, ToolLayer.LayerA, ToolExactness.Exact, ClaimClass.ObservedFact, "unit_scope", "time_window"),
            DeclaredTool.Create("layer_b.estimated_count", ToolLayer.LayerB, ToolExactness.Approximate, ClaimClass.ObservedFact, "unit_scope", "time_window"));

        var permission = PermissionContext.Of(
            "tenant_fixture", "process_engineer", ExactCountTool, "layer_b.estimated_count");

        var plan = DeterministicToolPlanner.Plan(Request(permission: permission, registry: registry));

        AssertOrdered(new[] { ExactCountTool }, plan.SelectedToolIds);
        var omitted = plan.Decisions.Single(d => d.ToolId == "layer_b.estimated_count");
        Assert.Equal(ToolDecisionCode.OmittedApproximateForExactValue, omitted.Code);
    }

    [Fact]
    public void ProbeE_AnExactStructuredToolIsOrderedBeforeAnIntelligenceTool()
    {
        var registry = ToolRegistry.Of(
            DeclaredTool.Create("zz_layer_b.observed", ToolLayer.LayerB, ToolExactness.Approximate, ClaimClass.ObservedFact, "unit_scope"),
            DeclaredTool.Create("aa_layer_a.observed", ToolLayer.LayerA, ToolExactness.Exact, ClaimClass.ObservedFact, "unit_scope"));

        var intent = ResolvedIntent.Create(
            "observed_summary", ClaimClass.ObservedFact, requiresExactValue: false, "unit_scope");

        var permission = PermissionContext.Of(
            "tenant_fixture", "process_engineer", "zz_layer_b.observed", "aa_layer_a.observed");

        var plan = DeterministicToolPlanner.Plan(
            Request(intent: intent, permission: permission, registry: registry));

        AssertOrdered(
            new[] { "aa_layer_a.observed", "zz_layer_b.observed" },
            plan.SelectedToolIds);
    }

    // ---------------------------------------------------------------- probe F

    [Fact]
    public void ProbeF_ASupportedIntelligenceIntentSelectsTheDeclaredMatchingTool()
    {
        var plan = DeterministicToolPlanner.Plan(Request(intent: AssociationIntent()));

        Assert.Equal(PlanningOutcome.Planned, plan.Outcome);
        AssertOrdered(new[] { AssociationTool }, plan.SelectedToolIds);
    }

    [Fact]
    public void ProbeF_AClaimIsNeverUpgraded()
    {
        // The four upgrades the architecture forbids, each refused by name.
        var upgrades = new[]
        {
            (Intent: ClaimClass.CausalEffect, Forbidden: AssociationTool),
            (Intent: ClaimClass.ObservedFact, Forbidden: PredictionTool),
            (Intent: ClaimClass.CausalEffect, Forbidden: SimilarityTool),
            (Intent: ClaimClass.ObservedFact, Forbidden: RemediationTool)
        };

        foreach (var upgrade in upgrades)
        {
            var intent = ResolvedIntent.Create(
                "claim_boundary_probe", upgrade.Intent, requiresExactValue: false,
                "unit_scope", "parameter");

            var plan = DeterministicToolPlanner.Plan(Request(intent: intent));

            Assert.DoesNotContain(upgrade.Forbidden, plan.SelectedToolIds);
            var decision = plan.Decisions.Single(d => d.ToolId == upgrade.Forbidden);
            Assert.Equal(ToolDecisionCode.OmittedClaimMismatch, decision.Code);
        }
    }

    [Fact]
    public void ProbeF_ACausalIntentSelectsOnlyTheCausalTool()
    {
        var intent = ResolvedIntent.Create(
            "parameter_causal_effect", ClaimClass.CausalEffect, requiresExactValue: false,
            "unit_scope", "parameter");

        var plan = DeterministicToolPlanner.Plan(Request(intent: intent));

        AssertOrdered(new[] { CausalTool }, plan.SelectedToolIds);
    }

    // ---------------------------------------------------------------- probe G

    [Fact]
    public void ProbeG_APerfectCapabilityMatchIsStillNeverPlannedWhenForbidden()
    {
        var permission = PermissionContext.Of("tenant_fixture", "viewer", ExactCountTool);
        var plan = DeterministicToolPlanner.Plan(
            Request(intent: AssociationIntent(), permission: permission));

        Assert.Equal(PlanningOutcome.Unsupported, plan.Outcome);
        Assert.Empty(plan.SelectedToolIds);

        var denied = plan.Decisions.Single(d => d.ToolId == AssociationTool);
        Assert.Equal(ToolDecisionCode.OmittedNotPermitted, denied.Code);
        Assert.Contains("whatever its capability", denied.Reason);
    }

    [Fact]
    public void ProbeG_ADeniedToolIsNotEvaluatedForFit()
    {
        // The omission reason must not describe a capability the caller may not know
        // this tenant has. Permission is checked first and nothing else is reported.
        var permission = PermissionContext.Of("tenant_fixture", "viewer", ExactCountTool);
        var plan = DeterministicToolPlanner.Plan(
            Request(intent: AssociationIntent(), permission: permission));

        var denied = plan.Decisions.Single(d => d.ToolId == CausalTool);
        Assert.Equal(ToolDecisionCode.OmittedNotPermitted, denied.Code);
        Assert.DoesNotContain("CausalEffect", denied.Reason);
    }

    // ---------------------------------------------------------------- probe H

    [Fact]
    public void ProbeH_AReorderedRegistryProducesTheIdenticalPlan()
    {
        var forward = Registry();
        var reversed = ToolRegistry.Of(forward.Tools.Reverse().ToArray());

        var first = DeterministicToolPlanner.Plan(Request(registry: forward));
        var second = DeterministicToolPlanner.Plan(Request(registry: reversed));

        AssertOrdered(first.SelectedToolIds, second.SelectedToolIds);
        Assert.Equal(first.PlanFingerprint(), second.PlanFingerprint());
        Assert.Equal(
            first.Decisions.Select(d => d.ToolId),
            second.Decisions.Select(d => d.ToolId));
    }

    [Fact]
    public void ProbeH_EntityOrderDoesNotMoveThePlan()
    {
        var forward = BoundEntities();
        var reversed = forward.Reverse().ToImmutableArray();

        Assert.Equal(
            DeterministicToolPlanner.Plan(Request(entities: forward)).PlanFingerprint(),
            DeterministicToolPlanner.Plan(Request(entities: reversed)).PlanFingerprint());
    }

    // ---------------------------------------------------------------- probe I

    [Fact]
    public void ProbeI_AnEquivalentRegistryCandidateProducesNoDuplicateStep()
    {
        var registry = ToolRegistry.Of(
            DeclaredTool.Create(ExactCountTool, ToolLayer.LayerA, ToolExactness.Exact, ClaimClass.ObservedFact, "unit_scope", "time_window"),
            DeclaredTool.Create(EquivalentCountTool, ToolLayer.LayerA, ToolExactness.Exact, ClaimClass.ObservedFact, "unit_scope", "time_window"));

        var plan = DeterministicToolPlanner.Plan(Request(registry: registry));

        AssertOrdered(new[] { ExactCountTool }, plan.SelectedToolIds);
        var omitted = plan.Decisions.Single(d => d.ToolId == EquivalentCountTool);
        Assert.Equal(ToolDecisionCode.OmittedEquivalentAlreadySelected, omitted.Code);
    }

    [Fact]
    public void ProbeI_WhichEquivalentSurvivesDoesNotDependOnDeclarationOrder()
    {
        var forward = ToolRegistry.Of(
            DeclaredTool.Create(ExactCountTool, ToolLayer.LayerA, ToolExactness.Exact, ClaimClass.ObservedFact, "unit_scope", "time_window"),
            DeclaredTool.Create(EquivalentCountTool, ToolLayer.LayerA, ToolExactness.Exact, ClaimClass.ObservedFact, "unit_scope", "time_window"));

        var reversed = ToolRegistry.Of(forward.Tools.Reverse().ToArray());

        AssertOrdered(
            DeterministicToolPlanner.Plan(Request(registry: forward)).SelectedToolIds,
            DeterministicToolPlanner.Plan(Request(registry: reversed)).SelectedToolIds);
    }

    [Fact]
    public void ProbeI_ARegistryDeclaringOneIdentifierTwiceIsRefused()
    {
        var tool = DeclaredTool.Create(
            ExactCountTool, ToolLayer.LayerA, ToolExactness.Exact, ClaimClass.ObservedFact, "unit_scope");

        Assert.Throws<ArgumentException>(() => ToolRegistry.Of(tool, tool));
    }

    // --------------------------------------------------------------- auditing

    [Fact]
    public void EveryDeclaredToolAppearsInTheAuditWithAVerdictAndASentence()
    {
        var plan = DeterministicToolPlanner.Plan(Request());

        Assert.Equal(Registry().Tools.Length, plan.Decisions.Length);
        Assert.All(plan.Decisions, d => Assert.False(string.IsNullOrWhiteSpace(d.Reason)));
        Assert.Equal(
            plan.Decisions.Select(d => d.ToolId).OrderBy(id => id, StringComparer.Ordinal),
            plan.Decisions.Select(d => d.ToolId));
    }

    [Fact]
    public void ThePlanRecordsTheTenantTheRoleAndTheCanonicalEntitiesItUsed()
    {
        var plan = DeterministicToolPlanner.Plan(Request());

        Assert.Equal("tenant_fixture", plan.TenantId);
        Assert.Equal("process_engineer", plan.CallerRole);
        Assert.Equal("exact_unit_count", plan.Intent.IntentCode);
        Assert.Contains(plan.Entities, e => e.Role == "unit_scope" && e.CanonicalId == "unit_scope_0001");
    }

    [Fact]
    public void ThePlanCarriesNoEvidenceAndNoAnswer()
    {
        var forbidden = typeof(ToolPlan)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name.ToLowerInvariant())
            .Where(name =>
                name.Contains("evidence") || name.Contains("chunk") || name.Contains("answer")
                || name.Contains("document") || name.Contains("citation"))
            .ToArray();

        Assert.Empty(forbidden);
    }
}
