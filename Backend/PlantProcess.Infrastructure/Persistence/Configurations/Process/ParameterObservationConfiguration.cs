using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Materials;
using PlantProcess.Domain.Entities.Process;
using PlantProcess.Infrastructure.Persistence.Configurations.Common;


namespace PlantProcess.Infrastructure.Persistence.Configurations.Process;

public class ParameterObservationConfiguration : IEntityTypeConfiguration<ParameterObservation>
{
    public void Configure(EntityTypeBuilder<ParameterObservation> builder)
    {
        builder.ToTable("parameter_observations");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");

        builder.Property(x => x.ObservedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.ObservedAtLocal).HasColumnType("timestamp without time zone");
        builder.Property(x => x.PlantTimeZoneId).IsRequired().HasMaxLength(100);

        builder.Property(x => x.NumericValue).HasPrecision(18, 6);
        builder.Property(x => x.TextValue).HasMaxLength(500);
        builder.Property(x => x.UnitOfMeasure).HasMaxLength(50);
        builder.Property(x => x.QualityFlag).IsRequired().HasMaxLength(50);
        builder.Property(x => x.RawValue).HasMaxLength(500);
        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.HasOne<MaterialUnit>()
            .WithMany()
            .HasForeignKey(x => x.MaterialUnitId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ParameterDefinition>()
            .WithMany()
            .HasForeignKey(x => x.ParameterDefinitionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<ProcessStepExecution>()
            .WithMany()
            .HasForeignKey(x => x.ProcessStepExecutionId)
            .OnDelete(DeleteBehavior.SetNull);

        builder.HasIndex(x => x.MaterialUnitId);
        builder.HasIndex(x => x.ParameterDefinitionId);
        builder.HasIndex(x => x.ProcessStepExecutionId);
        builder.HasIndex(x => x.EquipmentId);
        builder.HasIndex(x => x.ObservedAtUtc);
        builder.HasIndex(x => x.ObservedAtLocal);
        builder.HasIndex(x => new { x.MaterialUnitId, x.ParameterDefinitionId, x.ObservedAtUtc });

        builder.UsePostgresXminConcurrencyToken();
    }
}