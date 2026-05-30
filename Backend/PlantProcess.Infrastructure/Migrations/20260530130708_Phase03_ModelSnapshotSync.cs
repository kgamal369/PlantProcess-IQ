using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations;

public partial class Phase03_ModelSnapshotSync : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        // Snapshot-sync migration only.
        // The current database already contains the schema changes represented by the model snapshot.
        // This intentionally applies no physical schema changes.
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        // Intentionally empty.
        // Do not drop existing runtime columns from a snapshot-sync migration.
    }
}
