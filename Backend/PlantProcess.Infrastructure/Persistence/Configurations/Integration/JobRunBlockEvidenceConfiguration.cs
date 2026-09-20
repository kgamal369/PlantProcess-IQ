using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Integration;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Integration;

/// <summary>
/// Per-block runtime evidence. ToTable carries the name only; placement comes from
/// StorageTopologyMap. EXCLUDEFROMMIGRATIONS keeps script 846 the single DDL authority,
/// the same convention script 840 uses, so the canonical build's EF tier does not refuse
/// with a pending model change. The run foreign key lives in 846 and is deliberately not
/// modelled here, so no navigation can make this a second run history.
/// </summary>
public sealed class JobRunBlockEvidenceConfiguration : IEntityTypeConfiguration<JobRunBlockEvidence>
{
    public void Configure(EntityTypeBuilder<JobRunBlockEvidence> builder)
    {
        builder.ToTable("job_run_block_evidence", table => table.ExcludeFromMigrations());

        builder.HasKey(x => x.Id);

        builder.Property(x => x.JobRunHistoryId).IsRequired();

        builder.Property(x => x.BlockId)
            .IsRequired()
            .HasMaxLength(200);

        builder.Property(x => x.ExecutionOrdinal).IsRequired();

        builder.Property(x => x.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(x => x.StartedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.FinishedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.InputRows);
        builder.Property(x => x.OutputRows);
        builder.Property(x => x.DiagnosticCode).HasMaxLength(80);
        builder.Property(x => x.DiagnosticDetail);

        builder.Property(x => x.CreatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.UpdatedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.DeletedAtUtc).HasColumnType("timestamp with time zone");
        builder.Property(x => x.SourceSystem).HasMaxLength(100);
        builder.Property(x => x.SourceRecordId).HasMaxLength(100);
        builder.Property(x => x.DeletedReason).HasMaxLength(500);

        builder.HasIndex(x => new { x.JobRunHistoryId, x.BlockId }).IsUnique();
        builder.HasIndex(x => new { x.JobRunHistoryId, x.ExecutionOrdinal }).IsUnique();
    }
}
