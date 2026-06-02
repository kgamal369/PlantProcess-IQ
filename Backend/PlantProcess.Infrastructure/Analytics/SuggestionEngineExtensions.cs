using Microsoft.Extensions.DependencyInjection;
using PlantProcess.Application.Analytics.Suggestions;

namespace PlantProcess.Infrastructure.Analytics;

public static class SuggestionEngineExtensions
{
    public static IServiceCollection AddSuggestionEngine(this IServiceCollection services)
    {
        services.AddSingleton<ISuggestionEngine, SuggestionEngine>();
        services.AddScoped<ISuggestionStore, NpgsqlSuggestionStore>();
        services.AddScoped<SuggestionGenerationJob>();
        return services;
    }
}