// ============================================================
// FILE: Backend/PlantProcess.Application/DependencyInjection.cs
// FIX: Removed duplicate using directives for
//      PlantProcess.Application.Analytics.Interfaces
//      PlantProcess.Application.Analytics.Services
//      (were declared twice — lines 20496-20497 AND 20530-20531)
// ============================================================

using Microsoft.Extensions.DependencyInjection;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Analytics.Services;
using PlantProcess.Application.Common.Time;
using PlantProcess.Application.Dashboarding.Interfaces;
using PlantProcess.Application.Dashboarding.Services.Dashboards;
using PlantProcess.Application.Dashboarding.Services.Metadata;
using PlantProcess.Application.Dashboarding.Services.Queries;
using PlantProcess.Application.Dashboarding.Services.Widgets;
using PlantProcess.Application.Demo.Interfaces;
using PlantProcess.Application.Demo.Services;
using PlantProcess.Application.Integration.Contracts;
using PlantProcess.Application.Integration.Contracts.Jobs;
using PlantProcess.Application.Integration.Interfaces.Connectors;
using PlantProcess.Application.Integration.Interfaces.Import;
using PlantProcess.Application.Integration.Interfaces.Jobs;
using PlantProcess.Application.Integration.Interfaces.Mapping;
using PlantProcess.Application.Integration.Interfaces.SchemaConfiguration;
using PlantProcess.Application.Integration.Interfaces.SourceSystems;
using PlantProcess.Application.Integration.Interfaces.Staging;
using PlantProcess.Application.Integration.Services.Connectors;
using PlantProcess.Application.Integration.Services.Import;
using PlantProcess.Application.Integration.Services.Jobs;
using PlantProcess.Application.Integration.Services.Mapping;
using PlantProcess.Application.Integration.Services.SchemaConfiguration;
using PlantProcess.Application.Integration.Services.SourceSystems;
using PlantProcess.Application.Integration.Services.Staging;
using PlantProcess.Application.Licensing.Interfaces;
using PlantProcess.Application.Licensing.Options;
using PlantProcess.Application.Licensing.Services;
using PlantProcess.Application.Services.DataQuality;
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

        // Commercial license / feature enforcement
        services.AddOptions<LicenseOptions>()
            .BindConfiguration(LicenseOptions.SectionName)
            .Validate(options => !string.IsNullOrWhiteSpace(options.Tier), "PlantProcess:License:Tier is required.");

        services.AddSingleton<ILicenseService, LicenseService>();

        // Demo lifecycle readiness and proof surface
        services.AddScoped<IDemoLifecycleService, DemoLifecycleService>();

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
        services.AddScoped<IRiskScoreService, RiskScoreService>();
        services.AddScoped<IDashboardQueryService, DashboardQueryService>();
        services.AddScoped<IDashboardMetadataService, DashboardMetadataService>();
        services.AddScoped<IDashboardWidgetValidationService, DashboardWidgetValidationService>();
        services.AddScoped<IDashboardWidgetQueryService, DashboardWidgetQueryService>();
        services.AddScoped<IDashboardDefinitionService, DashboardDefinitionService>();

        // Analytics — correlation, feature engineering, ML readiness
        services.AddScoped<ICorrelationService, CorrelationService>();
        services.AddScoped<IFeatureEngineeringService, FeatureEngineeringService>();
        services.AddScoped<IQualityLabelBuilderService, QualityLabelBuilderService>();
        services.AddScoped<IMlReadinessService, MlReadinessService>();
        services.AddScoped<IWidgetQueryExpressionService, WidgetQueryExpressionService>();
        
        // Reporting / customer demo pack
        services.AddScoped<IInvestigationReportService, InvestigationReportService>();

        return services;
    }
}