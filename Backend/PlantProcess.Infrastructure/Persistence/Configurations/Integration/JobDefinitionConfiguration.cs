using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Integration;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PlantProcess.Domain.Enums.Integration;

using PlantProcess.Infrastructure.Persistence.Configurations.Common;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Integration;

public sealed class JobDefinitionConfiguration : IEntityTypeConfiguration<JobDefinition>
{
    // T-064. Chapter 3 4.5.5a names the stored vocabulary: current_published and
    // pinned. The converter is declared over the NON-nullable enum on purpose, so
    // EF handles null itself. A converter written over the nullable type would be
    // handed null and would answer current_published for a job that has no target
    // at all - a policy written beside an absent identity, which the coherence
    // constraint in script 824 rejects and which nobody would see until it did.
    private static readonly ValueConverter<JobTargetVersionPolicy, string> VersionPolicyConverter =
        new(
            value => value == JobTargetVersionPolicy.Pinned ? "pinned" : "current_published",
            value => value == "pinned"
                ? JobTargetVersionPolicy.Pinned
                : JobTargetVersionPolicy.CurrentPublished);

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

        // T-064. The governed target. No foreign key: Chapter 3 4.5.5a points it
        // at definition_store(id). T-089/T-090 establish that authority and T-106
        // owns the physical convergence. The DDL, the closed policy
        // vocabulary and the coherence constraints live in script 824.
        builder.Property(x => x.TargetDefinitionId)
            .HasColumnName("target_definition_id");

        builder.Property(x => x.TargetDefinitionKind)
            .HasColumnName("target_definition_kind")
            .HasMaxLength(64);

        builder.Property(x => x.TargetVersionPolicy)
            .HasColumnName("target_version_policy")
            .HasMaxLength(20)
            .HasConversion(VersionPolicyConverter);

        builder.Property(x => x.TargetDefinitionVersion)
            .HasColumnName("target_definition_version");

        // jsonb, not text. The payload is validated by the database as well as by
        // the entity, so a row written by anything other than this application
        // still cannot carry malformed parameters.
        builder.Property(x => x.TargetParametersJson)
            .HasColumnName("target_parameters")
            .HasColumnType("jsonb");

        builder.HasIndex(x => new { x.TargetDefinitionKind, x.TargetDefinitionId });

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