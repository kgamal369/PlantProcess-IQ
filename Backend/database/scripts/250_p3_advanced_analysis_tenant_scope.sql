-- =============================================================================
-- 250_p3_advanced_analysis_tenant_scope.sql   (P3-05)
-- Tenant-scope the correlation run + result tables for the .NET advanced engine.
-- Idempotent.
-- =============================================================================
ALTER TABLE public.ml_correlation_compute_runs ADD COLUMN IF NOT EXISTS tenant_id uuid NULL;
ALTER TABLE public.ml_correlation_results_v2  ADD COLUMN IF NOT EXISTS tenant_id uuid NULL;

CREATE INDEX IF NOT EXISTS ix_ml_correlation_compute_runs_tenant
    ON public.ml_correlation_compute_runs(tenant_id, target_outcome_key);

CREATE INDEX IF NOT EXISTS ix_ml_correlation_results_v2_tenant
    ON public.ml_correlation_results_v2(tenant_id, outcome_key, effect_size DESC NULLS LAST);

SELECT 'P3-05 applied: tenant_id added to correlation run + result tables' AS status;
