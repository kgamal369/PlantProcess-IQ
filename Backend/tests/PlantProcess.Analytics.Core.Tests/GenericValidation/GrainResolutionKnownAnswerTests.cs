// Analysis Subject and Grain contract - deterministic acceptance.
//
// Backlog origin: T-209.
//
// The generic validation fixture is the oracle. Its duration-weighted answer is 365/3
// and its unweighted answer is 340/3, which is exactly what a hidden grain conversion
// would return: plausible, confident and wrong. The contract's job is to make that
// number unreachable without a declaration - not to outlaw the arithmetic mean, which
// is legitimate wherever a customer explicitly declares it.
using System;
using System.Linq;
using PlantProcess.Analytics.Core.Kernel;
using Xunit;

namespace PlantProcess.Analytics.Core.Tests.GenericValidation;

[Trait("BacklogTask", "T-209")]
public sealed class GrainResolutionKnownAnswerTests
{
    // Opaque keys throughout. A reader cannot infer an industry from this test, which is
    // the point: the contract must work identically whatever the customer calls things.
    private const string ChildGrainKey = "GRAIN-LEVEL-1";
    private const string ParentGrainKey = "GRAIN-LEVEL-0";
    private const string SubjectKey = "SUBJECT-A";
    private const string DurationWeightedCode = "duration_weighted";
    private const string WeightKey = "duration_minutes";

    private static readonly GrainIdentifier Child = GrainIdentifier.Declared(ChildGrainKey);
    private static readonly GrainIdentifier Parent = GrainIdentifier.Declared(ParentGrainKey);

    private static AnalysisSubjectAndGrainRegistry RegistryWithLineage()
    {
        var registry = new AnalysisSubjectAndGrainRegistry();

        Assert.True(registry.TryDeclareGrain(new GrainDefinition(Parent, GrainIdentifier.Undeclared), out _));
        Assert.True(registry.TryDeclareGrain(new GrainDefinition(Child, Parent), out _));
        Assert.True(registry.TryDeclareSubject(new AnalysisSubjectDefinition(SubjectKey, Child), out _));

        return registry;
    }

    private static void DeclareRollUp(AnalysisSubjectAndGrainRegistry registry)
    {
        Assert.True(registry.TryDeclareTransformation(
            new GrainTransformation(Child, Parent, DurationWeightedCode, WeightKey),
            out _));
    }

    // ---------------------------------------------------------------- fail closed

    [Fact]
    public void The_registry_starts_empty_with_no_subject_grain_or_lineage_of_any_kind()
    {
        var registry = new AnalysisSubjectAndGrainRegistry();

        Assert.Equal(0, registry.SubjectCount);
        Assert.Equal(0, registry.GrainCount);
        Assert.Equal(0, registry.TransformationCount);

        foreach (var key in new[] { "SUBJECT-A", "anything", "" })
        {
            Assert.False(GrainResolutionKernel.Resolve(registry, key, Parent).IsResolved);
        }
    }

    [Fact]
    public void An_undeclared_subject_fails_closed()
    {
        var registry = RegistryWithLineage();

        var resolution = GrainResolutionKernel.Resolve(registry, "SUBJECT-NEVER-DECLARED", Child);

        Assert.False(resolution.IsResolved);
        Assert.Null(resolution.Plan);
        Assert.Equal(GrainContractCodes.SubjectNotDeclared, resolution.Verdict.Code);
        Assert.Equal(TerminalState.RefusedByGuard, resolution.Verdict.Outcome);

        // A missing declaration is never reported as a property of the customer's data.
        Assert.Equal(ExclusionAttribution.Declaration, resolution.Verdict.Attribution);
    }

    [Fact]
    public void An_undeclared_grain_fails_closed()
    {
        var registry = RegistryWithLineage();

        foreach (var candidate in new[] { GrainIdentifier.Undeclared, GrainIdentifier.Declared("GRAIN-NEVER-DECLARED") })
        {
            var resolution = GrainResolutionKernel.Resolve(registry, SubjectKey, candidate);

            Assert.False(resolution.IsResolved);
            Assert.Equal(GrainContractCodes.GrainNotDeclared, resolution.Verdict.Code);
            Assert.Equal(ExclusionAttribution.Declaration, resolution.Verdict.Attribution);
        }
    }

    [Fact]
    public void An_empty_or_whitespace_grain_key_can_never_become_a_grain()
    {
        foreach (var candidate in new string?[] { null, "", "   " })
        {
            Assert.False(GrainIdentifier.TryCreate(candidate, out var identifier));
            Assert.False(identifier.IsDeclared);
            Assert.Same(GrainIdentifier.Undeclared, identifier);
        }

        Assert.Throws<ArgumentException>(() => GrainIdentifier.Declared("  "));
    }

    [Fact]
    public void A_refusal_never_carries_a_value_that_could_be_mistaken_for_zero()
    {
        var registry = RegistryWithLineage();

        var resolution = GrainResolutionKernel.Resolve(registry, "SUBJECT-NEVER-DECLARED", Parent);

        Assert.False(resolution.IsResolved);
        Assert.Null(resolution.Plan);
        Assert.Null(resolution.Verdict.Transformation);
        Assert.False(resolution.Verdict.RequiresTransformation);
    }

    // ------------------------------------------------------------- compatibility

    [Fact]
    public void Same_grain_requires_no_transformation()
    {
        var registry = RegistryWithLineage();

        var resolution = GrainResolutionKernel.Resolve(registry, SubjectKey, Child);

        Assert.True(resolution.IsResolved);
        Assert.NotNull(resolution.Plan);
        Assert.False(resolution.Plan!.RequiresTransformation);
        Assert.Null(resolution.Plan.Transformation);
        Assert.Equal(GrainContractCodes.SameGrain, resolution.Verdict.Code);
    }

    [Fact]
    public void Declared_lineage_is_not_permission_to_cross_it()
    {
        var registry = RegistryWithLineage();

        var resolution = GrainResolutionKernel.Resolve(registry, SubjectKey, Parent);

        Assert.False(resolution.IsResolved);
        Assert.Null(resolution.Plan);
        Assert.Equal(GrainRelationship.TargetIsAncestor, resolution.Verdict.Relationship);
        Assert.Equal(GrainContractCodes.TransformationNotDeclared, resolution.Verdict.Code);
    }

    [Fact]
    public void A_declared_transformation_permits_the_crossing_and_carries_its_code()
    {
        var registry = RegistryWithLineage();
        DeclareRollUp(registry);

        var resolution = GrainResolutionKernel.Resolve(registry, SubjectKey, Parent);

        Assert.True(resolution.IsResolved);
        Assert.True(resolution.Plan!.RequiresTransformation);
        Assert.Equal(DurationWeightedCode, resolution.Plan.Transformation!.TransformationCode);
        Assert.Equal(WeightKey, resolution.Plan.Transformation.WeightKey);
        Assert.Equal(GrainContractCodes.TransformationDeclared, resolution.Verdict.Code);
    }

    [Fact]
    public void Compatibility_is_directional()
    {
        var registry = RegistryWithLineage();
        DeclareRollUp(registry);

        Assert.True(GrainResolutionKernel.Compatibility(registry, Child, Parent).IsPermitted);

        var downward = GrainResolutionKernel.Compatibility(registry, Parent, Child);
        Assert.False(downward.IsPermitted);
        Assert.Equal(GrainRelationship.TargetIsDescendant, downward.Relationship);
        Assert.Equal(GrainContractCodes.TransformationNotDeclared, downward.Code);
    }

    [Fact]
    public void Unrelated_grains_refuse_and_are_distinct_from_undeclared_ones()
    {
        var registry = RegistryWithLineage();
        var sibling = GrainIdentifier.Declared("GRAIN-OTHER-BRANCH");
        Assert.True(registry.TryDeclareGrain(new GrainDefinition(sibling, GrainIdentifier.Undeclared), out _));

        var verdict = GrainResolutionKernel.Compatibility(registry, Child, sibling);

        Assert.False(verdict.IsPermitted);
        Assert.Equal(GrainRelationship.Unrelated, verdict.Relationship);
        Assert.Equal(GrainContractCodes.IncompatibleGrain, verdict.Code);

        // Somebody has spoken and the answer is no. That is not the same as silence.
        Assert.NotEqual(GrainRelationship.Undeclared, verdict.Relationship);
    }

    [Fact]
    public void No_lineage_is_inferred_from_what_a_grain_is_called()
    {
        // Two keys a human would read as an obvious hierarchy. The contract sees two
        // unrelated declarations, because nobody declared a relationship.
        var registry = new AnalysisSubjectAndGrainRegistry();
        var outer = GrainIdentifier.Declared("lot");
        var inner = GrainIdentifier.Declared("batch");

        Assert.True(registry.TryDeclareGrain(new GrainDefinition(outer, GrainIdentifier.Undeclared), out _));
        Assert.True(registry.TryDeclareGrain(new GrainDefinition(inner, GrainIdentifier.Undeclared), out _));

        Assert.Equal(GrainRelationship.Unrelated, GrainResolutionKernel.Relate(registry, inner, outer));
        Assert.False(GrainResolutionKernel.Compatibility(registry, inner, outer).IsPermitted);
    }

    // ------------------------------------------------- transformation vocabulary

    [Fact]
    public void An_arithmetic_mean_is_declarable_because_the_contract_owns_no_mathematics()
    {
        // The law is that no transformation is chosen automatically, not that a mean is
        // universally invalid. Where a customer's semantics genuinely call for one, they
        // declare it, and this contract stores it without opinion.
        var registry = RegistryWithLineage();

        Assert.True(registry.TryDeclareTransformation(
            new GrainTransformation(Child, Parent, "arithmetic_mean", string.Empty), out _));

        var plan = GrainResolutionKernel.Resolve(registry, SubjectKey, Parent).Plan;

        Assert.NotNull(plan);
        Assert.Equal("arithmetic_mean", plan!.Transformation!.TransformationCode);
    }

    [Fact]
    public void Any_declared_code_is_carried_through_untouched_and_uninterpreted()
    {
        // Whether these mean anything, and whether they can be executed, is the
        // aggregation semantics kernel's judgement. This contract neither validates nor
        // ranks them - it only refuses to invent one.
        foreach (var declaredCode in new[] { "duration_weighted", "arithmetic_mean", "sum", "rate_integral", "customer_declared_semantic" })
        {
            var registry = RegistryWithLineage();

            Assert.True(registry.TryDeclareTransformation(
                new GrainTransformation(Child, Parent, declaredCode, WeightKey), out _));

            var plan = GrainResolutionKernel.Resolve(registry, SubjectKey, Parent).Plan;

            Assert.NotNull(plan);
            Assert.Equal(declaredCode, plan!.Transformation!.TransformationCode);
        }
    }

    [Fact]
    public void A_transformation_without_a_declared_code_is_not_a_declaration()
    {
        var registry = RegistryWithLineage();

        foreach (var blank in new[] { "", "   " })
        {
            Assert.False(registry.TryDeclareTransformation(
                new GrainTransformation(Child, Parent, blank, WeightKey), out var code));

            Assert.Equal(GrainContractCodes.TransformationCodeNotDeclared, code);
        }

        Assert.Equal(0, registry.TransformationCount);
    }

    [Fact]
    public void A_transformation_cannot_reference_an_undeclared_grain()
    {
        var registry = RegistryWithLineage();
        var ghost = GrainIdentifier.Declared("GRAIN-NEVER-DECLARED");

        Assert.False(registry.TryDeclareTransformation(
            new GrainTransformation(Child, ghost, "sum", string.Empty), out var code));

        Assert.Equal(GrainContractCodes.GrainNotDeclared, code);
    }

    // ------------------------------------------------- declaration invariants

    [Fact]
    public void An_identical_redeclaration_is_idempotent()
    {
        var registry = RegistryWithLineage();
        DeclareRollUp(registry);

        Assert.True(registry.TryDeclareGrain(new GrainDefinition(Child, Parent), out _));
        Assert.True(registry.TryDeclareSubject(new AnalysisSubjectDefinition(SubjectKey, Child), out _));
        Assert.True(registry.TryDeclareTransformation(
            new GrainTransformation(Child, Parent, DurationWeightedCode, WeightKey), out _));

        Assert.Equal(2, registry.GrainCount);
        Assert.Equal(1, registry.SubjectCount);
        Assert.Equal(1, registry.TransformationCount);
    }

    [Fact]
    public void A_conflicting_grain_redeclaration_fails_closed_instead_of_overwriting()
    {
        var registry = RegistryWithLineage();
        var other = GrainIdentifier.Declared("GRAIN-OTHER-BRANCH");
        Assert.True(registry.TryDeclareGrain(new GrainDefinition(other, GrainIdentifier.Undeclared), out _));

        Assert.False(registry.TryDeclareGrain(new GrainDefinition(Child, other), out var code));
        Assert.Equal(GrainContractCodes.ConflictingDeclaration, code);

        // The original lineage is intact: a later arrival does not win by arriving later.
        Assert.Equal(Parent, registry.ParentOf(Child));
    }

    [Fact]
    public void A_conflicting_subject_redeclaration_fails_closed_instead_of_overwriting()
    {
        var registry = RegistryWithLineage();

        Assert.False(registry.TryDeclareSubject(new AnalysisSubjectDefinition(SubjectKey, Parent), out var code));
        Assert.Equal(GrainContractCodes.ConflictingDeclaration, code);

        Assert.True(registry.TryGetSubject(SubjectKey, out var subject));
        Assert.Equal(Child, subject!.Grain);
    }

    [Fact]
    public void A_conflicting_transformation_redeclaration_fails_closed_instead_of_overwriting()
    {
        var registry = RegistryWithLineage();
        DeclareRollUp(registry);

        Assert.False(registry.TryDeclareTransformation(
            new GrainTransformation(Child, Parent, "sum", string.Empty), out var code));

        Assert.Equal(GrainContractCodes.ConflictingDeclaration, code);

        Assert.True(registry.TryGetTransformation(Child, Parent, out var stored));
        Assert.Equal(DurationWeightedCode, stored!.TransformationCode);
    }

    [Fact]
    public void A_lineage_cycle_is_rejected_at_declaration_time()
    {
        var registry = RegistryWithLineage();

        // A grain cannot be its own parent.
        var loner = GrainIdentifier.Declared("GRAIN-SELF");
        Assert.False(registry.TryDeclareGrain(new GrainDefinition(loner, loner), out var selfCode));
        Assert.Equal(GrainContractCodes.LineageCycle, selfCode);

        // And it cannot be placed beneath one of its own descendants.
        Assert.False(registry.TryDeclareGrain(new GrainDefinition(Parent, Child), out var loopCode));
        Assert.Equal(GrainContractCodes.LineageCycle, loopCode);

        // Neither attempt entered the registry, so traversal never has to survive one.
        Assert.Equal(2, registry.GrainCount);
        Assert.False(registry.IsGrainDeclared(loner));
        Assert.Equal(GrainIdentifier.Undeclared, registry.ParentOf(Parent));
    }

    // -------------------------------------------------------------- normalisation

    [Fact]
    public void One_normalisation_rule_prevents_a_key_becoming_two_identities()
    {
        var registry = RegistryWithLineage();

        // Declared once, findable under any surrounding whitespace, and never a second
        // subject. Trim only: no case folding, no vocabulary interpretation.
        foreach (var variant in new[] { SubjectKey, "  " + SubjectKey, SubjectKey + "  ", "  " + SubjectKey + "  " })
        {
            Assert.True(registry.TryGetSubject(variant, out var subject));
            Assert.Equal(SubjectKey, subject!.SubjectKey);
        }

        Assert.True(registry.TryDeclareSubject(new AnalysisSubjectDefinition("  " + SubjectKey + "  ", Child), out _));
        Assert.Equal(1, registry.SubjectCount);

        Assert.True(GrainIdentifier.TryCreate("  " + ChildGrainKey + "  ", out var padded));
        Assert.Equal(Child, padded);

        // Case is identity, not noise. Nothing here decides that two spellings mean the
        // same thing.
        Assert.False(registry.TryGetSubject(SubjectKey.ToLowerInvariant(), out _));
    }

    // ---------------------------------------------------------- fixture oracle

    [Fact]
    public void The_declared_plan_reproduces_the_fixture_duration_weighted_answer()
    {
        var registry = RegistryWithLineage();
        DeclareRollUp(registry);

        var plan = GrainResolutionKernel.Resolve(registry, SubjectKey, Parent).Plan;
        Assert.NotNull(plan);
        Assert.Equal(WeightKey, plan!.Transformation!.WeightKey);

        // The plan says weight by duration, so the caller weights by duration.
        var subjects = GenericProcessFixture.WeightedSubjects;
        var weighted = subjects.Sum(s => s.DurationMinutes * s.Mean) / subjects.Sum(s => s.DurationMinutes);

        Assert.Equal(ContinuousProcessKnownAnswers.DurationWeightedMean.AsDouble, weighted, 10);

        // And it is not the number an implicit conversion would have produced.
        Assert.NotEqual(ContinuousProcessKnownAnswers.UnweightedSubjectMean.AsDouble, weighted, 10);
    }

    [Fact]
    public void Without_a_declaration_the_wrong_answer_is_unreachable_rather_than_merely_discouraged()
    {
        // The same crossing, with no transformation declared. The caller receives a
        // refusal and no plan, so there is nothing to compute the unweighted 340/3 from.
        var registry = RegistryWithLineage();

        var resolution = GrainResolutionKernel.Resolve(registry, SubjectKey, Parent);

        Assert.False(resolution.IsResolved);
        Assert.Null(resolution.Plan);
        Assert.Equal(GrainContractCodes.TransformationNotDeclared, resolution.Verdict.Code);
        Assert.Equal(TerminalState.RefusedByGuard, resolution.Verdict.Outcome);
    }
}