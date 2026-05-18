using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.PlantLayout;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;

namespace PlantProcess.Infrastructure.Persistence.Configurations.PlantLayout;

public class AreaConfiguration : IEntityTypeConfiguration<Area>
{
    public void Configure(EntityTypeBuilder<Area> builder)
    {
        builder.ToTable("areas");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AreaCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.AreaName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.AreaType).HasMaxLength(100);

        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.HasOne<Site>()
            .WithMany()
            .HasForeignKey(x => x.SiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Area>()
            .WithMany()
            .HasForeignKey(x => x.ParentAreaId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SiteId);
        builder.HasIndex(x => x.ParentAreaId);
        builder.HasIndex(x => x.AreaType);

        builder.HasIndex(x => new { x.SiteId, x.AreaCode })
            .IsUnique();

        builder.UsePostgresXminConcurrencyToken();
    }
}