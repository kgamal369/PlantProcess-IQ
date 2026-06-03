\encoding UTF8
SET client_encoding = 'UTF8';

SELECT set_config('app.current_tenant', '00000000-0000-0000-0000-000000000001', false);

UPDATE public.ppiq_ed25519_activated_licenses
SET activation_status = 'superseded'
WHERE tenant_id = '00000000-0000-0000-0000-000000000001'
  AND activation_status = 'active'
  AND license_key <> 'PPIQ-00000000-1780491418081';

INSERT INTO public.ppiq_ed25519_activated_licenses
(
    tenant_id,
    license_key,
    key_id,
    compact_jws,
    tier,
    payload_json,
    features_json,
    limits_json,
    issued_at_utc,
    expires_at_utc,
    instance_id,
    verification_status,
    activation_status,
    verification_error,
    activated_at_utc,
    last_verified_at_utc
)
VALUES
(
    '00000000-0000-0000-0000-000000000001',
    'PPIQ-00000000-1780491418081',
    'ppiq-dev-ed25519-20260603',
    'eyJhbGciOiJFZERTQSIsInR5cCI6InBwaXEtbGljZW5zZStqd3MiLCJraWQiOiJwcGlxLWRldi1lZDI1NTE5LTIwMjYwNjAzIn0.eyJ0ZW5hbnRJZCI6IjAwMDAwMDAwLTAwMDAtMDAwMC0wMDAwLTAwMDAwMDAwMDAwMSIsImxpY2Vuc2VLZXkiOiJQUElRLTAwMDAwMDAwLTE3ODA0OTE0MTgwODEiLCJ0aWVyIjoiUHJvUGx1cyIsImlzc3VlZEF0VXRjIjoiMjAyNi0wNi0wM1QxMjo1Njo1OC4wODFaIiwiZXhwaXJlc0F0VXRjIjoiMjAyNy0wNi0wM1QxMjo1Njo1OC4wODFaIiwiZmVhdHVyZXMiOlsiUmVhZE9ubHlTb3VyY2VSZWdpc3RyeSIsIkNzdkltcG9ydCIsIkV4Y2VsSW1wb3J0IiwiUG9zdGdyZVNxbENvbm5lY3RvciIsIlNjaGVtYVNxbFZpZXdCdWlsZGVyIiwiQ3Jvc3NTb3VyY2VKb2luRXhlY3V0aW9uIiwiS3BpVmlld0J1aWxkZXIiLCJXaWRnZXRTY3JpcHRMYXllciIsIkRhc2hib2FyZFBhZ2VCdWlsZGVyIiwiRGF0YVF1YWxpdHlGdWxsU2NhbiIsIlJpc2tEYXNoYm9hcmRWaWV3IiwiQ29ycmVsYXRpb25NYW51YWxSdW4iLCJJbnZlc3RpZ2F0aW9uV29ya2Zsb3ciLCJNbExlYXJuaW5nSm9icyJdLCJsaW1pdHMiOnsibWF4VXNlcnMiOjI1LCJtYXhEYXRhU291cmNlcyI6OCwibWF4U2NoZWR1bGVkSm9icyI6MjUsIm1heERhc2hib2FyZHMiOjIwLCJtaW5SZWZyZXNoSW50ZXJ2YWxNaW51dGVzIjoyLCJtYXhQcmV2aWV3Um93cyI6MTAwMDB9fQ.xWU4JRS3WTgJkBxI3BXbH28Ljk5m0YpLpOvo_ZWAFZfc6xpMD-sgpZ39a2oeNpHn8IqmGnHVWHk6ByeWxXDqDQ',
    'ProPlus',
    '{"tenantId":"00000000-0000-0000-0000-000000000001","licenseKey":"PPIQ-00000000-1780491418081","tier":"ProPlus","issuedAtUtc":"2026-06-03T12:56:58.081Z","expiresAtUtc":"2027-06-03T12:56:58.081Z","features":["ReadOnlySourceRegistry","CsvImport","ExcelImport","PostgreSqlConnector","SchemaSqlViewBuilder","CrossSourceJoinExecution","KpiViewBuilder","WidgetScriptLayer","DashboardPageBuilder","DataQualityFullScan","RiskDashboardView","CorrelationManualRun","InvestigationWorkflow","MlLearningJobs"],"limits":{"maxUsers":25,"maxDataSources":8,"maxScheduledJobs":25,"maxDashboards":20,"minRefreshIntervalMinutes":2,"maxPreviewRows":10000}}'::jsonb,
    '["ReadOnlySourceRegistry","CsvImport","ExcelImport","PostgreSqlConnector","SchemaSqlViewBuilder","CrossSourceJoinExecution","KpiViewBuilder","WidgetScriptLayer","DashboardPageBuilder","DataQualityFullScan","RiskDashboardView","CorrelationManualRun","InvestigationWorkflow","MlLearningJobs"]'::jsonb,
    '{"maxUsers":25,"maxDataSources":8,"maxScheduledJobs":25,"maxDashboards":20,"minRefreshIntervalMinutes":2,"maxPreviewRows":10000}'::jsonb,
    '2026-06-03T12:56:58.081Z'::timestamptz,
    '2027-06-03T12:56:58.081Z'::timestamptz,
    NULL,
    'valid',
    'active',
    NULL,
    now(),
    now()
)
ON CONFLICT (tenant_id, license_key)
DO UPDATE SET
    key_id = EXCLUDED.key_id,
    compact_jws = EXCLUDED.compact_jws,
    tier = EXCLUDED.tier,
    payload_json = EXCLUDED.payload_json,
    features_json = EXCLUDED.features_json,
    limits_json = EXCLUDED.limits_json,
    issued_at_utc = EXCLUDED.issued_at_utc,
    expires_at_utc = EXCLUDED.expires_at_utc,
    instance_id = EXCLUDED.instance_id,
    verification_status = 'valid',
    activation_status = 'active',
    verification_error = NULL,
    last_verified_at_utc = now();

SELECT tenant_id, license_key, tier, verification_status, activation_status
FROM public.ppiq_v_ed25519_current_entitlements
WHERE tenant_id = '00000000-0000-0000-0000-000000000001';