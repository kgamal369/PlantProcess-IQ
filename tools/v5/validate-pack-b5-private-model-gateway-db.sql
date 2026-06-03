\encoding UTF8
SET client_encoding = 'UTF8';

SELECT set_config('app.current_tenant', '00000000-0000-0000-0000-000000000001', false);

SELECT 'P04 private model acceptance' AS gate, * FROM public.ppiq_v5_p04_private_model_acceptance();

DO $$
DECLARE
    bad_count integer;
BEGIN
    PERFORM set_config('app.current_tenant', '00000000-0000-0000-0000-000000000001', false);

    SELECT count(*) INTO bad_count
    FROM public.ppiq_v5_p04_private_model_acceptance()
    WHERE is_green = false;

    IF bad_count > 0 THEN
        RAISE EXCEPTION 'P04 private model DB acceptance has % failing checks.', bad_count;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM public.ppiq_private_model_endpoint_configs
        WHERE public_network_allowed = true
    ) THEN
        RAISE EXCEPTION 'Public AI/network endpoint is incorrectly allowed.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM public.ppiq_model_eval_golden_cases
        WHERE case_code = 'public-endpoint-rejected'
          AND expected_status = 'refuse'
    ) THEN
        RAISE EXCEPTION 'Public endpoint rejection golden case missing.';
    END IF;
END $$;