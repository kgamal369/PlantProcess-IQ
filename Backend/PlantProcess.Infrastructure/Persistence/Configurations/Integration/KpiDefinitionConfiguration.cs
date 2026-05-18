using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Integration;

public sealed class KpiDefinitionConfiguration : IEntityTypeConfiguration<KpiDefinition>
{
    public void Configure(EntityTypeBuilder<KpiDefinition> builder)
    {
        builder.ToTable("kpi_definitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.KpiCode).IsRequired().HasMaxLength(120);
        builder.Property(x => x.KpiName).IsRequired().HasMaxLength(250);
        builder.Property(x => x.KpiCategory).IsRequired().HasMaxLength(80);
        builder.Property(x => x.ValueExpression).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.Unit).HasMaxLength(80);
        builder.Property(x => x.DimensionExpression).HasMaxLength(1000);
        builder.Property(x => x.FilterExpression).HasMaxLength(2000);
        builder.Property(x => x.AggregationType).IsRequired().HasMaxLength(80);
        builder.Property(x => x.Description).HasMaxLength(1000);

        builder.Property(x => x.KpiOptionsJson)
            .IsRequired()
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");

        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.HasOne<SchemaViewDefinition>()
            .WithMany()
            .HasForeignKey(x => x.SchemaViewDefinitionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.KpiCode)
            .IsUnique()
            .HasFilter("is_deleted = FALSE");

        builder.HasIndex(x => x.KpiCategory);
        builder.HasIndex(x => x.SchemaViewDefinitionId);
        builder.HasIndex(x => x.IsActive);

        builder.UsePostgresXminConcurrencyToken();
    }
}