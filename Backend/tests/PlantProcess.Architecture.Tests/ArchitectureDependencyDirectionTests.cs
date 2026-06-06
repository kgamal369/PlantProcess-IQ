using System.Reflection;

namespace PlantProcess.Architecture.Tests;

public sealed class ArchitectureDependencyDirectionTests
{
    [Fact]
    public void Domain_must_not_reference_application_infrastructure_or_api()
    {
        AssertNoReferences(
            typeof(PlantProcess.Domain.Entities.Materials.MaterialUnit).Assembly,
            "PlantProcess.Application",
            "PlantProcess.Infrastructure",
            "PlantProcess.Api");
    }

    [Fact]
    public void Application_must_not_reference_infrastructure_or_api()
    {
        AssertNoReferences(
            typeof(PlantProcess.Application.DependencyInjection).Assembly,
            "PlantProcess.Infrastructure",
            "PlantProcess.Api");
    }

    [Fact]
    public void Infrastructure_must_not_reference_api()
    {
        AssertNoReferences(
            typeof(PlantProcess.Infrastructure.DependencyInjection).Assembly,
            "PlantProcess.Api");
    }

    [Fact]
    public void Api_must_not_reference_test_projects()
    {
        AssertNoReferences(
            typeof(Program).Assembly,
            "PlantProcess.Application.UnitTests",
            "PlantProcess.Api.IntegrationTests",
            "PlantProcess.Infrastructure.IntegrationTests",
            "PlantProcess.Architecture.Tests");
    }

    [Fact]
    public void Domain_must_remain_free_of_entity_framework_references()
    {
        AssertNoReferences(
            typeof(PlantProcess.Domain.Entities.Materials.MaterialUnit).Assembly,
            "Microsoft.EntityFrameworkCore",
            "Npgsql.EntityFrameworkCore.PostgreSQL");
    }

    [Fact]
    public void Analytics_core_must_not_depend_on_api_or_infrastructure()
    {
        var assembly = AppDomain.CurrentDomain.GetAssemblies()
            .FirstOrDefault(a => a.GetName().Name == "PlantProcess.Analytics.Core");

        if (assembly is null)
            return;

        AssertNoReferences(assembly, "PlantProcess.Api", "PlantProcess.Infrastructure");
    }

    private static void AssertNoReferences(Assembly assembly, params string[] forbidden)
    {
        var refs = assembly.GetReferencedAssemblies()
            .Select(x => x.Name ?? "")
            .ToHashSet(StringComparer.OrdinalIgnoreCase);

        foreach (var item in forbidden)
            Assert.DoesNotContain(item, refs);
    }
}