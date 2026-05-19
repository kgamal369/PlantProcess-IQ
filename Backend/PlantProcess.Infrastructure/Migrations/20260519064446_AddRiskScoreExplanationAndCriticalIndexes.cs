using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddRiskScoreExplanationAndCriticalIndexes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<int>(
                name: "import_interval_minutes",
                table: "connection_profiles",
                type: "integer",
                nullable: false,
                defaultValue: 15);

            migrationBuilder.AddColumn<string>(
                name: "import_schedule_expression",
                table: "connection_profiles",
                type: "character varying(250)",
                maxLength: 250,
                nullable: false,
                defaultValue: "Every 15 minutes");

            migrationBuilder.CreateTable(
                name: "job_run_histories",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    job_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    job_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    trigger_source = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    triggered_by = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    failure_reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    run_message = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    result_summary_json = table.Column<string>(type: "jsonb", nullable: true),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_synthetic = table.Column<bool>(type: "boolean", nullable: false),
                    source_system = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source_record_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_job_run_histories", x => x.id);
                    table.ForeignKey(
                        name: "fk_job_run_histories_job_definitions_job_definition_id",
                        column: x => x.job_definition_id,
                        principalTable: "job_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateIndex(
                name: "ix_connection_profiles_import_interval_minutes",
                table: "connection_profiles",
                column: "import_interval_minutes");

            migrationBuilder.CreateIndex(
                name: "ix_job_run_histories_job_code",
                table: "job_run_histories",
                column: "job_code");

            migrationBuilder.CreateIndex(
                name: "ix_job_run_histories_job_definition_id",
                table: "job_run_histories",
                column: "job_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_job_run_histories_job_definition_id_started_at_utc",
                table: "job_run_histories",
                columns: new[] { "job_definition_id", "started_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_job_run_histories_started_at_utc",
                table: "job_run_histories",
                column: "started_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_job_run_histories_status",
                table: "job_run_histories",
                column: "status");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_run_histories");

            migrationBuilder.DropIndex(
                name: "ix_connection_profiles_import_interval_minutes",
                table: "connection_profiles");

            migrationBuilder.DropColumn(
                name: "import_interval_minutes",
                table: "connection_profiles");

            migrationBuilder.DropColumn(
                name: "import_schedule_expression",
                table: "connection_profiles");
        }
    }
}
