\set ON_ERROR_STOP on
\qecho PPIQ_PSQL_EXECUTED 845_source_time_authority_persistence

-- ============================================================================
-- PlantProcess IQ - Source Time Authority persistence (backlog reference T-233)
--
-- Persists the frozen Source Time Authority contract exactly as the analytics
-- kernel declares it: one declaration per timestamp SIGNAL on a declared
-- source, carrying the single role that signal answers, where its offset comes
-- from, its declared zone authority, its resolution, its maximum clock skew
-- and the uncertainty convention. Source, signal and zone are opaque declared
-- keys. A site is expressed through the declared source key; no site column
-- is introduced, because the contract carries none.
--
-- Declaration invariants are the kernel's: trim only, exact case, identical
-- redeclaration returns the existing identity, a different declaration is
-- ST07. Durations are stored as exact 100-nanosecond ticks so a persisted
-- declaration round-trips to an equal contract value.
--
-- Observation time provenance: parameter observations keep the timestamp the
-- source produced, the timestamp a historian or server produced, and the
-- instant this product received the value. Existing rows take their receipt
-- time from created_at_utc, which is when the row was written. No alignment
-- algorithm lives here, and nothing is seeded.
-- ============================================================================

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE SCHEMA IF NOT EXISTS ppiq_meta;

DO $predecessor$
BEGIN
    IF to_regclass('ppiq_meta.tenants') IS NULL THEN
        RAISE EXCEPTION
            'PPIQ_SOURCE_TIME_PREDECESSOR_MISSING: ppiq_meta.tenants must exist before source time authority persistence.';
    END IF;
    IF to_regclass('ppiq_plant.parameter_observations') IS NULL THEN
        RAISE EXCEPTION
            'PPIQ_SOURCE_TIME_PREDECESSOR_MISSING: ppiq_plant.parameter_observations must exist (topology convergence runs first).';
    END IF;
END
$predecessor$;

CREATE TABLE IF NOT EXISTS ppiq_meta.source_time_authorities
(
    id                          uuid         NOT NULL DEFAULT gen_random_uuid(),
    tenant_id                   uuid         NOT NULL,
    source_key                  varchar(200) NOT NULL,
    signal_key                  varchar(200) NOT NULL,
    time_role                   varchar(16)  NOT NULL,
    offset_origin               varchar(24)  NOT NULL,
    fixed_offset_ticks          bigint       NOT NULL DEFAULT 0,
    zone_key                    varchar(200) NULL,
    resolution_ticks            bigint       NOT NULL,
    max_clock_skew_ticks        bigint       NOT NULL,
    uncertainty_convention      varchar(32)  NOT NULL,
    effective_from_utc          timestamptz  NOT NULL,
    effective_to_utc            timestamptz  NULL,
    created_at_utc              timestamptz  NOT NULL DEFAULT now(),
    created_by                  uuid         NULL,
    source_system               varchar(100) NULL,
    source_record_id            varchar(200) NULL,

    CONSTRAINT pk_source_time_authorities PRIMARY KEY (id),
    CONSTRAINT fk_source_time_authority_tenant FOREIGN KEY (tenant_id)
        REFERENCES ppiq_meta.tenants (id) ON DELETE RESTRICT,
    CONSTRAINT ux_source_time_authority_tenant_signal_effective
        UNIQUE (tenant_id, source_key, signal_key, effective_from_utc),
    CONSTRAINT ck_source_time_authority_source_key CHECK
        (source_key <> '' AND source_key = btrim(source_key)),
    CONSTRAINT ck_source_time_authority_signal_key CHECK
        (signal_key <> '' AND signal_key = btrim(signal_key)),
    CONSTRAINT ck_source_time_authority_role CHECK
        (time_role IN ('SourceAsserted','Effective','Ingestion')),
    CONSTRAINT ck_source_time_authority_offset_origin CHECK
        (offset_origin IN ('EmbeddedInValue','DeclaredFixedOffset','DeclaredZoneRule')),
    CONSTRAINT ck_source_time_authority_fixed_offset_range CHECK
        (fixed_offset_ticks BETWEEN -504000000000 AND 504000000000),
    CONSTRAINT ck_source_time_authority_zone CHECK
        ((offset_origin = 'DeclaredZoneRule'
              AND zone_key IS NOT NULL AND zone_key <> '' AND zone_key = btrim(zone_key))
         OR (offset_origin <> 'DeclaredZoneRule' AND zone_key IS NULL)),
    CONSTRAINT ck_source_time_authority_quality CHECK
        (resolution_ticks >= 0 AND max_clock_skew_ticks >= 0),
    CONSTRAINT ck_source_time_authority_convention CHECK
        (uncertainty_convention IN ('ResolutionIsHalfWidth','ResolutionIsQuantisationStep')),
    CONSTRAINT ck_source_time_authority_effective_window CHECK
        (effective_to_utc IS NULL OR effective_to_utc > effective_from_utc)
);

CREATE INDEX IF NOT EXISTS ix_source_time_authority_tenant_signal
    ON ppiq_meta.source_time_authorities (tenant_id, source_key, signal_key, effective_from_utc DESC);

-- ---------------------------------------------------------------------------
-- The one declaration function. Mirrors the kernel registry's refusals so the
-- database refuses what the contract refuses, even for a writer that bypasses
-- the application. Overlapping effective windows for one signal are a
-- conflict unless the declaration and the window are identical.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION ppiq_meta.declare_source_time_signal
(
    p_tenant_id                 uuid,
    p_source_key                text,
    p_signal_key                text,
    p_time_role                 text,
    p_offset_origin             text,
    p_fixed_offset_ticks        bigint,
    p_zone_key                  text,
    p_resolution_ticks          bigint,
    p_max_clock_skew_ticks      bigint,
    p_uncertainty_convention    text,
    p_effective_from_utc        timestamptz,
    p_effective_to_utc          timestamptz,
    p_created_by                uuid DEFAULT NULL,
    p_source_system             text DEFAULT NULL,
    p_source_record_id          text DEFAULT NULL
)
RETURNS uuid
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, ppiq_meta
AS $fn$
DECLARE
    v_source    text := btrim(COALESCE(p_source_key, ''));
    v_signal    text := btrim(COALESCE(p_signal_key, ''));
    v_zone      text := NULLIF(btrim(COALESCE(p_zone_key, '')), '');
    v_origin    text := btrim(COALESCE(p_offset_origin, ''));
    v_fixed     bigint := COALESCE(p_fixed_offset_ticks, 0);
    v_existing  ppiq_meta.source_time_authorities%ROWTYPE;
    v_result    uuid;
BEGIN
    IF p_tenant_id IS NULL OR v_source = '' OR v_signal = '' THEN
        RAISE EXCEPTION 'ST01 time_signal_not_declared: tenant, source key and signal key are required.'
            USING ERRCODE = 'P0001';
    END IF;

    IF v_origin = '' OR v_origin = 'Undeclared' THEN
        RAISE EXCEPTION 'ST02 time_offset_not_declared: signal % on source % declares no offset origin.', v_signal, v_source
            USING ERRCODE = 'P0001';
    END IF;

    IF p_time_role NOT IN ('SourceAsserted','Effective','Ingestion') THEN
        RAISE EXCEPTION 'ST03 time_role_not_authorised: signal % on source % declares unknown role %.', v_signal, v_source, p_time_role
            USING ERRCODE = 'P0001';
    END IF;

    IF v_origin NOT IN ('EmbeddedInValue','DeclaredFixedOffset','DeclaredZoneRule') THEN
        RAISE EXCEPTION 'ST02 time_offset_not_declared: signal % on source % declares unknown offset origin %.', v_signal, v_source, v_origin
            USING ERRCODE = 'P0001';
    END IF;

    IF p_uncertainty_convention NOT IN ('ResolutionIsHalfWidth','ResolutionIsQuantisationStep') THEN
        RAISE EXCEPTION 'ST05 time_quality_not_declared: signal % on source % declares unknown uncertainty convention %.', v_signal, v_source, p_uncertainty_convention
            USING ERRCODE = 'P0001';
    END IF;

    IF v_origin = 'DeclaredZoneRule' AND v_zone IS NULL THEN
        RAISE EXCEPTION 'ST11 zone_authority_not_declared: signal % on source % declares a zone rule without a zone key.', v_signal, v_source
            USING ERRCODE = 'P0001';
    END IF;

    IF v_origin <> 'DeclaredZoneRule' AND v_zone IS NOT NULL THEN
        RAISE EXCEPTION 'ST09 offset_declaration_conflict: signal % on source % declares a zone key without a zone rule.', v_signal, v_source
            USING ERRCODE = 'P0001';
    END IF;

    IF v_origin = 'DeclaredFixedOffset' AND (v_fixed < -504000000000 OR v_fixed > 504000000000) THEN
        RAISE EXCEPTION 'ST09 offset_declaration_conflict: fixed offset of signal % on source % is outside the fourteen hour range.', v_signal, v_source
            USING ERRCODE = 'P0001';
    END IF;

    IF p_resolution_ticks IS NULL OR p_max_clock_skew_ticks IS NULL
       OR p_resolution_ticks < 0 OR p_max_clock_skew_ticks < 0 THEN
        RAISE EXCEPTION 'ST05 time_quality_not_declared: signal % on source % declares no valid resolution and clock skew.', v_signal, v_source
            USING ERRCODE = 'P0001';
    END IF;

    IF p_effective_from_utc IS NULL
       OR (p_effective_to_utc IS NOT NULL AND p_effective_to_utc <= p_effective_from_utc) THEN
        RAISE EXCEPTION 'STP01 effective_window_invalid: signal % on source % needs an effective start before its end.', v_signal, v_source
            USING ERRCODE = 'P0001';
    END IF;

    -- One declaration at a time per signal, so two concurrent writers cannot
    -- both pass the overlap check.
    PERFORM pg_advisory_xact_lock(
        hashtextextended(p_tenant_id::text || '|' || v_source || '|' || v_signal, 8675309001));

    SELECT *
      INTO v_existing
      FROM ppiq_meta.source_time_authorities a
     WHERE a.tenant_id = p_tenant_id
       AND a.source_key = v_source
       AND a.signal_key = v_signal
       AND a.effective_from_utc = p_effective_from_utc;

    IF FOUND THEN
        IF v_existing.time_role = p_time_role
           AND v_existing.offset_origin = v_origin
           AND v_existing.fixed_offset_ticks = v_fixed
           AND v_existing.zone_key IS NOT DISTINCT FROM v_zone
           AND v_existing.resolution_ticks = p_resolution_ticks
           AND v_existing.max_clock_skew_ticks = p_max_clock_skew_ticks
           AND v_existing.uncertainty_convention = p_uncertainty_convention
           AND v_existing.effective_to_utc IS NOT DISTINCT FROM p_effective_to_utc THEN
            RETURN v_existing.id;
        END IF;

        RAISE EXCEPTION 'ST07 conflicting_declaration: signal % on source % already has a different declaration at %.',
            v_signal, v_source, p_effective_from_utc
            USING ERRCODE = 'P0001';
    END IF;

    IF EXISTS
    (
        SELECT 1
          FROM ppiq_meta.source_time_authorities a
         WHERE a.tenant_id = p_tenant_id
           AND a.source_key = v_source
           AND a.signal_key = v_signal
           AND a.effective_from_utc < COALESCE(p_effective_to_utc, 'infinity'::timestamptz)
           AND COALESCE(a.effective_to_utc, 'infinity'::timestamptz) > p_effective_from_utc
    ) THEN
        RAISE EXCEPTION 'ST07 conflicting_declaration: signal % on source % already has a declaration in force within this window.',
            v_signal, v_source
            USING ERRCODE = 'P0001';
    END IF;

    INSERT INTO ppiq_meta.source_time_authorities
    (
        tenant_id, source_key, signal_key, time_role, offset_origin,
        fixed_offset_ticks, zone_key, resolution_ticks, max_clock_skew_ticks,
        uncertainty_convention, effective_from_utc, effective_to_utc,
        created_by, source_system, source_record_id
    )
    VALUES
    (
        p_tenant_id, v_source, v_signal, p_time_role, v_origin,
        v_fixed, v_zone, p_resolution_ticks, p_max_clock_skew_ticks,
        p_uncertainty_convention, p_effective_from_utc, p_effective_to_utc,
        p_created_by,
        NULLIF(btrim(COALESCE(p_source_system, '')), ''),
        NULLIF(btrim(COALESCE(p_source_record_id, '')), '')
    )
    RETURNING id INTO v_result;

    RETURN v_result;
END
$fn$;

-- Declaration is the only runtime write surface. PUBLIC never receives it implicitly.
REVOKE ALL ON FUNCTION ppiq_meta.declare_source_time_signal(
    uuid, text, text, text, text, bigint, text, bigint, bigint, text,
    timestamptz, timestamptz, uuid, text, text) FROM PUBLIC;

-- ---------------------------------------------------------------------------
-- Observation time provenance. Added without a default first, so existing
-- rows are not stamped with the migration instant; their receipt time is the
-- instant the row was written.
-- ---------------------------------------------------------------------------
ALTER TABLE ppiq_plant.parameter_observations
    ADD COLUMN IF NOT EXISTS source_timestamp_utc timestamptz NULL;
ALTER TABLE ppiq_plant.parameter_observations
    ADD COLUMN IF NOT EXISTS server_timestamp_utc timestamptz NULL;
ALTER TABLE ppiq_plant.parameter_observations
    ADD COLUMN IF NOT EXISTS ingested_at_utc timestamptz NULL;

UPDATE ppiq_plant.parameter_observations
   SET ingested_at_utc = created_at_utc
 WHERE ingested_at_utc IS NULL;

ALTER TABLE ppiq_plant.parameter_observations
    ALTER COLUMN ingested_at_utc SET DEFAULT now();
ALTER TABLE ppiq_plant.parameter_observations
    ALTER COLUMN ingested_at_utc SET NOT NULL;

-- ---------------------------------------------------------------------------
-- Drift refusal. IF NOT EXISTS is not evidence that a pre-existing object has
-- this contract.
-- ---------------------------------------------------------------------------
DO $shape$
DECLARE
    v_missing text[];
BEGIN
    SELECT array_agg(required.column_name ORDER BY required.column_name)
      INTO v_missing
      FROM (VALUES
        ('id'),('tenant_id'),('source_key'),('signal_key'),('time_role'),('offset_origin'),
        ('fixed_offset_ticks'),('zone_key'),('resolution_ticks'),('max_clock_skew_ticks'),
        ('uncertainty_convention'),('effective_from_utc'),('effective_to_utc')
      ) AS required(column_name)
     WHERE NOT EXISTS
     (
         SELECT 1 FROM information_schema.columns c
          WHERE c.table_schema = 'ppiq_meta'
            AND c.table_name = 'source_time_authorities'
            AND c.column_name = required.column_name
     );
    IF v_missing IS NOT NULL THEN
        RAISE EXCEPTION 'PPIQ_SOURCE_TIME_DRIFT: source_time_authorities missing columns %', v_missing;
    END IF;

    SELECT array_agg(required.column_name ORDER BY required.column_name)
      INTO v_missing
      FROM (VALUES ('source_timestamp_utc'),('server_timestamp_utc'),('ingested_at_utc')) AS required(column_name)
     WHERE NOT EXISTS
     (
         SELECT 1 FROM information_schema.columns c
          WHERE c.table_schema = 'ppiq_plant'
            AND c.table_name = 'parameter_observations'
            AND c.column_name = required.column_name
     );
    IF v_missing IS NOT NULL THEN
        RAISE EXCEPTION 'PPIQ_SOURCE_TIME_DRIFT: parameter_observations missing time provenance columns %', v_missing;
    END IF;

    IF NOT EXISTS
    (
        SELECT 1 FROM information_schema.columns c
         WHERE c.table_schema = 'ppiq_plant'
           AND c.table_name = 'parameter_observations'
           AND c.column_name = 'ingested_at_utc'
           AND c.is_nullable = 'NO'
    ) THEN
        RAISE EXCEPTION 'PPIQ_SOURCE_TIME_DRIFT: parameter_observations.ingested_at_utc must be NOT NULL.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ux_source_time_authority_tenant_signal_effective') THEN
        RAISE EXCEPTION 'PPIQ_SOURCE_TIME_DRIFT: tenant-scoped signal identity constraint is missing.';
    END IF;
END
$shape$;

-- ---------------------------------------------------------------------------
-- Runtime role, guarded so the script replays where the role is absent.
-- Direct table DML is forbidden. Declarations go through the governed function.
-- ---------------------------------------------------------------------------
DO $grants$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plantprocess_app') THEN
        GRANT USAGE ON SCHEMA ppiq_meta TO plantprocess_app;
        REVOKE INSERT, UPDATE, DELETE ON ppiq_meta.source_time_authorities FROM plantprocess_app;
        GRANT SELECT ON ppiq_meta.source_time_authorities TO plantprocess_app;
        GRANT EXECUTE ON FUNCTION ppiq_meta.declare_source_time_signal(
            uuid, text, text, text, text, bigint, text, bigint, bigint, text,
            timestamptz, timestamptz, uuid, text, text) TO plantprocess_app;
    END IF;
END
$grants$;

COMMENT ON TABLE ppiq_meta.source_time_authorities IS
    'PPIQ Source Time Authority. Tenant-scoped, effective-dated persistence of the frozen per-signal time contract. Starts empty; nothing is seeded.';
COMMENT ON COLUMN ppiq_plant.parameter_observations.source_timestamp_utc IS
    'Timestamp produced by the source or device, when the source supplies one.';
COMMENT ON COLUMN ppiq_plant.parameter_observations.server_timestamp_utc IS
    'Timestamp produced by a historian or server, when distinct from the source timestamp.';
COMMENT ON COLUMN ppiq_plant.parameter_observations.ingested_at_utc IS
    'Instant this product received the value. Platform-owned; never an authored field.';

COMMIT;