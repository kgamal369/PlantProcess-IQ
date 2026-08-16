using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class T044R1MigrationParity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "position_end_m",
                table: "quality_events",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "position_start_m",
                table: "quality_events",
                type: "numeric",
                nullable: true);

            migrationBuilder.AddColumn<decimal>(
                name: "width_position_mm",
                table: "quality_events",
                type: "numeric",
                nullable: true);

            migrationBuilder.CreateTable(
                name: "product_specifications",
                columns: table => new
                {
                    id = table.Column<Guid>(type: "uuid", nullable: false),
                    specification_code = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    product_family = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: true),
                    grade_or_recipe = table.Column<string>(type: "character varying(100)", maxLength: 100, nullable: false),
                    parameter_definition_id = table.Column<Guid>(type: "uuid", nullable: false),
                    min_value = table.Column<decimal>(type: "numeric", nullable: true),
                    target_value = table.Column<decimal>(type: "numeric", nullable: true),
                    max_value = table.Column<decimal>(type: "numeric", nullable: true),
                    unit_of_measure = table.Column<string>(type: "character varying(50)", maxLength: 50, nullable: true),
                    effective_from_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: false),
                    effective_to_utc = table.Column<DateTime>(type: "timestamp with time zone", nullable: true),
                    provenance = table.Column<string>(type: "character varying(200)", maxLength: 200, nullable: true),
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
                    table.PrimaryKey("pk_product_specifications", x => x.id);
                    table.ForeignKey(
                        name: "fk_product_specifications_parameter_definitions_parameter_defi",
                        column: x => x.parameter_definition_id,
                        principalTable: "parameter_definitions",
                        principalColumn: "id",
                        onDelete: ReferentialAction.Restrict);
                });

            migrationBuilder.CreateIndex(
                name: "ix_product_specifications_grade_or_recipe",
                table: "product_specifications",
                column: "grade_or_recipe");

            migrationBuilder.CreateIndex(
                name: "ix_product_specifications_grade_or_recipe_parameter_definition",
                table: "product_specifications",
                columns: new[] { "grade_or_recipe", "parameter_definition_id" });

            migrationBuilder.CreateIndex(
                name: "ix_product_specifications_grade_or_recipe_parameter_definition1",
                table: "product_specifications",
                columns: new[] { "grade_or_recipe", "parameter_definition_id", "effective_from_utc" },
                unique: true,
                filter: "is_deleted = false");

            migrationBuilder.CreateIndex(
                name: "ix_product_specifications_parameter_definition_id",
                table: "product_specifications",
                column: "parameter_definition_id");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "product_specifications");

            migrationBuilder.DropColumn(
                name: "position_end_m",
                table: "quality_events");

            migrationBuilder.DropColumn(
                name: "position_start_m",
                table: "quality_events");

            migrationBuilder.DropColumn(
                name: "width_position_mm",
                table: "quality_events");
        }
    }
}
