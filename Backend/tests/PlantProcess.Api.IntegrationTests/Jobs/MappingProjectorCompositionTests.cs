using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PlantProcess.Application.Common.Persistence;
using PlantProcess.Application.Integration.Interfaces.Mapping;
using PlantProcess.Application.Integration.Services.Mapping;
using PlantProcess.Application.Temporal;
using PlantProcess.Infrastructure.Persistence;
using Xunit;

namespace PlantProcess.Api.IntegrationTests.Jobs;

// A focused constructor-composition regression, not a whole-host acceptance test.
// Descriptors come from production registration methods. Database calls are not made.
public sealed class MappingProjectorCompositionTests
{
    private static ServiceProvider Provider()
    {
        const string connection = "Host=127.0.0.1;Port=1;Database=ppiq_composition_unused;Username=unused;Password=unused;Timeout=1";
        IConfiguration configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["ConnectionStrings:PlantProcessDb"] = connection
            }).Build();
        var registered = new ServiceCollection();
        PlantProcess.Application.DependencyInjection.AddApplication(registered);
        PlantProcess.Infrastructure.DependencyInjection.AddInfrastructure(registered, configuration);

        IServiceCollection focused = new ServiceCollection();
        foreach (Type type in new[]
        {
            typeof(IMappingRowProjector), typeof(ProjectionRowValidationService),
            typeof(ISourceTimeAuthorityRegistryProvider)
        })
        {
            ServiceDescriptor descriptor = Assert.Single(registered.Where(d => d.ServiceType == type));
            Assert.Equal(ServiceLifetime.Scoped, descriptor.Lifetime);
            focused.Add(descriptor);
        }
        focused.AddSingleton<NpgsqlDataSource>(_ => NpgsqlDataSource.Create(connection));
        focused.AddScoped<IPlantProcessDbContext>(_ => new PlantProcessDbContext(
            new DbContextOptionsBuilder<PlantProcessDbContext>().UseNpgsql(connection).Options));
        return focused.BuildServiceProvider(new ServiceProviderOptions
        {
            ValidateOnBuild = true, ValidateScopes = true
        });
    }

    private static object? Dependency(object instance, string name)
    {
        FieldInfo? field = instance.GetType().GetField(name, BindingFlags.Instance | BindingFlags.NonPublic);
        Assert.NotNull(field);
        return field!.GetValue(instance);
    }

    [Fact]
    public void Registered_projector_receives_both_production_dependencies()
    {
        using var provider = Provider();
        using var scope = provider.CreateScope();
        var services = scope.ServiceProvider;
        var projector = services.GetRequiredService<IMappingRowProjector>();
        Assert.IsType<MappingRowProjector>(projector);
        Assert.Same(services.GetRequiredService<IPlantProcessDbContext>(), Dependency(projector, "_dbContext"));
        Assert.Same(services.GetRequiredService<ISourceTimeAuthorityRegistryProvider>(), Dependency(projector, "_sourceTimeAuthority"));
        Assert.Same(services.GetRequiredService<ProjectionRowValidationService>(), Dependency(projector, "_advancedValidation"));
    }

    [Fact]
    public void Projector_and_validation_follow_the_request_scope()
    {
        using var provider = Provider();
        using var first = provider.CreateScope();
        using var second = provider.CreateScope();
        var a = first.ServiceProvider.GetRequiredService<IMappingRowProjector>();
        var b = second.ServiceProvider.GetRequiredService<IMappingRowProjector>();
        Assert.Same(a, first.ServiceProvider.GetRequiredService<IMappingRowProjector>());
        Assert.NotSame(a, b);
        Assert.NotSame(Dependency(a, "_dbContext"), Dependency(b, "_dbContext"));
        Assert.NotSame(Dependency(a, "_advancedValidation"), Dependency(b, "_advancedValidation"));
        Assert.NotSame(Dependency(a, "_sourceTimeAuthority"), Dependency(b, "_sourceTimeAuthority"));
    }
}
