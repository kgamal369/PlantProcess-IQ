-- ============================================================================
-- PlantProcess IQ
-- File: Backend/database/scripts/095_create_runtime_app_role_admin_only.sql
--
-- Purpose:
--   One-time creation of the restricted runtime application role.
--
-- Run as:
--   postgres / database admin / any role with CREATEROLE.
--
-- Do NOT run as:
--   plantprocess, unless plantprocess has CREATEROLE.
-- ============================================================================

DO $$
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM pg_roles
        WHERE rolname = 'plantprocess_app'
    ) THEN
        CREATE ROLE plantprocess_app
            LOGIN
            PASSWORD 'CHANGE_ME_STRONG_APP_PASSWORD'
            NOSUPERUSER
            NOCREATEDB
            NOCREATEROLE
            INHERIT;
    ELSE
        RAISE NOTICE 'Role plantprocess_app already exists. Skipping CREATE ROLE.';
    END IF;
END $$;

DO $$
DECLARE
    database_name text;
BEGIN
    SELECT current_database() INTO database_name;

    EXECUTE format(
        'GRANT CONNECT ON DATABASE %I TO plantprocess_app',
        database_name
    );
END $$;

GRANT USAGE ON SCHEMA public TO plantprocess_app;

SELECT
    'plantprocess_app runtime role created or verified' AS status,
    current_database() AS database_name,
    current_user AS executed_by,
    EXISTS (
        SELECT 1
        FROM pg_roles
        WHERE rolname = 'plantprocess_app'
    ) AS runtime_role_exists;