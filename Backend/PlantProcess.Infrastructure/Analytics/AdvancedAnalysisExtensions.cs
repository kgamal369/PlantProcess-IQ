using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using PlantProcess.Application.Analytics.Advanced;

namespace PlantProcess.Infrastructure.Analytics;

/// <summary>
/// P3 DI. Registers the .NET Analytics.Core compute service alongside the existing
/// Pearson path. The "Analytics:AdvancedEngine:Enabled" flag is read here for the
/// Phase-4 live swap; in Phase 3 the service is registered but NOT wired to the live
/// correlation endpoint.
/// </summary>
public static class AdvancedAnalysisExtensions
{
    public static IServiceCollection AddAdvancedAnalysis(this IServiceCollection services)
    {
        services.AddScoped<IFeatureVectorLoader, NpgsqlFeatureVectorLoader>();
        services.AddScoped<IAdvancedResultWriter, NpgsqlAdvancedResultWriter>();
        services.AddScoped<IAdvancedCorrelationService, AdvancedCorrelationComputeService>();
        return services;
    }
}
