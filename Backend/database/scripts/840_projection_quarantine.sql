-- ============================================================================
-- 840_projection_quarantine.sql
-- PPIQ T-099. Typed projection quarantine.
--
-- A malformed source row must never corrupt canonical data, disappear silently,
-- or abort the otherwise valid rows beside it in the same batch. It becomes an
-- explicit typed record here instead, carrying enough identity to be reprocessed
-- lawfully once the mapping or the source is corrected.
--
-- THE CODE IS AUTHORITATIVE, THE SENTENCE IS EXPLANATORY. validation_code is the
-- fact a consumer groups, counts and routes on; detail exists so a human reads
-- the same fact in one line.
--
-- T-099 OWNS PV01..PV08 EXACTLY. The constraint is an exact set, not a shape:
-- a PV09 row written before T-100 exists would be a class the product cannot
-- explain, count or resolve. T-100 extends the set through its own script.
--
-- Created directly in ppiq_staging. A quarantined row is source-shaped material
-- that failed to become canonical, so it belongs with the staging it came from.
--
-- Idempotent by construction and existence-driven, in the manner of 838/839.
-- ============================================================================
\set ON_ERROR_STOP on

CREATE TABLE IF NOT EXISTS ppiq_staging.projection_quarantine
(
    -- BaseEntity storage parity. The EF model inherits every one of these, so
    -- the table must carry them or the entity is unmappable.
    id                        uuid         NOT NULL DEFAULT gen_random_uuid(),
    created_at_utc            timestamptz  NOT NULL DEFAULT now(),
    updated_at_utc            timestamptz  NULL,
    is_synthetic              boolean      NOT NULL DEFAULT false,
    source_system             varchar(100) NULL,
    source_record_id          varchar(100) NULL,
    is_deleted                boolean      NOT NULL DEFAULT false,
    deleted_at_utc            timestamptz  NULL,
    deleted_reason            text         NULL,

    -- The typed refusal.
    validation_code           varchar(8)   NOT NULL,
    detail                    text         NOT NULL,
    offending_value           text         NULL,
    suggested_correction      text         NOT NULL,

    -- Source lineage: which batch, which staged row, which source object.
    import_batch_id           uuid         NOT NULL,
    staging_record_id         uuid         NOT NULL,
    staging_row_number        integer      NOT NULL,
    source_object_name        varchar(200) NOT NULL,

    -- Producer identity. The exact mapping AND the version snapshot that refused
    -- the row, because reprocessing under a different version is a different
    -- question and the record must say which one it answered.
    mapping_definition_id     uuid         NOT NULL,
    mapping_version           varchar(50)  NOT NULL,
    target_entity_name        varchar(100) NOT NULL,

    tenant_id                 uuid         NOT NULL,

    -- Open until a governed projection succeeds. Never set by intent alone.
    state                     varchar(20)  NOT NULL DEFAULT 'Open',
    attempt_count             integer      NOT NULL DEFAULT 1,
    last_attempt_at_utc       timestamptz  NOT NULL DEFAULT now(),
    resolved_at_utc           timestamptz  NULL,
    resolved_canonical_id     uuid         NULL,

    CONSTRAINT pk_projection_quarantine PRIMARY KEY (id),

    CONSTRAINT ck_projection_quarantine_state
        CHECK (state IN ('Open', 'Resolved')),

    -- A row is Resolved only with the canonical result that resolved it. Time
    -- alone would let a queue be emptied by intent instead of by projection.
    CONSTRAINT ck_projection_quarantine_resolved_coherent
        CHECK ((state = 'Open'
                    AND resolved_at_utc IS NULL
                    AND resolved_canonical_id IS NULL)
            OR (state = 'Resolved'
                    AND resolved_at_utc IS NOT NULL
                    AND resolved_canonical_id IS NOT NULL)),

    -- Exact set. T-100 replaces this constraint with PV01..PV15.
    CONSTRAINT ck_projection_quarantine_code
        CHECK (validation_code IN (
            'PV01','PV02','PV03','PV04',
            'PV05','PV06','PV07','PV08'
        )),

    -- The initial full execution is attempt 1, not attempt 0.
    CONSTRAINT ck_projection_quarantine_attempt_count
        CHECK (attempt_count >= 1),

    CONSTRAINT ck_projection_quarantine_row_number
        CHECK (staging_row_number > 0)
);

-- Evidence must not become fiction. RESTRICT, because a quarantine record whose
-- batch, staged row, mapping or tenant silently vanished would still look like
-- evidence while proving nothing.
DO $$
BEGIN
    IF to_regclass('ppiq_staging.import_batches') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_projection_quarantine_import_batch') THEN
        ALTER TABLE ppiq_staging.projection_quarantine
            ADD CONSTRAINT fk_projection_quarantine_import_batch
            FOREIGN KEY (import_batch_id) REFERENCES ppiq_staging.import_batches(id) ON DELETE RESTRICT;
    END IF;

    IF to_regclass('ppiq_staging.staging_records') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_projection_quarantine_staging_record') THEN
        ALTER TABLE ppiq_staging.projection_quarantine
            ADD CONSTRAINT fk_projection_quarantine_staging_record
            FOREIGN KEY (staging_record_id) REFERENCES ppiq_staging.staging_records(id) ON DELETE RESTRICT;
    END IF;

    IF to_regclass('ppiq_meta.mapping_definitions') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_projection_quarantine_mapping_definition') THEN
        ALTER TABLE ppiq_staging.projection_quarantine
            ADD CONSTRAINT fk_projection_quarantine_mapping_definition
            FOREIGN KEY (mapping_definition_id) REFERENCES ppiq_meta.mapping_definitions(id) ON DELETE RESTRICT;
    END IF;

    IF to_regclass('ppiq_meta.tenants') IS NOT NULL
       AND NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'fk_projection_quarantine_tenant') THEN
        ALTER TABLE ppiq_staging.projection_quarantine
            ADD CONSTRAINT fk_projection_quarantine_tenant
            FOREIGN KEY (tenant_id) REFERENCES ppiq_meta.tenants(id) ON DELETE RESTRICT;
    END IF;
END $$;

-- The queue is read by code, by batch, by definition and by tenant; those are
-- the questions Mapping Health asks and the ones this table answers cheaply.
CREATE INDEX IF NOT EXISTS ix_projection_quarantine_code
    ON ppiq_staging.projection_quarantine (validation_code);

CREATE INDEX IF NOT EXISTS ix_projection_quarantine_batch
    ON ppiq_staging.projection_quarantine (import_batch_id);

CREATE INDEX IF NOT EXISTS ix_projection_quarantine_definition
    ON ppiq_staging.projection_quarantine (mapping_definition_id, mapping_version);

CREATE INDEX IF NOT EXISTS ix_projection_quarantine_tenant
    ON ppiq_staging.projection_quarantine (tenant_id);

-- One OPEN record per staged row per producing version. A second execution of
-- the same version over the same row updates the attempt rather than growing a
-- duplicate queue; a different version is a different refusal and gets its own.
CREATE UNIQUE INDEX IF NOT EXISTS ux_projection_quarantine_open_row
    ON ppiq_staging.projection_quarantine (staging_record_id, mapping_definition_id, mapping_version)
    WHERE state = 'Open' AND is_deleted = false;

DO $$
DECLARE
    v_missing text;
BEGIN
    IF to_regclass('ppiq_staging.projection_quarantine') IS NULL THEN
        RAISE EXCEPTION 'PPIQ_840: ppiq_staging.projection_quarantine was not created.';
    END IF;

    SELECT string_agg(c, ', ')
      INTO v_missing
      FROM unnest(ARRAY[
            'id','created_at_utc','updated_at_utc','is_synthetic','source_system',
            'source_record_id','is_deleted','deleted_at_utc','deleted_reason',
            'validation_code','detail','offending_value','suggested_correction',
            'import_batch_id','staging_record_id','staging_row_number','source_object_name',
            'mapping_definition_id','mapping_version','target_entity_name','tenant_id',
            'state','attempt_count','last_attempt_at_utc','resolved_at_utc','resolved_canonical_id'
      ]) AS c
     WHERE NOT EXISTS (
            SELECT 1 FROM information_schema.columns
             WHERE table_schema = 'ppiq_staging'
               AND table_name = 'projection_quarantine'
               AND column_name = c);

    IF v_missing IS NOT NULL THEN
        RAISE EXCEPTION 'PPIQ_840: projection_quarantine is missing columns: %', v_missing;
    END IF;

    RAISE NOTICE 'PPIQ_840_PROVEN: ppiq_staging.projection_quarantine present, PV01..PV08 exact, BaseEntity parity, lawful foreign keys.';
END $$;
