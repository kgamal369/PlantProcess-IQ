-- ============================================================
-- PlantProcess IQ Doctrine v5
-- P06 · Blended Provenance & Canonical Depth
--
-- Adds:
--   - contribution_weight
--   - is_transition
--   - provenance_confidence
--   - deferred child-weight sum guard
--   - blended attribution views/functions
--
-- Idempotent.
-- ============================================================

\set ON_ERROR_STOP on

BEGIN;

ALTER TABLE IF EXISTS public.genealogy_edges
    ADD COLUMN IF NOT EXISTS contribution_weight numeric(9, 6) NOT NULL DEFAULT 1.000000;

ALTER TABLE IF EXISTS public.genealogy_edges
    ADD COLUMN IF NOT EXISTS is_transition boolean NOT NULL DEFAULT false;

ALTER TABLE IF EXISTS public.genealogy_edges
    ADD COLUMN IF NOT EXISTS provenance_confidence numeric(9, 6) NOT NULL DEFAULT 1.000000;

ALTER TABLE IF EXISTS public.genealogy_edges
    DROP CONSTRAINT IF EXISTS ck_genealogy_edges_contribution_weight_range;

ALTER TABLE IF EXISTS public.genealogy_edges
    ADD CONSTRAINT ck_genealogy_edges_contribution_weight_range
    CHECK (contribution_weight > 0 AND contribution_weight <= 1);

ALTER TABLE IF EXISTS public.genealogy_edges
    DROP CONSTRAINT IF EXISTS ck_genealogy_edges_provenance_confidence_range;

ALTER TABLE IF EXISTS public.genealogy_edges
    ADD CONSTRAINT ck_genealogy_edges_provenance_confidence_range
    CHECK (provenance_confidence >= 0 AND provenance_confidence <= 1);

-- Backfill: single parent = 1.0; multi-parent children split evenly
-- unless already explicitly weighted.
WITH active_edges AS
(
    SELECT
        id,
        child_material_unit_id,
        count(*) OVER (PARTITION BY child_material_unit_id) AS parent_count
    FROM public.genealogy_edges
    WHERE COALESCE(is_deleted, false) = false
),
backfill AS
(
    SELECT
        id,
        parent_count,
        round((1.0 / parent_count)::numeric, 6) AS new_weight
    FROM active_edges
)
UPDATE public.genealogy_edges ge
SET contribution_weight = bf.new_weight,
    is_transition = bf.parent_count > 1,
    provenance_confidence = CASE WHEN bf.parent_count > 1 THEN LEAST(ge.provenance_confidence, 0.950000) ELSE ge.provenance_confidence END
FROM backfill bf
WHERE ge.id = bf.id
  AND (
      ge.contribution_weight IS NULL
      OR ge.contribution_weight = 1.000000
      OR bf.parent_count > 1
  );

CREATE INDEX IF NOT EXISTS ix_genealogy_edges_child_weight_transition_v5
ON public.genealogy_edges(child_material_unit_id, is_transition, contribution_weight)
WHERE COALESCE(is_deleted, false) = false;

CREATE INDEX IF NOT EXISTS ix_genealogy_edges_parent_weight_v5
ON public.genealogy_edges(parent_material_unit_id, contribution_weight)
WHERE COALESCE(is_deleted, false) = false;

-- ------------------------------------------------------------
-- Deferred weight-sum guard.
-- ------------------------------------------------------------

CREATE OR REPLACE FUNCTION public.ppiq_genealogy_edge_weight_guard()
RETURNS trigger
LANGUAGE plpgsql
AS $$
DECLARE
    bad_child uuid;
    bad_sum numeric;
BEGIN
    SELECT child_material_unit_id, sum(contribution_weight)
    INTO bad_child, bad_sum
    FROM public.genealogy_edges
    WHERE COALESCE(is_deleted, false) = false
    GROUP BY child_material_unit_id
    HAVING abs(sum(contribution_weight) - 1.0) > 0.015
    LIMIT 1;

    IF bad_child IS NOT NULL THEN
        RAISE EXCEPTION 'Genealogy contribution weights must sum to 1.0 per child. child=%, sum=%', bad_child, bad_sum;
    END IF;

    RETURN NULL;
END $$;

DROP TRIGGER IF EXISTS ppiq_genealogy_edge_weight_guard_after_change ON public.genealogy_edges;

CREATE CONSTRAINT TRIGGER ppiq_genealogy_edge_weight_guard_after_change
AFTER INSERT OR UPDATE OR DELETE ON public.genealogy_edges
DEFERRABLE INITIALLY DEFERRED
FOR EACH ROW
EXECUTE FUNCTION public.ppiq_genealogy_edge_weight_guard();

-- ------------------------------------------------------------
-- Blended edge / attribution views.
-- ------------------------------------------------------------

CREATE OR REPLACE VIEW public.ppiq_v5_blended_genealogy_edges AS
SELECT
    ge.id,
    ge.parent_material_unit_id,
    parent.material_code AS parent_material_code,
    ge.child_material_unit_id,
    child.material_code AS child_material_code,
    ge.relationship_type,
    ge.contribution_weight,
    ge.is_transition,
    ge.provenance_confidence,
    ge.effective_from_utc,
    ge.effective_to_utc,
    ge.source_system,
    ge.source_record_id
FROM public.genealogy_edges ge
LEFT JOIN public.material_units parent ON parent.id = ge.parent_material_unit_id
LEFT JOIN public.material_units child ON child.id = ge.child_material_unit_id
WHERE COALESCE(ge.is_deleted, false) = false;

CREATE OR REPLACE VIEW public.ppiq_v5_child_weight_status AS
SELECT
    child_material_unit_id,
    count(*) AS parent_count,
    sum(contribution_weight) AS contribution_sum,
    bool_or(is_transition) AS has_transition,
    min(provenance_confidence) AS min_confidence,
    abs(sum(contribution_weight) - 1.0) <= 0.015 AS is_green
FROM public.genealogy_edges
WHERE COALESCE(is_deleted, false) = false
GROUP BY child_material_unit_id;

CREATE OR REPLACE FUNCTION public.ppiq_v5_blended_attribution_for_child(p_child_material_unit_id uuid)
RETURNS TABLE
(
    parent_material_unit_id uuid,
    child_material_unit_id uuid,
    contribution_weight numeric,
    is_transition boolean,
    provenance_confidence numeric,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT
        parent_material_unit_id,
        child_material_unit_id,
        contribution_weight,
        is_transition,
        provenance_confidence,
        CASE
            WHEN is_transition THEN 'Transition / blended provenance edge. Parent contribution is weighted.'
            ELSE 'Normal single-parent provenance edge.'
        END AS evidence
    FROM public.genealogy_edges
    WHERE child_material_unit_id = p_child_material_unit_id
      AND COALESCE(is_deleted, false) = false
    ORDER BY contribution_weight DESC, parent_material_unit_id;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_v5_p06_acceptance()
RETURNS TABLE
(
    check_name text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT 'Genealogy weight columns exist',
           EXISTS (
             SELECT 1 FROM information_schema.columns
             WHERE table_schema='public' AND table_name='genealogy_edges' AND column_name='contribution_weight'
           )
           AND EXISTS (
             SELECT 1 FROM information_schema.columns
             WHERE table_schema='public' AND table_name='genealogy_edges' AND column_name='is_transition'
           )
           AND EXISTS (
             SELECT 1 FROM information_schema.columns
             WHERE table_schema='public' AND table_name='genealogy_edges' AND column_name='provenance_confidence'
           ),
           'contribution_weight, is_transition and provenance_confidence are present.'
    UNION ALL
    SELECT 'Weight range constraints exist',
           EXISTS (
             SELECT 1 FROM pg_constraint WHERE conname='ck_genealogy_edges_contribution_weight_range'
           )
           AND EXISTS (
             SELECT 1 FROM pg_constraint WHERE conname='ck_genealogy_edges_provenance_confidence_range'
           ),
           'range constraints installed.'
    UNION ALL
    SELECT 'Child weights currently sum to one',
           NOT EXISTS (
             SELECT 1 FROM public.ppiq_v5_child_weight_status WHERE is_green = false
           ),
           COALESCE(
             (SELECT string_agg(child_material_unit_id::text || '=' || contribution_sum::text, ', ')
              FROM public.ppiq_v5_child_weight_status WHERE is_green = false),
             'All active child provenance weights are within tolerance.'
           )
    UNION ALL
    SELECT 'Blended attribution function exists',
           to_regprocedure('public.ppiq_v5_blended_attribution_for_child(uuid)') IS NOT NULL,
           'ppiq_v5_blended_attribution_for_child installed.'
    UNION ALL
    SELECT 'Blended genealogy view exists',
           to_regclass('public.ppiq_v5_blended_genealogy_edges') IS NOT NULL,
           'ppiq_v5_blended_genealogy_edges installed.';
$$;

COMMIT;

SELECT * FROM public.ppiq_v5_p06_acceptance();