using Microsoft.Extensions.DependencyInjection;
using PlantProcess.Application.Analytics.Interfaces;

namespace PlantProcess.Analytics.Engine;

public static class AnalyticsEngineServiceCollectionExtensions
{
    public const string ManagedEngineKey = "managed";

    /// <summary>
    /// Registers the managed engine as a KEYED ICorrelationComputeEngine ("managed"), leaving the existing
    /// default (PostgresCorrelationComputeEngine) untouched. Requires ICanonicalFeatureSource and
    /// IAnalysisFindingSink to be registered (Postgres adapters = increment 1b).
    /// </summary>
    public static IServiceCollection AddManagedStatisticalEngine(this IServiceCollection services)
    {
        services.AddKeyedScoped<ICorrelationComputeEngine, ManagedStatisticalComputeEngine>(ManagedEngineKey);
        return services;
    }
}