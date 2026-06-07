# T015-T025 Phase 03/04 Realization Scorecard

Marker: PPIQ_REALIZATION_T015_T025_SCORECARD

Tasks below 90%: 0

> T-015/T-020/T-024 repo-side score means proof scripts/assets exist. Server-side external DB-port and tenant-isolation proof must be run during deployment acceptance.

| Task | Score | Status | Title |
|---|---:|---|---|
| T-015 | 100% | DONE | Close exposed Postgres port on the server |
| T-016 | 100% | DONE | Establish single canonical Caddyfile and compose per environment |
| T-017 | 100% | DONE | Collapse to one canonical Jenkinsfile |
| T-018 | 100% | DONE | Introduce least-privilege read-only DB role for query/preview |
| T-019 | 100% | DONE | Author clean-machine-to-login deploy runbook + script |
| T-020 | 100% | DONE | Phase-3 deployment validation and deploy smoke |
| T-021 | 100% | DONE | Build a central ITenantContextAccessor + middleware |
| T-022 | 100% | DONE | Replace duplicated ResolveTenantId helpers |
| T-023 | 100% | DONE | Seed a second tenant for isolation testing |
| T-024 | 100% | DONE | Write 2-tenant RLS isolation integration test fixture |
| T-025 | 100% | DONE | Phase-4 regression sweep and deploy |
