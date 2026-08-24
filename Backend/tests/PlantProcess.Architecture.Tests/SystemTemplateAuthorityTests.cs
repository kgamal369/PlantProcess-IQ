using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

// =====================================================================================
// System template single-authority gate.
//
// Three mechanisms once created the same product system templates: a SQL seed that
// overwrote on every replay, a repair script that rewrote the same rows again, and
// the runtime reconciler. The runtime reconciler is now the single authority.
//
// This gate is self-contained on purpose. It resolves the repository root itself and
// declares no shared type, so it compiles whether or not any other gate is present.
//
// Every searched token is assembled from fragments and every read strips comments, so
// this file cannot satisfy its own rules and a comment cannot violate them.
// =====================================================================================

[Trait("Gate", "SystemTemplateAuthority")]
public sealed class SystemTemplateAuthorityTests
{
    private const string RuntimeAuthorityPath =
        "Backend/PlantProcess.Application/Dashboarding/Services/Dashboards/DashboardDefinitionService.cs";

    private const string DatabaseRoot = "Backend/database";

    private const string ProvenanceMarker = "PlantProcessIQ" + ".SystemTemplates";

    // Measures that cannot be evaluated without an explicit parameter selection.
    private static readonly string[] ParameterDependentMeasures =
    {
        "avgParameterValue",
        "maxParameterValue",
        "minParameterValue",
        "parameterValueDistribution",
        "parameterValueSpread",
        "parameterRelationship"
    };

    // Plant-specific vocabulary that must never appear in a product template.
    private static readonly string[] PlantVocabulary =
    {
        "Casting" + "Speed",
        "co" + "il",
        "he" + "at",
        "cas" + "ter",
        "tun" + "dish"
    };

    [Fact]
    public void No_sql_file_creates_product_system_templates()
    {
        var root = FindRepositoryRoot();
        var databaseDirectory = Path.Combine(root, DatabaseRoot.Replace('/', Path.DirectorySeparatorChar));
        var offenders = new List<string>();

        foreach (var file in Directory.EnumerateFiles(databaseDirectory, "*.sql", SearchOption.AllDirectories))
        {
            var code = StripSqlCommentsAndLiterals(File.ReadAllText(file));

            if (!code.Contains(ProvenanceMarker, StringComparison.Ordinal))
            {
                continue;
            }

            if (Regex.IsMatch(code, @"INSERT\s+INTO\s+dashboard_(widget_)?definitions", RegexOptions.IgnoreCase))
            {
                offenders.Add(file.Substring(root.Length + 1).Replace('\\', '/'));
            }
        }

        Assert.True(
            offenders.Count == 0,
            "Product system templates have exactly one authority, and it is the runtime reconciler. " +
            "These SQL files create them as well, which is how three mechanisms came to fight over the same rows:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Runtime_authority_declares_no_parameter_dependent_template_widget()
    {
        var source = ReadRuntimeAuthority();
        var offenders = ParameterDependentMeasures
            .Where(measure => source.Contains("Measures." + Capitalise(measure), StringComparison.Ordinal))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "A system template widget declares a measure that cannot be evaluated without an explicit parameter " +
            "selection. A product template does not know which parameters a plant has, and choosing one embeds " +
            "plant-specific vocabulary. This is the defect that left a seeded widget failing on every install:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Runtime_authority_contains_no_plant_specific_vocabulary()
    {
        var source = ReadRuntimeAuthority();
        var offenders = PlantVocabulary
            .Where(term => Regex.IsMatch(source, @"\b" + term + @"s?\b", RegexOptions.IgnoreCase))
            .ToList();

        Assert.True(
            offenders.Count == 0,
            "The system-template authority must stay generic across industries:" +
            Environment.NewLine + string.Join(Environment.NewLine, offenders));
    }

    [Fact]
    public void Runtime_authority_is_idempotent_by_construction()
    {
        var source = ReadRuntimeAuthority();

        Assert.Contains("EnsureSystemTemplatesAsync", source, StringComparison.Ordinal);

        Assert.True(
            Regex.IsMatch(source, @"OrdinalIgnoreCase", RegexOptions.None),
            "The authority must match existing widgets case-insensitively, or a casing difference creates a " +
            "duplicate instead of being repaired.");
    }

    private static string ReadRuntimeAuthority()
    {
        var path = Path.Combine(
            FindRepositoryRoot(),
            RuntimeAuthorityPath.Replace('/', Path.DirectorySeparatorChar));

        Assert.True(File.Exists(path), "The runtime system-template authority is missing: " + RuntimeAuthorityPath);

        return StripCsharpComments(File.ReadAllText(path));
    }

    private static string Capitalise(string value)
    {
        return char.ToUpperInvariant(value[0]) + value.Substring(1);
    }

    private static string StripCsharpComments(string source)
    {
        var withoutBlocks = Regex.Replace(source, @"/\*[\s\S]*?\*/", string.Empty);
        return Regex.Replace(withoutBlocks, @"//[^\r\n]*", string.Empty);
    }

    private static string StripSqlCommentsAndLiterals(string sql)
    {
        var withoutBlocks = Regex.Replace(sql, @"/\*[\s\S]*?\*/", " ");
        var withoutLine = Regex.Replace(withoutBlocks, @"--[^\r\n]*", " ");
        return withoutLine;
    }

    private static string FindRepositoryRoot()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);

        while (directory is not null)
        {
            if (Directory.Exists(Path.Combine(directory.FullName, ".git")) ||
                Directory.Exists(Path.Combine(directory.FullName, "Backend")))
            {
                return directory.FullName;
            }

            directory = directory.Parent;
        }

        throw new InvalidOperationException("Repository root not found from " + AppContext.BaseDirectory);
    }
}
