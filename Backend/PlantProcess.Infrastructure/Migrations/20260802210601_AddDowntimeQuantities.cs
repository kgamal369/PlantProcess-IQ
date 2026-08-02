using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AddDowntimeQuantities : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.AddColumn<decimal>(
                name: "production_impact_minutes",
                table: "downtime_events",
                type: "numeric(12,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddColumn<decimal>(
                name: "stopped_minutes",
                table: "downtime_events",
                type: "numeric(12,3)",
                nullable: false,
                defaultValue: 0m);

            migrationBuilder.AddCheckConstraint(
                name: "ck_downtime_events_production_impact_minutes_nonneg",
                table: "downtime_events",
                sql: "production_impact_minutes >= 0");

            migrationBuilder.AddCheckConstraint(
                name: "ck_downtime_events_stopped_minutes_nonneg",
                table: "downtime_events",
                sql: "stopped_minutes >= 0");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.DropCheckConstraint(
                name: "ck_downtime_events_production_impact_minutes_nonneg",
                table: "downtime_events");

            migrationBuilder.DropCheckConstraint(
                name: "ck_downtime_events_stopped_minutes_nonneg",
                table: "downtime_events");

            migrationBuilder.DropColumn(
                name: "production_impact_minutes",
                table: "downtime_events");

            migrationBuilder.DropColumn(
                name: "stopped_minutes",
                table: "downtime_events");
        }
    }
}
