using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Integration;

public sealed class JobRunDependencyConfiguration : IEntityTypeConfiguration<JobRunDependency>
{
    // The design's five tokens. Written here as data rather than derived from
    // the member names, because SkippedOptional and FailedUpstream do not
    // lower-case into skipped_optional and failed_upstream on their own, and a
    // clever derivation that is wrong is worse than a short table that is right.
    private static readonly Dictionary<JobDependencyResolution, string> ToToken = new()
    {
        [JobDependencyResolution.Satisfied] = "satisfied",
        [JobDependencyResolution.StaleAccepted] = "stale_accepted",
        [JobDependencyResolution.Blocked] = "blocked",
        [JobDependencyResolution.SkippedOptional] = "skipped_optional",
        [JobDependencyResolution.FailedUpstream] = "failed_upstream"
    };

    public void Configure(EntityTypeBuilder<JobRunDependency> builder)
    {
        builder.ToTable("job_run_dependencies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.RunId).HasColumnName("run_id").IsRequired();
        builder.Property(x => x.DependsOnRunId).HasColumnName("depends_on_run_id");
        builder.Property(x => x.JobDefinitionId).HasColumnName("job_definition_id").IsRequired();
        builder.Property(x => x.DependsOnJobDefinitionId).HasColumnName("depends_on_job_definition_id").IsRequired();

        builder.Property(x => x.Resolution)
            .HasColumnName("resolution")
            .HasMaxLength(20)
            .IsRequired()
            .HasConversion(
                value => ToToken[value],
                value => ToToken.First(pair => pair.Value == value).Key);

        builder.Property(x => x.ResolvedAtUtc)
            .HasColumnName("resolved_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        builder.Property(x => x.ExpectedVersion).HasColumnName("expected_version");
        builder.Property(x => x.ActualVersion).HasColumnName("actual_version");
        builder.Property(x => x.Reason).HasColumnName("reason");
        builder.Property(x => x.WatermarkInherited).HasColumnName("watermark_inherited");

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.HasIndex(x => x.RunId);
        builder.HasIndex(x => x.DependsOnRunId);
    }
}