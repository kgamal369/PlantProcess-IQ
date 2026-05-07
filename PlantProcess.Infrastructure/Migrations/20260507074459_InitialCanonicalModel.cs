using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialCanonicalModel : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "defect_catalogs",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    defect_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    defect_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    defect_category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    industry_template = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("pk_defect_catalogs", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "material_units",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    material_unit_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    product_family = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    grade_or_recipe = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    production_start_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    production_end_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    production_start_local = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    production_end_local = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    plant_time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    plant_utc_offset_minutes = table.Column<int>(type: "integer", nullable: false),
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
                    table.PrimaryKey("pk_material_units", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "parameter_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parameter_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parameter_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    value_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    unit_of_measure = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    parameter_category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    industry_template = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    expected_min_value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    expected_max_value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
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
                    table.PrimaryKey("pk_parameter_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "data_quality_issues",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    issue_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: false),
                    affected_entity_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    affected_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
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
                    table.PrimaryKey("pk_data_quality_issues", x => x.id);
                    table.ForeignKey(
                        name: "fk_data_quality_issues_material_units_material_unit_id",
                        column: x => x.material_unit_id,
                        principalTable: "material_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "genealogy_edges",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_material_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    child_material_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    relationship_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    effective_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
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
                    table.PrimaryKey("pk_genealogy_edges", x => x.id);
                    table.ForeignKey(
                        name: "fk_genealogy_edges_material_units_child_material_unit_id",
                        column: x => x.child_material_unit_id,
                        principalTable: "material_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_genealogy_edges_material_units_parent_material_unit_id",
                        column: x => x.parent_material_unit_id,
                        principalTable: "material_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "material_aliases",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    alias_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    alias_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    xmin = table.Column<uint>(type: "xid", rowVersion: true, nullable: false),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_synthetic = table.Column<bool>(type: "boolean", nullable: false),
                    source_system = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_record_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_material_aliases", x => x.id);
                    table.ForeignKey(
                        name: "fk_material_aliases_material_units_material_unit_id",
                        column: x => x.material_unit_id,
                        principalTable: "material_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "process_step_executions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operation_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    operation_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    line_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    crew_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ended_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    started_at_local = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ended_at_local = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    plant_time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    plant_utc_offset_minutes = table.Column<int>(type: "integer", nullable: false),
                    execution_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("pk_process_step_executions", x => x.id);
                    table.ForeignKey(
                        name: "fk_process_step_executions_material_units_material_unit_id",
                        column: x => x.material_unit_id,
                        principalTable: "material_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "quality_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    defect_catalog_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    event_at_local = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    plant_time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    plant_utc_offset_minutes = table.Column<int>(type: "integer", nullable: false),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    severity = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    decision = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("pk_quality_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_quality_events_defect_catalogs_defect_catalog_id",
                        column: x => x.defect_catalog_id,
                        principalTable: "defect_catalogs",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_quality_events_material_units_material_unit_id",
                        column: x => x.material_unit_id,
                        principalTable: "material_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "risk_scores",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    risk_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    score = table.Column<decimal>(type: "numeric(8,6)", precision: 8, scale: 6, nullable: false),
                    risk_class = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    main_contributors_json = table.Column<string>(type: "jsonb", nullable: true),
                    scored_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    scored_at_local = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    plant_time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    plant_utc_offset_minutes = table.Column<int>(type: "integer", nullable: false),
                    model_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("pk_risk_scores", x => x.id);
                    table.ForeignKey(
                        name: "fk_risk_scores_material_units_material_unit_id",
                        column: x => x.material_unit_id,
                        principalTable: "material_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "downtime_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    process_step_execution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    ended_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    started_at_local = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    ended_at_local = table.Column<DateTime>(type: "timestamp without time zone", nullable: true),
                    plant_time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    plant_utc_offset_minutes = table.Column<int>(type: "integer", nullable: false),
                    downtime_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    reason_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.PrimaryKey("pk_downtime_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_downtime_events_material_units_material_unit_id",
                        column: x => x.material_unit_id,
                        principalTable: "material_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_downtime_events_process_step_executions_process_step_execut",
                        column: x => x.process_step_execution_id,
                        principalTable: "process_step_executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "parameter_observations",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_unit_id = table.Column<Guid>(type: "uuid", nullable: false),
                    process_step_execution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parameter_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    observed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    observed_at_local = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    plant_time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    plant_utc_offset_minutes = table.Column<int>(type: "integer", nullable: false),
                    numeric_value = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    text_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    boolean_value = table.Column<bool>(type: "boolean", nullable: true),
                    unit_of_measure = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    quality_flag = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    raw_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
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
                    table.PrimaryKey("pk_parameter_observations", x => x.id);
                    table.ForeignKey(
                        name: "fk_parameter_observations_material_units_material_unit_id",
                        column: x => x.material_unit_id,
                        principalTable: "material_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_parameter_observations_parameter_definitions_parameter_defi",
                        column: x => x.parameter_definition_id,
                        principalTable: "parameter_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_parameter_observations_process_step_executions_process_step",
                        column: x => x.process_step_execution_id,
                        principalTable: "process_step_executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateTable(
                name: "process_events",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_unit_id = table.Column<Guid>(type: "uuid", nullable: true),
                    process_step_execution_id = table.Column<Guid>(type: "uuid", nullable: true),
                    equipment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    event_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    event_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    event_at_local = table.Column<DateTime>(type: "timestamp without time zone", nullable: false),
                    plant_time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    plant_utc_offset_minutes = table.Column<int>(type: "integer", nullable: false),
                    event_value = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("pk_process_events", x => x.id);
                    table.ForeignKey(
                        name: "fk_process_events_material_units_material_unit_id",
                        column: x => x.material_unit_id,
                        principalTable: "material_units",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_process_events_process_step_executions_process_step_executi",
                        column: x => x.process_step_execution_id,
                        principalTable: "process_step_executions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                });

            migrationBuilder.CreateIndex(
                name: "ix_data_quality_issues_affected_entity_id",
                table: "data_quality_issues",
                column: "affected_entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_data_quality_issues_issue_type",
                table: "data_quality_issues",
                column: "issue_type");

            migrationBuilder.CreateIndex(
                name: "ix_data_quality_issues_material_unit_id",
                table: "data_quality_issues",
                column: "material_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_data_quality_issues_severity",
                table: "data_quality_issues",
                column: "severity");

            migrationBuilder.CreateIndex(
                name: "ix_defect_catalogs_defect_category",
                table: "defect_catalogs",
                column: "defect_category");

            migrationBuilder.CreateIndex(
                name: "ix_defect_catalogs_defect_code",
                table: "defect_catalogs",
                column: "defect_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_defect_catalogs_industry_template",
                table: "defect_catalogs",
                column: "industry_template");

            migrationBuilder.CreateIndex(
                name: "ix_downtime_events_downtime_type",
                table: "downtime_events",
                column: "downtime_type");

            migrationBuilder.CreateIndex(
                name: "ix_downtime_events_equipment_id",
                table: "downtime_events",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_downtime_events_material_unit_id",
                table: "downtime_events",
                column: "material_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_downtime_events_process_step_execution_id",
                table: "downtime_events",
                column: "process_step_execution_id");

            migrationBuilder.CreateIndex(
                name: "ix_downtime_events_started_at_local",
                table: "downtime_events",
                column: "started_at_local");

            migrationBuilder.CreateIndex(
                name: "ix_downtime_events_started_at_utc",
                table: "downtime_events",
                column: "started_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_genealogy_edges_child_material_unit_id",
                table: "genealogy_edges",
                column: "child_material_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_genealogy_edges_parent_material_unit_id",
                table: "genealogy_edges",
                column: "parent_material_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_genealogy_edges_parent_material_unit_id_child_material_unit",
                table: "genealogy_edges",
                columns: new[] { "parent_material_unit_id", "child_material_unit_id" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_material_aliases_alias_code",
                table: "material_aliases",
                column: "alias_code");

            migrationBuilder.CreateIndex(
                name: "ix_material_aliases_material_unit_id",
                table: "material_aliases",
                column: "material_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_aliases_material_unit_id_alias_code_source_system",
                table: "material_aliases",
                columns: new[] { "material_unit_id", "alias_code", "source_system" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_material_aliases_source_system",
                table: "material_aliases",
                column: "source_system");

            migrationBuilder.CreateIndex(
                name: "ix_material_units_material_code",
                table: "material_units",
                column: "material_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_material_units_material_unit_type",
                table: "material_units",
                column: "material_unit_type");

            migrationBuilder.CreateIndex(
                name: "ix_material_units_material_unit_type_grade_or_recipe",
                table: "material_units",
                columns: new[] { "material_unit_type", "grade_or_recipe" });

            migrationBuilder.CreateIndex(
                name: "ix_material_units_site_id",
                table: "material_units",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_units_site_id_material_unit_type",
                table: "material_units",
                columns: new[] { "site_id", "material_unit_type" });

            migrationBuilder.CreateIndex(
                name: "ix_parameter_definitions_industry_template",
                table: "parameter_definitions",
                column: "industry_template");

            migrationBuilder.CreateIndex(
                name: "ix_parameter_definitions_parameter_category",
                table: "parameter_definitions",
                column: "parameter_category");

            migrationBuilder.CreateIndex(
                name: "ix_parameter_definitions_parameter_code",
                table: "parameter_definitions",
                column: "parameter_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_parameter_observations_equipment_id",
                table: "parameter_observations",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_parameter_observations_material_unit_id",
                table: "parameter_observations",
                column: "material_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_parameter_observations_material_unit_id_parameter_definitio",
                table: "parameter_observations",
                columns: new[] { "material_unit_id", "parameter_definition_id", "observed_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_parameter_observations_observed_at_local",
                table: "parameter_observations",
                column: "observed_at_local");

            migrationBuilder.CreateIndex(
                name: "ix_parameter_observations_observed_at_utc",
                table: "parameter_observations",
                column: "observed_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_parameter_observations_parameter_definition_id",
                table: "parameter_observations",
                column: "parameter_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_parameter_observations_process_step_execution_id",
                table: "parameter_observations",
                column: "process_step_execution_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_events_equipment_id",
                table: "process_events",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_events_event_at_utc",
                table: "process_events",
                column: "event_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_process_events_event_type",
                table: "process_events",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_process_events_event_type_event_at_utc",
                table: "process_events",
                columns: new[] { "event_type", "event_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_process_events_material_unit_id",
                table: "process_events",
                column: "material_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_events_process_step_execution_id",
                table: "process_events",
                column: "process_step_execution_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_step_executions_crew_code_started_at_local",
                table: "process_step_executions",
                columns: new[] { "crew_code", "started_at_local" });

            migrationBuilder.CreateIndex(
                name: "ix_process_step_executions_equipment_id",
                table: "process_step_executions",
                column: "equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_step_executions_material_unit_id",
                table: "process_step_executions",
                column: "material_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_process_step_executions_material_unit_id_operation_type_sta",
                table: "process_step_executions",
                columns: new[] { "material_unit_id", "operation_type", "started_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_process_step_executions_operation_type",
                table: "process_step_executions",
                column: "operation_type");

            migrationBuilder.CreateIndex(
                name: "ix_process_step_executions_operation_type_started_at_local",
                table: "process_step_executions",
                columns: new[] { "operation_type", "started_at_local" });

            migrationBuilder.CreateIndex(
                name: "ix_process_step_executions_started_at_local",
                table: "process_step_executions",
                column: "started_at_local");

            migrationBuilder.CreateIndex(
                name: "ix_process_step_executions_started_at_utc",
                table: "process_step_executions",
                column: "started_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_quality_events_defect_catalog_id",
                table: "quality_events",
                column: "defect_catalog_id");

            migrationBuilder.CreateIndex(
                name: "ix_quality_events_event_at_local",
                table: "quality_events",
                column: "event_at_local");

            migrationBuilder.CreateIndex(
                name: "ix_quality_events_event_at_utc",
                table: "quality_events",
                column: "event_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_quality_events_event_type",
                table: "quality_events",
                column: "event_type");

            migrationBuilder.CreateIndex(
                name: "ix_quality_events_material_unit_id",
                table: "quality_events",
                column: "material_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_quality_events_material_unit_id_event_type_event_at_utc",
                table: "quality_events",
                columns: new[] { "material_unit_id", "event_type", "event_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_risk_scores_material_unit_id",
                table: "risk_scores",
                column: "material_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_risk_scores_material_unit_id_risk_type_scored_at_utc",
                table: "risk_scores",
                columns: new[] { "material_unit_id", "risk_type", "scored_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_risk_scores_risk_type",
                table: "risk_scores",
                column: "risk_type");

            migrationBuilder.CreateIndex(
                name: "ix_risk_scores_scored_at_utc",
                table: "risk_scores",
                column: "scored_at_utc");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "data_quality_issues");

            migrationBuilder.DropTable(
                name: "downtime_events");

            migrationBuilder.DropTable(
                name: "genealogy_edges");

            migrationBuilder.DropTable(
                name: "material_aliases");

            migrationBuilder.DropTable(
                name: "parameter_observations");

            migrationBuilder.DropTable(
                name: "process_events");

            migrationBuilder.DropTable(
                name: "quality_events");

            migrationBuilder.DropTable(
                name: "risk_scores");

            migrationBuilder.DropTable(
                name: "parameter_definitions");

            migrationBuilder.DropTable(
                name: "process_step_executions");

            migrationBuilder.DropTable(
                name: "defect_catalogs");

            migrationBuilder.DropTable(
                name: "material_units");
        }
    }
}
