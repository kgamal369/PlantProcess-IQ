using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Analytics;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Analytics;

public class RiskScoreConfiguration : IEntityTypeConfiguration<RiskScore>
{
    public void Configure(EntityTypeBuilder<RiskScore> builder)
    {
        builder.ToTable("risk_scores");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RiskType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Score).HasPrecision(8, 6);
        builder.Property(x => x.RiskClass).HasMaxLength(50);
        builder.Property(x => x.MainContributorsJson).HasColumnType("jsonb");
        builder.Property(x => x.ModelVersion).HasMaxLength(100);
        builder.Property(x => x.PlantTimeZoneId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");

        builder.Property(x => x.ScoredAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ScoredAtLocal).HasColumnType("timestamp without time zone");

        builder.HasOne<MaterialUnit>()
            .WithMany()
            .HasForeignKey(x => x.MaterialUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.MaterialUnitId);
        builder.HasIndex(x => x.RiskType);
        builder.HasIndex(x => x.ScoredAtUtc);
        builder.HasIndex(x => new { x.MaterialUnitId, x.RiskType, x.ScoredAtUtc });

        builder.UsePostgresXminConcurrencyToken();
    }
}