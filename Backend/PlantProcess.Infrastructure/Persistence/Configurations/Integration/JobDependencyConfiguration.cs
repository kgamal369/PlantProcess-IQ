using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Integration;

public sealed class JobDependencyConfiguration : IEntityTypeConfiguration<JobDependency>
{
    public void Configure(EntityTypeBuilder<JobDependency> builder)
    {
        builder.ToTable("job_dependencies");

        builder.HasKey(x => x.Id);

        builder.Property(x => x.JobDefinitionId)
            .HasColumnName("job_definition_id")
            .IsRequired();

        builder.Property(x => x.DependsOnJobDefinitionId)
            .HasColumnName("depends_on_job_definition_id")
            .IsRequired();

        // Stored as the lower-case token the design and the CHECK both name, not
        // as the C# member spelling, so the database vocabulary is the design's.
        builder.Property(x => x.DependencyKind)
            .HasColumnName("dependency_kind")
            .HasMaxLength(20)
            .IsRequired()
            .HasConversion(
                value => value.ToString().ToLowerInvariant(),
                value => (JobDependencyKind)Enum.Parse(typeof(JobDependencyKind), value, true));

        builder.Property(x => x.IsRequired)
            .HasColumnName("is_required")
            .IsRequired();

        builder.Property(x => x.DependsOnVersion)
            .HasColumnName("depends_on_version");

        builder.Property(x => x.StalenessToleranceMinutes)
            .HasColumnName("staleness_tolerance_minutes");

        builder.Property(x => x.AllowStaleReuse)
            .HasColumnName("allow_stale_reuse")
            .HasDefaultValue(false)
            .IsRequired();

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.HasIndex(x => x.JobDefinitionId);
        builder.HasIndex(x => x.DependsOnJobDefinitionId);

        builder.HasIndex(x => new { x.JobDefinitionId, x.DependsOnJobDefinitionId })
            .IsUnique();
    }
}