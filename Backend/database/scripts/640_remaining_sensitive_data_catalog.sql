-- ============================================================
-- PlantProcess IQ Remaining Pack A
-- Sensitive data catalog for P02-T03 hardening
-- ============================================================

CREATE EXTENSION IF NOT EXISTS pgcrypto;

CREATE TABLE IF NOT EXISTS public.ppiq_sensitive_data_catalog
(
    id uuid PRIMARY KEY DEFAULT gen_random_uuid(),
    table_name text NOT NULL,
    column_name text NOT NULL,
    sensitivity_class text NOT NULL,
    protection_required text NOT NULL,
    current_status text NOT NULL DEFAULT 'cataloged',
    owner_module text NOT NULL,
    notes text NOT NULL DEFAULT '',
    created_at_utc timestamptz NOT NULL DEFAULT now(),
    CONSTRAINT ux_ppiq_sensitive_data_catalog UNIQUE(table_name, column_name),
    CONSTRAINT ck_ppiq_sensitive_class CHECK(sensitivity_class IN ('secret','personal','license','session','audit','commercial')),
    CONSTRAINT ck_ppiq_protection_required CHECK(protection_required IN ('encrypted','hashed','redacted','retention-bounded','derived-only')),
    CONSTRAINT ck_ppiq_sensitive_status CHECK(current_status IN ('cataloged','protected','accepted-risk','not-applicable'))
);

INSERT INTO public.ppiq_sensitive_data_catalog
(table_name, column_name, sensitivity_class, protection_required, current_status, owner_module, notes)
VALUES
('ppiq_enterprise_mfa_secrets', 'totp_secret_encrypted', 'secret', 'encrypted', 'protected', 'Identity', 'TOTP secret must never be returned by API.'),
('ppiq_enterprise_recovery_codes', 'recovery_code_hash', 'secret', 'hashed', 'protected', 'Identity', 'Recovery codes are one-way hashed.'),
('ppiq_enterprise_sessions', 'refresh_token_hash', 'session', 'hashed', 'protected', 'Identity', 'Refresh tokens must be stored hashed.'),
('ppiq_sso_scim_users', 'email', 'personal', 'retention-bounded', 'cataloged', 'Identity', 'Personal identity attribute; must be included in DSAR and retention policy.'),
('ppiq_lead_captures', 'email', 'personal', 'retention-bounded', 'cataloged', 'Website/Leads', 'Lead email must be DSAR/export/erasure controlled.'),
('ppiq_lead_captures', 'phone', 'personal', 'retention-bounded', 'cataloged', 'Website/Leads', 'Lead phone must be DSAR/export/erasure controlled.'),
('ppiq_signed_licenses', 'license_payload_json', 'license', 'derived-only', 'cataloged', 'Licensing', 'Entitlements must derive from verified license only.'),
('ppiq_audit_events', 'old_value_json', 'audit', 'retention-bounded', 'cataloged', 'Compliance', 'Old values may include sensitive info and must be retention bounded.'),
('ppiq_audit_events', 'new_value_json', 'audit', 'retention-bounded', 'cataloged', 'Compliance', 'New values may include sensitive info and must be retention bounded.')
ON CONFLICT (table_name, column_name) DO UPDATE
SET sensitivity_class = EXCLUDED.sensitivity_class,
    protection_required = EXCLUDED.protection_required,
    owner_module = EXCLUDED.owner_module,
    notes = EXCLUDED.notes;

CREATE OR REPLACE FUNCTION public.ppiq_validate_sensitive_data_catalog()
RETURNS TABLE
(
    check_name text,
    is_green boolean,
    evidence text
)
LANGUAGE sql
AS $$
    SELECT
        'Sensitive catalog exists',
        to_regclass('public.ppiq_sensitive_data_catalog') IS NOT NULL,
        'Sensitive data catalog installed.'
    UNION ALL
    SELECT
        'Identity/session secrets cataloged',
        EXISTS (
            SELECT 1
            FROM public.ppiq_sensitive_data_catalog
            WHERE owner_module = 'Identity'
              AND protection_required IN ('encrypted','hashed')
        ),
        'MFA/session/SCIM sensitive fields are cataloged.'
    UNION ALL
    SELECT
        'Leads personal data cataloged',
        EXISTS (
            SELECT 1
            FROM public.ppiq_sensitive_data_catalog
            WHERE table_name = 'ppiq_lead_captures'
              AND sensitivity_class = 'personal'
        ),
        'Lead personal fields are cataloged.'
    UNION ALL
    SELECT
        'Audit retention-sensitive fields cataloged',
        EXISTS (
            SELECT 1
            FROM public.ppiq_sensitive_data_catalog
            WHERE table_name = 'ppiq_audit_events'
              AND protection_required = 'retention-bounded'
        ),
        'Audit JSON fields are cataloged as retention bounded.';
$$;