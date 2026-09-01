\set ON_ERROR_STOP on
\qecho PPIQ_PSQL_EXECUTED 835_analysis_subject_grain_authority

-- ============================================================================
-- PlantProcess IQ - T-231
-- Persist the frozen T-209 Analysis Subject + Grain authority.
--
-- This migration does NOT create a seventeenth DefinitionKind. Grain definitions
-- bind to an existing canonical definition identity and an exact canonical version.
-- Analysis subjects are tenant-scoped resolved identities in ppiq_plant.
-- No default grain is seeded and there is no material FK.
-- ============================================================================

BEGIN;

CREATE EXTENSION IF NOT EXISTS pgcrypto;
CREATE SCHEMA IF NOT EXISTS ppiq_meta;
CREATE SCHEMA IF NOT EXISTS ppiq_plant;

DO $predecessor$
BEGIN
    IF to_regclass('ppiq_meta.definition_store') IS NULL
       OR to_regclass('ppiq_meta.definition_versions') IS NULL THEN
        RAISE EXCEPTION
            'PPIQ_835_PREDECESSOR_MISSING: canonical definition_store/definition_versions must exist before T-231.';
    END IF;
    IF to_regclass('ppiq_meta.tenants') IS NULL THEN
        RAISE EXCEPTION
            'PPIQ_835_PREDECESSOR_MISSING: ppiq_meta.tenants must exist before T-231.';
    END IF;
END
$predecessor$;

CREATE TABLE IF NOT EXISTS ppiq_meta.analysis_grain_definitions
(
    id                              uuid        NOT NULL DEFAULT gen_random_uuid(),
    tenant_id                       uuid        NOT NULL,
    grain_code                      varchar(80) NOT NULL,
    grain_kind                      varchar(32) NOT NULL,
    time_semantics                  varchar(16) NOT NULL,
    identity_definition_id          uuid        NOT NULL,
    identity_definition_version     integer     NOT NULL,
    parent_grain_code               varchar(80) NULL,
    is_primary                      boolean     NOT NULL DEFAULT false,
    expected_cardinality_per_day    bigint      NULL,
    effective_from_utc              timestamptz NOT NULL,
    effective_to_utc                timestamptz NULL,
    created_at_utc                  timestamptz NOT NULL DEFAULT now(),
    created_by                      uuid        NULL,
    source_system                   varchar(100) NULL,
    source_record_id                varchar(200) NULL,

    CONSTRAINT pk_analysis_grain_definitions PRIMARY KEY (id),
    CONSTRAINT fk_analysis_grain_tenant FOREIGN KEY (tenant_id)
        REFERENCES ppiq_meta.tenants (id) ON DELETE RESTRICT,
    CONSTRAINT fk_analysis_grain_definition FOREIGN KEY (identity_definition_id)
        REFERENCES ppiq_meta.definition_store (id) ON DELETE RESTRICT,
    CONSTRAINT fk_analysis_grain_definition_version
        FOREIGN KEY (identity_definition_id, identity_definition_version)
        REFERENCES ppiq_meta.definition_versions (definition_id, version_number)
        ON DELETE RESTRICT,
    CONSTRAINT ux_analysis_grain_tenant_code_effective
        UNIQUE (tenant_id, grain_code, effective_from_utc),
    CONSTRAINT ck_analysis_grain_kind CHECK (grain_kind IN
        ('DiscreteEntity','Batch','Lot','Campaign','ProcessWindow','FlowInterval','Custom')),
    CONSTRAINT ck_analysis_grain_time_semantics CHECK (time_semantics IN ('Instant','Interval')),
    CONSTRAINT ck_analysis_grain_code_nonblank CHECK (btrim(grain_code) <> ''),
    CONSTRAINT ck_analysis_grain_parent_nonblank CHECK
        (parent_grain_code IS NULL OR btrim(parent_grain_code) <> ''),
    CONSTRAINT ck_analysis_grain_cardinality CHECK
        (expected_cardinality_per_day IS NULL OR expected_cardinality_per_day >= 0),
    CONSTRAINT ck_analysis_grain_effective_window CHECK
        (effective_to_utc IS NULL OR effective_to_utc > effective_from_utc),
    CONSTRAINT ck_analysis_grain_not_self_parent CHECK
        (parent_grain_code IS NULL OR parent_grain_code <> grain_code)
);

CREATE INDEX IF NOT EXISTS ix_analysis_grain_tenant_code
    ON ppiq_meta.analysis_grain_definitions (tenant_id, grain_code, effective_from_utc DESC);
CREATE INDEX IF NOT EXISTS ix_analysis_grain_definition_version
    ON ppiq_meta.analysis_grain_definitions
       (tenant_id, identity_definition_id, identity_definition_version);
CREATE INDEX IF NOT EXISTS ix_analysis_grain_parent
    ON ppiq_meta.analysis_grain_definitions (tenant_id, parent_grain_code)
    WHERE parent_grain_code IS NOT NULL;

CREATE TABLE IF NOT EXISTS ppiq_plant.analysis_subjects
(
    subject_id              uuid        NOT NULL DEFAULT gen_random_uuid(),
    tenant_id               uuid        NOT NULL,
    grain_definition_id     uuid        NOT NULL,
    subject_kind            varchar(40) NOT NULL,
    entity_kind             varchar(80) NULL,
    entity_id               uuid        NULL,
    subject_key             varchar(200) NULL,
    window_from_utc         timestamptz NULL,
    window_to_utc           timestamptz NULL,
    context                 jsonb       NOT NULL DEFAULT '{}'::jsonb,
    lineage_hash            char(64)    NOT NULL,
    created_at_utc          timestamptz NOT NULL DEFAULT now(),
    created_by              uuid        NULL,
    source_system           varchar(100) NULL,
    source_record_id        varchar(200) NULL,

    CONSTRAINT pk_analysis_subjects PRIMARY KEY (subject_id),
    CONSTRAINT fk_analysis_subject_tenant FOREIGN KEY (tenant_id)
        REFERENCES ppiq_meta.tenants (id) ON DELETE RESTRICT,
    CONSTRAINT fk_analysis_subject_grain FOREIGN KEY (grain_definition_id)
        REFERENCES ppiq_meta.analysis_grain_definitions (id) ON DELETE RESTRICT,
    CONSTRAINT ux_analysis_subject_tenant_grain_lineage
        UNIQUE (tenant_id, grain_definition_id, lineage_hash),
    CONSTRAINT ck_analysis_subject_kind_nonblank CHECK (btrim(subject_kind) <> ''),
    CONSTRAINT ck_analysis_subject_key_nonblank CHECK
        (subject_key IS NULL OR btrim(subject_key) <> ''),
    CONSTRAINT ck_analysis_subject_identity_present CHECK
        (entity_id IS NOT NULL
         OR subject_key IS NOT NULL
         OR (window_from_utc IS NOT NULL AND window_to_utc IS NOT NULL)),
    CONSTRAINT ck_analysis_subject_window_pair CHECK
        ((window_from_utc IS NULL AND window_to_utc IS NULL)
         OR (window_from_utc IS NOT NULL AND window_to_utc IS NOT NULL)),
    CONSTRAINT ck_analysis_subject_window_order CHECK
        (window_to_utc IS NULL OR window_to_utc >= window_from_utc)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_analysis_subject_tenant_subject_key
    ON ppiq_plant.analysis_subjects (tenant_id, subject_key)
    WHERE subject_key IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_analysis_subject_tenant_grain
    ON ppiq_plant.analysis_subjects (tenant_id, grain_definition_id);
CREATE INDEX IF NOT EXISTS ix_analysis_subject_tenant_entity
    ON ppiq_plant.analysis_subjects (tenant_id, entity_kind, entity_id)
    WHERE entity_id IS NOT NULL;
CREATE INDEX IF NOT EXISTS ix_analysis_subject_tenant_window
    ON ppiq_plant.analysis_subjects (tenant_id, window_from_utc, window_to_utc)
    WHERE window_from_utc IS NOT NULL;

-- ---------------------------------------------------------------------------
-- One declaration function for every grain kind. It preserves T-209 semantics:
-- trim only, exact case, idempotent identical redeclaration, GR06 conflict,
-- GR02 missing parent, GR07 cycle. It binds to an exact published canonical
-- definition version and never invents a default grain.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION ppiq_meta.declare_analysis_grain
(
    p_tenant_id                       uuid,
    p_grain_code                      text,
    p_grain_kind                      text,
    p_time_semantics                  text,
    p_identity_definition_id          uuid,
    p_identity_definition_version     integer,
    p_parent_grain_code               text,
    p_is_primary                      boolean,
    p_expected_cardinality_per_day    bigint,
    p_effective_from_utc              timestamptz,
    p_effective_to_utc                timestamptz,
    p_created_by                      uuid DEFAULT NULL,
    p_source_system                   text DEFAULT NULL,
    p_source_record_id                text DEFAULT NULL
)
RETURNS uuid
LANGUAGE plpgsql
AS $fn$
DECLARE
    v_code       text := btrim(COALESCE(p_grain_code, ''));
    v_parent     text := NULLIF(btrim(COALESCE(p_parent_grain_code, '')), '');
    v_existing   ppiq_meta.analysis_grain_definitions%ROWTYPE;
    v_current    text;
    v_found      boolean;
    v_result     uuid;
    v_guard      integer := 0;
BEGIN
    IF p_tenant_id IS NULL THEN
        RAISE EXCEPTION 'GR02 grain_not_declared: tenant is required.' USING ERRCODE = 'P0001';
    END IF;
    IF v_code = '' THEN
        RAISE EXCEPTION 'GR02 grain_not_declared: grain code is required.' USING ERRCODE = 'P0001';
    END IF;
    IF p_effective_from_utc IS NULL THEN
        RAISE EXCEPTION 'GR02 grain_not_declared: effective_from_utc is required.' USING ERRCODE = 'P0001';
    END IF;

    IF NOT EXISTS
    (
        SELECT 1
          FROM ppiq_meta.definition_store s
          JOIN ppiq_meta.definition_versions v
            ON v.definition_id = s.id
           AND v.version_number = p_identity_definition_version
         WHERE s.id = p_identity_definition_id
           AND s.tenant_id = p_tenant_id
           AND v.tenant_id = p_tenant_id
           AND v.status = 'published'
           AND s.is_deleted = false
           AND v.is_deleted = false
    ) THEN
        RAISE EXCEPTION
            'GR02 grain_not_declared: canonical definition % version % is not a published definition for this tenant.',
            p_identity_definition_id, p_identity_definition_version
            USING ERRCODE = 'P0001';
    END IF;

    SELECT *
      INTO v_existing
      FROM ppiq_meta.analysis_grain_definitions g
     WHERE g.tenant_id = p_tenant_id
       AND g.grain_code = v_code
       AND g.effective_from_utc = p_effective_from_utc;

    IF FOUND THEN
        IF v_existing.grain_kind = p_grain_kind
           AND v_existing.time_semantics = p_time_semantics
           AND v_existing.identity_definition_id = p_identity_definition_id
           AND v_existing.identity_definition_version = p_identity_definition_version
           AND v_existing.parent_grain_code IS NOT DISTINCT FROM v_parent
           AND v_existing.is_primary = COALESCE(p_is_primary, false)
           AND v_existing.expected_cardinality_per_day IS NOT DISTINCT FROM p_expected_cardinality_per_day
           AND v_existing.effective_to_utc IS NOT DISTINCT FROM p_effective_to_utc THEN
            RETURN v_existing.id;
        END IF;

        RAISE EXCEPTION
            'GR06 conflicting_declaration: grain % already has a different declaration at %.',
            v_code, p_effective_from_utc
            USING ERRCODE = 'P0001';
    END IF;

    IF v_parent IS NOT NULL THEN
        IF v_parent = v_code THEN
            RAISE EXCEPTION 'GR07 lineage_cycle: grain % cannot parent itself.', v_code USING ERRCODE = 'P0001';
        END IF;

        SELECT EXISTS
        (
            SELECT 1
              FROM ppiq_meta.analysis_grain_definitions g
             WHERE g.tenant_id = p_tenant_id
               AND g.grain_code = v_parent
               AND g.effective_from_utc <= p_effective_from_utc
               AND (g.effective_to_utc IS NULL OR g.effective_to_utc > p_effective_from_utc)
        ) INTO v_found;

        IF NOT v_found THEN
            RAISE EXCEPTION
                'GR02 grain_not_declared: parent grain % is not declared for this tenant/effective time.',
                v_parent USING ERRCODE = 'P0001';
        END IF;

        v_current := v_parent;
        LOOP
            EXIT WHEN v_current IS NULL;
            v_guard := v_guard + 1;
            IF v_guard > 64 THEN
                RAISE EXCEPTION 'GR07 lineage_cycle: lineage depth exceeded 64 while declaring %.', v_code USING ERRCODE = 'P0001';
            END IF;
            IF v_current = v_code THEN
                RAISE EXCEPTION 'GR07 lineage_cycle: declaring % beneath % closes a cycle.', v_code, v_parent USING ERRCODE = 'P0001';
            END IF;

            SELECT g.parent_grain_code
              INTO v_current
              FROM ppiq_meta.analysis_grain_definitions g
             WHERE g.tenant_id = p_tenant_id
               AND g.grain_code = v_current
               AND g.effective_from_utc <= p_effective_from_utc
               AND (g.effective_to_utc IS NULL OR g.effective_to_utc > p_effective_from_utc)
             ORDER BY g.effective_from_utc DESC
             LIMIT 1;

            IF NOT FOUND THEN
                v_current := NULL;
            END IF;
        END LOOP;
    END IF;

    INSERT INTO ppiq_meta.analysis_grain_definitions
    (
        tenant_id, grain_code, grain_kind, time_semantics,
        identity_definition_id, identity_definition_version,
        parent_grain_code, is_primary, expected_cardinality_per_day,
        effective_from_utc, effective_to_utc,
        created_by, source_system, source_record_id
    )
    VALUES
    (
        p_tenant_id, v_code, p_grain_kind, p_time_semantics,
        p_identity_definition_id, p_identity_definition_version,
        v_parent, COALESCE(p_is_primary, false), p_expected_cardinality_per_day,
        p_effective_from_utc, p_effective_to_utc,
        p_created_by,
        NULLIF(btrim(COALESCE(p_source_system, '')), ''),
        NULLIF(btrim(COALESCE(p_source_record_id, '')), '')
    )
    RETURNING id INTO v_result;

    RETURN v_result;
END
$fn$;

-- ---------------------------------------------------------------------------
-- Subject identity. The hash is a stable identity key only; it contains no
-- industry vocabulary and no material assumption. A subject_key is unique per
-- tenant because the frozen T-209 registry resolves a subject by that key.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION ppiq_plant.declare_analysis_subject
(
    p_tenant_id              uuid,
    p_grain_definition_id    uuid,
    p_subject_kind           text,
    p_entity_kind            text,
    p_entity_id              uuid,
    p_subject_key            text,
    p_window_from_utc        timestamptz,
    p_window_to_utc          timestamptz,
    p_context                jsonb DEFAULT '{}'::jsonb,
    p_created_by             uuid DEFAULT NULL,
    p_source_system          text DEFAULT NULL,
    p_source_record_id       text DEFAULT NULL
)
RETURNS uuid
LANGUAGE plpgsql
AS $fn$
DECLARE
    v_subject_key   text := NULLIF(btrim(COALESCE(p_subject_key, '')), '');
    v_entity_kind   text := NULLIF(btrim(COALESCE(p_entity_kind, '')), '');
    v_subject_kind  text := btrim(COALESCE(p_subject_kind, ''));
    v_context       jsonb := COALESCE(p_context, '{}'::jsonb);
    v_hash          char(64);
    v_existing      ppiq_plant.analysis_subjects%ROWTYPE;
    v_result        uuid;
BEGIN
    IF p_tenant_id IS NULL OR p_grain_definition_id IS NULL OR v_subject_kind = '' THEN
        RAISE EXCEPTION 'GR01 subject_not_declared: tenant, grain and subject kind are required.' USING ERRCODE = 'P0001';
    END IF;

    IF (p_window_from_utc IS NULL) <> (p_window_to_utc IS NULL) THEN
        RAISE EXCEPTION 'GR01 subject_not_declared: interval requires both endpoints.' USING ERRCODE = 'P0001';
    END IF;
    IF p_window_from_utc IS NOT NULL AND p_window_to_utc < p_window_from_utc THEN
        RAISE EXCEPTION 'GR01 subject_not_declared: interval end precedes start.' USING ERRCODE = 'P0001';
    END IF;
    IF p_entity_id IS NULL AND v_subject_key IS NULL AND p_window_from_utc IS NULL THEN
        RAISE EXCEPTION 'GR01 subject_not_declared: no subject identity was declared.' USING ERRCODE = 'P0001';
    END IF;

    IF NOT EXISTS
    (
        SELECT 1
          FROM ppiq_meta.analysis_grain_definitions g
         WHERE g.id = p_grain_definition_id
           AND g.tenant_id = p_tenant_id
    ) THEN
        RAISE EXCEPTION 'GR02 grain_not_declared: grain definition is not owned by this tenant.' USING ERRCODE = 'P0001';
    END IF;

    v_hash := encode(
        digest(
            concat_ws('|',
                p_tenant_id::text,
                p_grain_definition_id::text,
                v_subject_kind,
                COALESCE(v_entity_kind, ''),
                COALESCE(p_entity_id::text, ''),
                COALESCE(v_subject_key, ''),
                COALESCE(to_char(p_window_from_utc AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.US'), ''),
                COALESCE(to_char(p_window_to_utc AT TIME ZONE 'UTC', 'YYYY-MM-DD"T"HH24:MI:SS.US'), '')
            ),
            'sha256'
        ),
        'hex'
    );

    IF v_subject_key IS NOT NULL THEN
        SELECT * INTO v_existing
          FROM ppiq_plant.analysis_subjects s
         WHERE s.tenant_id = p_tenant_id
           AND s.subject_key = v_subject_key;
    ELSE
        SELECT * INTO v_existing
          FROM ppiq_plant.analysis_subjects s
         WHERE s.tenant_id = p_tenant_id
           AND s.grain_definition_id = p_grain_definition_id
           AND s.lineage_hash = v_hash;
    END IF;

    IF FOUND THEN
        IF v_existing.grain_definition_id = p_grain_definition_id
           AND v_existing.subject_kind = v_subject_kind
           AND v_existing.entity_kind IS NOT DISTINCT FROM v_entity_kind
           AND v_existing.entity_id IS NOT DISTINCT FROM p_entity_id
           AND v_existing.subject_key IS NOT DISTINCT FROM v_subject_key
           AND v_existing.window_from_utc IS NOT DISTINCT FROM p_window_from_utc
           AND v_existing.window_to_utc IS NOT DISTINCT FROM p_window_to_utc
           AND v_existing.context = v_context THEN
            RETURN v_existing.subject_id;
        END IF;

        RAISE EXCEPTION
            'GR06 conflicting_declaration: subject % already resolves to a different declaration.',
            COALESCE(v_subject_key, v_hash::text)
            USING ERRCODE = 'P0001';
    END IF;

    INSERT INTO ppiq_plant.analysis_subjects
    (
        tenant_id, grain_definition_id, subject_kind,
        entity_kind, entity_id, subject_key,
        window_from_utc, window_to_utc,
        context, lineage_hash,
        created_by, source_system, source_record_id
    )
    VALUES
    (
        p_tenant_id, p_grain_definition_id, v_subject_kind,
        v_entity_kind, p_entity_id, v_subject_key,
        p_window_from_utc, p_window_to_utc,
        v_context, v_hash,
        p_created_by,
        NULLIF(btrim(COALESCE(p_source_system, '')), ''),
        NULLIF(btrim(COALESCE(p_source_record_id, '')), '')
    )
    RETURNING subject_id INTO v_result;

    RETURN v_result;
END
$fn$;

CREATE OR REPLACE FUNCTION ppiq_plant.resolve_analysis_subject
(
    p_tenant_id    uuid,
    p_subject_key  text
)
RETURNS TABLE
(
    subject_id                      uuid,
    tenant_id                       uuid,
    grain_definition_id             uuid,
    subject_kind                    varchar(40),
    entity_kind                     varchar(80),
    entity_id                       uuid,
    subject_key                     varchar(200),
    window_from_utc                 timestamptz,
    window_to_utc                   timestamptz,
    context                         jsonb,
    lineage_hash                    char(64),
    grain_code                      varchar(80),
    grain_kind                      varchar(32),
    time_semantics                  varchar(16),
    identity_definition_id          uuid,
    identity_definition_version     integer,
    grain_effective_from_utc        timestamptz,
    grain_effective_to_utc          timestamptz
)
LANGUAGE plpgsql
STABLE
AS $fn$
BEGIN
    IF p_tenant_id IS NULL OR NULLIF(btrim(COALESCE(p_subject_key, '')), '') IS NULL THEN
        RAISE EXCEPTION 'GR01 subject_not_declared: subject key is required.' USING ERRCODE = 'P0001';
    END IF;

    IF NOT EXISTS
    (
        SELECT 1
          FROM ppiq_plant.analysis_subjects s
         WHERE s.tenant_id = p_tenant_id
           AND s.subject_key = btrim(p_subject_key)
    ) THEN
        RAISE EXCEPTION 'GR01 subject_not_declared: subject % is not declared.', btrim(p_subject_key) USING ERRCODE = 'P0001';
    END IF;

    RETURN QUERY
    SELECT
        s.subject_id,
        s.tenant_id,
        s.grain_definition_id,
        s.subject_kind,
        s.entity_kind,
        s.entity_id,
        s.subject_key,
        s.window_from_utc,
        s.window_to_utc,
        s.context,
        s.lineage_hash,
        g.grain_code,
        g.grain_kind,
        g.time_semantics,
        g.identity_definition_id,
        g.identity_definition_version,
        g.effective_from_utc,
        g.effective_to_utc
    FROM ppiq_plant.analysis_subjects s
    JOIN ppiq_meta.analysis_grain_definitions g
      ON g.id = s.grain_definition_id
     AND g.tenant_id = s.tenant_id
    WHERE s.tenant_id = p_tenant_id
      AND s.subject_key = btrim(p_subject_key);
END
$fn$;

-- ---------------------------------------------------------------------------
-- Drift refusal. CREATE TABLE IF NOT EXISTS is not evidence that a pre-existing
-- object has the T-231 contract. Refuse incompatible shapes rather than silently
-- accepting them.
-- ---------------------------------------------------------------------------
DO $shape$
DECLARE
    v_missing text[];
BEGIN
    SELECT array_agg(required.column_name ORDER BY required.column_name)
      INTO v_missing
      FROM (VALUES
        ('id'),('tenant_id'),('grain_code'),('grain_kind'),('time_semantics'),
        ('identity_definition_id'),('identity_definition_version'),('parent_grain_code'),
        ('is_primary'),('expected_cardinality_per_day'),('effective_from_utc'),('effective_to_utc')
      ) AS required(column_name)
      WHERE NOT EXISTS
      (
          SELECT 1 FROM information_schema.columns c
           WHERE c.table_schema='ppiq_meta'
             AND c.table_name='analysis_grain_definitions'
             AND c.column_name=required.column_name
      );
    IF v_missing IS NOT NULL THEN
        RAISE EXCEPTION 'PPIQ_835_DRIFT: analysis_grain_definitions missing columns %', v_missing;
    END IF;

    SELECT array_agg(required.column_name ORDER BY required.column_name)
      INTO v_missing
      FROM (VALUES
        ('subject_id'),('tenant_id'),('grain_definition_id'),('subject_kind'),
        ('entity_kind'),('entity_id'),('subject_key'),('window_from_utc'),('window_to_utc'),
        ('context'),('lineage_hash')
      ) AS required(column_name)
      WHERE NOT EXISTS
      (
          SELECT 1 FROM information_schema.columns c
           WHERE c.table_schema='ppiq_plant'
             AND c.table_name='analysis_subjects'
             AND c.column_name=required.column_name
      );
    IF v_missing IS NOT NULL THEN
        RAISE EXCEPTION 'PPIQ_835_DRIFT: analysis_subjects missing columns %', v_missing;
    END IF;

    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_analysis_grain_definition_version') THEN
        RAISE EXCEPTION 'PPIQ_835_DRIFT: exact canonical definition-version FK is missing.';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_constraint WHERE conname='fk_analysis_subject_grain') THEN
        RAISE EXCEPTION 'PPIQ_835_DRIFT: analysis subject grain FK is missing.';
    END IF;
END
$shape$;

COMMENT ON TABLE ppiq_meta.analysis_grain_definitions IS
    'PPIQ T-231. Tenant-scoped durable projection of the frozen T-209 GrainDefinition contract, bound to an exact canonical definition version. No default grain is seeded.';
COMMENT ON TABLE ppiq_plant.analysis_subjects IS
    'PPIQ T-231. Tenant-scoped AnalysisSubject identities. Supports entity, equipment-window and flow-interval subjects with no mandatory material identifier.';

COMMIT;
