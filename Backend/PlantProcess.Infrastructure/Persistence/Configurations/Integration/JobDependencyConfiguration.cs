using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Integration;

/// <summary>
/// T-106. The dependency edge, mapped without a schema name: placement is
/// topology, and StorageTopologyConvention applies the frozen assignment after
/// every configuration has run. Naming it here would be a second placement
/// authority disagreeing with the first.
/// </summary>
public sealed class JobDependencyConfiguration : IEntityTypeConfiguration<JobDependency>
{
    public void Configure(EntityTypeBuilder<JobDependency> builder)
    {
        builder.ToTable("job_dependencies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.JobDefinitionId)
            .HasColumnName("job_definition_id")
            .IsRequired();

        builder.Property(x => x.DependsOnJobDefinitionId)
            .HasColumnName("depends_on_job_definition_id")
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.DeletedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.SourceSystem)
            .HasMaxLength(100);

        builder.Property(x => x.SourceRecordId)
            .HasMaxLength(100);

        builder.Property(x => x.DeletedReason)
            .HasMaxLength(500);

        builder.HasIndex(x => x.JobDefinitionId);
        builder.HasIndex(x => x.DependsOnJobDefinitionId);

        builder.HasIndex(x => new { x.JobDefinitionId, x.DependsOnJobDefinitionId })
            .IsUnique();
    }
}