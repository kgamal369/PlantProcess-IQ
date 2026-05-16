using Microsoft.Extensions.DependencyInjection;
using PlantProcess.Application.Common.Time;
using PlantProcess.Application.Services.Analytics.Interfaces;
using PlantProcess.Application.Services.Analytics.Services;
using PlantProcess.Application.Services.Dashboard.Interfaces;
using PlantProcess.Application.Services.Dashboard.Services;
using PlantProcess.Application.Services.DataQuality;
using PlantProcess.Application.Services.Integration;
using PlantProcess.Application.Services.Integration.Interfaces;
using PlantProcess.Application.Services.Integration.Jobs;
using PlantProcess.Application.Services.Integration.Services;
using PlantProcess.Application.Services.Materials;
using PlantProcess.Application.Services.PlantLayout;
using PlantProcess.Application.Services.Process;
using PlantProcess.Application.Services.Quality;
using PlantProcess.Application.Services.Readiness;
using PlantProcess.Application.Services.Reporting;

namespace PlantProcess.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        // Cross-cutting application services
        services.AddSingleton<IClock, SystemClock>();
        services.AddSingleton<IPlantTimeContextResolver, PlantTimeContextResolver>();

        // Readiness
        services.AddScoped<IApplicationReadinessService, ApplicationReadinessService>();

        // Integration / ingestion / mapping / import orchestration
        services.AddScoped<ISourceSystemService, SourceSystemService>();
        services.AddScoped<IImportBatchService, ImportBatchService>();
        services.AddScoped<IMappingDefinitionService, MappingDefinitionService>();
        services.AddScoped<IStagingRecordService, StagingRecordService>();
        services.AddScoped<IMappingExecutionService, MappingExecutionService>();
        services.AddScoped<IImportWorkflowService, ImportWorkflowService>();
        services.AddScoped<IImportBatchQueueProcessorService, ImportBatchQueueProcessorService>();
        services.AddScoped<IConnectorConfigurationService, ConnectorConfigurationService>();
        services.AddScoped<ISchemaConfigurationService, SchemaConfigurationService>();
        services.AddScoped<IJobDefinitionService, JobDefinitionService>();
        services.AddScoped<IJobRegistrationService, JobRegistrationService>();
        services.AddScoped<IJobRuntimeService, JobRuntimeService>();
        services.AddScoped<IJobRunOrchestratorService, JobRunOrchestratorService>();
        services.AddScoped<IIncrementalSyncStateService, IncrementalSyncStateService>();

        // Canonical material and genealogy workflow
        services.AddScoped<IMaterialService, MaterialService>();
        services.AddScoped<IGenealogyService, GenealogyService>();

        // Plant layout read model
        services.AddScoped<IPlantLayoutQueryService, PlantLayoutQueryService>();

        // Process and quality workflow
        services.AddScoped<IProcessDataService, ProcessDataService>();
        services.AddScoped<IQualityService, QualityService>();
        services.AddScoped<IQualityQueryService, QualityQueryService>();

        // Data quality and analytics
        services.AddScoped<IDataQualityService, DataQualityService>();
        services.AddScoped<IFeatureEngineeringService, FeatureEngineeringService>();
        services.AddScoped<IRiskScoreService, RiskScoreService>();
        services.AddScoped<ICorrelationService, CorrelationService>();
        services.AddScoped<IDashboardQueryService, DashboardQueryService>();
        services.AddScoped<IDashboardMetadataService, DashboardMetadataService>();
        services.AddScoped<IDashboardWidgetValidationService, DashboardWidgetValidationService>();
        services.AddScoped<IDashboardWidgetQueryService, DashboardWidgetQueryService>();
        services.AddScoped<IDashboardDefinitionService, DashboardDefinitionService>();


        // Reporting / customer demo pack
        services.AddScoped<IInvestigationReportService, InvestigationReportService>();

        return services;
    }
}
