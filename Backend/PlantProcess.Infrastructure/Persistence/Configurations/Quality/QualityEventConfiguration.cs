using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.Quality;

using PlantProcess.Infrastructure.Persistence.Configurations;
namespace PlantProcess.Infrastructure.Persistence.Configurations.Quality;

public class QualityEventConfiguration : IEntityTypeConfiguration<QualityEvent>
{
    public void Configure(EntityTypeBuilder<QualityEvent> builder)
    {
        builder.ToTable("quality_events");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Severity).HasMaxLength(50);
        builder.Property(x => x.Decision).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.PlantTimeZoneId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");

        builder.Property(x => x.EventAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.EventAtLocal).HasColumnType("timestamp without time zone");

        builder.HasOne<MaterialUnit>()
            .WithMany()
            .HasForeignKey(x => x.MaterialUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<DefectCatalog>()
            .WithMany(x => x.QualityEvents)
            .HasForeignKey(x => x.DefectCatalogId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.MaterialUnitId);
        builder.HasIndex(x => x.DefectCatalogId);
        builder.HasIndex(x => x.EventType);
        builder.HasIndex(x => x.EventAtUtc);
        builder.HasIndex(x => x.EventAtLocal);
        builder.HasIndex(x => new { x.MaterialUnitId, x.EventType, x.EventAtUtc });

        builder.UsePostgresXminConcurrencyToken();
    }
}
