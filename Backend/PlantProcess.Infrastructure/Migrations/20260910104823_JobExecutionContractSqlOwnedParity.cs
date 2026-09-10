using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <summary>
    /// EF model-parity checkpoint for the governed job dependency model.
    ///
    /// Physical DDL ownership remains in:
    ///   Backend/database/scripts/838_job_orchestration_convergence.sql
    ///   Backend/database/scripts/839_job_execution_contract_completion.sql
    ///
    /// The canonical installer applies the EF baseline before the ordered SQL
    /// path. Up/Down are intentionally empty so this checkpoint updates EF's
    /// model snapshot without creating a second physical schema authority.
    /// </summary>
    public partial class JobExecutionContractSqlOwnedParity : Migration
    {
        protected override void Up(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op: physical schema is created by canonical SQL 838/839.
        }

        protected override void Down(MigrationBuilder migrationBuilder)
        {
            // Intentionally no-op: EF does not own the SQL-governed physical objects.
        }
    }
}