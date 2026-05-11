using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.PlantLayout;
using PlantProcess.Infrastructure.Persistence.Configurations;

namespace PlantProcess.Infrastructure.Persistence.Configurations.PlantLayout;

public class SiteConfiguration : IEntityTypeConfiguration<Site>
{
    public void Configure(EntityTypeBuilder<Site> builder)
    {
        builder.ToTable("sites");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.SiteCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SiteName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.CompanyName).HasMaxLength(200);
        builder.Property(x => x.CountryCode).HasMaxLength(10);
        builder.Property(x => x.TimeZoneId).IsRequired().HasMaxLength(100);

        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.HasIndex(x => x.SiteCode).IsUnique();
        builder.HasIndex(x => x.CountryCode);

        builder.UsePostgresXminConcurrencyToken();
    }
}