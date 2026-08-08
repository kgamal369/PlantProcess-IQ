\set ON_ERROR_STOP on

-- ============================================================================
-- PlantProcess IQ
-- 780_t073_widget_result_evidence.sql
--
-- T-073. The persisted evidence snapshot that a WidgetResult citation resolves
-- to. Idempotent and generic: no page, widget, industry or plant vocabulary
-- appears anywhere in this file, because the rows are produced by executing
-- whatever widgets the installation actually defines.
--
-- Why this table exists at all: a Dataset handle proves a table or view exists.
-- It does NOT prove that a particular widget returned a particular number under
-- a particular filter over a particular population. Nothing already persisted
-- carries that, so a numeric assistant claim had no artifact to resolve to.
--
-- The unique constraint is load bearing. The producer derives
-- result_fingerprint deterministically from the normalised result and the query
-- identity, then inserts ON CONFLICT DO NOTHING. An unchanged reindex therefore
-- REUSES the existing evidence id rather than minting a new one, so a citation
-- stays valid and the numeric evidence identity does not drift.
--
-- Tenant safety follows canon.assistant_chunk exactly: an explicit tenant_id
-- column, filtered by predicate at every read. That is the pattern the
-- neighbouring assistant table already uses.
-- ============================================================================

CREATE SCHEMA IF NOT EXISTS canon;

CREATE TABLE IF NOT EXISTS canon.assistant_widget_result (
    id                   uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id            uuid        NOT NULL,
    page_code            text        NOT NULL,
    widget_code          text        NOT NULL,
    widget_definition_id uuid        NULL,
    query_fingerprint    text        NOT NULL,
    generated_at_utc     timestamptz NOT NULL,
    filter_context_json  jsonb       NOT NULL DEFAULT '{}'::jsonb,
    population_count     integer     NOT NULL,
    result_json          jsonb       NOT NULL,
    result_fingerprint   text        NOT NULL,
    created_at_utc       timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_assistant_widget_result UNIQUE (tenant_id, result_fingerprint),
    CONSTRAINT ck_assistant_widget_result_population CHECK (population_count >= 0)
);

CREATE INDEX IF NOT EXISTS ix_assistant_widget_result_tenant_widget
    ON canon.assistant_widget_result (tenant_id, widget_code);

CREATE INDEX IF NOT EXISTS ix_assistant_widget_result_tenant_page
    ON canon.assistant_widget_result (tenant_id, page_code);

CREATE INDEX IF NOT EXISTS ix_assistant_widget_result_generated
    ON canon.assistant_widget_result (tenant_id, generated_at_utc DESC);

COMMENT ON TABLE canon.assistant_widget_result IS
    'T-073: exact widget execution snapshot a WidgetResult provenance handle resolves to. One row per distinct (tenant, result fingerprint).';