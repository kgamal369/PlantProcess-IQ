using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Phase3_ModelConsistencyFixes : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_parameter_definitions_parameter_code",
                table: "parameter_definitions");

            migrationBuilder.DropIndex(
                name: "ix_material_units_material_code",
                table: "material_units");

            migrationBuilder.AddColumn<Guid>(
                name: "operation_definition_id",
                table: "process_step_executions",
                type: "uuid",
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_process_step_executions_operation_code",
                table: "process_step_executions",
                column: "operation_code");

            migrationBuilder.CreateIndex(
                name: "ix_process_step_executions_operation_definition_id",
                table: "process_step_executions",
                column: "operation_definition_id");

            migrationBuilder.CreateIndex(
                name: "ix_parameter_definitions_industry_template_parameter_code",
                table: "parameter_definitions",
                columns: new[] { "industry_template", "parameter_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_material_units_site_id_material_code",
                table: "material_units",
                columns: new[] { "site_id", "material_code" },
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_material_units_source_system_source_record_id",
                table: "material_units",
                columns: new[] { "source_system", "source_record_id" },
                unique: true,
                filter: "source_system IS NOT NULL AND source_record_id IS NOT NULL");

            migrationBuilder.AddForeignKey(
                name: "fk_process_step_executions_operation_definitions_operation_def",
                table: "process_step_executions",
                column: "operation_definition_id",
                principalTable: "operation_definitions",
                principalColumn: "id",
                onDelete: ReferentialAction.Restrict);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropForeignKey(
                name: "fk_process_step_executions_operation_definitions_operation_def",
                table: "process_step_executions");

            migrationBuilder.DropIndex(
                name: "ix_process_step_executions_operation_code",
                table: "process_step_executions");

            migrationBuilder.DropIndex(
                name: "ix_process_step_executions_operation_definition_id",
                table: "process_step_executions");

            migrationBuilder.DropIndex(
                name: "ix_parameter_definitions_industry_template_parameter_code",
                table: "parameter_definitions");

            migrationBuilder.DropIndex(
                name: "ix_material_units_site_id_material_code",
                table: "material_units");

            migrationBuilder.DropIndex(
                name: "ix_material_units_source_system_source_record_id",
                table: "material_units");

            migrationBuilder.DropColumn(
                name: "operation_definition_id",
                table: "process_step_executions");

            migrationBuilder.CreateIndex(
                name: "ix_parameter_definitions_parameter_code",
                table: "parameter_definitions",
                column: "parameter_code",
                unique: true);

            migrationBuilder.CreateIndex(
                name: "ix_material_units_material_code",
                table: "material_units",
                column: "material_code",
                unique: true);
        }
    }
}
