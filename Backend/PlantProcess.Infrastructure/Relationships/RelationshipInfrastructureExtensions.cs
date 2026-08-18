using Microsoft.Extensions.DependencyInjection;
using PlantProcess.Application.Relationships;

namespace PlantProcess.Infrastructure.Relationships;

/// <summary>
/// T-057. One registration point for the relationship vertical.
///
/// The service and the publication seam are the SAME instance: publishing and
/// reading back must not be able to disagree about what a relationship means.
/// </summary>
public static class RelationshipInfrastructureExtensions
{
    public static IServiceCollection AddRelationshipInfrastructure(this IServiceCollection services)
    {
        services.AddScoped<IRelationshipStore, NpgsqlRelationshipStore>();
        services.AddScoped<RelationshipService>();
        services.AddScoped<IRelationshipService>(sp => sp.GetRequiredService<RelationshipService>());
        services.AddScoped<IRelationshipPublicationService>(sp => sp.GetRequiredService<RelationshipService>());

        // T-058. The resolver reads the published model through the service, and
        // the planner reads the model only through the resolver. Neither ever
        // reaches storage: that is the whole product rule, expressed as a
        // dependency graph rather than as a comment.
        services.AddScoped<IRelationshipResolver, RelationshipResolver>();
        services.AddScoped<IRelationshipJoinPlanner, RelationshipJoinPlanner>();
        return services;
    }
}