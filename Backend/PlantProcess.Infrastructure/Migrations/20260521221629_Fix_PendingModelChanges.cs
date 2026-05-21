using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class Fix_PendingModelChanges : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_dashboard_widget_definitions_widget_code",
                table: "dashboard_widget_definitions");

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_widget_definitions_widget_code",
                table: "dashboard_widget_definitions",
                columns: new[] { "dashboard_definition_id", "widget_code" },
                unique: true);
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropIndex(
                name: "ix_dashboard_widget_definitions_widget_code",
                table: "dashboard_widget_definitions");

            migrationBuilder.CreateIndex(
                name: "ix_dashboard_widget_definitions_widget_code",
                table: "dashboard_widget_definitions",
                column: "widget_code",
                unique: true);
        }
    }
}
