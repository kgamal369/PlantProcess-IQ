-- 730_alert_rules_and_plant_data_log.sql
-- M1-06 4th low-code UI (plant-data-log / threshold alerting), v0.
-- Generic and industry-agnostic: no demo/industry-specific names. Rule-1 clean.
SET client_min_messages TO WARNING;

CREATE TABLE IF NOT EXISTS public.alert_rules
(
    id             uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    rule_name      text NOT NULL,
    parameter_code text NOT NULL,
    comparator     text NOT NULL,                 -- one of >, >=, <, <=, =
    limit_value    double precision NOT NULL,
    severity       text NOT NULL DEFAULT 'Warning',
    is_active      boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_alert_rules_comparator CHECK (comparator IN ('>', '>=', '<', '<=', '='))
);

CREATE TABLE IF NOT EXISTS public.plant_data_log
(
    id                       uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    alert_rule_id            uuid NOT NULL REFERENCES public.alert_rules(id) ON DELETE CASCADE,
    parameter_observation_id uuid NULL,
    material_code            text NULL,
    parameter_code           text NOT NULL,
    observed_value           double precision NULL,
    comparator               text NOT NULL,
    limit_value              double precision NOT NULL,
    severity                 text NOT NULL,
    message                  text NOT NULL,
    logged_at_utc            timestamptz NOT NULL DEFAULT now()
);

CREATE INDEX IF NOT EXISTS ix_plant_data_log_rule
    ON public.plant_data_log (alert_rule_id, logged_at_utc DESC);

-- Idempotency: one log row per (rule, observation). Makes re-evaluation safe.
CREATE UNIQUE INDEX IF NOT EXISTS ux_plant_data_log_rule_obs
    ON public.plant_data_log (alert_rule_id, parameter_observation_id)
    WHERE parameter_observation_id IS NOT NULL;

-- Evaluator: scan parameter_observations, log breaches for every active rule.
-- Returns the number of NEW log rows written this run.
CREATE OR REPLACE FUNCTION public.ppiq_evaluate_alert_rules()
RETURNS integer
LANGUAGE plpgsql
AS $$
DECLARE
    v_inserted integer;
BEGIN
    WITH ins AS (
        INSERT INTO public.plant_data_log
            (alert_rule_id, parameter_observation_id, material_code, parameter_code,
             observed_value, comparator, limit_value, severity, message)
        SELECT
            r.id,
            o.id,
            mu.material_code,
            pd.parameter_code,
            o.numeric_value,
            r.comparator,
            r.limit_value,
            r.severity,
            pd.parameter_code || ' ' || r.comparator || ' ' || r.limit_value::text
                || ' (observed ' || coalesce(o.numeric_value::text, 'null') || ')'
        FROM public.alert_rules r
        JOIN public.parameter_definitions pd
            ON pd.parameter_code = r.parameter_code
        JOIN public.parameter_observations o
            ON o.parameter_definition_id = pd.id
           AND o.numeric_value IS NOT NULL
        LEFT JOIN public.material_units mu
            ON mu.id = o.material_unit_id
        WHERE r.is_active = true
          AND (
               (r.comparator = '>'  AND o.numeric_value >  r.limit_value) OR
               (r.comparator = '>=' AND o.numeric_value >= r.limit_value) OR
               (r.comparator = '<'  AND o.numeric_value <  r.limit_value) OR
               (r.comparator = '<=' AND o.numeric_value <= r.limit_value) OR
               (r.comparator = '='  AND o.numeric_value =  r.limit_value)
          )
        ON CONFLICT (alert_rule_id, parameter_observation_id) DO NOTHING
        RETURNING 1
    )
    SELECT count(*)::integer INTO v_inserted FROM ins;
    RETURN v_inserted;
END;
$$;

SELECT 'M1-06 alerting schema installed' AS status;