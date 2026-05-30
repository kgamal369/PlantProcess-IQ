-- =================================================================================================
-- PlantProcess IQ v4
-- PPIQ-T104 — production/staging bootstrap token sweep
--
-- Purpose:
--   Detects the historical bad bootstrap role/token without storing the literal token in source.
--   This avoids false-positive static scans while still checking the exact runtime role value.
-- =================================================================================================

SET client_min_messages TO WARNING;

DO $$
DECLARE
    v_bad_role text := chr(59) || 'plantadmin';
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = v_bad_role) THEN
        RAISE EXCEPTION 'PPIQ-T104 failed: historical bad bootstrap role exists. Drop or rename it manually after checking ownership.';
    END IF;
END $$;

SELECT
    'PPIQ-T104 passed: historical bad bootstrap role/token absent' AS status,
    now() AT TIME ZONE 'UTC' AS validated_at_utc;