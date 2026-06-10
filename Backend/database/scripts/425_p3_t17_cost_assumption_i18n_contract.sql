
-- ============================================================================
-- PlantProcess IQ — P3-T17 / PPIQ-T017 Cost Assumption Management i18n Contract
-- Marker: PPIQ_P3_T17_COST_I18N_CONTRACT
--
-- Idempotent catalog for high-traffic Cost Assumption Management strings.
-- Local laptop: apply to native Windows PostgreSQL if DB proof is required.
-- Server/customer: apply through the environment-specific migration/runbook.
-- ============================================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

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

CREATE OR REPLACE FUNCTION public.ppiq_p3_t17_cost_i18n_status()
RETURNS TABLE
(
    gate_code text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT
        'CostI18nStringKeys',
        COUNT(*) = 3,
        'Expected 3 v5.p3.cost base keys, found ' || COUNT(*)::text
    FROM public.ppiq_i18n_string_keys
    WHERE namespace = 'v5.p3.cost'
      AND string_key IN ('title', 'description', 'save')
      AND screen_code = 'v5-p3-cost-assumptions'
      AND is_high_traffic = true

    UNION ALL

    SELECT
        'CostI18nTranslations',
        COUNT(*) = 9,
        'Expected 9 translations for en/de/ar x title/description/save, found ' || COUNT(*)::text
    FROM public.ppiq_i18n_translations
    WHERE namespace = 'v5.p3.cost'
      AND locale_code IN ('en', 'de', 'ar')
      AND string_key IN ('title', 'description', 'save')

    UNION ALL

    SELECT
        'ArabicRtlTranslationBasis',
        EXISTS
        (
            SELECT 1
            FROM public.ppiq_i18n_translations
            WHERE locale_code = 'ar'
              AND namespace = 'v5.p3.cost'
              AND string_key = 'title'
              AND translated_text ~ '[\u0600-\u06FF]'
        ),
        'Arabic title translation exists and uses Arabic script.';
$$;
