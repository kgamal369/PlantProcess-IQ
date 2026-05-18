using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Integration;

public sealed class StagingRecordConfiguration : IEntityTypeConfiguration<StagingRecord>
{
    public void Configure(EntityTypeBuilder<StagingRecord> builder)
    {
        builder.ToTable("staging_records");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceObjectName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.RowNumber).IsRequired();
        builder.Property(x => x.RawJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.ProcessingStatus).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ProcessingError).HasMaxLength(4000);
        builder.Property(x => x.CanonicalEntityName).HasMaxLength(200);
        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ProcessedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasOne<ImportBatch>()
            .WithMany()
            .HasForeignKey(x => x.ImportBatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ImportBatchId);
        builder.HasIndex(x => new { x.ImportBatchId, x.IsProcessed });
        builder.HasIndex(x => new { x.ImportBatchId, x.ProcessingStatus });
        builder.HasIndex(x => new { x.ImportBatchId, x.SourceObjectName, x.RowNumber }).IsUnique();
        builder.HasIndex(x => x.CanonicalEntityId);

        builder.UsePostgresXminConcurrencyToken();
    }
}
