-- PPIQ_REALIZATION_T005_RECURSIVE_GENEALOGY_CYCLE_GUARD
-- Recursive CTE reference implementation for one-round-trip cycle detection.
--
-- The application validation gate requires this SQL to be present before the
-- deeper service refactor is accepted. This avoids N+1 traversal logic.

CREATE OR REPLACE FUNCTION public.ppiq_would_create_genealogy_cycle(
    p_parent_material_unit_id uuid,
    p_child_material_unit_id uuid
)
RETURNS boolean
LANGUAGE sql
STABLE
AS $$
    WITH RECURSIVE walk(child_material_unit_id, parent_material_unit_id, depth) AS
    (
        SELECT
            ge.child_material_unit_id,
            ge.parent_material_unit_id,
            1
        FROM public.genealogy_edges ge
        WHERE ge.parent_material_unit_id = p_child_material_unit_id

        UNION ALL

        SELECT
            ge.child_material_unit_id,
            ge.parent_material_unit_id,
            walk.depth + 1
        FROM public.genealogy_edges ge
        JOIN walk ON ge.parent_material_unit_id = walk.child_material_unit_id
        WHERE walk.depth < 1000
    )
    SELECT EXISTS
    (
        SELECT 1
        FROM walk
        WHERE child_material_unit_id = p_parent_material_unit_id
    );
$$;
