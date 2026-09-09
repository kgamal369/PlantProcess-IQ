using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// PPIQ T-254. THE CONNECTOR SURFACES DO NOT KEEP THEIR OWN TRUTH.
///
/// Two narrow rules, not a generic string scanner:
///
///   R1  A consumer may not author provider AVAILABILITY. The backend decides it in
///       ProviderAvailability and ships it as ProviderTypeDto.IsAvailableNow, so a
///       browser string saying "Planned:" or "Not available yet" is a second answer
///       to a question that already has one.
///
///   R2  No surface may print a fixed count of LIVE sources. Live is runtime state -
///       reachable, certified, lagging - and it lives in the connector runtime truth
///       view. A number typed into a page was true on the day it was typed.
///
/// It reads CODE: comments are stripped first, so a file explaining the rule cannot
/// violate it. Tests and fixtures are excluded, because a fixture may legitimately
/// contain the very string the rule forbids in product source. This file lives under
/// Backend and scans Frontend and Website, so it cannot match itself.
///
/// Rule validity is proven against SYNTHETIC samples, so the negative controls keep
/// working after the tree reaches zero.
/// </summary>
[Trait("Gate", "ConnectorTruthConvergence")]
public sealed class ConnectorTruthConvergenceGuardTests
{
    private static readonly string[] ScannedDirectories =
    {
        "Frontend/PlantProcess.Web/src",
        "Website/PlantProcess.Website/src",
    };

    /// <summary>R1. Availability asserted in a consumer string literal.</summary>
    private static readonly Regex AuthoredAvailability = new(
        "\"Planned:|Not available yet|PROVIDER_DETAIL",
        RegexOptions.Compiled);

    /// <summary>R2. A fixed count of live sources.</summary>
    private static readonly Regex FixedLiveSourceCount = new(
        @"\d+\s+live\s+source|live\s+source\s+systems",
        RegexOptions.Compiled | RegexOptions.IgnoreCase);

    [Fact]
    public void No_consumer_authors_provider_availability()
    {
        AssertNoMatch(AuthoredAvailability,
            "Provider availability belongs to ProviderAvailability and reaches the surface as "
            + "IsAvailableNow. A consumer that states it keeps a second truth:");
    }

    [Fact]
    public void No_surface_prints_a_fixed_count_of_live_sources()
    {
        AssertNoMatch(FixedLiveSourceCount,
            "Live is runtime state the connector truth view owns. A number printed in a page "
            + "is an operational claim the page cannot support:");
    }

    [Fact]
    public void The_guard_scans_something_and_is_not_passing_on_an_empty_set()
    {
        // A path rule that matched no files would report green forever.
        Assert.NotEmpty(ProductionSources());
    }

    // ------------------------------------------------------------ NEGATIVE CONTROLS

    [Fact]
    public void Negative_control_a_reintroduced_provider_detail_map_goes_red()
    {
        const string sample =
            "const PROVIDER_DETAIL: Record<string, string> = { Sap: \"Planned: not yet\" };";

        Assert.Matches(AuthoredAvailability, StripComments(sample));
    }

    [Fact]
    public void Negative_control_a_reintroduced_live_count_goes_red()
    {
        const string sample = "<span>unified from 6 live source systems.</span>";

        Assert.Matches(FixedLiveSourceCount, StripComments(sample));
    }

    [Fact]
    public void Negative_control_a_test_fixture_is_not_product_source()
    {
        // The exclusion is by filename, so it is asserted on filenames rather than by
        // planting a forbidden string in a real fixture.
        Assert.False(IsProductionSource("connectorTruthConvergence.test.tsx"));
        Assert.False(IsProductionSource("phase7-golive.spec.ts"));
        Assert.True(IsProductionSource("AdminDbConfigurationTab.tsx"));
    }

    [Fact]
    public void Negative_control_prose_about_connectors_stays_silent()
    {
        const string capability =
            "<p>Supports industrial data sources including PostgreSQL, SQL Server and Oracle.</p>";
        const string explanation =
            "// The card renders pt.description, so nothing here says Planned: or Not available yet.";

        Assert.DoesNotMatch(AuthoredAvailability, StripComments(capability));
        Assert.DoesNotMatch(FixedLiveSourceCount, StripComments(capability));
        Assert.DoesNotMatch(AuthoredAvailability, StripComments(explanation));
    }

    // -------------------------------------------------------------------- MACHINERY

    private static void AssertNoMatch(Regex rule, string why)
    {
        var offences = new List<string>();

        foreach (var file in ProductionSources())
        {
            var code = StripComments(File.ReadAllText(file));
            var lines = code.Replace("\r\n", "\n").Split('\n');

            for (var i = 0; i < lines.Length; i++)
            {
                if (rule.IsMatch(lines[i]))
                {
                    offences.Add(Path.GetFileName(file) + ":" + (i + 1) + "  " + lines[i].Trim());
                }
            }
        }

        Assert.True(
            offences.Count == 0,
            why + Environment.NewLine + string.Join(Environment.NewLine, offences));
    }

    private static bool IsProductionSource(string fileName) =>
        !fileName.Contains(".test.", StringComparison.Ordinal)
        && !fileName.Contains(".spec.", StringComparison.Ordinal);

    private static string[] ProductionSources()
    {
        var root = FindRepositoryRoot();
        var files = new List<string>();

        foreach (var relative in ScannedDirectories)
        {
            var directory = Path.Combine(root, relative.Replace('/', Path.DirectorySeparatorChar));
            Assert.True(Directory.Exists(directory), "A scanned surface has moved: " + relative);

            files.AddRange(Directory
                .EnumerateFiles(directory, "*.*", SearchOption.AllDirectories)
                .Where(f => f.EndsWith(".ts", StringComparison.Ordinal) || f.EndsWith(".tsx", StringComparison.Ordinal))
                .Where(f => IsProductionSource(Path.GetFileName(f))));
        }

        return files.OrderBy(f => f, StringComparer.Ordinal).ToArray();
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