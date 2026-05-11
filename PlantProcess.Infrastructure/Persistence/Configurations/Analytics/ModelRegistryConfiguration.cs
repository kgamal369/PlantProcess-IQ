using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Analytics;
using PlantProcess.Infrastructure.Persistence.Configurations;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Analytics;

public class ModelRegistryConfiguration : IEntityTypeConfiguration<ModelRegistry>
{
    public void Configure(EntityTypeBuilder<ModelRegistry> builder)
    {
        builder.ToTable("model_registries");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ModelCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ModelName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ModelType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ModelVersion).IsRequired().HasMaxLength(100);
        builder.Property(x => x.RiskType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.ArtifactUri).HasMaxLength(1000);
        builder.Property(x => x.TrainingDataSummaryJson).HasColumnType("jsonb");
        builder.Property(x => x.MetricsJson).HasColumnType("jsonb");
        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.Property(x => x.RegisteredAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.ModelCode).IsUnique();
        builder.HasIndex(x => new { x.RiskType, x.ModelVersion });
        builder.HasIndex(x => x.IsActive);

        builder.UsePostgresXminConcurrencyToken();
    }
}
