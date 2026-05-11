using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.PlantLayout;
using PlantProcess.Infrastructure.Persistence.Configurations;

namespace PlantProcess.Infrastructure.Persistence.Configurations.PlantLayout;

public class EquipmentConfiguration : IEntityTypeConfiguration<Equipment>
{
    public void Configure(EntityTypeBuilder<Equipment> builder)
    {
        builder.ToTable("equipment");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EquipmentCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.EquipmentName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.EquipmentType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Manufacturer).HasMaxLength(200);

        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.HasOne<Site>()
            .WithMany()
            .HasForeignKey(x => x.SiteId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Area>()
            .WithMany()
            .HasForeignKey(x => x.AreaId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<Equipment>()
            .WithMany()
            .HasForeignKey(x => x.ParentEquipmentId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SiteId);
        builder.HasIndex(x => x.AreaId);
        builder.HasIndex(x => x.ParentEquipmentId);
        builder.HasIndex(x => x.EquipmentType);
        builder.HasIndex(x => x.IsActive);

        builder.HasIndex(x => new { x.SiteId, x.EquipmentCode })
            .IsUnique();

        builder.UsePostgresXminConcurrencyToken();
    }
}