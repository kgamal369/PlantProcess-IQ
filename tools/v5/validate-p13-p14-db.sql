\encoding UTF8
SET client_encoding = 'UTF8';

SELECT set_config('app.current_tenant', '00000000-0000-0000-0000-000000000001', false);

SELECT 'P13 acceptance' AS gate, * FROM public.ppiq_v5_p13_acceptance();
SELECT 'P14 acceptance' AS gate, * FROM public.ppiq_v5_p14_acceptance();

DO $$
DECLARE
    bad_count integer;
BEGIN
    PERFORM set_config('app.current_tenant', '00000000-0000-0000-0000-000000000001', false);

    SELECT count(*) INTO bad_count
    FROM public.ppiq_v5_p13_acceptance()
    WHERE is_green = false;

    IF bad_count > 0 THEN
        RAISE EXCEPTION 'P13 DB acceptance has % failing checks.', bad_count;
    END IF;

    SELECT count(*) INTO bad_count
    FROM public.ppiq_v5_p14_acceptance()
    WHERE is_green = false;

    IF bad_count > 0 THEN
        RAISE EXCEPTION 'P14 DB acceptance has % failing checks.', bad_count;
    END IF;
END $$;