-- ============================================================
-- PlantProcess IQ — P03/P04 hotfix
-- Fixes:
--   1. ppiq_walk_genealogy recursive CTE shape for PostgreSQL
--   2. ppiq_validate_safe_sql typed error priority
-- ============================================================

CREATE OR REPLACE FUNCTION public.ppiq_validate_safe_sql(p_sql text)
RETURNS jsonb
LANGUAGE plpgsql
AS $$
DECLARE
    v_trim text := btrim(COALESCE(p_sql, ''));
    v_lower text := lower(btrim(COALESCE(p_sql, '')));
BEGIN
    IF v_trim = '' THEN
        RETURN jsonb_build_object(
            'isValid', false,
            'code', 'EmptySql',
            'message', 'SQL view text is empty.'
        );
    END IF;

    -- Forbidden statements must win over generic "must be SELECT" errors.
    -- This gives the HMI typed resolver feedback such as ForbiddenStatement.
    IF v_lower ~ '(^|[[:space:];])(insert|update|delete|drop|alter|truncate|grant|revoke|copy|execute|exec|merge|vacuum|analyze|call|do|listen|notify|prepare|deallocate|create|replace)($|[[:space:];])' THEN
        RETURN jsonb_build_object(
            'isValid', false,
            'code', 'ForbiddenStatement',
            'message', 'SQL contains a forbidden write/DDL/control statement.'
        );
    END IF;

    IF NOT (v_lower LIKE 'select %' OR v_lower LIKE 'with %') THEN
        RETURN jsonb_build_object(
            'isValid', false,
            'code', 'MustBeSelectOnly',
            'message', 'Only SELECT or WITH read-only queries are allowed.'
        );
    END IF;

    IF v_lower ~ '(pg_sleep|dblink|pg_read_file|pg_write_file|lo_import|lo_export)' THEN
        RETURN jsonb_build_object(
            'isValid', false,
            'code', 'ForbiddenFunction',
            'message', 'SQL contains a forbidden function.'
        );
    END IF;

    IF position(';' in regexp_replace(v_lower, ';\s*$', '')) > 0 THEN
        RETURN jsonb_build_object(
            'isValid', false,
            'code', 'MultipleStatements',
            'message', 'Only one SQL statement is allowed.'
        );
    END IF;

    IF v_lower ~ '\bcross\s+join\b' THEN
        RETURN jsonb_build_object(
            'isValid', false,
            'code', 'AmbiguousJoinKey',
            'message', 'CROSS JOIN is rejected. Declare an explicit business key or join predicate.'
        );
    END IF;

    RETURN jsonb_build_object(
        'isValid', true,
        'code', 'Ok',
        'message', 'SQL accepted as read-only candidate. Publish flow must still run EXPLAIN and timeout-limited preview.',
        'enforcedRowLimit', 1000,
        'statementTimeoutMs', 5000
    );
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_walk_genealogy(
    p_tenant_id uuid,
    p_material_key text,
    p_direction text DEFAULT 'both',
    p_max_depth integer DEFAULT 8
)
RETURNS jsonb
LANGUAGE plpgsql
AS $$
DECLARE
    v_result jsonb;
    v_direction text := lower(COALESCE(NULLIF(p_direction, ''), 'both'));
    v_max_depth integer := LEAST(GREATEST(COALESCE(p_max_depth, 8), 1), 30);
BEGIN
    IF v_direction NOT IN ('both', 'backward', 'forward') THEN
        RETURN jsonb_build_array(
            jsonb_build_object(
                'errorCode', 'InvalidDirection',
                'message', 'Direction must be both, backward, or forward.'
            )
        );
    END IF;

    /*
     * PostgreSQL recursive CTE rule:
     * one recursive self-reference per recursive CTE branch.
     *
     * Therefore we keep backward and forward traversal in separate recursive CTEs
     * instead of using several UNION ALL recursive branches under one CTE.
     */
    WITH RECURSIVE
    start_node AS (
        SELECT
            id,
            material_key,
            material_type
        FROM public.canonical_material_units
        WHERE tenant_id = p_tenant_id
          AND lower(material_key) = lower(p_material_key)
        LIMIT 1
    ),

    backward_walk AS (
        SELECT
            0 AS depth,
            s.id AS material_unit_id,
            s.material_key,
            s.material_type,
            NULL::uuid AS via_edge_id,
            'self'::text AS direction,
            ARRAY[s.id]::uuid[] AS visited
        FROM start_node s
        WHERE v_direction IN ('both', 'backward')

        UNION ALL

        SELECT
            bw.depth + 1,
            parent.id,
            parent.material_key,
            parent.material_type,
            e.id,
            'backward'::text,
            bw.visited || parent.id
        FROM backward_walk bw
        JOIN public.canonical_genealogy_edges e
            ON e.tenant_id = p_tenant_id
           AND e.child_material_unit_id = bw.material_unit_id
        JOIN public.canonical_material_units parent
            ON parent.id = e.parent_material_unit_id
        WHERE bw.depth < v_max_depth
          AND NOT parent.id = ANY(bw.visited)
    ),

    forward_walk AS (
        SELECT
            0 AS depth,
            s.id AS material_unit_id,
            s.material_key,
            s.material_type,
            NULL::uuid AS via_edge_id,
            'self'::text AS direction,
            ARRAY[s.id]::uuid[] AS visited
        FROM start_node s
        WHERE v_direction IN ('both', 'forward')

        UNION ALL

        SELECT
            fw.depth + 1,
            child.id,
            child.material_key,
            child.material_type,
            e.id,
            'forward'::text,
            fw.visited || child.id
        FROM forward_walk fw
        JOIN public.canonical_genealogy_edges e
            ON e.tenant_id = p_tenant_id
           AND e.parent_material_unit_id = fw.material_unit_id
        JOIN public.canonical_material_units child
            ON child.id = e.child_material_unit_id
        WHERE fw.depth < v_max_depth
          AND NOT child.id = ANY(fw.visited)
    ),

    self_node AS (
        SELECT
            0 AS depth,
            s.id AS material_unit_id,
            s.material_key,
            s.material_type,
            NULL::uuid AS via_edge_id,
            'self'::text AS direction
        FROM start_node s
    ),

    combined AS (
        SELECT * FROM self_node

        UNION ALL

        SELECT
            depth,
            material_unit_id,
            material_key,
            material_type,
            via_edge_id,
            direction
        FROM backward_walk
        WHERE depth > 0

        UNION ALL

        SELECT
            depth,
            material_unit_id,
            material_key,
            material_type,
            via_edge_id,
            direction
        FROM forward_walk
        WHERE depth > 0
    )

    SELECT COALESCE(
        jsonb_agg(
            jsonb_build_object(
                'depth', depth,
                'materialUnitId', material_unit_id,
                'materialKey', material_key,
                'materialType', material_type,
                'direction', direction,
                'viaEdgeId', via_edge_id
            )
            ORDER BY depth, direction, material_key
        ),
        '[]'::jsonb
    )
    INTO v_result
    FROM combined;

    RETURN v_result;
END;
$$;

COMMENT ON FUNCTION public.ppiq_walk_genealogy(uuid, text, text, integer) IS
    'P04 hotfixed bidirectional genealogy walker using separate recursive CTEs for PostgreSQL compliance.';

COMMENT ON FUNCTION public.ppiq_validate_safe_sql(text) IS
    'P03 hotfixed Safe-SQL validator with ForbiddenStatement priority before generic SELECT-only rejection.';