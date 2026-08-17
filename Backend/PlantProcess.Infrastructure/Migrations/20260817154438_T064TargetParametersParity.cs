using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class T064TargetParametersParity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<string>(
                name: "target_parameters",
                table: "job_run_histories",
                type: "jsonb",
                nullable: true);

            migrationBuilder.AddColumn<string>(
                name: "target_parameters",
                table: "job_definitions",
                type: "jsonb",
                nullable: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropColumn(
                name: "target_parameters",
                table: "job_run_histories");

            migrationBuilder.DropColumn(
                name: "target_parameters",
                table: "job_definitions");
        }
    }
}
