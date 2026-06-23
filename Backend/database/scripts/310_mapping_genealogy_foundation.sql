-- 310_mapping_genealogy_foundation.sql
-- Demo-minimum mapping / genealogy / safe-SQL foundation (rebuilt; descriptive name).
-- The previous 310 was a PowerShell generator mis-saved as .sql and is quarantined.
--
-- Design:
--  * canonical_* mirror relations are VIEWS over the EF source tables. They are always in
--    sync with seeded data, carry zero projection drift, are read-only, and create cleanly
--    because EF runs (dotnet ef database update) BEFORE the numbered SQL scripts.
--    BaseEntity has no tenant column, so the single demo tenant is injected as a constant.
--  * business-key + mapping-version metadata are thin committed tables (DB-only constructs).
--  * ppiq_rollback_mapping_version (consumed by the lifecycle proof in 312/313) was lost with
--    the old 310 and is redefined here so the proof + validation can roll a version back.
-- Scripts 311/312/313/321 (already present) define ppiq_walk_genealogy, ppiq_resolve_safe_sql,
-- ppiq_material_investigation, the validators, the golden-thread, and the v_ppiq_p04_* views;
-- they run AFTER this script and depend only on the relations/functions created here.

-- ---------------------------------------------------------------------------
-- Canonical material units  (consumed by ppiq_walk_genealogy + ppiq_material_investigation)
-- ---------------------------------------------------------------------------
CREATE OR REPLACE VIEW public.canonical_material_units AS
SELECT
    mu.id,
    '00000000-0000-0000-0000-000000000001'::uuid AS tenant_id,
    mu.material_code               AS material_key,
    mu.material_unit_type          AS material_type,
    mu.production_start_utc,
    mu.created_at_utc,
    NULL::text                     AS heat_key,
    jsonb_build_object(
        'productFamily', mu.product_family,
        'gradeOrRecipe', mu.grade_or_recipe,
        'siteId',        mu.site_id
    )                              AS attributes
FROM public.material_units mu
WHERE mu.is_deleted = false;

-- ---------------------------------------------------------------------------
-- Canonical genealogy edges  (consumed by the recursive walk + investigation)
-- ---------------------------------------------------------------------------
CREATE OR REPLACE VIEW public.canonical_genealogy_edges AS
SELECT
    ge.id,
    '00000000-0000-0000-0000-000000000001'::uuid AS tenant_id,
    ge.parent_material_unit_id,
    ge.child_material_unit_id,
    ge.relationship_type           AS edge_type,
    ge.provenance_confidence       AS confidence
FROM public.genealogy_edges ge
WHERE ge.is_deleted = false;

-- ---------------------------------------------------------------------------
-- Canonical equipment  (consumed by v_ppiq_p04_downtime_value_impact)
-- ---------------------------------------------------------------------------
CREATE OR REPLACE VIEW public.canonical_equipment AS
SELECT
    eq.id,
    '00000000-0000-0000-0000-000000000001'::uuid AS tenant_id,
    eq.equipment_code
FROM public.equipment eq
WHERE eq.is_deleted = false;

-- ---------------------------------------------------------------------------
-- Canonical downtime events  (consumed by v_ppiq_p04_downtime_value_impact)
-- Minutes are computed from the start/end window; buffer/cascade default for the demo.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE VIEW public.canonical_downtime_events AS
SELECT
    de.id,
    '00000000-0000-0000-0000-000000000001'::uuid AS tenant_id,
    de.equipment_id,
    de.downtime_type               AS stoppage_class,
    de.reason_code                 AS reason,
    COALESCE(EXTRACT(EPOCH FROM (de.ended_at_utc - de.started_at_utc)) / 60.0, 0)::numeric AS equipment_stoppage_minutes,
    0::numeric                     AS buffer_absorbed_minutes,
    1.0::numeric                   AS cascade_amplification_factor,
    COALESCE(EXTRACT(EPOCH FROM (de.ended_at_utc - de.started_at_utc)) / 60.0, 0)::numeric AS production_stoppage_minutes
FROM public.downtime_events de
WHERE de.is_deleted = false;

-- ---------------------------------------------------------------------------
-- Canonical quality events  (investigation QualityEvent section)
-- severity/description are synthesized (not present on the source) so the view always creates.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE VIEW public.canonical_quality_events AS
SELECT
    qe.id,
    '00000000-0000-0000-0000-000000000001'::uuid AS tenant_id,
    qe.material_unit_id,
    qe.event_type                  AS quality_event_type,
    qe.event_at_utc                AS event_time_utc,
    NULL::text                     AS severity,
    NULL::text                     AS description,
    qe.defect_catalog_id::text     AS defect_code,
    '{}'::jsonb                    AS attributes
FROM public.quality_events qe
WHERE qe.is_deleted = false;

-- ---------------------------------------------------------------------------
-- Canonical process step executions + parameter observations
-- Thin tables for the demo-minimum (timeline projection from source deferred to V2).
-- Created with the exact columns ppiq_material_investigation reads so the function compiles.
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.canonical_process_step_executions (
    id                  uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           uuid        NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    material_unit_id    uuid        NOT NULL,
    process_code        text,
    start_utc           timestamptz,
    end_utc             timestamptz,
    equipment_id        uuid,
    attributes          jsonb       NOT NULL DEFAULT '{}'::jsonb
);

CREATE TABLE IF NOT EXISTS public.canonical_parameter_observations (
    id                  uuid        PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id           uuid        NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    material_unit_id    uuid        NOT NULL,
    parameter_code      text,
    observed_at_utc     timestamptz,
    numeric_value       numeric,
    text_value          text,
    unit                text,
    attributes          jsonb       NOT NULL DEFAULT '{}'::jsonb
);

-- ---------------------------------------------------------------------------
-- Business-key dictionary  (consumed by ppiq_validate_business_key_dictionary)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.ppiq_business_key_definitions (
    id              uuid    PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid    NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    key_code        text    NOT NULL,
    entity_scope    text,
    version_number  integer NOT NULL DEFAULT 1,
    created_at_utc  timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS public.ppiq_business_key_members (
    id              uuid    PRIMARY KEY DEFAULT gen_random_uuid(),
    definition_id   uuid    NOT NULL REFERENCES public.ppiq_business_key_definitions(id) ON DELETE CASCADE,
    member_role     text,
    source_field    text,
    sort_order      integer NOT NULL DEFAULT 0,
    created_at_utc  timestamptz NOT NULL DEFAULT now()
);

-- ---------------------------------------------------------------------------
-- Canonical mapping versions  (consumed by ppiq_validate_canonical_mapping_version,
-- ppiq_run_mapping_lifecycle_proof, ppiq_rollback_mapping_version)
-- ---------------------------------------------------------------------------
CREATE TABLE IF NOT EXISTS public.ppiq_mapping_versions (
    id               uuid    PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id        uuid    NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    mapping_code     text,
    display_name     text,
    canonical_entity text,
    environment      text,
    version_number   integer,
    definition       jsonb   NOT NULL DEFAULT '{}'::jsonb,
    status           text    NOT NULL DEFAULT 'Draft',
    created_at_utc   timestamptz NOT NULL DEFAULT now()
);

-- Optional registry table named in the backlog; harmless and kept for completeness.
CREATE TABLE IF NOT EXISTS public.canonical_business_keys (
    id               uuid    PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id        uuid    NOT NULL DEFAULT '00000000-0000-0000-0000-000000000001',
    key_code         text    NOT NULL,
    canonical_entity text,
    created_at_utc   timestamptz NOT NULL DEFAULT now()
);

-- ---------------------------------------------------------------------------
-- ppiq_rollback_mapping_version  (lost with the old 310; redefined here)
-- Returns a typed jsonb verdict consumed by the lifecycle proof's Rollback step.
-- ---------------------------------------------------------------------------
CREATE OR REPLACE FUNCTION public.ppiq_rollback_mapping_version(p_version_id uuid)
RETURNS jsonb
LANGUAGE plpgsql
AS $fn$
DECLARE
    v_status text;
BEGIN
    SELECT status INTO v_status
    FROM public.ppiq_mapping_versions
    WHERE id = p_version_id;

    IF NOT FOUND THEN
        RETURN jsonb_build_object(
            'isValid', false,
            'code',    'NoSuchMappingVersion',
            'message', 'Mapping version does not exist.'
        );
    END IF;

    IF v_status = 'Published' THEN
        RETURN jsonb_build_object(
            'isValid', false,
            'code',    'CannotRollbackPublished',
            'message', 'A published mapping version cannot be rolled back; supersede it instead.'
        );
    END IF;

    UPDATE public.ppiq_mapping_versions
    SET status = 'RolledBack'
    WHERE id = p_version_id;

    RETURN jsonb_build_object(
        'isValid', true,
        'code',    'RolledBack',
        'message', 'Mapping version rolled back.'
    );
END;
$fn$;