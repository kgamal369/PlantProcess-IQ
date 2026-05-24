-- ============================================================================
-- PlantProcess IQ
-- File: Backend/database/scripts/096_harden_audit_log_immutability_local_fallback.sql
--
-- Task:
--   BE-ADD-001 — AuditLogEntry immutability local proof
--
-- Purpose:
--   Enforce append-only behavior on audit_log_entries using database triggers.
--
-- Why this file exists:
--   Your current database user owns audit_log_entries but does NOT have
--   CREATEROLE permission. Therefore, the separate runtime role
--   plantprocess_app cannot be created from this connection.
--
-- What this proves:
--   UPDATE / DELETE / TRUNCATE are blocked at database level by trigger.
--
-- What this does NOT prove:
--   Separate runtime-role privilege isolation.
--
-- Required user:
--   The current table owner or a database admin.
--
-- Safe:
--   Idempotent. Can be rerun.
-- ============================================================================

BEGIN;

-- ============================================================================
-- 1. Pre-flight validation
-- ============================================================================

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = 'public'
          AND table_name = 'audit_log_entries'
    ) THEN
        RAISE EXCEPTION
            'audit_log_entries table does not exist. Run EF migrations before audit immutability hardening.';
    END IF;
END $$;

-- ============================================================================
-- 2. Trigger function
-- ============================================================================

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

-- ============================================================================
-- 3. UPDATE protection
-- ============================================================================

DROP TRIGGER IF EXISTS trg_prevent_audit_log_update
ON public.audit_log_entries;

CREATE TRIGGER trg_prevent_audit_log_update
BEFORE UPDATE ON public.audit_log_entries
FOR EACH ROW
EXECUTE FUNCTION public.prevent_audit_log_mutation();

-- ============================================================================
-- 4. DELETE protection
-- ============================================================================

DROP TRIGGER IF EXISTS trg_prevent_audit_log_delete
ON public.audit_log_entries;

CREATE TRIGGER trg_prevent_audit_log_delete
BEFORE DELETE ON public.audit_log_entries
FOR EACH ROW
EXECUTE FUNCTION public.prevent_audit_log_mutation();

-- ============================================================================
-- 5. TRUNCATE protection
-- ============================================================================

DROP TRIGGER IF EXISTS trg_prevent_audit_log_truncate
ON public.audit_log_entries;

CREATE TRIGGER trg_prevent_audit_log_truncate
BEFORE TRUNCATE ON public.audit_log_entries
FOR EACH STATEMENT
EXECUTE FUNCTION public.prevent_audit_log_mutation();

-- ============================================================================
-- 6. Verification output
-- ============================================================================

SELECT
    'local trigger-based audit immutability applied' AS status,
    current_database() AS database_name,
    current_user AS executed_by,
    (
        SELECT tableowner
        FROM pg_tables
        WHERE schemaname = 'public'
          AND tablename = 'audit_log_entries'
    ) AS audit_table_owner;

SELECT
    trigger_name,
    event_manipulation,
    action_timing,
    event_object_schema,
    event_object_table
FROM information_schema.triggers
WHERE event_object_schema = 'public'
  AND event_object_table = 'audit_log_entries'
ORDER BY trigger_name, event_manipulation;

COMMIT;