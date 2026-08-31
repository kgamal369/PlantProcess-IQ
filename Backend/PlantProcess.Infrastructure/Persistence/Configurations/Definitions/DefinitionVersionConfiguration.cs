using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Definitions;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Definitions;

/// <summary>
/// PPIQ T-090. Maps DefinitionVersion onto the canonical store.
///
/// The table moved from ppiq_meta.ppiq_definition_versions to
/// ppiq_meta.definition_versions. Script 831 owns the DDL, so this stays
/// excluded from migrations - EF describing the table would be a second schema
/// authority for one set of columns.
///
/// Every property is configured with no value generation and the entity has no
/// public mutators, so EF can materialise a version but cannot originate one.
/// Creation belongs to CanonicalDefinitionWriter, under the parent row lock and
/// inside the caller's transaction.
/// </summary>
public sealed class DefinitionVersionConfiguration : IEntityTypeConfiguration<DefinitionVersion>
{
    public void Configure(EntityTypeBuilder<DefinitionVersion> builder)
    {
        builder.ToTable("definition_versions", "ppiq_meta", table => table.ExcludeFromMigrations());

        builder.HasKey(x => x.Id).HasName("pk_definition_versions");

        builder.Property(x => x.Id).HasColumnName("id");
        builder.Property(x => x.TenantId).HasColumnName("tenant_id").IsRequired();
        builder.Property(x => x.DefinitionId).HasColumnName("definition_id").IsRequired();
        builder.Property(x => x.VersionNumber).HasColumnName("version_number").IsRequired();
        builder.Property(x => x.Status).HasColumnName("status").HasMaxLength(20).IsRequired();
        builder.Property(x => x.Mode).HasColumnName("mode").HasMaxLength(6).IsRequired();
        builder.Property(x => x.GraphJson).HasColumnName("graph_json").HasColumnType("jsonb");
        builder.Property(x => x.DefinitionHash).HasColumnName("definition_hash").HasMaxLength(64).IsRequired();
        builder.Property(x => x.CreatedAtUtc).HasColumnName("created_at_utc").IsRequired();
        builder.Property(x => x.CreatedBy).HasColumnName("created_by");
        builder.Property(x => x.PublishedAtUtc).HasColumnName("published_at_utc");
        builder.Property(x => x.IsDeleted).HasColumnName("is_deleted").IsRequired();

        builder.Ignore(x => x.IsPublished);

        builder.HasIndex(x => new { x.DefinitionId, x.VersionNumber })
            .IsUnique()
            .HasDatabaseName("uq_definition_versions_number");
    }
}
