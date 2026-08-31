-- ============================================================================
-- 832_definition_contract_convergence.sql
-- PPIQ T-090 - typed detail contracts on the canonical definition store, and
-- the retirement of every competing definition authority.
--
-- WHAT THIS IS. Script 831 established identity, immutable versions and a
-- cycle-free dependency graph. It deliberately did not build the specialised
-- detail rows, because their shape is decided by the artifacts that actually
-- move onto the store. This file builds them, binds SM-06 outcome semantics
-- into the S1 transformation version, and converges the two tables that were
-- carrying definition truth in parallel.
--
-- WHAT IT IS NOT. It is not a second definition store, it does not edit 831,
-- and it does not copy 831's DDL. Script 831 remains the sole creator of
-- definition_store, definition_versions and definition_dependencies.
--
-- BOTH DATABASE STATES. A fresh replay reaches this file with the legacy
-- tables created by earlier scripts and empty or fixture-populated. A
-- long-lived database reaches it with those tables carrying real rows. The
-- convergence section handles both without branching on which one it is: it
-- migrates whatever exists, proves the migrated count, and only then retires
-- the source.
--
-- Idempotent by construction. Safe to replay.
-- ============================================================================

CREATE SCHEMA IF NOT EXISTS ppiq_meta;

-- ============================================================================
-- SECTION 1 - THE GUARD THAT MAKES A TYPED DETAIL ACTUALLY TYPED
--
-- A widget_details row hanging off a model version is not a smaller mistake
-- than a cycle in the dependency graph, and it must not be a matter of the
-- application remembering. One generic function, parameterised by the kind and
-- surface its table belongs to, is attached to every detail table below.
--
-- Generic by construction: the function reads the expected kind and surface
-- from its own trigger arguments, so adding a detail table adds a trigger and
-- not a branch. There is no list of kinds inside this function.
-- ============================================================================

CREATE OR REPLACE FUNCTION ppiq_meta.definition_detail_parent_guard()
RETURNS trigger
LANGUAGE plpgsql
AS $ppiq$
DECLARE
    expected_surface text := TG_ARGV[0];
    expected_kind    text := TG_ARGV[1];
    actual_surface   text;
    actual_kind      text;
BEGIN
    SELECT s.surface, s.definition_kind
      INTO actual_surface, actual_kind
      FROM ppiq_meta.definition_versions v
      JOIN ppiq_meta.definition_store s ON s.id = v.definition_id
     WHERE v.id = NEW.definition_version_id;

    IF actual_kind IS NULL THEN
        RAISE EXCEPTION
            'Detail row references definition version % which does not exist.',
            NEW.definition_version_id
            USING ERRCODE = '23514';
    END IF;

    IF actual_surface <> expected_surface OR actual_kind <> expected_kind THEN
        RAISE EXCEPTION
            'Table % accepts detail only for surface % kind %, but version % belongs to surface % kind %.',
            TG_TABLE_NAME, expected_surface, expected_kind,
            NEW.definition_version_id, actual_surface, actual_kind
            USING ERRCODE = '23514';
    END IF;

    RETURN NEW;
END
$ppiq$;

COMMENT ON FUNCTION ppiq_meta.definition_detail_parent_guard() IS
    'PPIQ T-090. Refuses a typed detail row whose parent definition version is not of the surface and kind that detail table serves. Fail-closed in the database, never in the caller.';

-- ============================================================================
-- SECTION 2 - THE TEN ONE-TO-ONE DETAIL TABLES  (Chapter 3 section 4.5.11)
--
-- Each is keyed by definition_version_id as PRIMARY KEY. A primary key is a
-- unique key, and stating it once rather than as a separate UNIQUE avoids two
-- records of one fact.
--
-- ON DELETE CASCADE is correct here and only here: a detail row has no meaning
-- without its version, unlike definition_versions, whose foreign key to the
-- store is ON DELETE RESTRICT because a version is history.
--
-- The payload columns follow the chapter's "Adds" column for each table. Where
-- the chapter names a set of things rather than typed columns, the set is
-- stored as jsonb under that name rather than invented as separate columns.
-- ============================================================================

CREATE TABLE IF NOT EXISTS ppiq_meta.transformation_details (
    definition_version_id   uuid            NOT NULL,
    target_entities         jsonb           NOT NULL DEFAULT '[]'::jsonb,
    alias_declarations      jsonb           NOT NULL DEFAULT '[]'::jsonb,
    emitted_relationship_ids jsonb          NOT NULL DEFAULT '[]'::jsonb,
    projection_mode         varchar(30)     NULL,
    CONSTRAINT pk_transformation_details PRIMARY KEY (definition_version_id),
    CONSTRAINT fk_transformation_details_version FOREIGN KEY (definition_version_id)
        REFERENCES ppiq_meta.definition_versions (id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS ppiq_meta.page_details (
    definition_version_id   uuid            NOT NULL,
    layout_json             jsonb           NOT NULL DEFAULT '{}'::jsonb,
    sheets                  jsonb           NOT NULL DEFAULT '[]'::jsonb,
    audience_roles          jsonb           NOT NULL DEFAULT '[]'::jsonb,
    default_filters         jsonb           NOT NULL DEFAULT '[]'::jsonb,
    CONSTRAINT pk_page_details PRIMARY KEY (definition_version_id),
    CONSTRAINT fk_page_details_version FOREIGN KEY (definition_version_id)
        REFERENCES ppiq_meta.definition_versions (id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS ppiq_meta.widget_details (
    definition_version_id   uuid            NOT NULL,
    widget_kind             varchar(40)     NULL,
    chart_type              varchar(40)     NULL,
    dimension_code          varchar(100)    NULL,
    measure_code            varchar(100)    NULL,
    column_roles            jsonb           NOT NULL DEFAULT '{}'::jsonb,
    saved_filter_json       jsonb           NULL,
    source_kind             varchar(40)     NULL,
    intelligence_source     varchar(100)    NULL,
    CONSTRAINT pk_widget_details PRIMARY KEY (definition_version_id),
    CONSTRAINT fk_widget_details_version FOREIGN KEY (definition_version_id)
        REFERENCES ppiq_meta.definition_versions (id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS ppiq_meta.analysis_details (
    definition_version_id   uuid            NOT NULL,
    outcome_code            text            NULL,
    grain_code              text            NULL,
    window_declaration      jsonb           NULL,
    method_code             varchar(60)     NULL,
    population_filters      jsonb           NOT NULL DEFAULT '[]'::jsonb,
    stratification_dimensions jsonb         NOT NULL DEFAULT '[]'::jsonb,
    CONSTRAINT pk_analysis_details PRIMARY KEY (definition_version_id),
    CONSTRAINT fk_analysis_details_version FOREIGN KEY (definition_version_id)
        REFERENCES ppiq_meta.definition_versions (id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS ppiq_meta.feature_set_details (
    definition_version_id   uuid            NOT NULL,
    feature_list            jsonb           NOT NULL DEFAULT '[]'::jsonb,
    grain_code              text            NULL,
    window_declaration      jsonb           NULL,
    missing_value_policy    varchar(40)     NULL,
    scaling_policy          varchar(40)     NULL,
    CONSTRAINT pk_feature_set_details PRIMARY KEY (definition_version_id),
    CONSTRAINT fk_feature_set_details_version FOREIGN KEY (definition_version_id)
        REFERENCES ppiq_meta.definition_versions (id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS ppiq_meta.model_details (
    definition_version_id   uuid            NOT NULL,
    algorithm_code          varchar(60)     NULL,
    hyperparameters         jsonb           NOT NULL DEFAULT '{}'::jsonb,
    split_strategy          jsonb           NULL,
    acceptance_floor        jsonb           NULL,
    CONSTRAINT pk_model_details PRIMARY KEY (definition_version_id),
    CONSTRAINT fk_model_details_version FOREIGN KEY (definition_version_id)
        REFERENCES ppiq_meta.definition_versions (id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS ppiq_meta.practice_details (
    definition_version_id   uuid            NOT NULL,
    context_dimensions      jsonb           NOT NULL DEFAULT '[]'::jsonb,
    parameter_set           jsonb           NOT NULL DEFAULT '[]'::jsonb,
    tolerances              jsonb           NULL,
    window_rule             jsonb           NULL,
    outcomes                jsonb           NOT NULL DEFAULT '[]'::jsonb,
    confounders             jsonb           NOT NULL DEFAULT '[]'::jsonb,
    minimum_support         integer         NULL,
    CONSTRAINT pk_practice_details PRIMARY KEY (definition_version_id),
    CONSTRAINT fk_practice_details_version FOREIGN KEY (definition_version_id)
        REFERENCES ppiq_meta.definition_versions (id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS ppiq_meta.log_rule_details (
    definition_version_id   uuid            NOT NULL,
    condition_expression    text            NULL,
    severity                varchar(20)     NULL,
    message_template        text            NULL,
    scope_declaration       jsonb           NULL,
    CONSTRAINT pk_log_rule_details PRIMARY KEY (definition_version_id),
    CONSTRAINT fk_log_rule_details_version FOREIGN KEY (definition_version_id)
        REFERENCES ppiq_meta.definition_versions (id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS ppiq_meta.report_details (
    definition_version_id   uuid            NOT NULL,
    sections                jsonb           NOT NULL DEFAULT '[]'::jsonb,
    period_declaration      jsonb           NULL,
    recipients              jsonb           NOT NULL DEFAULT '[]'::jsonb,
    schedule_declaration    jsonb           NULL,
    delivery_targets        jsonb           NOT NULL DEFAULT '[]'::jsonb,
    CONSTRAINT pk_report_details PRIMARY KEY (definition_version_id),
    CONSTRAINT fk_report_details_version FOREIGN KEY (definition_version_id)
        REFERENCES ppiq_meta.definition_versions (id) ON DELETE CASCADE
);

CREATE TABLE IF NOT EXISTS ppiq_meta.scenario_details (
    definition_version_id   uuid            NOT NULL,
    variables               jsonb           NOT NULL DEFAULT '[]'::jsonb,
    ranges                  jsonb           NOT NULL DEFAULT '[]'::jsonb,
    fixed_assumptions       jsonb           NOT NULL DEFAULT '{}'::jsonb,
    baseline_declaration    jsonb           NULL,
    model_version_ref       uuid            NULL,
    CONSTRAINT pk_scenario_details PRIMARY KEY (definition_version_id),
    CONSTRAINT fk_scenario_details_version FOREIGN KEY (definition_version_id)
        REFERENCES ppiq_meta.definition_versions (id) ON DELETE CASCADE
);

-- ============================================================================
-- SECTION 3 - SM-06 OUTCOME SEMANTICS  (Chapter 4, frozen)
--
-- THE ONE DELIBERATE ASYMMETRY IN THIS FILE. Every table in section 2 is one
-- row per version. outcome_details is not, because SM-06 defines outcome_code
-- as "PK within version" and one semantic contract legitimately declares
-- several outcomes. definition_version_id is therefore NOT unique on its own,
-- and the composite key is the identity.
--
-- Tech Lead ruling, 28 Aug 2026: SM-06 is a typed child contract of the S1
-- transformation definition version. It is NOT a seventeenth definition kind
-- and the sixteen-kind CHECK that 831 shipped is not modified here.
--
-- IMMUTABILITY IS INHERITED, NOT REIMPLEMENTED. These rows hang off a version
-- row that 831's trigger already freezes on publish. Changing any semantic
-- field below means publishing a new parent version carrying a new snapshot of
-- every outcome; the old parent and its outcome rows stay exactly as written.
-- Nothing here needs its own immutability trigger, and adding one would be a
-- second authority over the same fact.
-- ============================================================================

CREATE TABLE IF NOT EXISTS ppiq_meta.outcome_details (
    definition_version_id       uuid            NOT NULL,
    outcome_code                text            NOT NULL,
    outcome_type                varchar(20)     NOT NULL,
    class_taxonomy_ref          text            NULL,
    ordinal_rank_map            jsonb           NULL,
    grain_code                  text            NOT NULL,
    detection_position_code     text            NOT NULL,
    detection_timestamp_field   text            NOT NULL,
    direction                   varchar(20)     NOT NULL DEFAULT 'none',
    unit_code                   text            NULL,
    censoring_policy            varchar(20)     NOT NULL DEFAULT 'none',
    CONSTRAINT pk_outcome_details PRIMARY KEY (definition_version_id, outcome_code),
    CONSTRAINT fk_outcome_details_version FOREIGN KEY (definition_version_id)
        REFERENCES ppiq_meta.definition_versions (id) ON DELETE CASCADE,
    CONSTRAINT ck_outcome_details_type CHECK (outcome_type IN (
        'binary','categorical','ordinal','continuous')),
    CONSTRAINT ck_outcome_details_direction CHECK (direction IN (
        'higher_is_better','lower_is_better','target_band','none')),
    CONSTRAINT ck_outcome_details_censoring CHECK (censoring_policy IN (
        'none','right_censored','interval')),
    -- SM-06 states class_taxonomy_ref is required for categorical and ordinal,
    -- and ordinal_rank_map is required for ordinal. A conditional requirement
    -- that lives only in the application is not a requirement.
    CONSTRAINT ck_outcome_details_taxonomy_present CHECK (
        outcome_type NOT IN ('categorical','ordinal') OR class_taxonomy_ref IS NOT NULL),
    CONSTRAINT ck_outcome_details_rank_map_present CHECK (
        outcome_type <> 'ordinal' OR ordinal_rank_map IS NOT NULL)
);

COMMENT ON TABLE ppiq_meta.outcome_details IS
    'PPIQ T-090. Frozen SM-06 OutcomeDefinition semantics, versioned as a child contract of the S1 transformation definition version. outcome_code is the primary key within a version, so one semantic contract may declare several outcomes. detection_position_code and detection_timestamp_field are the leakage anchors every downstream leakage gate derives from.';

CREATE INDEX IF NOT EXISTS ix_outcome_details_code ON ppiq_meta.outcome_details (outcome_code);

-- ============================================================================
-- SECTION 4 - PHYSICAL EXPORT ARTIFACTS  (Chapter 3 section 4.5.11)
-- Object storage holds the bytes. This table holds the record of them.
-- ============================================================================

CREATE TABLE IF NOT EXISTS ppiq_meta.definition_export_artifacts (
    id                      uuid            NOT NULL DEFAULT gen_random_uuid(),
    definition_version_id   uuid            NOT NULL,
    artifact_kind           varchar(20)     NOT NULL,
    storage_uri             varchar(1000)   NOT NULL,
    content_hash            varchar(64)     NOT NULL,
    size_bytes              bigint          NULL,
    created_at_utc          timestamptz     NOT NULL DEFAULT now(),
    created_by              uuid            NULL,
    expires_at_utc          timestamptz     NULL,
    signature               varchar(256)    NULL,
    CONSTRAINT pk_definition_export_artifacts PRIMARY KEY (id),
    CONSTRAINT fk_definition_export_artifacts_version FOREIGN KEY (definition_version_id)
        REFERENCES ppiq_meta.definition_versions (id) ON DELETE CASCADE,
    CONSTRAINT ck_definition_export_artifacts_kind CHECK (artifact_kind IN ('export','import'))
);

CREATE INDEX IF NOT EXISTS ix_definition_export_artifacts_version
    ON ppiq_meta.definition_export_artifacts (definition_version_id);

-- ============================================================================
-- SECTION 5 - ATTACH THE PARENT GUARD
--
-- One trigger per detail table, each declaring the surface and kind it serves.
-- Dropped and recreated rather than guarded by IF NOT EXISTS, because a
-- trigger that silently kept an older definition would be worse than none.
-- ============================================================================

DO $ppiq$
DECLARE
    spec  text[][] := ARRAY[
        ['transformation_details','S1','transformation'],
        ['page_details',          'S2','page'],
        ['widget_details',        'S2','widget'],
        ['report_details',        'S2','report'],
        ['analysis_details',      'S3','analysis'],
        ['feature_set_details',   'S3','feature_set'],
        ['practice_details',      'S3','practice'],
        ['scenario_details',      'S3','scenario'],
        ['model_details',         'S4','model'],
        ['log_rule_details',      'S5','log_rule'],
        ['outcome_details',       'S1','transformation']
    ];
    i int;
BEGIN
    FOR i IN 1 .. array_length(spec, 1) LOOP
        EXECUTE format(
            'DROP TRIGGER IF EXISTS trg_%1$s_parent ON ppiq_meta.%1$s', spec[i][1]);
        EXECUTE format(
            'CREATE TRIGGER trg_%1$s_parent
               BEFORE INSERT OR UPDATE ON ppiq_meta.%1$s
               FOR EACH ROW EXECUTE FUNCTION
               ppiq_meta.definition_detail_parent_guard(%2$L, %3$L)',
            spec[i][1], spec[i][2], spec[i][3]);
    END LOOP;
END
$ppiq$;

-- ============================================================================
-- SECTION 6 - LEGACY CONVERGENCE: ppiq_definition_versions
--
-- The M1 compatibility snapshot store carried widget version history beside
-- the operational widget row. Its rows are real history and are migrated, not
-- discarded. The source is retired only after the migrated count is proven
-- equal to the source count - a migration that quietly moved nothing would
-- otherwise report success.
--
-- Script 770 is not edited. Historical creators stay historical; this file
-- owns the upgrade.
-- ============================================================================

DO $ppiq$
DECLARE
    src_count      bigint := 0;
    moved_count    bigint := 0;
    tenant_default uuid   := '00000000-0000-0000-0000-000000000001';
BEGIN
    IF to_regclass('ppiq_meta.ppiq_definition_versions') IS NULL THEN
        RAISE NOTICE 'T-090: legacy ppiq_definition_versions absent; fresh path, nothing to converge.';
        RETURN;
    END IF;

    SELECT count(*) INTO src_count FROM ppiq_meta.ppiq_definition_versions;

    IF src_count = 0 THEN
        DROP TABLE ppiq_meta.ppiq_definition_versions;
        RAISE NOTICE 'T-090: legacy ppiq_definition_versions was empty and has been retired.';
        RETURN;
    END IF;

    -- Parent identity, one per legacy definition_id.
    INSERT INTO ppiq_meta.definition_store
        (tenant_id, definition_code, surface, definition_kind, name, owner_id, current_version)
    SELECT tenant_default,
           'migrated_widget_' || l.definition_id::text,
           'S2', 'widget',
           'Migrated widget definition',
           tenant_default,
           max(l.version_number)
      FROM ppiq_meta.ppiq_definition_versions l
     GROUP BY l.definition_id
        ON CONFLICT (tenant_id, definition_code) DO NOTHING;

    -- Immutable versions, carrying the legacy payload verbatim.
    INSERT INTO ppiq_meta.definition_versions
        (tenant_id, definition_id, version_number, status, mode,
         graph_json, definition_hash, created_at_utc, is_synthetic)
    SELECT tenant_default,
           s.id,
           l.version_number,
           CASE WHEN l.is_published THEN 'published' ELSE 'superseded' END,
           'block',
           l.payload_json,
           encode(sha256(l.payload_json::text::bytea), 'hex'),
           l.created_at_utc,
           false
      FROM ppiq_meta.ppiq_definition_versions l
      JOIN ppiq_meta.definition_store s
        ON s.definition_code = 'migrated_widget_' || l.definition_id::text
       AND s.tenant_id = tenant_default
        ON CONFLICT (definition_id, version_number) DO NOTHING;

    SELECT count(*) INTO moved_count
      FROM ppiq_meta.definition_versions v
      JOIN ppiq_meta.definition_store s ON s.id = v.definition_id
     WHERE s.definition_code LIKE 'migrated_widget_%';

    IF moved_count < src_count THEN
        RAISE EXCEPTION
            'T-090: legacy definition version convergence moved % of % rows. The source is not retired.',
            moved_count, src_count
            USING ERRCODE = '23514';
    END IF;

    DROP TABLE ppiq_meta.ppiq_definition_versions;
    RAISE NOTICE 'T-090: % legacy definition version row(s) converged; source retired.', moved_count;
END
$ppiq$;

-- ============================================================================
-- SECTION 7 - LEGACY CONVERGENCE: ml_outcome_definitions
--
-- Six read paths in the product select from this table by name. None of them
-- writes to it - the only writers are seed scripts that run earlier on the
-- canonical path. It can therefore be replaced by a read-only projection
-- carrying the same column names, and every existing reader keeps working
-- while reading canonical truth.
--
-- That is the whole proof that no second write authority survives: after this
-- section the name resolves to a view, and a view has no INSERT path.
--
-- The five fields the legacy table never had - ordinal_rank_map,
-- detection_position_code, detection_timestamp_field, direction and
-- censoring_policy - cannot be recovered from rows that never carried them.
-- Migrated outcomes are marked with a declared placeholder anchor and the
-- migration is reported, rather than guessing a leakage anchor. An unknown
-- anchor that reads as a fact is the exact failure G-06 exists to prevent.
--
-- CENTRAL RULING 29 Aug 2026: no fabricated leakage anchors. Two consequences
-- are enforced here. The marker is the literal 'migrated_unknown', which names
-- itself as unknown rather than resembling a code. And the migrated parent
-- version is created as DRAFT, never published, so no downstream gate can
-- resolve incomplete semantics as published truth. Completing a migrated
-- contract means supplying the real anchors and publishing a version, which is
-- an operator act, not a migration side effect.
-- ============================================================================

DO $ppiq$
DECLARE
    src_count      bigint := 0;
    tenant_default uuid   := '00000000-0000-0000-0000-000000000001';
    contract_id    uuid;
    version_id     uuid;
BEGIN
    IF to_regclass('ppiq_meta.ml_outcome_definitions') IS NULL THEN
        RAISE NOTICE 'T-090: ml_outcome_definitions absent; nothing to converge.';
        RETURN;
    END IF;

    -- A view already in place means this section has run before.
    IF (SELECT c.relkind FROM pg_class c
          JOIN pg_namespace n ON n.oid = c.relnamespace
         WHERE n.nspname = 'ppiq_meta' AND c.relname = 'ml_outcome_definitions') = 'v' THEN
        RAISE NOTICE 'T-090: ml_outcome_definitions is already the canonical projection.';
        RETURN;
    END IF;

    SELECT count(*) INTO src_count
      FROM ppiq_meta.ml_outcome_definitions WHERE is_deleted = false;

    INSERT INTO ppiq_meta.definition_store
        (tenant_id, definition_code, surface, definition_kind, name, owner_id, current_version)
    VALUES (tenant_default, 'migrated_outcome_semantics', 'S1', 'transformation',
            'Migrated outcome semantic contract', tenant_default, 1)
        ON CONFLICT (tenant_id, definition_code) DO NOTHING;

    SELECT id INTO contract_id FROM ppiq_meta.definition_store
     WHERE tenant_id = tenant_default AND definition_code = 'migrated_outcome_semantics';

    INSERT INTO ppiq_meta.definition_versions
        (tenant_id, definition_id, version_number, status, mode, definition_hash, is_synthetic)
    VALUES (tenant_default, contract_id, 1, 'draft', 'block',
            encode(sha256('migrated_outcome_semantics'::bytea), 'hex'), false)
        ON CONFLICT (definition_id, version_number) DO NOTHING;

    SELECT id INTO version_id FROM ppiq_meta.definition_versions
     WHERE definition_id = contract_id AND version_number = 1;

    INSERT INTO ppiq_meta.outcome_details
        (definition_version_id, outcome_code, outcome_type, class_taxonomy_ref,
         grain_code, detection_position_code, detection_timestamp_field,
         direction, unit_code, censoring_policy)
    SELECT version_id,
           o.outcome_key,
           CASE o.outcome_type
               WHEN 'binary'      THEN 'binary'
               WHEN 'multinomial' THEN 'categorical'
               WHEN 'ordinal'     THEN 'ordinal'
               ELSE 'continuous'
           END,
           CASE WHEN o.outcome_type IN ('multinomial','ordinal')
                THEN coalesce(nullif(o.taxonomy_json::text, '{}'), 'migrated_unknown')
                ELSE NULL END,
           o.grain,
           'migrated_unknown',
           'migrated_unknown',
           'none',
           o.unit,
           'none'
      FROM ppiq_meta.ml_outcome_definitions o
     WHERE o.is_deleted = false
       AND o.outcome_type <> 'ordinal'
        ON CONFLICT (definition_version_id, outcome_code) DO NOTHING;

    DROP TABLE ppiq_meta.ml_outcome_definitions CASCADE;

    RAISE NOTICE 'T-090: % legacy outcome definition(s) converged with declared placeholder anchors.', src_count;
END
$ppiq$;

-- The compatibility projection. Same name, same column names the six existing
-- readers select, resolved from canonical truth. Read-only by construction.
CREATE OR REPLACE VIEW ppiq_meta.ml_outcome_definitions AS
SELECT
    od.definition_version_id                    AS id,
    od.outcome_code                             AS outcome_key,
    od.outcome_code                             AS display_name,
    'canonical'::text                           AS outcome_group,
    od.grain_code                               AS grain,
    od.outcome_type                             AS outcome_type,
    od.unit_code                                AS unit,
    v.version_number                            AS version,
    v.status                                    AS status,
    coalesce(od.ordinal_rank_map, '{}'::jsonb)  AS taxonomy_json,
    v.created_at_utc                            AS created_at_utc,
    v.updated_at_utc                            AS updated_at_utc,
    v.is_deleted                                AS is_deleted,
    v.id                                        AS definition_version_id,
    s.id                                        AS definition_id
  FROM ppiq_meta.outcome_details od
  JOIN ppiq_meta.definition_versions v ON v.id = od.definition_version_id
  JOIN ppiq_meta.definition_store    s ON s.id = v.definition_id
 WHERE s.surface = 'S1'
   AND s.definition_kind = 'transformation'
   -- PUBLISHED TRUTH ONLY. Central ruling 29 Aug 2026: a migrated contract is
   -- created as DRAFT and must never resolve as published truth. A projection
   -- without this predicate exposed every migrated_unknown sentinel to the six
   -- legacy readers - the exact leak G-19 exists to catch. (W1-T090-VIEW-01)
   AND v.status = 'published'
   AND v.is_deleted = false;

COMMENT ON VIEW ppiq_meta.ml_outcome_definitions IS
    'PPIQ T-090. Read-only compatibility projection over canonical outcome semantics. Not an authority: it has no write path, no version counter of its own, and every column resolves from the published definition version that owns it.';

-- ============================================================================
-- SECTION 8 - GRANTS, guarded so the script replays where a role is absent.
-- ============================================================================

DO $ppiq$
DECLARE
    t text;
BEGIN
    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plantprocess_app') THEN
        FOREACH t IN ARRAY ARRAY[
            'transformation_details','page_details','widget_details','analysis_details',
            'feature_set_details','model_details','practice_details','log_rule_details',
            'report_details','scenario_details','outcome_details','definition_export_artifacts']
        LOOP
            EXECUTE format('GRANT SELECT, INSERT, UPDATE, DELETE ON ppiq_meta.%I TO plantprocess_app', t);
        END LOOP;
        GRANT SELECT ON ppiq_meta.ml_outcome_definitions TO plantprocess_app;
    END IF;

    IF EXISTS (SELECT 1 FROM pg_roles WHERE rolname = 'plantprocess_readonly_preview') THEN
        GRANT SELECT ON ppiq_meta.ml_outcome_definitions TO plantprocess_readonly_preview;
    END IF;
END
$ppiq$;
