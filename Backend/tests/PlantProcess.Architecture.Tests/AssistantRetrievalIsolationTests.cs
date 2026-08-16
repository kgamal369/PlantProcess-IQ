using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using PlantProcess.Application.Assistant.Retrieval;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-180 isolation boundary.
///
/// This layer ends at an evidence package. It does not phrase an answer, does not
/// verify one, does not register itself into production and does not know that a
/// presentation surface exists. Each of those is proven here rather than promised in
/// a comment, because the whole value of an isolated module is that it stayed isolated.
/// </summary>
public sealed class AssistantRetrievalIsolationTests
{
    private static readonly string[] RetrievalFiles =
    {
        "RetrievalContracts.cs",
        "PermissionSafeCandidateFilter.cs",
        "HybridRanker.cs",
        "EvidencePacker.cs",
        "RetrievalBenchmarkHooks.cs"
    };

    private static string RepositoryRoot()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);

        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, "Backend")))
        {
            dir = dir.Parent;
        }

        Assert.True(dir is not null, "could not locate the repository root");
        return dir!.FullName;
    }

    private static string RetrievalDirectory() => Path.Combine(
        RepositoryRoot(), "Backend", "PlantProcess.Application", "Assistant", "Retrieval");

    /// <summary>Source with comments removed, so a guard judges code and not prose.</summary>
    private static string CodeOf(string fileName)
    {
        var path = Path.Combine(RetrievalDirectory(), fileName);
        Assert.True(File.Exists(path), "retrieval file is missing: " + path);

        var raw = File.ReadAllText(path);
        var withoutBlocks = Regex.Replace(raw, @"/\*[\s\S]*?\*/", string.Empty);
        return Regex.Replace(withoutBlocks, @"(?m)^\s*//.*$", string.Empty);
    }

    [Fact]
    public void NoModelOrGatewayParticipatesInRetrievalGovernance()
    {
        // Permission, deduplication, budgeting, tie-breaking and truncation are
        // deterministic code. Needles are assembled so this guard cannot match itself.
        var forbidden = new[]
        {
            "IAssistant" + "Model",
            "IEmbed" + "der",
            "Model" + "Gateway",
            "Grounded" + "AssistantGateway",
            "Http" + "Client",
            "prompt",
            "completion"
        };

        foreach (var file in RetrievalFiles)
        {
            var code = CodeOf(file);
            foreach (var needle in forbidden)
            {
                Assert.False(
                    code.Contains(needle, StringComparison.OrdinalIgnoreCase),
                    file + " names '" + needle + "' in code.");
            }
        }
    }

    [Fact]
    public void TheLayerImportsNoPresentationOrDashboardCode()
    {
        var forbidden = new[]
        {
            "PlantProcess.Application.Dashboarding",
            "PlantProcess.Api",
            "PlantProcess.Infrastructure",
            "Microsoft.AspNetCore"
        };

        foreach (var file in RetrievalFiles)
        {
            var code = CodeOf(file);
            foreach (var needle in forbidden)
            {
                Assert.False(
                    code.Contains(needle, StringComparison.Ordinal),
                    file + " references " + needle + ".");
            }
        }
    }

    [Fact]
    public void TheLayerPersistsNothingAndReachesNoStore()
    {
        var forbidden = new[]
        {
            "Npgsql", "DbContext", "ExecuteSql", "SaveChanges", "connectionString",
            "Socket", "Process.Start"
        };

        foreach (var file in RetrievalFiles)
        {
            var code = CodeOf(file);
            foreach (var needle in forbidden)
            {
                Assert.False(
                    code.Contains(needle, StringComparison.OrdinalIgnoreCase),
                    file + " names '" + needle + "'.");
            }
        }
    }

    [Fact]
    public void TheLayerDoesNotVerifyAnswersOrPhraseThem()
    {
        // Answer verification is T-181 and phrasing is downstream of that. A method
        // here that claimed either would be this task reaching past its own end.
        var forbidden = new[] { "Verify", "Citation", "Hallucin", "AnswerText", "Q01", "Q-01" };

        foreach (var file in RetrievalFiles)
        {
            var code = CodeOf(file);
            foreach (var needle in forbidden)
            {
                Assert.False(
                    code.Contains(needle, StringComparison.OrdinalIgnoreCase),
                    file + " names '" + needle + "', which belongs to T-181.");
            }
        }
    }

    [Fact]
    public void NothingRegistersTheRetrievalLayerIntoProduction()
    {
        var root = RepositoryRoot();
        var candidates = new[]
        {
            Path.Combine(root, "Backend", "PlantProcess.Application", "DependencyInjection.cs"),
            Path.Combine(root, "Backend", "PlantProcess.Api", "Program.cs")
        };

        foreach (var path in candidates.Where(File.Exists))
        {
            var text = File.ReadAllText(path);
            foreach (var name in new[] { nameof(EvidencePacker), nameof(HybridRanker), nameof(PermissionSafeCandidateFilter) })
            {
                Assert.False(
                    text.Contains(name, StringComparison.Ordinal),
                    Path.GetFileName(path) + " registers " + name + "; T-138 owns integration.");
            }
        }
    }

    [Fact]
    public void APermittedCandidateSetCanOnlyComeFromThePermissionFilter()
    {
        // The central invariant, checked on the compiled surface rather than by
        // reading the code: no public constructor exists, so no caller outside this
        // assembly can fabricate a pool that skipped permission.
        var constructors = typeof(PermittedCandidateSet)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance);

        Assert.Empty(constructors);
    }

    [Fact]
    public void EveryRankingEntryPointDemandsAPermittedSet()
    {
        var ranking = typeof(HybridRanker)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .ToArray();

        Assert.NotEmpty(ranking);
        Assert.All(ranking, method =>
            Assert.Contains(
                method.GetParameters(),
                parameter => parameter.ParameterType == typeof(PermittedCandidateSet)));
    }

    [Fact]
    public void TheEvidencePackPublishesNoCountOfWhatWasForbidden()
    {
        var names = typeof(EvidencePack)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name.ToLowerInvariant())
            .ToArray();

        foreach (var forbidden in new[] { "rejected", "forbidden", "denied", "filteredout" })
        {
            Assert.DoesNotContain(names, name => name.Contains(forbidden, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void TheMeasurementRecordCarriesNoVerdict()
    {
        var names = typeof(RetrievalMeasurement)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name.ToLowerInvariant())
            .ToArray();

        foreach (var forbidden in new[] { "verdict", "winner", "recommended", "passed", "better" })
        {
            Assert.DoesNotContain(names, name => name.Contains(forbidden, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void EveryRetrievalTypeIsImmutable()
    {
        var types = typeof(EvidencePack).Assembly
            .GetTypes()
            .Where(t => t.Namespace == "PlantProcess.Application.Assistant.Retrieval")
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsInterface)
            .ToArray();

        Assert.NotEmpty(types);

        foreach (var type in types)
        {
            var settable = type
                .GetProperties(BindingFlags.Public | BindingFlags.Instance)
                .Where(p => p.CanWrite && p.SetMethod is { IsPublic: true })
                .Where(p => p.SetMethod!.ReturnParameter
                    .GetRequiredCustomModifiers()
                    .All(m => m.Name != "IsExternalInit"))
                .Select(p => type.Name + "." + p.Name)
                .ToArray();

            Assert.Empty(settable);
        }
    }
}
