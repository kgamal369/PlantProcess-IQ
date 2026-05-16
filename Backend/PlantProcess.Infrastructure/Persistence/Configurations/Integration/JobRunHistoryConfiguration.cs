using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Integration;
using PlantProcess.Infrastructure.Persistence.Configurations;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Integration;

public sealed class JobRunHistoryConfiguration : IEntityTypeConfiguration<JobRunHistory>
{
    public void Configure(EntityTypeBuilder<JobRunHistory> builder)
    {
        builder.ToTable("job_run_histories");

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

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(50);

        builder.Property(x => x.TriggerSource)
            .IsRequired()
            .HasMaxLength(120);

        builder.Property(x => x.TriggeredBy)
            .HasMaxLength(200);

        builder.Property(x => x.CorrelationId)
            .HasMaxLength(120);

        builder.Property(x => x.FailureReason)
            .HasMaxLength(4000);

        builder.Property(x => x.RunMessage)
            .HasMaxLength(4000);

        builder.Property(x => x.ResultSummaryJson)
            .HasColumnType("jsonb");

        builder.Property(x => x.CreatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.UpdatedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.DeletedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.StartedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.CompletedAtUtc)
            .HasColumnType("timestamp with time zone");

        builder.Property(x => x.SourceSystem)
            .HasMaxLength(100);

        builder.Property(x => x.SourceRecordId)
            .HasMaxLength(100);

        builder.Property(x => x.DeletedReason)
            .HasMaxLength(500);

        builder.HasOne<JobDefinition>()
            .WithMany()
            .HasForeignKey(x => x.JobDefinitionId)
            .OnDelete(DeleteBehavior.Cascade);

        builder.HasIndex(x => x.JobDefinitionId);
        builder.HasIndex(x => x.JobCode);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.StartedAtUtc);
        builder.HasIndex(x => new { x.JobDefinitionId, x.StartedAtUtc });

        builder.UsePostgresXminConcurrencyToken();
    }
}