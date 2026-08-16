using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using PlantProcess.Application.Assistant.Verification;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-181 isolation boundary.
///
/// The most important guard in this file is the first one. A verifier that consulted a
/// language model to judge a language model's output would inherit the failure it
/// exists to catch, and the whole value of this layer is that it cannot.
/// </summary>
public sealed class AssistantVerificationIsolationTests
{
    private static readonly string[] VerificationFiles =
    {
        "VerificationContracts.cs",
        "ClaimPhrasePolicy.cs",
        "AnswerVerifier.cs",
        "QualityGates.cs",
        "QualityHarness.cs"
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

    private static string VerificationDirectory() => Path.Combine(
        RepositoryRoot(), "Backend", "PlantProcess.Application", "Assistant", "Verification");

    private static string CodeOf(string fileName)
    {
        var path = Path.Combine(VerificationDirectory(), fileName);
        Assert.True(File.Exists(path), "verification file is missing: " + path);

        var raw = File.ReadAllText(path);
        var withoutBlocks = Regex.Replace(raw, @"/\*[\s\S]*?\*/", string.Empty);
        return Regex.Replace(withoutBlocks, @"(?m)^\s*//.*$", string.Empty);
    }

    [Fact]
    public void TheVerifierNeverCallsAModel()
    {
        var forbidden = new[]
        {
            "IAssistant" + "Model",
            "IEmbed" + "der",
            "Model" + "Gateway",
            "Grounded" + "AssistantGateway",
            "Http" + "Client",
            "completion",
            "chat.completions"
        };

        foreach (var file in VerificationFiles)
        {
            var code = CodeOf(file);
            foreach (var needle in forbidden)
            {
                Assert.False(
                    code.Contains(needle, StringComparison.OrdinalIgnoreCase),
                    file + " names '" + needle + "' in code. A verifier that asked a model "
                        + "to judge a model would inherit the failure it exists to catch.");
            }
        }
    }

    [Fact]
    public void TheVerifierIsAPureFunctionOfItsInputs()
    {
        Assert.True(typeof(AnswerVerifier).IsAbstract);
        Assert.True(typeof(AnswerVerifier).IsSealed);
        Assert.Empty(typeof(AnswerVerifier).GetConstructors(BindingFlags.Public | BindingFlags.Instance));
        Assert.Empty(typeof(AnswerVerifier)
            .GetFields(BindingFlags.Public | BindingFlags.Instance));
    }

    [Fact]
    public void TheLayerImportsNoPresentationRouteOrInfrastructureCode()
    {
        var forbidden = new[]
        {
            "PlantProcess.Application.Dashboarding",
            "PlantProcess.Api",
            "PlantProcess.Infrastructure",
            "Microsoft.AspNetCore"
        };

        foreach (var file in VerificationFiles)
        {
            var code = CodeOf(file);
            foreach (var needle in forbidden)
            {
                Assert.False(code.Contains(needle, StringComparison.Ordinal), file + " references " + needle + ".");
            }
        }
    }

    [Fact]
    public void TheLayerPersistsNothing()
    {
        var forbidden = new[] { "Npgsql", "DbContext", "ExecuteSql", "SaveChanges", "connectionString" };

        foreach (var file in VerificationFiles)
        {
            var code = CodeOf(file);
            foreach (var needle in forbidden)
            {
                Assert.False(code.Contains(needle, StringComparison.OrdinalIgnoreCase), file + " names '" + needle + "'.");
            }
        }
    }

    [Fact]
    public void NothingRegistersTheVerifierIntoProduction()
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
            foreach (var name in new[] { nameof(AnswerVerifier), nameof(QualityHarness) })
            {
                Assert.False(
                    text.Contains(name, StringComparison.Ordinal),
                    Path.GetFileName(path) + " registers " + name + "; T-138 owns integration.");
            }
        }
    }

    [Fact]
    public void TheRuntimeGatesCarryNoFabricatedValue()
    {
        // The single most damaging thing this harness could do is report a plausible
        // latency. It cannot: the four runtime gates have no value at all.
        var gates = QualityHarness.RuntimeGates();

        Assert.Equal(4, gates.Length);
        Assert.All(gates, gate =>
        {
            Assert.Null(gate.Value);
            Assert.Null(gate.Numerator);
            Assert.Null(gate.Denominator);
            Assert.Equal(MeasurementState.CapabilityUnavailable, gate.State);
            Assert.NotEqual(GateVerdict.Pass, gate.Verdict);
        });
    }

    [Fact]
    public void TheGateIdentifiersAreTheFrozenEleven()
    {
        var names = Enum.GetValues<QualityGateId>().Select(g => g.ToString()).ToArray();

        Assert.Equal(11, names.Length);
        foreach (var expected in new[]
        {
            "Q01_ToolSelectionAccuracy", "Q02_Groundedness", "Q03_CitationCorrectness",
            "Q04_UnsupportedClaimRate", "Q05_RefusalCorrectness", "Q06_CausalOverreachRate",
            "Q07_MultilingualFidelity", "Q08_TimeToFirstToken", "Q09_TotalAnswerLatency",
            "Q10_ServingThroughput", "Q11_MemoryPerConcurrentSession"
        })
        {
            Assert.Contains(expected, names);
        }
    }

    [Fact]
    public void ThePhrasePolicyCarriesNoPlantOrDomainSpecificRule()
    {
        // The policy is about the strength of a claim, not about any process, so the
        // same table serves every industry the product is sold into.
        var code = CodeOf("ClaimPhrasePolicy.cs").ToLowerInvariant();

        foreach (var token in new[] { "temperature", "pressure", "furnace", "mill", "casting", "chemistry" })
        {
            Assert.False(code.Contains(token, StringComparison.Ordinal),
                "the phrase policy names '" + token + "', which is domain-specific.");
        }
    }

    [Fact]
    public void EveryVerificationTypeIsImmutable()
    {
        var types = typeof(VerificationReport).Assembly
            .GetTypes()
            .Where(t => t.Namespace == "PlantProcess.Application.Assistant.Verification")
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
