using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Integration;

public sealed class SourceDatasetDefinitionConfiguration : IEntityTypeConfiguration<SourceDatasetDefinition>
{
    public void Configure(EntityTypeBuilder<SourceDatasetDefinition> builder)
    {
        builder.ToTable("source_dataset_definitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DatasetCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.DatasetName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DatasetKind).IsRequired().HasMaxLength(50);
        builder.Property(x => x.SourceObjectName).IsRequired().HasMaxLength(300);
        builder.Property(x => x.SourceSchemaName).HasMaxLength(200);
        builder.Property(x => x.PrimaryTimestampField).HasMaxLength(200);
        builder.Property(x => x.IncrementalCursorField).HasMaxLength(200);
        builder.Property(x => x.LastCursorValue).HasMaxLength(500);
        builder.Property(x => x.Description).HasMaxLength(1000);

        builder.Property(x => x.DatasetOptionsJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");

        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.HasOne<ConnectionProfile>()
            .WithMany()
            .HasForeignKey(x => x.ConnectionProfileId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.ConnectionProfileId);
        builder.HasIndex(x => x.DatasetKind);
        builder.HasIndex(x => x.SourceObjectName);
        builder.HasIndex(x => x.IsActive);

        builder.HasIndex(x => new { x.ConnectionProfileId, x.DatasetCode })
            .IsUnique()
            .HasFilter("is_deleted = FALSE");

        builder.UsePostgresXminConcurrencyToken();
    }
}