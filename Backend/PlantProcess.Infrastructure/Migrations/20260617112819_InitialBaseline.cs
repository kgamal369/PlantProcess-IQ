using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class InitialBaseline : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.CreateTable(
                name: "audit_log_entries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    occurred_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    http_method = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: false),
                    endpoint = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: false),
                    action_category = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    outcome_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    user_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    user_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    resource_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    resource_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    client_ip = table.Column<string>(type: "character varying(45)", maxLength: 45, nullable: true),
                    user_agent = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    correlation_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    http_status_code = table.Column<int>(type: "integer", nullable: true),
                    metadata_json = table.Column<string>(type: "jsonb", nullable: true),
                    created_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    updated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    is_synthetic = table.Column<bool>(type: "boolean", nullable: false),
                    source_system = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    source_record_id = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_reason = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log_entries", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "correlation_results",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    correlation_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    subject_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    outcome_code = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    score = table.Column<decimal>(type: "numeric(18,6)", precision: 18, scale: 6, nullable: true),
                    result_json = table.Column<string>(type: "jsonb", nullable: false),
                    calculated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("pk_correlation_results", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dashboard_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    user_id = table.Column<Guid>(type: "uuid", nullable: true),
                    dashboard_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    layout_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_default = table.Column<bool>(type: "boolean", nullable: false),
                    is_system_template = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_dashboard_definitions", x => x.id);
                });

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
                name: "industry_templates",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    template_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    template_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    industry_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_industry_templates", x => x.id);
                });

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
                name: "model_registries",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    model_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    model_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    model_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    model_version = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    risk_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    artifact_uri = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    training_data_summary_json = table.Column<string>(type: "jsonb", nullable: true),
                    metrics_json = table.Column<string>(type: "jsonb", nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    registered_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
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
                    table.PrimaryKey("pk_model_registries", x => x.id);
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
                name: "sites",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    site_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    company_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    country_code = table.Column<string>(type: "character varying(10)", maxLength: 10, nullable: true),
                    time_zone_id = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
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
                    table.PrimaryKey("pk_sites", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "source_system_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_system_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    source_system_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_system_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_read_only_source = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_source_system_definitions", x => x.id);
                });

            migrationBuilder.CreateTable(
                name: "dashboard_widget_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    dashboard_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    widget_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    widget_title = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    widget_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    chart_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    dimension_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    measure_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parameter_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    filter_json = table.Column<string>(type: "jsonb", nullable: false),
                    layout_json = table.Column<string>(type: "jsonb", nullable: false),
                    display_options_json = table.Column<string>(type: "jsonb", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    query_expression = table.Column<string>(type: "text", nullable: true),
                    advanced_expression_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    expression_version = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)1),
                    expression_enabled = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    expression_last_validated_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    expression_last_validation_status = table.Column<short>(type: "smallint", nullable: false, defaultValue: (short)0),
                    expression_last_validation_message = table.Column<string>(type: "text", nullable: true),
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
                    table.PrimaryKey("pk_dashboard_widget_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_dashboard_widget_definitions_dashboard_definitions_dashboar",
                        column: x => x.dashboard_definition_id,
                        principalTable: "dashboard_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "material_unit_type_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    industry_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    material_unit_type_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    material_unit_type_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_material_unit_type_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_material_unit_type_definitions_industry_templates_industry_",
                        column: x => x.industry_template_id,
                        principalTable: "industry_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "operation_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    industry_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    operation_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    operation_category = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_operation_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_operation_definitions_industry_templates_industry_template_",
                        column: x => x.industry_template_id,
                        principalTable: "industry_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "routes",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    industry_template_id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    route_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    product_family = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_routes", x => x.id);
                    table.ForeignKey(
                        name: "fk_routes_industry_templates_industry_template_id",
                        column: x => x.industry_template_id,
                        principalTable: "industry_templates",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

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

            migrationBuilder.CreateTable(
                name: "areas",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    parent_area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    area_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    area_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    area_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    sort_order = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("pk_areas", x => x.id);
                    table.ForeignKey(
                        name: "fk_areas_areas_parent_area_id",
                        column: x => x.parent_area_id,
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_areas_sites_site_id",
                        column: x => x.site_id,
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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
                    table.ForeignKey(
                        name: "fk_material_units_sites_site_id",
                        column: x => x.site_id,
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "connection_profiles",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_system_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    connection_profile_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    connection_profile_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    provider_type = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    connection_mode = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    host_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: true),
                    port = table.Column<int>(type: "integer", nullable: true),
                    database_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    schema_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    file_root_path = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    api_base_url = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    secret_reference = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    connection_options_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
                    import_schedule_expression = table.Column<string>(type: "character varying(250)", maxLength: 250, nullable: false, defaultValue: "Every 15 minutes"),
                    import_interval_minutes = table.Column<int>(type: "integer", nullable: false, defaultValue: 15),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    read_only_enforced = table.Column<bool>(type: "boolean", nullable: false),
                    description = table.Column<string>(type: "character varying(1000)", maxLength: 1000, nullable: true),
                    last_tested_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    last_test_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    last_test_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("pk_connection_profiles", x => x.id);
                    table.ForeignKey(
                        name: "fk_connection_profiles_source_system_definitions_source_system",
                        column: x => x.source_system_definition_id,
                        principalTable: "source_system_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "import_batches",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_system_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_batch_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    import_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    started_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    completed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    source_object_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    file_name = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    checksum = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    row_count = table.Column<int>(type: "integer", nullable: true),
                    error_message = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
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
                    table.PrimaryKey("pk_import_batches", x => x.id);
                    table.ForeignKey(
                        name: "fk_import_batches_source_system_definitions_source_system_defi",
                        column: x => x.source_system_definition_id,
                        principalTable: "source_system_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "mapping_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_system_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    mapping_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    mapping_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_object_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    target_entity_name = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    mapping_json = table.Column<string>(type: "jsonb", nullable: false),
                    mapping_version = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
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
                    table.PrimaryKey("pk_mapping_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_mapping_definitions_source_system_definitions_source_system",
                        column: x => x.source_system_definition_id,
                        principalTable: "source_system_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateTable(
                name: "route_steps",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    route_id = table.Column<Guid>(type: "uuid", nullable: false),
                    operation_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    sequence_no = table.Column<int>(type: "integer", nullable: false),
                    expected_material_unit_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    is_required = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_route_steps", x => x.id);
                    table.ForeignKey(
                        name: "fk_route_steps_operation_definitions_operation_definition_id",
                        column: x => x.operation_definition_id,
                        principalTable: "operation_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_route_steps_routes_route_id",
                        column: x => x.route_id,
                        principalTable: "routes",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "equipment",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    site_id = table.Column<Guid>(type: "uuid", nullable: false),
                    area_id = table.Column<Guid>(type: "uuid", nullable: true),
                    parent_equipment_id = table.Column<Guid>(type: "uuid", nullable: true),
                    equipment_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    equipment_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    equipment_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    manufacturer = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
                    sort_order = table.Column<int>(type: "integer", nullable: true),
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
                    table.PrimaryKey("pk_equipment", x => x.id);
                    table.ForeignKey(
                        name: "fk_equipment_areas_area_id",
                        column: x => x.area_id,
                        principalTable: "areas",
                        principalColumn: "id",
                        onDelete: ReferentialAction.SetNull);
                    table.ForeignKey(
                        name: "fk_equipment_equipment_parent_equipment_id",
                        column: x => x.parent_equipment_id,
                        principalTable: "equipment",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                    table.ForeignKey(
                        name: "fk_equipment_sites_site_id",
                        column: x => x.site_id,
                        principalTable: "sites",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
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
                    contribution_weight = table.Column<decimal>(type: "numeric(9,6)", nullable: false, defaultValue: 1.0m),
                    is_transition = table.Column<bool>(type: "boolean", nullable: false, defaultValue: false),
                    provenance_confidence = table.Column<decimal>(type: "numeric(9,6)", nullable: false, defaultValue: 1.0m),
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
                    operation_definition_id = table.Column<Guid>(type: "uuid", nullable: true),
                    operation_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    operation_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
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
                    table.ForeignKey(
                        name: "fk_process_step_executions_operation_definitions_operation_def",
                        column: x => x.operation_definition_id,
                        principalTable: "operation_definitions",
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
                    explanation_json = table.Column<string>(type: "jsonb", nullable: true),
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
                name: "source_dataset_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    connection_profile_id = table.Column<Guid>(type: "uuid", nullable: false),
                    dataset_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    dataset_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    dataset_kind = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    next_run_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    source_object_name = table.Column<string>(type: "character varying(300)", maxLength: 300, nullable: false),
                    source_schema_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    primary_timestamp_field = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    incremental_cursor_field = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
                    last_cursor_value = table.Column<string>(type: "character varying(500)", maxLength: 500, nullable: true),
                    refresh_interval_seconds = table.Column<int>(type: "integer", nullable: false),
                    dataset_options_json = table.Column<string>(type: "jsonb", nullable: false, defaultValue: "{}"),
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
                    table.PrimaryKey("pk_source_dataset_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_source_dataset_definitions_connection_profiles_connection_p",
                        column: x => x.connection_profile_id,
                        principalTable: "connection_profiles",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
                });

            migrationBuilder.CreateTable(
                name: "staging_records",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    import_batch_id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_object_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    row_number = table.Column<int>(type: "integer", nullable: false),
                    raw_json = table.Column<string>(type: "jsonb", nullable: false),
                    is_processed = table.Column<bool>(type: "boolean", nullable: false),
                    processed_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    processing_status = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: false),
                    processing_error = table.Column<string>(type: "character varying(4000)", maxLength: 4000, nullable: true),
                    canonical_entity_id = table.Column<Guid>(type: "uuid", nullable: true),
                    canonical_entity_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("pk_staging_records", x => x.id);
                    table.ForeignKey(
                        name: "fk_staging_records_import_batches_import_batch_id",
                        column: x => x.import_batch_id,
                        principalTable: "import_batches",
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
                name: "source_field_definitions",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    source_dataset_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    field_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    display_name = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: false),
                    source_data_type = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    ordinal = table.Column<int>(type: "integer", nullable: false),
                    is_nullable = table.Column<bool>(type: "boolean", nullable: false),
                    max_length = table.Column<int>(type: "integer", nullable: true),
                    numeric_precision = table.Column<int>(type: "integer", nullable: true),
                    numeric_scale = table.Column<int>(type: "integer", nullable: true),
                    sample_value = table.Column<string>(type: "character varying(2000)", maxLength: 2000, nullable: true),
                    is_primary_key_candidate = table.Column<bool>(type: "boolean", nullable: false),
                    is_timestamp_candidate = table.Column<bool>(type: "boolean", nullable: false),
                    is_active = table.Column<bool>(type: "boolean", nullable: false),
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
                    table.PrimaryKey("pk_source_field_definitions", x => x.id);
                    table.ForeignKey(
                        name: "fk_source_field_definitions_source_dataset_definitions_source_",
                        column: x => x.source_dataset_definition_id,
                        principalTable: "source_dataset_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Cascade);
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
                name: "ix_areas_area_type",
                table: "areas",
                column: "area_type");

            migrationBuilder.CreateIndex(
                name: "ix_areas_parent_area_id",
                table: "areas",
                column: "parent_area_id");

            migrationBuilder.CreateIndex(
                name: "ix_areas_site_id",
                table: "areas",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "ix_areas_site_id_area_code",
                table: "areas",
                columns: new[] { "site_id", "area_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_correlation",
                table: "audit_log_entries",
                column: "correlation_id");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_created_at",
                table: "audit_log_entries",
                column: "created_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_is_deleted",
                table: "audit_log_entries",
                column: "is_deleted");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_occurred_at",
                table: "audit_log_entries",
                column: "occurred_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_resource",
                table: "audit_log_entries",
                columns: new[] { "resource_type", "resource_id" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_user_occurred",
                table: "audit_log_entries",
                columns: new[] { "user_id", "occurred_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_connection_profiles_connection_profile_code",
                table: "connection_profiles",
                column: "connection_profile_code",
                unique: true,
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "ix_connection_profiles_import_interval_minutes",
                table: "connection_profiles",
                column: "import_interval_minutes");

            migrationBuilder.CreateIndex(
                name: "ix_connection_profiles_is_active",
                table: "connection_profiles",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_connection_profiles_provider_type",
                table: "connection_profiles",
                column: "provider_type");

            migrationBuilder.CreateIndex(
                name: "ix_connection_profiles_source_system_definition_id",
                table: "connection_profiles",
                column: "source_system_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_correlation_results_calculated_at_utc",
                table: "correlation_results",
                column: "calculated_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_correlation_results_correlation_type",
                table: "correlation_results",
                column: "correlation_type");

            migrationBuilder.CreateIndex(
                name: "ix_correlation_results_correlation_type_subject_code_outcome_c",
                table: "correlation_results",
                columns: new[] { "correlation_type", "subject_code", "outcome_code" });

            migrationBuilder.CreateIndex(
                name: "ix_correlation_results_outcome_code",
                table: "correlation_results",
                column: "outcome_code");

            migrationBuilder.CreateIndex(
                name: "ix_correlation_results_subject_code",
                table: "correlation_results",
                column: "subject_code");

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_definitions_dashboard_code",
                table: "dashboard_definitions",
                column: "dashboard_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_definitions_is_active",
                table: "dashboard_definitions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_definitions_is_default",
                table: "dashboard_definitions",
                column: "is_default");

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_definitions_is_system_template",
                table: "dashboard_definitions",
                column: "is_system_template");

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_definitions_user_id",
                table: "dashboard_definitions",
                column: "user_id");

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_widget_definitions_chart_type_dimension_code_meas",
                table: "dashboard_widget_definitions",
                columns: new[] { "chart_type", "dimension_code", "measure_code" });

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_widget_definitions_dashboard_definition_id",
                table: "dashboard_widget_definitions",
                column: "dashboard_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_widget_definitions_dashboard_definition_id_sort_o",
                table: "dashboard_widget_definitions",
                columns: new[] { "dashboard_definition_id", "sort_order" });

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_widget_definitions_expression_refresh",
                table: "dashboard_widget_definitions",
                columns: new[] { "expression_enabled", "expression_last_validated_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_widget_definitions_is_active",
                table: "dashboard_widget_definitions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_widget_definitions_widget_code",
                table: "dashboard_widget_definitions",
                columns: new[] { "dashboard_definition_id", "widget_code" },
                unique: true);

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
                name: "ix_equipment_area_id",
                table: "equipment",
                column: "area_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_equipment_type",
                table: "equipment",
                column: "equipment_type");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_is_active",
                table: "equipment",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_parent_equipment_id",
                table: "equipment",
                column: "parent_equipment_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_site_id",
                table: "equipment",
                column: "site_id");

            migrationBuilder.CreateIndex(
                name: "ix_equipment_site_id_equipment_code",
                table: "equipment",
                columns: new[] { "site_id", "equipment_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_genealogy_edges_child_material_unit_id",
                table: "genealogy_edges",
                column: "child_material_unit_id");

            migrationBuilder.CreateIndex(
                name: "ix_genealogy_edges_child_material_unit_id_is_transition_contri",
                table: "genealogy_edges",
                columns: new[] { "child_material_unit_id", "is_transition", "contribution_weight" });

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
                name: "ix_import_batches_import_batch_code",
                table: "import_batches",
                column: "import_batch_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_import_batches_source_system_definition_id",
                table: "import_batches",
                column: "source_system_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_import_batches_started_at_utc",
                table: "import_batches",
                column: "started_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_import_batches_status",
                table: "import_batches",
                column: "status");

            migrationBuilder.CreateIndex(
                name: "ix_industry_templates_industry_name",
                table: "industry_templates",
                column: "industry_name");

            migrationBuilder.CreateIndex(
                name: "ix_industry_templates_is_active",
                table: "industry_templates",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_industry_templates_template_code",
                table: "industry_templates",
                column: "template_code",
                unique: true);

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
                name: "ix_mapping_definitions_is_active",
                table: "mapping_definitions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_mapping_definitions_mapping_code",
                table: "mapping_definitions",
                column: "mapping_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_mapping_definitions_source_system_definition_id",
                table: "mapping_definitions",
                column: "source_system_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_mapping_definitions_source_system_definition_id_source_obje",
                table: "mapping_definitions",
                columns: new[] { "source_system_definition_id", "source_object_name", "target_entity_name", "mapping_version" });

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
                name: "ix_material_unit_type_definitions_industry_template_id",
                table: "material_unit_type_definitions",
                column: "industry_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_material_unit_type_definitions_industry_template_id_materia",
                table: "material_unit_type_definitions",
                columns: new[] { "industry_template_id", "material_unit_type_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_material_unit_type_definitions_is_active",
                table: "material_unit_type_definitions",
                column: "is_active");

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
                name: "ix_material_units_site_id_material_code",
                table: "material_units",
                columns: new[] { "site_id", "material_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_material_units_site_id_material_unit_type",
                table: "material_units",
                columns: new[] { "site_id", "material_unit_type" });

            migrationBuilder.CreateIndex(
                name: "ix_material_units_source_system_source_record_id",
                table: "material_units",
                columns: new[] { "source_system", "source_record_id" },
                unique: true,
                filter: "source_system IS NOT NULL AND source_record_id IS NOT NULL");

            migrationBuilder.CreateIndex(
                name: "ix_model_registries_is_active",
                table: "model_registries",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_model_registries_model_code",
                table: "model_registries",
                column: "model_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_model_registries_risk_type_model_version",
                table: "model_registries",
                columns: new[] { "risk_type", "model_version" });

            migrationBuilder.CreateIndex(
                name: "ix_operation_definitions_industry_template_id",
                table: "operation_definitions",
                column: "industry_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_operation_definitions_industry_template_id_operation_code",
                table: "operation_definitions",
                columns: new[] { "industry_template_id", "operation_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_operation_definitions_is_active",
                table: "operation_definitions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_operation_definitions_operation_category",
                table: "operation_definitions",
                column: "operation_category");

            migrationBuilder.CreateIndex(
                name: "ix_parameter_definitions_industry_template",
                table: "parameter_definitions",
                column: "industry_template");

            migrationBuilder.CreateIndex(
                name: "ix_parameter_definitions_industry_template_parameter_code",
                table: "parameter_definitions",
                columns: new[] { "industry_template", "parameter_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_parameter_definitions_parameter_category",
                table: "parameter_definitions",
                column: "parameter_category");

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
                name: "ix_process_step_executions_operation_code",
                table: "process_step_executions",
                column: "operation_code");

            migrationBuilder.CreateIndex(
                name: "ix_process_step_executions_operation_definition_id",
                table: "process_step_executions",
                column: "operation_definition_id");

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
                name: "ix_risk_scores_risk_class",
                table: "risk_scores",
                column: "risk_class");

            migrationBuilder.CreateIndex(
                name: "ix_risk_scores_risk_type",
                table: "risk_scores",
                column: "risk_type");

            migrationBuilder.CreateIndex(
                name: "ix_risk_scores_risk_type_risk_class_score",
                table: "risk_scores",
                columns: new[] { "risk_type", "risk_class", "score" });

            migrationBuilder.CreateIndex(
                name: "ix_risk_scores_score",
                table: "risk_scores",
                column: "score");

            migrationBuilder.CreateIndex(
                name: "ix_risk_scores_scored_at_utc",
                table: "risk_scores",
                column: "scored_at_utc");

            migrationBuilder.CreateIndex(
                name: "ix_route_steps_expected_material_unit_type",
                table: "route_steps",
                column: "expected_material_unit_type");

            migrationBuilder.CreateIndex(
                name: "ix_route_steps_operation_definition_id",
                table: "route_steps",
                column: "operation_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_route_steps_route_id",
                table: "route_steps",
                column: "route_id");

            migrationBuilder.CreateIndex(
                name: "ix_route_steps_route_id_sequence_no",
                table: "route_steps",
                columns: new[] { "route_id", "sequence_no" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_routes_industry_template_id",
                table: "routes",
                column: "industry_template_id");

            migrationBuilder.CreateIndex(
                name: "ix_routes_industry_template_id_route_code",
                table: "routes",
                columns: new[] { "industry_template_id", "route_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_routes_is_active",
                table: "routes",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_routes_product_family",
                table: "routes",
                column: "product_family");

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

            migrationBuilder.CreateIndex(
                name: "ix_sites_country_code",
                table: "sites",
                column: "country_code");

            migrationBuilder.CreateIndex(
                name: "ix_sites_site_code",
                table: "sites",
                column: "site_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_source_dataset_definitions_connection_profile_id",
                table: "source_dataset_definitions",
                column: "connection_profile_id");

            migrationBuilder.CreateIndex(
                name: "ix_source_dataset_definitions_connection_profile_id_dataset_co",
                table: "source_dataset_definitions",
                columns: new[] { "connection_profile_id", "dataset_code" },
                unique: true,
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "ix_source_dataset_definitions_dataset_kind",
                table: "source_dataset_definitions",
                column: "dataset_kind");

            migrationBuilder.CreateIndex(
                name: "ix_source_dataset_definitions_is_active",
                table: "source_dataset_definitions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_source_dataset_definitions_next_run",
                table: "source_dataset_definitions",
                columns: new[] { "is_active", "is_deleted", "next_run_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_source_dataset_definitions_source_object_name",
                table: "source_dataset_definitions",
                column: "source_object_name");

            migrationBuilder.CreateIndex(
                name: "ix_source_field_definitions_field_name",
                table: "source_field_definitions",
                column: "field_name");

            migrationBuilder.CreateIndex(
                name: "ix_source_field_definitions_ordinal",
                table: "source_field_definitions",
                column: "ordinal");

            migrationBuilder.CreateIndex(
                name: "ix_source_field_definitions_source_data_type",
                table: "source_field_definitions",
                column: "source_data_type");

            migrationBuilder.CreateIndex(
                name: "ix_source_field_definitions_source_dataset_definition_id",
                table: "source_field_definitions",
                column: "source_dataset_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_source_field_definitions_source_dataset_definition_id_field",
                table: "source_field_definitions",
                columns: new[] { "source_dataset_definition_id", "field_name" },
                unique: true,
                filter: "is_deleted = FALSE");

            migrationBuilder.CreateIndex(
                name: "ix_source_system_definitions_is_active",
                table: "source_system_definitions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_source_system_definitions_source_system_code",
                table: "source_system_definitions",
                column: "source_system_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_source_system_definitions_source_system_type",
                table: "source_system_definitions",
                column: "source_system_type");

            migrationBuilder.CreateIndex(
                name: "ix_staging_records_canonical_entity_id",
                table: "staging_records",
                column: "canonical_entity_id");

            migrationBuilder.CreateIndex(
                name: "ix_staging_records_import_batch_id",
                table: "staging_records",
                column: "import_batch_id");

            migrationBuilder.CreateIndex(
                name: "ix_staging_records_import_batch_id_is_processed",
                table: "staging_records",
                columns: new[] { "import_batch_id", "is_processed" });

            migrationBuilder.CreateIndex(
                name: "ix_staging_records_import_batch_id_processing_status",
                table: "staging_records",
                columns: new[] { "import_batch_id", "processing_status" });

            migrationBuilder.CreateIndex(
                name: "ix_staging_records_import_batch_id_source_object_name_row_numb",
                table: "staging_records",
                columns: new[] { "import_batch_id", "source_object_name", "row_number" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log_entries");

            migrationBuilder.DropTable(
                name: "correlation_results");

            migrationBuilder.DropTable(
                name: "dashboard_widget_definitions");

            migrationBuilder.DropTable(
                name: "data_quality_issues");

            migrationBuilder.DropTable(
                name: "downtime_events");

            migrationBuilder.DropTable(
                name: "equipment");

            migrationBuilder.DropTable(
                name: "genealogy_edges");

            migrationBuilder.DropTable(
                name: "job_run_histories");

            migrationBuilder.DropTable(
                name: "kpi_definitions");

            migrationBuilder.DropTable(
                name: "mapping_definitions");

            migrationBuilder.DropTable(
                name: "material_aliases");

            migrationBuilder.DropTable(
                name: "material_unit_type_definitions");

            migrationBuilder.DropTable(
                name: "model_registries");

            migrationBuilder.DropTable(
                name: "parameter_observations");

            migrationBuilder.DropTable(
                name: "process_events");

            migrationBuilder.DropTable(
                name: "quality_events");

            migrationBuilder.DropTable(
                name: "risk_scores");

            migrationBuilder.DropTable(
                name: "route_steps");

            migrationBuilder.DropTable(
                name: "source_field_definitions");

            migrationBuilder.DropTable(
                name: "staging_records");

            migrationBuilder.DropTable(
                name: "dashboard_definitions");

            migrationBuilder.DropTable(
                name: "areas");

            migrationBuilder.DropTable(
                name: "job_definitions");

            migrationBuilder.DropTable(
                name: "schema_view_definitions");

            migrationBuilder.DropTable(
                name: "parameter_definitions");

            migrationBuilder.DropTable(
                name: "process_step_executions");

            migrationBuilder.DropTable(
                name: "defect_catalogs");

            migrationBuilder.DropTable(
                name: "routes");

            migrationBuilder.DropTable(
                name: "import_batches");

            migrationBuilder.DropTable(
                name: "source_dataset_definitions");

            migrationBuilder.DropTable(
                name: "material_units");

            migrationBuilder.DropTable(
                name: "operation_definitions");

            migrationBuilder.DropTable(
                name: "connection_profiles");

            migrationBuilder.DropTable(
                name: "sites");

            migrationBuilder.DropTable(
                name: "industry_templates");

            migrationBuilder.DropTable(
                name: "source_system_definitions");
        }
    }
}
