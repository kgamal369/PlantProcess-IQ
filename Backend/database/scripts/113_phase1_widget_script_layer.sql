-- ============================================================================
-- PlantProcess IQ
-- Phase 1 Widget Script Layer DB Foundation
--
-- Task:
--   PPIQ-WF-012
--
-- Purpose:
--   Adds optional expression/script storage to dashboard widget definitions
--   without breaking existing widgets. The backend still executes through
--   the safe metadata-backed widget query service, never arbitrary SQL.
--
-- Safe:
--   Idempotent. Can run locally and later on server DB.
-- ============================================================================

SET client_min_messages TO WARNING;
SET TIME ZONE 'UTC';

DO $$
BEGIN
    IF to_regclass('public.dashboard_widget_definitions') IS NOT NULL THEN
        ALTER TABLE public.dashboard_widget_definitions
            ADD COLUMN IF NOT EXISTS query_expression text;

        ALTER TABLE public.dashboard_widget_definitions
            ADD COLUMN IF NOT EXISTS advanced_expression_json jsonb NOT NULL DEFAULT '{}'::jsonb;

        ALTER TABLE public.dashboard_widget_definitions
            ADD COLUMN IF NOT EXISTS expression_version text NOT NULL DEFAULT 'phase1.v1';

        ALTER TABLE public.dashboard_widget_definitions
            ADD COLUMN IF NOT EXISTS expression_enabled boolean NOT NULL DEFAULT false;

        ALTER TABLE public.dashboard_widget_definitions
            ADD COLUMN IF NOT EXISTS expression_last_validated_at_utc timestamptz;

        ALTER TABLE public.dashboard_widget_definitions
            ADD COLUMN IF NOT EXISTS expression_last_validation_status text;

        ALTER TABLE public.dashboard_widget_definitions
            ADD COLUMN IF NOT EXISTS expression_last_validation_message text;

        CREATE INDEX IF NOT EXISTS ix_dashboard_widget_definitions_expression_enabled
            ON public.dashboard_widget_definitions(expression_enabled);

        CREATE INDEX IF NOT EXISTS ix_dashboard_widget_definitions_advanced_expression_json
            ON public.dashboard_widget_definitions
            USING gin(advanced_expression_json);
    END IF;
END $$;

DO $$
BEGIN
    IF to_regclass('public.dashboard_widget_expression_audit') IS NULL THEN
        CREATE TABLE public.dashboard_widget_expression_audit
        (
            id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
            dashboard_widget_definition_id uuid NULL,
            expression_text text NOT NULL,
            expression_json jsonb NOT NULL DEFAULT '{}'::jsonb,
            validation_status text NOT NULL,
            validation_message text NULL,
            executed_by text NULL,
            executed_at_utc timestamptz NOT NULL DEFAULT now(),
            source_system text NULL,
            source_record_id text NULL
        );

        CREATE INDEX ix_dashboard_widget_expression_audit_widget
            ON public.dashboard_widget_expression_audit(dashboard_widget_definition_id);

        CREATE INDEX ix_dashboard_widget_expression_audit_executed_at
            ON public.dashboard_widget_expression_audit(executed_at_utc DESC);
    END IF;
END $$;

SELECT 'Phase 1 widget script layer DB foundation applied' AS status;