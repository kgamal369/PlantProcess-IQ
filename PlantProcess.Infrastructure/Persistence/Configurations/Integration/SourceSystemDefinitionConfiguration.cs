using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Integration;

public class SourceSystemDefinitionConfiguration : IEntityTypeConfiguration<SourceSystemDefinition>
{
    public void Configure(EntityTypeBuilder<SourceSystemDefinition> builder)
    {
        builder.ToTable("source_system_definitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SourceSystemCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SourceSystemName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.SourceSystemType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(1000);

        // T-08: new column — must be persisted
        builder.Property(x => x.IsActive).IsRequired();

        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.HasIndex(x => x.SourceSystemCode).IsUnique();
        builder.HasIndex(x => x.SourceSystemType);
        builder.HasIndex(x => x.IsActive);

        builder.UsePostgresXminConcurrencyToken();
    }
}