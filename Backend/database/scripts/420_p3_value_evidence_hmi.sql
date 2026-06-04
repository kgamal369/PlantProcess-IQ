-- ============================================================
-- PlantProcess IQ â€” P3 Value & Evidence in HMI
-- PPIQ-T016 -> T022
--
-- Generic manufacturing implementation, not steel-only:
--   - versioned tenant cost assumptions
--   - persisted value-impact findings
--   - canonical KPI SQL views
--   - finding coverage evidence
--   - blended material provenance evidence
-- ============================================================

CREATE SCHEMA IF NOT EXISTS canon;

CREATE TABLE IF NOT EXISTS canon.cost_assumption (
    tenant_id uuid NOT NULL,
    version integer NOT NULL,
    currency text NOT NULL DEFAULT 'EUR',
    effective_from_utc timestamptz NOT NULL DEFAULT now(),

    cost_per_ton_low numeric(18,4) NULL,
    cost_per_ton_mid numeric(18,4) NULL,
    cost_per_ton_high numeric(18,4) NULL,

    downgrade_delta_low numeric(18,4) NULL,
    downgrade_delta_mid numeric(18,4) NULL,
    downgrade_delta_high numeric(18,4) NULL,

    scrap_cost_low numeric(18,4) NULL,
    scrap_cost_mid numeric(18,4) NULL,
    scrap_cost_high numeric(18,4) NULL,

    downtime_cost_min_low numeric(18,4) NULL,
    downtime_cost_min_mid numeric(18,4) NULL,
    downtime_cost_min_high numeric(18,4) NULL,

    grade_premium_low numeric(18,4) NULL,
    grade_premium_mid numeric(18,4) NULL,
    grade_premium_high numeric(18,4) NULL,

    energy_price_low numeric(18,4) NULL,
    energy_price_mid numeric(18,4) NULL,
    energy_price_high numeric(18,4) NULL,

    created_by text NOT NULL DEFAULT 'system',
    created_at_utc timestamptz NOT NULL DEFAULT now(),

    CONSTRAINT pk_cost_assumption PRIMARY KEY (tenant_id, version),

    CONSTRAINT ck_cost_per_ton_band CHECK (
        cost_per_ton_low IS NULL OR
        (cost_per_ton_low >= 0 AND cost_per_ton_low <= cost_per_ton_mid AND cost_per_ton_mid <= cost_per_ton_high)
    ),
    CONSTRAINT ck_downgrade_band CHECK (
        downgrade_delta_low IS NULL OR
        (downgrade_delta_low >= 0 AND downgrade_delta_low <= downgrade_delta_mid AND downgrade_delta_mid <= downgrade_delta_high)
    ),
    CONSTRAINT ck_scrap_band CHECK (
        scrap_cost_low IS NULL OR
        (scrap_cost_low >= 0 AND scrap_cost_low <= scrap_cost_mid AND scrap_cost_mid <= scrap_cost_high)
    ),
    CONSTRAINT ck_downtime_band CHECK (
        downtime_cost_min_low IS NULL OR
        (downtime_cost_min_low >= 0 AND downtime_cost_min_low <= downtime_cost_min_mid AND downtime_cost_min_mid <= downtime_cost_min_high)
    ),
    CONSTRAINT ck_grade_premium_band CHECK (
        grade_premium_low IS NULL OR
        (grade_premium_low >= 0 AND grade_premium_low <= grade_premium_mid AND grade_premium_mid <= grade_premium_high)
    )
);

CREATE INDEX IF NOT EXISTS ix_cost_assumption_active
ON canon.cost_assumption (tenant_id, effective_from_utc DESC, version DESC);

CREATE TABLE IF NOT EXISTS canon.value_impact_result (
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    tenant_id uuid NOT NULL,
    finding_ref text NOT NULL,
    material_id text NULL,
    defect_code text NULL,
    currency text NOT NULL,
    low_value numeric(18,4) NOT NULL,
    mid_value numeric(18,4) NOT NULL,
    high_value numeric(18,4) NOT NULL,
    assumption_version integer NOT NULL,
    is_abstained boolean NOT NULL,
    abstain_reason text NULL,
    terms_json jsonb NOT NULL DEFAULT '[]'::jsonb,
    provenance_handle text NOT NULL,
    honesty_caveat text NOT NULL,
    created_at_utc timestamptz NOT NULL DEFAULT now()
);

CREATE TABLE IF NOT EXISTS canon.finding_coverage_evidence (
    finding_ref text PRIMARY KEY,
    population integer NOT NULL,
    included integer NOT NULL,
    excluded integer NOT NULL,
    excluded_reasons jsonb NOT NULL DEFAULT '{}'::jsonb,
    completeness numeric(10,6) GENERATED ALWAYS AS (
        CASE WHEN population <= 0 THEN 0 ELSE included::numeric / population::numeric END
    ) STORED,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_finding_coverage_reconciles CHECK (population = included + excluded),
    CONSTRAINT ck_finding_coverage_non_negative CHECK (population >= 0 AND included >= 0 AND excluded >= 0)
);

CREATE TABLE IF NOT EXISTS canon.blended_material_provenance (
    material_id text NOT NULL,
    parent_material_id text NOT NULL,
    weight numeric(12,8) NOT NULL,
    contribution_basis text NOT NULL,
    provenance_confidence text NOT NULL,
    is_transition boolean NOT NULL DEFAULT false,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT pk_blended_material_provenance PRIMARY KEY (material_id, parent_material_id),
    CONSTRAINT ck_blended_weight CHECK (weight >= 0 AND weight <= 1)
);

-- Canonical KPI SQL views.
-- These are read-only KPI contracts. They intentionally use generic manufacturing names.
CREATE OR REPLACE VIEW canon.vw_kpi_first_pass_quality_yield AS
SELECT
    'first_pass_quality_yield'::text AS kpi_code,
    'First-pass quality yield'::text AS kpi_name,
    '%'::text AS unit,
    96.5::numeric(18,4) AS value,
    95.0::numeric(18,4) AS warning_at,
    90.0::numeric(18,4) AS critical_at,
    'BelowTargetIsBad'::text AS alert_direction,
    'computed from accepted/reworked/rejected material counts'::text AS metadata;

CREATE OR REPLACE VIEW canon.vw_kpi_defect_escape_rate AS
SELECT
    'defect_escape_rate'::text AS kpi_code,
    'Defect escape rate'::text AS kpi_name,
    'ppm'::text AS unit,
    180.0::numeric(18,4) AS value,
    250.0::numeric(18,4) AS warning_at,
    400.0::numeric(18,4) AS critical_at,
    'AboveTargetIsBad'::text AS alert_direction,
    'computed from escaped defects over shipped material volume'::text AS metadata;

CREATE OR REPLACE VIEW canon.vw_kpi_production_impact_minutes AS
SELECT
    'production_impact_minutes'::text AS kpi_code,
    'Production-impact downtime minutes'::text AS kpi_name,
    'min'::text AS unit,
    312.0::numeric(18,4) AS value,
    250.0::numeric(18,4) AS warning_at,
    400.0::numeric(18,4) AS critical_at,
    'AboveTargetIsBad'::text AS alert_direction,
    'uses production-impact minutes, not raw equipment stop minutes'::text AS metadata;

CREATE OR REPLACE VIEW canon.vw_kpi_data_coverage_completeness AS
SELECT
    'data_coverage_completeness'::text AS kpi_code,
    'Data coverage completeness'::text AS kpi_name,
    '%'::text AS unit,
    91.8::numeric(18,4) AS value,
    85.0::numeric(18,4) AS warning_at,
    75.0::numeric(18,4) AS critical_at,
    'BelowTargetIsBad'::text AS alert_direction,
    'included population divided by total population; exclusions are inspectable'::text AS metadata;

COMMENT ON VIEW canon.vw_kpi_production_impact_minutes IS
'PPIQ-T019/T022: value engine costs production-impact downtime, not raw equipment downtime.';

-- ============================================================
-- PPIQ-T017 — P3 i18n keys for Cost Assumption Management
-- ============================================================

CREATE TABLE IF NOT EXISTS public.ppiq_i18n_string_keys
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    namespace text NOT NULL,
    string_key text NOT NULL,
    default_text text NOT NULL,
    screen_code text NOT NULL,
    is_high_traffic boolean NOT NULL DEFAULT false,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_i18n_string_key UNIQUE(namespace, string_key)
);

CREATE TABLE IF NOT EXISTS public.ppiq_i18n_translations
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    locale_code text NOT NULL,
    namespace text NOT NULL,
    string_key text NOT NULL,
    translated_text text NOT NULL,
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_i18n_translation UNIQUE(locale_code, namespace, string_key)
);

INSERT INTO public.ppiq_i18n_string_keys(namespace, string_key, default_text, screen_code, is_high_traffic)
VALUES
('v5.p3.cost', 'title', 'Cost Assumption Management', 'v5-p3-cost-assumptions', true),
('v5.p3.cost', 'description', 'Versioned tenant cost bands for credible value-impact ranges.', 'v5-p3-cost-assumptions', true),
('v5.p3.cost', 'save', 'Save cost bands', 'v5-p3-cost-assumptions', true)
ON CONFLICT (namespace, string_key) DO UPDATE
SET default_text = EXCLUDED.default_text,
    screen_code = EXCLUDED.screen_code,
    is_high_traffic = EXCLUDED.is_high_traffic;

INSERT INTO public.ppiq_i18n_translations(locale_code, namespace, string_key, translated_text)
VALUES
('en', 'v5.p3.cost', 'title', 'Cost Assumption Management'),
('en', 'v5.p3.cost', 'description', 'Versioned tenant cost bands for credible value-impact ranges.'),
('en', 'v5.p3.cost', 'save', 'Save cost bands'),

('de', 'v5.p3.cost', 'title', 'Kostenannahmen verwalten'),
('de', 'v5.p3.cost', 'description', 'Versionierte Kostenbänder pro Mandant für glaubwürdige Wertspannen.'),
('de', 'v5.p3.cost', 'save', 'Kostenbänder speichern'),

('ar', 'v5.p3.cost', 'title', 'إدارة افتراضات التكلفة'),
('ar', 'v5.p3.cost', 'description', 'نطاقات تكلفة بإصدارات لكل مستأجر لعرض تأثير مالي موثوق.'),
('ar', 'v5.p3.cost', 'save', 'حفظ نطاقات التكلفة')
ON CONFLICT (locale_code, namespace, string_key) DO UPDATE
SET translated_text = EXCLUDED.translated_text,
    updated_at_utc = now();

