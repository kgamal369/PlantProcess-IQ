\set ON_ERROR_STOP on

-- ============================================================================
-- 302_p01_p02_authstore_lineage_lock.sql
-- Deploy-safe and idempotent. Locks the AuthStore refresh-token FK lineage to
-- the runtime tables AuthStore.cs actually uses (public.app_users + public.tenants)
-- and self-heals drift (e.g. a user_id FK repointed to ppiq_auth_users).
--
-- Contains NO credentials and NO seed rows, so it is safe on prod/demo and does
-- not trip the no-demo-tenant-fallback / devseed-production-artifact gates.
--
-- On a healthy database (FK already correct) this is a pure no-op: it does not
-- delete any token and does not drop/recreate any constraint.
-- ============================================================================
SET client_min_messages TO WARNING;

DO $$
DECLARE
    user_fk_target   text;
    tenant_fk_target text;
    needs_fix        boolean := false;
    r                record;
BEGIN
    IF to_regclass('public.auth_refresh_tokens') IS NULL
       OR to_regclass('public.app_users') IS NULL
       OR to_regclass('public.tenants') IS NULL THEN
        RAISE NOTICE '302 lineage-lock: auth tables not present yet; skipping (ordering).';
        RETURN;
    END IF;

    SELECT tgt.relname INTO user_fk_target
    FROM pg_constraint con
    JOIN pg_class src ON src.oid = con.conrelid
    JOIN pg_class tgt ON tgt.oid = con.confrelid
    JOIN unnest(con.conkey) AS k(attnum) ON true
    JOIN pg_attribute a ON a.attrelid = src.oid AND a.attnum = k.attnum
    WHERE con.contype = 'f' AND src.relname = 'auth_refresh_tokens' AND a.attname = 'user_id'
    LIMIT 1;

    SELECT tgt.relname INTO tenant_fk_target
    FROM pg_constraint con
    JOIN pg_class src ON src.oid = con.conrelid
    JOIN pg_class tgt ON tgt.oid = con.confrelid
    JOIN unnest(con.conkey) AS k(attnum) ON true
    JOIN pg_attribute a ON a.attrelid = src.oid AND a.attnum = k.attnum
    WHERE con.contype = 'f' AND src.relname = 'auth_refresh_tokens' AND a.attname = 'tenant_id'
    LIMIT 1;

    IF user_fk_target   IS DISTINCT FROM 'app_users' THEN needs_fix := true; END IF;
    IF tenant_fk_target IS DISTINCT FROM 'tenants'   THEN needs_fix := true; END IF;

    IF NOT needs_fix THEN
        RAISE NOTICE '302 lineage-lock: FK lineage already correct (app_users / tenants); no-op.';
        RETURN;
    END IF;

    RAISE NOTICE '302 lineage-lock: correcting drift (user_id -> %, tenant_id -> %).',
        COALESCE(user_fk_target, '<none>'), COALESCE(tenant_fk_target, '<none>');

    -- Remove only refresh tokens that would violate the corrected FK (stale sessions).
    DELETE FROM public.auth_refresh_tokens rt
    WHERE NOT EXISTS (SELECT 1 FROM public.app_users u WHERE u.id = rt.user_id)
       OR NOT EXISTS (SELECT 1 FROM public.tenants   t WHERE t.id = rt.tenant_id);

    FOR r IN
        SELECT DISTINCT con.conname
        FROM pg_constraint con
        JOIN unnest(con.conkey) AS k(attnum) ON true
        JOIN pg_attribute a ON a.attrelid = con.conrelid AND a.attnum = k.attnum
        WHERE con.conrelid = 'public.auth_refresh_tokens'::regclass
          AND con.contype = 'f'
          AND a.attname IN ('tenant_id', 'user_id')
    LOOP
        EXECUTE format('ALTER TABLE public.auth_refresh_tokens DROP CONSTRAINT IF EXISTS %I', r.conname);
    END LOOP;

    ALTER TABLE public.auth_refresh_tokens
        ADD CONSTRAINT auth_refresh_tokens_tenant_id_fkey
        FOREIGN KEY (tenant_id) REFERENCES public.tenants(id) ON DELETE CASCADE;

    ALTER TABLE public.auth_refresh_tokens
        ADD CONSTRAINT auth_refresh_tokens_user_id_fkey
        FOREIGN KEY (user_id) REFERENCES public.app_users(id) ON DELETE CASCADE;
END $$;

-- Self-verify (fail the deploy loudly rather than pretend to pass).
DO $$
DECLARE t text;
BEGIN
    IF to_regclass('public.auth_refresh_tokens') IS NULL THEN RETURN; END IF;
    SELECT tgt.relname INTO t
    FROM pg_constraint con
    JOIN pg_class src ON src.oid = con.conrelid
    JOIN pg_class tgt ON tgt.oid = con.confrelid
    JOIN unnest(con.conkey) AS k(attnum) ON true
    JOIN pg_attribute a ON a.attrelid = src.oid AND a.attnum = k.attnum
    WHERE con.contype = 'f' AND src.relname = 'auth_refresh_tokens' AND a.attname = 'user_id'
    LIMIT 1;
    IF t IS DISTINCT FROM 'app_users' THEN
        RAISE EXCEPTION '302 lineage-lock FAILED: auth_refresh_tokens.user_id FK targets %, expected app_users', COALESCE(t, '<none>');
    END IF;
END $$;

SELECT '302_authstore_lineage_lock_ok' AS status;