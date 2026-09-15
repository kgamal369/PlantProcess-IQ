using System.Text.RegularExpressions;
using PlantProcess.Domain.Common;
using Xunit;

namespace PlantProcess.Application.UnitTests.Integration;

public sealed class ProjectionValidationFixtureCompletenessTests
{
    [Fact]
    public void Every_declared_PV_code_has_live_fixture_coverage()
    {
        var expected = Enumerable.Range(1, 15)
            .Select(index => $"PV{index:00}")
            .ToArray();

        Assert.Equal(
            expected,
            Enum.GetNames(typeof(ProjectionValidationCode))
                .OrderBy(name => name, StringComparer.Ordinal)
                .ToArray());

        var root = FindRepoRoot();

        var fixtureFiles = new[]
        {
            Path.Combine(
                root,
                "Backend",
                "tests",
                "PlantProcess.Infrastructure.IntegrationTests",
                "Quarantine",
                "ProjectionQuarantineClassificationTests.cs"),
            Path.Combine(
                root,
                "Backend",
                "tests",
                "PlantProcess.Infrastructure.IntegrationTests",
                "Quarantine",
                "ProjectionQuarantineAdvancedClassificationTests.cs")
        };

        foreach (var file in fixtureFiles)
            Assert.True(File.Exists(file), $"Missing fixture file: {file}");

        var fixtureCodes = fixtureFiles
            .SelectMany(file =>
                Regex.Matches(
                        File.ReadAllText(file),
                        @"public\s+async\s+Task\s+(PV\d{2})_")
                    .Select(match => match.Groups[1].Value))
            .ToArray();

        // Completeness is about taxonomy coverage, not forbidding additional
        // regression tests for a class. A second PV02 test is stronger evidence,
        // not a reason to make the ratchet red.
        var distinctCoverage = fixtureCodes
            .Distinct(StringComparer.Ordinal)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        Assert.Equal(expected, distinctCoverage);
        Assert.True(
            fixtureCodes.Length >= 15,
            $"Expected at least 15 live classification fixtures; found {fixtureCodes.Length}.");

        foreach (var expectedCode in expected)
            Assert.Contains(expectedCode, fixtureCodes);

        foreach (ProjectionValidationCode code in
                 Enum.GetValues(typeof(ProjectionValidationCode)))
        {
            Assert.False(
                string.IsNullOrWhiteSpace(
                    ProjectionValidationCorrection.For(code)));
        }
    }

    private static string FindRepoRoot()
    {
        var current = new DirectoryInfo(AppContext.BaseDirectory);

        while (current is not null)
        {
            if (Directory.Exists(Path.Combine(current.FullName, "Backend")) &&
                Directory.Exists(Path.Combine(current.FullName, "Frontend")))
                return current.FullName;

            current = current.Parent;
        }

        throw new InvalidOperationException(
            "Repo root could not be found.");
    }
}