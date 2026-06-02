using Microsoft.Extensions.DependencyInjection;
using PlantProcess.Application.Analytics.Value;

namespace PlantProcess.Infrastructure.Analytics;

public static class ValueEngineExtensions
{
    /// <summary>Registers the deterministic value engine, cost-assumption store, and value-impact repository.</summary>
    public static IServiceCollection AddValueEngine(this IServiceCollection services)
    {
        services.AddSingleton<IValueImpactEngine, ValueImpactEngine>();
        services.AddScoped<ICostAssumptionStore, NpgsqlCostAssumptionStore>();
        services.AddScoped<NpgsqlValueImpactRepository>();
        return services;
    }
}