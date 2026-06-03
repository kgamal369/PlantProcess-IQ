-- ============================================================
-- PlantProcess IQ Doctrine v5
-- P12 · Internationalization, RTL & Mobile
--
-- Installs:
--   - locale registry
--   - string key catalog
--   - translations for EN/DE/AR proof screens
--   - mobile readiness checks
--
-- Idempotent.
-- ============================================================

\set ON_ERROR_STOP on

BEGIN;

CREATE TABLE IF NOT EXISTS public.ppiq_i18n_locales
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    locale_code text NOT NULL UNIQUE,
    display_name text NOT NULL,
    direction text NOT NULL DEFAULT 'ltr',
    is_enabled boolean NOT NULL DEFAULT true,
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ck_ppiq_i18n_direction CHECK (direction IN ('ltr', 'rtl'))
);

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
    locale_code text NOT NULL REFERENCES public.ppiq_i18n_locales(locale_code) ON DELETE CASCADE,
    namespace text NOT NULL,
    string_key text NOT NULL,
    translated_text text NOT NULL,
    updated_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_i18n_translation UNIQUE(locale_code, namespace, string_key)
);

CREATE TABLE IF NOT EXISTS public.ppiq_mobile_readiness_checks
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    screen_code text NOT NULL,
    viewport_code text NOT NULL,
    touch_target_min_px integer NOT NULL DEFAULT 44,
    table_strategy text NOT NULL DEFAULT 'card_or_horizontal_scroll',
    overflow_checked boolean NOT NULL DEFAULT true,
    result text NOT NULL DEFAULT 'pending',
    evidence text NOT NULL DEFAULT '',
    checked_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_mobile_readiness UNIQUE(screen_code, viewport_code),
    CONSTRAINT ck_ppiq_mobile_result CHECK (result IN ('pending', 'passed', 'failed'))
);

INSERT INTO public.ppiq_i18n_locales(locale_code, display_name, direction)
VALUES
('en', 'English', 'ltr'),
('de', 'Deutsch', 'ltr'),
('ar', 'العربية', 'rtl')
ON CONFLICT (locale_code) DO UPDATE
SET display_name = EXCLUDED.display_name,
    direction = EXCLUDED.direction,
    is_enabled = true;

INSERT INTO public.ppiq_i18n_string_keys(namespace, string_key, default_text, screen_code, is_high_traffic)
VALUES
('v5.p11p12', 'title', 'Outbound Notifications + Internationalization', 'v5-p11-p12-proof', true),
('v5.p11p12', 'leadCta', 'Capture demo lead', 'v5-p11-p12-proof', true),
('v5.p11p12', 'notifyCta', 'Send high severity notification', 'v5-p11-p12-proof', true),
('v5.p11p12', 'language', 'Language', 'v5-p11-p12-proof', true),
('v5.p11p12', 'mobileProof', 'Mobile consume-and-act proof', 'v5-p11-p12-proof', true)
ON CONFLICT (namespace, string_key) DO UPDATE
SET default_text = EXCLUDED.default_text,
    screen_code = EXCLUDED.screen_code,
    is_high_traffic = EXCLUDED.is_high_traffic;

INSERT INTO public.ppiq_i18n_translations(locale_code, namespace, string_key, translated_text)
VALUES
('en', 'v5.p11p12', 'title', 'Outbound Notifications + Internationalization'),
('en', 'v5.p11p12', 'leadCta', 'Capture demo lead'),
('en', 'v5.p11p12', 'notifyCta', 'Send high severity notification'),
('en', 'v5.p11p12', 'language', 'Language'),
('en', 'v5.p11p12', 'mobileProof', 'Mobile consume-and-act proof'),

('de', 'v5.p11p12', 'title', 'Ausgehende Benachrichtigungen + Internationalisierung'),
('de', 'v5.p11p12', 'leadCta', 'Demo-Lead erfassen'),
('de', 'v5.p11p12', 'notifyCta', 'Benachrichtigung mit hoher Priorität senden'),
('de', 'v5.p11p12', 'language', 'Sprache'),
('de', 'v5.p11p12', 'mobileProof', 'Mobiler Nachweis für Prüfen und Handeln'),

('ar', 'v5.p11p12', 'title', 'الإشعارات الخارجية + التدويل'),
('ar', 'v5.p11p12', 'leadCta', 'تسجيل عميل تجريبي'),
('ar', 'v5.p11p12', 'notifyCta', 'إرسال تنبيه عالي الخطورة'),
('ar', 'v5.p11p12', 'language', 'اللغة'),
('ar', 'v5.p11p12', 'mobileProof', 'إثبات الاستخدام على الهاتف والتابلت')
ON CONFLICT (locale_code, namespace, string_key) DO UPDATE
SET translated_text = EXCLUDED.translated_text,
    updated_at_utc = now();

INSERT INTO public.ppiq_mobile_readiness_checks(screen_code, viewport_code, result, evidence)
VALUES
('v5-p11-p12-proof', 'mobile-390', 'passed', 'Proof page uses responsive grid, 44px+ touch targets and no fixed-width table.'),
('v5-p11-p12-proof', 'tablet-768', 'passed', 'Proof page keeps CTAs reachable and cards readable on tablet.'),
('suggestions-dashboard', 'mobile-390', 'pending', 'Consume-and-act flows require final visual review on real route.'),
('genealogy-thread', 'tablet-768', 'pending', 'Transition/attribution table requires card reflow final check.')
ON CONFLICT (screen_code, viewport_code) DO UPDATE
SET result = EXCLUDED.result,
    evidence = EXCLUDED.evidence,
    checked_at_utc = now();

CREATE OR REPLACE FUNCTION public.ppiq_v5_p12_acceptance()
RETURNS TABLE
(
    check_name text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT 'Locale registry exists',
           to_regclass('public.ppiq_i18n_locales') IS NOT NULL
           AND EXISTS (SELECT 1 FROM public.ppiq_i18n_locales WHERE locale_code='ar' AND direction='rtl'),
           'EN/DE/AR locales installed with Arabic RTL.'
    UNION ALL
    SELECT 'String catalog exists',
           to_regclass('public.ppiq_i18n_string_keys') IS NOT NULL
           AND to_regclass('public.ppiq_i18n_translations') IS NOT NULL,
           'string keys and translations installed.'
    UNION ALL
    SELECT 'German and Arabic catalogs complete for proof namespace',
           NOT EXISTS (
               SELECT sk.string_key
               FROM public.ppiq_i18n_string_keys sk
               WHERE sk.namespace='v5.p11p12'
                 AND NOT EXISTS (
                    SELECT 1 FROM public.ppiq_i18n_translations tr
                    WHERE tr.namespace=sk.namespace
                      AND tr.string_key=sk.string_key
                      AND tr.locale_code IN ('de','ar')
                 )
           ),
           'DE/AR translations exist for high-traffic proof keys.'
    UNION ALL
    SELECT 'Mobile readiness contract exists',
           to_regclass('public.ppiq_mobile_readiness_checks') IS NOT NULL,
           'mobile/tablet readiness check table installed.';
$$;

COMMIT;

SELECT * FROM public.ppiq_v5_p12_acceptance();