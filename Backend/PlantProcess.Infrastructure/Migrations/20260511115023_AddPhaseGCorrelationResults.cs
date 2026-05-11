using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddPhaseGCorrelationResults : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
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
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropTable(
                name: "correlation_results");
        }
    }
}
