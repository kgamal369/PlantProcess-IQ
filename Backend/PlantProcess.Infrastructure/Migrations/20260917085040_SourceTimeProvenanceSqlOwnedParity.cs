using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <summary>
    /// SQL-owned parity. The canonical source-time authority SQL migration is
    /// the single DDL authority for the three observation time-provenance columns.
    /// Also synchronizes only pre-measured job fields already owned by canonical SQL
    /// 843 (scheduling/freshness) and 844 (cancellation); EF store facets are pinned
    /// to those committed SQL types and no job behavior changes here.
    /// The pre-edit model probe and disposable SQL replay prove that ownership.
    /// This migration only moves the model snapshot so the canonical EF tier does not
    /// refuse with PendingModelChangesWarning; it performs no schema operation of its own.
    /// </summary>
    public partial class SourceTimeProvenanceSqlOwnedParity : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
        }

        /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
        }
    }
}
