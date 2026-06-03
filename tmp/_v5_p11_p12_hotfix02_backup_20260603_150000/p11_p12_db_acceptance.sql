\encoding UTF8
SET client_encoding = 'UTF8';

SELECT 'P11 acceptance' AS gate, * FROM public.ppiq_v5_p11_acceptance();
SELECT 'P12 acceptance' AS gate, * FROM public.ppiq_v5_p12_acceptance();

DO $$
DECLARE
    bad_count integer;
BEGIN
    SELECT count(*) INTO bad_count
    FROM public.ppiq_v5_p11_acceptance()
    WHERE is_green = false;

    IF bad_count > 0 THEN
        RAISE EXCEPTION 'P11 DB acceptance has % failing checks.', bad_count;
    END IF;

    SELECT count(*) INTO bad_count
    FROM public.ppiq_v5_p12_acceptance()
    WHERE is_green = false;

    IF bad_count > 0 THEN
        RAISE EXCEPTION 'P12 DB acceptance has % failing checks.', bad_count;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM public.ppiq_i18n_locales
        WHERE locale_code = 'ar'
          AND direction = 'rtl'
    ) THEN
        RAISE EXCEPTION 'Arabic RTL locale was not installed.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM public.ppiq_i18n_translations
        WHERE locale_code = 'ar'
          AND namespace = 'v5.p11p12'
    ) THEN
        RAISE EXCEPTION 'Arabic translations were not installed.';
    END IF;
END $$;