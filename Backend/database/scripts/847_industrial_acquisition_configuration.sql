\set ON_ERROR_STOP on
\qecho PPIQ_PSQL_EXECUTED industrial_acquisition_configuration

-- ============================================================================
-- PlantProcess IQ - Industrial acquisition configuration and stable field
-- authority.
--
-- WHAT THIS ADDS, AND WHAT IT DOES NOT.
--   1. The acquisition configuration becomes a canonical definition kind on the
--      existing definition store (surface S1). Its immutable versions, hash and
--      publication are the store's own; this file adds only the typed detail
--      projection of that content. Historical script 831 is not edited: the kind
--      CHECK is superseded here, keeping every existing literal.
--   2. An explicit tenant governance binding over the legacy source dataset.
--      One dataset can be governed by exactly one tenant; the binding is made
--      only by the governed function, never by a first read.
--   3. Stable field identities with immutable revisions. field_id is the
--      identity; locator, key, label, type and unit are revision metadata. A
--      changed locator or type never inherits an identity without an explicit
--      reconciliation.
--   4. Immutable raw layout revisions per governed dataset.
--
-- No runtime session, receipt, schedule, capacity admission or accepted record
-- lives here. Legacy connection, dataset and field tables are not altered.
-- Idempotent: guarded creates, replaced constraints and functions.
-- ============================================================================

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE SCHEMA IF NOT EXISTS ppiq_meta;

DO $predecessor$
BEGIN
    IF to_regclass('ppiq_meta.tenants') IS NULL
       OR to_regclass('ppiq_meta.definition_store') IS NULL
       OR to_regclass('ppiq_meta.definition_versions') IS NULL
       OR to_regclass('ppiq_meta.source_dataset_definitions') IS NULL
       OR to_regclass('ppiq_meta.connection_profiles') IS NULL THEN
        RAISE EXCEPTION
            'PPIQ_ACQUISITION_PREDECESSOR_MISSING: tenants, the definition store and the governed source catalogue must exist first.';
    END IF;
    IF to_regprocedure('ppiq_meta.definition_detail_parent_guard()') IS NULL THEN
        RAISE EXCEPTION
            'PPIQ_ACQUISITION_PREDECESSOR_MISSING: the definition detail parent guard must exist first.';
    END IF;
END
$predecessor$;

-- ---------------------------------------------------------------------------
-- 1. Definition kind authority: the current CHECK supersedes the historical one.
-- ---------------------------------------------------------------------------
ALTER TABLE ppiq_meta.definition_store DROP CONSTRAINT IF EXISTS ck_definition_store_kind;
ALTER TABLE ppiq_meta.definition_store
    ADD CONSTRAINT ck_definition_store_kind CHECK (definition_kind IN (
        'transformation','page','widget','filter','master_dimension','master_measure',
        'hierarchy','bookmark','saved_query','analysis','feature_set','model',
        'practice','log_rule','report','scenario','acquisition_configuration'));

CREATE TABLE IF NOT EXISTS ppiq_meta.acquisition_configuration_details (
    definition_version_id       uuid            NOT NULL,
    dataset_governance_id       uuid            NULL,
    provider_type               varchar(64)     NULL,
    layout_revision             integer         NULL,
    field_references            jsonb           NULL,
    recording_groups            jsonb           NULL,
    source_requirements         jsonb           NULL,
    source_time_reference       jsonb           NULL,
    storage_references          jsonb           NULL,
    accepted_record_contract    jsonb           NULL,
    CONSTRAINT pk_acquisition_configuration_details PRIMARY KEY (definition_version_id),
    CONSTRAINT fk_acquisition_configuration_details_version FOREIGN KEY (definition_version_id)
        REFERENCES ppiq_meta.definition_versions (id) ON DELETE CASCADE
);

DROP TRIGGER IF EXISTS trg_acquisition_configuration_details_parent
    ON ppiq_meta.acquisition_configuration_details;
CREATE TRIGGER trg_acquisition_configuration_details_parent
    BEFORE INSERT OR UPDATE ON ppiq_meta.acquisition_configuration_details
    FOR EACH ROW EXECUTE FUNCTION
    ppiq_meta.definition_detail_parent_guard('S1', 'acquisition_configuration');

COMMENT ON TABLE ppiq_meta.acquisition_configuration_details IS
    'PPIQ acquisition configuration. Typed projection of one immutable S1 acquisition_configuration definition version. Not a second configuration authority.';

-- ---------------------------------------------------------------------------
-- 2. Dataset governance: one legacy dataset, exactly one owning tenant.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ppiq_meta.source_dataset_governance (
    id                              uuid            NOT NULL DEFAULT gen_random_uuid(),
    tenant_id                       uuid            NOT NULL,
    source_dataset_definition_id    uuid            NOT NULL,
    connection_profile_id           uuid            NOT NULL,
    provider_type                   varchar(64)     NOT NULL,
    governed_at_utc                 timestamptz     NOT NULL DEFAULT now(),
    governed_by                     uuid            NULL,
    CONSTRAINT pk_source_dataset_governance PRIMARY KEY (id),
    CONSTRAINT ux_source_dataset_governance_dataset UNIQUE (source_dataset_definition_id),
    CONSTRAINT ux_source_dataset_governance_tenant_id UNIQUE (tenant_id, id),
    CONSTRAINT fk_source_dataset_governance_tenant FOREIGN KEY (tenant_id)
        REFERENCES ppiq_meta.tenants (id) ON DELETE RESTRICT,
    CONSTRAINT fk_source_dataset_governance_dataset FOREIGN KEY (source_dataset_definition_id)
        REFERENCES ppiq_meta.source_dataset_definitions (id) ON DELETE RESTRICT,
    CONSTRAINT fk_source_dataset_governance_connection FOREIGN KEY (connection_profile_id)
        REFERENCES ppiq_meta.connection_profiles (id) ON DELETE RESTRICT,
    CONSTRAINT ck_source_dataset_governance_provider CHECK
        (provider_type <> '' AND provider_type = btrim(provider_type))
);

-- ---------------------------------------------------------------------------
-- 3. Stable field identity and immutable revisions.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ppiq_meta.source_field_identities (
    field_id                    uuid            NOT NULL DEFAULT gen_random_uuid(),
    tenant_id                   uuid            NOT NULL,
    dataset_governance_id       uuid            NOT NULL,
    current_revision            integer         NOT NULL DEFAULT 0,
    created_at_utc              timestamptz     NOT NULL DEFAULT now(),
    created_by                  uuid            NULL,
    CONSTRAINT pk_source_field_identities PRIMARY KEY (field_id),
    CONSTRAINT ux_source_field_identities_tenant_field UNIQUE (tenant_id, field_id),
    CONSTRAINT fk_source_field_identities_governance FOREIGN KEY (tenant_id, dataset_governance_id)
        REFERENCES ppiq_meta.source_dataset_governance (tenant_id, id) ON DELETE RESTRICT,
    CONSTRAINT ck_source_field_identities_revision CHECK (current_revision >= 0)
);

CREATE INDEX IF NOT EXISTS ix_source_field_identities_governance
    ON ppiq_meta.source_field_identities (dataset_governance_id);

CREATE TABLE IF NOT EXISTS ppiq_meta.source_field_revisions (
    id                          uuid            NOT NULL DEFAULT gen_random_uuid(),
    tenant_id                   uuid            NOT NULL,
    field_id                    uuid            NOT NULL,
    revision                    integer         NOT NULL,
    provider_type               varchar(64)     NOT NULL,
    locator_kind                varchar(32)     NOT NULL,
    source_locator              jsonb           NOT NULL,
    locator_identity            varchar(64)     NOT NULL,
    field_key                   varchar(200)    NOT NULL,
    display_name                varchar(200)    NOT NULL,
    declared_type               varchar(32)     NOT NULL,
    type_shape                  jsonb           NOT NULL DEFAULT '{}'::jsonb,
    source_unit                 varchar(64)     NULL,
    roles                       text[]          NOT NULL,
    layout_revision             integer         NULL,
    change_kind                 varchar(24)     NOT NULL,
    semantic_hash               varchar(64)     NOT NULL,
    created_at_utc              timestamptz     NOT NULL DEFAULT now(),
    created_by                  uuid            NULL,
    CONSTRAINT pk_source_field_revisions PRIMARY KEY (id),
    CONSTRAINT ux_source_field_revisions_number UNIQUE (field_id, revision),
    CONSTRAINT fk_source_field_revisions_identity FOREIGN KEY (tenant_id, field_id)
        REFERENCES ppiq_meta.source_field_identities (tenant_id, field_id) ON DELETE RESTRICT,
    CONSTRAINT ck_source_field_revisions_number CHECK (revision >= 1),
    CONSTRAINT ck_source_field_revisions_locator_kind CHECK
        (locator_kind IN ('relational_column','file_column','opc_node','raw_member')),
    CONSTRAINT ck_source_field_revisions_roles CHECK
        (cardinality(roles) >= 1
         AND roles <@ ARRAY['payload','trigger','event_identity','quality','time','key']::text[]),
    CONSTRAINT ck_source_field_revisions_change_kind CHECK
        (change_kind IN ('registered','metadata','reconciled_locator','reconciled_type')),
    CONSTRAINT ck_source_field_revisions_layout CHECK
        (layout_revision IS NULL OR (layout_revision >= 1 AND locator_kind = 'raw_member')),
    CONSTRAINT ck_source_field_revisions_hashes CHECK
        (locator_identity ~ '^[0-9a-f]{64}$' AND semantic_hash ~ '^[0-9a-f]{64}$')
);

CREATE INDEX IF NOT EXISTS ix_source_field_revisions_locator
    ON ppiq_meta.source_field_revisions (locator_identity);

-- ---------------------------------------------------------------------------
-- 4. Immutable raw layout revisions.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ppiq_meta.source_layout_revisions (
    id                          uuid            NOT NULL DEFAULT gen_random_uuid(),
    tenant_id                   uuid            NOT NULL,
    dataset_governance_id       uuid            NOT NULL,
    revision                    integer         NOT NULL,
    layout_kind                 varchar(32)     NOT NULL,
    region_bytes                integer         NOT NULL,
    layout_document             jsonb           NOT NULL,
    semantic_hash               varchar(64)     NOT NULL,
    created_at_utc              timestamptz     NOT NULL DEFAULT now(),
    created_by                  uuid            NULL,
    CONSTRAINT pk_source_layout_revisions PRIMARY KEY (id),
    CONSTRAINT ux_source_layout_revisions_number UNIQUE (dataset_governance_id, revision),
    CONSTRAINT ux_source_layout_revisions_hash UNIQUE (dataset_governance_id, semantic_hash),
    CONSTRAINT fk_source_layout_revisions_governance FOREIGN KEY (tenant_id, dataset_governance_id)
        REFERENCES ppiq_meta.source_dataset_governance (tenant_id, id) ON DELETE RESTRICT,
    CONSTRAINT ck_source_layout_revisions_number CHECK (revision >= 1),
    CONSTRAINT ck_source_layout_revisions_kind CHECK (layout_kind IN ('raw_block')),
    CONSTRAINT ck_source_layout_revisions_region CHECK (region_bytes BETWEEN 1 AND 65536),
    CONSTRAINT ck_source_layout_revisions_hash CHECK (semantic_hash ~ '^[0-9a-f]{64}$')
);

-- ---------------------------------------------------------------------------
-- 5. Revisions are history. Neither an update nor a delete is lawful.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION ppiq_meta.industrial_revision_reject_mutation()
RETURNS trigger
LANGUAGE plpgsql
AS $fn$
BEGIN
    RAISE EXCEPTION 'IAR01 revision_immutable: % rows are immutable history; declare a new revision.', TG_TABLE_NAME
        USING ERRCODE = '23514';
END
$fn$;

DROP TRIGGER IF EXISTS trg_source_field_revisions_immutable ON ppiq_meta.source_field_revisions;
CREATE TRIGGER trg_source_field_revisions_immutable
    BEFORE UPDATE OR DELETE ON ppiq_meta.source_field_revisions
    FOR EACH ROW EXECUTE FUNCTION ppiq_meta.industrial_revision_reject_mutation();

DROP TRIGGER IF EXISTS trg_source_layout_revisions_immutable ON ppiq_meta.source_layout_revisions;
CREATE TRIGGER trg_source_layout_revisions_immutable
    BEFORE UPDATE OR DELETE ON ppiq_meta.source_layout_revisions
    FOR EACH ROW EXECUTE FUNCTION ppiq_meta.industrial_revision_reject_mutation();

-- ---------------------------------------------------------------------------
-- 6. Governance: explicit, atomic, one tenant per dataset.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION ppiq_meta.govern_source_dataset
(
    p_tenant_id                     uuid,
    p_source_dataset_definition_id  uuid,
    p_actor                         uuid DEFAULT NULL
)
RETURNS uuid
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, ppiq_meta, pg_temp
AS $fn$
DECLARE
    v_connection    uuid;
    v_provider      text;
    v_existing      ppiq_meta.source_dataset_governance%ROWTYPE;
    v_id            uuid;
BEGIN
    IF p_tenant_id IS NULL OR p_source_dataset_definition_id IS NULL THEN
        RAISE EXCEPTION 'IAG01 dataset_not_found: a tenant and a dataset are required.'
            USING ERRCODE = 'P0001';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM ppiq_meta.tenants t WHERE t.id = p_tenant_id) THEN
        RAISE EXCEPTION 'IAG01 dataset_not_found: tenant % does not exist.', p_tenant_id
            USING ERRCODE = 'P0001';
    END IF;

    SELECT d.connection_profile_id
      INTO v_connection
      FROM ppiq_meta.source_dataset_definitions d
     WHERE d.id = p_source_dataset_definition_id
       AND d.is_deleted = false;

    IF v_connection IS NULL THEN
        RAISE EXCEPTION 'IAG01 dataset_not_found: dataset % does not exist.', p_source_dataset_definition_id
            USING ERRCODE = 'P0001';
    END IF;

    SELECT btrim(c.provider_type)
      INTO v_provider
      FROM ppiq_meta.connection_profiles c
     WHERE c.id = v_connection
       AND c.is_deleted = false;

    IF v_provider IS NULL OR v_provider = '' THEN
        RAISE EXCEPTION 'IAG04 connection_incoherent: dataset % has no live connection with a provider.', p_source_dataset_definition_id
            USING ERRCODE = 'P0001';
    END IF;

    -- One claim at a time per dataset; the unique constraint is the final word.
    PERFORM pg_advisory_xact_lock(
        hashtextextended('ppiq.dataset.governance|' || p_source_dataset_definition_id::text, 0));

    SELECT * INTO v_existing
      FROM ppiq_meta.source_dataset_governance g
     WHERE g.source_dataset_definition_id = p_source_dataset_definition_id;

    IF FOUND THEN
        IF v_existing.tenant_id <> p_tenant_id THEN
            RAISE EXCEPTION 'IAG02 dataset_governed_by_another_tenant: dataset % is governed by another tenant.', p_source_dataset_definition_id
                USING ERRCODE = 'P0001';
        END IF;
        IF v_existing.connection_profile_id <> v_connection THEN
            RAISE EXCEPTION 'IAG04 connection_incoherent: dataset % moved connection after governance.', p_source_dataset_definition_id
                USING ERRCODE = 'P0001';
        END IF;
        RETURN v_existing.id;
    END IF;

    INSERT INTO ppiq_meta.source_dataset_governance
        (tenant_id, source_dataset_definition_id, connection_profile_id, provider_type, governed_by)
    VALUES
        (p_tenant_id, p_source_dataset_definition_id, v_connection, v_provider, p_actor)
    ON CONFLICT (source_dataset_definition_id) DO NOTHING
    RETURNING id INTO v_id;

    IF v_id IS NULL THEN
        SELECT * INTO v_existing
          FROM ppiq_meta.source_dataset_governance g
         WHERE g.source_dataset_definition_id = p_source_dataset_definition_id;
        IF v_existing.tenant_id <> p_tenant_id THEN
            RAISE EXCEPTION 'IAG02 dataset_governed_by_another_tenant: dataset % is governed by another tenant.', p_source_dataset_definition_id
                USING ERRCODE = 'P0001';
        END IF;
        v_id := v_existing.id;
    END IF;

    RETURN v_id;
END
$fn$;

-- ---------------------------------------------------------------------------
-- 7. Field revision declaration: identity is field_id, never the locator.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION ppiq_meta.declare_source_field_revision
(
    p_tenant_id             uuid,
    p_governance_id         uuid,
    p_field_id              uuid,
    p_locator_kind          text,
    p_source_locator        jsonb,
    p_locator_identity      text,
    p_field_key             text,
    p_display_name          text,
    p_declared_type         text,
    p_type_shape            jsonb,
    p_source_unit           text,
    p_roles                 text[],
    p_layout_revision       integer,
    p_reconcile             text,
    p_semantic_hash         text,
    p_actor                 uuid DEFAULT NULL
)
RETURNS TABLE (out_field_id uuid, out_revision integer, out_change_kind text, out_created boolean)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, ppiq_meta, pg_temp
AS $fn$
DECLARE
    v_gov_tenant    uuid;
    v_provider      text;
    v_bound         uuid;
    v_target        uuid;
    v_cur           ppiq_meta.source_field_revisions%ROWTYPE;
    v_has_cur       boolean := false;
    v_change        text;
    v_next          integer;
    v_new_field     boolean := false;
BEGIN
    IF p_tenant_id IS NULL OR p_governance_id IS NULL OR p_source_locator IS NULL
       OR p_locator_identity IS NULL OR p_field_key IS NULL OR p_display_name IS NULL
       OR p_declared_type IS NULL OR p_semantic_hash IS NULL OR p_roles IS NULL THEN
        RAISE EXCEPTION 'IAF01 field_declaration_invalid: the declaration is incomplete.'
            USING ERRCODE = 'P0001';
    END IF;

    IF p_reconcile IS NOT NULL AND (p_reconcile <> 'same_field' OR p_field_id IS NULL) THEN
        RAISE EXCEPTION 'IAF01 field_declaration_invalid: reconciliation is same_field and names a field_id.'
            USING ERRCODE = 'P0001';
    END IF;

    SELECT g.tenant_id, g.provider_type INTO v_gov_tenant, v_provider
      FROM ppiq_meta.source_dataset_governance g
     WHERE g.id = p_governance_id;

    IF v_gov_tenant IS NULL OR v_gov_tenant <> p_tenant_id THEN
        RAISE EXCEPTION 'IAG03 dataset_not_governed: the dataset is not governed by this tenant.'
            USING ERRCODE = 'P0001';
    END IF;

    PERFORM pg_advisory_xact_lock(hashtextextended('ppiq.dataset.fields|' || p_governance_id::text, 0));

    IF p_layout_revision IS NOT NULL AND NOT EXISTS (
        SELECT 1 FROM ppiq_meta.source_layout_revisions l
         WHERE l.dataset_governance_id = p_governance_id AND l.revision = p_layout_revision) THEN
        RAISE EXCEPTION 'IAL02 layout_revision_unknown: layout revision % does not exist for this dataset.', p_layout_revision
            USING ERRCODE = 'P0001';
    END IF;

    -- Which field currently reads from exactly this locator, if any.
    SELECT i.field_id INTO v_bound
      FROM ppiq_meta.source_field_identities i
      JOIN ppiq_meta.source_field_revisions r
        ON r.field_id = i.field_id AND r.revision = i.current_revision
     WHERE i.dataset_governance_id = p_governance_id
       AND r.locator_identity = p_locator_identity
     LIMIT 1;

    IF p_field_id IS NULL THEN
        v_target := v_bound;
    ELSE
        IF NOT EXISTS (
            SELECT 1 FROM ppiq_meta.source_field_identities i
             WHERE i.field_id = p_field_id
               AND i.tenant_id = p_tenant_id
               AND i.dataset_governance_id = p_governance_id) THEN
            RAISE EXCEPTION 'IAF06 field_not_found: field % is not governed by this dataset.', p_field_id
                USING ERRCODE = 'P0001';
        END IF;
        IF v_bound IS NOT NULL AND v_bound <> p_field_id THEN
            RAISE EXCEPTION 'IAF02 locator_bound_to_other_field: that locator already belongs to field %.', v_bound
                USING ERRCODE = 'P0001';
        END IF;
        v_target := p_field_id;
    END IF;

    IF v_target IS NOT NULL THEN
        SELECT r.* INTO v_cur
          FROM ppiq_meta.source_field_identities i
          JOIN ppiq_meta.source_field_revisions r
            ON r.field_id = i.field_id AND r.revision = i.current_revision
         WHERE i.field_id = v_target;
        v_has_cur := FOUND;
    END IF;

    IF v_has_cur THEN
        IF v_cur.semantic_hash = p_semantic_hash THEN
            RETURN QUERY SELECT v_target, v_cur.revision, 'unchanged'::text, false;
            RETURN;
        END IF;

        IF v_cur.locator_identity <> p_locator_identity AND p_reconcile IS DISTINCT FROM 'same_field' THEN
            RAISE EXCEPTION 'IAF03 locator_change_requires_reconciliation: field % reads a different locator; confirm same_field or register a new field.', v_target
                USING ERRCODE = 'P0001';
        END IF;

        IF (v_cur.declared_type <> p_declared_type OR v_cur.type_shape <> COALESCE(p_type_shape, '{}'::jsonb))
           AND p_reconcile IS DISTINCT FROM 'same_field' THEN
            RAISE EXCEPTION 'IAF04 type_change_requires_reconciliation: field % changes type; confirm same_field.', v_target
                USING ERRCODE = 'P0001';
        END IF;

        v_change := CASE
            WHEN v_cur.locator_identity <> p_locator_identity THEN 'reconciled_locator'
            WHEN v_cur.declared_type <> p_declared_type
                 OR v_cur.type_shape <> COALESCE(p_type_shape, '{}'::jsonb) THEN 'reconciled_type'
            ELSE 'metadata'
        END;
        v_next := v_cur.revision + 1;
    ELSE
        v_change := 'registered';
        v_next := 1;
    END IF;

    IF EXISTS (
        SELECT 1
          FROM ppiq_meta.source_field_identities i
          JOIN ppiq_meta.source_field_revisions r
            ON r.field_id = i.field_id AND r.revision = i.current_revision
         WHERE i.dataset_governance_id = p_governance_id
           AND r.field_key = p_field_key
           AND i.field_id IS DISTINCT FROM v_target) THEN
        RAISE EXCEPTION 'IAF05 field_key_not_unique: key % already belongs to another field.', p_field_key
            USING ERRCODE = 'P0001';
    END IF;

    IF v_target IS NULL THEN
        INSERT INTO ppiq_meta.source_field_identities (tenant_id, dataset_governance_id, created_by)
        VALUES (p_tenant_id, p_governance_id, p_actor)
        RETURNING field_id INTO v_target;
        v_new_field := true;
    END IF;

    INSERT INTO ppiq_meta.source_field_revisions
        (tenant_id, field_id, revision, provider_type, locator_kind, source_locator, locator_identity,
         field_key, display_name, declared_type, type_shape, source_unit, roles, layout_revision,
         change_kind, semantic_hash, created_by)
    VALUES
        (p_tenant_id, v_target, v_next, v_provider, p_locator_kind, p_source_locator, p_locator_identity,
         p_field_key, p_display_name, p_declared_type, COALESCE(p_type_shape, '{}'::jsonb), p_source_unit,
         p_roles, p_layout_revision, v_change, p_semantic_hash, p_actor);

    UPDATE ppiq_meta.source_field_identities
       SET current_revision = v_next
     WHERE field_id = v_target;

    RETURN QUERY SELECT v_target, v_next, v_change, true;
END
$fn$;

-- ---------------------------------------------------------------------------
-- 8. Layout revision declaration: identical content resolves to its revision.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION ppiq_meta.declare_source_layout_revision
(
    p_tenant_id         uuid,
    p_governance_id     uuid,
    p_layout_kind       text,
    p_region_bytes      integer,
    p_layout_document   jsonb,
    p_semantic_hash     text,
    p_actor             uuid DEFAULT NULL
)
RETURNS TABLE (out_revision integer, out_created boolean)
LANGUAGE plpgsql
SECURITY DEFINER
SET search_path = pg_catalog, ppiq_meta, pg_temp
AS $fn$
DECLARE
    v_gov_tenant    uuid;
    v_existing      integer;
    v_next          integer;
    v_missing       integer;
BEGIN
    SELECT g.tenant_id INTO v_gov_tenant
      FROM ppiq_meta.source_dataset_governance g
     WHERE g.id = p_governance_id;

    IF v_gov_tenant IS NULL OR p_tenant_id IS NULL OR v_gov_tenant <> p_tenant_id THEN
        RAISE EXCEPTION 'IAG03 dataset_not_governed: the dataset is not governed by this tenant.'
            USING ERRCODE = 'P0001';
    END IF;

    IF p_layout_document IS NULL OR jsonb_typeof(p_layout_document -> 'members') <> 'array' THEN
        RAISE EXCEPTION 'IAL01 layout_invalid: the layout document declares no members.'
            USING ERRCODE = 'P0001';
    END IF;

    PERFORM pg_advisory_xact_lock(hashtextextended('ppiq.dataset.layouts|' || p_governance_id::text, 0));

    SELECT count(*) INTO v_missing
      FROM jsonb_array_elements(p_layout_document -> 'members') m
     WHERE NOT EXISTS (
        SELECT 1 FROM ppiq_meta.source_field_identities i
         WHERE i.dataset_governance_id = p_governance_id
           AND i.field_id::text = (m ->> 'fieldId'));

    IF v_missing > 0 THEN
        RAISE EXCEPTION 'IAL01 layout_invalid: % member(s) name a field this dataset does not govern.', v_missing
            USING ERRCODE = 'P0001';
    END IF;

    SELECT l.revision INTO v_existing
      FROM ppiq_meta.source_layout_revisions l
     WHERE l.dataset_governance_id = p_governance_id
       AND l.semantic_hash = p_semantic_hash;

    IF v_existing IS NOT NULL THEN
        RETURN QUERY SELECT v_existing, false;
        RETURN;
    END IF;

    SELECT COALESCE(max(l.revision), 0) + 1 INTO v_next
      FROM ppiq_meta.source_layout_revisions l
     WHERE l.dataset_governance_id = p_governance_id;

    INSERT INTO ppiq_meta.source_layout_revisions
        (tenant_id, dataset_governance_id, revision, layout_kind, region_bytes, layout_document, semantic_hash, created_by)
    VALUES
        (p_tenant_id, p_governance_id, v_next, p_layout_kind, p_region_bytes, p_layout_document, p_semantic_hash, p_actor);

    RETURN QUERY SELECT v_next, true;
END
$fn$;

REVOKE ALL ON FUNCTION ppiq_meta.govern_source_dataset(uuid, uuid, uuid) FROM PUBLIC;
REVOKE ALL ON FUNCTION ppiq_meta.declare_source_field_revision(
    uuid, uuid, uuid, text, jsonb, text, text, text, text, jsonb, text, text[], integer, text, text, uuid) FROM PUBLIC;
REVOKE ALL ON FUNCTION ppiq_meta.declare_source_layout_revision(
    uuid, uuid, text, integer, jsonb, text, uuid) FROM PUBLIC;

-- ---------------------------------------------------------------------------
-- 9. Drift refusal. IF NOT EXISTS is not evidence of the contract.
-- ---------------------------------------------------------------------------
DO $shape$
DECLARE
    v_missing text[];
BEGIN
    SELECT array_agg(required.t || '.' || required.c ORDER BY required.t, required.c)
      INTO v_missing
      FROM (VALUES
        ('source_dataset_governance','tenant_id'),('source_dataset_governance','source_dataset_definition_id'),
        ('source_dataset_governance','provider_type'),
        ('source_field_identities','field_id'),('source_field_identities','current_revision'),
        ('source_field_revisions','locator_identity'),('source_field_revisions','semantic_hash'),
        ('source_field_revisions','field_key'),('source_field_revisions','declared_type'),
        ('source_layout_revisions','layout_document'),('source_layout_revisions','semantic_hash'),
        ('acquisition_configuration_details','dataset_governance_id'),
        ('acquisition_configuration_details','recording_groups')
      ) AS required(t, c)
     WHERE NOT EXISTS (
         SELECT 1 FROM information_schema.columns col
          WHERE col.table_schema = 'ppiq_meta' AND col.table_name = required.t AND col.column_name = required.c);
    IF v_missing IS NOT NULL THEN
        RAISE EXCEPTION 'PPIQ_ACQUISITION_DRIFT: missing columns %', v_missing;
    END IF;

    IF NOT EXISTS (
        SELECT 1 FROM pg_constraint
         WHERE conname = 'ck_definition_store_kind'
           AND pg_get_constraintdef(oid) LIKE '%acquisition_configuration%') THEN
        RAISE EXCEPTION 'PPIQ_ACQUISITION_DRIFT: the definition kind authority does not admit acquisition_configuration.';
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname = 'ux_source_dataset_governance_dataset') THEN
        RAISE EXCEPTION 'PPIQ_ACQUISITION_DRIFT: one-tenant-per-dataset constraint is missing.';
    END IF;
END
$shape$;

-- ---------------------------------------------------------------------------
-- 10. Runtime role. Governed tables are read directly and written only through
-- the governed functions. The detail projection is written by the canonical
-- definition writer, like every other detail table.
-- ---------------------------------------------------------------------------
DO $grants$
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plantprocess_app') THEN
        GRANT USAGE ON SCHEMA ppiq_meta TO plantprocess_app;
        REVOKE INSERT, UPDATE, DELETE, TRUNCATE ON ppiq_meta.source_dataset_governance FROM plantprocess_app;
        REVOKE INSERT, UPDATE, DELETE, TRUNCATE ON ppiq_meta.source_field_identities FROM plantprocess_app;
        REVOKE INSERT, UPDATE, DELETE, TRUNCATE ON ppiq_meta.source_field_revisions FROM plantprocess_app;
        REVOKE INSERT, UPDATE, DELETE, TRUNCATE ON ppiq_meta.source_layout_revisions FROM plantprocess_app;
        GRANT SELECT ON ppiq_meta.source_dataset_governance TO plantprocess_app;
        GRANT SELECT ON ppiq_meta.source_field_identities TO plantprocess_app;
        GRANT SELECT ON ppiq_meta.source_field_revisions TO plantprocess_app;
        GRANT SELECT ON ppiq_meta.source_layout_revisions TO plantprocess_app;
        GRANT SELECT, INSERT, UPDATE, DELETE ON ppiq_meta.acquisition_configuration_details TO plantprocess_app;
        GRANT EXECUTE ON FUNCTION ppiq_meta.govern_source_dataset(uuid, uuid, uuid) TO plantprocess_app;
        GRANT EXECUTE ON FUNCTION ppiq_meta.declare_source_field_revision(
            uuid, uuid, uuid, text, jsonb, text, text, text, text, jsonb, text, text[], integer, text, text, uuid) TO plantprocess_app;
        GRANT EXECUTE ON FUNCTION ppiq_meta.declare_source_layout_revision(
            uuid, uuid, text, integer, jsonb, text, uuid) TO plantprocess_app;
    END IF;
END
$grants$;

COMMENT ON TABLE ppiq_meta.source_dataset_governance IS
    'PPIQ industrial integration. Explicit tenant governance of one legacy source dataset. One tenant per dataset.';
COMMENT ON TABLE ppiq_meta.source_field_identities IS
    'PPIQ industrial integration. Stable provider-neutral field identity. Canonical field/tag identity for industrial integration; the legacy connector tag catalogue is compatibility only.';
COMMENT ON TABLE ppiq_meta.source_field_revisions IS
    'PPIQ industrial integration. Immutable field revisions: locator, key, label, type, unit and roles.';
COMMENT ON TABLE ppiq_meta.source_layout_revisions IS
    'PPIQ industrial integration. Immutable declarative raw layout revisions.';

COMMIT;