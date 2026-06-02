-- ============================================================================
-- 363_p05_kpi_targets_alerts.sql  (T-025 per-tenant targets + threshold alerts)
-- Idempotent.
-- ============================================================================
CREATE TABLE IF NOT EXISTS public.kpi_targets (
    id                 uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id          uuid NOT NULL,
    kpi_code           text NOT NULL,
    target_value       double precision NOT NULL,
    alert_direction    text NOT NULL DEFAULT 'BelowTargetIsBad',  -- AboveTargetIsBad | BelowTargetIsBad
    warning_at         double precision NULL,
    critical_at        double precision NULL,
    unit               text NULL,
    effective_from_utc timestamptz NOT NULL DEFAULT now(),
    effective_to_utc   timestamptz NULL,
    is_active          boolean NOT NULL DEFAULT true,
    created_at_utc     timestamptz NOT NULL DEFAULT now(),
    updated_at_utc     timestamptz NULL
);

-- One active target per (tenant, kpi). Per-tenant isolation is enforced by tenant_id.
CREATE UNIQUE INDEX IF NOT EXISTS ux_kpi_targets_tenant_kpi_active
ON public.kpi_targets(tenant_id, kpi_code) WHERE is_active = true;

CREATE TABLE IF NOT EXISTS public.kpi_evaluation_alerts (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL,
    kpi_code        text NOT NULL,
    evaluated_value double precision NULL,
    target_value    double precision NULL,
    severity        text NOT NULL,                 -- Warning | Critical
    message         text NULL,
    created_at_utc  timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_kpi_evaluation_alerts_tenant_kpi
ON public.kpi_evaluation_alerts(tenant_id, kpi_code, created_at_utc DESC);