-- Canonical default-tenant identity repair (idempotent, runs every migrate).
-- ppiq_tenants.id defaults to gen_random_uuid(), and 300/010 insert the
-- "default" tenant without an explicit id, so it never carries the well-known
-- id 00000000-0000-0000-0000-000000000001 that the canonical views, RLS,
-- licenses and integration tests are all keyed to. This re-ids the default
-- tenant to the canonical id and repoints every FK child generically.
DO $repair$
DECLARE
    v_new uuid := '00000000-0000-0000-0000-000000000001';
    v_old uuid;
    r record;
BEGIN
    IF to_regclass('public.ppiq_tenants') IS NULL THEN
        RETURN;
    END IF;

    SELECT id INTO v_old
    FROM public.ppiq_tenants
    WHERE lower(tenant_code) = 'default' AND id <> v_new
    LIMIT 1;

    IF v_old IS NULL THEN
        RETURN;  -- already canonical, or no default tenant present
    END IF;

    -- Insert the canonical-id row under a temporary code to avoid colliding
    -- with the existing default on the lower(tenant_code) unique index.
    INSERT INTO public.ppiq_tenants
        (id, tenant_code, display_name, license_tier, is_active, created_at_utc, updated_at_utc)
    SELECT v_new, 'default__canonical_migrating', display_name, license_tier, is_active, created_at_utc, now()
    FROM public.ppiq_tenants
    WHERE id = v_old
    ON CONFLICT (id) DO NOTHING;

    -- Repoint every foreign key that references public.ppiq_tenants(id).
    FOR r IN
        SELECT con.conrelid::regclass AS child_tbl, att.attname AS child_col
        FROM pg_constraint con
        JOIN pg_attribute att
          ON att.attrelid = con.conrelid AND att.attnum = ANY (con.conkey)
        WHERE con.contype = 'f'
          AND con.confrelid = 'public.ppiq_tenants'::regclass
    LOOP
        EXECUTE format('UPDATE %s SET %I = $1 WHERE %I = $2', r.child_tbl, r.child_col, r.child_col)
        USING v_new, v_old;
    END LOOP;

    DELETE FROM public.ppiq_tenants WHERE id = v_old;
    UPDATE public.ppiq_tenants SET tenant_code = 'default' WHERE id = v_new;
END
$repair$;