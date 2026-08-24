// Rule validity, proven synthetically.
//
// Backlog origin: T-206.
//
// Every rule is exercised against a positive sample that MUST trigger and a
// neighbouring generic sample that MUST NOT. Validity therefore does not depend on
// the repository still containing debt. After the vocabulary sweep drives the tree
// to zero, these tests are unchanged and still green, and the gate still blocks
// regression.
using System;
using System.Linq;
using Xunit;

namespace PlantProcess.Architecture.Tests;

[Trait("BacklogTask", "T-206")]
public sealed class ScopeAwareGenericityRuleTests
{
    private const string ProductPath = "Backend/PlantProcess.Application/Sample.cs";

    private static bool Triggers(string ruleId, string code)
    {
        return ScopeAwareGenericity.ScanText(ProductPath, code).Any(f => f.RuleId == ruleId);
    }

    public static TheoryData<string, string, string> Samples()
    {
        var data = new TheoryData<string, string, string>();

        // rule, MUST trigger, MUST NOT trigger
        data.Add("UA-01",
            "var rows = new[] { new { equipment = \"Caster 1\" }, new { equipment = \"Mill 1\" } };",
            "var rows = catalogue.ResolveEquipment(request.EquipmentKey);");

        data.Add("UA-02",
            "var handle = inputs.CoilId is null ? null : \"coil:\" + inputs.CoilId;",
            "var handle = inputs.SubjectId is null ? null : subject.Kind + \":\" + inputs.SubjectId;");

        data.Add("UA-03",
            "var grain = string.IsNullOrWhiteSpace(request.Grain) ? \"coil\" : request.Grain.Trim();",
            "var grain = registry.ResolveGrain(request) ?? throw new GrainUnresolvedException(request);");

        data.Add("UA-04",
            "RequireBand(assumptions.GradePremiumPerTon, \"grade_premium_per_ton\", missing);",
            "RequireBand(assumptions.ValuePerUnit, configuredParameter.Key, missing);");

        data.Add("UA-05",
            "kind: \"coil\" | \"heat\" | \"cast\" | \"slab\" | \"process\" | string;",
            "kind: SubjectKind; // resolved from the customer's declared subject registry");

        data.Add("UA-06",
            "var sql = \"SELECT effective_sample_key, numeric_value, heat_id FROM outcomes\";",
            "var sql = \"SELECT effective_sample_key, numeric_value, \" + stratumColumn + \" FROM outcomes\";");

        data.Add("UA-07",
            "var isGrade = f.FeatureKey.IndexOf(\"grade\", StringComparison.OrdinalIgnoreCase) >= 0;",
            "var isStratum = f.FeatureKey.Equals(declared.StratumKey, StringComparison.Ordinal);");

        return data;
    }

    [Theory]
    [MemberData(nameof(Samples))]
    public void Every_rule_fires_on_its_construct_and_stays_silent_on_the_generic_neighbour(
        string ruleId, string mustTrigger, string mustNotTrigger)
    {
        Assert.True(
            Triggers(ruleId, mustTrigger),
            "Genericity gate: rule " + ruleId + " failed to detect its own positive sample. The rule is broken.");

        Assert.False(
            Triggers(ruleId, mustNotTrigger),
            "Genericity gate: rule " + ruleId + " fired on a generic, metadata-driven construct. " +
            "This is an architecture guard, not a word filter, and a false positive here teaches " +
            "the next worker to disable it.");
    }

    [Fact]
    public void Descriptive_text_that_enumerates_many_industries_is_not_a_violation()
    {
        // The most genericity-affirming line in the dimension registry lists many
        // material types precisely so that none is privileged. A gate that fails this
        // line is measuring the wrong thing.
        const string code =
            "new DimensionDescriptor(\"material_type\", \"Generic material type such as batch, slab, coil, lot, tire, roll or component.\");";

        Assert.Empty(ScopeAwareGenericity.ScanText(ProductPath, code));
    }

    [Fact]
    public void Comments_are_stripped_before_scanning()
    {
        const string code =
            "// this comment mentions coil_id and \"Caster 1\"\nvar x = 1; /* and heat_id here */\nvar y = 2;";

        Assert.Empty(ScopeAwareGenericity.ScanText(ProductPath, code));
    }

    [Fact]
    public void Scope_classification_is_total_and_separator_insensitive()
    {
        Assert.Equal(GenericityScope.ProductGeneric, ScopeAwareGenericity.Classify("Backend/PlantProcess.Application/X.cs"));
        Assert.Equal(GenericityScope.ProductGeneric, ScopeAwareGenericity.Classify("Backend\\PlantProcess.Application\\X.cs"));
        Assert.Equal(GenericityScope.CustomerAsset,  ScopeAwareGenericity.Classify("Backend/tools/emulate_plant.py"));
        Assert.Equal(GenericityScope.TestOrFixture,  ScopeAwareGenericity.Classify("Backend/tests/X/YTests.cs"));
        Assert.Equal(GenericityScope.Documentation,  ScopeAwareGenericity.Classify("docs/quality/x.md"));
        Assert.Equal(GenericityScope.Excluded,       ScopeAwareGenericity.Classify(
            "Backend/tests/PlantProcess.Architecture.Tests/ScopeAwareGenericity.cs"));
    }

    [Fact]
    public void Customer_asset_scope_is_never_scanned()
    {
        Assert.Empty(ScopeAwareGenericity.ScanText("Backend/tools/generate_demo.py", "grain = \"coil\""));
    }

    [Fact]
    public void A_fingerprint_survives_line_movement_and_changes_with_the_construct()
    {
        // The product law this protects: identity must be stable under reformatting and
        // unstable under semantic change. If it were the reverse, a tidy-up would drop
        // every grandfathered entry, or swapping one hardcoded default for another would
        // silently inherit the old one.
        const string original = "var grain = string.IsNullOrWhiteSpace(request.Grain) ? \"coil\" : request.Grain;";
        const string edited   = "var grain = string.IsNullOrWhiteSpace(request.Grain) ? \"heat\" : request.Grain;";

        var first  = ScopeAwareGenericity.ScanText(ProductPath, original).First(f => f.RuleId == "UA-03").Fingerprint;
        var moved  = ScopeAwareGenericity.ScanText(ProductPath, "\n\n\n" + original).First(f => f.RuleId == "UA-03").Fingerprint;
        var second = ScopeAwareGenericity.ScanText(ProductPath, edited).First(f => f.RuleId == "UA-03").Fingerprint;

        Assert.Equal(first, moved);
        Assert.NotEqual(first, second);
    }

    [Fact]
    public void A_fingerprint_changes_when_the_file_changes()
    {
        const string code = "var grain = string.IsNullOrWhiteSpace(request.Grain) ? \"coil\" : request.Grain;";

        var here  = ScopeAwareGenericity.ScanText("Backend/PlantProcess.Application/A.cs", code).First().Fingerprint;
        var there = ScopeAwareGenericity.ScanText("Backend/PlantProcess.Application/B.cs", code).First().Fingerprint;

        Assert.NotEqual(here, there);
    }

    [Fact]
    public void The_product_generic_walk_is_not_vacuous()
    {
        var files = ScopeAwareGenericity.ProductGenericFiles(ScopeAwareGenericity.RepositoryRoot());

        Assert.True(
            files.Count > 400,
            "Genericity gate: the walk visited only " + files.Count + " product files. " +
            "A gate that scans nothing passes forever and proves nothing.");
    }
}