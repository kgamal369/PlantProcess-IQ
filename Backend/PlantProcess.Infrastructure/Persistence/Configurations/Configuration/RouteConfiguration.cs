using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Configuration;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;
using PlantRoute = PlantProcess.Domain.Entities.Configuration.Route;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Configuration;

public class RouteConfiguration : IEntityTypeConfiguration<PlantRoute>
{
    public void Configure(EntityTypeBuilder<PlantRoute> builder)
    {
        builder.ToTable("routes");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RouteCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.RouteName).IsRequired().HasMaxLength(200);
        builder.Property(x => x.ProductFamily).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(1000);

        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.HasOne<IndustryTemplate>()
            .WithMany()
            .HasForeignKey(x => x.IndustryTemplateId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.IndustryTemplateId);
        builder.HasIndex(x => new { x.IndustryTemplateId, x.RouteCode }).IsUnique();
        builder.HasIndex(x => x.ProductFamily);
        builder.HasIndex(x => x.IsActive);

        builder.UsePostgresXminConcurrencyToken();
    }
}