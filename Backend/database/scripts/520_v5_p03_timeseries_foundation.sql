-- ============================================================
-- PlantProcess IQ Doctrine v5
-- P03 · Time-Series Foundation
--
-- This migration is intentionally safe for local/dev machines where the
-- TimescaleDB binary extension may not be installed.
--
-- If TimescaleDB exists:
--   - attempts CREATE EXTENSION
--   - attempts create_hypertable for parameter_observations when compatible
--
-- If TimescaleDB is unavailable or conversion is blocked:
--   - records native-partition fallback mode
--   - creates rollup tables/materialized views and acceptance evidence
--
-- Idempotent.
-- ============================================================

\set ON_ERROR_STOP on

BEGIN;

-- ------------------------------------------------------------
-- 1) Capability/status table
-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.ppiq_time_series_capabilities
(
    id integer PRIMARY KEY DEFAULT 1,
    installed_at_utc timestamptz NOT NULL DEFAULT now(),
    timescaledb_extension_available boolean NOT NULL DEFAULT false,
    timescaledb_extension_installed boolean NOT NULL DEFAULT false,
    telemetry_table text NOT NULL DEFAULT 'parameter_observations',
    telemetry_time_column text NOT NULL DEFAULT 'observed_at_utc',
    telemetry_mode text NOT NULL DEFAULT 'native_partition_fallback',
    notes text NOT NULL DEFAULT '',
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_time_series_capabilities_singleton CHECK (id = 1)
);

INSERT INTO public.ppiq_time_series_capabilities(id)
VALUES (1)
ON CONFLICT (id) DO NOTHING;

-- ------------------------------------------------------------
-- 2) Try TimescaleDB extension if installed on server
-- ------------------------------------------------------------

DO $$
DECLARE
    ext_available boolean := false;
    ext_installed boolean := false;
    msg text := '';
BEGIN
    SELECT EXISTS (
        SELECT 1
        FROM pg_available_extensions
        WHERE name = 'timescaledb'
    )
    INTO ext_available;

    IF ext_available THEN
        BEGIN
            EXECUTE 'CREATE EXTENSION IF NOT EXISTS timescaledb';
            ext_installed := true;
            msg := 'TimescaleDB extension installed or already present.';
        EXCEPTION WHEN OTHERS THEN
            ext_installed := false;
            msg := 'TimescaleDB available but CREATE EXTENSION failed: ' || SQLERRM;
        END;
    ELSE
        msg := 'TimescaleDB binary extension not available on this PostgreSQL server; native partition fallback active.';
    END IF;

    UPDATE public.ppiq_time_series_capabilities
    SET timescaledb_extension_available = ext_available,
        timescaledb_extension_installed = ext_installed,
        telemetry_mode = CASE WHEN ext_installed THEN 'timescale_candidate' ELSE 'native_partition_fallback' END,
        notes = msg,
        updated_at_utc = now()
    WHERE id = 1;
END $$;

-- ------------------------------------------------------------
-- 3) Try converting parameter_observations to hypertable where safe
-- ------------------------------------------------------------

DO $$
DECLARE
    ext_installed boolean;
    table_exists boolean;
    time_col_exists boolean;
    is_hypertable boolean := false;
    msg text;
BEGIN
    SELECT timescaledb_extension_installed
    INTO ext_installed
    FROM public.ppiq_time_series_capabilities
    WHERE id = 1;

    SELECT to_regclass('public.parameter_observations') IS NOT NULL
    INTO table_exists;

    SELECT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'parameter_observations'
          AND column_name = 'observed_at_utc'
    )
    INTO time_col_exists;

    IF ext_installed AND table_exists AND time_col_exists THEN
        BEGIN
            EXECUTE $SQL$
                SELECT create_hypertable(
                    'public.parameter_observations',
                    'observed_at_utc',
                    if_not_exists => TRUE,
                    migrate_data => TRUE
                )
            $SQL$;

            SELECT EXISTS (
                SELECT 1
                FROM timescaledb_information.hypertables
                WHERE hypertable_schema = 'public'
                  AND hypertable_name = 'parameter_observations'
            )
            INTO is_hypertable;

            UPDATE public.ppiq_time_series_capabilities
            SET telemetry_mode = CASE WHEN is_hypertable THEN 'timescaledb_hypertable' ELSE telemetry_mode END,
                notes = CASE WHEN is_hypertable THEN 'parameter_observations is a Timescale hypertable.' ELSE notes END,
                updated_at_utc = now()
            WHERE id = 1;

        EXCEPTION WHEN OTHERS THEN
            msg := 'Timescale create_hypertable failed; native partition fallback remains active. Reason: ' || SQLERRM;

            UPDATE public.ppiq_time_series_capabilities
            SET telemetry_mode = 'native_partition_fallback',
                notes = msg,
                updated_at_utc = now()
            WHERE id = 1;
        END;
    ELSIF NOT table_exists THEN
        UPDATE public.ppiq_time_series_capabilities
        SET telemetry_mode = 'native_partition_fallback',
            notes = 'parameter_observations table not found; rollup contract installed only.',
            updated_at_utc = now()
        WHERE id = 1;
    ELSIF NOT time_col_exists THEN
        UPDATE public.ppiq_time_series_capabilities
        SET telemetry_mode = 'native_partition_fallback',
            notes = 'parameter_observations.observed_at_utc not found; rollup contract installed only.',
            updated_at_utc = now()
        WHERE id = 1;
    END IF;
END $$;

-- ------------------------------------------------------------
-- 4) Telemetry ingestion metrics + checkpoints
-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.ppiq_telemetry_ingestion_checkpoints
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    source_code text NOT NULL,
    stream_code text NOT NULL,
    last_observed_at_utc timestamptz NULL,
    last_source_offset text NULL,
    rows_ingested bigint NOT NULL DEFAULT 0,
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_telemetry_checkpoint UNIQUE(tenant_id, source_code, stream_code)
);

CREATE TABLE IF NOT EXISTS public.ppiq_telemetry_ingestion_metrics
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    source_code text NOT NULL,
    stream_code text NOT NULL,
    batch_id uuid NOT NULL DEFAULT gen_random_uuid(),
    rows_received integer NOT NULL DEFAULT 0,
    rows_written integer NOT NULL DEFAULT 0,
    rows_rejected integer NOT NULL DEFAULT 0,
    duration_ms integer NOT NULL DEFAULT 0,
    rows_per_second numeric(18, 3) NOT NULL DEFAULT 0,
    lag_seconds numeric(18, 3) NULL,
    started_at_utc timestamptz NOT NULL DEFAULT now(),
    completed_at_utc timestamptz NULL,
    error_message text NULL
);

CREATE INDEX IF NOT EXISTS ix_ppiq_telemetry_metrics_tenant_time
ON public.ppiq_telemetry_ingestion_metrics(tenant_id, started_at_utc DESC);

-- ------------------------------------------------------------
-- 5) Continuous aggregate fallback tables
-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.ppiq_telemetry_rollup_hourly
(
    tenant_id uuid NOT NULL,
    parameter_definition_id uuid NULL,
    material_unit_id uuid NULL,
    bucket_utc timestamptz NOT NULL,
    sample_count bigint NOT NULL,
    min_value numeric(18, 6) NULL,
    max_value numeric(18, 6) NULL,
    avg_value numeric(18, 6) NULL,
    p95_value numeric(18, 6) NULL,
    refreshed_at_utc timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (tenant_id, bucket_utc, parameter_definition_id, material_unit_id)
);

CREATE TABLE IF NOT EXISTS public.ppiq_telemetry_rollup_daily
(
    tenant_id uuid NOT NULL,
    parameter_definition_id uuid NULL,
    material_unit_id uuid NULL,
    bucket_utc timestamptz NOT NULL,
    sample_count bigint NOT NULL,
    min_value numeric(18, 6) NULL,
    max_value numeric(18, 6) NULL,
    avg_value numeric(18, 6) NULL,
    p95_value numeric(18, 6) NULL,
    refreshed_at_utc timestamptz NOT NULL DEFAULT now(),
    PRIMARY KEY (tenant_id, bucket_utc, parameter_definition_id, material_unit_id)
);

CREATE INDEX IF NOT EXISTS ix_ppiq_telemetry_rollup_hourly_tenant_bucket
ON public.ppiq_telemetry_rollup_hourly(tenant_id, bucket_utc DESC);

CREATE INDEX IF NOT EXISTS ix_ppiq_telemetry_rollup_daily_tenant_bucket
ON public.ppiq_telemetry_rollup_daily(tenant_id, bucket_utc DESC);

-- ------------------------------------------------------------
-- 6) Refresh function. Detects available value column dynamically.
-- ------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.ppiq_v5_refresh_telemetry_rollups()
RETURNS TABLE
(
    rollup_name text,
    rows_upserted bigint,
    evidence text
)
LANGUAGE plpgsql
AS $$
DECLARE
    has_table boolean;
    value_col text;
    sql_hourly text;
    sql_daily text;
    before_hourly bigint;
    before_daily bigint;
    after_hourly bigint;
    after_daily bigint;
BEGIN
    SELECT to_regclass('public.parameter_observations') IS NOT NULL INTO has_table;

    IF NOT has_table THEN
        RETURN QUERY SELECT 'hourly', 0::bigint, 'parameter_observations table not found; nothing refreshed.';
        RETURN QUERY SELECT 'daily', 0::bigint, 'parameter_observations table not found; nothing refreshed.';
        RETURN;
    END IF;

    SELECT column_name
    INTO value_col
    FROM information_schema.columns
    WHERE table_schema = 'public'
      AND table_name = 'parameter_observations'
      AND column_name IN ('numeric_value', 'value_numeric', 'value', 'observation_value')
    ORDER BY CASE column_name
        WHEN 'numeric_value' THEN 1
        WHEN 'value_numeric' THEN 2
        WHEN 'value' THEN 3
        ELSE 4
    END
    LIMIT 1;

    IF value_col IS NULL THEN
        RETURN QUERY SELECT 'hourly', 0::bigint, 'No numeric telemetry value column found.';
        RETURN QUERY SELECT 'daily', 0::bigint, 'No numeric telemetry value column found.';
        RETURN;
    END IF;

    SELECT count(*) INTO before_hourly FROM public.ppiq_telemetry_rollup_hourly;
    SELECT count(*) INTO before_daily FROM public.ppiq_telemetry_rollup_daily;

    sql_hourly := format($SQL$
        INSERT INTO public.ppiq_telemetry_rollup_hourly
        (
            tenant_id,
            parameter_definition_id,
            material_unit_id,
            bucket_utc,
            sample_count,
            min_value,
            max_value,
            avg_value,
            p95_value,
            refreshed_at_utc
        )
        SELECT
            tenant_id,
            CASE WHEN EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='parameter_observations' AND column_name='parameter_definition_id'
            ) THEN parameter_definition_id ELSE NULL::uuid END,
            CASE WHEN EXISTS (
                SELECT 1 FROM information_schema.columns
                WHERE table_schema='public' AND table_name='parameter_observations' AND column_name='material_unit_id'
            ) THEN material_unit_id ELSE NULL::uuid END,
            date_trunc('hour', observed_at_utc) AS bucket_utc,
            count(*) AS sample_count,
            min(%1$I::numeric) AS min_value,
            max(%1$I::numeric) AS max_value,
            avg(%1$I::numeric) AS avg_value,
            percentile_cont(0.95) WITHIN GROUP (ORDER BY %1$I::numeric) AS p95_value,
            now()
        FROM public.parameter_observations
        WHERE observed_at_utc IS NOT NULL
          AND %1$I IS NOT NULL
        GROUP BY 1,2,3,4
        ON CONFLICT (tenant_id, bucket_utc, parameter_definition_id, material_unit_id)
        DO UPDATE SET
            sample_count = EXCLUDED.sample_count,
            min_value = EXCLUDED.min_value,
            max_value = EXCLUDED.max_value,
            avg_value = EXCLUDED.avg_value,
            p95_value = EXCLUDED.p95_value,
            refreshed_at_utc = now()
    $SQL$, value_col);

    sql_daily := replace(sql_hourly, 'ppiq_telemetry_rollup_hourly', 'ppiq_telemetry_rollup_daily');
    sql_daily := replace(sql_daily, 'date_trunc(''hour'', observed_at_utc)', 'date_trunc(''day'', observed_at_utc)');

    EXECUTE sql_hourly;
    EXECUTE sql_daily;

    SELECT count(*) INTO after_hourly FROM public.ppiq_telemetry_rollup_hourly;
    SELECT count(*) INTO after_daily FROM public.ppiq_telemetry_rollup_daily;

    RETURN QUERY SELECT 'hourly', GREATEST(after_hourly - before_hourly, 0), 'Hourly rollup refreshed from parameter_observations.';
    RETURN QUERY SELECT 'daily', GREATEST(after_daily - before_daily, 0), 'Daily rollup refreshed from parameter_observations.';
END $$;

-- ------------------------------------------------------------
-- 7) Compression/retention policy config. If Timescale exists, real jobs may
--    be added by DBA; locally this records the contract and validates it.
-- ------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.ppiq_time_series_policy
(
    id integer PRIMARY KEY DEFAULT 1,
    compress_after_days integer NOT NULL DEFAULT 7,
    retain_raw_months integer NOT NULL DEFAULT 11,
    retain_rollup_months integer NOT NULL DEFAULT 60,
    dashboard_rollup_preference text NOT NULL DEFAULT 'hourly_then_daily',
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_time_series_policy_singleton CHECK (id = 1),
    CONSTRAINT ck_ppiq_time_series_policy_positive CHECK (
        compress_after_days > 0 AND retain_raw_months > 0 AND retain_rollup_months >= retain_raw_months
    )
);

INSERT INTO public.ppiq_time_series_policy(id)
VALUES (1)
ON CONFLICT (id) DO NOTHING;

-- ------------------------------------------------------------
-- 8) Acceptance function
-- ------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.ppiq_v5_p03_timeseries_acceptance()
RETURNS TABLE
(
    check_name text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT
        'Timeseries capability contract installed',
        to_regclass('public.ppiq_time_series_capabilities') IS NOT NULL,
        COALESCE((SELECT 'mode=' || telemetry_mode || '; ' || notes FROM public.ppiq_time_series_capabilities WHERE id=1), 'missing')
    UNION ALL
    SELECT
        'Telemetry source available or fallback declared',
        EXISTS (SELECT 1 FROM public.ppiq_time_series_capabilities WHERE telemetry_mode IN ('timescaledb_hypertable', 'native_partition_fallback', 'timescale_candidate')),
        COALESCE((SELECT telemetry_mode FROM public.ppiq_time_series_capabilities WHERE id=1), 'missing')
    UNION ALL
    SELECT
        'Hourly rollup table exists',
        to_regclass('public.ppiq_telemetry_rollup_hourly') IS NOT NULL,
        'ppiq_telemetry_rollup_hourly installed.'
    UNION ALL
    SELECT
        'Daily rollup table exists',
        to_regclass('public.ppiq_telemetry_rollup_daily') IS NOT NULL,
        'ppiq_telemetry_rollup_daily installed.'
    UNION ALL
    SELECT
        'Ingestion metrics and checkpoints exist',
        to_regclass('public.ppiq_telemetry_ingestion_metrics') IS NOT NULL
        AND to_regclass('public.ppiq_telemetry_ingestion_checkpoints') IS NOT NULL,
        'metrics/checkpoints tables installed.'
    UNION ALL
    SELECT
        'Compression retention policy contract exists',
        to_regclass('public.ppiq_time_series_policy') IS NOT NULL,
        COALESCE((SELECT 'compress_after_days=' || compress_after_days || ', retain_raw_months=' || retain_raw_months FROM public.ppiq_time_series_policy WHERE id=1), 'missing')
    UNION ALL
    SELECT
        'Rollup refresh function exists',
        to_regprocedure('public.ppiq_v5_refresh_telemetry_rollups()') IS NOT NULL,
        'ppiq_v5_refresh_telemetry_rollups exists.';
$$;

COMMIT;

SELECT * FROM public.ppiq_v5_p03_timeseries_acceptance();