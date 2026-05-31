# PlantProcess IQ — P00A Test Register v1

Generated: 2026-05-31T11:07:14.744Z

## Purpose

This document is the official P00A Test Register baseline. It records the disposition of the 113 current test / validation items and focuses implementation only on the 77 items that are not KEEP.

## Scope

- Total register items: 113
- KEEP items: 36
- Implementation items: 77

## Disposition Summary

| Disposition | Count | Implementation Meaning |
|---|---:|---|
| KEEP | 36 | Already accepted; no implementation required now |
| DELETE | 8 | Archive and remove no-op/duplicate tests |
| MODIFY | 31 | Strengthen existing tests; behavioural implementation required |
| ADD | 14 | Add missing tests; behavioural implementation required |
| TRANSFER→TEST | 5 | Convert regex/script validation into real behavioural tests |
| KEEP-AS-GATE | 8 | Keep structural scripts as CI gates |
| RETIRE | 11 | Mark old phase validators as retire-pending-replacement |

## Current Implementation Status

| Group | Status |
|---|---|
| DELETE | Implemented by this pack using backup archive |
| RETIRE | Marked with retire-pending-replacement banner |
| TRANSFER→TEST | Marked with transfer-to-real-test banner |
| KEEP-AS-GATE | Marked as structural CI gate |
| MODIFY | Pending behavioural test implementation |
| ADD | Pending behavioural test implementation |

## Deleted / Archived Items

- `Backend/tests/PlantProcess.Api.IntegrationTests/ApiTestEnvironmentTests.cs` — No-op integration smoke test; superseded by real API integration tests.
- `Backend/tests/PlantProcess.Application.UnitTests/ApplicationTestEnvironmentTests.cs` — Assembly-load smoke only; superseded by real application unit tests.
- `Backend/tests/PlantProcess.Domain.Tests/DomainTestEnvironmentTests.cs` — Assembly-load smoke only; superseded by real domain tests.
- `Backend/tests/PlantProcess.PerformanceTests/PerformanceTestEnvironmentTests.cs` — No real performance assertion; replaced later by actual performance tests.
- `Backend/tests/PlantProcess.Infrastructure.IntegrationTests/InfrastructureTestEnvironmentTests.cs` — Assembly-load smoke only; superseded by real infrastructure integration tests.
- `Frontend/PlantProcess.Web/src/test/smoke/frontendSmoke.test.ts` — Seven-line frontend smoke test; superseded by route/full-stack E2E and real unit tests.
- `Frontend/PlantProcess.Web/e2e/phase2-navigation-refresh-survival.spec.ts` — Duplicate refresh-survival coverage; consolidate into nav-graph-refresh-survival journey.
- `Frontend/PlantProcess.Web/e2e/phase1-route-refresh.spec.ts` — Duplicate route refresh coverage; consolidate into refresh-survival journey.

## Retire-Pending-Replacement Items

- `tools/validation/validate-phase01-phase02-gates.mjs` → Auth/data lifecycle tests + CI gating
- `tools/validation/validate-phase01-phase02-v5-gates.mjs` → Current behavioural tests
- `tools/validation/validate-phase03-gates.mjs` → Delta import integration tests
- `tools/validation/validate-v6-phase01-phase02-completion.cjs` → Auth/data lifecycle tests
- `tools/validation/validate-v7-phase01.cjs` → Auth/deploy behavioural tests
- `tools/validation/validate-v7-phase01-acceptance.cjs` → Auth/deploy behavioural tests
- `tools/validation/validate-v7-phase02-phase03-acceptance.cjs` → Data lifecycle tests
- `tools/phase78/validate-phase7-phase8-acceptance.cjs` → Widget/page-builder E2E tests
- `Frontend/PlantProcess.Web/tools/phase3/validate-phase3-phase4-acceptance.cjs` → Consolidated E2E journeys
- `Frontend/PlantProcess.Web/tools/phase56/validate-phase5-phase6-acceptance.cjs` → Analytics E2E tests
- `Backend/tools/validate-sprint6-tasks-4-8.ps1` → Backend behavioural tests

## Transfer-To-Real-Test Items

- `tools/validation/validate-v7-phase04-phase05-acceptance.cjs` → Backend/tests/PlantProcess.Api.IntegrationTests/Analytics/MlLearningCoreIntegrationTests.cs
- `tools/validation/validate-v7-phase04-phase05-completion.cjs` → Backend/tests/PlantProcess.Api.IntegrationTests/Analytics/MlLearningCoreIntegrationTests.cs
- `tools/validation/validate-t208-exposure.cjs` → Deployment exposure integration test / deploy check
- `tools/validation/validate-sql-script-hygiene.cjs` → Backend/tests/PlantProcess.Infrastructure.IntegrationTests/Database/SqlScriptHygieneApplyTests.cs
- `Frontend/PlantProcess.Web/scripts/validate-api-client-policy.mjs` → Frontend Vitest apiClient policy test + ESLint rule

## Keep-As-Gate Items

- `Frontend/PlantProcess.Web/scripts/validate-standard-imports.mjs` — Keep as structural CI gate
- `Frontend/PlantProcess.Web/scripts/validate-forbidden-copy.mjs` — Keep as forbidden-copy CI gate
- `Frontend/PlantProcess.Web/scripts/validate-no-console-in-src.mjs` — Keep as no-console CI gate
- `Frontend/PlantProcess.Web/scripts/validate-ui-system-rollout.mjs` — Keep as UI rollout structural gate
- `Frontend/PlantProcess.Web/tools/ui/validate-ui-standards.mjs` — Keep as UI standards structural gate
- `Frontend/PlantProcess.Web/tools/ui/validate-phase2-full-ui-standards.mjs` — Keep as full UI standards structural gate
- `tools/validation/prove-standard-import-gate.cjs` — Keep as meta-gate proving import-gate enforcement
- `Website/PlantProcess.Website/scripts/validate-website-content.mjs` — Keep as website content structural gate
