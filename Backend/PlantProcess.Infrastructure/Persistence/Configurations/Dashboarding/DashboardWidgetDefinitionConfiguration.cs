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

        builder.Property(x => x.WidgetCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.WidgetTitle).IsRequired().HasMaxLength(200);
        builder.Property(x => x.WidgetType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.ChartType).IsRequired().HasMaxLength(50);
        builder.Property(x => x.DimensionCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.MeasureCode).IsRequired().HasMaxLength(100);
        builder.Property(x => x.ParameterCode).HasMaxLength(100);

        builder.Property(x => x.FilterJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.LayoutJson).IsRequired().HasColumnType("jsonb");
        builder.Property(x => x.DisplayOptionsJson).IsRequired().HasColumnType("jsonb");

        builder.Property(x => x.SortOrder).IsRequired();
        builder.Property(x => x.IsActive).IsRequired();

        builder.Property(x => x.QueryExpression)
            .HasColumnName("query_expression")
            .HasColumnType("text");

        builder.Property(x => x.AdvancedExpressionJson)
            .HasColumnName("advanced_expression_json")
            .HasColumnType("jsonb")
            .HasDefaultValue("{}");

        builder.Property(x => x.ExpressionVersion)
            .HasColumnName("expression_version")
            .HasColumnType("smallint")
            .HasDefaultValue((short)1);

        builder.Property(x => x.ExpressionEnabled)
            .HasColumnName("expression_enabled")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.ExpressionLastValidatedAtUtc)
            .HasColumnName("expression_last_validated_at_utc")
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.ExpressionLastValidationStatus)
            .HasColumnName("expression_last_validation_status")
            .HasConversion<short>()
            .HasColumnType("smallint")
            .HasDefaultValue(WidgetExpressionStatus.Pending)
            .IsRequired();

        builder.Property(x => x.ExpressionLastValidationMessage)
            .HasColumnName("expression_last_validation_message")
            .HasColumnType("text");

        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasOne(x => x.DashboardDefinition)
            .WithMany(x => x.Widgets)
            .HasForeignKey(x => x.DashboardDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.DashboardDefinitionId);

        builder.HasIndex(x => new { x.DashboardDefinitionId, x.WidgetCode })
            .IsUnique()
            .HasDatabaseName("ix_dashboard_widget_definitions_widget_code");

        builder.HasIndex(x => new { x.DashboardDefinitionId, x.SortOrder });

        builder.HasIndex(x => new { x.ChartType, x.DimensionCode, x.MeasureCode });

        builder.HasIndex(x => x.IsActive);

        builder.HasIndex(x => new { x.ExpressionEnabled, x.ExpressionLastValidatedAtUtc })
            .HasDatabaseName("ix_dashboard_widget_definitions_expression_refresh");

        builder.UsePostgresXminConcurrencyToken();
    }
}
