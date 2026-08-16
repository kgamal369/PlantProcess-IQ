using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using PlantProcess.Application.Assistant.Serving;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-137 isolation boundary.
///
/// The frozen validation asks for a repository dependency test proving that no
/// production assistant registration and no M1 route or file changed in this task. That
/// is the subject of this file: the runtime exists, and nothing in the running product
/// can reach it.
/// </summary>
public sealed class AssistantServingIsolationTests
{
    private static readonly string[] ServingFiles =
    {
        "ServingContracts.cs",
        "ScopedPayloadBuilder.cs",
        "GovernedModelServingRuntime.cs",
        "ServingReadiness.cs"
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

    private static string ServingDirectory() => Path.Combine(
        RepositoryRoot(), "Backend", "PlantProcess.Application", "Assistant", "Serving");

    private static string CodeOf(string fileName)
    {
        var path = Path.Combine(ServingDirectory(), fileName);
        Assert.True(File.Exists(path), "serving file is missing: " + path);

        var raw = File.ReadAllText(path);
        var withoutBlocks = Regex.Replace(raw, @"/\*[\s\S]*?\*/", string.Empty);
        return Regex.Replace(withoutBlocks, @"(?m)^\s*//.*$", string.Empty);
    }

    [Fact]
    public void NothingRegistersTheServingRuntimeIntoProduction()
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
            foreach (var name in new[]
            {
                nameof(GovernedModelServingRuntime), nameof(IModelServingRuntime),
                nameof(IModelTransport), nameof(ModelGatewayAdapter)
            })
            {
                Assert.False(
                    text.Contains(name, StringComparison.Ordinal),
                    Path.GetFileName(path) + " registers " + name + "; T-138 owns the cutover.");
            }
        }
    }

    [Fact]
    public void TheExistingAssistantSurfaceDoesNotReferenceThisRuntime()
    {
        // The M1 dock keeps working exactly as it did. If it named this runtime, the
        // cutover would already have happened.
        var assistant = Path.Combine(
            RepositoryRoot(), "Backend", "PlantProcess.Application", "Assistant");

        var existing = Directory.Exists(assistant)
            ? Directory.GetFiles(assistant, "*.cs", SearchOption.TopDirectoryOnly)
            : Array.Empty<string>();

        foreach (var path in existing)
        {
            var text = File.ReadAllText(path);
            Assert.False(
                text.Contains("Assistant.Serving", StringComparison.Ordinal),
                Path.GetFileName(path) + " references the serving namespace.");
        }
    }

    [Fact]
    public void TheLayerCarriesNoConcreteHttpOrProviderClient()
    {
        // Transport is a seam. A concrete client here would reintroduce exactly the
        // provider-specific assumption this task exists to remove.
        var forbidden = new[]
        {
            "Http" + "Client", "WebRequest", "RestSharp", "OpenAI", "Azure.AI",
            "Anthropic", "System.Net.Http"
        };

        foreach (var file in ServingFiles)
        {
            var code = CodeOf(file);
            foreach (var needle in forbidden)
            {
                Assert.False(
                    code.Contains(needle, StringComparison.OrdinalIgnoreCase),
                    file + " names '" + needle + "', which belongs behind the transport seam.");
            }
        }
    }

    [Fact]
    public void TheLayerAssumesNoVerbOrProviderBodyShape()
    {
        var forbidden = new[] { "\"POST\"", "\"GET\"", "chat/completions", "\"messages\"", "\"choices\"" };

        foreach (var file in ServingFiles)
        {
            var code = CodeOf(file);
            foreach (var needle in forbidden)
            {
                Assert.False(
                    code.Contains(needle, StringComparison.Ordinal),
                    file + " assumes the provider shape '" + needle + "'.");
            }
        }
    }

    [Fact]
    public void TheLayerPersistsNothingAndReachesNoStore()
    {
        var forbidden = new[] { "Npgsql", "DbContext", "SaveChanges", "connectionString" };

        foreach (var file in ServingFiles)
        {
            var code = CodeOf(file);
            foreach (var needle in forbidden)
            {
                Assert.False(code.Contains(needle, StringComparison.OrdinalIgnoreCase), file + " names '" + needle + "'.");
            }
        }
    }

    [Fact]
    public void TheRequestTypeHasNowhereToPutTenancyOrPermission()
    {
        var names = typeof(ModelInvocationRequest)
            .GetProperties(BindingFlags.Public | BindingFlags.Instance)
            .Select(p => p.Name.ToLowerInvariant())
            .ToArray();

        foreach (var forbidden in new[] { "tenant", "role", "permission", "permitted", "omitted", "fingerprint" })
        {
            Assert.DoesNotContain(names, name => name.Contains(forbidden, StringComparison.Ordinal));
        }
    }

    [Fact]
    public void TheReadinessReportCannotClaimARuntimeThatNothingStarted()
    {
        var report = ServingReadiness.ForIsolatedImplementation(contractTestsPass: true);

        Assert.True(report.IsAttained(ServingReadinessState.ImplementationGreen));
        Assert.False(report.IsAttained(ServingReadinessState.RuntimeStarted));
        Assert.False(report.IsAttained(ServingReadinessState.BenchmarkMeasured));
        Assert.False(report.IsProductionCertified);
    }

    [Fact]
    public void EveryServingTypeIsImmutable()
    {
        var types = typeof(ModelInvocationRequest).Assembly
            .GetTypes()
            .Where(t => t.Namespace == "PlantProcess.Application.Assistant.Serving")
            .Where(t => t.IsClass && !t.IsAbstract && !t.IsInterface)
            .Where(t => t != typeof(GovernedModelServingRuntime))
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
