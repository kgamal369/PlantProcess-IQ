# PlantProcess IQ - API Raw SQL Quarantine Contract

Generated at: 
2026-06-04 13:56:25

## Purpose

Pack 4B does not remove raw SQL yet. It creates a controlled baseline so that the team can refactor safely without losing visibility.

## Contract

1. API endpoints should not own direct database access long-term.
2. Direct Npgsql usage inside PlantProcess.Api is quarantined and must be burned down in Pack 4C+.
3. User-defined SQL, KPI SQL-view logic, and configurable SQL must route through a single validator/allowlist path.
4. Tenant/RLS context must be applied consistently across EF and raw SQL paths.
5. New raw SQL inside API must not be added silently; it must appear in this register or be moved to Application/Infrastructure immediately.

## Refactor tracks

- HealthAndReadinessDiagnostics
- AdminDiagnosticsAndConfiguration
- AnalyticsAndKpiRuntime
- CanonicalDataAndIntegrationRuntime
- SecurityIdentityTenantRuntime
- UnclassifiedApiRuntime

## Burn-down order

1. Move analytics/KPI/customer-facing runtime SQL first.
2. Move security/tenant/license/auth related SQL next.
3. Keep health/readiness diagnostics last, because some low-level DB probes may remain intentionally close to API startup diagnostics.
4. Replace direct Npgsql in API with Application interfaces and Infrastructure implementations.
5. Add integration tests around each moved query before deleting the old endpoint-level SQL.


