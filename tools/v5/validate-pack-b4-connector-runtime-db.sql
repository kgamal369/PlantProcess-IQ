\encoding UTF8
SET client_encoding = 'UTF8';

SELECT set_config('app.current_tenant', '00000000-0000-0000-0000-000000000001', false);

SELECT 'P07 runtime acceptance' AS gate, * FROM public.ppiq_v5_p07_runtime_acceptance();

DO $$
DECLARE
    bad_count integer;
    reading_count integer;
BEGIN
    PERFORM set_config('app.current_tenant', '00000000-0000-0000-0000-000000000001', false);

    SELECT count(*) INTO bad_count
    FROM public.ppiq_v5_p07_runtime_acceptance()
    WHERE is_green = false;

    IF bad_count > 0 THEN
        RAISE EXCEPTION 'P07 runtime DB acceptance has % failing checks.', bad_count;
    END IF;

    SELECT count(*) INTO reading_count
    FROM public.ppiq_connector_runtime_readings
    WHERE provider_code = 'mock-historian-runtime';

    IF reading_count < 10 THEN
        RAISE EXCEPTION 'Expected seeded mock historian readings, got %.', reading_count;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM public.ppiq_connector_runtime_sources
        WHERE provider_code = 'mock-historian-runtime'
          AND is_read_only = false
    ) THEN
        RAISE EXCEPTION 'Read-only enforcement failed: mock historian source is not read-only.';
    END IF;
END $$;