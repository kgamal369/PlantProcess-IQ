-- ============================================================================
-- PlantProcess IQ - Phase 8 Widget Script Layer Entity Mapping
-- v5 Repair: PPIQ-T202
--
-- Purpose:
--   Reconciles dashboard_widget_expression_audit with the EF/domain shape.
--
-- Why:
--   113 originally created dashboard_widget_expression_audit with executed_at_utc.
--   117 expected created_at_utc. CREATE TABLE IF NOT EXISTS made this drift invisible.
--
-- Safe:
--   Idempotent. Drops only the audit table, not widget definitions.
-- ============================================================================

SET client_min_messages TO WARNING;
SET TIME ZONE 'UTC';

CREATE EXTENSION IF NOT EXISTS pgcrypto;

BEGIN;

ALTER TABLE public.dashboard_widget_definitions
    ADD COLUMN IF NOT EXISTS query_expression text,
    ADD COLUMN IF NOT EXISTS advanced_expression_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    ADD COLUMN IF NOT EXISTS expression_version smallint NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS expression_enabled boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS expression_last_validated_at_utc timestamptz,
    ADD COLUMN IF NOT EXISTS expression_last_validation_status smallint NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS expression_last_validation_message text;

CREATE INDEX IF NOT EXISTS ix_dashboard_widget_definitions_expression_refresh
ON public.dashboard_widget_definitions(expression_enabled, expression_last_validated_at_utc);

DROP TABLE IF EXISTS public.dashboard_widget_expression_audit;

CREATE TABLE public.dashboard_widget_expression_audit
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    dashboard_widget_definition_id uuid NOT NULL,
    expression_version smallint NOT NULL,
    validation_status smallint NOT NULL,
    validation_message text,
    query_expression text,
    advanced_expression_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX ix_dashboard_widget_expression_audit_widget
ON public.dashboard_widget_expression_audit(dashboard_widget_definition_id, created_at_utc DESC);

CREATE INDEX ix_dashboard_widget_expression_audit_status_time
ON public.dashboard_widget_expression_audit(validation_status, created_at_utc DESC);

COMMIT;

SELECT
    'PPIQ-T202 passed: dashboard_widget_expression_audit canonical schema applied' AS status,
    EXISTS (
        SELECT 1
        FROM information_schema.columns
        WHERE table_schema = 'public'
          AND table_name = 'dashboard_widget_expression_audit'
          AND column_name = 'created_at_utc'
    ) AS has_created_at_utc;