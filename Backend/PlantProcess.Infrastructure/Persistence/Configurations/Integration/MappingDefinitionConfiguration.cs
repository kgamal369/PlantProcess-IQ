using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Integration;

public class MappingDefinitionConfiguration : IEntityTypeConfiguration<MappingDefinition>
{
    public void Configure(EntityTypeBuilder<MappingDefinition> builder)
    {
        builder.ToTable("mapping_definitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.MappingCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.MappingName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.SourceObjectName)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.TargetEntityName)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.MappingJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.MappingVersion)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.SourceSystem)
            .HasMaxLength(100);

        builder.Property(x => x.SourceRecordId)
            .HasMaxLength(100);

        builder.Property(x => x.DeletedReason)
            .HasMaxLength(500);

        builder.HasOne<SourceSystemDefinition>()
            .WithMany()
            .HasForeignKey(x => x.SourceSystemDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.SourceSystemDefinitionId);

        builder.HasIndex(x => x.MappingCode)
            .IsUnique();

        builder.HasIndex(x => new
        {
            x.SourceSystemDefinitionId,
            x.SourceObjectName,
            x.TargetEntityName,
            x.MappingVersion
        });

        builder.HasIndex(x => x.IsActive);

        builder.UsePostgresXminConcurrencyToken();
    }
}