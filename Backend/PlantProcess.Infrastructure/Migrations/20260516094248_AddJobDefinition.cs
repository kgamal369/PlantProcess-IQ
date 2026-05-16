using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddJobDefinition : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "job_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    job_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    job_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    job_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    target_id = table.Column<Guid>(type: "uuid", nullable: true),
                    target_type = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: true),
                    schedule_expression = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    is_enabled = table.Column<bool>(type: "boolean", nullable: false),
                    last_run_started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_run_completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_run_duration_ms = table.Column<long>(type: "bigint", nullable: true),
                    last_run_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    last_failure_reason = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    next_run_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_job_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "schema_view_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_view_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    schema_view_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    view_kind = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    primary_source_dataset_definition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    sql_text = table.Column<string>(type: "text", nullable: false),
                    source_dataset_ids_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    output_schema_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "[]"),
                    max_preview_rows = table.Column<int>(type: "integer", nullable: false),
                    timeout_seconds = table.Column<int>(type: "integer", nullable: false),
                    is_approved = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    last_validated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_validation_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    last_validation_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_schema_view_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_schema_view_definitions_source_dataset_definitions_primary_",
                        column: x => x.primary_source_dataset_definition_id,
                        principalTable: "source_dataset_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "kpi_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    schema_view_definition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    kpi_code = table.Column<string>(type: "character varying(120)", maxLength: 120, nullable: false),
                    kpi_name = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false),
                    kpi_category = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    value_expression = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    unit = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: true),
                    dimension_expression = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    filter_expression = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    aggregation_type = table.Column<string>(type: "character varying(80)", maxLength: 80, nullable: false),
                    kpi_options_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
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
                    table.PrimaryKey("pk_kpi_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_kpi_definitions_schema_view_definitions_schema_view_definit",
                        column: x => x.schema_view_definition_id,
                        principalTable: "schema_view_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_job_definitions_is_enabled",
                table: "job_definitions",
                column: "is_enabled");

            migrationBuilder.CreateIndex(
                name: "ix_job_definitions_job_code",
                table: "job_definitions",
                column: "job_code",
                unique: true,
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "ix_job_definitions_job_type",
                table: "job_definitions",
                column: "job_type");

            migrationBuilder.CreateIndex(
                name: "ix_job_definitions_last_run_status",
                table: "job_definitions",
                column: "last_run_status");

            migrationBuilder.CreateIndex(
                name: "ix_job_definitions_target_id",
                table: "job_definitions",
                column: "target_id");

            migrationBuilder.CreateIndex(
                name: "ix_kpi_definitions_is_active",
                table: "kpi_definitions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_kpi_definitions_kpi_category",
                table: "kpi_definitions",
                column: "kpi_category");

            migrationBuilder.CreateIndex(
                name: "ix_kpi_definitions_kpi_code",
                table: "kpi_definitions",
                column: "kpi_code",
                unique: true,
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "ix_kpi_definitions_schema_view_definition_id",
                table: "kpi_definitions",
                column: "schema_view_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_schema_view_definitions_is_active",
                table: "schema_view_definitions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_schema_view_definitions_is_approved",
                table: "schema_view_definitions",
                column: "is_approved");

            migrationBuilder.CreateIndex(
                name: "ix_schema_view_definitions_last_validation_status",
                table: "schema_view_definitions",
                column: "last_validation_status");

            migrationBuilder.CreateIndex(
                name: "ix_schema_view_definitions_primary_source_dataset_definition_id",
                table: "schema_view_definitions",
                column: "primary_source_dataset_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_schema_view_definitions_schema_view_code",
                table: "schema_view_definitions",
                column: "schema_view_code",
                unique: true,
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "ix_schema_view_definitions_view_kind",
                table: "schema_view_definitions",
                column: "view_kind");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "job_definitions");

            migrationBuilder.DropTable(
                name: "kpi_definitions");

            migrationBuilder.DropTable(
                name: "schema_view_definitions");
        }
    }
}
