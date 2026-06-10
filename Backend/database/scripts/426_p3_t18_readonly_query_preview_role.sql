-- ============================================================================
-- PlantProcess IQ — P3-T18 Least-Privilege Read-Only DB Role
-- Marker: PPIQ_P3_T18_READONLY_QUERY_PREVIEW_ROLE
-- ============================================================================

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ppiq_query_preview_readonly') THEN
        CREATE ROLE ppiq_query_preview_readonly
            NOLOGIN
            NOSUPERUSER
            NOCREATEDB
            NOCREATEROLE
            NOINHERIT
            NOREPLICATION
            NOBYPASSRLS;
    END IF;

    ALTER ROLE ppiq_query_preview_readonly
        NOSUPERUSER
        NOCREATEDB
        NOCREATEROLE
        NOINHERIT
        NOREPLICATION
        NOBYPASSRLS;
END $$;

DO $$
DECLARE
    preview_password text := current_setting('ppiq.query_preview_password', true);
BEGIN
    IF preview_password IS NOT NULL AND length(preview_password) >= 64 THEN
        IF NOT EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'ppiq_query_preview_login') THEN
            EXECUTE format(
                'CREATE ROLE ppiq_query_preview_login LOGIN PASSWORD %L IN ROLE ppiq_query_preview_readonly NOSUPERUSER NOCREATEDB NOCREATEROLE NOREPLICATION NOBYPASSRLS',
                preview_password
            );
        ELSE
            EXECUTE format('ALTER ROLE ppiq_query_preview_login PASSWORD %L', preview_password);
            ALTER ROLE ppiq_query_preview_login
                LOGIN
                NOSUPERUSER
                NOCREATEDB
                NOCREATEROLE
                NOREPLICATION
                NOBYPASSRLS;
        END IF;
    ELSE
        RAISE NOTICE 'ppiq.query_preview_password not supplied or shorter than 64 chars. Login role not created/updated.';
    END IF;
END $$;

REVOKE ALL ON SCHEMA public FROM ppiq_query_preview_readonly;
GRANT USAGE ON SCHEMA public TO ppiq_query_preview_readonly;

DO $$
DECLARE
    r record;
BEGIN
    FOR r IN
        SELECT n.nspname AS schema_name, c.relname AS relation_name
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relkind IN ('v', 'm')
          AND (
              c.relname LIKE 'ppiq_preview_%'
              OR c.relname LIKE 'ppiq_read_%'
              OR c.relname LIKE 'v_%'
              OR c.relname LIKE 'vw_%'
          )
    LOOP
        EXECUTE format(
            'GRANT SELECT ON %I.%I TO ppiq_query_preview_readonly',
            r.schema_name,
            r.relation_name
        );
    END LOOP;
END $$;

DO $$
DECLARE
    r record;
BEGIN
    FOR r IN
        SELECT n.nspname AS schema_name, c.relname AS relation_name
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relkind = 'r'
    LOOP
        EXECUTE format(
            'REVOKE INSERT, UPDATE, DELETE, TRUNCATE, REFERENCES, TRIGGER ON %I.%I FROM ppiq_query_preview_readonly',
            r.schema_name,
            r.relation_name
        );
    END LOOP;
END $$;

CREATE OR REPLACE FUNCTION public.ppiq_p3_t18_readonly_preview_role_status()
RETURNS TABLE
(
    gate_code text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    WITH role_row AS
    (
        SELECT *
        FROM pg_roles
        WHERE rolname = 'ppiq_query_preview_readonly'
    ),
    base_tables AS
    (
        SELECT n.nspname, c.relname
        FROM pg_class c
        JOIN pg_namespace n ON n.oid = c.relnamespace
        WHERE n.nspname = 'public'
          AND c.relkind = 'r'
    ),
    dangerous_table_privileges AS
    (
        SELECT nspname, relname, privilege
        FROM base_tables
        CROSS JOIN LATERAL
        (
            VALUES
                ('INSERT'),
                ('UPDATE'),
                ('DELETE'),
                ('TRUNCATE'),
                ('REFERENCES'),
                ('TRIGGER')
        ) p(privilege)
        WHERE has_table_privilege('ppiq_query_preview_readonly', format('%I.%I', nspname, relname), p.privilege)
    ),
    broad_base_table_select AS
    (
        SELECT nspname, relname
        FROM base_tables
        WHERE has_table_privilege('ppiq_query_preview_readonly', format('%I.%I', nspname, relname), 'SELECT')
    )
    SELECT
        'ReadonlyRoleExists',
        EXISTS (SELECT 1 FROM role_row),
        'ppiq_query_preview_readonly role exists.'
    UNION ALL
    SELECT
        'NoAdminAttributes',
        COALESCE(
            (
                SELECT NOT rolsuper
                   AND NOT rolcreatedb
                   AND NOT rolcreaterole
                   AND NOT rolreplication
                   AND NOT rolbypassrls
                FROM role_row
            ),
            false
        ),
        'Role is not superuser, createdb, createrole, replication, or bypassrls.'
    UNION ALL
    SELECT
        'SchemaUsageOnly',
        has_schema_privilege('ppiq_query_preview_readonly', 'public', 'USAGE'),
        'Role has USAGE on public schema for approved read models.'
    UNION ALL
    SELECT
        'NoWritePrivilegesOnBaseTables',
        NOT EXISTS (SELECT 1 FROM dangerous_table_privileges),
        'Role has no INSERT/UPDATE/DELETE/TRUNCATE/REFERENCES/TRIGGER privilege on public base tables.'
    UNION ALL
    SELECT
        'NoBroadBaseTableSelect',
        NOT EXISTS (SELECT 1 FROM broad_base_table_select),
        'Role has no broad SELECT privilege on ordinary public base tables.'
    UNION ALL
    SELECT
        'OptionalLoginIsSafeWhenPresent',
        NOT EXISTS
        (
            SELECT 1
            FROM pg_roles
            WHERE rolname = 'ppiq_query_preview_login'
              AND (rolsuper OR rolcreatedb OR rolcreaterole OR rolreplication OR rolbypassrls)
        ),
        'Optional login role has no dangerous admin attributes when present.';
$$;