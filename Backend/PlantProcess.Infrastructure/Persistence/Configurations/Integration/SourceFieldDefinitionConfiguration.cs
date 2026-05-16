using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Persistence.Configurations;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Integration;

public sealed class SourceFieldDefinitionConfiguration : IEntityTypeConfiguration<SourceFieldDefinition>
{
    public void Configure(EntityTypeBuilder<SourceFieldDefinition> builder)
    {
        builder.ToTable("source_field_definitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.FieldName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DisplayName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.SourceDataType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SampleValue).HasMaxLength(2000);

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");

        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.HasOne<SourceDatasetDefinition>()
            .WithMany()
            .HasForeignKey(x => x.SourceDatasetDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.SourceDatasetDefinitionId);
        builder.HasIndex(x => x.FieldName);
        builder.HasIndex(x => x.SourceDataType);
        builder.HasIndex(x => x.Ordinal);
        builder.HasIndex(x => new { x.SourceDatasetDefinitionId, x.FieldName })
            .IsUnique()
            .HasFilter("is_deleted = FALSE");

        builder.UsePostgresXminConcurrencyToken();
    }
}