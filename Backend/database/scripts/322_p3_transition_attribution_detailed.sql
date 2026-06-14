-- =============================================================================
-- 322_p3_transition_attribution_detailed.sql        (PPIQ-303, idempotent)
-- Weighted shared attribution that surfaces parent heat CODES + casting-position
-- basis, and a sum-to-1.0 validator. (A3.8 / G15)
-- =============================================================================

CREATE OR REPLACE FUNCTION public.ppiq_v5_blended_attribution_detailed(p_child uuid)
RETURNS TABLE (
    parent_material_unit_id uuid,
    parent_material_code    text,
    child_material_unit_id  uuid,
    contribution_weight     numeric,
    contribution_percent    numeric,
    is_transition           boolean,
    provenance_confidence   numeric,
    position_basis          text,
    evidence                text
)
LANGUAGE sql
STABLE
AS $fn$
    SELECT
        e.parent_material_unit_id,
        p.material_code,
        e.child_material_unit_id,
        e.contribution_weight,
        round(e.contribution_weight * 100.0, 2)                                     AS contribution_percent,
        e.is_transition,
        COALESCE(e.provenance_confidence, 1.0)                                      AS provenance_confidence,
        CASE
            WHEN e.is_transition THEN
                format('Casting-position weighted: heat %s contributes %s%% of the coil length across the cast transition.',
                       p.material_code, round(e.contribution_weight * 100.0, 1))
            ELSE
                format('Single-parent provenance: heat %s = 100%% of this coil.', p.material_code)
        END                                                                         AS position_basis,
        CASE
            WHEN e.is_transition THEN 'Transition / blended provenance edge; weight is the casting-position share.'
            ELSE 'Normal single-parent provenance edge.'
        END                                                                         AS evidence
    FROM public.genealogy_edges e
    JOIN public.material_units  p ON p.id = e.parent_material_unit_id
    WHERE e.child_material_unit_id = p_child
      AND COALESCE(e.is_deleted, false) = false
    ORDER BY e.contribution_weight DESC, p.material_code;
$fn$;

-- Weights for a child must sum to 1.0 +/- 0.01 (else attribution is incoherent).
CREATE OR REPLACE FUNCTION public.ppiq_v5_attribution_weight_ok(p_child uuid)
RETURNS boolean
LANGUAGE sql
STABLE
AS $fn$
    SELECT abs(COALESCE(sum(e.contribution_weight), 0) - 1.0) <= 0.01
    FROM public.genealogy_edges e
    WHERE e.child_material_unit_id = p_child
      AND COALESCE(e.is_deleted, false) = false;
$fn$;