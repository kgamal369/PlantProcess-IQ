using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Persistence.Configurations;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Integration;

public sealed class JobDefinitionConfiguration : IEntityTypeConfiguration<JobDefinition>
{
    public void Configure(EntityTypeBuilder<JobDefinition> builder)
    {
        builder.ToTable("job_definitions");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.JobCode)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(x => x.JobName)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(x => x.JobType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(80);

        builder.Property(x => x.TargetType)
            .HasMaxLength(120);

        builder.Property(x => x.ScheduleExpression)
            .IsRequired()
            .HasMaxLength(250);

        builder.Property(x => x.LastRunStatus)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.LastFailureReason)
            .HasMaxLength(4000);

        builder.Property(x => x.Description)
            .HasMaxLength(1000);

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.DeletedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.LastRunStartedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.LastRunCompletedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.NextRunAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.SourceSystem)
            .HasMaxLength(100);

        builder.Property(x => x.SourceRecordId)
            .HasMaxLength(100);

        builder.Property(x => x.DeletedReason)
            .HasMaxLength(500);

        builder.HasIndex(x => x.JobType);
        builder.HasIndex(x => x.IsEnabled);
        builder.HasIndex(x => x.LastRunStatus);
        builder.HasIndex(x => x.TargetId);

        builder.HasIndex(x => x.JobCode)
            .IsUnique()
            .HasFilter("is_deleted = FALSE");

        builder.UsePostgresXminConcurrencyToken();
    }
}