using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDashboardDefinitions : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
                name: "ix_dashboard_widget_definitions_is_active",
                table: "dashboard_widget_definitions",
                column: "is_active");

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_widget_definitions_widget_code",
                table: "dashboard_widget_definitions",
                column: "widget_code",
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "dashboard_widget_definitions");

            migrationBuilder.DropTable(
                name: "dashboard_definitions");
        }
    }
}
