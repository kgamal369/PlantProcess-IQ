using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Integration;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;
using PlantProcess.Domain.Enums.Integration;

using PlantProcess.Infrastructure.Persistence.Configurations.Common;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Integration;

public sealed class JobRunHistoryConfiguration : IEntityTypeConfiguration<JobRunHistory>
{
    // T-064. Same stored vocabulary as job_definitions, same reason for declaring
    // the converter over the non-nullable enum: a run with no target must record
    // no policy rather than a plausible one.
    private static readonly ValueConverter<JobTargetVersionPolicy, string> VersionPolicyConverter =
        new(
            value => value == JobTargetVersionPolicy.Pinned ? "pinned" : "current_published",
            value => value == "pinned"
                ? JobTargetVersionPolicy.Pinned
                : JobTargetVersionPolicy.CurrentPublished);

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

        // T-064. What the run actually executed. Script 824 owns the DDL.
        builder.Property(x => x.TargetDefinitionId)
            .HasColumnName("target_definition_id");

        builder.Property(x => x.TargetDefinitionKind)
            .HasColumnName("target_definition_kind")
            .HasMaxLength(64);

        builder.Property(x => x.TargetDefinitionVersion)
            .HasColumnName("target_definition_version");

        // jsonb, not text. The payload is validated by the database as well as by
        // the entity, so a row written by anything other than this application
        // still cannot carry malformed parameters.
        builder.Property(x => x.TargetParametersJson)
            .HasColumnName("target_parameters")
            .HasColumnType("jsonb");

        builder.Property(x => x.TargetVersionPolicy)
            .HasColumnName("target_version_policy")
            .HasMaxLength(20)
            .HasConversion(VersionPolicyConverter);

        // SQL 843/844 are the DDL authority for these already-shipped job fields.
        // Pin only their relational store facets here so EF metadata cannot drift from
        // the committed canonical SQL contract. No job behavior is changed.
        builder.Property(x => x.NominalAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.OccurrenceKey).HasColumnType("character varying(80)");
        builder.Property(x => x.CancellationRequestedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.CancellationRequestedBy).HasColumnType("character varying(200)");
        builder.Property(x => x.CancellationReason).HasColumnType("character varying(500)");
        builder.Property(x => x.CancellationAcknowledgedAtUtc).HasColumnType("timestamp with time zone");

        builder.HasIndex(x => x.JobDefinitionId);
        builder.HasIndex(x => x.JobCode);
        builder.HasIndex(x => x.Status);
        builder.HasIndex(x => x.StartedAtUtc);
        builder.HasIndex(x => new { x.JobDefinitionId, x.StartedAtUtc });

        builder.UsePostgresXminConcurrencyToken();
    }
}