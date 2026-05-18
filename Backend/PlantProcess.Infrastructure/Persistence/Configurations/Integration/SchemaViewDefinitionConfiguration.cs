using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Integration;

public sealed class SchemaViewDefinitionConfiguration : IEntityTypeConfiguration<SchemaViewDefinition>
{
    public void Configure(EntityTypeBuilder<SchemaViewDefinition> builder)
    {
        builder.ToTable("schema_view_definitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SchemaViewCode).IsRequired().HasMaxLength(120);
        builder.Property(x => x.SchemaViewName).IsRequired().HasMaxLength(250);
        builder.Property(x => x.ViewKind).IsRequired().HasMaxLength(80);

        builder.Property(x => x.SqlText)
            .IsRequired()
            .HasColumnType("text");

        builder.Property(x => x.SourceDatasetIdsJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("[]");

        builder.Property(x => x.OutputSchemaJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("[]");

        builder.Property(x => x.LastValidationStatus).HasMaxLength(50);
        builder.Property(x => x.LastValidationMessage).HasMaxLength(2000);
        builder.Property(x => x.Description).HasMaxLength(1000);

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.LastValidatedAtUtc).HasColumnType("timestamp with time zone");

        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.HasOne<SourceDatasetDefinition>()
            .WithMany()
            .HasForeignKey(x => x.PrimarySourceDatasetDefinitionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.SchemaViewCode)
            .IsUnique()
            .HasFilter("is_deleted = FALSE");

        builder.HasIndex(x => x.ViewKind);
        builder.HasIndex(x => x.PrimarySourceDatasetDefinitionId);
        builder.HasIndex(x => x.IsActive);
        builder.HasIndex(x => x.IsApproved);
        builder.HasIndex(x => x.LastValidationStatus);

        builder.UsePostgresXminConcurrencyToken();
    }
}