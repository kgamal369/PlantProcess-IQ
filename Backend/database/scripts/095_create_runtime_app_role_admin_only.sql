\set ON_ERROR_STOP on

\if :{?plantprocess_app_password}
\else
\echo 'Required psql variable is missing: plantprocess_app_password'
\echo 'Usage: psql -v plantprocess_app_password=<password> -f 095_create_runtime_app_role_admin_only.sql'
\quit 1
\endif
-- ============================================================================
-- PlantProcess IQ
-- File: Backend/database/scripts/095_create_runtime_app_role_admin_only.sql
--
-- Purpose:
--   Create (or align) the restricted, non-superuser runtime application role.
--   psql variable substitution does NOT occur inside dollar-quoted ($$) blocks,
--   so the password is interpolated in plain SELECTs and the resulting DDL is
--   run via \gexec. Idempotent and install-portable.
--
-- Run as: a role with CREATEROLE (postgres / DBA / local superuser ppiq_dev).
-- ============================================================================

-- Create the role only if it does not already exist.
SELECT format(
    'CREATE ROLE plantprocess_app LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS INHERIT',
    :'plantprocess_app_password')
WHERE NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plantprocess_app')
\gexec

-- If it already exists, align its password and attributes (no privilege drift).
SELECT format(
    'ALTER ROLE plantprocess_app WITH LOGIN PASSWORD %L NOSUPERUSER NOCREATEDB NOCREATEROLE NOBYPASSRLS INHERIT',
    :'plantprocess_app_password')
WHERE EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plantprocess_app')
\gexec

-- Minimal grants here; table/sequence privileges are applied by the late grant script.
DO $$
BEGIN
    EXECUTE format('GRANT CONNECT ON DATABASE %I TO plantprocess_app', current_database());
END $$;

GRANT USAGE ON SCHEMA public TO plantprocess_app;

SELECT 'plantprocess_app runtime role created or verified' AS status,
       current_database() AS database_name,
       current_user AS executed_by,
       EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plantprocess_app') AS runtime_role_exists;