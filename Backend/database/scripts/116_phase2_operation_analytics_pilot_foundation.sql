-- ============================================================================
-- PlantProcess IQ
-- Phase 2 Step 3/4/5 Foundation
--
-- Tasks:
--   PPIQ-WF-008  Cross-source join view builder polish
--   PPIQ-WF-009  KPI definition to parameter binding
--   PPIQ-HARD-026 Long operation progress
--   PPIQ-WF-015  Save-as-inspection-job
--   PPIQ-WF-018  Honest ML job lifecycle state machine
--   PPIQ-WF-023  Tenant isolation decision lock
--   PPIQ-WF-024  Audit log query readiness
--   PPIQ-DEMO-017 Demo language truth audit
-- ============================================================================

SET client_min_messages TO WARNING;
SET TIME ZONE 'UTC';

BEGIN;

-- ============================================================================
-- KPI to Parameter binding
-- ============================================================================

CREATE TABLE IF NOT EXISTS public.kpi_parameter_bindings
(
    id uuid PRIMARY KEY,
    kpi_code text NOT NULL,
    kpi_name text NOT NULL,
    parameter_definition_id uuid NOT NULL REFERENCES public.parameter_definitions(id),
    aggregation_method text NOT NULL DEFAULT 'Average',
    unit_of_measure text NULL,
    filter_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    is_active boolean NOT NULL DEFAULT true,
    is_synthetic boolean NOT NULL DEFAULT false,
    source_system text NULL,
    source_record_id text NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NULL,
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at_utc timestamptz NULL,
    deleted_reason text NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_kpi_parameter_bindings_kpi_code_active
ON public.kpi_parameter_bindings (lower(kpi_code))
WHERE is_deleted = false;

CREATE INDEX IF NOT EXISTS ix_kpi_parameter_bindings_parameter_definition_id
ON public.kpi_parameter_bindings (parameter_definition_id)
WHERE is_deleted = false;

-- ============================================================================
-- Long operation progress
-- ============================================================================

CREATE TABLE IF NOT EXISTS public.long_operation_progress
(
    id uuid PRIMARY KEY,
    operation_code text NOT NULL,
    operation_type text NOT NULL,
    operation_name text NOT NULL,
    status text NOT NULL DEFAULT 'Queued',
    percent_complete numeric(5,2) NOT NULL DEFAULT 0,
    current_step text NULL,
    total_steps integer NULL,
    completed_steps integer NULL,
    message text NULL,
    started_at_utc timestamptz NOT NULL DEFAULT now(),
    completed_at_utc timestamptz NULL,
    failed_at_utc timestamptz NULL,
    failure_reason text NULL,
    correlation_id text NULL,
    requested_by text NULL,
    metadata_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NULL
);

CREATE INDEX IF NOT EXISTS ix_long_operation_progress_status
ON public.long_operation_progress (status, started_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_long_operation_progress_code
ON public.long_operation_progress (operation_code);

-- ============================================================================
-- Inspection jobs saved from investigation/correlation
-- ============================================================================

CREATE TABLE IF NOT EXISTS public.inspection_jobs
(
    id uuid PRIMARY KEY,
    inspection_job_code text NOT NULL,
    inspection_job_name text NOT NULL,
    inspection_type text NOT NULL,
    source_correlation_run_id uuid NULL,
    parameter_code text NULL,
    defect_type text NULL,
    site_id uuid NULL,
    equipment_id uuid NULL,
    rule_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    schedule_expression text NOT NULL DEFAULT 'Manual',
    is_enabled boolean NOT NULL DEFAULT true,
    honest_state text NOT NULL DEFAULT 'RuleBasedMonitoring',
    last_run_at_utc timestamptz NULL,
    last_run_status text NULL,
    last_result_json jsonb NULL,
    description text NULL,
    is_synthetic boolean NOT NULL DEFAULT false,
    source_system text NULL,
    source_record_id text NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NULL,
    is_deleted boolean NOT NULL DEFAULT false,
    deleted_at_utc timestamptz NULL,
    deleted_reason text NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_inspection_jobs_code_active
ON public.inspection_jobs (lower(inspection_job_code))
WHERE is_deleted = false;

CREATE INDEX IF NOT EXISTS ix_inspection_jobs_state_enabled
ON public.inspection_jobs (honest_state, is_enabled)
WHERE is_deleted = false;

-- ============================================================================
-- Honest ML lifecycle state
-- ============================================================================

CREATE TABLE IF NOT EXISTS public.ml_job_lifecycle_states
(
    id uuid PRIMARY KEY,
    ml_job_code text NOT NULL,
    ml_job_name text NOT NULL,
    state text NOT NULL,
    state_reason text NOT NULL,
    readiness_score numeric(5,2) NULL,
    label_count integer NULL,
    feature_count integer NULL,
    last_evaluated_at_utc timestamptz NULL,
    next_recommended_action text NULL,
    no_production_prediction boolean NOT NULL DEFAULT true,
    metadata_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    is_active boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NULL,
    is_deleted boolean NOT NULL DEFAULT false
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_ml_job_lifecycle_states_code_active
ON public.ml_job_lifecycle_states (lower(ml_job_code))
WHERE is_deleted = false;

-- ============================================================================
-- Tenant isolation decision lock
-- ============================================================================

CREATE TABLE IF NOT EXISTS public.tenant_isolation_decisions
(
    id uuid PRIMARY KEY,
    decision_code text NOT NULL,
    decision_title text NOT NULL,
    selected_model text NOT NULL,
    decision_status text NOT NULL DEFAULT 'LockedForPilot',
    decision_reason text NOT NULL,
    allowed_scope text NOT NULL,
    blocked_scope text NOT NULL,
    valid_from_utc timestamptz NOT NULL DEFAULT now(),
    valid_until_utc timestamptz NULL,
    approved_by text NULL,
    evidence_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NULL,
    is_deleted boolean NOT NULL DEFAULT false
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_tenant_isolation_decisions_code_active
ON public.tenant_isolation_decisions (lower(decision_code))
WHERE is_deleted = false;

-- ============================================================================
-- Demo language truth rules
-- ============================================================================

CREATE TABLE IF NOT EXISTS public.demo_language_truth_rules
(
    id uuid PRIMARY KEY,
    rule_code text NOT NULL,
    forbidden_phrase text NOT NULL,
    safer_replacement text NOT NULL,
    severity text NOT NULL DEFAULT 'Critical',
    rationale text NOT NULL,
    is_active boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    updated_at_utc timestamptz NULL
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_demo_language_truth_rules_code
ON public.demo_language_truth_rules (lower(rule_code));

INSERT INTO public.tenant_isolation_decisions
(
    id,
    decision_code,
    decision_title,
    selected_model,
    decision_status,
    decision_reason,
    allowed_scope,
    blocked_scope,
    approved_by,
    evidence_json
)
SELECT
    '00000000-0000-0000-0000-000000020023'::uuid,
    'PPIQ-WF-023-SINGLE-TENANT-PILOT',
    'Single-tenant pilot deployment lock',
    'SingleTenantPerPilot',
    'LockedForPilot',
    'Early paid pilots must avoid multi-tenant complexity, customer data leakage risk, and premature SaaS assumptions.',
    'One customer/pilot per isolated deployment/database/environment.',
    'No shared customer database, no shared source data, no cross-customer analytics, no multi-tenant billing logic in pilot stage.',
    'PlantProcessIQ.ProductOwner',
    '{"task":"PPIQ-WF-023","phase":"Phase2","decision":"single-tenant until pilot evidence proves multi-tenant need"}'::jsonb
WHERE NOT EXISTS (
    SELECT 1
    FROM public.tenant_isolation_decisions
    WHERE lower(decision_code) = lower('PPIQ-WF-023-SINGLE-TENANT-PILOT')
      AND is_deleted = false
);

INSERT INTO public.demo_language_truth_rules
(
    id,
    rule_code,
    forbidden_phrase,
    safer_replacement,
    severity,
    rationale
)
VALUES
(
    '00000000-0000-0000-0000-000000017001'::uuid,
    'NO_GUARANTEED_ROOT_CAUSE',
    'guaranteed root cause',
    'suspected contributor based on available evidence',
    'Critical',
    'Phase 2 uses rule-based/correlation evidence, not guaranteed causal proof.'
),
(
    '00000000-0000-0000-0000-000000017002'::uuid,
    'NO_PRODUCTION_AI_CLAIM',
    'AI predicts defects in production',
    'rule-based risk scoring and ML-readiness workflow are available; trained production ML requires validated labels',
    'Critical',
    'Avoids overclaiming production ML capability.'
),
(
    '00000000-0000-0000-0000-000000017003'::uuid,
    'NO_MES_REPLACEMENT',
    'replaces MES',
    'adds an intelligence layer above MES/L2/SCADA/BI',
    'Critical',
    'Product positioning must stay non-replacement and read-only.'
)
ON CONFLICT (lower(rule_code)) DO NOTHING;

COMMIT;

SELECT 'Phase 2 operation analytics pilot foundation applied' AS status;

