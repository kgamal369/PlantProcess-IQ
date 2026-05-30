-- =================================================================================================
-- PlantProcess IQ v4
-- Phase 03  Two-Stage Delta Import Architecture
--
-- Implements:
--   PPIQ-T113  dump-copy store preserving each source table shape
--   PPIQ-T114  stage-1 delta import source -> dump using last-index comparison
--   PPIQ-T115  stage-2 dump -> canonical schema refresh with no double-processing
--   PPIQ-T116  two-stage import job telemetry and crash/duration metadata
-- =================================================================================================

SET client_min_messages TO WARNING;
SET TIME ZONE 'UTC';

CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

CREATE SCHEMA IF NOT EXISTS dump_store;

-- -------------------------------------------------------------------------------------------------
-- T116  extend JobDefinition / JobRunHistory without breaking EF mapping.
-- EF ignores extra DB columns, while SQL runtime and admin endpoints can use them.
-- -------------------------------------------------------------------------------------------------

ALTER TABLE public.job_definitions
    ADD COLUMN IF NOT EXISTS job_category text NULL,
    ADD COLUMN IF NOT EXISTS stage_key text NULL,
    ADD COLUMN IF NOT EXISTS last_started_heartbeat_utc timestamptz NULL,
    ADD COLUMN IF NOT EXISTS last_success_row_count bigint NULL,
    ADD COLUMN IF NOT EXISTS last_failed_row_count bigint NULL,
    ADD COLUMN IF NOT EXISTS consecutive_failure_count integer NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS last_timeout_seconds integer NULL,
    ADD COLUMN IF NOT EXISTS next_backoff_until_utc timestamptz NULL,
    ADD COLUMN IF NOT EXISTS runtime_options_json jsonb NOT NULL DEFAULT '{}'::jsonb;

ALTER TABLE public.job_run_histories
    ADD COLUMN IF NOT EXISTS stage_key text NULL,
    ADD COLUMN IF NOT EXISTS imported_row_count bigint NULL,
    ADD COLUMN IF NOT EXISTS skipped_row_count bigint NULL,
    ADD COLUMN IF NOT EXISTS inserted_canonical_row_count bigint NULL,
    ADD COLUMN IF NOT EXISTS last_index_before text NULL,
    ADD COLUMN IF NOT EXISTS last_index_after text NULL,
    ADD COLUMN IF NOT EXISTS timeout_seconds integer NULL,
    ADD COLUMN IF NOT EXISTS crash_detected boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS runtime_options_json jsonb NOT NULL DEFAULT '{}'::jsonb;

CREATE INDEX IF NOT EXISTS ix_job_definitions_phase3_category
ON public.job_definitions(job_category)
WHERE is_deleted = false;

CREATE INDEX IF NOT EXISTS ix_job_run_histories_phase3_stage
ON public.job_run_histories(stage_key, started_at_utc);

-- -------------------------------------------------------------------------------------------------
-- T113  registry for dump-copy tables.
-- The dump tables themselves preserve the customer's/source's own column shape.
-- Extra metadata is kept here and in run tables, not inside the dump tables.
-- -------------------------------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.source_table_dump_registry
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NULL,
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at_utc timestamptz NULL,
    deleted_reason text NULL,

    source_system_code text NOT NULL,
    source_schema_name text NOT NULL,
    source_table_name text NOT NULL,

    dump_schema_name text NOT NULL DEFAULT 'dump_store',
    dump_table_name text NOT NULL,

    primary_key_columns text[] NOT NULL,
    last_index_column text NOT NULL,
    last_index_value_text text NULL,
    last_index_value_type text NULL,

    source_columns_json jsonb NOT NULL DEFAULT '[]'::jsonb,
    source_column_count integer NOT NULL DEFAULT 0,
    source_shape_hash text NULL,

    stage1_status text NOT NULL DEFAULT 'NeverRun',
    stage2_status text NOT NULL DEFAULT 'NeverRun',

    last_stage1_run_id uuid NULL,
    last_stage2_run_id uuid NULL,

    last_stage1_started_at_utc timestamptz NULL,
    last_stage1_completed_at_utc timestamptz NULL,
    last_stage1_duration_ms bigint NULL,
    last_stage1_inserted_rows bigint NOT NULL DEFAULT 0,

    last_stage2_started_at_utc timestamptz NULL,
    last_stage2_completed_at_utc timestamptz NULL,
    last_stage2_duration_ms bigint NULL,
    last_stage2_canonical_rows bigint NOT NULL DEFAULT 0,

    last_synced_at_utc timestamptz NULL,
    last_error text NULL,

    import_cycle_minutes integer NOT NULL DEFAULT 2,
    hmi_refresh_seconds integer NOT NULL DEFAULT 30,
    lease_owner text NULL,
    lease_until_utc timestamptz NULL,

    is_active boolean NOT NULL DEFAULT true
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_source_table_dump_registry_source
ON public.source_table_dump_registry(lower(source_schema_name), lower(source_table_name))
WHERE is_deleted = false;

CREATE UNIQUE INDEX IF NOT EXISTS ux_source_table_dump_registry_dump
ON public.source_table_dump_registry(lower(dump_schema_name), lower(dump_table_name))
WHERE is_deleted = false;

CREATE INDEX IF NOT EXISTS ix_source_table_dump_registry_active
ON public.source_table_dump_registry(is_active, stage1_status, stage2_status)
WHERE is_deleted = false;

-- -------------------------------------------------------------------------------------------------
-- T114/T115/T118  import runs and stage-2 processed watermark.
-- -------------------------------------------------------------------------------------------------

CREATE TABLE IF NOT EXISTS public.two_stage_import_runs
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),

    registry_id uuid NULL REFERENCES public.source_table_dump_registry(id) ON DELETE SET NULL,
    run_kind text NOT NULL,
    run_status text NOT NULL,

    source_system_code text NULL,
    source_schema_name text NULL,
    source_table_name text NULL,
    dump_schema_name text NULL,
    dump_table_name text NULL,

    started_at_utc timestamptz NOT NULL DEFAULT now(),
    completed_at_utc timestamptz NULL,
    duration_ms bigint NULL,

    inserted_rows bigint NOT NULL DEFAULT 0,
    skipped_existing_rows bigint NOT NULL DEFAULT 0,
    canonical_rows bigint NOT NULL DEFAULT 0,

    last_index_before text NULL,
    last_index_after text NULL,

    requested_by text NULL,
    correlation_id text NULL,
    message text NULL,
    failure_reason text NULL,
    result_json jsonb NOT NULL DEFAULT '{}'::jsonb
);

CREATE INDEX IF NOT EXISTS ix_two_stage_import_runs_registry_started
ON public.two_stage_import_runs(registry_id, started_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_two_stage_import_runs_kind_status
ON public.two_stage_import_runs(run_kind, run_status, started_at_utc DESC);

CREATE TABLE IF NOT EXISTS public.two_stage_processed_watermarks
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    registry_id uuid NOT NULL REFERENCES public.source_table_dump_registry(id) ON DELETE CASCADE,
    consumer_code text NOT NULL DEFAULT 'STAGE2_CANONICAL',
    last_processed_index_value_text text NULL,
    last_processed_at_utc timestamptz NULL,
    processed_rows bigint NOT NULL DEFAULT 0,
    updated_at_utc timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ux_two_stage_processed_watermarks UNIQUE(registry_id, consumer_code)
);

-- -------------------------------------------------------------------------------------------------
-- Helpers
-- -------------------------------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.ppiq_identifier(p_value text)
RETURNS text
LANGUAGE plpgsql
AS $$
DECLARE
    v_value text;
BEGIN
    v_value := lower(regexp_replace(coalesce(trim(p_value), ''), '[^a-zA-Z0-9_]', '_', 'g'));
    v_value := regexp_replace(v_value, '_+', '_', 'g');
    v_value := trim(both '_' from v_value);

    IF v_value = '' THEN
        RAISE EXCEPTION 'Invalid SQL identifier: value is empty.';
    END IF;

    IF length(v_value) > 55 THEN
        v_value := left(v_value, 46) || '_' || substr(md5(v_value), 1, 8);
    END IF;

    RETURN v_value;
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_column_pg_type(
    p_schema_name text,
    p_table_name text,
    p_column_name text)
RETURNS text
LANGUAGE plpgsql
AS $$
DECLARE
    v_type text;
BEGIN
    SELECT format_type(a.atttypid, a.atttypmod)
    INTO v_type
    FROM pg_attribute a
    JOIN pg_class c ON c.oid = a.attrelid
    JOIN pg_namespace n ON n.oid = c.relnamespace
    WHERE n.nspname = p_schema_name
      AND c.relname = p_table_name
      AND a.attname = p_column_name
      AND a.attnum > 0
      AND NOT a.attisdropped;

    IF v_type IS NULL THEN
        RAISE EXCEPTION 'Column %.%.% does not exist.', p_schema_name, p_table_name, p_column_name;
    END IF;

    RETURN v_type;
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_ensure_phase3_site()
RETURNS uuid
LANGUAGE plpgsql
AS $$
DECLARE
    v_site_id uuid;
BEGIN
    SELECT id
    INTO v_site_id
    FROM public.sites
    WHERE is_deleted = false
    ORDER BY created_at_utc
    LIMIT 1;

    IF v_site_id IS NOT NULL THEN
        RETURN v_site_id;
    END IF;

    v_site_id := gen_random_uuid();

    INSERT INTO public.sites
    (
        id,
        created_at_utc,
        updated_at_utc,
        is_synthetic,
        source_system,
        source_record_id,
        is_deleted,
        deleted_at_utc,
        deleted_reason,
        site_code,
        site_name,
        company_name,
        country_code,
        time_zone_id
    )
    VALUES
    (
        v_site_id,
        now(),
        NULL,
        true,
        'phase3-two-stage-import',
        'PHASE3-DEMO-SITE',
        false,
        NULL,
        NULL,
        'PPIQ_DEMO_SITE',
        'PlantProcess IQ Demo Site',
        'PlantProcess IQ',
        'DE',
        'Europe/Berlin'
    );

    RETURN v_site_id;
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_upsert_phase3_job(
    p_job_code text,
    p_job_name text,
    p_job_type text,
    p_job_category text,
    p_stage_key text,
    p_schedule_expression text,
    p_description text)
RETURNS uuid
LANGUAGE plpgsql
AS $$
DECLARE
    v_job_id uuid;
BEGIN
    SELECT id
    INTO v_job_id
    FROM public.job_definitions
    WHERE lower(job_code) = lower(trim(p_job_code))
      AND is_deleted = false
    LIMIT 1;

    IF v_job_id IS NULL THEN
        v_job_id := gen_random_uuid();

        INSERT INTO public.job_definitions
        (
            id,
            created_at_utc,
            updated_at_utc,
            is_synthetic,
            source_system,
            source_record_id,
            is_deleted,
            deleted_at_utc,
            deleted_reason,
            job_code,
            job_name,
            job_type,
            target_id,
            target_type,
            schedule_expression,
            is_enabled,
            last_run_started_at_utc,
            last_run_completed_at_utc,
            last_run_duration_ms,
            last_run_status,
            last_failure_reason,
            next_run_at_utc,
            description,
            job_category,
            stage_key,
            runtime_options_json
        )
        VALUES
        (
            v_job_id,
            now(),
            NULL,
            true,
            'phase3-two-stage-import',
            p_job_code,
            false,
            NULL,
            NULL,
            upper(trim(p_job_code)),
            trim(p_job_name),
            trim(p_job_type),
            NULL,
            'TwoStageImport',
            trim(p_schedule_expression),
            true,
            NULL,
            NULL,
            NULL,
            'NeverRun',
            NULL,
            NULL,
            trim(p_description),
            trim(p_job_category),
            trim(p_stage_key),
            '{}'::jsonb
        );
    ELSE
        UPDATE public.job_definitions
        SET
            updated_at_utc = now(),
            job_name = trim(p_job_name),
            job_type = trim(p_job_type),
            target_type = 'TwoStageImport',
            schedule_expression = trim(p_schedule_expression),
            description = trim(p_description),
            job_category = trim(p_job_category),
            stage_key = trim(p_stage_key)
        WHERE id = v_job_id;
    END IF;

    RETURN v_job_id;
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_start_phase3_job_history(
    p_job_code text,
    p_job_name text,
    p_job_type text,
    p_job_category text,
    p_stage_key text,
    p_trigger_source text,
    p_triggered_by text,
    p_correlation_id text,
    p_timeout_seconds integer,
    p_runtime_options_json jsonb)
RETURNS uuid
LANGUAGE plpgsql
AS $$
DECLARE
    v_job_id uuid;
    v_history_id uuid;
BEGIN
    v_job_id := public.ppiq_upsert_phase3_job(
        p_job_code,
        p_job_name,
        p_job_type,
        p_job_category,
        p_stage_key,
        CASE
            WHEN p_stage_key = 'Stage1DeltaImport' THEN 'Every 2 minutes'
            WHEN p_stage_key = 'Stage2CanonicalRefresh' THEN 'Every 30 seconds'
            ELSE 'Manual / On demand'
        END,
        'PlantProcess IQ Phase 03 two-stage import runtime job.'
    );

    UPDATE public.job_definitions
    SET
        updated_at_utc = now(),
        last_run_started_at_utc = now(),
        last_run_completed_at_utc = NULL,
        last_run_duration_ms = NULL,
        last_run_status = 'Running',
        last_failure_reason = NULL,
        last_started_heartbeat_utc = now(),
        last_timeout_seconds = p_timeout_seconds,
        runtime_options_json = coalesce(p_runtime_options_json, '{}'::jsonb)
    WHERE id = v_job_id;

    v_history_id := gen_random_uuid();

    INSERT INTO public.job_run_histories
    (
        id,
        created_at_utc,
        updated_at_utc,
        is_synthetic,
        source_system,
        source_record_id,
        is_deleted,
        deleted_at_utc,
        deleted_reason,
        job_definition_id,
        job_code,
        job_name,
        job_type,
        status,
        started_at_utc,
        completed_at_utc,
        duration_ms,
        trigger_source,
        triggered_by,
        correlation_id,
        failure_reason,
        run_message,
        result_summary_json,
        stage_key,
        timeout_seconds,
        runtime_options_json
    )
    VALUES
    (
        v_history_id,
        now(),
        NULL,
        true,
        'phase3-two-stage-import',
        p_job_code || ':' || v_history_id::text,
        false,
        NULL,
        NULL,
        v_job_id,
        upper(trim(p_job_code)),
        trim(p_job_name),
        trim(p_job_type),
        'Running',
        now(),
        NULL,
        NULL,
        coalesce(nullif(trim(p_trigger_source), ''), 'Admin UI'),
        nullif(trim(coalesce(p_triggered_by, '')), ''),
        nullif(trim(coalesce(p_correlation_id, '')), ''),
        NULL,
        'Job is running.',
        '{}'::jsonb,
        trim(p_stage_key),
        p_timeout_seconds,
        coalesce(p_runtime_options_json, '{}'::jsonb)
    );

    RETURN v_history_id;
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_finish_phase3_job_history(
    p_history_id uuid,
    p_job_code text,
    p_status text,
    p_message text,
    p_failure_reason text,
    p_imported_row_count bigint,
    p_skipped_row_count bigint,
    p_inserted_canonical_row_count bigint,
    p_last_index_before text,
    p_last_index_after text,
    p_result_json jsonb)
RETURNS void
LANGUAGE plpgsql
AS $$
DECLARE
    v_completed_at timestamptz := now();
    v_started_at timestamptz;
    v_duration_ms bigint;
BEGIN
    SELECT started_at_utc
    INTO v_started_at
    FROM public.job_run_histories
    WHERE id = p_history_id;

    v_duration_ms := greatest(0, floor(extract(epoch FROM (v_completed_at - coalesce(v_started_at, v_completed_at))) * 1000)::bigint);

    UPDATE public.job_run_histories
    SET
        updated_at_utc = v_completed_at,
        status = p_status,
        completed_at_utc = v_completed_at,
        duration_ms = v_duration_ms,
        failure_reason = nullif(p_failure_reason, ''),
        run_message = nullif(p_message, ''),
        result_summary_json = coalesce(p_result_json, '{}'::jsonb),
        imported_row_count = p_imported_row_count,
        skipped_row_count = p_skipped_row_count,
        inserted_canonical_row_count = p_inserted_canonical_row_count,
        last_index_before = p_last_index_before,
        last_index_after = p_last_index_after
    WHERE id = p_history_id;

    UPDATE public.job_definitions
    SET
        updated_at_utc = v_completed_at,
        last_run_completed_at_utc = v_completed_at,
        last_run_duration_ms = v_duration_ms,
        last_run_status = p_status,
        last_failure_reason = nullif(p_failure_reason, ''),
        last_success_row_count = CASE WHEN p_status = 'Ok' THEN coalesce(p_imported_row_count, 0) + coalesce(p_inserted_canonical_row_count, 0) ELSE last_success_row_count END,
        last_failed_row_count = CASE WHEN p_status IN ('Failed', 'Timeout') THEN coalesce(p_imported_row_count, 0) + coalesce(p_inserted_canonical_row_count, 0) ELSE last_failed_row_count END,
        consecutive_failure_count = CASE WHEN p_status = 'Ok' THEN 0 ELSE consecutive_failure_count + 1 END
    WHERE lower(job_code) = lower(trim(p_job_code))
      AND is_deleted = false;
END;
$$;

-- -------------------------------------------------------------------------------------------------
-- T113  register/provision dump-copy tables.
-- -------------------------------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.ppiq_register_dump_source(
    p_source_system_code text,
    p_source_schema_name text,
    p_source_table_name text,
    p_primary_key_columns text[],
    p_last_index_column text,
    p_import_cycle_minutes integer DEFAULT 2,
    p_hmi_refresh_seconds integer DEFAULT 30)
RETURNS uuid
LANGUAGE plpgsql
AS $$
DECLARE
    v_registry_id uuid;
    v_dump_schema text := 'dump_store';
    v_dump_table text;
    v_columns_json jsonb;
    v_column_count integer;
    v_shape_hash text;
    v_last_index_type text;
    v_idx_name text;
    v_pk text;
BEGIN
    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.tables
        WHERE table_schema = p_source_schema_name
          AND table_name = p_source_table_name
    ) THEN
        RAISE EXCEPTION 'Source table %.% does not exist.', p_source_schema_name, p_source_table_name;
    END IF;

    IF p_primary_key_columns IS NULL OR array_length(p_primary_key_columns, 1) IS NULL THEN
        RAISE EXCEPTION 'Primary key columns are required for %.%.', p_source_schema_name, p_source_table_name;
    END IF;

    IF EXISTS (
        SELECT 1
        FROM unnest(p_primary_key_columns) AS pk(column_name)
        WHERE NOT EXISTS (
            SELECT 1
            FROM information_schema.columns c
            WHERE c.table_schema = p_source_schema_name
              AND c.table_name = p_source_table_name
              AND c.column_name = pk.column_name
        )
    ) THEN
        RAISE EXCEPTION 'One or more primary key columns do not exist for %.%.', p_source_schema_name, p_source_table_name;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = p_source_schema_name
          AND table_name = p_source_table_name
          AND column_name = p_last_index_column
    ) THEN
        RAISE EXCEPTION 'Last-index column %.%.% does not exist.', p_source_schema_name, p_source_table_name, p_last_index_column;
    END IF;

    v_dump_table := public.ppiq_identifier(p_source_schema_name || '__' || p_source_table_name);
    v_last_index_type := public.ppiq_column_pg_type(p_source_schema_name, p_source_table_name, p_last_index_column);

    SELECT
        coalesce(
            jsonb_agg(
                jsonb_build_object(
                    'ordinal', ordinal_position,
                    'columnName', column_name,
                    'dataType', data_type,
                    'udtName', udt_name,
                    'isNullable', is_nullable,
                    'columnDefault', column_default
                )
                ORDER BY ordinal_position
            ),
            '[]'::jsonb
        ),
        count(*)::integer
    INTO v_columns_json, v_column_count
    FROM information_schema.columns
    WHERE table_schema = p_source_schema_name
      AND table_name = p_source_table_name;

    v_shape_hash := md5(v_columns_json::text);

    EXECUTE format(
        'CREATE TABLE IF NOT EXISTS %I.%I (LIKE %I.%I INCLUDING ALL)',
        v_dump_schema,
        v_dump_table,
        p_source_schema_name,
        p_source_table_name
    );

    v_idx_name := public.ppiq_identifier('ix_' || v_dump_table || '_' || p_last_index_column);
    EXECUTE format(
        'CREATE INDEX IF NOT EXISTS %I ON %I.%I(%I)',
        v_idx_name,
        v_dump_schema,
        v_dump_table,
        p_last_index_column
    );

    foreach v_pk in array p_primary_key_columns
    loop
        v_idx_name := public.ppiq_identifier('ix_' || v_dump_table || '_' || v_pk);
        EXECUTE format(
            'CREATE INDEX IF NOT EXISTS %I ON %I.%I(%I)',
            v_idx_name,
            v_dump_schema,
            v_dump_table,
            v_pk
        );
    end loop;

    SELECT id
    INTO v_registry_id
    FROM public.source_table_dump_registry
    WHERE lower(source_schema_name) = lower(p_source_schema_name)
      AND lower(source_table_name) = lower(p_source_table_name)
      AND is_deleted = false
    LIMIT 1;

    IF v_registry_id IS NULL THEN
        v_registry_id := gen_random_uuid();

        INSERT INTO public.source_table_dump_registry
        (
            id,
            source_system_code,
            source_schema_name,
            source_table_name,
            dump_schema_name,
            dump_table_name,
            primary_key_columns,
            last_index_column,
            last_index_value_type,
            source_columns_json,
            source_column_count,
            source_shape_hash,
            import_cycle_minutes,
            hmi_refresh_seconds
        )
        VALUES
        (
            v_registry_id,
            upper(trim(p_source_system_code)),
            p_source_schema_name,
            p_source_table_name,
            v_dump_schema,
            v_dump_table,
            p_primary_key_columns,
            p_last_index_column,
            v_last_index_type,
            v_columns_json,
            v_column_count,
            v_shape_hash,
            greatest(1, coalesce(p_import_cycle_minutes, 2)),
            greatest(5, coalesce(p_hmi_refresh_seconds, 30))
        );
    ELSE
        UPDATE public.source_table_dump_registry
        SET
            updated_at_utc = now(),
            source_system_code = upper(trim(p_source_system_code)),
            dump_schema_name = v_dump_schema,
            dump_table_name = v_dump_table,
            primary_key_columns = p_primary_key_columns,
            last_index_column = p_last_index_column,
            last_index_value_type = v_last_index_type,
            source_columns_json = v_columns_json,
            source_column_count = v_column_count,
            source_shape_hash = v_shape_hash,
            import_cycle_minutes = greatest(1, coalesce(p_import_cycle_minutes, 2)),
            hmi_refresh_seconds = greatest(5, coalesce(p_hmi_refresh_seconds, 30)),
            is_active = true,
            last_error = NULL
        WHERE id = v_registry_id;
    END IF;

    RETURN v_registry_id;
END;
$$;

-- -------------------------------------------------------------------------------------------------
-- T114  stage-1 delta import source -> dump.
-- -------------------------------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.ppiq_run_stage1_delta_import(
    p_registry_id uuid,
    p_requested_by text DEFAULT 'manual',
    p_max_rows integer DEFAULT 50000,
    p_timeout_seconds integer DEFAULT 120)
RETURNS TABLE
(
    run_id uuid,
    registry_id uuid,
    status text,
    inserted_rows bigint,
    skipped_existing_rows bigint,
    last_index_before text,
    last_index_after text,
    duration_ms bigint,
    message text
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_registry public.source_table_dump_registry%ROWTYPE;
    v_run_id uuid := gen_random_uuid();
    v_history_id uuid;
    v_started_at timestamptz := now();
    v_completed_at timestamptz;
    v_duration_ms bigint := 0;
    v_inserted_rows bigint := 0;
    v_skipped_rows bigint := 0;
    v_last_before text;
    v_last_after text;
    v_message text := '';
    v_failure text := '';
    v_columns text;
    v_source_columns text;
    v_pk_join text;
    v_index_condition text;
    v_index_type text;
    v_sql text;
    v_dump_count bigint := 0;
    v_lease_owner text := gen_random_uuid()::text;
BEGIN
    SELECT *
    INTO v_registry
    FROM public.source_table_dump_registry
    WHERE id = p_registry_id
      AND is_deleted = false
      AND is_active = true
    FOR UPDATE;

    IF v_registry.id IS NULL THEN
        RAISE EXCEPTION 'Active dump registry % was not found.', p_registry_id;
    END IF;

    v_history_id := public.ppiq_start_phase3_job_history(
        'PPIQ_STAGE1_DELTA_IMPORT',
        'Stage 1 Delta Import  Source to Dump Store',
        'DbLinkImport',
        'TwoStageDeltaImport',
        'Stage1DeltaImport',
        'Admin UI',
        p_requested_by,
        NULL,
        p_timeout_seconds,
        jsonb_build_object('registryId', p_registry_id, 'maxRows', p_max_rows)
    );

    IF v_registry.lease_until_utc IS NOT NULL AND v_registry.lease_until_utc > now() THEN
        v_completed_at := now();
        v_duration_ms := greatest(0, floor(extract(epoch FROM (v_completed_at - v_started_at)) * 1000)::bigint);
        v_message := 'Another Stage-1 import is already running for this source table. Multi-import was blocked.';

        INSERT INTO public.two_stage_import_runs
        (
            id,
            registry_id,
            run_kind,
            run_status,
            source_system_code,
            source_schema_name,
            source_table_name,
            dump_schema_name,
            dump_table_name,
            started_at_utc,
            completed_at_utc,
            duration_ms,
            requested_by,
            message,
            result_json
        )
        VALUES
        (
            v_run_id,
            v_registry.id,
            'Stage1DeltaImport',
            'Failed',
            v_registry.source_system_code,
            v_registry.source_schema_name,
            v_registry.source_table_name,
            v_registry.dump_schema_name,
            v_registry.dump_table_name,
            v_started_at,
            v_completed_at,
            v_duration_ms,
            p_requested_by,
            v_message,
            jsonb_build_object('blockedByLeaseOwner', v_registry.lease_owner, 'leaseUntilUtc', v_registry.lease_until_utc)
        );

        UPDATE public.source_table_dump_registry
        SET
            updated_at_utc = now(),
            stage1_status = 'Failed',
            last_stage1_run_id = v_run_id,
            last_stage1_started_at_utc = v_started_at,
            last_stage1_completed_at_utc = v_completed_at,
            last_stage1_duration_ms = v_duration_ms,
            last_error = v_message
        WHERE id = v_registry.id;

        PERFORM public.ppiq_finish_phase3_job_history(
            v_history_id,
            'PPIQ_STAGE1_DELTA_IMPORT',
            'Failed',
            v_message,
            v_message,
            0,
            0,
            0,
            v_registry.last_index_value_text,
            v_registry.last_index_value_text,
            jsonb_build_object('registryId', v_registry.id, 'multiImportBlocked', true)
        );

        RETURN QUERY SELECT v_run_id, v_registry.id, 'Failed'::text, 0::bigint, 0::bigint, v_registry.last_index_value_text, v_registry.last_index_value_text, v_duration_ms, v_message;
        RETURN;
    END IF;

    UPDATE public.source_table_dump_registry
    SET
        updated_at_utc = now(),
        stage1_status = 'Running',
        lease_owner = v_lease_owner,
        lease_until_utc = now() + make_interval(secs => p_timeout_seconds),
        last_stage1_started_at_utc = v_started_at,
        last_error = NULL
    WHERE id = v_registry.id;

    v_last_before := v_registry.last_index_value_text;
    v_index_type := public.ppiq_column_pg_type(v_registry.source_schema_name, v_registry.source_table_name, v_registry.last_index_column);

    SELECT
        string_agg(format('%I', column_name), ', ' ORDER BY ordinal_position),
        string_agg(format('s.%I', column_name), ', ' ORDER BY ordinal_position)
    INTO v_columns, v_source_columns
    FROM information_schema.columns
    WHERE table_schema = v_registry.source_schema_name
      AND table_name = v_registry.source_table_name;

    SELECT string_agg(format('d.%1$I IS NOT DISTINCT FROM s.%1$I', pk), ' AND ')
    INTO v_pk_join
    FROM unnest(v_registry.primary_key_columns) AS pk;

    IF v_last_before IS NULL THEN
        v_index_condition := 'TRUE';
    ELSE
        v_index_condition := format('s.%1$I > %2$L::%3$s', v_registry.last_index_column, v_last_before, v_index_type);
    END IF;

    v_sql := format(
        'INSERT INTO %1$I.%2$I (%3$s)
         SELECT %4$s
         FROM %5$I.%6$I s
         WHERE %7$s
           AND NOT EXISTS (
                SELECT 1
                FROM %1$I.%2$I d
                WHERE %8$s
           )
         ORDER BY s.%9$I
         LIMIT %10$s',
        v_registry.dump_schema_name,
        v_registry.dump_table_name,
        v_columns,
        v_source_columns,
        v_registry.source_schema_name,
        v_registry.source_table_name,
        v_index_condition,
        v_pk_join,
        v_registry.last_index_column,
        greatest(1, coalesce(p_max_rows, 50000))
    );

    EXECUTE v_sql;
    GET DIAGNOSTICS v_inserted_rows = ROW_COUNT;

    EXECUTE format(
        'SELECT max(%1$I)::text FROM %2$I.%3$I',
        v_registry.last_index_column,
        v_registry.dump_schema_name,
        v_registry.dump_table_name
    )
    INTO v_last_after;

    EXECUTE format(
        'SELECT count(*) FROM %1$I.%2$I',
        v_registry.dump_schema_name,
        v_registry.dump_table_name
    )
    INTO v_dump_count;

    v_completed_at := now();
    v_duration_ms := greatest(0, floor(extract(epoch FROM (v_completed_at - v_started_at)) * 1000)::bigint);
    v_message := format('Stage-1 delta import completed. Inserted %s new rows into %s.%s. Dump now has %s rows.',
        v_inserted_rows,
        v_registry.dump_schema_name,
        v_registry.dump_table_name,
        v_dump_count);

    INSERT INTO public.two_stage_import_runs
    (
        id,
        registry_id,
        run_kind,
        run_status,
        source_system_code,
        source_schema_name,
        source_table_name,
        dump_schema_name,
        dump_table_name,
        started_at_utc,
        completed_at_utc,
        duration_ms,
        inserted_rows,
        skipped_existing_rows,
        last_index_before,
        last_index_after,
        requested_by,
        message,
        result_json
    )
    VALUES
    (
        v_run_id,
        v_registry.id,
        'Stage1DeltaImport',
        'Ok',
        v_registry.source_system_code,
        v_registry.source_schema_name,
        v_registry.source_table_name,
        v_registry.dump_schema_name,
        v_registry.dump_table_name,
        v_started_at,
        v_completed_at,
        v_duration_ms,
        v_inserted_rows,
        v_skipped_rows,
        v_last_before,
        v_last_after,
        p_requested_by,
        v_message,
        jsonb_build_object('dumpRowCount', v_dump_count, 'maxRows', p_max_rows)
    );

    UPDATE public.source_table_dump_registry
    SET
        updated_at_utc = now(),
        stage1_status = 'Ok',
        last_stage1_run_id = v_run_id,
        last_stage1_completed_at_utc = v_completed_at,
        last_stage1_duration_ms = v_duration_ms,
        last_stage1_inserted_rows = v_inserted_rows,
        last_index_value_text = coalesce(v_last_after, last_index_value_text),
        last_synced_at_utc = v_completed_at,
        lease_owner = NULL,
        lease_until_utc = NULL,
        last_error = NULL
    WHERE id = v_registry.id;

    PERFORM public.ppiq_finish_phase3_job_history(
        v_history_id,
        'PPIQ_STAGE1_DELTA_IMPORT',
        'Ok',
        v_message,
        NULL,
        v_inserted_rows,
        v_skipped_rows,
        0,
        v_last_before,
        v_last_after,
        jsonb_build_object('registryId', v_registry.id, 'dumpRowCount', v_dump_count)
    );

    RETURN QUERY SELECT v_run_id, v_registry.id, 'Ok'::text, v_inserted_rows, v_skipped_rows, v_last_before, v_last_after, v_duration_ms, v_message;

EXCEPTION WHEN OTHERS THEN
    v_failure := SQLERRM;
    v_completed_at := now();
    v_duration_ms := greatest(0, floor(extract(epoch FROM (v_completed_at - v_started_at)) * 1000)::bigint);

    UPDATE public.source_table_dump_registry
    SET
        updated_at_utc = now(),
        stage1_status = 'Failed',
        last_stage1_run_id = v_run_id,
        last_stage1_completed_at_utc = v_completed_at,
        last_stage1_duration_ms = v_duration_ms,
        lease_owner = NULL,
        lease_until_utc = NULL,
        last_error = v_failure
    WHERE id = p_registry_id;

    INSERT INTO public.two_stage_import_runs
    (
        id,
        registry_id,
        run_kind,
        run_status,
        started_at_utc,
        completed_at_utc,
        duration_ms,
        requested_by,
        failure_reason,
        message
    )
    VALUES
    (
        v_run_id,
        p_registry_id,
        'Stage1DeltaImport',
        'Failed',
        v_started_at,
        v_completed_at,
        v_duration_ms,
        p_requested_by,
        v_failure,
        v_failure
    );

    IF v_history_id IS NOT NULL THEN
        PERFORM public.ppiq_finish_phase3_job_history(
            v_history_id,
            'PPIQ_STAGE1_DELTA_IMPORT',
            'Failed',
            v_failure,
            v_failure,
            0,
            0,
            0,
            v_last_before,
            v_last_after,
            jsonb_build_object('registryId', p_registry_id, 'failure', v_failure)
        );
    END IF;

    RETURN QUERY SELECT v_run_id, p_registry_id, 'Failed'::text, 0::bigint, 0::bigint, v_last_before, v_last_after, v_duration_ms, v_failure;
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_run_stage1_delta_import_all(
    p_requested_by text DEFAULT 'manual',
    p_max_rows integer DEFAULT 50000,
    p_timeout_seconds integer DEFAULT 120)
RETURNS TABLE
(
    run_id uuid,
    registry_id uuid,
    status text,
    inserted_rows bigint,
    skipped_existing_rows bigint,
    last_index_before text,
    last_index_after text,
    duration_ms bigint,
    message text
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_registry_id uuid;
BEGIN
    FOR v_registry_id IN
        SELECT id
        FROM public.source_table_dump_registry
        WHERE is_deleted = false
          AND is_active = true
        ORDER BY source_schema_name, source_table_name
    LOOP
        RETURN QUERY
        SELECT *
        FROM public.ppiq_run_stage1_delta_import(
            v_registry_id,
            p_requested_by,
            p_max_rows,
            p_timeout_seconds);
    END LOOP;
END;
$$;

-- -------------------------------------------------------------------------------------------------
-- T115  stage-2 dump -> generic/canonical schema refresh.
-- The mappings below are steel-demo mappings, but the architecture remains generic:
-- source-specific shape is isolated to dump_store and this configured stage-2 mapping layer.
-- -------------------------------------------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.ppiq_run_stage2_canonical_refresh(
    p_registry_id uuid,
    p_requested_by text DEFAULT 'manual',
    p_max_minutes integer DEFAULT 1)
RETURNS TABLE
(
    run_id uuid,
    registry_id uuid,
    status text,
    canonical_rows bigint,
    last_index_before text,
    last_index_after text,
    duration_ms bigint,
    message text
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_registry public.source_table_dump_registry%ROWTYPE;
    v_run_id uuid := gen_random_uuid();
    v_history_id uuid;
    v_started_at timestamptz := now();
    v_completed_at timestamptz;
    v_duration_ms bigint := 0;
    v_canonical_rows bigint := 0;
    v_step_rows bigint := 0;
    v_watermark_before text;
    v_watermark_after text;
    v_index_type text;
    v_index_condition text;
    v_sql text;
    v_site_id uuid;
    v_message text := '';
    v_failure text := '';
BEGIN
    SELECT *
    INTO v_registry
    FROM public.source_table_dump_registry
    WHERE id = p_registry_id
      AND is_deleted = false
      AND is_active = true
    FOR UPDATE;

    IF v_registry.id IS NULL THEN
        RAISE EXCEPTION 'Active dump registry % was not found.', p_registry_id;
    END IF;

    v_history_id := public.ppiq_start_phase3_job_history(
        'PPIQ_STAGE2_CANONICAL_REFRESH',
        'Stage 2 Canonical Refresh  Dump Store to Generic Schema',
        'CanonicalRefresh',
        'TwoStageCanonicalRefresh',
        'Stage2CanonicalRefresh',
        'Admin UI',
        p_requested_by,
        NULL,
        greatest(30, coalesce(p_max_minutes, 1) * 60),
        jsonb_build_object('registryId', p_registry_id, 'maxMinutes', p_max_minutes)
    );

    UPDATE public.source_table_dump_registry
    SET
        updated_at_utc = now(),
        stage2_status = 'Running',
        last_stage2_started_at_utc = v_started_at,
        last_error = NULL
    WHERE id = v_registry.id;

    INSERT INTO public.two_stage_processed_watermarks(registry_id, consumer_code)
    VALUES(v_registry.id, 'STAGE2_CANONICAL')
    ON CONFLICT(registry_id, consumer_code) DO NOTHING;

    SELECT last_processed_index_value_text
    INTO v_watermark_before
    FROM public.two_stage_processed_watermarks
    WHERE registry_id = v_registry.id
      AND consumer_code = 'STAGE2_CANONICAL';

    v_index_type := public.ppiq_column_pg_type(v_registry.source_schema_name, v_registry.source_table_name, v_registry.last_index_column);

    IF v_watermark_before IS NULL THEN
        v_index_condition := 'TRUE';
    ELSE
        v_index_condition := format('d.%1$I > %2$L::%3$s', v_registry.last_index_column, v_watermark_before, v_index_type);
    END IF;

    v_site_id := public.ppiq_ensure_phase3_site();

    IF v_registry.source_schema_name = 'src_meltshop_pg' AND v_registry.source_table_name = 'heats' THEN
        v_sql := format($q$
            INSERT INTO public.material_units
            (
                id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
                is_deleted, deleted_at_utc, deleted_reason,
                material_code, material_unit_type, product_family, grade_or_recipe, site_id,
                production_start_utc, production_end_utc, production_start_local, production_end_local,
                plant_time_zone_id, plant_utc_offset_minutes
            )
            SELECT
                gen_random_uuid(), now(), NULL, true, 'phase3-dump:src_meltshop_pg.heats', 'heat:' || d.heat_no,
                false, NULL, NULL,
                d.heat_no, 'Heat', 'FlatSteel', d.steel_grade, %1$L::uuid,
                d.tap_start_utc, d.tap_end_utc,
                (d.tap_start_utc + interval '60 minutes')::timestamp,
                CASE WHEN d.tap_end_utc IS NULL THEN NULL ELSE (d.tap_end_utc + interval '60 minutes')::timestamp END,
                'Europe/Berlin', 60
            FROM %2$I.%3$I d
            WHERE %4$s
              AND NOT EXISTS (
                    SELECT 1
                    FROM public.material_units m
                    WHERE m.site_id = %1$L::uuid
                      AND m.material_code = d.heat_no
                      AND m.is_deleted = false
              )
        $q$, v_site_id, v_registry.dump_schema_name, v_registry.dump_table_name, v_index_condition);

        EXECUTE v_sql;
        GET DIAGNOSTICS v_step_rows = ROW_COUNT;
        v_canonical_rows := v_canonical_rows + v_step_rows;

    ELSIF v_registry.source_schema_name = 'src_caster_oracle_shape' AND v_registry.source_table_name = 'cast_pieces' THEN
        v_sql := format($q$
            INSERT INTO public.material_units
            (
                id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
                is_deleted, deleted_at_utc, deleted_reason,
                material_code, material_unit_type, product_family, grade_or_recipe, site_id,
                production_start_utc, production_end_utc, production_start_local, production_end_local,
                plant_time_zone_id, plant_utc_offset_minutes
            )
            SELECT
                gen_random_uuid(), now(), NULL, true, 'phase3-dump:src_caster_oracle_shape.cast_pieces', 'slab:' || d.piece_id,
                false, NULL, NULL,
                d.piece_id, 'Slab', 'FlatSteel', NULL, %1$L::uuid,
                d.cut_time, d.cut_time,
                (d.cut_time + interval '60 minutes')::timestamp,
                (d.cut_time + interval '60 minutes')::timestamp,
                'Europe/Berlin', 60
            FROM %2$I.%3$I d
            WHERE %4$s
              AND NOT EXISTS (
                    SELECT 1
                    FROM public.material_units m
                    WHERE m.site_id = %1$L::uuid
                      AND m.material_code = d.piece_id
                      AND m.is_deleted = false
              )
        $q$, v_site_id, v_registry.dump_schema_name, v_registry.dump_table_name, v_index_condition);

        EXECUTE v_sql;
        GET DIAGNOSTICS v_step_rows = ROW_COUNT;
        v_canonical_rows := v_canonical_rows + v_step_rows;

        v_sql := format($q$
            INSERT INTO public.genealogy_edges
            (
                id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
                is_deleted, deleted_at_utc, deleted_reason,
                parent_material_unit_id, child_material_unit_id, relationship_type,
                effective_from_utc, effective_to_utc
            )
            SELECT
                gen_random_uuid(), now(), NULL, true, 'phase3-dump:src_caster_oracle_shape.cast_pieces', 'heat-slab:' || d.heat_no || ':' || d.piece_id,
                false, NULL, NULL,
                heat.id, slab.id, 'ProducedInto',
                d.cut_time, NULL
            FROM %1$I.%2$I d
            JOIN public.material_units heat
              ON heat.material_code = d.heat_no
             AND heat.site_id = %3$L::uuid
             AND heat.is_deleted = false
            JOIN public.material_units slab
              ON slab.material_code = d.piece_id
             AND slab.site_id = %3$L::uuid
             AND slab.is_deleted = false
            WHERE %4$s
              AND NOT EXISTS (
                    SELECT 1
                    FROM public.genealogy_edges g
                    WHERE g.parent_material_unit_id = heat.id
                      AND g.child_material_unit_id = slab.id
                      AND g.is_deleted = false
              )
        $q$, v_registry.dump_schema_name, v_registry.dump_table_name, v_site_id, v_index_condition);

        EXECUTE v_sql;
        GET DIAGNOSTICS v_step_rows = ROW_COUNT;
        v_canonical_rows := v_canonical_rows + v_step_rows;

    ELSIF v_registry.source_schema_name = 'src_hsm_oracle_shape' AND v_registry.source_table_name = 'hsm_coils' THEN
        v_sql := format($q$
            INSERT INTO public.material_units
            (
                id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
                is_deleted, deleted_at_utc, deleted_reason,
                material_code, material_unit_type, product_family, grade_or_recipe, site_id,
                production_start_utc, production_end_utc, production_start_local, production_end_local,
                plant_time_zone_id, plant_utc_offset_minutes
            )
            SELECT
                gen_random_uuid(), now(), NULL, true, 'phase3-dump:src_hsm_oracle_shape.hsm_coils', 'coil:' || d.coil_id,
                false, NULL, NULL,
                d.coil_id, 'Coil', 'FlatSteel', NULL, %1$L::uuid,
                d.rolling_start_time, d.rolling_end_time,
                (d.rolling_start_time + interval '60 minutes')::timestamp,
                CASE WHEN d.rolling_end_time IS NULL THEN NULL ELSE (d.rolling_end_time + interval '60 minutes')::timestamp END,
                'Europe/Berlin', 60
            FROM %2$I.%3$I d
            WHERE %4$s
              AND NOT EXISTS (
                    SELECT 1
                    FROM public.material_units m
                    WHERE m.site_id = %1$L::uuid
                      AND m.material_code = d.coil_id
                      AND m.is_deleted = false
              )
        $q$, v_site_id, v_registry.dump_schema_name, v_registry.dump_table_name, v_index_condition);

        EXECUTE v_sql;
        GET DIAGNOSTICS v_step_rows = ROW_COUNT;
        v_canonical_rows := v_canonical_rows + v_step_rows;

        v_sql := format($q$
            INSERT INTO public.genealogy_edges
            (
                id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
                is_deleted, deleted_at_utc, deleted_reason,
                parent_material_unit_id, child_material_unit_id, relationship_type,
                effective_from_utc, effective_to_utc
            )
            SELECT
                gen_random_uuid(), now(), NULL, true, 'phase3-dump:src_hsm_oracle_shape.hsm_coils', 'slab-coil:' || d.input_piece_id || ':' || d.coil_id,
                false, NULL, NULL,
                slab.id, coil.id, 'RolledInto',
                d.rolling_start_time, d.rolling_end_time
            FROM %1$I.%2$I d
            JOIN public.material_units slab
              ON slab.material_code = d.input_piece_id
             AND slab.site_id = %3$L::uuid
             AND slab.is_deleted = false
            JOIN public.material_units coil
              ON coil.material_code = d.coil_id
             AND coil.site_id = %3$L::uuid
             AND coil.is_deleted = false
            WHERE %4$s
              AND NOT EXISTS (
                    SELECT 1
                    FROM public.genealogy_edges g
                    WHERE g.parent_material_unit_id = slab.id
                      AND g.child_material_unit_id = coil.id
                      AND g.is_deleted = false
              )
        $q$, v_registry.dump_schema_name, v_registry.dump_table_name, v_site_id, v_index_condition);

        EXECUTE v_sql;
        GET DIAGNOSTICS v_step_rows = ROW_COUNT;
        v_canonical_rows := v_canonical_rows + v_step_rows;

    ELSIF v_registry.source_schema_name = 'src_inspection_mysql_shape' AND v_registry.source_table_name = 'parsytec_surface_defects' THEN
        v_sql := format($q$
            INSERT INTO public.quality_events
            (
                id, created_at_utc, updated_at_utc, is_synthetic, source_system, source_record_id,
                is_deleted, deleted_at_utc, deleted_reason,
                material_unit_id, defect_catalog_id, event_at_utc, event_at_local,
                plant_time_zone_id, plant_utc_offset_minutes,
                event_type, severity, decision, description
            )
            SELECT
                gen_random_uuid(), now(), NULL, true, 'phase3-dump:src_inspection_mysql_shape.parsytec_surface_defects', 'defect:' || d.defect_row_id::text,
                false, NULL, NULL,
                coil.id, NULL, d.event_time_utc, (d.event_time_utc + interval '60 minutes')::timestamp,
                'Europe/Berlin', 60,
                'Defect', d.defect_severity, NULL,
                concat_ws(' | ', d.defect_code, d.defect_name, d.defect_class, d.side_code, 'confidence=' || coalesce(d.confidence_pct::text, 'n/a'))
            FROM %1$I.%2$I d
            JOIN public.material_units coil
              ON coil.material_code = d.coil_id
             AND coil.site_id = %3$L::uuid
             AND coil.is_deleted = false
            WHERE %4$s
              AND NOT EXISTS (
                    SELECT 1
                    FROM public.quality_events q
                    WHERE q.source_system = 'phase3-dump:src_inspection_mysql_shape.parsytec_surface_defects'
                      AND q.source_record_id = 'defect:' || d.defect_row_id::text
                      AND q.is_deleted = false
              )
        $q$, v_registry.dump_schema_name, v_registry.dump_table_name, v_site_id, v_index_condition);

        EXECUTE v_sql;
        GET DIAGNOSTICS v_step_rows = ROW_COUNT;
        v_canonical_rows := v_canonical_rows + v_step_rows;
    END IF;

    EXECUTE format(
        'SELECT max(%1$I)::text FROM %2$I.%3$I',
        v_registry.last_index_column,
        v_registry.dump_schema_name,
        v_registry.dump_table_name
    )
    INTO v_watermark_after;

    UPDATE public.two_stage_processed_watermarks
    SET
        last_processed_index_value_text = coalesce(v_watermark_after, last_processed_index_value_text),
        last_processed_at_utc = now(),
        processed_rows = processed_rows + v_canonical_rows,
        updated_at_utc = now()
    WHERE registry_id = v_registry.id
      AND consumer_code = 'STAGE2_CANONICAL';

    v_completed_at := now();
    v_duration_ms := greatest(0, floor(extract(epoch FROM (v_completed_at - v_started_at)) * 1000)::bigint);

    v_message := format(
        'Stage-2 canonical refresh completed for %.%. Canonical rows inserted: %s.',
        v_registry.source_schema_name,
        v_registry.source_table_name,
        v_canonical_rows
    );

    INSERT INTO public.two_stage_import_runs
    (
        id,
        registry_id,
        run_kind,
        run_status,
        source_system_code,
        source_schema_name,
        source_table_name,
        dump_schema_name,
        dump_table_name,
        started_at_utc,
        completed_at_utc,
        duration_ms,
        canonical_rows,
        last_index_before,
        last_index_after,
        requested_by,
        message,
        result_json
    )
    VALUES
    (
        v_run_id,
        v_registry.id,
        'Stage2CanonicalRefresh',
        'Ok',
        v_registry.source_system_code,
        v_registry.source_schema_name,
        v_registry.source_table_name,
        v_registry.dump_schema_name,
        v_registry.dump_table_name,
        v_started_at,
        v_completed_at,
        v_duration_ms,
        v_canonical_rows,
        v_watermark_before,
        v_watermark_after,
        p_requested_by,
        v_message,
        jsonb_build_object('siteId', v_site_id, 'canonicalRows', v_canonical_rows)
    );

    UPDATE public.source_table_dump_registry
    SET
        updated_at_utc = now(),
        stage2_status = 'Ok',
        last_stage2_run_id = v_run_id,
        last_stage2_completed_at_utc = v_completed_at,
        last_stage2_duration_ms = v_duration_ms,
        last_stage2_canonical_rows = v_canonical_rows,
        last_error = NULL
    WHERE id = v_registry.id;

    PERFORM public.ppiq_finish_phase3_job_history(
        v_history_id,
        'PPIQ_STAGE2_CANONICAL_REFRESH',
        'Ok',
        v_message,
        NULL,
        0,
        0,
        v_canonical_rows,
        v_watermark_before,
        v_watermark_after,
        jsonb_build_object('registryId', v_registry.id, 'canonicalRows', v_canonical_rows)
    );

    RETURN QUERY SELECT v_run_id, v_registry.id, 'Ok'::text, v_canonical_rows, v_watermark_before, v_watermark_after, v_duration_ms, v_message;

EXCEPTION WHEN OTHERS THEN
    v_failure := SQLERRM;
    v_completed_at := now();
    v_duration_ms := greatest(0, floor(extract(epoch FROM (v_completed_at - v_started_at)) * 1000)::bigint);

    UPDATE public.source_table_dump_registry
    SET
        updated_at_utc = now(),
        stage2_status = 'Failed',
        last_stage2_run_id = v_run_id,
        last_stage2_completed_at_utc = v_completed_at,
        last_stage2_duration_ms = v_duration_ms,
        last_error = v_failure
    WHERE id = p_registry_id;

    INSERT INTO public.two_stage_import_runs
    (
        id,
        registry_id,
        run_kind,
        run_status,
        started_at_utc,
        completed_at_utc,
        duration_ms,
        requested_by,
        failure_reason,
        message
    )
    VALUES
    (
        v_run_id,
        p_registry_id,
        'Stage2CanonicalRefresh',
        'Failed',
        v_started_at,
        v_completed_at,
        v_duration_ms,
        p_requested_by,
        v_failure,
        v_failure
    );

    IF v_history_id IS NOT NULL THEN
        PERFORM public.ppiq_finish_phase3_job_history(
            v_history_id,
            'PPIQ_STAGE2_CANONICAL_REFRESH',
            'Failed',
            v_failure,
            v_failure,
            0,
            0,
            0,
            v_watermark_before,
            v_watermark_after,
            jsonb_build_object('registryId', p_registry_id, 'failure', v_failure)
        );
    END IF;

    RETURN QUERY SELECT v_run_id, p_registry_id, 'Failed'::text, 0::bigint, v_watermark_before, v_watermark_after, v_duration_ms, v_failure;
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_run_stage2_canonical_refresh_all(
    p_requested_by text DEFAULT 'manual',
    p_max_minutes integer DEFAULT 1)
RETURNS TABLE
(
    run_id uuid,
    registry_id uuid,
    status text,
    canonical_rows bigint,
    last_index_before text,
    last_index_after text,
    duration_ms bigint,
    message text
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_registry_id uuid;
BEGIN
    FOR v_registry_id IN
        SELECT id
        FROM public.source_table_dump_registry
        WHERE is_deleted = false
          AND is_active = true
        ORDER BY source_schema_name, source_table_name
    LOOP
        RETURN QUERY
        SELECT *
        FROM public.ppiq_run_stage2_canonical_refresh(
            v_registry_id,
            p_requested_by,
            p_max_minutes);
    END LOOP;
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_run_two_stage_full_cycle(
    p_requested_by text DEFAULT 'manual',
    p_max_rows integer DEFAULT 50000,
    p_stage1_timeout_seconds integer DEFAULT 120,
    p_stage2_max_minutes integer DEFAULT 1)
RETURNS TABLE
(
    stage text,
    run_id uuid,
    registry_id uuid,
    status text,
    affected_rows bigint,
    last_index_before text,
    last_index_after text,
    duration_ms bigint,
    message text
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT
        'Stage1DeltaImport'::text,
        s.run_id,
        s.registry_id,
        s.status,
        s.inserted_rows,
        s.last_index_before,
        s.last_index_after,
        s.duration_ms,
        s.message
    FROM public.ppiq_run_stage1_delta_import_all(
        p_requested_by,
        p_max_rows,
        p_stage1_timeout_seconds) s;

    RETURN QUERY
    SELECT
        'Stage2CanonicalRefresh'::text,
        s.run_id,
        s.registry_id,
        s.status,
        s.canonical_rows,
        s.last_index_before,
        s.last_index_after,
        s.duration_ms,
        s.message
    FROM public.ppiq_run_stage2_canonical_refresh_all(
        p_requested_by,
        p_stage2_max_minutes) s;
END;
$$;

-- -------------------------------------------------------------------------------------------------
-- Baseline registration for Phase-1 demo source-shaped tables.
-- This is demo configuration, not hard-coded product scope.
-- -------------------------------------------------------------------------------------------------

DO $$
BEGIN
    PERFORM public.ppiq_register_dump_source('MELTSHOP_PG', 'src_meltshop_pg', 'heats', ARRAY['heat_no'], 'source_updated_at_utc', 2, 30);
    PERFORM public.ppiq_register_dump_source('MELTSHOP_PG', 'src_meltshop_pg', 'lf_treatment', ARRAY['treatment_id'], 'source_updated_at_utc', 2, 30);

    PERFORM public.ppiq_register_dump_source('CASTER_ORACLE', 'src_caster_oracle_shape', 'cast_sequence', ARRAY['sequence_no'], 'last_update_ts', 2, 30);
    PERFORM public.ppiq_register_dump_source('CASTER_ORACLE', 'src_caster_oracle_shape', 'cast_pieces', ARRAY['piece_id'], 'last_update_ts', 2, 30);

    PERFORM public.ppiq_register_dump_source('HSM_ORACLE', 'src_hsm_oracle_shape', 'hsm_coils', ARRAY['coil_id'], 'last_update_ts', 2, 30);
    PERFORM public.ppiq_register_dump_source('HSM_ORACLE', 'src_hsm_oracle_shape', 'hsm_pass_measurements', ARRAY['measurement_id'], 'last_update_ts', 2, 30);

    PERFORM public.ppiq_register_dump_source('PICKLING_MSSQL', 'src_pkl_mssql_shape', 'pickle_orders', ARRAY['order_id'], 'modified_at_utc', 2, 30);
    PERFORM public.ppiq_register_dump_source('PICKLING_MSSQL', 'src_pkl_mssql_shape', 'qa_lab_results', ARRAY['lab_result_id'], 'modified_at_utc', 2, 30);

    PERFORM public.ppiq_register_dump_source('INSPECTION_MYSQL', 'src_inspection_mysql_shape', 'parsytec_surface_defects', ARRAY['defect_row_id'], 'updated_at_utc', 2, 30);
    PERFORM public.ppiq_register_dump_source('INSPECTION_MYSQL', 'src_inspection_mysql_shape', 'downtime_events', ARRAY['downtime_id'], 'updated_at_utc', 2, 30);
END $$;

-- -------------------------------------------------------------------------------------------------
-- Dump-based views for Phase 03.
-- -------------------------------------------------------------------------------------------------

CREATE OR REPLACE VIEW public.v_phase3_dump_material_genealogy_join AS
SELECT
    h.heat_no,
    cp.piece_id AS slab_id,
    hc.coil_id,
    h.steel_grade,
    h.furnace_code,
    cp.caster_id,
    cp.strand_no,
    hc.mill_line,
    h.tap_start_utc,
    cp.cut_time AS caster_cut_time_utc,
    hc.rolling_start_time,
    hc.rolling_end_time,
    cp.width_mm AS slab_width_mm,
    cp.thickness_mm AS slab_thickness_mm,
    hc.actual_width_mm AS coil_width_mm,
    hc.actual_thickness_mm AS coil_thickness_mm,
    hc.actual_fdt_c,
    hc.actual_ct_c
FROM dump_store.src_meltshop_pg_heats h
JOIN dump_store.src_caster_oracle_shape_cast_pieces cp
    ON cp.heat_no = h.heat_no
LEFT JOIN dump_store.src_hsm_oracle_shape_hsm_coils hc
    ON hc.input_piece_id = cp.piece_id;

CREATE OR REPLACE VIEW public.v_phase3_dump_surface_defect_join AS
SELECT
    hc.coil_id,
    hc.input_piece_id AS slab_id,
    hc.heat_no,
    hc.mill_line,
    psd.defect_code,
    psd.defect_name,
    psd.defect_class,
    psd.defect_severity,
    psd.side_code,
    psd.position_start_m,
    psd.position_end_m,
    psd.confidence_pct,
    psd.event_time_utc,
    hc.actual_fdt_c,
    hc.actual_ct_c,
    hc.actual_thickness_mm,
    hc.actual_width_mm
FROM dump_store.src_hsm_oracle_shape_hsm_coils hc
JOIN dump_store.src_inspection_mysql_shape_parsytec_surface_defects psd
    ON psd.coil_id = hc.coil_id;

CREATE OR REPLACE VIEW public.v_phase3_dump_kpi_quality_temperature_window AS
SELECT
    hc.mill_line,
    date_trunc('day', hc.rolling_start_time) AS production_day_utc,
    COUNT(*) AS coil_count,
    AVG(hc.actual_fdt_c) AS avg_fdt_c,
    AVG(hc.actual_ct_c) AS avg_ct_c,
    COUNT(psd.defect_row_id) AS defect_count,
    CASE
        WHEN COUNT(*) = 0 THEN 0
        ELSE ROUND(COUNT(psd.defect_row_id)::numeric / COUNT(*)::numeric * 100, 4)
    END AS defects_per_100_coils,
    MIN(hc.rolling_start_time) AS first_rolling_time_utc,
    MAX(hc.rolling_end_time) AS last_rolling_time_utc
FROM dump_store.src_hsm_oracle_shape_hsm_coils hc
LEFT JOIN dump_store.src_inspection_mysql_shape_parsytec_surface_defects psd
    ON psd.coil_id = hc.coil_id
GROUP BY
    hc.mill_line,
    date_trunc('day', hc.rolling_start_time);

-- Register Phase 03 dump views in the Phase 02 canonical catalog when available.
DO $$
BEGIN
    IF to_regclass('public.canonical_schema_views') IS NOT NULL THEN
        IF EXISTS (
            SELECT 1
            FROM pg_proc
            WHERE proname = 'ppiq_register_canonical_schema_view'
        ) THEN
            PERFORM public.ppiq_register_canonical_schema_view(
                'PPIQ_PHASE3_DUMP_MATERIAL_GENEALOGY',
                'Phase 03 Dump Material Genealogy View',
                'MaterialGenealogy',
                'MaterialGenealogy',
                'public',
                'v_phase3_dump_material_genealogy_join',
                'SELECT * FROM public.v_phase3_dump_material_genealogy_join',
                jsonb_build_object('source', 'dump_store', 'stage', 'Phase03'),
                jsonb_build_object('columns', ARRAY['heat_no','slab_id','coil_id','steel_grade','furnace_code','caster_id','mill_line']),
                jsonb_build_array()
            );

            PERFORM public.ppiq_register_canonical_schema_view(
                'PPIQ_PHASE3_DUMP_SURFACE_DEFECT_JOIN',
                'Phase 03 Dump Surface Defect View',
                'QualityDefect',
                'QualityEvent',
                'public',
                'v_phase3_dump_surface_defect_join',
                'SELECT * FROM public.v_phase3_dump_surface_defect_join',
                jsonb_build_object('source', 'dump_store', 'stage', 'Phase03'),
                jsonb_build_object('columns', ARRAY['coil_id','defect_code','defect_name','defect_severity','event_time_utc']),
                jsonb_build_array()
            );

            PERFORM public.ppiq_register_canonical_schema_view(
                'PPIQ_PHASE3_DUMP_KPI_QUALITY_TEMPERATURE',
                'Phase 03 Dump KPI Temperature / Quality Window',
                'KpiView',
                'KpiDefinition',
                'public',
                'v_phase3_dump_kpi_quality_temperature_window',
                'SELECT * FROM public.v_phase3_dump_kpi_quality_temperature_window',
                jsonb_build_object('source', 'dump_store', 'stage', 'Phase03'),
                jsonb_build_object('columns', ARRAY['mill_line','production_day_utc','coil_count','avg_fdt_c','avg_ct_c','defect_count']),
                jsonb_build_array()
            );
        END IF;
    END IF;
END $$;

-- Ensure Phase 03 global jobs exist.
DO $$
BEGIN
    PERFORM public.ppiq_upsert_phase3_job(
        'PPIQ_STAGE1_DELTA_IMPORT',
        'Stage 1 Delta Import  Source to Dump Store',
        'DbLinkImport',
        'TwoStageDeltaImport',
        'Stage1DeltaImport',
        'Every 2 minutes',
        'Copies only new/changed source records into a source-shaped dump-copy table.'
    );

    PERFORM public.ppiq_upsert_phase3_job(
        'PPIQ_STAGE2_CANONICAL_REFRESH',
        'Stage 2 Canonical Refresh  Dump Store to Generic Schema',
        'CanonicalRefresh',
        'TwoStageCanonicalRefresh',
        'Stage2CanonicalRefresh',
        'Every 30 seconds',
        'Refreshes the generic/canonical schema from dump-copy tables using processed watermarks.'
    );

    PERFORM public.ppiq_upsert_phase3_job(
        'PPIQ_TWO_STAGE_FULL_CYCLE',
        'Two-Stage Import Full Cycle',
        'Custom',
        'TwoStageFullCycle',
        'TwoStageFullCycle',
        'Manual / On demand',
        'Runs Stage 1 and Stage 2 together for demo and validation.'
    );
END $$;

COMMIT;

SELECT
    'PPIQ-T113-T118 Phase 03 two-stage delta import foundation ready' AS status,
    (SELECT count(*) FROM public.source_table_dump_registry WHERE is_deleted = false) AS registered_source_tables,
    (SELECT count(*) FROM information_schema.tables WHERE table_schema = 'dump_store') AS dump_tables;