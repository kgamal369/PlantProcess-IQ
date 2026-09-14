using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Integration;

/// <summary>
/// PPIQ T-099. ToTable carries the name only. Schema placement comes from
/// StorageTopologyMap through StorageTopologyConvention, which is the single
/// placement authority for the whole model.
///
/// EXCLUDEFROMMIGRATIONS KEEPS 840 THE SINGLE DDL AUTHORITY, the same convention
/// T-213 states for 833_customer_assessment_history.sql and T-039 uses for
/// definition_versions. Without it this entity is a model change with no
/// migration behind it, and the canonical build's EF baseline tier refuses with
/// PendingModelChangesWarning before a single script runs. The table is created
/// by script 840 and by nothing else, which is what the manifest declares.
/// </summary>
public class ProjectionQuarantineConfiguration : IEntityTypeConfiguration<ProjectionQuarantineRecord>
{
    public void Configure(EntityTypeBuilder<ProjectionQuarantineRecord> builder)
    {
        builder.ToTable("projection_quarantine", table => table.ExcludeFromMigrations());

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ValidationCode)
            .IsRequired()
            .HasMaxLength(8);

        builder.Property(x => x.Detail)
            .IsRequired();

        builder.Property(x => x.OffendingValue);

        builder.Property(x => x.SuggestedCorrection)
            .IsRequired();

        builder.Property(x => x.ImportBatchId).IsRequired();
        builder.Property(x => x.StagingRecordId).IsRequired();
        builder.Property(x => x.StagingRowNumber).IsRequired();

        builder.Property(x => x.SourceObjectName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.MappingDefinitionId).IsRequired();

        builder.Property(x => x.MappingVersion)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.TargetEntityName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.TenantId).IsRequired();

        builder.Property(x => x.State)
            .IsRequired()
            .HasMaxLength(20);

        builder.Property(x => x.AttemptCount).IsRequired();
        builder.Property(x => x.LastAttemptAtUtc).IsRequired();
        builder.Property(x => x.ResolvedAtUtc);
        builder.Property(x => x.ResolvedCanonicalId);

        builder.HasIndex(x => x.ValidationCode);
        builder.HasIndex(x => x.ImportBatchId);
        builder.HasIndex(x => new { x.MappingDefinitionId, x.MappingVersion });
        builder.HasIndex(x => x.TenantId);
    }
}
