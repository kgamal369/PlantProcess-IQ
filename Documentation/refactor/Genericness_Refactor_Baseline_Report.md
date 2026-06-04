# PlantProcess IQ - Genericness, Refactor & Test-Stability Baseline

Generated at: 2026-06-04 13:52:03

## Purpose

Pack 4A creates a non-invasive baseline for the next stabilization dimension: genericness, tenant safety, SQL access consistency, config hygiene, and test reliability.

## Gate Summary

| Status | Count |
|---|---:|
| BLOCKER | 1 |
| OK | 4 |

## Finding Severity Summary

| Severity | Count |
|---|---:|
| HIGH | 262 |
| INFO | 164 |
| LOW | 31 |
| MEDIUM | 79 |

## Top Finding Categories

| Category | Count |
|---|---:|
| Raw SQL access inside API | 191 |
| Tenant/RLS/GUC touchpoint | 119 |
| Hardcoded demo tenant | 71 |
| Demo tenant naming assumption | 54 |
| SQL validator touchpoint | 41 |
| Raw EF SQL usage | 18 |
| Thin frontend/e2e test | 16 |
| Thin backend test | 15 |
| Skipped or placeholder test | 7 |
| Allowlist provider touchpoint | 4 |

## Recommended Pack 4 burn-down order

1. Remove committed config secret patterns and appsettings drift.
2. Eliminate hardcoded demo tenant/product assumptions from runtime paths.
3. Standardize tenant/RLS/GUC handling across EF and raw SQL paths.
4. Move direct API raw SQL access behind application/infrastructure services or quarantine it clearly.
5. Convert skipped/thin tests into real regression coverage or delete obsolete specs.
