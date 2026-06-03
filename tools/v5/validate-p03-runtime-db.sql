\encoding UTF8
SET client_encoding = 'UTF8';

DO $$
BEGIN
    IF to_regclass('public.ppiq_telemetry_points') IS NULL
       AND to_regclass('public.time_series_observations') IS NULL
       AND to_regclass('public.parameter_observations') IS NULL THEN
        RAISE EXCEPTION 'No telemetry/time-series table found. P03 runtime DB guard failed.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM pg_proc
        WHERE proname ILIKE '%telemetry%'
           OR proname ILIKE '%timeseries%'
           OR proname ILIKE '%time_series%'
    ) THEN
        RAISE NOTICE 'No telemetry procedure found. This may be OK if ingestion is worker/API based.';
    END IF;

    IF EXISTS (SELECT 1 FROM pg_extension WHERE extname = 'timescaledb') THEN
        RAISE NOTICE 'TimescaleDB extension installed.';
    ELSE
        RAISE NOTICE 'TimescaleDB extension not installed. P03 is running on plain PostgreSQL compatibility mode.';
    END IF;
END $$;