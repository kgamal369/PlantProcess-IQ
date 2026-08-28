using System;
using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class StorageTopologyCheckpoint : Migration
    {
                /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Checkpoint only. The three governed schemas are ensured here so that every
            // later historical script and the terminal convergence file find them present.
            // No table is relocated by a migration: relocation happens once, after every
            // historical creator has run, in the terminal convergence SQL.
            migrationBuilder.EnsureSchema(name: "ppiq_meta");
            migrationBuilder.EnsureSchema(name: "ppiq_plant");
            migrationBuilder.EnsureSchema(name: "ppiq_staging");
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // The schemas are left in place: dropping them would take the relocated
            // tables with them, and this migration never moved a table.
        }
    }
}
