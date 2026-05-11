using Microsoft.EntityFrameworkCore;
using PlantProcess.Domain.Entities.Analytics;
using PlantProcess.Domain.Entities.Configuration;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.PlantLayout;
using PlantProcess.Domain.Entities.Process;
using PlantProcess.Domain.Entities.Quality;
using System.Collections.Generic;

namespace PlantProcess.Application.Common.Persistence;

public interface IPlantProcessDbContext
{
    DbSet<Site> Sites { get; }
    DbSet<Area> Areas { get; }
    DbSet<Equipment> Equipment { get; }
    DbSet<Equipment> Equipments { get; }

    DbSet<IndustryTemplate> IndustryTemplates { get; }
    DbSet<MaterialUnitTypeDefinition> MaterialUnitTypeDefinitions { get; }
    DbSet<OperationDefinition> OperationDefinitions { get; }
    DbSet<Route> Routes { get; }
    DbSet<RouteStep> RouteSteps { get; }

    DbSet<SourceSystemDefinition> SourceSystemDefinitions { get; }
    DbSet<ImportBatch> ImportBatches { get; }
    DbSet<MappingDefinition> MappingDefinitions { get; }
    DbSet<StagingRecord> StagingRecords { get; }

    DbSet<MaterialUnit> MaterialUnits { get; }
    DbSet<MaterialAlias> MaterialAliases { get; }
    DbSet<GenealogyEdge> GenealogyEdges { get; }

    DbSet<ProcessStepExecution> ProcessStepExecutions { get; }
    DbSet<ParameterDefinition> ParameterDefinitions { get; }
    DbSet<ParameterObservation> ParameterObservations { get; }
    DbSet<ProcessEvent> ProcessEvents { get; }
    DbSet<DowntimeEvent> DowntimeEvents { get; }

    DbSet<DefectCatalog> DefectCatalogs { get; }
    DbSet<QualityEvent> QualityEvents { get; }
    DbSet<DataQualityIssue> DataQualityIssues { get; }

    DbSet<RiskScore> RiskScores { get; }
    DbSet<CorrelationResult> CorrelationResults { get; }
    DbSet<ModelRegistry> ModelRegistries { get; }

    Task<int> SaveChangesAsync(CancellationToken cancellationToken = default);
}
