-- ============================================================================
-- PlantProcess IQ - Phase 8 Widget Script Layer Entity Mapping
-- Purpose:
--   Backfill and validate dashboard_widget_definitions expression columns.
--   Mirrors 113_phase1_widget_script_layer.sql and EF entity mapping.
-- Safe:
--   Idempotent.
-- ============================================================================

BEGIN;

ALTER TABLE dashboard_widget_definitions
    ADD COLUMN IF NOT EXISTS query_expression text,
    ADD COLUMN IF NOT EXISTS advanced_expression_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    ADD COLUMN IF NOT EXISTS expression_version smallint NOT NULL DEFAULT 1,
    ADD COLUMN IF NOT EXISTS expression_enabled boolean NOT NULL DEFAULT false,
    ADD COLUMN IF NOT EXISTS expression_last_validated_at_utc timestamptz,
    ADD COLUMN IF NOT EXISTS expression_last_validation_status smallint NOT NULL DEFAULT 0,
    ADD COLUMN IF NOT EXISTS expression_last_validation_message text;

CREATE INDEX IF NOT EXISTS ix_dashboard_widget_definitions_expression_refresh
ON dashboard_widget_definitions(expression_enabled, expression_last_validated_at_utc);

CREATE TABLE IF NOT EXISTS dashboard_widget_expression_audit
(
    id uuid PRIMARY KEY,
    dashboard_widget_definition_id uuid NOT NULL,
    expression_version smallint NOT NULL,
    validation_status smallint NOT NULL,
    validation_message text,
    query_expression text,
    advanced_expression_json jsonb NOT NULL DEFAULT '{}'::jsonb,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_dashboard_widget_expression_audit_widget
ON dashboard_widget_expression_audit(dashboard_widget_definition_id, created_at_utc DESC);

COMMIT;
