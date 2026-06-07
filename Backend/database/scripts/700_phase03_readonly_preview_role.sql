-- PPIQ_REALIZATION_T018_READONLY_PREVIEW_ROLE
-- Least-privilege read-only role for query/widget/preview surfaces.

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plantprocess_readonly_preview') THEN
        CREATE ROLE plantprocess_readonly_preview LOGIN PASSWORD 'CHANGE_ME_READONLY_PREVIEW_PASSWORD';
    END IF;
END $$;

GRANT CONNECT ON DATABASE plantprocessiq TO plantprocess_readonly_preview;
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
