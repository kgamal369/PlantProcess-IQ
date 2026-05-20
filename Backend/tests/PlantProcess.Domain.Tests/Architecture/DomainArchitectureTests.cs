using System.Runtime.CompilerServices;
using FluentAssertions;
using PlantProcess.Domain.Common;

namespace PlantProcess.Domain.Tests.Architecture;

public sealed class DomainArchitectureTests
{
    [Fact]
    public void All_real_domain_entities_should_inherit_base_entity()
    {
        var domainAssembly = typeof(BaseEntity).Assembly;

        var entityTypes = domainAssembly
            .GetTypes()
            .Where(type =>
                type.IsClass
                && !type.IsAbstract
                && !type.IsNested
                && !type.IsDefined(typeof(CompilerGeneratedAttribute), inherit: false)
                && type.Namespace != null
                && type.Namespace.Contains(".Entities.", StringComparison.OrdinalIgnoreCase)
                && !type.Name.Contains("<", StringComparison.OrdinalIgnoreCase)
                && !type.Name.Contains(">", StringComparison.OrdinalIgnoreCase))
            .ToList();

        entityTypes.Should().NotBeEmpty("the domain assembly must contain real entity classes");

        entityTypes
            .Where(type => !typeof(BaseEntity).IsAssignableFrom(type))
            .Should()
            .BeEmpty("all real domain entities must inherit BaseEntity for audit, timestamps, soft delete and concurrency consistency");
    }

    [Fact]
    public void Domain_entities_should_not_reference_entity_framework()
    {
        var domainAssembly = typeof(BaseEntity).Assembly;

        var forbiddenReferences = domainAssembly
            .GetReferencedAssemblies()
            .Where(assembly =>
                assembly.Name != null
                && assembly.Name.StartsWith("Microsoft.EntityFrameworkCore", StringComparison.OrdinalIgnoreCase))
            .ToList();

        forbiddenReferences
            .Should()
            .BeEmpty("Domain project must stay persistence-agnostic");
    }
}