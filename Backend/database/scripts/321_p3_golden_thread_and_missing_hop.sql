-- =============================================================================
-- 321_p3_golden_thread_and_missing_hop.sql          (PPIQ-302, idempotent)
-- Both-direction genealogy thread on the CUSTOMER's keys (reuses the live
-- ppiq_walk_genealogy over canonical_material_units / canonical_genealogy_edges)
-- plus a typed 'MissingHop' when the upstream chain to a heat is broken.
-- =============================================================================

CREATE OR REPLACE FUNCTION public.ppiq_golden_thread(
    p_tenant_id   uuid,
    p_coil_key    text,
    p_max_depth   integer DEFAULT 12
)
RETURNS jsonb
LANGUAGE plpgsql
STABLE
AS $fn$
DECLARE
    v_walk      jsonb;
    v_back      jsonb;
    v_fwd       jsonb;
    v_heat      jsonb;
    v_has_heat  boolean;
BEGIN
    IF to_regclass('public.canonical_material_units') IS NULL
       OR to_regclass('public.canonical_genealogy_edges') IS NULL THEN
        RETURN jsonb_build_object('errorCode','GenealogyUnavailable',
            'message','canonical genealogy layer is not present in this database.');
    END IF;

    v_walk := public.ppiq_walk_genealogy(p_tenant_id, p_coil_key, 'both', p_max_depth);

    -- An invalid-direction / error array is passed straight through.
    IF jsonb_typeof(v_walk) = 'array'
       AND jsonb_array_length(v_walk) >= 1
       AND (v_walk->0 ? 'errorCode') THEN
        RETURN v_walk;
    END IF;

    -- Customer-key-only projections of each direction (no internal UUIDs surfaced).
    SELECT COALESCE(jsonb_agg(jsonb_build_object(
                'materialKey', node->>'materialKey',
                'materialType', node->>'materialType',
                'depth', (node->>'depth')::int,
                'direction', node->>'direction')
            ORDER BY (node->>'depth')::int), '[]'::jsonb)
      INTO v_back
      FROM jsonb_array_elements(COALESCE(v_walk->'nodes', v_walk)) AS node
     WHERE node->>'direction' IN ('self','backward');

    SELECT COALESCE(jsonb_agg(jsonb_build_object(
                'materialKey', node->>'materialKey',
                'materialType', node->>'materialType',
                'depth', (node->>'depth')::int,
                'direction', node->>'direction')
            ORDER BY (node->>'depth')::int), '[]'::jsonb)
      INTO v_fwd
      FROM jsonb_array_elements(COALESCE(v_walk->'nodes', v_walk)) AS node
     WHERE node->>'direction' IN ('self','forward');

    -- The terminal upstream node should be a heat (melt). If none, the chain is broken.
    SELECT to_jsonb(node), true
      INTO v_heat, v_has_heat
      FROM jsonb_array_elements(COALESCE(v_walk->'nodes', v_walk)) AS node
     WHERE lower(COALESCE(node->>'materialType','')) LIKE 'heat%'
     ORDER BY (node->>'depth')::int DESC
     LIMIT 1;

    IF NOT COALESCE(v_has_heat, false) THEN
        RETURN jsonb_build_object(
            'coilKey', p_coil_key,
            'errorCode', 'MissingHop',
            'missingHop', 'heat',
            'message', format('Genealogy thread for %s does not reach a heat - the melt-chemistry hop is missing.', p_coil_key),
            'backward', v_back,
            'forward',  v_fwd);
    END IF;

    RETURN jsonb_build_object(
        'coilKey',         p_coil_key,
        'customerKeysOnly', true,
        'backward',        v_back,     -- coil -> slab -> ... -> heat
        'forward',         v_fwd,      -- heat/coil -> downstream coils
        'heat',            jsonb_build_object(
                               'materialKey', v_heat->>'materialKey',
                               'materialType', v_heat->>'materialType'));
END;
$fn$;