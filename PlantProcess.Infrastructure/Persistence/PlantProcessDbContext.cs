using System.Linq.Expressions;
using System.Reflection;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PlantProcess.Domain.Common;
using PlantProcess.Domain.Entities.Analytics;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.PlantLayout;
using PlantProcess.Domain.Entities.Process;
using PlantProcess.Domain.Entities.Quality;


namespace PlantProcess.Infrastructure.Persistence;

public class PlantProcessDbContext : DbContext
{
    public DbSet<MaterialUnit> MaterialUnits => Set<MaterialUnit>();
    public DbSet<MaterialAlias> MaterialAliases => Set<MaterialAlias>();
    public DbSet<GenealogyEdge> GenealogyEdges => Set<GenealogyEdge>();

    public DbSet<ProcessStepExecution> ProcessStepExecutions => Set<ProcessStepExecution>();
    public DbSet<ParameterDefinition> ParameterDefinitions => Set<ParameterDefinition>();
    public DbSet<ParameterObservation> ParameterObservations => Set<ParameterObservation>();

    public DbSet<DefectCatalog> DefectCatalogs => Set<DefectCatalog>();
    public DbSet<QualityEvent> QualityEvents => Set<QualityEvent>();
    public DbSet<ProcessEvent> ProcessEvents => Set<ProcessEvent>();
    public DbSet<DowntimeEvent> DowntimeEvents => Set<DowntimeEvent>();
    public DbSet<RiskScore> RiskScores => Set<RiskScore>();
    public DbSet<DataQualityIssue> DataQualityIssues => Set<DataQualityIssue>();

    public DbSet<Site> Sites => Set<Site>();
    public DbSet<Area> Areas => Set<Area>();
    public DbSet<Equipment> Equipment => Set<Equipment>();
    public DbSet<SourceSystemDefinition> SourceSystemDefinitions => Set<SourceSystemDefinition>();

    public PlantProcessDbContext(DbContextOptions<PlantProcessDbContext> options)
        : base(options)
    {
    }

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        base.OnModelCreating(modelBuilder);

        modelBuilder.ApplyConfigurationsFromAssembly(Assembly.GetExecutingAssembly());

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
                // Important:
                // Plant-local timestamps must stay local and must not be converted to UTC.
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
            var property = Expression.Property(parameter, nameof(BaseEntity.IsDeleted));
            var condition = Expression.Equal(property, Expression.Constant(false));
            var lambda = Expression.Lambda(condition, parameter);

            modelBuilder.Entity(entityType.ClrType).HasQueryFilter(lambda);
        }
    }
}