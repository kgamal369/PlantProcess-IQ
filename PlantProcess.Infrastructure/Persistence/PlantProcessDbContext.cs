using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PlantProcess.Domain.Common;
using PlantProcess.Domain.Entities.Analytics;
using PlantProcess.Domain.Entities.Configuration;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.PlantLayout;
using PlantProcess.Domain.Entities.Process;
using PlantProcess.Domain.Entities.Quality;
using PlantProcess.Application.Common.Persistence;

namespace PlantProcess.Infrastructure.Persistence;

public class PlantProcessDbContext : DbContext, IPlantProcessDbContext
{
    // ----------------------------
    // Plant Layout
    // ----------------------------
    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Equipment> Equipment => Set<Equipment>();

    // Optional alias if you prefer plural naming in some endpoints/services.
    public DbSet<Equipment> Equipments => Set<Equipment>();

    // ----------------------------
    // Configuration / Templates
    // ----------------------------
    public DbSet<IndustryTemplate> IndustryTemplates => Set<IndustryTemplate>();
    public DbSet<MaterialUnitTypeDefinition> MaterialUnitTypeDefinitions => Set<MaterialUnitTypeDefinition>();
    public DbSet<OperationDefinition> OperationDefinitions => Set<OperationDefinition>();
    public DbSet<Route> Routes => Set<Route>();
    public DbSet<RouteStep> RouteSteps => Set<RouteStep>();

    // ----------------------------
    // Integration / Ingestion
    // ----------------------------
    public DbSet<SourceSystemDefinition> SourceSystemDefinitions => Set<SourceSystemDefinition>();
    public DbSet<ImportBatch> ImportBatches => Set<ImportBatch>();
    public DbSet<MappingDefinition> MappingDefinitions => Set<MappingDefinition>();

    // ----------------------------
    // Materials / Genealogy
    // ----------------------------
    public DbSet<MaterialUnit> MaterialUnits => Set<MaterialUnit>();
    public DbSet<MaterialAlias> MaterialAliases => Set<MaterialAlias>();
    public DbSet<GenealogyEdge> GenealogyEdges => Set<GenealogyEdge>();

    // ----------------------------
    // Process / Parameters / Events
    // ----------------------------
    public DbSet<ProcessStepExecution> ProcessStepExecutions => Set<ProcessStepExecution>();
    public DbSet<ParameterDefinition> ParameterDefinitions => Set<ParameterDefinition>();
    public DbSet<ParameterObservation> ParameterObservations => Set<ParameterObservation>();
    public DbSet<ProcessEvent> ProcessEvents => Set<ProcessEvent>();
    public DbSet<DowntimeEvent> DowntimeEvents => Set<DowntimeEvent>();

    // ----------------------------
    // Quality / Data Quality
    // ----------------------------
    public DbSet<DefectCatalog> DefectCatalogs => Set<DefectCatalog>();
    public DbSet<QualityEvent> QualityEvents => Set<QualityEvent>();
    public DbSet<DataQualityIssue> DataQualityIssues => Set<DataQualityIssue>();

    // ----------------------------
    // Analytics / ML Outputs
    // ----------------------------
    public DbSet<RiskScore> RiskScores => Set<RiskScore>();

    public PlantProcessDbContext(DbContextOptions<PlantProcessDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        // Applies all IEntityTypeConfiguration<T> classes from:
        // PlantProcess.Infrastructure/Persistence/Configurations/*
        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

        // Important:
        // Must run after configurations, so all mapped properties are already known.
        ApplyUtcDateTimeConverters(modelBuilder);
        ApplySoftDeleteQueryFilters(modelBuilder);
    }

    private static void ApplyUtcDateTimeConverters(ModelBuilder modelBuilder)
    {
        var utcConverter = new ValueConverter<DateTime, DateTime>(
            value => value.Kind == DateTimeKind.Utc
                ? value
                : DateTime.SpecifyKind(value, DateTimeKind.Utc),
            value => DateTime.SpecifyKind(value, DateTimeKind.Utc));

        var nullableUtcConverter = new ValueConverter<DateTime?, DateTime?>(
            value => value.HasValue
                ? value.Value.Kind == DateTimeKind.Utc
                    ? value.Value
                    : DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                : value,
            value => value.HasValue
                ? DateTime.SpecifyKind(value.Value, DateTimeKind.Utc)
                : value);

        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            foreach (var property in entityType.GetProperties())
            {
                // Plant-local timestamps must stay local.
                // They should be stored as "timestamp without time zone".
                // Examples:
                // ProductionStartLocal
                // StartedAtLocal
                // ObservedAtLocal
                // EventAtLocal
                // ScoredAtLocal
                if (property.Name.EndsWith("Local", StringComparison.OrdinalIgnoreCase))
                {
                    continue;
                }

                if (property.ClrType == typeof(DateTime))
                {
                    property.SetValueConverter(utcConverter);
                }

                if (property.ClrType == typeof(DateTime?))
                {
                    property.SetValueConverter(nullableUtcConverter);
                }
            }
        }
    }

    private static void ApplySoftDeleteQueryFilters(ModelBuilder modelBuilder)
    {
        foreach (var entityType in modelBuilder.Model.GetEntityTypes())
        {
            if (!typeof(BaseEntity).IsAssignableFrom(entityType.ClrType))
            {
                continue;
            }

            var parameter = Expression.Parameter(entityType.ClrType, "e");

            var isDeletedProperty = Expression.Property(
                parameter,
                nameof(BaseEntity.IsDeleted));

            var condition = Expression.Equal(
                isDeletedProperty,
                Expression.Constant(false));

            var lambda = Expression.Lambda(condition, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}