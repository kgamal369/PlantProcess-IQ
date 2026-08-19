// ============================================================
// FILE: Backend/PlantProcess.Application/DependencyInjection.cs
// FIX: Removed duplicate using directives for
//      PlantProcess.Application.Analytics.Interfaces
//      PlantProcess.Application.Analytics.Services
//      (were declared twice — lines 20496-20497 AND 20530-20531)
// ============================================================

using Microsoft.Extensions.DependencyInjection;
using PlantProcess.Application.Analytics.Interfaces;
using PlantProcess.Application.Analytics.Engines;
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
        services.AddScoped<Jobs.Targeting.IJobTargetClassPolicy, Jobs.Targeting.DeclaredJobTargetClassPolicy>();
        // T-065 bridge. The IJobTargetLookup registration moved to Infrastructure,
        // where the composite that also reads the analysis-job compatibility store
        // lives. Application must not name an Infrastructure type, and a second
        // registration here would make the runtime authority depend on which line
        // ran last. The concrete JobTargetLookup is still registered - in
        // Infrastructure - and is still the job_definitions authority.
        services.AddScoped<Jobs.Targeting.IJobTargetResolver, Jobs.Targeting.JobTargetResolver>();

        services.AddScoped<IImportWorkflowService, ImportWorkflowService>();
        services.AddScoped<IImportBatchQueueProcessorService, ImportBatchQueueProcessorService>();
        services.AddScoped<IConnectorConfigurationService, ConnectorConfigurationService>();
        services.AddScoped<IConnectorSchemaDriftService, ConnectorSchemaDriftService>();
        services.AddScoped<ISchemaConfigurationService, SchemaConfigurationService>();
        services.AddScoped<IJobDefinitionService, JobDefinitionService>();
        services.AddScoped<IJobRegistrationService, JobRegistrationService>();
        services.AddScoped<IJobRuntimeService, JobRuntimeService>();
        services.AddScoped<IJobRunOrchestratorService, JobRunOrchestratorService>();
        services.AddScoped<IIncrementalSyncStateService, IncrementalSyncStateService>();
        services.AddScoped<IDeltaImportExecutionService, DeltaImportExecutionService>();
        services.AddScoped<IBackfillExecutionService, BackfillExecutionService>();

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
        services.AddSingleton<IEmbeddingProvider, DeterministicEmbeddingProvider>();
        services.AddSingleton<LocalNarrativeProvider>();
        services.AddSingleton<ApiNarrativeProvider>();
        services.AddSingleton<INarrativeProvider, ConfiguredNarrativeProvider>();
        services.AddScoped<IWidgetQueryExpressionService, WidgetQueryExpressionService>();
        
        // Reporting / customer demo pack
        services.AddScoped<IInvestigationReportService, InvestigationReportService>();
        // T-045-R1-B. A DUPLICATE PAIR WAS REMOVED HERE.
        //
        // CanonicalCorrelationEngine was registered twice - once fully
        // qualified here, once through the using directive four lines below.
        // IEnumerable<ICorrelationEngine> then yielded it twice, both with
        // Key "canonical", and CorrelationEngineRegistry's ToDictionary threw
        // on construction. The canonical correlation path was unreachable
        // through DI: not blocked by readiness, not short of data, simply
        // unresolvable. The registration below is the intentional one.
        
        // PPIQ-T010: canonical analytics engine registry.
        services.AddScoped<ICorrelationEngine, CanonicalCorrelationEngine>();
        services.AddScoped<ICorrelationEngineRegistry, CorrelationEngineRegistry>();
return services;
    }
}


