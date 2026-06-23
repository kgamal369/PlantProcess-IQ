-- ============================================================================
-- PlantProcess IQ - runtime application role privileges (least privilege)
-- Runs LAST so every model + decoration table exists. Idempotent. No-op when
-- the role is absent (an install that did not provision plantprocess_app).
-- Grants the non-superuser, NOBYPASSRLS role exactly what the app needs while
-- RLS still constrains it and audit_log_entries stays append-only.
-- ============================================================================
\set ON_ERROR_STOP on

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plantprocess_app') THEN
        RAISE NOTICE 'plantprocess_app absent - skipping runtime grants.';
        RETURN;
    END IF;

    EXECUTE format('GRANT CONNECT ON DATABASE %I TO plantprocess_app', current_database());

    GRANT USAGE, CREATE ON SCHEMA public TO plantprocess_app;
    GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA public TO plantprocess_app;
    GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA public TO plantprocess_app;
    ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT SELECT, INSERT, UPDATE, DELETE ON TABLES TO plantprocess_app;
    ALTER DEFAULT PRIVILEGES IN SCHEMA public GRANT USAGE, SELECT ON SEQUENCES TO plantprocess_app;

    IF EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'ppiq_meta') THEN
        GRANT USAGE ON SCHEMA ppiq_meta TO plantprocess_app;
        GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA ppiq_meta TO plantprocess_app;
        GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA ppiq_meta TO plantprocess_app;
    END IF;
    IF EXISTS (SELECT 1 FROM information_schema.schemata WHERE schema_name = 'ppiq_plant') THEN
        GRANT USAGE ON SCHEMA ppiq_plant TO plantprocess_app;
        GRANT SELECT, INSERT, UPDATE, DELETE ON ALL TABLES IN SCHEMA ppiq_plant TO plantprocess_app;
        GRANT USAGE, SELECT ON ALL SEQUENCES IN SCHEMA ppiq_plant TO plantprocess_app;
    END IF;

    -- audit_log_entries is append-only: the app may INSERT and SELECT only.
    IF EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema = 'public' AND table_name = 'audit_log_entries') THEN
        GRANT SELECT, INSERT ON public.audit_log_entries TO plantprocess_app;
        REVOKE UPDATE, DELETE, TRUNCATE ON public.audit_log_entries FROM plantprocess_app;
    END IF;
END $$;

SELECT 'runtime app role privileges applied' AS status,
       current_database() AS database_name,
       EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plantprocess_app'
               AND NOT rolsuper AND NOT rolbypassrls) AS role_is_least_privilege;