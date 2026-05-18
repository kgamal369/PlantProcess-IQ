using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.Process;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;


namespace PlantProcess.Infrastructure.Persistence.Configurations.Process;

public class ProcessEventConfiguration : IEntityTypeConfiguration<ProcessEvent>
{
    public void Configure(EntityTypeBuilder<ProcessEvent> builder)
    {
        builder.ToTable("process_events");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.EventType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.EventValue).HasMaxLength(200);
        builder.Property(x => x.Description).HasMaxLength(1000);
        builder.Property(x => x.PlantTimeZoneId).IsRequired().HasMaxLength(100);
        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");

        builder.Property(x => x.EventAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.EventAtLocal).HasColumnType("timestamp without time zone");

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
        builder.HasIndex(x => x.EventType);
        builder.HasIndex(x => x.EventAtUtc);
        builder.HasIndex(x => new { x.EventType, x.EventAtUtc });

        builder.UsePostgresXminConcurrencyToken();
    }
}