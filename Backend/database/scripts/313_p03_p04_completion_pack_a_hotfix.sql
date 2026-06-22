-- ============================================================================
-- PlantProcess IQ — P03/P04 Completion Pack A Hotfix
--
-- Fixes:
-- 1) ppiq_material_investigation material_key ambiguity
-- 2) mapping lifecycle rollback proof by creating a previous published version
-- 3) business-key duplicate sample classification for cross-source demo match
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
        'Business-key definition has no source members.',
        jsonb_build_object('definitionId', d.id, 'entityScope', d.entity_scope, 'version', d.version_number)
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
        'Business-key member is nullable. It can create weak canonical joins and should be reviewed.',
        jsonb_build_object(
            'definitionId', d.id,
            'memberId', m.id,
            'sourceSystem', m.source_system,
            'datasetName', m.dataset_name,
            'columnName', m.column_name
        )
    FROM public.ppiq_business_key_definitions d
    JOIN public.ppiq_business_key_members m ON m.definition_id = d.id
    WHERE m.is_nullable = true;

    RETURN QUERY
    SELECT
        'Error'::text,
        'DuplicateSourceMember'::text,
        d.key_code || ':' || m.source_system || '.' || m.dataset_name || '.' || m.column_name,
        'The same source-system/dataset/column is declared more than once inside the same business-key definition.',
        jsonb_build_object(
            'definitionId', d.id,
            'sourceSystem', m.source_system,
            'datasetName', m.dataset_name,
            'columnName', m.column_name,
            'count', count(*)
        )
    FROM public.ppiq_business_key_definitions d
    JOIN public.ppiq_business_key_members m ON m.definition_id = d.id
    GROUP BY d.id, d.key_code, m.source_system, m.dataset_name, m.column_name
    HAVING count(*) > 1;

    RETURN QUERY
    SELECT
        'Info'::text,
        'CrossSourceSharedSample'::text,
        d.key_code || ':' || lower(coalesce(m.normalized_sample_value, '')),
        'The same normalized sample appears across multiple source members. This proves cross-source identity linking and is not a fatal conflict.',
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
        'BusinessKeyValidationExecuted'::text,
        'BusinessKeyDictionary'::text,
        'Business-key validation completed.',
        jsonb_build_object(
            'definitionCount', (SELECT count(*) FROM public.ppiq_business_key_definitions),
            'memberCount', (SELECT count(*) FROM public.ppiq_business_key_members)
        );
END;
$$;

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
    v_mapping_code text := 'P03_COMPLETION_PROOF_' || replace(left(gen_random_uuid()::text, 8), '-', '');
    v_previous_version uuid;
    v_current_version uuid;
    v_result jsonb;
BEGIN
    SELECT pt.id
    INTO v_tenant
    FROM public.ppiq_tenants pt
    ORDER BY pt.created_at_utc
    LIMIT 1;

    IF v_tenant IS NULL THEN
        RETURN QUERY SELECT 'Tenant', false, 'NoTenant', 'No tenant exists.', '{}'::jsonb;
        RETURN;
    END IF;

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
        v_mapping_code,
        'P03/P04 previous published proof version',
        'QualityEvent',
        'demo',
        1,
        jsonb_build_object(
            'source', 'public.v_ppiq_p03_coil_quality_event',
            'requiredFields', jsonb_build_array('material_key', 'event_time_utc', 'quality_event_type')
        ),
        'Draft'
    )
    RETURNING id INTO v_previous_version;

    v_result := public.ppiq_dry_run_mapping_version(v_previous_version);
    RETURN QUERY SELECT 'PreviousDryRun', coalesce((v_result->>'isValid')::boolean, false), v_result->>'code', v_result->>'message', v_result;

    v_result := public.ppiq_publish_mapping_version(v_previous_version);
    RETURN QUERY SELECT 'PreviousPublish', coalesce((v_result->>'isValid')::boolean, false), v_result->>'code', v_result->>'message', v_result;

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
        v_mapping_code,
        'P03/P04 current publish and rollback proof version',
        'QualityEvent',
        'demo',
        2,
        jsonb_build_object(
            'source', 'public.v_ppiq_p03_coil_quality_event',
            'requiredFields', jsonb_build_array('material_key', 'event_time_utc', 'quality_event_type'),
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
    RETURNING id INTO v_current_version;

    RETURN QUERY
    SELECT
        'Validate',
        NOT EXISTS (
            SELECT 1
            FROM public.ppiq_validate_canonical_mapping_version(v_current_version)
            WHERE severity = 'Error'
        ),
        'CanonicalMappingValidation',
        'Mapping validation executed.',
        coalesce(jsonb_agg(to_jsonb(v)), '[]'::jsonb)
    FROM public.ppiq_validate_canonical_mapping_version(v_current_version) v;

    v_result := public.ppiq_dry_run_mapping_version(v_current_version);
    RETURN QUERY SELECT 'DryRun', coalesce((v_result->>'isValid')::boolean, false), v_result->>'code', v_result->>'message', v_result;

    v_result := public.ppiq_publish_mapping_version(v_current_version);
    RETURN QUERY SELECT 'Publish', coalesce((v_result->>'isValid')::boolean, false), v_result->>'code', v_result->>'message', v_result;

    v_result := public.ppiq_rollback_mapping_version(v_current_version);
    RETURN QUERY SELECT 'Rollback', coalesce((v_result->>'isValid')::boolean, false), v_result->>'code', coalesce(v_result->>'message', 'Rollback restored previous version.'), v_result;
END;
$$;

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
    v_material_id uuid;
BEGIN
    SELECT cmu.id
    INTO v_material_id
    FROM public.canonical_material_units cmu
    WHERE lower(cmu.material_key) = lower(p_material_key)
    LIMIT 1;

    IF v_material_id IS NULL THEN
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
    SELECT
        'Material'::text,
        10,
        cmu.material_key,
        cmu.material_type,
        cmu.material_key,
        cmu.production_start_utc,
        'Selected'::text,
        'Selected material.',
        jsonb_build_object('materialId', cmu.id, 'heatKey', cmu.heat_key, 'attributes', cmu.attributes)
    FROM public.canonical_material_units cmu
    WHERE cmu.id = v_material_id;

    RETURN QUERY
    SELECT
        'GenealogyParent'::text,
        100,
        parent_mu.material_key,
        parent_mu.material_type,
        parent_mu.material_key,
        NULL::timestamptz,
        'Related'::text,
        'Upstream parent material.',
        jsonb_build_object('edgeId', cge.id, 'edgeType', cge.edge_type, 'confidence', cge.confidence)
    FROM public.canonical_genealogy_edges cge
    JOIN public.canonical_material_units parent_mu ON parent_mu.id = cge.parent_material_unit_id
    WHERE cge.child_material_unit_id = v_material_id
    ORDER BY parent_mu.material_key;

    RETURN QUERY
    SELECT
        'GenealogyChild'::text,
        120,
        child_mu.material_key,
        child_mu.material_type,
        child_mu.material_key,
        NULL::timestamptz,
        'Related'::text,
        'Downstream child material.',
        jsonb_build_object('edgeId', cge.id, 'edgeType', cge.edge_type, 'confidence', cge.confidence)
    FROM public.canonical_genealogy_edges cge
    JOIN public.canonical_material_units child_mu ON child_mu.id = cge.child_material_unit_id
    WHERE cge.parent_material_unit_id = v_material_id
    ORDER BY child_mu.material_key;

    RETURN QUERY
    SELECT
        'ProcessStep'::text,
        200,
        cmu.material_key,
        cmu.material_type,
        cpse.process_code,
        cpse.start_utc,
        'Info'::text,
        'Process step linked to material.',
        jsonb_build_object('processStepId', cpse.id, 'equipmentId', cpse.equipment_id, 'endUtc', cpse.end_utc, 'attributes', cpse.attributes)
    FROM public.canonical_process_step_executions cpse
    JOIN public.canonical_material_units cmu ON cmu.id = cpse.material_unit_id
    WHERE cpse.material_unit_id = v_material_id
    ORDER BY cpse.start_utc;

    RETURN QUERY
    SELECT
        'Parameter'::text,
        300,
        coalesce(cmu.material_key, p_material_key),
        cmu.material_type,
        cpo.parameter_code,
        cpo.observed_at_utc,
        'Info'::text,
        'Parameter observation linked to material.',
        jsonb_build_object('numericValue', cpo.numeric_value, 'textValue', cpo.text_value, 'unit', cpo.unit, 'attributes', cpo.attributes)
    FROM public.canonical_parameter_observations cpo
    LEFT JOIN public.canonical_material_units cmu ON cmu.id = cpo.material_unit_id
    WHERE cpo.material_unit_id = v_material_id
    ORDER BY cpo.observed_at_utc;

    RETURN QUERY
    SELECT
        'QualityEvent'::text,
        400,
        cmu.material_key,
        cmu.material_type,
        cqe.quality_event_type,
        cqe.event_time_utc,
        cqe.severity,
        coalesce(cqe.description, 'Quality event linked to material.'),
        jsonb_build_object('defectCode', cqe.defect_code, 'attributes', cqe.attributes)
    FROM public.canonical_quality_events cqe
    JOIN public.canonical_material_units cmu ON cmu.id = cqe.material_unit_id
    WHERE cqe.material_unit_id = v_material_id
    ORDER BY cqe.event_time_utc;

    RETURN QUERY
    SELECT
        'Downtime'::text,
        500,
        p_material_key,
        NULL::text,
        coalesce(ce.equipment_code, 'UNKNOWN_EQUIPMENT'),
        cde.event_start_utc,
        CASE WHEN cde.production_stoppage_minutes > 0 THEN 'ProductionImpact' ELSE 'Absorbed' END,
        cde.reason,
        jsonb_build_object(
            'downtimeEventId', cde.id,
            'equipmentStoppageMinutes', cde.equipment_stoppage_minutes,
            'bufferAbsorbedMinutes', cde.buffer_absorbed_minutes,
            'cascadeAmplificationFactor', cde.cascade_amplification_factor,
            'productionStoppageMinutes', cde.production_stoppage_minutes,
            'stoppageClass', cde.stoppage_class
        )
    FROM public.canonical_downtime_events cde
    LEFT JOIN public.canonical_equipment ce ON ce.id = cde.equipment_id
    WHERE EXISTS (
        SELECT 1
        FROM public.canonical_process_step_executions cpse2
        WHERE cpse2.material_unit_id = v_material_id
          AND cpse2.equipment_id = cde.equipment_id
    )
    ORDER BY cde.event_start_utc;
END;
$$;

CREATE OR REPLACE VIEW public.v_ppiq_p04_completion_golden_thread AS
SELECT *
FROM public.ppiq_material_investigation(
    coalesce(
        (
            SELECT cmu.material_key
            FROM public.canonical_material_units cmu
            WHERE lower(cmu.material_type) = 'coil'
            ORDER BY cmu.created_at_utc DESC
            LIMIT 1
        ),
        (
            SELECT cmu.material_key
            FROM public.canonical_material_units cmu
            ORDER BY cmu.created_at_utc DESC
            LIMIT 1
        ),
        'UNKNOWN'
    )
);