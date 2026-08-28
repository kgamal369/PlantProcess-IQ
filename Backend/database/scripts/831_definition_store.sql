-- ============================================================================
-- The definition store.
--
-- One authoritative model for every no-code and SQL artifact. There is no
-- second source of truth: every specialised detail row references the
-- authoritative definition version.
--
-- Two behaviours are load-bearing and are enforced by the database rather than
-- by the application, because an authority that depends on every caller
-- behaving is not an authority:
--
--   a published version cannot be edited, only superseded
--   a dependency that would close a cycle is refused
--
-- Idempotent: guarded creates throughout, so a replay changes nothing.
-- ============================================================================

CREATE SCHEMA IF NOT EXISTS ppiq_meta;

-- ---------------------------------------------------------------- parent ----
CREATE TABLE IF NOT EXISTS ppiq_meta.definition_store (
    id                  uuid            NOT NULL DEFAULT gen_random_uuid(),
    tenant_id           uuid            NOT NULL,
    definition_code     varchar(100)    NOT NULL,
    surface             varchar(4)      NOT NULL,
    definition_kind     varchar(40)     NOT NULL,
    name                varchar(200)    NOT NULL,
    owner_id            uuid            NOT NULL,
    folder_path         varchar(500)    NULL,
    tags                text[]          NULL,
    current_version     integer         NOT NULL DEFAULT 0,
    is_locked           boolean         NOT NULL DEFAULT false,
    created_at_utc      timestamptz     NOT NULL DEFAULT now(),
    created_by          uuid            NULL,
    updated_at_utc      timestamptz     NULL,
    updated_by          uuid            NULL,
    is_deleted          boolean         NOT NULL DEFAULT false,
    deleted_at_utc      timestamptz     NULL,
    deleted_reason      varchar(500)    NULL,
    is_synthetic        boolean         NOT NULL DEFAULT false,
    CONSTRAINT pk_definition_store PRIMARY KEY (id),
    CONSTRAINT uq_definition_store_tenant_code UNIQUE (tenant_id, definition_code),
    CONSTRAINT ck_definition_store_surface CHECK (surface IN ('S1','S2','S3','S4','S5')),
    CONSTRAINT ck_definition_store_kind CHECK (definition_kind IN (
        'transformation','page','widget','filter','master_dimension','master_measure',
        'hierarchy','bookmark','saved_query','analysis','feature_set','model',
        'practice','log_rule','report','scenario'))
);

CREATE INDEX IF NOT EXISTS ix_definition_store_surface_kind ON ppiq_meta.definition_store (surface, definition_kind);
CREATE INDEX IF NOT EXISTS ix_definition_store_owner       ON ppiq_meta.definition_store (owner_id);
CREATE INDEX IF NOT EXISTS ix_definition_store_folder      ON ppiq_meta.definition_store (folder_path);
CREATE INDEX IF NOT EXISTS ix_definition_store_tags        ON ppiq_meta.definition_store USING gin (tags);

-- ------------------------------------------------------------- versions ----
CREATE TABLE IF NOT EXISTS ppiq_meta.definition_versions (
    id                  uuid            NOT NULL DEFAULT gen_random_uuid(),
    tenant_id           uuid            NOT NULL,
    definition_id       uuid            NOT NULL,
    version_number      integer         NOT NULL,
    status              varchar(20)     NOT NULL,
    mode                varchar(6)      NOT NULL,
    graph_json          jsonb           NULL,
    sql_text            text            NULL,
    compiled_sql        text            NULL,
    definition_hash     varchar(64)     NOT NULL,
    input_bindings      jsonb           NULL,
    output_schema       jsonb           NULL,
    validation_result   jsonb           NULL,
    validated_at_utc    timestamptz     NULL,
    published_at_utc    timestamptz     NULL,
    published_by        uuid            NULL,
    rollback_pointer    integer         NULL,
    drift_detail        jsonb           NULL,
    created_at_utc      timestamptz     NOT NULL DEFAULT now(),
    created_by          uuid            NULL,
    updated_at_utc      timestamptz     NULL,
    updated_by          uuid            NULL,
    is_deleted          boolean         NOT NULL DEFAULT false,
    deleted_at_utc      timestamptz     NULL,
    deleted_reason      varchar(500)    NULL,
    is_synthetic        boolean         NOT NULL DEFAULT false,
    CONSTRAINT pk_definition_versions PRIMARY KEY (id),
    CONSTRAINT uq_definition_versions_number UNIQUE (definition_id, version_number),
    CONSTRAINT fk_definition_versions_store FOREIGN KEY (definition_id)
        REFERENCES ppiq_meta.definition_store (id) ON DELETE RESTRICT,
    CONSTRAINT ck_definition_versions_status CHECK (status IN (
        'draft','validated','published','paused_by_drift','rolled_back','superseded')),
    CONSTRAINT ck_definition_versions_mode CHECK (mode IN ('block','sql'))
);

CREATE INDEX IF NOT EXISTS ix_definition_versions_def_status ON ppiq_meta.definition_versions (definition_id, status);
CREATE INDEX IF NOT EXISTS ix_definition_versions_published  ON ppiq_meta.definition_versions (status) WHERE status = 'published';
CREATE INDEX IF NOT EXISTS ix_definition_versions_hash       ON ppiq_meta.definition_versions (definition_hash);

-- --------------------------------------------------------- dependencies ----
CREATE TABLE IF NOT EXISTS ppiq_meta.definition_dependencies (
    id                          uuid         NOT NULL DEFAULT gen_random_uuid(),
    tenant_id                   uuid         NOT NULL,
    definition_id               uuid         NOT NULL,
    depends_on_definition_id    uuid         NOT NULL,
    depends_on_version          integer      NULL,
    dependency_kind             varchar(30)  NOT NULL,
    is_required                 boolean      NOT NULL DEFAULT true,
    created_at_utc              timestamptz  NOT NULL DEFAULT now(),
    created_by                  uuid         NULL,
    updated_at_utc              timestamptz  NULL,
    updated_by                  uuid         NULL,
    is_deleted                  boolean      NOT NULL DEFAULT false,
    deleted_at_utc              timestamptz  NULL,
    deleted_reason              varchar(500) NULL,
    is_synthetic                boolean      NOT NULL DEFAULT false,
    CONSTRAINT pk_definition_dependencies PRIMARY KEY (id),
    CONSTRAINT uq_definition_dependencies UNIQUE (definition_id, depends_on_definition_id, dependency_kind),
    CONSTRAINT fk_definition_dependencies_from FOREIGN KEY (definition_id)
        REFERENCES ppiq_meta.definition_store (id) ON DELETE RESTRICT,
    CONSTRAINT fk_definition_dependencies_to FOREIGN KEY (depends_on_definition_id)
        REFERENCES ppiq_meta.definition_store (id) ON DELETE RESTRICT,
    CONSTRAINT ck_definition_dependencies_kind CHECK (dependency_kind IN (
        'source','master_item','relationship','feature_set','model','page')),
    CONSTRAINT ck_definition_dependencies_not_self CHECK (definition_id <> depends_on_definition_id)
);

CREATE INDEX IF NOT EXISTS ix_definition_dependencies_from ON ppiq_meta.definition_dependencies (definition_id);
CREATE INDEX IF NOT EXISTS ix_definition_dependencies_to   ON ppiq_meta.definition_dependencies (depends_on_definition_id);

-- ----------------------------------------------------------- immutability ---
-- A published version records what actually ran. Editing one would silently
-- rewrite the meaning of every result that already names it, so the only
-- change a published row accepts is the transition to superseded.
CREATE OR REPLACE FUNCTION ppiq_meta.definition_versions_reject_published_edit()
RETURNS trigger
LANGUAGE plpgsql
AS $fn$
BEGIN
    IF OLD.status <> 'published' THEN
        RETURN NEW;
    END IF;

    IF NEW.status = 'superseded'
       AND NEW.id                IS NOT DISTINCT FROM OLD.id
       AND NEW.definition_id     IS NOT DISTINCT FROM OLD.definition_id
       AND NEW.version_number    IS NOT DISTINCT FROM OLD.version_number
       AND NEW.mode              IS NOT DISTINCT FROM OLD.mode
       AND NEW.graph_json        IS NOT DISTINCT FROM OLD.graph_json
       AND NEW.sql_text          IS NOT DISTINCT FROM OLD.sql_text
       AND NEW.compiled_sql      IS NOT DISTINCT FROM OLD.compiled_sql
       AND NEW.definition_hash   IS NOT DISTINCT FROM OLD.definition_hash
       AND NEW.input_bindings    IS NOT DISTINCT FROM OLD.input_bindings
       AND NEW.output_schema     IS NOT DISTINCT FROM OLD.output_schema
       AND NEW.published_at_utc  IS NOT DISTINCT FROM OLD.published_at_utc
       AND NEW.published_by      IS NOT DISTINCT FROM OLD.published_by
    THEN
        RETURN NEW;
    END IF;

    RAISE EXCEPTION
        'definition version %.% is published and immutable; supersede it or create a new version',
        OLD.definition_id, OLD.version_number
        USING ERRCODE = '23514';
END;
$fn$;

DROP TRIGGER IF EXISTS trg_definition_versions_immutable ON ppiq_meta.definition_versions;
CREATE TRIGGER trg_definition_versions_immutable
    BEFORE UPDATE ON ppiq_meta.definition_versions
    FOR EACH ROW EXECUTE FUNCTION ppiq_meta.definition_versions_reject_published_edit();

-- ------------------------------------------------------------ cycle guard ---
-- A dependency cycle makes resolution order undefined and publication
-- non-terminating. The graph is walked from the proposed target back towards
-- the proposed source; reaching it means the edge would close a cycle.
CREATE OR REPLACE FUNCTION ppiq_meta.definition_dependencies_reject_cycle()
RETURNS trigger
LANGUAGE plpgsql
AS $fn$
DECLARE
    closes_cycle boolean;
BEGIN
    IF NEW.is_deleted THEN
        RETURN NEW;
    END IF;

    WITH RECURSIVE reachable(definition_id) AS (
        SELECT NEW.depends_on_definition_id
        UNION
        SELECT d.depends_on_definition_id
        FROM   ppiq_meta.definition_dependencies d
        JOIN   reachable r ON r.definition_id = d.definition_id
        WHERE  d.is_deleted = false
    )
    SELECT EXISTS (SELECT 1 FROM reachable WHERE definition_id = NEW.definition_id)
    INTO   closes_cycle;

    IF closes_cycle THEN
        RAISE EXCEPTION
            'dependency % -> % would close a cycle',
            NEW.definition_id, NEW.depends_on_definition_id
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END;
$fn$;

DROP TRIGGER IF EXISTS trg_definition_dependencies_no_cycle ON ppiq_meta.definition_dependencies;
CREATE TRIGGER trg_definition_dependencies_no_cycle
    BEFORE INSERT OR UPDATE ON ppiq_meta.definition_dependencies
    FOR EACH ROW EXECUTE FUNCTION ppiq_meta.definition_dependencies_reject_cycle();