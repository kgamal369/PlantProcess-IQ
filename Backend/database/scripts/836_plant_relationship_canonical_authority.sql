-- =============================================================================
-- 836_plant_relationship_canonical_authority.sql
-- The canonical plant relationship model.
--
-- Script 827 created a compatibility surface in the public schema and said, in
-- its own header, that it was not the final model. This script is the final
-- model: ppiq_meta.plant_relationships, ppiq_meta.plant_relationship_members
-- and ppiq_meta.plant_relationship_paths, exactly as the design chapter's
-- relationship authority declares them.
--
-- Three things become possible here that 827 deliberately refused to fake:
--
--   * the schema is ppiq_meta, which now exists;
--   * source_definition_id carries a real FOREIGN KEY to definition_store,
--     because the definition lifecycle now exists. A relationship can no longer
--     name a definition that was never published;
--   * the compatibility surface is retired rather than left beside the
--     canonical one, because two places to look is the same defect as no place.
--
-- Convergence is fail-closed. Legacy rows move only when their definition
-- identity actually resolves; if any row cannot move, the legacy tables are
-- kept and the script raises rather than dropping data it could not carry.
--
-- Replayable: safe to run more than once.
-- =============================================================================

CREATE SCHEMA IF NOT EXISTS ppiq_meta;

-- -----------------------------------------------------------------------------
-- One declared relationship.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ppiq_meta.plant_relationships
(
    id                        uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id                 uuid         NOT NULL,
    relationship_code         varchar(100) NOT NULL,
    left_entity               varchar(100) NOT NULL,
    right_entity              varchar(100) NOT NULL,
    join_type                 varchar(10)  NOT NULL,
    cardinality               varchar(10)  NOT NULL,
    grain_left                varchar(50)  NOT NULL,
    grain_right               varchar(50)  NOT NULL,
    is_grain_converting       boolean      GENERATED ALWAYS AS (grain_left <> grain_right) STORED,
    attribution_rule          varchar(30)  NULL,
    attribution_expression    text         NULL,
    is_preferred_path         boolean      NOT NULL DEFAULT false,
    ambiguity_state           varchar(20)  NOT NULL DEFAULT 'unambiguous',
    validation_state          varchar(20)  NOT NULL DEFAULT 'unproven',
    validation_detail         jsonb        NULL,
    source_definition_id      uuid         NOT NULL
        REFERENCES ppiq_meta.definition_store (id) ON DELETE RESTRICT,
    source_definition_version integer      NOT NULL,
    effective_from_utc        timestamptz  NOT NULL,
    retired_at_utc            timestamptz  NULL,

    CONSTRAINT ck_plant_relationships_join_type
        CHECK (join_type IN ('inner', 'left', 'right', 'full')),
    CONSTRAINT ck_plant_relationships_cardinality
        CHECK (cardinality IN ('1-1', '1-n', 'n-1', 'n-m')),
    CONSTRAINT ck_plant_relationships_attribution_rule
        CHECK (attribution_rule IS NULL
               OR attribution_rule IN ('weighted', 'equal_split', 'first_parent', 'none')),
    CONSTRAINT ck_plant_relationships_ambiguity_state
        CHECK (ambiguity_state IN ('unambiguous', 'ambiguous', 'resolved')),
    CONSTRAINT ck_plant_relationships_validation_state
        CHECK (validation_state IN ('unproven', 'validated', 'failed')),

    -- Grain conversion without an attribution rule is how a parent's value gets
    -- silently double counted across its children. Refused in the database as
    -- well as at publish, because one of the two will eventually be bypassed.
    CONSTRAINT ck_plant_relationships_grain_needs_attribution
        CHECK (grain_left = grain_right
               OR (attribution_rule IS NOT NULL AND attribution_rule <> 'none')),

    CONSTRAINT ck_plant_relationships_definition_version_positive
        CHECK (source_definition_version > 0)
);

-- A code identifies ONE live relationship per tenant. Retired rows keep their
-- code so history stays readable, which is why the uniqueness is partial.
CREATE UNIQUE INDEX IF NOT EXISTS ux_plant_relationships_tenant_code_live
    ON ppiq_meta.plant_relationships (tenant_id, relationship_code)
    WHERE retired_at_utc IS NULL;

CREATE INDEX IF NOT EXISTS ix_plant_relationships_left_right
    ON ppiq_meta.plant_relationships (tenant_id, left_entity, right_entity);

CREATE INDEX IF NOT EXISTS ix_plant_relationships_right_left
    ON ppiq_meta.plant_relationships (tenant_id, right_entity, left_entity);

CREATE INDEX IF NOT EXISTS ix_plant_relationships_definition
    ON ppiq_meta.plant_relationships (tenant_id, source_definition_id);

CREATE INDEX IF NOT EXISTS ix_plant_relationships_ambiguous
    ON ppiq_meta.plant_relationships (tenant_id)
    WHERE ambiguity_state = 'ambiguous';

CREATE INDEX IF NOT EXISTS ix_plant_relationships_not_validated
    ON ppiq_meta.plant_relationships (tenant_id)
    WHERE validation_state <> 'validated';

CREATE INDEX IF NOT EXISTS ix_plant_relationships_live
    ON ppiq_meta.plant_relationships (tenant_id)
    WHERE retired_at_utc IS NULL;

-- -----------------------------------------------------------------------------
-- Ordered key pairs. The order is part of the meaning: a composite key declared
-- out of order is a different join, not the same join written differently.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ppiq_meta.plant_relationship_members
(
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    relationship_id uuid         NOT NULL
        REFERENCES ppiq_meta.plant_relationships (id) ON DELETE CASCADE,
    left_column     varchar(200) NOT NULL,
    right_column    varchar(200) NOT NULL,
    member_order    smallint     NOT NULL,
    comparison      varchar(10)  NOT NULL DEFAULT '=',

    CONSTRAINT ux_plant_relationship_members_order
        UNIQUE (relationship_id, member_order),
    CONSTRAINT ck_plant_relationship_members_order_non_negative
        CHECK (member_order >= 0)
);

CREATE INDEX IF NOT EXISTS ix_plant_relationship_members_relationship
    ON ppiq_meta.plant_relationship_members (relationship_id, member_order);

-- -----------------------------------------------------------------------------
-- Materialised transitive paths.
--
-- The canonical home exists here because the design chapter declares it and a
-- table that only half the model can see is worse than one that is empty. It
-- has no producer yet: resolution today walks the declared relationships and
-- reads preference from the relationship row. Writing rows here to make the
-- table look used would be manufacturing evidence, so nothing writes to it.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS ppiq_meta.plant_relationship_paths
(
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid         NOT NULL,
    from_entity     varchar(100) NOT NULL,
    to_entity       varchar(100) NOT NULL,
    hop_count       smallint     NOT NULL,
    path_json       jsonb        NOT NULL,
    crosses_grain   boolean      NOT NULL,
    is_preferred    boolean      NOT NULL DEFAULT false,
    computed_at_utc timestamptz  NOT NULL DEFAULT now(),

    CONSTRAINT ck_plant_relationship_paths_hop_count_positive
        CHECK (hop_count > 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_plant_relationship_paths_identity
    ON ppiq_meta.plant_relationship_paths (tenant_id, from_entity, to_entity, path_json);

CREATE INDEX IF NOT EXISTS ix_plant_relationship_paths_pair
    ON ppiq_meta.plant_relationship_paths (tenant_id, from_entity, to_entity, is_preferred);

CREATE INDEX IF NOT EXISTS ix_plant_relationship_paths_preferred
    ON ppiq_meta.plant_relationship_paths (tenant_id)
    WHERE is_preferred;

-- -----------------------------------------------------------------------------
-- Convergence off the 827 compatibility surface.
--
-- Rows move only when their definition identity resolves, because the canonical
-- table carries a real foreign key and a legacy row naming a definition that
-- never existed is exactly the thing that key exists to catch. If any row
-- cannot move, nothing is dropped and the script refuses: a silent partial
-- migration produces a model that is missing joins nobody will notice until a
-- number comes out wrong.
-- -----------------------------------------------------------------------------
DO $convergence$
DECLARE
    legacy_total   bigint := 0;
    orphan_total   bigint := 0;
    moved_total    bigint := 0;
BEGIN
    IF to_regclass('public.ppiq_plant_relationships') IS NULL THEN
        RAISE NOTICE 'PPIQ_T095_CONVERGENCE: no compatibility surface present; canonical model stands alone.';
        RETURN;
    END IF;

    EXECUTE 'SELECT count(*) FROM public.ppiq_plant_relationships' INTO legacy_total;

    EXECUTE $orphan$
        SELECT count(*)
        FROM public.ppiq_plant_relationships l
        WHERE NOT EXISTS (
            SELECT 1 FROM ppiq_meta.definition_store d WHERE d.id = l.source_definition_id)
    $orphan$ INTO orphan_total;

    IF orphan_total > 0 THEN
        RAISE EXCEPTION
            'PPIQ_T095_CONVERGENCE_BLOCKED: % of % compatibility relationship row(s) name a definition that does not exist in ppiq_meta.definition_store. The compatibility tables are left in place; nothing was dropped.',
            orphan_total, legacy_total;
    END IF;

    EXECUTE $move$
        INSERT INTO ppiq_meta.plant_relationships
            (id, tenant_id, relationship_code, left_entity, right_entity, join_type, cardinality,
             grain_left, grain_right, attribution_rule, attribution_expression, is_preferred_path,
             ambiguity_state, validation_state, validation_detail, source_definition_id,
             source_definition_version, effective_from_utc, retired_at_utc)
        SELECT l.id, l.tenant_id, l.relationship_code, l.left_entity, l.right_entity, l.join_type,
               l.cardinality, l.grain_left, l.grain_right, l.attribution_rule, l.attribution_expression,
               l.is_preferred_path, l.ambiguity_state, l.validation_state, l.validation_detail,
               l.source_definition_id, l.source_definition_version, l.effective_from_utc, l.retired_at_utc
        FROM public.ppiq_plant_relationships l
        ON CONFLICT (id) DO NOTHING
    $move$;

    GET DIAGNOSTICS moved_total = ROW_COUNT;

    EXECUTE $members$
        INSERT INTO ppiq_meta.plant_relationship_members
            (id, relationship_id, left_column, right_column, member_order, comparison)
        SELECT m.id, m.relationship_id, m.left_column, m.right_column, m.member_order, m.comparison
        FROM public.ppiq_plant_relationship_members m
        WHERE EXISTS (
            SELECT 1 FROM ppiq_meta.plant_relationships r WHERE r.id = m.relationship_id)
        ON CONFLICT (id) DO NOTHING
    $members$;

    IF to_regclass('public.ppiq_plant_relationship_paths') IS NOT NULL THEN
        EXECUTE $paths$
            INSERT INTO ppiq_meta.plant_relationship_paths
                (id, tenant_id, from_entity, to_entity, hop_count, path_json, crosses_grain,
                 is_preferred, computed_at_utc)
            SELECT p.id, p.tenant_id, p.from_entity, p.to_entity, p.hop_count, p.path_json,
                   p.crosses_grain, p.is_preferred, p.computed_at_utc
            FROM public.ppiq_plant_relationship_paths p
            ON CONFLICT (id) DO NOTHING
        $paths$;
    END IF;

    DROP TABLE IF EXISTS public.ppiq_plant_relationship_members;
    DROP TABLE IF EXISTS public.ppiq_plant_relationship_paths;
    DROP TABLE IF EXISTS public.ppiq_plant_relationships;

    RAISE NOTICE
        'PPIQ_T095_CONVERGENCE: % compatibility relationship row(s) carried to the canonical model; compatibility surface retired.',
        moved_total;
END
$convergence$;
