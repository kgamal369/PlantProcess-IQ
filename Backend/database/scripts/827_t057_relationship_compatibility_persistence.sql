-- =============================================================================
-- 827_t057_relationship_compatibility_persistence.sql
-- T-057. M1 compatibility persistence for the plant relationship model.
--
-- THESE TABLES ARE NOT THE FINAL MODEL.
--
-- The canonical home is ppiq_meta.plant_relationships,
-- ppiq_meta.plant_relationship_members and ppiq_meta.plant_relationship_paths,
-- and T-095 owns the convergence. Two things that belong to the canonical model
-- are deliberately ABSENT here rather than faked:
--
--   * the ppiq_meta schema itself, which T-087 creates;
--   * the foreign key source_definition_id -> definition_store(id), because
--     definition_store is T-089's. The column carries the same semantic identity
--     so a published relationship still names the definition VERSION that
--     emitted it, but there is no invented FK to a table that does not exist.
--
-- Nothing outside the store adapter names these tables. That is what makes the
-- T-095 replacement a change of one file instead of a change of a contract.
--
-- Replayable: safe to run more than once.
-- =============================================================================

CREATE TABLE IF NOT EXISTS public.ppiq_plant_relationships
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
    source_definition_id      uuid         NOT NULL,
    source_definition_version integer      NOT NULL,
    effective_from_utc        timestamptz  NOT NULL,
    retired_at_utc            timestamptz  NULL,

    CONSTRAINT ck_ppiq_plant_relationships_join_type
        CHECK (join_type IN ('inner', 'left', 'right', 'full')),
    CONSTRAINT ck_ppiq_plant_relationships_cardinality
        CHECK (cardinality IN ('1-1', '1-n', 'n-1', 'n-m')),
    CONSTRAINT ck_ppiq_plant_relationships_attribution_rule
        CHECK (attribution_rule IS NULL
               OR attribution_rule IN ('weighted', 'equal_split', 'first_parent', 'none')),
    CONSTRAINT ck_ppiq_plant_relationships_ambiguity_state
        CHECK (ambiguity_state IN ('unambiguous', 'ambiguous', 'resolved')),
    CONSTRAINT ck_ppiq_plant_relationships_validation_state
        CHECK (validation_state IN ('unproven', 'validated', 'failed')),

    -- Grain conversion without an attribution rule is how a parent's value gets
    -- silently double counted across its children. Refused in the database as
    -- well as at publish, because one of the two will eventually be bypassed.
    CONSTRAINT ck_ppiq_plant_relationships_grain_needs_attribution
        CHECK (grain_left = grain_right
               OR (attribution_rule IS NOT NULL AND attribution_rule <> 'none')),

    CONSTRAINT ck_ppiq_plant_relationships_definition_version_positive
        CHECK (source_definition_version > 0)
);

-- A code identifies ONE live relationship per tenant. Retired rows keep their
-- code so history stays readable, which is why the uniqueness is partial.
CREATE UNIQUE INDEX IF NOT EXISTS ux_ppiq_plant_relationships_tenant_code_live
    ON public.ppiq_plant_relationships (tenant_id, relationship_code)
    WHERE retired_at_utc IS NULL;

CREATE INDEX IF NOT EXISTS ix_ppiq_plant_relationships_left_right
    ON public.ppiq_plant_relationships (tenant_id, left_entity, right_entity);

CREATE INDEX IF NOT EXISTS ix_ppiq_plant_relationships_right_left
    ON public.ppiq_plant_relationships (tenant_id, right_entity, left_entity);

CREATE INDEX IF NOT EXISTS ix_ppiq_plant_relationships_definition
    ON public.ppiq_plant_relationships (tenant_id, source_definition_id);

CREATE INDEX IF NOT EXISTS ix_ppiq_plant_relationships_ambiguous
    ON public.ppiq_plant_relationships (tenant_id)
    WHERE ambiguity_state = 'ambiguous';

CREATE INDEX IF NOT EXISTS ix_ppiq_plant_relationships_not_validated
    ON public.ppiq_plant_relationships (tenant_id)
    WHERE validation_state <> 'validated';

CREATE INDEX IF NOT EXISTS ix_ppiq_plant_relationships_live
    ON public.ppiq_plant_relationships (tenant_id)
    WHERE retired_at_utc IS NULL;

-- -----------------------------------------------------------------------------
-- Ordered key pairs. The order is part of the meaning: a composite key declared
-- out of order is a different join, not the same join written differently.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.ppiq_plant_relationship_members
(
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    relationship_id uuid         NOT NULL
        REFERENCES public.ppiq_plant_relationships (id) ON DELETE CASCADE,
    left_column     varchar(200) NOT NULL,
    right_column    varchar(200) NOT NULL,
    member_order    smallint     NOT NULL,
    comparison      varchar(10)  NOT NULL DEFAULT '=',

    CONSTRAINT ux_ppiq_plant_relationship_members_order
        UNIQUE (relationship_id, member_order),
    CONSTRAINT ck_ppiq_plant_relationship_members_order_non_negative
        CHECK (member_order >= 0)
);

CREATE INDEX IF NOT EXISTS ix_ppiq_plant_relationship_members_relationship
    ON public.ppiq_plant_relationship_members (relationship_id, member_order);

-- -----------------------------------------------------------------------------
-- Materialised transitive paths. Created here so the compatibility surface is
-- complete; T-058 owns computing and reading them. T-057 writes nothing to it.
-- -----------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.ppiq_plant_relationship_paths
(
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id      uuid         NOT NULL,
    from_entity    varchar(100) NOT NULL,
    to_entity      varchar(100) NOT NULL,
    hop_count      smallint     NOT NULL,
    path_json      jsonb        NOT NULL,
    crosses_grain  boolean      NOT NULL,
    is_preferred   boolean      NOT NULL DEFAULT false,
    computed_at_utc timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT ck_ppiq_plant_relationship_paths_hop_count_positive
        CHECK (hop_count > 0)
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_ppiq_plant_relationship_paths_identity
    ON public.ppiq_plant_relationship_paths (tenant_id, from_entity, to_entity, path_json);

CREATE INDEX IF NOT EXISTS ix_ppiq_plant_relationship_paths_pair
    ON public.ppiq_plant_relationship_paths (tenant_id, from_entity, to_entity, is_preferred);

CREATE INDEX IF NOT EXISTS ix_ppiq_plant_relationship_paths_preferred
    ON public.ppiq_plant_relationship_paths (tenant_id)
    WHERE is_preferred;