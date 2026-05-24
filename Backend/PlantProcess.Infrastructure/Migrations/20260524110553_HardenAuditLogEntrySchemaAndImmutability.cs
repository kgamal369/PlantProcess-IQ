using Microsoft.EntityFrameworkCore.Migrations;

#nullable disable

namespace PlantProcess.Infrastructure.Migrations;

public partial class HardenAuditLogEntrySchemaAndImmutability : Migration
{
    protected override void Up(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            -- ============================================================================
            -- PlantProcess IQ
            -- Migration: HardenAuditLogEntrySchemaAndImmutability
            --
            -- Purpose:
            --   1. Make sure audit_log_entries contains BaseEntity columns.
            --   2. Add audit indexes if missing.
            --   3. Add database trigger guards to make audit_log_entries append-only.
            --
            -- Why:
            --   Local DBeaver hardening must be migration-backed so server DBs
            --   receive the same protection automatically.
            -- ============================================================================

            DROP TRIGGER IF EXISTS trg_prevent_audit_log_truncate
            ON public.audit_log_entries;

            DROP TRIGGER IF EXISTS trg_prevent_audit_log_delete
            ON public.audit_log_entries;

            DROP TRIGGER IF EXISTS trg_prevent_audit_log_update
            ON public.audit_log_entries;

            ALTER TABLE public.audit_log_entries
            ADD COLUMN IF NOT EXISTS created_at_utc timestamp with time zone NOT NULL DEFAULT now();

            ALTER TABLE public.audit_log_entries
            ADD COLUMN IF NOT EXISTS updated_at_utc timestamp with time zone NULL;

            ALTER TABLE public.audit_log_entries
            ADD COLUMN IF NOT EXISTS is_synthetic boolean NOT NULL DEFAULT false;

            ALTER TABLE public.audit_log_entries
            ADD COLUMN IF NOT EXISTS source_system character varying(100) NULL;

            ALTER TABLE public.audit_log_entries
            ADD COLUMN IF NOT EXISTS source_record_id character varying(200) NULL;

            ALTER TABLE public.audit_log_entries
            ADD COLUMN IF NOT EXISTS is_deleted boolean NOT NULL DEFAULT false;

            ALTER TABLE public.audit_log_entries
            ADD COLUMN IF NOT EXISTS deleted_at_utc timestamp with time zone NULL;

            ALTER TABLE public.audit_log_entries
            ADD COLUMN IF NOT EXISTS deleted_reason character varying(500) NULL;

            UPDATE public.audit_log_entries
            SET created_at_utc = COALESCE(created_at_utc, occurred_at_utc, now())
            WHERE created_at_utc IS NULL;

            UPDATE public.audit_log_entries
            SET is_synthetic = COALESCE(is_synthetic, false)
            WHERE is_synthetic IS NULL;

            UPDATE public.audit_log_entries
            SET is_deleted = COALESCE(is_deleted, false)
            WHERE is_deleted IS NULL;

            CREATE INDEX IF NOT EXISTS ix_audit_log_occurred_at
            ON public.audit_log_entries (occurred_at_utc);

            CREATE INDEX IF NOT EXISTS ix_audit_log_user_occurred
            ON public.audit_log_entries (user_id, occurred_at_utc);

            CREATE INDEX IF NOT EXISTS ix_audit_log_resource
            ON public.audit_log_entries (resource_type, resource_id);

            CREATE INDEX IF NOT EXISTS ix_audit_log_correlation
            ON public.audit_log_entries (correlation_id);

            CREATE INDEX IF NOT EXISTS ix_audit_log_created_at
            ON public.audit_log_entries (created_at_utc);

            CREATE INDEX IF NOT EXISTS ix_audit_log_is_deleted
            ON public.audit_log_entries (is_deleted);

            CREATE OR REPLACE FUNCTION public.prevent_audit_log_mutation()
            RETURNS trigger
            LANGUAGE plpgsql
            AS $$
            BEGIN
                RAISE EXCEPTION
                    'audit_log_entries is append-only. Operation % is not allowed.',
                    TG_OP
                    USING ERRCODE = 'P0001';
            END;
            $$;

            CREATE TRIGGER trg_prevent_audit_log_update
            BEFORE UPDATE ON public.audit_log_entries
            FOR EACH ROW
            EXECUTE FUNCTION public.prevent_audit_log_mutation();

            CREATE TRIGGER trg_prevent_audit_log_delete
            BEFORE DELETE ON public.audit_log_entries
            FOR EACH ROW
            EXECUTE FUNCTION public.prevent_audit_log_mutation();

            CREATE TRIGGER trg_prevent_audit_log_truncate
            BEFORE TRUNCATE ON public.audit_log_entries
            FOR EACH STATEMENT
            EXECUTE FUNCTION public.prevent_audit_log_mutation();
            """);
    }

    protected override void Down(MigrationBuilder migrationBuilder)
    {
        migrationBuilder.Sql("""
            DROP TRIGGER IF EXISTS trg_prevent_audit_log_truncate
            ON public.audit_log_entries;

            DROP TRIGGER IF EXISTS trg_prevent_audit_log_delete
            ON public.audit_log_entries;

            DROP TRIGGER IF EXISTS trg_prevent_audit_log_update
            ON public.audit_log_entries;

            DROP FUNCTION IF EXISTS public.prevent_audit_log_mutation();
            """);

        migrationBuilder.Sql("""
            DROP INDEX IF EXISTS public.ix_audit_log_is_deleted;
            DROP INDEX IF EXISTS public.ix_audit_log_created_at;
            DROP INDEX IF EXISTS public.ix_audit_log_correlation;
            DROP INDEX IF EXISTS public.ix_audit_log_resource;
            DROP INDEX IF EXISTS public.ix_audit_log_user_occurred;
            DROP INDEX IF EXISTS public.ix_audit_log_occurred_at;
            """);

        // Do not drop BaseEntity columns in Down().
        // They may already contain production audit metadata.
    }
}