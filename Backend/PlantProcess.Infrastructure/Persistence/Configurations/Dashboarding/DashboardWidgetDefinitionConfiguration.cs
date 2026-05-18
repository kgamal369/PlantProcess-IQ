using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Dashboarding;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Dashboarding;

public class DashboardWidgetDefinitionConfiguration : IEntityTypeConfiguration<DashboardWidgetDefinition>
{
    public void Configure(EntityTypeBuilder<DashboardWidgetDefinition> builder)
    {
        builder.ToTable("dashboard_widget_definitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.WidgetCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.WidgetTitle)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.WidgetType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.ChartType)
            .IsRequired()
            .HasMaxLength(50);

        builder.Property(x => x.DimensionCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.MeasureCode)
            .IsRequired()
            .HasMaxLength(100);

        builder.Property(x => x.ParameterCode)
            .HasMaxLength(100);

        builder.Property(x => x.FilterJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.LayoutJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.DisplayOptionsJson)
            .IsRequired()
            .HasColumnType("jsonb");

        builder.Property(x => x.SortOrder)
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

        builder.HasOne(x => x.DashboardDefinition)
            .WithMany(x => x.Widgets)
            .HasForeignKey(x => x.DashboardDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.DashboardDefinitionId);

        builder.HasIndex(x => x.WidgetCode)
            .IsUnique();

        builder.HasIndex(x => new
        {
            x.DashboardDefinitionId,
            x.SortOrder
        });

        builder.HasIndex(x => new
        {
            x.ChartType,
            x.DimensionCode,
            x.MeasureCode
        });

        builder.HasIndex(x => x.IsActive);

        builder.UsePostgresXminConcurrencyToken();
    }
}
