\encoding UTF8
SET client_encoding = 'UTF8';

SELECT set_config('app.current_tenant', '00000000-0000-0000-0000-000000000001', false);

DO $$
DECLARE
    active_count integer;
    current_tier text;
BEGIN
    SELECT count(*) INTO active_count
    FROM public.ppiq_v_ed25519_current_entitlements
    WHERE tenant_id = '00000000-0000-0000-0000-000000000001'
      AND verification_status = 'valid'
      AND activation_status = 'active'
      AND expires_at_utc > now();

    IF active_count < 1 THEN
        RAISE EXCEPTION 'No active valid Ed25519 license exists for tenant %. Run Pack B1/B2 activation first.', '00000000-0000-0000-0000-000000000001';
    END IF;

    SELECT tier INTO current_tier
    FROM public.ppiq_v_ed25519_current_entitlements
    WHERE tenant_id = '00000000-0000-0000-0000-000000000001'
    LIMIT 1;

    IF current_tier IS NULL THEN
        RAISE EXCEPTION 'Current Ed25519 tier is NULL.';
    END IF;

    IF current_tier NOT IN ('Light','Pro','ProPlus','Pro Plus','Enterprise') THEN
        RAISE EXCEPTION 'Unexpected verified license tier: %', current_tier;
    END IF;
END $$;

SELECT
    'Pack B2 verified entitlement source' AS check_name,
    true AS is_green,
    'public.ppiq_v_ed25519_current_entitlements has an active valid license.' AS evidence;