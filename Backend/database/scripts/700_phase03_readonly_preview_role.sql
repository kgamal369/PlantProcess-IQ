-- PPIQ_REALIZATION_T018_READONLY_PREVIEW_ROLE
-- Least-privilege read-only role for query/widget/preview surfaces.

DO $$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plantprocess_readonly_preview') THEN
        ALTER ROLE plantprocess_readonly_preview NOLOGIN PASSWORD NULL;
    ELSE
        CREATE ROLE plantprocess_readonly_preview NOLOGIN;
    END IF;
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO plantprocess_readonly_preview', current_database());
END $$;

GRANT USAGE ON SCHEMA public TO plantprocess_readonly_preview;
GRANT SELECT ON ALL TABLES IN SCHEMA public TO plantprocess_readonly_preview;
GRANT SELECT ON ALL SEQUENCES IN SCHEMA public TO plantprocess_readonly_preview;

ALTER DEFAULT PRIVILEGES IN SCHEMA public
GRANT SELECT ON TABLES TO plantprocess_readonly_preview;

REVOKE CREATE ON SCHEMA public FROM plantprocess_readonly_preview;

CREATE OR REPLACE FUNCTION public.ppiq_validate_readonly_preview_role()
RETURNS TABLE
(
    gate_code text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT
        'PPIQ-T018-READONLY-PREVIEW-ROLE',
        EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plantprocess_readonly_preview'),
        'Read-only preview role exists; deploy validation must confirm denied write operations.';
$$;
