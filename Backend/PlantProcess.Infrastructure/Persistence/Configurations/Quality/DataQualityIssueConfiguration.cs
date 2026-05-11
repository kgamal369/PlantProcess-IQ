using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.Quality;


namespace PlantProcess.Infrastructure.Persistence.Configurations.Quality;

public class DataQualityIssueConfiguration : IEntityTypeConfiguration<DataQualityIssue>
{
    public void Configure(EntityTypeBuilder<DataQualityIssue> builder)
    {
        builder.ToTable("data_quality_issues");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.IssueType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Severity).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Description).IsRequired().HasMaxLength(2000);
        builder.Property(x => x.AffectedEntityName).HasMaxLength(200);
        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasOne<MaterialUnit>()
            .WithMany()
            .HasForeignKey(x => x.MaterialUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.MaterialUnitId);
        builder.HasIndex(x => x.IssueType);
        builder.HasIndex(x => x.Severity);
        builder.HasIndex(x => x.AffectedEntityId);

        builder.UsePostgresXminConcurrencyToken();
    }
}