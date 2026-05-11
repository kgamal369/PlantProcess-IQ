using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.PlantLayout;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Materials;

public class MaterialUnitConfiguration : IEntityTypeConfiguration<MaterialUnit>
{
    public void Configure(EntityTypeBuilder<MaterialUnit> builder)
    {
        builder.ToTable("material_units");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MaterialCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.MaterialUnitType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ProductFamily).HasMaxLength(100);
        builder.Property(x => x.GradeOrRecipe).HasMaxLength(100);
        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");

        builder.Property(x => x.ProductionStartUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ProductionEndUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ProductionStartLocal).HasColumnType("timestamp without time zone");
        builder.Property(x => x.ProductionEndLocal).HasColumnType("timestamp without time zone");

        builder.Property(x => x.PlantTimeZoneId).IsRequired().HasMaxLength(100);

        builder.HasMany(x => x.Aliases)
            .WithOne()
            .HasForeignKey(x => x.MaterialUnitId)
            .OnDelete(DeleteBehavior.Cascade);
        builder.Navigation(x => x.Aliases)
             .UsePropertyAccessMode(PropertyAccessMode.Field);

        builder.HasIndex(x => new { x.SiteId, x.MaterialCode }).IsUnique();

        builder.HasIndex(x => new { x.SourceSystem, x.SourceRecordId })
            .IsUnique()
            .HasFilter("source_system IS NOT NULL AND source_record_id IS NOT NULL");
        
        builder.HasIndex(x => x.SiteId);
        builder.HasIndex(x => x.MaterialUnitType);
        builder.HasIndex(x => new { x.SiteId, x.MaterialUnitType });
        builder.HasIndex(x => new { x.MaterialUnitType, x.GradeOrRecipe });

        builder.HasOne<Site>()
            .WithMany()
            .HasForeignKey(x => x.SiteId)
            .OnDelete(DeleteBehavior.Restrict);
        builder.UsePostgresXminConcurrencyToken();
    }
}