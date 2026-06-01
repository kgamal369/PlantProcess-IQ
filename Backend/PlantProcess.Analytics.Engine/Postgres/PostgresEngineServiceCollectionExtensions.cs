using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using PlantProcess.Application.Analytics.Interfaces;

namespace PlantProcess.Analytics.Engine.Postgres;

public static class PostgresEngineServiceCollectionExtensions
{
    /// <summary>
    /// Registers the Postgres feature source + finding sink and the managed engine as a KEYED
    /// ICorrelationComputeEngine ("managed"), leaving the default PostgresCorrelationComputeEngine untouched.
    /// Requires NpgsqlDataSource to already be registered (it is, in Infrastructure DI).
    /// </summary>
    public static IServiceCollection AddManagedStatisticalEngineWithPostgres(this IServiceCollection services, double freshnessCadenceDays = 7.0)
    {
        services.AddSingleton<ICanonicalFeatureSource>(sp =>
            new PostgresCanonicalFeatureSource(sp.GetRequiredService<NpgsqlDataSource>(), freshnessCadenceDays));
        services.AddSingleton<IAnalysisFindingSink>(sp =>
            new PostgresAnalysisFindingSink(sp.GetRequiredService<NpgsqlDataSource>()));
        services.AddKeyedScoped<ICorrelationComputeEngine, ManagedStatisticalComputeEngine>(AnalyticsEngineServiceCollectionExtensions.ManagedEngineKey);
        return services;
    }
}