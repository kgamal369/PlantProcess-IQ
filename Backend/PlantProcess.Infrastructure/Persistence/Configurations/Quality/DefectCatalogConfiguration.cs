using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Quality;


namespace PlantProcess.Infrastructure.Persistence.Configurations.Quality;

public class DefectCatalogConfiguration : IEntityTypeConfiguration<DefectCatalog>
{
    public void Configure(EntityTypeBuilder<DefectCatalog> builder)
    {
        builder.ToTable("defect_catalogs");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DefectCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.DefectName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.DefectCategory).HasMaxLength(100);
        builder.Property(x => x.IndustryTemplate).HasMaxLength(100);
        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.DefectCode).IsUnique();
        builder.HasIndex(x => x.DefectCategory);
        builder.HasIndex(x => x.IndustryTemplate);

        builder.UsePostgresXminConcurrencyToken();
    }
}