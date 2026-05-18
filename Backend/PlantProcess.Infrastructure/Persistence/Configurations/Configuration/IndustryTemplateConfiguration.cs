using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Configuration;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Configuration;

public class IndustryTemplateConfiguration : IEntityTypeConfiguration<IndustryTemplate>
{
    public void Configure(EntityTypeBuilder<IndustryTemplate> builder)
    {
        builder.ToTable("industry_templates");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.TemplateCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.TemplateName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.IndustryName).IsRequired().HasMaxLength(100);
        builder.Property(x => x.Version).IsRequired().HasMaxLength(50);
        builder.Property(x => x.Description).HasMaxLength(1000);

        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.HasIndex(x => x.TemplateCode).IsUnique();
        builder.HasIndex(x => x.IndustryName);
        builder.HasIndex(x => x.IsActive);

        builder.UsePostgresXminConcurrencyToken();
    }
}