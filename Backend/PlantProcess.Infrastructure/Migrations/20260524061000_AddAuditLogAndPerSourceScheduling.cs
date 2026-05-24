using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddAuditLogAndPerSourceScheduling : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<DateTime>(
                name: "next_run_at_utc",
                table: "source_dataset_definitions",
                type: "timestamp with time zone",
                nullable: true);

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
                    source_system = table.Column<string>(type: "text", nullable: true),
                    source_record_id = table.Column<string>(type: "text", nullable: true),
                    is_deleted = table.Column<bool>(type: "boolean", nullable: false),
                    deleted_at_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    deleted_reason = table.Column<string>(type: "text", nullable: true)
                },
                constraints: table =>
                {
                    table.PrimaryKey("pk_audit_log_entries", x => x.id);
                });

            migrationBuilder.CreateIndex(
                name: "ix_source_dataset_definitions_next_run",
                table: "source_dataset_definitions",
                columns: new[] { "is_active", "is_deleted", "next_run_at_utc" });

            migrationBuilder.CreateIndex(
                name: "ix_audit_log_correlation",
                table: "audit_log_entries",
                column: "correlation_id");

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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "audit_log_entries");

            migrationBuilder.DropIndex(
                name: "ix_source_dataset_definitions_next_run",
                table: "source_dataset_definitions");

            migrationBuilder.DropColumn(
                name: "next_run_at_utc",
                table: "source_dataset_definitions");
        }
    }
}
