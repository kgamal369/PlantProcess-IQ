using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.Process;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;


namespace PlantProcess.Infrastructure.Persistence.Configurations.Process;

public class DowntimeEventConfiguration : IEntityTypeConfiguration<DowntimeEvent>
{
    public void Configure(EntityTypeBuilder<DowntimeEvent> builder)
    {
        // Chapter 3 4.5.4 names both columns, their type and their CHECK.
        builder.ToTable("downtime_events", t =>
        {
            t.HasCheckConstraint("ck_downtime_events_stopped_minutes_nonneg", "stopped_minutes >= 0");
            t.HasCheckConstraint("ck_downtime_events_production_impact_minutes_nonneg", "production_impact_minutes >= 0");
        });

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DowntimeType).IsRequired().HasMaxLength(100);

        builder.Property(x => x.StoppedMinutes)
            .HasColumnName("stopped_minutes")
            .HasColumnType("numeric(12,3)")
            .IsRequired();

        builder.Property(x => x.ProductionImpactMinutes)
            .HasColumnName("production_impact_minutes")
            .HasColumnType("numeric(12,3)")
            .IsRequired();
        builder.Property(x => x.ReasonCode).HasMaxLength(100);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.PlantTimeZoneId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");

        builder.Property(x => x.StartedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.EndedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.StartedAtLocal).HasColumnType("timestamp without time zone");
        builder.Property(x => x.EndedAtLocal).HasColumnType("timestamp without time zone");

        builder.HasOne<MaterialUnit>()
            .WithMany()
            .HasForeignKey(x => x.MaterialUnitId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasOne<ProcessStepExecution>()
            .WithMany()
            .HasForeignKey(x => x.ProcessStepExecutionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.MaterialUnitId);
        builder.HasIndex(x => x.ProcessStepExecutionId);
        builder.HasIndex(x => x.EquipmentId);
        builder.HasIndex(x => x.DowntimeType);
        builder.HasIndex(x => x.StartedAtUtc);
        builder.HasIndex(x => x.StartedAtLocal);

        builder.UsePostgresXminConcurrencyToken();
    }
}