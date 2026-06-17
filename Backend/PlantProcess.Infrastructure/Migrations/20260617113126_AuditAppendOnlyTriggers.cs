using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations
{
    /// <inheritdoc />
    public partial class AuditAppendOnlyTriggers : Migration
    {
        /// <inheritdoc />
        protected override void Up(MigrationBuilder migrationBuilder)
        {
                migrationBuilder.Sql(@"
            CREATE OR REPLACE FUNCTION public.prevent_audit_log_mutation()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                RAISE EXCEPTION 'audit_log_entries is append-only. Operation % is not allowed.', TG_OP
                    USING ERRCODE = 'P0001';
            END;
            $$;");

                migrationBuilder.Sql(@"
            CREATE OR REPLACE TRIGGER trg_prevent_audit_log_update
            BEFORE UPDATE ON public.audit_log_entries
            FOR EACH ROW EXECUTE FUNCTION public.prevent_audit_log_mutation();");

                migrationBuilder.Sql(@"
            CREATE OR REPLACE TRIGGER trg_prevent_audit_log_delete
            BEFORE DELETE ON public.audit_log_entries
            FOR EACH ROW EXECUTE FUNCTION public.prevent_audit_log_mutation();");

                migrationBuilder.Sql(@"
            CREATE OR REPLACE TRIGGER trg_prevent_audit_log_truncate
            BEFORE TRUNCATE ON public.audit_log_entries
            FOR EACH STATEMENT EXECUTE FUNCTION public.prevent_audit_log_mutation();");
            }

            /// <inheritdoc />
        protected override void Down(MigrationBuilder migrationBuilder)
        {
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_prevent_audit_log_truncate ON public.audit_log_entries;");
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_prevent_audit_log_delete ON public.audit_log_entries;");
            migrationBuilder.Sql(@"DROP TRIGGER IF EXISTS trg_prevent_audit_log_update ON public.audit_log_entries;");
            migrationBuilder.Sql(@"DROP FUNCTION IF EXISTS public.prevent_audit_log_mutation();");
        }
    }
}
