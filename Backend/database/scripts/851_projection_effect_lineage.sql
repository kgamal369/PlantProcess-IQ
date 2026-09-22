\set ON_ERROR_STOP on
\qecho PPIQ_PSQL_EXECUTED 851_projection_effect_lineage

-- ============================================================================
-- PlantProcess IQ - canonical projection effect lineage (backlog reference T-104)
--
-- One row per accepted evaluation of one governed source identity by one run:
-- the exact Transformation definition, version and immutable hash that produced
-- the canonical effect, the run that executed it, the run's projection generation,
-- a digest of the deterministic business effect, and the accepted source lineage
-- (batch, receipt, content hash, dataset governance) when the input relation
-- exposes it.
--
-- Mapping version is provenance, never identity. The canonical row keeps its
-- governed source identity and its system Id across every reprojection; this
-- ledger records which version produced which effect, and exactly one evaluation
-- per identity is current.
--
-- Append-only. The only permitted change to a written row is retiring its
-- current flag when a later accepted evaluation replaces it. Tenant scope is
-- forced row-level security on the same GUC every accepted relation uses.
--
-- Additive and idempotent. No EF entity maps this table; this script is the
-- single DDL authority for it.
-- ============================================================================

BEGIN;

DO $predecessor$
BEGIN
    IF to_regclass('ppiq_plant.material_units') IS NULL THEN
        RAISE EXCEPTION
            'PPIQ_PROJECTION_LINEAGE_PREDECESSOR_MISSING: ppiq_plant.material_units must exist (topology convergence runs first).';
    END IF;
END
$predecessor$;

CREATE SEQUENCE IF NOT EXISTS ppiq_plant.canonical_projection_generation_seq
    AS bigint START WITH 1 INCREMENT BY 1 NO CYCLE;

CREATE TABLE IF NOT EXISTS ppiq_plant.canonical_projection_effects
(
    effect_id                    uuid          NOT NULL DEFAULT gen_random_uuid(),
    tenant_id                    uuid          NOT NULL,
    target_entity                varchar(100)  NOT NULL,
    material_unit_id             uuid          NOT NULL,
    source_system                varchar(100)  NOT NULL,
    source_record_id             varchar(200)  NOT NULL,
    definition_id                uuid          NOT NULL,
    definition_version           integer       NOT NULL,
    definition_hash              varchar(64)   NOT NULL,
    job_run_history_id           uuid          NOT NULL,
    projection_generation        bigint        NOT NULL,
    projection_mode              varchar(20)   NOT NULL,
    effect_kind                  varchar(20)   NOT NULL,
    effect_hash                  char(64)      NOT NULL,
    source_batch_id              uuid          NULL,
    source_receipt_id            uuid          NULL,
    source_content_hash          varchar(64)   NULL,
    source_dataset_governance_id uuid          NULL,
    supersedes_effect_id         uuid          NULL,
    is_current                   boolean       NOT NULL,
    recorded_at_utc              timestamptz   NOT NULL DEFAULT clock_timestamp(),

    CONSTRAINT pk_canonical_projection_effects PRIMARY KEY (effect_id),
    CONSTRAINT fk_canonical_projection_effects_unit FOREIGN KEY (material_unit_id)
        REFERENCES ppiq_plant.material_units (id) ON DELETE CASCADE,
    CONSTRAINT fk_canonical_projection_effects_supersedes FOREIGN KEY (supersedes_effect_id)
        REFERENCES ppiq_plant.canonical_projection_effects (effect_id) ON DELETE CASCADE,
    CONSTRAINT ux_canonical_projection_effects_run
        UNIQUE (target_entity, source_system, source_record_id, job_run_history_id),
    CONSTRAINT ck_canonical_projection_effects_target CHECK (target_entity = 'MaterialUnit'),
    CONSTRAINT ck_canonical_projection_effects_version CHECK (definition_version > 0),
    CONSTRAINT ck_canonical_projection_effects_hash CHECK (length(btrim(definition_hash)) > 0),
    CONSTRAINT ck_canonical_projection_effects_generation CHECK (projection_generation > 0),
    CONSTRAINT ck_canonical_projection_effects_mode CHECK (projection_mode IN ('Ordinary', 'Reproject')),
    CONSTRAINT ck_canonical_projection_effects_kind CHECK
        (effect_kind IN ('Inserted', 'Superseded', 'Reattributed')),
    CONSTRAINT ck_canonical_projection_effects_effect_hash CHECK (effect_hash ~ '^[0-9a-f]{64}$'),
    CONSTRAINT ck_canonical_projection_effects_identity CHECK
        (length(btrim(source_system)) > 0 AND length(btrim(source_record_id)) > 0),
    CONSTRAINT ck_canonical_projection_effects_inserted CHECK
        (effect_kind <> 'Inserted' OR supersedes_effect_id IS NULL)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_canonical_projection_effects_current
    ON ppiq_plant.canonical_projection_effects (target_entity, source_system, source_record_id)
    WHERE is_current;

CREATE INDEX IF NOT EXISTS ix_canonical_projection_effects_unit
    ON ppiq_plant.canonical_projection_effects (material_unit_id, recorded_at_utc);

CREATE INDEX IF NOT EXISTS ix_canonical_projection_effects_definition
    ON ppiq_plant.canonical_projection_effects (tenant_id, definition_id, definition_version);

CREATE INDEX IF NOT EXISTS ix_canonical_projection_effects_run
    ON ppiq_plant.canonical_projection_effects (job_run_history_id);

-- A written evaluation is immutable. Retiring the current flag is the one change,
-- and a retired evaluation never becomes current again.
CREATE OR REPLACE FUNCTION ppiq_plant.canonical_projection_effects_guard()
RETURNS trigger LANGUAGE plpgsql SET search_path = pg_catalog AS $guard$
BEGIN
    IF ROW(NEW.effect_id, NEW.tenant_id, NEW.target_entity, NEW.material_unit_id, NEW.source_system,
           NEW.source_record_id, NEW.definition_id, NEW.definition_version, NEW.definition_hash,
           NEW.job_run_history_id, NEW.projection_generation, NEW.projection_mode, NEW.effect_kind,
           NEW.effect_hash, NEW.source_batch_id, NEW.source_receipt_id, NEW.source_content_hash,
           NEW.source_dataset_governance_id, NEW.supersedes_effect_id, NEW.recorded_at_utc)
       IS DISTINCT FROM
       ROW(OLD.effect_id, OLD.tenant_id, OLD.target_entity, OLD.material_unit_id, OLD.source_system,
           OLD.source_record_id, OLD.definition_id, OLD.definition_version, OLD.definition_hash,
           OLD.job_run_history_id, OLD.projection_generation, OLD.projection_mode, OLD.effect_kind,
           OLD.effect_hash, OLD.source_batch_id, OLD.source_receipt_id, OLD.source_content_hash,
           OLD.source_dataset_governance_id, OLD.supersedes_effect_id, OLD.recorded_at_utc) THEN
        RAISE EXCEPTION 'PPIQ_PROJECTION_LINEAGE_IMMUTABLE: a recorded evaluation cannot be rewritten.'
            USING ERRCODE = 'P0001';
    END IF;
    IF NEW.is_current AND NOT OLD.is_current THEN
        RAISE EXCEPTION 'PPIQ_PROJECTION_LINEAGE_IMMUTABLE: a retired evaluation cannot become current again.'
            USING ERRCODE = 'P0001';
    END IF;
    RETURN NEW;
END
$guard$;

DROP TRIGGER IF EXISTS trg_canonical_projection_effects_guard ON ppiq_plant.canonical_projection_effects;
CREATE TRIGGER trg_canonical_projection_effects_guard
    BEFORE UPDATE ON ppiq_plant.canonical_projection_effects
    FOR EACH ROW EXECUTE FUNCTION ppiq_plant.canonical_projection_effects_guard();

ALTER TABLE ppiq_plant.canonical_projection_effects ENABLE ROW LEVEL SECURITY;
ALTER TABLE ppiq_plant.canonical_projection_effects FORCE ROW LEVEL SECURITY;
DROP POLICY IF EXISTS canonical_projection_tenant ON ppiq_plant.canonical_projection_effects;
CREATE POLICY canonical_projection_tenant ON ppiq_plant.canonical_projection_effects
    USING (tenant_id = nullif(current_setting('app.current_tenant', true), '')::uuid)
    WITH CHECK (tenant_id = nullif(current_setting('app.current_tenant', true), '')::uuid);

-- The stable lineage query. Invoker rights, so the caller's tenant scope applies.
CREATE OR REPLACE VIEW ppiq_plant.canonical_projection_current
    WITH (security_invoker = true) AS
SELECT e.effect_id,
       e.tenant_id,
       e.target_entity,
       e.material_unit_id AS canonical_row_id,
       e.source_system,
       e.source_record_id,
       e.definition_id,
       e.definition_version,
       e.definition_hash,
       e.job_run_history_id,
       e.projection_generation,
       e.projection_mode,
       e.effect_kind,
       e.effect_hash,
       e.source_batch_id,
       e.source_receipt_id,
       e.source_content_hash,
       e.source_dataset_governance_id,
       e.supersedes_effect_id,
       e.recorded_at_utc
  FROM ppiq_plant.canonical_projection_effects e
 WHERE e.is_current;

REVOKE ALL ON ppiq_plant.canonical_projection_effects FROM PUBLIC;
REVOKE ALL ON ppiq_plant.canonical_projection_current FROM PUBLIC;
REVOKE ALL ON SEQUENCE ppiq_plant.canonical_projection_generation_seq FROM PUBLIC;

DO $grants$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plantprocess_app') THEN
        REVOKE ALL ON ppiq_plant.canonical_projection_effects FROM plantprocess_app;
        GRANT SELECT, INSERT ON ppiq_plant.canonical_projection_effects TO plantprocess_app;
        GRANT UPDATE (is_current) ON ppiq_plant.canonical_projection_effects TO plantprocess_app;
        GRANT SELECT ON ppiq_plant.canonical_projection_current TO plantprocess_app;
        GRANT USAGE ON SEQUENCE ppiq_plant.canonical_projection_generation_seq TO plantprocess_app;
    END IF;
END
$grants$;

COMMIT;
