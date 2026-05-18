using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Dashboarding;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Dashboarding;

public class DashboardDefinitionConfiguration : IEntityTypeConfiguration<DashboardDefinition>
{
    public void Configure(EntityTypeBuilder<DashboardDefinition> builder)
    {
        builder.ToTable("dashboard_definitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DashboardCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.Name)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.LayoutJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.IsDefault)
            .IsRequired();

        builder.Property(x => x.IsSystemTemplate)
            .IsRequired();

        builder.Property(x => x.IsActive)
            .IsRequired();

        builder.Property(x => x.SourceSystem)
            .HasMaxLength(100);

        builder.Property(x => x.SourceRecordId)
            .HasMaxLength(100);

        builder.Property(x => x.DeletedReason)
            .HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.DeletedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.HasMany(x => x.Widgets)
            .WithOne(x => x.DashboardDefinition)
            .HasForeignKey(x => x.DashboardDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.DashboardCode)
            .IsUnique();

        builder.HasIndex(x => x.UserId);

        builder.HasIndex(x => x.IsDefault);

        builder.HasIndex(x => x.IsSystemTemplate);

        builder.HasIndex(x => x.IsActive);

        builder.UsePostgresXminConcurrencyToken();
    }
}
