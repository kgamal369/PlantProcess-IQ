// ============================================================================
// Persistence records for the customer assessment history.
//
// These are EF rows, not Domain semantic entities. They are named Record so
// that nobody mistakes them for one, they never cross the Application
// boundary, and they are never exposed by the API.
//
// They deliberately do not inherit BaseEntity: adding an inheritance
// relationship to satisfy a convention would change the Domain architecture
// rule, which T-213 does not own.
//
// ExcludeFromMigrations keeps 833_customer_assessment_history.sql the single
// DDL authority for both tables.
// ============================================================================

using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace PlantProcess.Infrastructure.CustomerAssessment
{
    public sealed class CustomerAssessmentRecord
    {
        public Guid AssessmentId { get; set; }
        public Guid TenantId { get; set; }
        public string LineageCode { get; set; } = string.Empty;
        public string? DisplayName { get; set; }
        public DateTimeOffset CreatedAtUtc { get; set; }
    }

    public sealed class CustomerAssessmentVersionRecord
    {
        public Guid AssessmentVersionId { get; set; }
        public Guid AssessmentId { get; set; }
        public int VersionNumber { get; set; }
        public string ContractVersion { get; set; } = string.Empty;
        public string RuleVersion { get; set; } = string.Empty;
        public string SemanticFingerprint { get; set; } = string.Empty;
        public string IntakeJson { get; set; } = string.Empty;
        public string ReportJson { get; set; } = string.Empty;
        public DateTimeOffset CreatedAtUtc { get; set; }
    }

    public sealed class CustomerAssessmentRecordConfiguration
        : IEntityTypeConfiguration<CustomerAssessmentRecord>
    {
        public void Configure(EntityTypeBuilder<CustomerAssessmentRecord> builder)
        {
            builder.ToTable(
                "customer_assessments",
                "ppiq_meta",
                table => table.ExcludeFromMigrations());

            builder.HasKey(x => x.AssessmentId);

            builder.Property(x => x.AssessmentId).HasColumnName("assessment_id");
            builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
            builder.Property(x => x.LineageCode)
                   .HasColumnName("lineage_code")
                   .HasMaxLength(128)
                   .IsRequired();
            builder.Property(x => x.DisplayName)
                   .HasColumnName("display_name")
                   .HasMaxLength(256);
            builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

            builder.HasIndex(x => new { x.TenantId, x.LineageCode })
                   .IsUnique()
                   .HasDatabaseName("ux_customer_assessments_tenant_lineage");
        }
    }

    public sealed class CustomerAssessmentVersionRecordConfiguration
        : IEntityTypeConfiguration<CustomerAssessmentVersionRecord>
    {
        public void Configure(EntityTypeBuilder<CustomerAssessmentVersionRecord> builder)
        {
            builder.ToTable(
                "customer_assessment_versions",
                "ppiq_meta",
                table => table.ExcludeFromMigrations());

            builder.HasKey(x => x.AssessmentVersionId);

            builder.Property(x => x.AssessmentVersionId).HasColumnName("assessment_version_id");
            builder.Property(x => x.AssessmentId).HasColumnName("assessment_id").IsRequired();
            builder.Property(x => x.VersionNumber).HasColumnName("version_number").IsRequired();
            builder.Property(x => x.ContractVersion)
                   .HasColumnName("contract_version")
                   .HasMaxLength(32)
                   .IsRequired();
            builder.Property(x => x.RuleVersion)
                   .HasColumnName("rule_version")
                   .HasMaxLength(32)
                   .IsRequired();
            builder.Property(x => x.SemanticFingerprint)
                   .HasColumnName("semantic_fingerprint")
                   .HasColumnType("char(64)")
                   .IsRequired();
            builder.Property(x => x.IntakeJson)
                   .HasColumnName("intake_json")
                   .HasColumnType("jsonb")
                   .IsRequired();
            builder.Property(x => x.ReportJson)
                   .HasColumnName("report_json")
                   .HasColumnType("jsonb")
                   .IsRequired();
            builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();

            builder.HasIndex(x => new { x.AssessmentId, x.VersionNumber })
                   .IsUnique()
                   .HasDatabaseName("ux_customer_assessment_versions_number");

            builder.HasIndex(x => new { x.AssessmentId, x.SemanticFingerprint })
                   .IsUnique()
                   .HasDatabaseName("ux_customer_assessment_versions_fingerprint");
        }
    }
}
