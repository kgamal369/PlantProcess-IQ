using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Configuration;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Configuration;

public class OperationDefinitionConfiguration : IEntityTypeConfiguration<OperationDefinition>
{
    public void Configure(EntityTypeBuilder<OperationDefinition> builder)
    {
        builder.ToTable("operation_definitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OperationCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.OperationName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.OperationCategory).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(1000);

        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.HasOne<IndustryTemplate>()
            .WithMany()
            .HasForeignKey(x => x.IndustryTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.IndustryTemplateId);
        builder.HasIndex(x => new { x.IndustryTemplateId, x.OperationCode }).IsUnique();
        builder.HasIndex(x => x.OperationCategory);
        builder.HasIndex(x => x.IsActive);

        builder.UsePostgresXminConcurrencyToken();
    }
}