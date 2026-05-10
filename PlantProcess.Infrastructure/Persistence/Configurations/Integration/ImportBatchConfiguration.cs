using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Persistence.Configurations;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Integration;

public class ImportBatchConfiguration : IEntityTypeConfiguration<ImportBatch>
{
    public void Configure(EntityTypeBuilder<ImportBatch> builder)
    {
        builder.ToTable("import_batches");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ImportBatchCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ImportType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Status).IsRequired().HasMaxLength(50);
        builder.Property(x => x.SourceObjectName).HasMaxLength(200);
        builder.Property(x => x.FileName).HasMaxLength(500);
        builder.Property(x => x.Checksum).HasMaxLength(200);
        builder.Property(x => x.ErrorMessage).HasMaxLength(2000);

        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.Property(x => x.StartedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CompletedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasOne<SourceSystemDefinition>()
            .WithMany()
            .HasForeignKey(x => x.SourceSystemDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SourceSystemDefinitionId);
        builder.HasIndex(x => x.ImportBatchCode).IsUnique();
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.StartedAtUtc);

        builder.UsePostgresXminConcurrencyToken();
    }
}