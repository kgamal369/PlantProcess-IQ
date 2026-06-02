-- PPIQ Phase 4 — cost-assumption store (versioned, tenant-scoped, banded) + before/after audit.
-- Idempotent; mirrors the canon.* convention. Apply like 320_phase2_canonical_completion.sql.
CREATE SCHEMA IF NOT EXISTS canon;

CREATE TABLE IF NOT EXISTS canon.cost_assumption (
    id                        uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id                 uuid NOT NULL,
    version                   integer NOT NULL,
    currency                  text NOT NULL DEFAULT 'EUR',
    cost_per_ton_low          numeric(18,4) NULL,
    cost_per_ton_mid          numeric(18,4) NULL,
    cost_per_ton_high         numeric(18,4) NULL,
    downgrade_delta_low       numeric(18,4) NULL,
    downgrade_delta_mid       numeric(18,4) NULL,
    downgrade_delta_high      numeric(18,4) NULL,
    scrap_cost_low            numeric(18,4) NULL,
    scrap_cost_mid            numeric(18,4) NULL,
    scrap_cost_high           numeric(18,4) NULL,
    downtime_cost_min_low     numeric(18,4) NULL,
    downtime_cost_min_mid     numeric(18,4) NULL,
    downtime_cost_min_high    numeric(18,4) NULL,
    grade_premium_low         numeric(18,4) NULL,
    grade_premium_mid         numeric(18,4) NULL,
    grade_premium_high        numeric(18,4) NULL,
    energy_price_low          numeric(18,4) NULL,
    energy_price_mid          numeric(18,4) NULL,
    energy_price_high         numeric(18,4) NULL,
    created_by                text NOT NULL DEFAULT 'system',
    created_at                timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT uq_cost_assumption_tenant_version UNIQUE (tenant_id, version),
    CONSTRAINT ck_cost_assumption_ccy CHECK (char_length(currency) = 3)
);

CREATE TABLE IF NOT EXISTS canon.cost_assumption_audit (
    id              uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id       uuid NOT NULL,
    from_version    integer NULL,
    to_version      integer NOT NULL,
    actor           text NOT NULL DEFAULT 'system',
    before_json     jsonb NOT NULL DEFAULT '{}'::jsonb,
    after_json      jsonb NOT NULL DEFAULT '{}'::jsonb,
    changed_at      timestamptz NOT NULL DEFAULT now()
);

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='canon' AND indexname='ix_cost_assumption_tenant') THEN
        EXECUTE 'CREATE INDEX ix_cost_assumption_tenant ON canon.cost_assumption (tenant_id, version DESC)';
    END IF;
    IF NOT EXISTS (SELECT 1 FROM pg_indexes WHERE schemaname='canon' AND indexname='ix_cost_assumption_audit_tenant') THEN
        EXECUTE 'CREATE INDEX ix_cost_assumption_audit_tenant ON canon.cost_assumption_audit (tenant_id, changed_at DESC)';
    END IF;
END $$;

DO $$
BEGIN
    IF NOT EXISTS (SELECT 1 FROM information_schema.tables WHERE table_schema='canon' AND table_name='cost_assumption') THEN
        RAISE EXCEPTION 'canon.cost_assumption missing after apply';
    END IF;
END $$;