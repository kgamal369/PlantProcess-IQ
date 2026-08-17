using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class T064JobTargetDefinitionParity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<Guid>(
                name: "target_definition_id",
                table: "job_run_histories",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_definition_kind",
                table: "job_run_histories",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "target_definition_version",
                table: "job_run_histories",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_version_policy",
                table: "job_run_histories",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.AddColumn<Guid>(
                name: "target_definition_id",
                table: "job_definitions",
                type: "uuid",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_definition_kind",
                table: "job_definitions",
                type: "character varying(64)",
                maxLength: 64,
                nullable: true);

            migrationBuilder.AddColumn<int>(
                name: "target_definition_version",
                table: "job_definitions",
                type: "integer",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_version_policy",
                table: "job_definitions",
                type: "character varying(20)",
                maxLength: 20,
                nullable: true);

            migrationBuilder.CreateIndex(
                name: "ix_job_definitions_target_definition_kind_target_definition_id",
                table: "job_definitions",
                columns: new[] { "target_definition_kind", "target_definition_id" });
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_job_definitions_target_definition_kind_target_definition_id",
                table: "job_definitions");

            migrationBuilder.DropColumn(
                name: "target_definition_id",
                table: "job_run_histories");

            migrationBuilder.DropColumn(
                name: "target_definition_kind",
                table: "job_run_histories");

            migrationBuilder.DropColumn(
                name: "target_definition_version",
                table: "job_run_histories");

            migrationBuilder.DropColumn(
                name: "target_version_policy",
                table: "job_run_histories");

            migrationBuilder.DropColumn(
                name: "target_definition_id",
                table: "job_definitions");

            migrationBuilder.DropColumn(
                name: "target_definition_kind",
                table: "job_definitions");

            migrationBuilder.DropColumn(
                name: "target_definition_version",
                table: "job_definitions");

            migrationBuilder.DropColumn(
                name: "target_version_policy",
                table: "job_definitions");
        }
    }
}
