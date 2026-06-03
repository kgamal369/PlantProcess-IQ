\encoding UTF8
SET client_encoding = 'UTF8';

SELECT set_config('app.current_tenant', '00000000-0000-0000-0000-000000000001', false);

INSERT INTO public.ppiq_notification_channels
(
    tenant_id,
    channel_code,
    channel_type,
    display_name,
    destination
)
VALUES
(
    '00000000-0000-0000-0000-000000000001',
    'mock-sales-email',
    'mock_smtp',
    'Mock Sales Email',
    'sales@plantprocessiq.local'
),
(
    '00000000-0000-0000-0000-000000000001',
    'mock-alert-webhook',
    'webhook',
    'Mock Alert Webhook',
    'https://webhook.local/plantprocessiq'
)
ON CONFLICT (tenant_id, channel_code) DO UPDATE
SET channel_type = EXCLUDED.channel_type,
    display_name = EXCLUDED.display_name,
    destination = EXCLUDED.destination,
    is_enabled = true;

INSERT INTO public.ppiq_notification_preferences
(
    tenant_id,
    role_code,
    event_type,
    channel_code,
    is_enabled
)
VALUES
(
    '00000000-0000-0000-0000-000000000001',
    'CommercialAdmin',
    'lead_created',
    'mock-sales-email',
    true
),
(
    '00000000-0000-0000-0000-000000000001',
    'PlantAdmin',
    'high_severity_suggestion',
    'mock-alert-webhook',
    true
)
ON CONFLICT DO NOTHING;

SELECT 'P11 acceptance' AS gate, * FROM public.ppiq_v5_p11_acceptance();
SELECT 'P12 acceptance' AS gate, * FROM public.ppiq_v5_p12_acceptance();

DO $$
DECLARE
    bad_count integer;
BEGIN
    PERFORM set_config('app.current_tenant', '00000000-0000-0000-0000-000000000001', false);

    SELECT count(*) INTO bad_count
    FROM public.ppiq_v5_p11_acceptance()
    WHERE is_green = false;

    IF bad_count > 0 THEN
        RAISE EXCEPTION 'P11 DB acceptance has % failing checks.', bad_count;
    END IF;

    SELECT count(*) INTO bad_count
    FROM public.ppiq_v5_p12_acceptance()
    WHERE is_green = false;

    IF bad_count > 0 THEN
        RAISE EXCEPTION 'P12 DB acceptance has % failing checks.', bad_count;
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM public.ppiq_notification_channels
        WHERE tenant_id = '00000000-0000-0000-0000-000000000001'
          AND channel_code IN ('mock-sales-email', 'mock-alert-webhook')
          AND is_enabled = true
    ) THEN
        RAISE EXCEPTION 'P11 mock notification channels are not visible under tenant context.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM public.ppiq_i18n_locales
        WHERE locale_code = 'ar'
          AND direction = 'rtl'
    ) THEN
        RAISE EXCEPTION 'Arabic RTL locale was not installed.';
    END IF;

    IF NOT EXISTS (
        SELECT 1
        FROM public.ppiq_i18n_translations
        WHERE locale_code = 'ar'
          AND namespace = 'v5.p11p12'
    ) THEN
        RAISE EXCEPTION 'Arabic translations were not installed.';
    END IF;
END $$;