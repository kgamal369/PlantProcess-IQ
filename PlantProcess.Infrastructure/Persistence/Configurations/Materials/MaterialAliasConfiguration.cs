using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Materials;


namespace PlantProcess.Infrastructure.Persistence.Configurations.Materials;

public class MaterialAliasConfiguration : IEntityTypeConfiguration<MaterialAlias>
{
    public void Configure(EntityTypeBuilder<MaterialAlias> builder)
    {
        builder.ToTable("material_aliases");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.AliasCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.AliasType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.SourceSystem).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.MaterialUnitId);
        builder.HasIndex(x => x.AliasCode);
        builder.HasIndex(x => x.SourceSystem);

        builder.HasIndex(x => new
        {
            x.MaterialUnitId,
            x.AliasCode,
            x.SourceSystem
        }).IsUnique();

        builder.UsePostgresXminConcurrencyToken();
    }
}