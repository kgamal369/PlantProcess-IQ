-- 302_p02_authstore_runtime_lineage_lock.sql
--
-- PPIQ_REALIZATION_T009_AUTHSTORE_USER_TABLE_LINEAGE
--
-- Runtime decision:
-- Current AuthStore.cs uses the legacy runtime auth spine:
--   tenants
--   app_users
--   auth_refresh_tokens
--
-- Therefore auth_refresh_tokens.user_id must reference app_users(id),
-- not ppiq_auth_users(id). The newer ppiq_* security spine may coexist,
-- but it must not own AuthStore refresh-token runtime lineage until AuthStore
-- is migrated completely.

SET client_min_messages TO WARNING;

DO $$
BEGIN
    IF to_regclass('public.tenants') IS NULL THEN
        RAISE EXCEPTION 'T-009 failed: public.tenants does not exist. Apply the AuthStore compatibility bridge first.';
    END IF;

    IF to_regclass('public.app_users') IS NULL THEN
        RAISE EXCEPTION 'T-009 failed: public.app_users does not exist. Apply the AuthStore compatibility bridge first.';
    END IF;

    IF to_regclass('public.auth_refresh_tokens') IS NULL THEN
        RAISE EXCEPTION 'T-009 failed: public.auth_refresh_tokens does not exist. Apply the AuthStore compatibility bridge first.';
    END IF;
END $$;

BEGIN;

-- Remove broken local/test tokens before FK rewrite.
DELETE FROM public.auth_refresh_tokens rt
WHERE NOT EXISTS (SELECT 1 FROM public.app_users u WHERE u.id = rt.user_id)
   OR NOT EXISTS (SELECT 1 FROM public.tenants t WHERE t.id = rt.tenant_id);

-- Drop existing FK constraints on user_id / tenant_id, regardless of old target.
DO $$
DECLARE
    r record;
BEGIN
    FOR r IN
        SELECT DISTINCT con.conname
        FROM pg_constraint con
        JOIN unnest(con.conkey) AS k(attnum) ON true
        JOIN pg_attribute a
          ON a.attrelid = con.conrelid
         AND a.attnum = k.attnum
        WHERE con.conrelid = 'public.auth_refresh_tokens'::regclass
          AND con.contype = 'f'
          AND a.attname IN ('tenant_id', 'user_id')
    LOOP
        EXECUTE format('ALTER TABLE public.auth_refresh_tokens DROP CONSTRAINT IF EXISTS %I', r.conname);
    END LOOP;
END $$;

ALTER TABLE public.auth_refresh_tokens
    ADD CONSTRAINT auth_refresh_tokens_tenant_id_fkey
    FOREIGN KEY (tenant_id)
    REFERENCES public.tenants(id)
    ON DELETE CASCADE;

ALTER TABLE public.auth_refresh_tokens
    ADD CONSTRAINT auth_refresh_tokens_user_id_fkey
    FOREIGN KEY (user_id)
    REFERENCES public.app_users(id)
    ON DELETE CASCADE;

COMMIT;

CREATE OR REPLACE FUNCTION public.ppiq_assert_authstore_runtime_lineage()
RETURNS TABLE
(
    check_code text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT
        'auth_refresh_tokens_user_fk',
        EXISTS (
            SELECT 1
            FROM pg_constraint con
            JOIN pg_class src ON src.oid = con.conrelid
            JOIN pg_class tgt ON tgt.oid = con.confrelid
            JOIN unnest(con.conkey) AS k(attnum) ON true
            JOIN pg_attribute a
              ON a.attrelid = src.oid
             AND a.attnum = k.attnum
            WHERE con.contype = 'f'
              AND src.relname = 'auth_refresh_tokens'
              AND a.attname = 'user_id'
              AND tgt.relname = 'app_users'
        ),
        'auth_refresh_tokens.user_id references app_users.id'
    UNION ALL
    SELECT
        'auth_refresh_tokens_tenant_fk',
        EXISTS (
            SELECT 1
            FROM pg_constraint con
            JOIN pg_class src ON src.oid = con.conrelid
            JOIN pg_class tgt ON tgt.oid = con.confrelid
            JOIN unnest(con.conkey) AS k(attnum) ON true
            JOIN pg_attribute a
              ON a.attrelid = src.oid
             AND a.attnum = k.attnum
            WHERE con.contype = 'f'
              AND src.relname = 'auth_refresh_tokens'
              AND a.attname = 'tenant_id'
              AND tgt.relname = 'tenants'
        ),
        'auth_refresh_tokens.tenant_id references tenants.id';
$$;