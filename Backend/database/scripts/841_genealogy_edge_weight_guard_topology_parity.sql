-- ============================================================================
-- 841_genealogy_edge_weight_guard_topology_parity.sql
-- PPIQ T-099 (owned by CENTRAL ruling of 13-Sep-2026, option b).
--
-- WHAT WAS WRONG. 550 created the deferred constraint trigger
-- ppiq_genealogy_edge_weight_guard_after_change on public.genealogy_edges with
-- a plpgsql body that reads FROM public.genealogy_edges by name. When the T-090
-- storage topology convergence relocated the table to ppiq_plant, the trigger
-- followed the table, as triggers do, but the function body kept the old
-- schema-qualified name. On any database built from the canonical path every
-- INSERT, UPDATE or DELETE on genealogy_edges therefore fails at COMMIT with
-- 42P01 relation "public.genealogy_edges" does not exist. The first canonical
-- from-zero insert of a GenealogyEdge in the repository's history was T-099's
-- PV04 live gate, which is how this surfaced.
--
-- THE FIX. The function now reads from the relation the trigger actually fired
-- on, TG_TABLE_SCHEMA.TG_TABLE_NAME, so it follows the table under any future
-- placement instead of hard-coding one. Semantics are otherwise unchanged: the
-- same aggregate, the same tolerance, the same exception text, RETURN NULL.
-- Nothing else from 550 is touched. The trigger is not recreated.
--
-- The function is replaced through the trigger's own function OID, so this is
-- correct whether the function lives in public (fresh canonical build, ppiq_app
-- today) or has been relocated by a later convergence.
-- ============================================================================
\set ON_ERROR_STOP on

DO $$
DECLARE
    v_schema text;
    v_name   text;
BEGIN
    SELECT n.nspname, p.proname
      INTO v_schema, v_name
      FROM pg_trigger t
      JOIN pg_proc p      ON p.oid = t.tgfoid
      JOIN pg_namespace n ON n.oid = p.pronamespace
     WHERE t.tgname = 'ppiq_genealogy_edge_weight_guard_after_change'
       AND NOT t.tgisinternal
     LIMIT 1;

    IF v_name IS NULL THEN
        RAISE EXCEPTION 'PPIQ_841: trigger ppiq_genealogy_edge_weight_guard_after_change is not installed; 550 must run before 841.';
    END IF;

    EXECUTE format($def$
        CREATE OR REPLACE FUNCTION %I.%I()
        RETURNS trigger
        LANGUAGE plpgsql
        AS $body$
        DECLARE
            bad_child uuid;
            bad_sum   numeric;
        BEGIN
            -- Read the relation this trigger fired on, wherever the governed
            -- topology has placed it. Never a hard-coded schema.
            EXECUTE format(
                'SELECT child_material_unit_id, sum(contribution_weight) '
                '  FROM %%I.%%I '
                ' WHERE COALESCE(is_deleted, false) = false '
                ' GROUP BY child_material_unit_id '
                'HAVING abs(sum(contribution_weight) - 1.0) > 0.015 '
                ' LIMIT 1',
                TG_TABLE_SCHEMA, TG_TABLE_NAME)
            INTO bad_child, bad_sum;

            IF bad_child IS NOT NULL THEN
                RAISE EXCEPTION 'Genealogy contribution weights must sum to 1.0 per child. child=%%, sum=%%', bad_child, bad_sum;
            END IF;

            RETURN NULL;
        END
        $body$;
    $def$, v_schema, v_name);

    RAISE NOTICE 'PPIQ_841: replaced %.%() to resolve the relation through TG_TABLE_SCHEMA/TG_TABLE_NAME.', v_schema, v_name;
END $$;

-- Proof on this very database: the trigger's function no longer names the
-- pre-relocation schema, and the relation the trigger guards is the governed one.
DO $$
DECLARE
    v_def   text;
    v_rel   text;
BEGIN
    SELECT pg_get_functiondef(t.tgfoid), t.tgrelid::regclass::text
      INTO v_def, v_rel
      FROM pg_trigger t
     WHERE t.tgname = 'ppiq_genealogy_edge_weight_guard_after_change'
       AND NOT t.tgisinternal
     LIMIT 1;

    IF v_def IS NULL THEN
        RAISE EXCEPTION 'PPIQ_841: trigger function could not be read back.';
    END IF;

    IF position('public.genealogy_edges' IN v_def) > 0 THEN
        RAISE EXCEPTION 'PPIQ_841: function body still references public.genealogy_edges.';
    END IF;

    IF position('TG_TABLE_SCHEMA' IN v_def) = 0 THEN
        RAISE EXCEPTION 'PPIQ_841: function body does not resolve the relation through the trigger.';
    END IF;

    RAISE NOTICE 'PPIQ_841_PROVEN: weight guard bound to % and resolved through the trigger relation.', v_rel;
END $$;
