\encoding UTF8
SET client_encoding = 'UTF8';

SELECT set_config('app.current_tenant', '00000000-0000-0000-0000-000000000001', false);

SELECT 'P09 runtime acceptance' AS gate, * FROM public.ppiq_v5_p09_runtime_acceptance();

DO $$
DECLARE
    bad_count integer;
BEGIN
    PERFORM set_config('app.current_tenant', '00000000-0000-0000-0000-000000000001', false);

    SELECT count(*) INTO bad_count
    FROM public.ppiq_v5_p09_runtime_acceptance()
    WHERE is_green = false;

    IF bad_count > 0 THEN
        RAISE EXCEPTION 'P09 runtime DB acceptance has % failing checks.', bad_count;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM public.ppiq_oidc_runtime_jwks_keys
        WHERE tenant_id = '00000000-0000-0000-0000-000000000001'
          AND provider_code = 'runtime-keycloak'
          AND algorithm = 'RS256'
          AND status = 'active'
    ) THEN
        RAISE EXCEPTION 'Runtime JWKS key not registered.';
    END IF;
END $$;