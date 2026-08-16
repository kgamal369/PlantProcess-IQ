using System;
using System.IO;
using System.Linq;
using System.Reflection;
using System.Text.RegularExpressions;
using PlantProcess.Application.Assistant.Planning;
using Xunit;

namespace PlantProcess.Architecture.Tests;

/// <summary>
/// T-179 probe J and the isolation boundary.
///
/// Tool selection must be reproducible and auditable, which it cannot be if a model
/// participates in it. This file proves the absence of any model participation two
/// ways: by reading the planner's own source for anything that could reach a model,
/// and by reading its compiled surface for a dependency that could carry one in.
///
/// It also proves the planner is isolated: nothing registers it, nothing routes to it,
/// and it executes no tool. Integration is T-138's subject and no part of T-179.
/// </summary>
public sealed class AssistantPlannerIsolationTests
{
    private static readonly string[] PlannerFiles =
    {
        "PlanningContracts.cs",
        "DeterministicToolPlanner.cs"
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

    private static string PlannerDirectory() => Path.Combine(
        RepositoryRoot(), "Backend", "PlantProcess.Application", "Assistant", "Planning");

    /// <summary>Source with comments removed, so a guard judges code and not prose.</summary>
    private static string CodeOf(string fileName)
    {
        var path = Path.Combine(PlannerDirectory(), fileName);
        Assert.True(File.Exists(path), "planner file is missing: " + path);

        var raw = File.ReadAllText(path);
        var withoutBlocks = Regex.Replace(raw, @"/\*[\s\S]*?\*/", string.Empty);
        return Regex.Replace(withoutBlocks, @"(?m)^\s*//.*$", string.Empty);
    }

    // ---------------------------------------------------------------- probe J

    [Fact]
    public void ProbeJ_ThePlannerSourceCannotReachAModel()
    {
        // Assembled from fragments so this guard cannot match itself.
        var forbidden = new[]
        {
            "IAssistant" + "Model",
            "IEmbed" + "der",
            "Model" + "Gateway",
            "Grounded" + "AssistantGateway",
            "Assistant" + "Service",
            "Http" + "Client",
            "prompt",
            "completion",
            "embedding"
        };

        foreach (var file in PlannerFiles)
        {
            var code = CodeOf(file);
            foreach (var needle in forbidden)
            {
                Assert.False(
                    code.Contains(needle, StringComparison.OrdinalIgnoreCase),
                    file + " names '" + needle + "', which is a path to a model.");
            }
        }
    }

    [Fact]
    public void ProbeJ_ThePlannerTakesNoDependencyThatCouldCarryAModel()
    {
        // A static class with one entry point taking one request. There is no
        // constructor through which a gateway, a client or an embedder could be
        // injected, and no instance state that one could be assigned to.
        Assert.True(typeof(DeterministicToolPlanner).IsAbstract);
        Assert.True(typeof(DeterministicToolPlanner).IsSealed);

        var constructors = typeof(DeterministicToolPlanner)
            .GetConstructors(BindingFlags.Public | BindingFlags.Instance);
        Assert.Empty(constructors);

        var fields = typeof(DeterministicToolPlanner)
            .GetFields(BindingFlags.Public | BindingFlags.NonPublic | BindingFlags.Instance | BindingFlags.Static);
        Assert.Empty(fields);
    }

    [Fact]
    public void ProbeJ_TheOnlyPublicEntryPointIsAPureFunctionOfTheRequest()
    {
        var methods = typeof(DeterministicToolPlanner)
            .GetMethods(BindingFlags.Public | BindingFlags.Static | BindingFlags.DeclaredOnly)
            .ToArray();

        var plan = Assert.Single(methods);
        Assert.Equal(nameof(DeterministicToolPlanner.Plan), plan.Name);

        var parameter = Assert.Single(plan.GetParameters());
        Assert.Equal(typeof(PlanningRequest), parameter.ParameterType);
        Assert.Equal(typeof(ToolPlan), plan.ReturnType);
    }

    [Fact]
    public void ProbeJ_PlanningTheSameRequestRepeatedlyNeverVaries()
    {
        // A model call would be the most likely source of variation between runs, and
        // there is none. Repeated planning is byte-identical on its fingerprint.
        var registry = ToolRegistry.Of(
            DeclaredTool.Create("layer_a.exact", ToolLayer.LayerA, ToolExactness.Exact, ClaimClass.ObservedFact, "unit_scope"));

        var request = new PlanningRequest(
            PermissionContext.Of("tenant_fixture", "process_engineer", "layer_a.exact"),
            ResolvedIntent.Create("exact_probe", ClaimClass.ObservedFact, true, "unit_scope"),
            System.Collections.Immutable.ImmutableArray.Create(
                ResolvedEntity.Bound("unit_scope", "unit_scope_0001")),
            registry);

        var first = DeterministicToolPlanner.Plan(request).PlanFingerprint();
        for (var attempt = 0; attempt < 25; attempt++)
        {
            Assert.Equal(first, DeterministicToolPlanner.Plan(request).PlanFingerprint());
        }
    }

    // ------------------------------------------------------------- isolation

    [Fact]
    public void ThePlannerExecutesNothingAndPersistsNothing()
    {
        // "Process." would match the product's own namespace, so the needle names the
        // type precisely. A guard that cannot tell a legitimate literal from a
        // violation reports the wrong thing.
        var forbidden = new[]
        {
            "Npgsql", "DbContext", "ExecuteSql", "SaveChanges", "InsertInto",
            "HttpClient", "Socket", "Process.Start", "File.Write", "Directory.Create"
        };

        foreach (var file in PlannerFiles)
        {
            var code = CodeOf(file);
            foreach (var needle in forbidden)
            {
                Assert.False(
                    code.Contains(needle, StringComparison.Ordinal),
                    file + " names '" + needle + "', which this task must not do.");
            }
        }
    }

    [Fact]
    public void ThePlannerIsRegisteredNowhere()
    {
        // Integration is T-138. A registration here would be a cutover this task is
        // explicitly forbidden from making.
        var root = RepositoryRoot();
        var candidates = new[]
        {
            Path.Combine(root, "Backend", "PlantProcess.Application", "DependencyInjection.cs"),
            Path.Combine(root, "Backend", "PlantProcess.Api", "Program.cs")
        };

        foreach (var path in candidates.Where(File.Exists))
        {
            var text = File.ReadAllText(path);
            Assert.False(
                text.Contains(nameof(DeterministicToolPlanner), StringComparison.Ordinal),
                Path.GetFileName(path) + " registers the planner; T-138 owns integration.");
        }
    }

    [Fact]
    public void ThePlannerLivesInItsOwnNamespaceAndTouchesNoExistingAssistantType()
    {
        foreach (var file in PlannerFiles)
        {
            var code = CodeOf(file);
            Assert.Contains("namespace PlantProcess.Application.Assistant.Planning", code, StringComparison.Ordinal);

            // The existing Assistant surface is not referenced, so this module cannot
            // change M1 behaviour and cannot be broken by work on it.
            Assert.DoesNotContain("using PlantProcess.Application.Assistant;", code, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void EveryPlannerTypeIsImmutable()
    {
        // A plan a caller could mutate after the fact is not an audit record.
        var types = typeof(ToolPlan).Assembly
            .GetTypes()
            .Where(t => t.Namespace == "PlantProcess.Application.Assistant.Planning")
            .Where(t => t.IsClass && !t.IsAbstract)
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
