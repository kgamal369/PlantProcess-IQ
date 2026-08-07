using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using PlantProcess.Domain.Entities.Definitions;

namespace PlantProcess.Infrastructure.Persistence.Configurations.Definitions;

/// <summary>
/// PPIQ T-039. Maps the snapshot entity onto the table 770 creates. The DDL is
/// owned by the numbered replay chain, not by EF - this file describes what is
/// already there rather than asking EF to create it.
/// </summary>
public class DefinitionVersionConfiguration : IEntityTypeConfiguration<DefinitionVersion>
{
    public void Configure(EntityTypeBuilder<DefinitionVersion> builder)
    {
        // T-039 CORRECTION. The numbered replay chain owns this table's DDL -
        // script 770 creates it - so EF must MAP it without believing it should
        // create it. Without this, adding the entity leaves the model with
        // "pending changes" and Migrate() refuses to start the API at all.
        // ExcludeFromMigrations is the EF feature for a table managed outside
        // migrations; it does not affect querying or saving.
        builder.ToTable("ppiq_definition_versions", t => t.ExcludeFromMigrations());

        builder.HasKey(x => x.Id);

        builder.Property(x => x.DefinitionKind).IsRequired().HasColumnName("definition_kind").HasMaxLength(64);
        builder.Property(x => x.DefinitionId).IsRequired().HasColumnName("definition_id");
        builder.Property(x => x.VersionNumber).IsRequired().HasColumnName("version_number");
        builder.Property(x => x.PayloadJson).IsRequired().HasColumnName("payload_json").HasColumnType("jsonb");
        builder.Property(x => x.CreatedAtUtc).IsRequired().HasColumnName("created_at_utc");
        builder.Property(x => x.CreatedBy).HasColumnName("created_by").HasMaxLength(200);
        builder.Property(x => x.IsPublished).IsRequired().HasColumnName("is_published");
        builder.Property(x => x.Id).HasColumnName("id");

        builder
            .HasIndex(x => new { x.DefinitionKind, x.DefinitionId, x.VersionNumber })
            .IsUnique()
            .HasDatabaseName("ux_ppiq_definition_versions_kind_id_version");
    }
}