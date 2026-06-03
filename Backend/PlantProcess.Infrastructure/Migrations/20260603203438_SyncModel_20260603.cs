using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class SyncModel_20260603 : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("ALTER TABLE genealogy_edges ADD COLUMN IF NOT EXISTS contribution_weight numeric(9,6) NOT NULL DEFAULT 1.0;");

            migrationBuilder.Sql("ALTER TABLE genealogy_edges ADD COLUMN IF NOT EXISTS is_transition boolean NOT NULL DEFAULT FALSE;");

            migrationBuilder.Sql("ALTER TABLE genealogy_edges ADD COLUMN IF NOT EXISTS provenance_confidence numeric(9,6) NOT NULL DEFAULT 1.0;");

            migrationBuilder.Sql("CREATE INDEX IF NOT EXISTS ix_genealogy_edges_child_material_unit_id_is_transition_contri ON genealogy_edges (child_material_unit_id, is_transition, contribution_weight);");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("DROP INDEX IF EXISTS ix_genealogy_edges_child_material_unit_id_is_transition_contri;");

            migrationBuilder.Sql("ALTER TABLE genealogy_edges DROP COLUMN IF EXISTS contribution_weight;");

            migrationBuilder.Sql("ALTER TABLE genealogy_edges DROP COLUMN IF EXISTS is_transition;");

            migrationBuilder.Sql("ALTER TABLE genealogy_edges DROP COLUMN IF EXISTS provenance_confidence;");
        }
    }
}
