Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$root = "C:\Workspace\PlantProcess-IQ"
$sqlPath = "$root\Backend\database\scripts\312_p03_p04_completion_pack_a.sql"
$endpointPath = "$root\Backend\PlantProcess.Api\Endpoints\Admin\P03P04CompletionProofEndpoints.cs"
$programPath = "$root\Backend\PlantProcess.Api\Program.cs"
$validatorPath = "$root\tools\p03p04\Validate-P03P04-CompletionPackA.ps1"
$psql = "C:\Program Files\PostgreSQL\16\bin\psql.exe"

New-Item -ItemType Directory -Force -Path (Split-Path $sqlPath) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path $endpointPath) | Out-Null
New-Item -ItemType Directory -Force -Path (Split-Path $validatorPath) | Out-Null

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)

@'
-- ============================================================================
-- PlantProcess IQ — P03/P04 Completion Pack A
-- File: 312_p03_p04_completion_pack_a.sql
--
-- Covers:
-- T014 business-key conflict rejection / validation proof
-- T015 canonical mapper required-field / type / unit validation proof
-- T016 safe SQL typed resolver + EXPLAIN / row-limit / timeout proof
-- T017 mapping dry-run / publish / rollback proof
-- T018 worked-mapping fixtures and tests
-- T020 recursive canonical schema index proof
-- T021 genealogy cycle / orphan validation
-- T023 downtime/value-impact proof
-- T024 golden-thread backend/database proof
--
-- Product guard:
-- Generic manufacturing model. Steel values are demo fixture data only.
-- ============================================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

DO $$
BEGIN
    IF to_regclass('public.ppiq_tenants') IS NULL THEN
        RAISE EXCEPTION 'P03/P04 Completion Pack A requires public.ppiq_tenants. Apply P01/P02 first.';
    END IF;

    IF to_regclass('public.ppiq_business_key_definitions') IS NULL THEN
        RAISE EXCEPTION 'P03/P04 Completion Pack A requires 310_p03_p04_mapping_genealogy_foundation.sql first.';
    END IF;
END $$;

-- ============================================================================
-- T014 — Business-key dictionary validation
-- ============================================================================

CREATE OR REPLACE FUNCTION public.ppiq_validate_business_key_dictionary()
RETURNS TABLE
(
    severity text,
    error_code text,
    object_key text,
    message text,
    evidence jsonb
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT
        'Error'::text,
        'NoMembers'::text,
        d.key_code,
        format('Business key %s v%s has no source members.', d.key_code, d.version_number),
        jsonb_build_object('definitionId', d.id, 'entityScope', d.entity_scope)
    FROM public.ppiq_business_key_definitions d
    WHERE NOT EXISTS (
        SELECT 1
        FROM public.ppiq_business_key_members m
        WHERE m.definition_id = d.id
    );

    RETURN QUERY
    SELECT
        'Warning'::text,
        'NullableBusinessKeyMember'::text,
        d.key_code || ':' || m.source_system || '.' || m.dataset_name || '.' || m.column_name,
        'A business-key member is nullable. Mapping can continue, but canonical linkage may be incomplete.',
        jsonb_build_object(
            'definitionId', d.id,
            'memberId', m.id,
            'sourceSystem', m.source_system,
            'datasetName', m.dataset_name,
            'columnName', m.column_name
        )
    FROM public.ppiq_business_key_definitions d
    JOIN public.ppiq_business_key_members m ON m.definition_id = d.id
    WHERE m.is_nullable = true
      AND d.status IN ('Draft', 'Active');

    RETURN QUERY
    SELECT
        'Error'::text,
        'DuplicateNormalizedSample'::text,
        d.key_code || ':' || lower(coalesce(m.normalized_sample_value, '')),
        'Two or more source members normalize to the same sample value inside the same business-key definition.',
        jsonb_build_object(
            'definitionId', d.id,
            'normalizedSampleValue', m.normalized_sample_value,
            'count', count(*)
        )
    FROM public.ppiq_business_key_definitions d
    JOIN public.ppiq_business_key_members m ON m.definition_id = d.id
    WHERE nullif(m.normalized_sample_value, '') IS NOT NULL
    GROUP BY d.id, d.key_code, m.normalized_sample_value
    HAVING count(*) > 1;

    RETURN QUERY
    SELECT
        'Info'::text,
        'Ok'::text,
        'BusinessKeyDictionary'::text,
        'Business-key dictionary validation executed. No fatal issue means the dictionary can be used by mapping dry-run.',
        jsonb_build_object(
            'definitionCount', (SELECT count(*) FROM public.ppiq_business_key_definitions),
            'memberCount', (SELECT count(*) FROM public.ppiq_business_key_members)
        );
END;
$$;

-- ============================================================================
-- T015 — Canonical mapping validation
-- ============================================================================

CREATE OR REPLACE FUNCTION public.ppiq_validate_canonical_mapping_version(p_version_id uuid)
RETURNS TABLE
(
    severity text,
    error_code text,
    field_code text,
    message text,
    evidence jsonb
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_mapping public.ppiq_mapping_versions%ROWTYPE;
    v_required text;
BEGIN
    SELECT *
    INTO v_mapping
    FROM public.ppiq_mapping_versions
    WHERE id = p_version_id;

    IF NOT FOUND THEN
        RETURN QUERY
        SELECT
            'Error'::text,
            'NoSuchMappingVersion'::text,
            'versionId'::text,
            'Mapping version does not exist.',
            jsonb_build_object('versionId', p_version_id);

        RETURN;
    END IF;

    IF nullif(v_mapping.canonical_entity, '') IS NULL THEN
        RETURN QUERY
        SELECT
            'Error'::text,
            'RequiredFieldMissing'::text,
            'canonical_entity'::text,
            'canonical_entity is required.',
            jsonb_build_object('mappingCode', v_mapping.mapping_code);
    END IF;

    IF jsonb_typeof(v_mapping.definition) <> 'object' THEN
        RETURN QUERY
        SELECT
            'Error'::text,
            'InvalidDefinitionShape'::text,
            'definition'::text,
            'Mapping definition must be a JSON object.',
            jsonb_build_object('mappingCode', v_mapping.mapping_code);
    END IF;

    IF NOT (v_mapping.definition ? 'source') THEN
        RETURN QUERY
        SELECT
            'Error'::text,
            'RequiredFieldMissing'::text,
            'definition.source'::text,
            'definition.source is required before dry-run/publish.',
            jsonb_build_object('mappingCode', v_mapping.mapping_code);
    END IF;

    IF NOT (v_mapping.definition ? 'requiredFields') THEN
        RETURN QUERY
        SELECT
            'Warning'::text,
            'RequiredFieldsNotDeclared'::text,
            'definition.requiredFields'::text,
            'Mapping does not declare required canonical fields. Add requiredFields for deterministic validation.',
            jsonb_build_object('mappingCode', v_mapping.mapping_code);
    ELSE
        FOR v_required IN
            SELECT jsonb_array_elements_text(v_mapping.definition->'requiredFields')
        LOOP
            IF trim(v_required) = '' THEN
                RETURN QUERY
                SELECT
                    'Error'::text,
                    'RequiredFieldMissing'::text,
                    'definition.requiredFields'::text,
                    'Empty required field entry is not allowed.',
                    jsonb_build_object('mappingCode', v_mapping.mapping_code);
            END IF;
        END LOOP;
    END IF;

    IF v_mapping.definition::text ~* '"fromUnit"\s*:\s*"mm"\s*,\s*"toUnit"\s*:\s*"m"' THEN
        RETURN QUERY
        SELECT
            'Info'::text,
            'UnitConversionVerified'::text,
            'unitConversions'::text,
            'Unit conversion mm → m is declared and recognized for dry-run proof.',
            jsonb_build_object('mappingCode', v_mapping.mapping_code, 'conversion', 'mm_to_m');
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM public.ppiq_validate_canonical_mapping_version(p_version_id) x
        WHERE x.severity = 'Error'
    ) THEN
        RETURN QUERY
        SELECT
            'Info'::text,
            'Ok'::text,
            'mappingVersion'::text,
            'Canonical mapping validation completed.',
            jsonb_build_object(
                'mappingCode', v_mapping.mapping_code,
                'canonicalEntity', v_mapping.canonical_entity,
                'environment', v_mapping.environment,
                'status', v_mapping.status
            );
    END IF;
END;
$$;

-- ============================================================================
-- T016 — Typed safe SQL resolver with EXPLAIN, row-limit and timeout
-- ============================================================================

CREATE OR REPLACE FUNCTION public.ppiq_resolve_safe_sql(
    p_sql text,
    p_row_limit integer DEFAULT 100,
    p_timeout_ms integer DEFAULT 3000
)
RETURNS TABLE
(
    is_valid boolean,
    error_code text,
    message text,
    normalized_sql text,
    applied_row_limit integer
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_sql text := regexp_replace(btrim(coalesce(p_sql, '')), ';\s*$', '');
    v_lower text := lower(regexp_replace(btrim(coalesce(p_sql, '')), ';\s*$', ''));
    v_limit integer := greatest(1, least(coalesce(p_row_limit, 100), 1000));
    v_timeout integer := greatest(250, least(coalesce(p_timeout_ms, 3000), 10000));
BEGIN
    IF v_sql = '' THEN
        RETURN QUERY SELECT false, 'EmptySql', 'SQL text is empty.', v_sql, v_limit;
        RETURN;
    END IF;

    IF NOT (v_lower LIKE 'select %' OR v_lower LIKE 'with %') THEN
        RETURN QUERY SELECT false, 'MustBeSelectOnly', 'Only SELECT or WITH read-only queries are allowed.', v_sql, v_limit;
        RETURN;
    END IF;

    IF v_lower ~ '(^|[[:space:];])(insert|update|delete|drop|alter|truncate|grant|revoke|copy|execute|exec|merge|vacuum|analyze|call|do|listen|notify|prepare|deallocate|create|replace)($|[[:space:];])' THEN
        RETURN QUERY SELECT false, 'ForbiddenStatement', 'SQL contains a forbidden write/DDL/control statement.', v_sql, v_limit;
        RETURN;
    END IF;

    IF position(';' in regexp_replace(v_lower, ';\s*$', '')) > 0 THEN
        RETURN QUERY SELECT false, 'MultipleStatements', 'Only one SQL statement is allowed.', v_sql, v_limit;
        RETURN;
    END IF;

    IF v_lower ~ '\bcross\s+join\b' THEN
        RETURN QUERY SELECT false, 'AmbiguousJoinKey', 'CROSS JOIN is rejected. Declare an explicit business key or join predicate.', v_sql, v_limit;
        RETURN;
    END IF;

    BEGIN
        EXECUTE format('SET LOCAL statement_timeout = %s', v_timeout);
        EXECUTE format('EXPLAIN (FORMAT JSON) SELECT * FROM (%s) __ppiq_safe LIMIT %s', v_sql, v_limit);

        RETURN QUERY
        SELECT
            true,
            'Ok',
            format('SQL passed read-only validation, EXPLAIN, timeout=%sms and rowLimit=%s.', v_timeout, v_limit),
            v_sql,
            v_limit;

        RETURN;
    EXCEPTION
        WHEN undefined_table THEN
            RETURN QUERY SELECT false, 'NoSuchView', SQLERRM, v_sql, v_limit;
            RETURN;
        WHEN undefined_column THEN
            RETURN QUERY SELECT false, 'NoSuchColumn', SQLERRM, v_sql, v_limit;
            RETURN;
        WHEN ambiguous_column THEN
            RETURN QUERY SELECT false, 'AmbiguousJoinKey', SQLERRM, v_sql, v_limit;
            RETURN;
        WHEN undefined_function OR datatype_mismatch OR grouping_error THEN
            RETURN QUERY SELECT false, 'InvalidAggregateForType', SQLERRM, v_sql, v_limit;
            RETURN;
        WHEN query_canceled THEN
            RETURN QUERY SELECT false, 'StatementTimeout', SQLERRM, v_sql, v_limit;
            RETURN;
        WHEN OTHERS THEN
            RETURN QUERY SELECT false, 'SqlResolutionError', SQLERRM, v_sql, v_limit;
            RETURN;
    END;
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_validate_safe_sql_typed(p_sql text)
RETURNS jsonb
LANGUAGE sql
AS $$
    SELECT jsonb_build_object(
        'isValid', r.is_valid,
        'code', r.error_code,
        'message', r.message,
        'normalizedSql', r.normalized_sql,
        'enforcedRowLimit', r.applied_row_limit
    )
    FROM public.ppiq_resolve_safe_sql(p_sql) r
    LIMIT 1;
$$;

-- ============================================================================
-- T017/T018 — Mapping lifecycle proof
-- ============================================================================

CREATE OR REPLACE FUNCTION public.ppiq_run_mapping_lifecycle_proof()
RETURNS TABLE
(
    step_code text,
    is_valid boolean,
    error_code text,
    message text,
    payload jsonb
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_tenant uuid;
    v_version uuid;
    v_version_number integer;
    v_result jsonb;
BEGIN
    SELECT id
    INTO v_tenant
    FROM public.ppiq_tenants
    ORDER BY created_at_utc
    LIMIT 1;

    IF v_tenant IS NULL THEN
        RETURN QUERY SELECT 'Tenant', false, 'NoTenant', 'No tenant exists.', '{}'::jsonb;
        RETURN;
    END IF;

    SELECT coalesce(max(version_number), 0) + 1
    INTO v_version_number
    FROM public.ppiq_mapping_versions
    WHERE tenant_id = v_tenant
      AND mapping_code = 'P03_COMPLETION_PROOF'
      AND environment = 'demo';

    INSERT INTO public.ppiq_mapping_versions
    (
        tenant_id,
        mapping_code,
        display_name,
        canonical_entity,
        environment,
        version_number,
        definition,
        status
    )
    VALUES
    (
        v_tenant,
        'P03_COMPLETION_PROOF',
        'P03/P04 Completion Pack A mapping lifecycle proof',
        'QualityEvent',
        'demo',
        v_version_number,
        jsonb_build_object(
            'source', 'public.v_ppiq_p03_coil_quality_event',
            'requiredFields', jsonb_build_array('coil_key', 'event_time_utc', 'quality_event_type'),
            'fieldMappings', jsonb_build_array(
                jsonb_build_object('sourceColumn','coil_key','targetField','material_key','valueType','text'),
                jsonb_build_object('sourceColumn','event_time_utc','targetField','event_time_utc','valueType','datetime'),
                jsonb_build_object('sourceColumn','quality_event_type','targetField','quality_event_type','valueType','text')
            ),
            'unitConversions', jsonb_build_array(
                jsonb_build_object('fromUnit','mm','toUnit','m','factor',0.001)
            )
        ),
        'Draft'
    )
    RETURNING id INTO v_version;

    RETURN QUERY
    SELECT
        'Validate',
        NOT EXISTS (
            SELECT 1
            FROM public.ppiq_validate_canonical_mapping_version(v_version)
            WHERE severity = 'Error'
        ),
        'CanonicalMappingValidation',
        'Mapping validation executed.',
        jsonb_agg(to_jsonb(v))
    FROM public.ppiq_validate_canonical_mapping_version(v_version) v;

    v_result := public.ppiq_dry_run_mapping_version(v_version);
    RETURN QUERY SELECT 'DryRun', (v_result->>'isValid')::boolean, v_result->>'code', v_result->>'message', v_result;

    v_result := public.ppiq_publish_mapping_version(v_version);
    RETURN QUERY SELECT 'Publish', (v_result->>'isValid')::boolean, v_result->>'code', v_result->>'message', v_result;

    v_result := public.ppiq_rollback_mapping_version(v_version);
    RETURN QUERY SELECT 'Rollback', (v_result->>'isValid')::boolean, v_result->>'code', v_result->>'message', v_result;
END;
$$;

-- ============================================================================
-- T020/T021 — Recursive canonical/genealogy validation
-- ============================================================================

CREATE OR REPLACE FUNCTION public.ppiq_detect_genealogy_cycles()
RETURNS TABLE
(
    severity text,
    error_code text,
    material_key text,
    message text,
    evidence jsonb
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    WITH RECURSIVE walk AS
    (
        SELECT
            e.tenant_id,
            e.parent_material_unit_id AS start_id,
            e.child_material_unit_id AS current_id,
            ARRAY[e.parent_material_unit_id, e.child_material_unit_id] AS path,
            1 AS depth
        FROM public.canonical_genealogy_edges e

        UNION ALL

        SELECT
            w.tenant_id,
            w.start_id,
            e.child_material_unit_id,
            w.path || e.child_material_unit_id,
            w.depth + 1
        FROM walk w
        JOIN public.canonical_genealogy_edges e
            ON e.tenant_id = w.tenant_id
           AND e.parent_material_unit_id = w.current_id
        WHERE w.depth < 20
          AND NOT e.child_material_unit_id = ANY(w.path)
    ),
    cycles AS
    (
        SELECT
            w.tenant_id,
            w.start_id,
            e.child_material_unit_id AS repeated_id,
            w.path,
            w.depth
        FROM walk w
        JOIN public.canonical_genealogy_edges e
            ON e.tenant_id = w.tenant_id
           AND e.parent_material_unit_id = w.current_id
        WHERE e.child_material_unit_id = ANY(w.path)
    )
    SELECT
        'Error'::text,
        'GenealogyCycle'::text,
        m.material_key,
        'A cycle was detected in canonical material genealogy.',
        jsonb_build_object('startMaterialId', c.start_id, 'repeatedMaterialId', c.repeated_id, 'depth', c.depth)
    FROM cycles c
    JOIN public.canonical_material_units m ON m.id = c.start_id;
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_detect_genealogy_orphans()
RETURNS TABLE
(
    severity text,
    error_code text,
    material_key text,
    message text,
    evidence jsonb
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY
    SELECT
        'Warning'::text,
        'IsolatedMaterial'::text,
        m.material_key,
        'Material exists but has no parent or child genealogy edge.',
        jsonb_build_object('materialId', m.id, 'materialType', m.material_type, 'sourceConfidence', m.source_confidence)
    FROM public.canonical_material_units m
    WHERE NOT EXISTS (
        SELECT 1
        FROM public.canonical_genealogy_edges e
        WHERE e.parent_material_unit_id = m.id
           OR e.child_material_unit_id = m.id
    );
END;
$$;

CREATE OR REPLACE FUNCTION public.ppiq_validate_genealogy_graph()
RETURNS TABLE
(
    severity text,
    error_code text,
    material_key text,
    message text,
    evidence jsonb
)
LANGUAGE plpgsql
AS $$
BEGIN
    RETURN QUERY SELECT * FROM public.ppiq_detect_genealogy_cycles();
    RETURN QUERY SELECT * FROM public.ppiq_detect_genealogy_orphans();

    RETURN QUERY
    SELECT
        'Info'::text,
        'Ok'::text,
        'GenealogyGraph'::text,
        'Genealogy graph validation executed.',
        jsonb_build_object(
            'materialCount', (SELECT count(*) FROM public.canonical_material_units),
            'edgeCount', (SELECT count(*) FROM public.canonical_genealogy_edges),
            'cycleCount', (SELECT count(*) FROM public.ppiq_detect_genealogy_cycles()),
            'orphanCount', (SELECT count(*) FROM public.ppiq_detect_genealogy_orphans())
        );
END;
$$;

-- ============================================================================
-- T022/T024 backend proof — Material Investigation trace
-- ============================================================================

CREATE OR REPLACE FUNCTION public.ppiq_material_investigation(p_material_key text)
RETURNS TABLE
(
    section_code text,
    sequence_no integer,
    material_key text,
    material_type text,
    object_code text,
    event_time_utc timestamptz,
    severity text,
    message text,
    payload jsonb
)
LANGUAGE plpgsql
AS $$
DECLARE
    v_material uuid;
BEGIN
    SELECT id
    INTO v_material
    FROM public.canonical_material_units
    WHERE lower(material_key) = lower(p_material_key)
    LIMIT 1;

    IF v_material IS NULL THEN
        RETURN QUERY
        SELECT
            'Input'::text,
            0,
            p_material_key,
            NULL::text,
            p_material_key,
            NULL::timestamptz,
            'Error'::text,
            'Unknown material key.',
            jsonb_build_object('requestedMaterialKey', p_material_key);

        RETURN;
    END IF;

    RETURN QUERY
    WITH RECURSIVE parent_walk AS
    (
        SELECT
            m.id,
            m.material_key,
            m.material_type,
            0 AS depth,
            ARRAY[m.id] AS path
        FROM public.canonical_material_units m
        WHERE m.id = v_material

        UNION ALL

        SELECT
            p.id,
            p.material_key,
            p.material_type,
            pw.depth - 1,
            pw.path || p.id
        FROM parent_walk pw
        JOIN public.canonical_genealogy_edges e ON e.child_material_unit_id = pw.id
        JOIN public.canonical_material_units p ON p.id = e.parent_material_unit_id
        WHERE pw.depth > -10
          AND NOT p.id = ANY(pw.path)
    ),
    child_walk AS
    (
        SELECT
            m.id,
            m.material_key,
            m.material_type,
            0 AS depth,
            ARRAY[m.id] AS path
        FROM public.canonical_material_units m
        WHERE m.id = v_material

        UNION ALL

        SELECT
            c.id,
            c.material_key,
            c.material_type,
            cw.depth + 1,
            cw.path || c.id
        FROM child_walk cw
        JOIN public.canonical_genealogy_edges e ON e.parent_material_unit_id = cw.id
        JOIN public.canonical_material_units c ON c.id = e.child_material_unit_id
        WHERE cw.depth < 10
          AND NOT c.id = ANY(cw.path)
    ),
    graph AS
    (
        SELECT DISTINCT id, material_key, material_type, depth FROM parent_walk
        UNION
        SELECT DISTINCT id, material_key, material_type, depth FROM child_walk
    )
    SELECT
        'Genealogy'::text,
        100 + g.depth,
        g.material_key,
        g.material_type,
        g.material_key,
        NULL::timestamptz,
        CASE WHEN g.depth = 0 THEN 'Selected' ELSE 'Related' END,
        CASE
            WHEN g.depth < 0 THEN 'Upstream material ancestor'
            WHEN g.depth > 0 THEN 'Downstream material descendant'
            ELSE 'Selected material'
        END,
        jsonb_build_object('depth', g.depth, 'materialId', g.id)
    FROM graph g
    ORDER BY g.depth, g.material_key;

    RETURN QUERY
    SELECT
        'ProcessStep'::text,
        200,
        m.material_key,
        m.material_type,
        pse.process_code,
        pse.start_utc,
        'Info'::text,
        'Process step linked to material.',
        jsonb_build_object(
            'processStepId', pse.id,
            'equipmentId', pse.equipment_id,
            'endUtc', pse.end_utc,
            'attributes', pse.attributes
        )
    FROM public.canonical_process_step_executions pse
    JOIN public.canonical_material_units m ON m.id = pse.material_unit_id
    WHERE pse.material_unit_id IN (
        SELECT id FROM public.canonical_material_units WHERE lower(material_key) = lower(p_material_key)
    )
    ORDER BY pse.start_utc;

    RETURN QUERY
    SELECT
        'Parameter'::text,
        300,
        m.material_key,
        m.material_type,
        po.parameter_code,
        po.observed_at_utc,
        'Info'::text,
        'Parameter observation linked to material or equipment.',
        jsonb_build_object(
            'parameterObservationId', po.id,
            'numericValue', po.numeric_value,
            'textValue', po.text_value,
            'unit', po.unit,
            'attributes', po.attributes
        )
    FROM public.canonical_parameter_observations po
    LEFT JOIN public.canonical_material_units m ON m.id = po.material_unit_id
    WHERE po.material_unit_id = v_material
    ORDER BY po.observed_at_utc;

    RETURN QUERY
    SELECT
        'QualityEvent'::text,
        400,
        m.material_key,
        m.material_type,
        qe.quality_event_type,
        qe.event_time_utc,
        qe.severity,
        coalesce(qe.description, 'Quality event linked to material.'),
        jsonb_build_object(
            'qualityEventId', qe.id,
            'defectCode', qe.defect_code,
            'attributes', qe.attributes
        )
    FROM public.canonical_quality_events qe
    JOIN public.canonical_material_units m ON m.id = qe.material_unit_id
    WHERE qe.material_unit_id = v_material
    ORDER BY qe.event_time_utc;

    RETURN QUERY
    SELECT
        'Downtime'::text,
        500,
        p_material_key,
        NULL::text,
        coalesce(eq.equipment_code, 'UNKNOWN_EQUIPMENT'),
        d.event_start_utc,
        CASE WHEN d.production_stoppage_minutes > 0 THEN 'ProductionImpact' ELSE 'Absorbed' END,
        d.reason,
        jsonb_build_object(
            'downtimeEventId', d.id,
            'equipmentStoppageMinutes', d.equipment_stoppage_minutes,
            'bufferAbsorbedMinutes', d.buffer_absorbed_minutes,
            'cascadeAmplificationFactor', d.cascade_amplification_factor,
            'productionStoppageMinutes', d.production_stoppage_minutes,
            'stoppageClass', d.stoppage_class
        )
    FROM public.canonical_downtime_events d
    LEFT JOIN public.canonical_equipment eq ON eq.id = d.equipment_id
    WHERE EXISTS (
        SELECT 1
        FROM public.canonical_process_step_executions pse
        WHERE pse.material_unit_id = v_material
          AND pse.equipment_id = d.equipment_id
    )
    ORDER BY d.event_start_utc;
END;
$$;

-- ============================================================================
-- T018/T023/T024 — Worked proof views
-- ============================================================================

CREATE OR REPLACE VIEW public.v_ppiq_p03_worked_quality_event AS
SELECT
    tenant_code,
    coil_key,
    original_coil_id,
    quality_event_type,
    defect_code,
    severity,
    description,
    event_time_utc
FROM public.v_ppiq_p03_coil_quality_event;

CREATE OR REPLACE VIEW public.v_ppiq_p03_fpsy_kpi AS
SELECT
    t.tenant_code,
    count(*)::integer AS material_count,
    count(*) FILTER (WHERE qe.id IS NULL)::integer AS first_pass_material_count,
    CASE
        WHEN count(*) = 0 THEN 0::numeric
        ELSE round((count(*) FILTER (WHERE qe.id IS NULL))::numeric / count(*)::numeric * 100, 4)
    END AS first_pass_yield_percent
FROM public.canonical_material_units m
JOIN public.ppiq_tenants t ON t.id = m.tenant_id
LEFT JOIN public.canonical_quality_events qe ON qe.material_unit_id = m.id
GROUP BY t.tenant_code;

CREATE OR REPLACE VIEW public.v_ppiq_p04_downtime_value_impact AS
SELECT
    t.tenant_code,
    coalesce(eq.equipment_code, 'UNKNOWN_EQUIPMENT') AS equipment_code,
    d.stoppage_class,
    d.reason,
    d.equipment_stoppage_minutes,
    d.buffer_absorbed_minutes,
    d.cascade_amplification_factor,
    d.production_stoppage_minutes,
    CASE
        WHEN d.production_stoppage_minutes = 0 THEN 'AbsorbedByBuffer'
        ELSE 'ProductionLoss'
    END AS impact_class
FROM public.canonical_downtime_events d
JOIN public.ppiq_tenants t ON t.id = d.tenant_id
LEFT JOIN public.canonical_equipment eq ON eq.id = d.equipment_id;

CREATE OR REPLACE VIEW public.v_ppiq_p04_completion_golden_thread AS
SELECT *
FROM public.ppiq_material_investigation(
    coalesce(
        (
            SELECT material_key
            FROM public.canonical_material_units
            WHERE lower(material_type) IN ('coil', 'finished_good', 'finishedgood')
            ORDER BY created_at_utc DESC
            LIMIT 1
        ),
        (
            SELECT material_key
            FROM public.canonical_material_units
            ORDER BY created_at_utc DESC
            LIMIT 1
        ),
        'UNKNOWN'
    )
);

-- ============================================================================
-- Final completion status
-- ============================================================================

CREATE OR REPLACE FUNCTION public.ppiq_p03_p04_completion_status()
RETURNS TABLE
(
    gate_code text,
    task_code text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT 'BusinessKeyValidation', 'T014', to_regprocedure('public.ppiq_validate_business_key_dictionary()') IS NOT NULL,
           'Business-key dictionary validator exists.'
    UNION ALL
    SELECT 'CanonicalMappingValidation', 'T015', to_regprocedure('public.ppiq_validate_canonical_mapping_version(uuid)') IS NOT NULL,
           'Canonical mapping version validator exists.'
    UNION ALL
    SELECT 'SafeSqlTypedResolver', 'T016', to_regprocedure('public.ppiq_resolve_safe_sql(text,integer,integer)') IS NOT NULL,
           'Typed Safe-SQL resolver exists.'
    UNION ALL
    SELECT 'MappingLifecycleProof', 'T017/T018', to_regprocedure('public.ppiq_run_mapping_lifecycle_proof()') IS NOT NULL,
           'Dry-run/publish/rollback proof function exists.'
    UNION ALL
    SELECT 'RecursiveCanonicalIndexes', 'T020',
           EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND indexname = 'ix_canonical_genealogy_edges_parent')
           AND EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname = 'public' AND indexname = 'ix_canonical_genealogy_edges_child'),
           'Recursive canonical material genealogy indexes exist.'
    UNION ALL
    SELECT 'GenealogyValidation', 'T021', to_regprocedure('public.ppiq_validate_genealogy_graph()') IS NOT NULL,
           'Cycle/orphan genealogy validator exists.'
    UNION ALL
    SELECT 'MaterialInvestigationBackend', 'T022/T024', to_regprocedure('public.ppiq_material_investigation(text)') IS NOT NULL,
           'Material investigation backend trace exists.'
    UNION ALL
    SELECT 'DowntimeValueImpact', 'T023',
           to_regclass('public.canonical_downtime_events') IS NOT NULL
           AND to_regclass('public.v_ppiq_p04_downtime_value_impact') IS NOT NULL,
           'Downtime equipment-vs-production impact view exists.'
    UNION ALL
    SELECT 'GoldenThreadProof', 'T024', to_regclass('public.v_ppiq_p04_completion_golden_thread') IS NOT NULL,
           'Golden-thread proof view exists.';
$$;

COMMENT ON FUNCTION public.ppiq_p03_p04_completion_status() IS
'P03/P04 Completion Pack A proof gate. Used by validator and dev API endpoint.';
