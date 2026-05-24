// ============================================================
// File: Backend/PlantProcess.Infrastructure/Persistence/Configurations/Configuration/Audit/AuditLogEntryConfiguration.cs
// Task: BE-ADD-001 — AuditLogEntry full implementation
//
// Purpose:
//   Maps AuditLogEntry to audit_log_entries using snake_case column names.
//
// Important fix:
//   AuditLogEntry inherits BaseEntity. Without explicit mapping for the
//   inherited BaseEntity properties, EF Core tries to write columns like
//   "CreatedAtUtc", which do not exist in the PostgreSQL snake_case table.
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

        // ============================================================
        // BaseEntity mapping
        // ============================================================

        b.Property(x => x.Id)
            .HasColumnName("id")
            .IsRequired();

        b.Property(x => x.CreatedAtUtc)
            .HasColumnName("created_at_utc")
            .HasColumnType("timestamp with time zone")
            .IsRequired();

        b.Property(x => x.UpdatedAtUtc)
            .HasColumnName("updated_at_utc")
            .HasColumnType("timestamp with time zone");

        b.Property(x => x.IsSynthetic)
            .HasColumnName("is_synthetic")
            .IsRequired();

        b.Property(x => x.SourceSystem)
            .HasColumnName("source_system")
            .HasMaxLength(100);

        b.Property(x => x.SourceRecordId)
            .HasColumnName("source_record_id")
            .HasMaxLength(200);

        b.Property(x => x.IsDeleted)
            .HasColumnName("is_deleted")
            .IsRequired();

        b.Property(x => x.DeletedAtUtc)
            .HasColumnName("deleted_at_utc")
            .HasColumnType("timestamp with time zone");

        b.Property(x => x.DeletedReason)
            .HasColumnName("deleted_reason")
            .HasMaxLength(500);

        // ============================================================
        // AuditLogEntry mapping
        // ============================================================

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
            .HasMaxLength(45);

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

        // ============================================================
        // Query filter
        // ============================================================

        b.HasQueryFilter(x => !x.IsDeleted);

        // ============================================================
        // Indexes
        // ============================================================

        b.HasIndex(x => x.OccurredAtUtc)
            .HasDatabaseName("ix_audit_log_occurred_at");

        b.HasIndex(x => new { x.UserId, x.OccurredAtUtc })
            .HasDatabaseName("ix_audit_log_user_occurred");

        b.HasIndex(x => new { x.ResourceType, x.ResourceId })
            .HasDatabaseName("ix_audit_log_resource");

        b.HasIndex(x => x.CorrelationId)
            .HasDatabaseName("ix_audit_log_correlation");

        b.HasIndex(x => x.CreatedAtUtc)
            .HasDatabaseName("ix_audit_log_created_at");

        b.HasIndex(x => x.IsDeleted)
            .HasDatabaseName("ix_audit_log_is_deleted");
    }
}