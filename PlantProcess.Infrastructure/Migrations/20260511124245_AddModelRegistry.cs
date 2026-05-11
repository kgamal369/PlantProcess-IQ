using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddModelRegistry : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "model_registries");
        }
    }
}
