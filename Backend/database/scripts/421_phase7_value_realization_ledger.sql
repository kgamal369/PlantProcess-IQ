
-- ============================================================================
-- 421_phase7_value_realization_ledger.sql
-- PPIQ_REALIZATION_T039_VALUE_REALIZATION_LEDGER_SQL
-- Tracks realized value separately from projected value impact.
-- Baseline-vs-actual tracking is not automatic causal attribution.
-- ============================================================================

CREATE SCHEMA IF NOT EXISTS canon;

CREATE TABLE IF NOT EXISTS canon.value_realization_ledger (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    tracking_code text NOT NULL,
    source_recommendation_id text NULL,
    source_value_impact_id uuid NULL,
    metric_code text NOT NULL,
    currency text NOT NULL DEFAULT 'EUR',
    baseline_value numeric(18,4) NOT NULL,
    actual_value numeric(18,4) NOT NULL,
    improvement_units numeric(18,4) NOT NULL,
    potential_eur_low numeric(18,2) NOT NULL,
    potential_eur_mid numeric(18,2) NOT NULL,
    potential_eur_high numeric(18,2) NOT NULL,
    realized_eur_low numeric(18,2) NOT NULL,
    realized_eur_mid numeric(18,2) NOT NULL,
    realized_eur_high numeric(18,2) NOT NULL,
    capture_rate_mid numeric(18,4) NULL,
    roi_mid numeric(18,4) NULL,
    status text NOT NULL,
    attribution_caveat text NOT NULL,
    evidence jsonb NOT NULL DEFAULT '{}'::jsonb,
    recorded_at_utc timestamptz NOT NULL DEFAULT now(),
    recorded_by text NULL,
    CONSTRAINT ck_value_realization_realized_band CHECK (realized_eur_low <= realized_eur_mid AND realized_eur_mid <= realized_eur_high),
    CONSTRAINT ck_value_realization_potential_band CHECK (potential_eur_low <= potential_eur_mid AND potential_eur_mid <= potential_eur_high),
    CONSTRAINT ck_value_realization_currency CHECK (char_length(currency) = 3)
);

CREATE INDEX IF NOT EXISTS ix_value_realization_tenant_recorded
ON canon.value_realization_ledger(tenant_id, recorded_at_utc DESC);

CREATE INDEX IF NOT EXISTS ix_value_realization_source_value_impact
ON canon.value_realization_ledger(source_value_impact_id);
