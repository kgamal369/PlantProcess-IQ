using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class TodayAddRiskScoreExplanationAndCriticalIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "explanation_json",
                table: "risk_scores",
                type: "jsonb",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_risk_scores_risk_class",
                table: "risk_scores",
                column: "risk_class");

            migrationBuilder.CreateIndex(
                name: "ix_risk_scores_risk_type_risk_class_score",
                table: "risk_scores",
                columns: new[] { "risk_type", "risk_class", "score" });

            migrationBuilder.CreateIndex(
                name: "ix_risk_scores_score",
                table: "risk_scores",
                column: "score");

            // -----------------------------------------------------------------
            // P1-06 critical dashboard / investigation / risk performance indexes
            // Use raw SQL with IF NOT EXISTS because some of these may already
            // exist if you previously ran database/scripts/050_dashboard_phase8_9_10_indexes.sql.
            // -----------------------------------------------------------------

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_material_units_site_time_not_deleted
                ON material_units (site_id, production_start_utc, production_end_utc)
                WHERE is_deleted = FALSE;
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_quality_events_material_time_not_deleted
                ON quality_events (material_unit_id, event_at_utc)
                WHERE is_deleted = FALSE;
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_parameter_observations_observed_at_not_deleted
                ON parameter_observations (observed_at_utc)
                WHERE is_deleted = FALSE;
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_parameter_observations_material_time_not_deleted
                ON parameter_observations (material_unit_id, observed_at_utc)
                WHERE is_deleted = FALSE;
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_process_steps_material_time_not_deleted
                ON process_step_executions (material_unit_id, started_at_utc, ended_at_utc)
                WHERE is_deleted = FALSE;
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_process_steps_equipment_time_not_deleted
                ON process_step_executions (equipment_id, started_at_utc)
                WHERE is_deleted = FALSE;
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_genealogy_edges_parent_child_not_deleted
                ON genealogy_edges (parent_material_unit_id, child_material_unit_id)
                WHERE is_deleted = FALSE;
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_genealogy_edges_child_parent_not_deleted
                ON genealogy_edges (child_material_unit_id, parent_material_unit_id)
                WHERE is_deleted = FALSE;
                """);

            migrationBuilder.Sql("""
                CREATE INDEX IF NOT EXISTS ix_data_quality_issues_material_type_severity_not_deleted
                ON data_quality_issues (material_unit_id, issue_type, severity)
                WHERE is_deleted = FALSE;
                """);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_data_quality_issues_material_type_severity_not_deleted;
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_genealogy_edges_child_parent_not_deleted;
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_genealogy_edges_parent_child_not_deleted;
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_process_steps_equipment_time_not_deleted;
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_process_steps_material_time_not_deleted;
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_parameter_observations_material_time_not_deleted;
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_parameter_observations_observed_at_not_deleted;
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_quality_events_material_time_not_deleted;
                """);

            migrationBuilder.Sql("""
                DROP INDEX IF EXISTS ix_material_units_site_time_not_deleted;
                """);

            migrationBuilder.DropIndex(
                name: "ix_risk_scores_risk_class",
                table: "risk_scores");

            migrationBuilder.DropIndex(
                name: "ix_risk_scores_risk_type_risk_class_score",
                table: "risk_scores");

            migrationBuilder.DropIndex(
                name: "ix_risk_scores_score",
                table: "risk_scores");

            migrationBuilder.DropColumn(
                name: "explanation_json",
                table: "risk_scores");
        }
    }
}