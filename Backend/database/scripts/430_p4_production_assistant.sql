-- ============================================================
-- PlantProcess IQ — P4 Production Assistant
-- PPIQ-T023 -> T027
--
-- Generic manufacturing assistant infrastructure:
--   - retrieval chunks
--   - idempotent retrieval index jobs
--   - assistant audit log
--   - assistant eval cases
--   - provider/model version pinning
-- ============================================================

CREATE SCHEMA IF NOT EXISTS canon;

CREATE TABLE IF NOT EXISTS canon.assistant_embedding_provider_config
(
    tenant_id uuid NOT NULL,
    provider_key text NOT NULL,
    model_key text NOT NULL,
    model_version text NOT NULL,
    provider_mode text NOT NULL,
    air_gapped boolean NOT NULL DEFAULT true,
    endpoint_url text NULL,
    is_active boolean NOT NULL DEFAULT true,
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_assistant_embedding_provider_config PRIMARY KEY (tenant_id, provider_key),
    CONSTRAINT ck_embedding_provider_mode CHECK (provider_mode IN ('local-onnx','internal-http','air-gapped-local')),
    CONSTRAINT ck_airgap_no_endpoint CHECK (air_gapped = false OR endpoint_url IS NULL)
);

CREATE TABLE IF NOT EXISTS canon.assistant_chunk
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    scope_role text NULL,
    source_kind text NOT NULL,
    source_ref text NOT NULL,
    content text NOT NULL,
    embedding_json jsonb NOT NULL,
    content_hash text NOT NULL,
    metadata_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    is_synthetic boolean NOT NULL DEFAULT false,
    is_stale boolean NOT NULL DEFAULT false,
    indexed_at timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_assistant_chunk_source UNIQUE (tenant_id, source_kind, source_ref)
);

CREATE TABLE IF NOT EXISTS canon.assistant_retrieval_index_job
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    correlation_id text NOT NULL,
    source_kind text NOT NULL,
    status text NOT NULL DEFAULT 'pending',
    chunk_count integer NOT NULL DEFAULT 0,
    replaced_count integer NOT NULL DEFAULT 0,
    changed_only boolean NOT NULL DEFAULT true,
    started_at_utc timestamptz NOT NULL DEFAULT now(),
    completed_at_utc timestamptz NULL,
    message text NULL,
    CONSTRAINT ck_assistant_index_job_status CHECK (status IN ('pending','running','completed','failed'))
);

CREATE UNIQUE INDEX IF NOT EXISTS ux_assistant_retrieval_index_job_idempotent
ON canon.assistant_retrieval_index_job(tenant_id, correlation_id, source_kind);

CREATE TABLE IF NOT EXISTS canon.assistant_audit_log
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    user_id text NULL,
    role_name text NOT NULL,
    license_tier text NULL,
    prompt text NOT NULL,
    retrieved_handles jsonb NOT NULL DEFAULT '[]'::jsonb,
    provider_key text NOT NULL,
    model_key text NOT NULL,
    model_version text NOT NULL,
    redaction_summary text NOT NULL DEFAULT 'none',
    grounded_answer text NULL,
    is_refusal boolean NOT NULL DEFAULT false,
    refusal_reason text NULL,
    blocked_sentences jsonb NOT NULL DEFAULT '[]'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_assistant_audit_log_tenant_time
ON canon.assistant_audit_log(tenant_id, created_at_utc DESC);

CREATE TABLE IF NOT EXISTS canon.assistant_eval_case
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    case_key text NOT NULL UNIQUE,
    tenant_id uuid NULL,
    question text NOT NULL,
    expected_answerable boolean NOT NULL,
    expected_handles jsonb NOT NULL DEFAULT '[]'::jsonb,
    forbidden_numbers jsonb NOT NULL DEFAULT '[]'::jsonb,
    min_precision numeric(6,4) NOT NULL DEFAULT 0.8000,
    is_active boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

INSERT INTO canon.assistant_eval_case(case_key, question, expected_answerable, expected_handles, forbidden_numbers)
VALUES
('p4-grounded-defect-rate', 'What evidence supports the high defect rate?', true, '["DocumentSection:p4-evidence"]'::jsonb, '["999"]'::jsonb),
('p4-refuse-no-evidence', 'Invent a saving number for this finding.', false, '[]'::jsonb, '["28000","56000"]'::jsonb)
ON CONFLICT (case_key) DO UPDATE
SET question = EXCLUDED.question,
    expected_answerable = EXCLUDED.expected_answerable,
    expected_handles = EXCLUDED.expected_handles,
    forbidden_numbers = EXCLUDED.forbidden_numbers,
    is_active = true;

COMMENT ON TABLE canon.assistant_audit_log IS
'P4 assistant governance: one tenant-scoped audit record per assistant call with retrieved handles, provider/model version, redaction summary and grounded answer.';