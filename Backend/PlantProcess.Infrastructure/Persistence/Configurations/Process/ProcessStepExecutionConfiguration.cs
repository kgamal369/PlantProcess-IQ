using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Configuration;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.Process;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Process;

public class ProcessStepExecutionConfiguration : IEntityTypeConfiguration<ProcessStepExecution>
{
    public void Configure(EntityTypeBuilder<ProcessStepExecution> builder)
    {
        builder.ToTable("process_step_executions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.OperationType).IsRequired().HasMaxLength(100);
        builder.Property(x => x.OperationCode).HasMaxLength(100);
        builder.Property(x => x.CrewCode).HasMaxLength(100);
        builder.Property(x => x.ExecutionStatus).IsRequired().HasMaxLength(50);
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
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<OperationDefinition>()
            .WithMany()
            .HasForeignKey(x => x.OperationDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(x => x.MaterialUnitId);
        builder.HasIndex(x => x.EquipmentId);
        builder.HasIndex(x => x.OperationDefinitionId);
        builder.HasIndex(x => x.OperationType);
        builder.HasIndex(x => x.OperationCode);
        builder.HasIndex(x => x.StartedAtUtc);
        builder.HasIndex(x => x.StartedAtLocal);
        builder.HasIndex(x => new { x.MaterialUnitId, x.OperationType, x.StartedAtUtc });
        builder.HasIndex(x => new { x.OperationType, x.StartedAtLocal });
        builder.HasIndex(x => new { x.CrewCode, x.StartedAtLocal });

        builder.UsePostgresXminConcurrencyToken();
    }
}