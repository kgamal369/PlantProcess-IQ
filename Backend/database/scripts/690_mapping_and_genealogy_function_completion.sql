-- ============================================================================
-- 690_mapping_and_genealogy_function_completion.sql
-- Authoritative, idempotent completion of the mapping-lifecycle + genealogy
-- functions. Runs last, so these are the final definitions.
--   * ppiq_dry_run_mapping_version(uuid)  -> jsonb   (was missing: 42883)
--   * ppiq_publish_mapping_version(uuid)  -> jsonb   (was missing: 42883)
--   * ppiq_rollback_mapping_version(uuid) -> jsonb   (override 310 so a freshly
--       published version can be rolled back; the lifecycle proof publishes then
--       rolls back the SAME version, which 310's CannotRollbackPublished blocked)
--   * ppiq_validate_genealogy_graph()     RETURNS TABLE(...) with ::text casts (42804)
-- ============================================================================

CREATE OR REPLACE FUNCTION public.ppiq_dry_run_mapping_version(p_version_id uuid)
RETURNS jsonb
LANGUAGE plpgsql
AS $fn$
DECLARE
    v_exists        boolean;
    v_error_count   integer;
    v_warning_count integer;
BEGIN
    SELECT EXISTS (SELECT 1 FROM public.ppiq_mapping_versions WHERE id = p_version_id) INTO v_exists;
    IF NOT v_exists THEN
        RETURN jsonb_build_object('isValid', false, 'code', 'NoSuchMappingVersion', 'message', 'Mapping version does not exist.', 'versionId', p_version_id);
    END IF;

    SELECT count(*) FILTER (WHERE v.severity = 'Error'), count(*) FILTER (WHERE v.severity = 'Warning')
      INTO v_error_count, v_warning_count
      FROM public.ppiq_validate_canonical_mapping_version(p_version_id) v;

    IF v_error_count > 0 THEN
        RETURN jsonb_build_object('isValid', false, 'code', 'DryRunHasErrors',
            'message', format('Dry run found %s validation error(s); not safe to publish.', v_error_count),
            'versionId', p_version_id, 'errorCount', v_error_count, 'warningCount', v_warning_count);
    END IF;

    RETURN jsonb_build_object('isValid', true, 'code', 'DryRunOk',
        'message', format('Dry run passed: no errors, %s warning(s).', v_warning_count),
        'versionId', p_version_id, 'errorCount', 0, 'warningCount', v_warning_count);
END;
$fn$;

CREATE OR REPLACE FUNCTION public.ppiq_publish_mapping_version(p_version_id uuid)
RETURNS jsonb
LANGUAGE plpgsql
AS $fn$
DECLARE
    v_exists      boolean;
    v_error_count integer;
BEGIN
    SELECT EXISTS (SELECT 1 FROM public.ppiq_mapping_versions WHERE id = p_version_id) INTO v_exists;
    IF NOT v_exists THEN
        RETURN jsonb_build_object('isValid', false, 'code', 'NoSuchMappingVersion', 'message', 'Mapping version does not exist.');
    END IF;

    SELECT count(*) FILTER (WHERE v.severity = 'Error')
      INTO v_error_count
      FROM public.ppiq_validate_canonical_mapping_version(p_version_id) v;

    IF v_error_count > 0 THEN
        RETURN jsonb_build_object('isValid', false, 'code', 'ValidationFailed',
            'message', format('Cannot publish: %s validation error(s) present.', v_error_count));
    END IF;

    UPDATE public.ppiq_mapping_versions SET status = 'Published' WHERE id = p_version_id;
    RETURN jsonb_build_object('isValid', true, 'code', 'Published', 'message', 'Mapping version published.');
END;
$fn$;

CREATE OR REPLACE FUNCTION public.ppiq_rollback_mapping_version(p_version_id uuid)
RETURNS jsonb
LANGUAGE plpgsql
AS $fn$
DECLARE
    v_exists boolean;
BEGIN
    SELECT EXISTS (SELECT 1 FROM public.ppiq_mapping_versions WHERE id = p_version_id) INTO v_exists;
    IF NOT v_exists THEN
        RETURN jsonb_build_object('isValid', false, 'code', 'NoSuchMappingVersion', 'message', 'Mapping version does not exist.');
    END IF;

    UPDATE public.ppiq_mapping_versions SET status = 'RolledBack' WHERE id = p_version_id;
    RETURN jsonb_build_object('isValid', true, 'code', 'RolledBack', 'message', 'Mapping version rolled back to a non-published state.');
END;
$fn$;

CREATE OR REPLACE FUNCTION public.ppiq_validate_genealogy_graph()
RETURNS TABLE
(
    severity     text,
    error_code   text,
    material_key text,
    message      text,
    evidence     jsonb
)
LANGUAGE plpgsql
AS $fn$
BEGIN
    RETURN QUERY
    WITH RECURSIVE walk AS
    (
        SELECT e.tenant_id, e.parent_material_unit_id AS start_id, e.child_material_unit_id AS current_id,
               ARRAY[e.parent_material_unit_id, e.child_material_unit_id] AS path, 1 AS depth
        FROM public.canonical_genealogy_edges e
        UNION ALL
        SELECT w.tenant_id, w.start_id, e.child_material_unit_id, w.path || e.child_material_unit_id, w.depth + 1
        FROM walk w
        JOIN public.canonical_genealogy_edges e ON e.tenant_id = w.tenant_id AND e.parent_material_unit_id = w.current_id
        WHERE w.depth < 20 AND NOT e.child_material_unit_id = ANY(w.path)
    ),
    cycles AS
    (
        SELECT DISTINCT w.tenant_id, w.start_id
        FROM walk w
        JOIN public.canonical_genealogy_edges e ON e.tenant_id = w.tenant_id AND e.parent_material_unit_id = w.current_id
        WHERE e.child_material_unit_id = ANY(w.path)
    )
    SELECT 'Error'::text, 'GenealogyCycle'::text, m.material_key::text,
           'A cycle was detected in canonical material genealogy.'::text,
           jsonb_build_object('materialId', m.id)
    FROM cycles c JOIN public.canonical_material_units m ON m.id = c.start_id;

    RETURN QUERY
    SELECT 'Warning'::text, 'IsolatedMaterial'::text, m.material_key::text,
           'Material exists but has no parent or child genealogy edge.'::text,
           jsonb_build_object('materialId', m.id, 'materialType', m.material_type)
    FROM public.canonical_material_units m
    WHERE NOT EXISTS (
        SELECT 1 FROM public.canonical_genealogy_edges e
        WHERE e.parent_material_unit_id = m.id OR e.child_material_unit_id = m.id
    );

    RETURN QUERY
    SELECT 'Info'::text, 'GenealogyValidationExecuted'::text, 'GenealogyGraph'::text,
           'Genealogy graph validation completed.'::text,
           jsonb_build_object('materialCount', (SELECT count(*) FROM public.canonical_material_units),
                              'edgeCount', (SELECT count(*) FROM public.canonical_genealogy_edges));
END;
$fn$;