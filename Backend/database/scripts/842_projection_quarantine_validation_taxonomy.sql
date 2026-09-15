\set ON_ERROR_STOP on

-- Completes the projection quarantine taxonomy without creating a second
-- quarantine table or changing T-099 evidence lineage.
DO $$
DECLARE
    v_codes text[];
BEGIN
    IF to_regclass('ppiq_staging.projection_quarantine') IS NULL THEN
        RAISE EXCEPTION
            'PPIQ_842: ppiq_staging.projection_quarantine is missing.';
    END IF;

    ALTER TABLE ppiq_staging.projection_quarantine
        DROP CONSTRAINT IF EXISTS ck_projection_quarantine_code;

    ALTER TABLE ppiq_staging.projection_quarantine
        ADD CONSTRAINT ck_projection_quarantine_code
        CHECK (
            validation_code IN (
                'PV01','PV02','PV03','PV04','PV05',
                'PV06','PV07','PV08','PV09','PV10',
                'PV11','PV12','PV13','PV14','PV15'
            )
        );

    SELECT array_agg(m[1] ORDER BY m[1])
      INTO v_codes
      FROM pg_constraint c
      JOIN pg_class t ON t.oid = c.conrelid
      JOIN pg_namespace n ON n.oid = t.relnamespace
      CROSS JOIN LATERAL
        regexp_matches(
            pg_get_constraintdef(c.oid),
            '(PV[0-9]{2})',
            'g'
        ) AS m
     WHERE n.nspname = 'ppiq_staging'
       AND t.relname = 'projection_quarantine'
       AND c.conname = 'ck_projection_quarantine_code';

    IF v_codes IS DISTINCT FROM ARRAY[
        'PV01','PV02','PV03','PV04','PV05',
        'PV06','PV07','PV08','PV09','PV10',
        'PV11','PV12','PV13','PV14','PV15'
    ]::text[] THEN
        RAISE EXCEPTION
            'PPIQ_842: taxonomy is not exactly PV01..PV15: %',
            v_codes;
    END IF;

    RAISE NOTICE
        'PPIQ_842_PROVEN: validation_code is exactly PV01..PV15.';
END $$;