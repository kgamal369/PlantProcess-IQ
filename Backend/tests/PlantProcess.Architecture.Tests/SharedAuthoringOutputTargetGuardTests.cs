using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// PPIQ T-253. NO HARDCODED MATERIAL OUTPUT TARGET IN SHARED AUTHORING.
///
/// Scoped to one directory on purpose. Registering "MaterialUnit" as a global plant
/// vocabulary term would fire across the demo seed, the mapping service and the EF
/// model, where the name is legitimate: MaterialUnit IS a canonical entity and IS an
/// eligible target. What is forbidden is shared authoring COMPILING one, so the guard
/// is a path rule rather than a word ban.
///
/// It reads CODE. Comments are stripped first, because a file's own prose about a
/// defect must not count as the defect - this file says both literals out loud.
/// The guard lives under Backend and scans Frontend, so it cannot match itself; the
/// exclusion is structural rather than a name on a list.
///
/// Rule validity is proven against SYNTHETIC samples, never against real debt in the
/// tree, so the negative control still works after the tree reaches zero.
/// </summary>
[Trait("Gate", "SharedAuthoringOutputTarget")]
public sealed class SharedAuthoringOutputTargetGuardTests
{
    private const string ScannedDirectory = "Frontend/PlantProcess.Web/src/authoring";

    private static readonly Regex ForbiddenMaterialTarget = new(
        "\"MaterialUnit\"|'MaterialUnit'|canonical_material_units",
        RegexOptions.Compiled);

    [Fact]
    public void Shared_authoring_production_code_compiles_no_material_output_target()
    {
        var offences = new List<string>();

        foreach (var file in ProductionSources())
        {
            var code = StripComments(File.ReadAllText(file));
            var lines = code.Replace("\r\n", "\n").Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                if (ForbiddenMaterialTarget.IsMatch(lines[i]))
                {
                    offences.Add(Path.GetFileName(file) + ":" + (i + 1) + "  " + lines[i].Trim());
                }
            }
        }

        Assert.True(
            offences.Count == 0,
            "Shared authoring must resolve its output target through the governed catalogue, "
            + "never a material literal:" + Environment.NewLine + string.Join(Environment.NewLine, offences));
    }

    [Fact]
    public void The_guard_scans_something_and_is_not_silently_passing_on_an_empty_set()
    {
        // A path rule that matched no files would report green forever. This is the
        // difference between "no offences" and "never looked".
        Assert.NotEmpty(ProductionSources());
    }

    [Fact]
    public void Negative_control_the_rule_goes_red_on_a_reintroduced_entity_literal()
    {
        const string sample = "const s = serialisationOutcome(name, \"MaterialUnit\", nodes, edges);";

        Assert.Matches(ForbiddenMaterialTarget, StripComments(sample));
    }

    [Fact]
    public void Negative_control_the_rule_goes_red_on_a_reintroduced_relation_literal()
    {
        const string sample = "const body = { canonicalEntity: \"canonical_material_units\" };";

        Assert.Matches(ForbiddenMaterialTarget, StripComments(sample));
    }

    [Fact]
    public void Negative_control_the_rule_stays_silent_on_prose_that_merely_names_the_defect()
    {
        const string sample = "// The shell used to hardcode \"MaterialUnit\" and canonical_material_units.";

        Assert.DoesNotMatch(ForbiddenMaterialTarget, StripComments(sample));
    }

    [Fact]
    public void Negative_control_the_governed_call_shape_is_not_an_offence()
    {
        const string sample = "const s = serialisationOutcome(name, outputTarget, nodes, edges);";

        Assert.DoesNotMatch(ForbiddenMaterialTarget, StripComments(sample));
    }

    /// <summary>
    /// Production sources only. A test fixture may legitimately name an entity, and
    /// counting fixtures would make the guard about test data rather than about product
    /// behaviour.
    /// </summary>
    private static string[] ProductionSources()
    {
        var directory = Path.Combine(
            FindRepositoryRoot(), ScannedDirectory.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(Directory.Exists(directory), "The scanned authoring directory has moved: " + ScannedDirectory);

        return Directory
            .EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
            .Where(f => f.EndsWith(".ts", StringComparison.Ordinal) || f.EndsWith(".tsx", StringComparison.Ordinal))
            .Where(f => !Path.GetFileName(f).Contains(".test.", StringComparison.Ordinal))
            .OrderBy(f => f, StringComparer.Ordinal)
            .ToArray();
    }

    private static string StripComments(string source)
    {
        var withoutBlocks = Regex.Replace(source, @"/\*.*?\*/", string.Empty, RegexOptions.Singleline);
        return Regex.Replace(withoutBlocks, @"//[^\n]*", string.Empty);
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("The repository root could not be located from " + AppContext.BaseDirectory);
    }
}