using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Materials;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Materials;

public class GenealogyEdgeConfiguration : IEntityTypeConfiguration<GenealogyEdge>
{
    public void Configure(EntityTypeBuilder<GenealogyEdge> builder)
    {
        builder.ToTable("genealogy_edges");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RelationshipType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");

        builder.Property(x => x.EffectiveFromUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.EffectiveToUtc).HasColumnType("timestamp with time zone");

        builder.HasOne<MaterialUnit>()
            .WithMany()
            .HasForeignKey(x => x.ParentMaterialUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<MaterialUnit>()
            .WithMany()
            .HasForeignKey(x => x.ChildMaterialUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.ParentMaterialUnitId);
        builder.HasIndex(x => x.ChildMaterialUnitId);
        builder.HasIndex(x => new { x.ParentMaterialUnitId, x.ChildMaterialUnitId }).IsUnique();

        builder.UsePostgresXminConcurrencyToken();
    }
}