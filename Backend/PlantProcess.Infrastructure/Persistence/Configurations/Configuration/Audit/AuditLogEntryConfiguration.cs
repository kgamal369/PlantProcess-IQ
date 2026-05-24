// ============================================================
// File:    Backend/PlantProcess.Infrastructure/Persistence/Configurations/Audit/AuditLogEntryConfiguration.cs
// Task:    BE-ADD-001 (AuditLogEntry full implementation)
// ============================================================

using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Audit;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Audit;

public sealed class AuditLogEntryConfiguration : IEntityTypeConfiguration<AuditLogEntry>
{
    public void Configure(EntityTypeBuilder<AuditLogEntry> b)
    {
        b.ToTable("audit_log_entries");
        b.HasKey(x => x.Id);

        b.Property(x => x.Id).HasColumnName("id");

        b.Property(x => x.OccurredAtUtc)
            .HasColumnName("occurred_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.Property(x => x.HttpMethod)
            .HasColumnName("http_method")
            .HasMaxLength(10)
            .IsRequired();

        b.Property(x => x.Endpoint)
            .HasColumnName("endpoint")
            .HasMaxLength(500)
            .IsRequired();

        b.Property(x => x.ActionCategory)
            .HasColumnName("action_category")
            .HasMaxLength(50)
            .IsRequired();

        b.Property(x => x.OutcomeStatus)
            .HasColumnName("outcome_status")
            .HasMaxLength(50)
            .IsRequired();

        b.Property(x => x.UserId)
            .HasColumnName("user_id")
            .HasMaxLength(100);

        b.Property(x => x.UserName)
            .HasColumnName("user_name")
            .HasMaxLength(200);

        b.Property(x => x.ResourceType)
            .HasColumnName("resource_type")
            .HasMaxLength(100);

        b.Property(x => x.ResourceId)
            .HasColumnName("resource_id")
            .HasMaxLength(100);

        b.Property(x => x.ClientIp)
            .HasColumnName("client_ip")
            .HasMaxLength(45);   // IPv6 max length

        b.Property(x => x.UserAgent)
            .HasColumnName("user_agent")
            .HasMaxLength(500);

        b.Property(x => x.CorrelationId)
            .HasColumnName("correlation_id")
            .HasMaxLength(100);

        b.Property(x => x.HttpStatusCode)
            .HasColumnName("http_status_code");

        b.Property(x => x.MetadataJson)
            .HasColumnName("metadata_json")
            .HasColumnType("jsonb");

        // BaseEntity fields are managed by the base configuration
        // applied via the global UTC converter + soft-delete query filter
        // already in place across the codebase.

        // Indexes for the most common audit-log query patterns
        b.HasIndex(x => x.OccurredAtUtc)
            .HasDatabaseName("ix_audit_log_occurred_at");

        b.HasIndex(x => new { x.UserId, x.OccurredAtUtc })
            .HasDatabaseName("ix_audit_log_user_occurred");

        b.HasIndex(x => new { x.ResourceType, x.ResourceId })
            .HasDatabaseName("ix_audit_log_resource");

        b.HasIndex(x => x.CorrelationId)
            .HasDatabaseName("ix_audit_log_correlation");
    }
}
