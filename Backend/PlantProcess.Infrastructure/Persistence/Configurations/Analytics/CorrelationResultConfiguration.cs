using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Analytics;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;
namespace PlantProcess.Infrastructure.Persistence.Configurations.Analytics;

public class CorrelationResultConfiguration : IEntityTypeConfiguration<CorrelationResult>
{
    public void Configure(EntityTypeBuilder<CorrelationResult> builder)
    {
        builder.ToTable("correlation_results");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CorrelationType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SubjectCode).IsRequired().HasMaxLength(200);
        builder.Property(x => x.OutcomeCode).IsRequired().HasMaxLength(200);
        builder.Property(x => x.Score).HasPrecision(18, 6);
        builder.Property(x => x.ResultJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.Property(x => x.CalculatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.CorrelationType);
        builder.HasIndex(x => x.SubjectCode);
        builder.HasIndex(x => x.OutcomeCode);
        builder.HasIndex(x => x.CalculatedAtUtc);
        builder.HasIndex(x => new { x.CorrelationType, x.SubjectCode, x.OutcomeCode });

        builder.UsePostgresXminConcurrencyToken();
    }
}
