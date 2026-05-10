using Microsoft.Extensions.DependencyInjection;
using PlantProcess.Application.Common.Time;
using PlantProcess.Application.Services.Readiness;
using PlantProcess.Application.Services.Analytics;
using PlantProcess.Application.Services.DataQuality;
using PlantProcess.Application.Services.Integration;
using PlantProcess.Application.Services.Materials;
using PlantProcess.Application.Services.Process;
using PlantProcess.Application.Services.Quality;

namespace PlantProcess.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Cross-cutting application services
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPlantTimeContextResolver, PlantTimeContextResolver>();


        // Phase 1 real implementation service
        services.AddScoped<IApplicationReadinessService, ApplicationReadinessService>();

        // Phase 1 service contracts.
        // Concrete implementations will be added in Phase 2 when endpoint logic is moved from API to Application.
        services.AddScoped<ISourceSystemService, SourceSystemService>();
        services.AddScoped<IImportBatchService, ImportBatchService>();
        services.AddScoped<IMappingDefinitionService, MappingDefinitionService>();

        services.AddScoped<IMaterialService, MaterialService>();
        services.AddScoped<IGenealogyService, GenealogyService>();

        services.AddScoped<IProcessDataService, ProcessDataService>();
        services.AddScoped<IQualityService, QualityService>();
        services.AddScoped<IRiskScoreService, RiskScoreService>();
        services.AddScoped<IDataQualityService, DataQualityService>();

        return services;
    }
}