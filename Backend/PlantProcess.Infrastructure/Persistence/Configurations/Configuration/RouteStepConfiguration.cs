using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Configuration;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;
using PlantRoute = PlantProcess.Domain.Entities.Configuration.Route;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Configuration;

public class RouteStepConfiguration : IEntityTypeConfiguration<RouteStep>
{
    public void Configure(EntityTypeBuilder<RouteStep> builder)
    {
        builder.ToTable("route_steps");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.ExpectedMaterialUnitType).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(1000);

        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.HasOne<PlantRoute>()
            .WithMany()
            .HasForeignKey(x => x.RouteId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasOne<OperationDefinition>()
            .WithMany()
            .HasForeignKey(x => x.OperationDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.RouteId);
        builder.HasIndex(x => x.OperationDefinitionId);
        builder.HasIndex(x => new { x.RouteId, x.SequenceNo }).IsUnique();
        builder.HasIndex(x => x.ExpectedMaterialUnitType);

        builder.UsePostgresXminConcurrencyToken();
    }
}