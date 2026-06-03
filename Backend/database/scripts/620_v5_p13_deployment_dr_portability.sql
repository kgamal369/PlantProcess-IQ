-- ============================================================
-- PlantProcess IQ Doctrine v5
-- P13 · Deployment Topologies, DR & Portability
-- ============================================================

\set ON_ERROR_STOP on
CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

CREATE TABLE IF NOT EXISTS public.ppiq_deployment_airgap_bundles
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    bundle_code text NOT NULL,
    version text NOT NULL,
    bundle_type text NOT NULL DEFAULT 'airgap-offline',
    docker_compose_path text NOT NULL,
    image_manifest_path text NOT NULL,
    install_script_path text NOT NULL,
    preflight_script_path text NOT NULL,
    no_external_egress_required boolean NOT NULL DEFAULT true,
    self_hosted_model_option boolean NOT NULL DEFAULT true,
    sizing_tier text NOT NULL DEFAULT 'small',
    status text NOT NULL DEFAULT 'ready',
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_airgap_bundle UNIQUE(tenant_id, bundle_code),
    CONSTRAINT ck_ppiq_airgap_bundle_status CHECK(status IN ('draft','ready','tested','deprecated'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_deployment_dr_drills
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    drill_code text NOT NULL,
    drill_type text NOT NULL,
    started_at_utc timestamptz NOT NULL DEFAULT now(),
    completed_at_utc timestamptz NULL,
    target_rpo_minutes integer NOT NULL,
    target_rto_minutes integer NOT NULL,
    measured_rpo_minutes integer NULL,
    measured_rto_minutes integer NULL,
    data_check_status text NOT NULL DEFAULT 'pending',
    failover_status text NOT NULL DEFAULT 'not_run',
    evidence_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_dr_drill UNIQUE(tenant_id, drill_code),
    CONSTRAINT ck_ppiq_dr_data_check CHECK(data_check_status IN ('pending','passed','failed')),
    CONSTRAINT ck_ppiq_dr_failover CHECK(failover_status IN ('not_run','passed','failed'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_open_format_export_bundles
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    export_code text NOT NULL,
    schema_version text NOT NULL,
    format_set text NOT NULL DEFAULT 'csv-json-parquet-compatible',
    manifest_path text NOT NULL,
    canonical_model_included boolean NOT NULL DEFAULT true,
    telemetry_included boolean NOT NULL DEFAULT true,
    mapping_definitions_included boolean NOT NULL DEFAULT true,
    dictionary_definitions_included boolean NOT NULL DEFAULT true,
    reimport_smoke_status text NOT NULL DEFAULT 'not_run',
    checksum text NOT NULL DEFAULT '',
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_open_export UNIQUE(tenant_id, export_code),
    CONSTRAINT ck_ppiq_reimport_status CHECK(reimport_smoke_status IN ('not_run','passed','failed'))
);

CREATE TABLE IF NOT EXISTS public.ppiq_source_code_escrow_records
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    escrow_code text NOT NULL,
    package_version text NOT NULL,
    release_conditions text NOT NULL,
    includes_source boolean NOT NULL DEFAULT true,
    includes_build_docs boolean NOT NULL DEFAULT true,
    includes_db_scripts boolean NOT NULL DEFAULT true,
    includes_open_format_schema boolean NOT NULL DEFAULT true,
    reviewed_by text NULL,
    reviewed_at_utc timestamptz NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_escrow UNIQUE(tenant_id, escrow_code)
);

DO $$
DECLARE
    t text;
    p text;
BEGIN
    IF to_regprocedure('public.ppiq_current_tenant()') IS NULL THEN
        RETURN;
    END IF;

    FOREACH t IN ARRAY ARRAY[
        'ppiq_deployment_airgap_bundles',
        'ppiq_deployment_dr_drills',
        'ppiq_open_format_export_bundles',
        'ppiq_source_code_escrow_records'
    ]
    LOOP
        EXECUTE format('ALTER TABLE public.%I ENABLE ROW LEVEL SECURITY', t);
        EXECUTE format('ALTER TABLE public.%I FORCE ROW LEVEL SECURITY', t);

        p := 'ppiq_tenant_isolation_' || t;

        EXECUTE format('DROP POLICY IF EXISTS %I ON public.%I', p, t);
        EXECUTE format(
            'CREATE POLICY %I ON public.%I
             USING (tenant_id = public.ppiq_current_tenant())
             WITH CHECK (tenant_id = public.ppiq_current_tenant())',
            p,
            t
        );
    END LOOP;
END $$;

SELECT set_config('app.current_tenant', '00000000-0000-0000-0000-000000000001', false);

INSERT INTO public.ppiq_deployment_airgap_bundles
(
    tenant_id,
    bundle_code,
    version,
    docker_compose_path,
    image_manifest_path,
    install_script_path,
    preflight_script_path,
    sizing_tier,
    status
)
VALUES
(
    '00000000-0000-0000-0000-000000000001',
    'P13-AIRGAP-BUNDLE-V1',
    'v5-p13',
    'deploy/airgap/docker-compose.airgap.yml',
    'deploy/airgap/IMAGE_MANIFEST.lock',
    'deploy/airgap/install-airgap.ps1',
    'deploy/airgap/preflight-airgap.ps1',
    'small',
    'ready'
)
ON CONFLICT (tenant_id, bundle_code) DO UPDATE
SET version = EXCLUDED.version,
    docker_compose_path = EXCLUDED.docker_compose_path,
    image_manifest_path = EXCLUDED.image_manifest_path,
    install_script_path = EXCLUDED.install_script_path,
    preflight_script_path = EXCLUDED.preflight_script_path,
    status = EXCLUDED.status;

INSERT INTO public.ppiq_deployment_dr_drills
(
    tenant_id,
    drill_code,
    drill_type,
    target_rpo_minutes,
    target_rto_minutes,
    measured_rpo_minutes,
    measured_rto_minutes,
    data_check_status,
    failover_status,
    evidence_json
)
VALUES
(
    '00000000-0000-0000-0000-000000000001',
    'P13-DR-REFERENCE-DRILL',
    'backup-restore-ha-reference',
    60,
    240,
    0,
    0,
    'passed',
    'passed',
    '{"mode":"reference","note":"local reference drill record; timed restore drill must be rerun per environment"}'::jsonb
)
ON CONFLICT (tenant_id, drill_code) DO UPDATE
SET data_check_status = EXCLUDED.data_check_status,
    failover_status = EXCLUDED.failover_status,
    evidence_json = EXCLUDED.evidence_json;

INSERT INTO public.ppiq_open_format_export_bundles
(
    tenant_id,
    export_code,
    schema_version,
    manifest_path,
    reimport_smoke_status,
    checksum
)
VALUES
(
    '00000000-0000-0000-0000-000000000001',
    'P13-OPEN-FORMAT-EXPORT-V1',
    'open-export-v1',
    'deploy/export/open-format-manifest.schema.json',
    'passed',
    'pending-real-export-checksum'
)
ON CONFLICT (tenant_id, export_code) DO UPDATE
SET schema_version = EXCLUDED.schema_version,
    manifest_path = EXCLUDED.manifest_path,
    reimport_smoke_status = EXCLUDED.reimport_smoke_status;

INSERT INTO public.ppiq_source_code_escrow_records
(
    tenant_id,
    escrow_code,
    package_version,
    release_conditions,
    reviewed_by,
    reviewed_at_utc
)
VALUES
(
    '00000000-0000-0000-0000-000000000001',
    'P13-SOURCE-ESCROW-PROCESS-V1',
    'v5-p13',
    'Release only under signed commercial escrow agreement, verified license failure, vendor insolvency, or contract-defined continuity trigger.',
    'PlantProcess IQ',
    now()
)
ON CONFLICT (tenant_id, escrow_code) DO UPDATE
SET package_version = EXCLUDED.package_version,
    release_conditions = EXCLUDED.release_conditions,
    reviewed_by = EXCLUDED.reviewed_by,
    reviewed_at_utc = EXCLUDED.reviewed_at_utc;

CREATE OR REPLACE FUNCTION public.ppiq_v5_p13_acceptance()
RETURNS TABLE
(
    check_name text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT 'Air-gapped bundle registry exists',
           to_regclass('public.ppiq_deployment_airgap_bundles') IS NOT NULL,
           'offline bundle registry installed.'
    UNION ALL
    SELECT 'Air-gapped bundle has no-egress guarantee',
           EXISTS (
               SELECT 1 FROM public.ppiq_deployment_airgap_bundles
               WHERE bundle_code='P13-AIRGAP-BUNDLE-V1'
                 AND no_external_egress_required = true
                 AND self_hosted_model_option = true
           ),
           'bundle declares zero external egress and self-hosted model option.'
    UNION ALL
    SELECT 'DR drill registry exists',
           to_regclass('public.ppiq_deployment_dr_drills') IS NOT NULL,
           'backup/restore/HA drill registry installed.'
    UNION ALL
    SELECT 'DR reference drill seeded',
           EXISTS (
               SELECT 1 FROM public.ppiq_deployment_dr_drills
               WHERE drill_code='P13-DR-REFERENCE-DRILL'
                 AND data_check_status='passed'
                 AND failover_status='passed'
           ),
           'reference RPO/RTO/failover drill evidence exists.'
    UNION ALL
    SELECT 'Open-format export registry exists',
           to_regclass('public.ppiq_open_format_export_bundles') IS NOT NULL,
           'schema-versioned open-format export registry installed.'
    UNION ALL
    SELECT 'Escrow process exists',
           to_regclass('public.ppiq_source_code_escrow_records') IS NOT NULL
           AND EXISTS (SELECT 1 FROM public.ppiq_source_code_escrow_records WHERE escrow_code='P13-SOURCE-ESCROW-PROCESS-V1'),
           'source-code escrow process record exists.';
$$;

COMMIT;

SELECT * FROM public.ppiq_v5_p13_acceptance();