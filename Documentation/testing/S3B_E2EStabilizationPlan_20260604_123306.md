# PlantProcess IQ - S3B E2E Stabilization Plan

Generated at: 2026-06-04 12:33:11

This plan is analysis-only. No tests were moved, deleted, or edited.

## Summary by Bucket

| Bucket | Count |
|---|---:|
| E2E_RUNTIME_CONFIG | 3 |
| FRONTEND_THIN_TEST_BACKLOG | 17 |
| NPM_SCRIPT_SOURCE | 2 |
| QUARANTINE_REPAIR_CANDIDATE | 6 |
| SECONDARY_E2E_REVIEW | 2 |
| STABLE_CORE_AUTH_SMOKE | 5 |
| STABLE_CORE_CANDIDATE | 24 |
| STABLE_CORE_PRODUCT_JOURNEY | 4 |
| THIN_STUB_REVIEW | 3 |

## Stabilization Rule

First make a small stable E2E core green. Do not try to fix all E2E specs at once.
Legacy phase specs and thin specs should be quarantined or repaired before joining the default E2E run.
Product journey specs should be kept, repaired, and made profile-driven.

## Detailed Plan

| Priority | Bucket | Area | Source | Action | Reason |
|---|---|---|---|---|---|
| P0 | E2E_RUNTIME_CONFIG | E2EConfig | Frontend\PlantProcess.Web\playwright.config.ts | Make profile-driven and deterministic | Playwright config controls baseURL, retries, workers and reports. |
| P0 | E2E_RUNTIME_CONFIG | E2EConfig | Frontend\PlantProcess.Web\playwright.phase2.config.ts | Make profile-driven and deterministic | Playwright config controls baseURL, retries, workers and reports. |
| P0 | E2E_RUNTIME_CONFIG | E2EConfig | Frontend\PlantProcess.Web\playwright.phase9.config.ts | Make profile-driven and deterministic | Playwright config controls baseURL, retries, workers and reports. |
| P0 | NPM_SCRIPT_SOURCE | PackageScripts | Frontend\PlantProcess.Web\package.json | Review npm scripts before changing E2E command behavior | package.json defines build/test/e2e command contracts. |
| P0 | NPM_SCRIPT_SOURCE | PackageScripts | Website\PlantProcess.Website\package.json | Review npm scripts before changing E2E command behavior | package.json defines build/test/e2e command contracts. |
| P0 | QUARANTINE_REPAIR_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\journeys\p00-e2e-consolidation.contract.spec.ts | Move later to quarantine or repoint routes before enabling in main E2E | Phase/legacy specs are likely drifted from current product routes. |
| P0 | QUARANTINE_REPAIR_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\phase2-lifecycle-proof.spec.ts | Move later to quarantine or repoint routes before enabling in main E2E | Phase/legacy specs are likely drifted from current product routes. |
| P0 | QUARANTINE_REPAIR_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\phase4-ml-foundation-proof.spec.ts | Move later to quarantine or repoint routes before enabling in main E2E | Phase/legacy specs are likely drifted from current product routes. |
| P0 | QUARANTINE_REPAIR_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\phase5-scheduled-learning-proof.spec.ts | Move later to quarantine or repoint routes before enabling in main E2E | Phase/legacy specs are likely drifted from current product routes. |
| P0 | QUARANTINE_REPAIR_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\phase9-action-matrix.spec.ts | Move later to quarantine or repoint routes before enabling in main E2E | Phase/legacy specs are likely drifted from current product routes. |
| P0 | QUARANTINE_REPAIR_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\visual\phase56-analytics-system.visual.spec.ts | Move later to quarantine or repoint routes before enabling in main E2E | Phase/legacy specs are likely drifted from current product routes. |
| P0 | STABLE_CORE_AUTH_SMOKE | E2E | Frontend\PlantProcess.Web\e2e\api-smoke.spec.ts | Keep in first green E2E suite | Auth/API smoke should be the first always-green E2E gate. |
| P0 | STABLE_CORE_AUTH_SMOKE | E2E | Frontend\PlantProcess.Web\e2e\full-stack-smoke.spec.ts | Keep in first green E2E suite | Auth/API smoke should be the first always-green E2E gate. |
| P0 | STABLE_CORE_AUTH_SMOKE | E2E | Frontend\PlantProcess.Web\e2e\p0-auth-pages-contract.spec.ts | Keep in first green E2E suite | Auth/API smoke should be the first always-green E2E gate. |
| P0 | STABLE_CORE_AUTH_SMOKE | E2E | Frontend\PlantProcess.Web\e2e\route-smoke.spec.ts | Keep in first green E2E suite | Auth/API smoke should be the first always-green E2E gate. |
| P0 | STABLE_CORE_AUTH_SMOKE | E2E | Frontend\PlantProcess.Web\e2e\security\auth-matrix-admin.spec.ts | Keep in first green E2E suite | Auth/API smoke should be the first always-green E2E gate. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\a11y\phase56-accessibility.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\api\phase03-two-stage-import.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\critical-shell-regression.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\golden-path.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\hardening-matrix.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\license-and-demo-lifecycle.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\license-gate-ux.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\nav-graph-refresh-survival.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\p1-safety-net.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\p6-genealogy-widget-workflow.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\phase1-button-action-matrix.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\phase1-golden-demo.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\phase1-security-hardening.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\phase1-toast-mapping.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\phase23-pagebuilder-persistence.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\phase2-backend-outage.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\phase2-chart-interaction.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\phase2-responsive-multibrowser.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\phase3-dynamic-page-rendering.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\phase56-primary-flows.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\phase78-workflow-widget.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\phase9-responsive-states.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\visual\phase9-core.visual.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_CANDIDATE | E2E | Frontend\PlantProcess.Web\e2e\widget-builder-focused.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_PRODUCT_JOURNEY | E2E | Frontend\PlantProcess.Web\e2e\admin-db-focused.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_PRODUCT_JOURNEY | E2E | Frontend\PlantProcess.Web\e2e\admin-schema-focused.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_PRODUCT_JOURNEY | E2E | Frontend\PlantProcess.Web\e2e\dashboard-refresh-filter.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | STABLE_CORE_PRODUCT_JOURNEY | E2E | Frontend\PlantProcess.Web\e2e\p1-risk-dataquality-contract.spec.ts | Keep and repair first | Product journey specs should become the reliable regression core. |
| P0 | THIN_STUB_REVIEW | E2E | Frontend\PlantProcess.Web\e2e\api\phase02-data-lifecycle-contract.spec.ts | Expand into real journey or quarantine | Thin E2E specs create false confidence. |
| P0 | THIN_STUB_REVIEW | E2E | Frontend\PlantProcess.Web\e2e\dimension7-brand-identity.spec.ts | Expand into real journey or quarantine | Thin E2E specs create false confidence. |
| P0 | THIN_STUB_REVIEW | E2E | Frontend\PlantProcess.Web\e2e\p4-advanced-analysis.spec.ts | Expand into real journey or quarantine | Thin E2E specs create false confidence. |
| P1 | FRONTEND_THIN_TEST_BACKLOG | Frontend | Frontend\PlantProcess.Web\e2e\api\phase02-data-lifecycle-contract.spec.ts | Expand or remove after review | Thin frontend tests should not be counted as strong coverage. |
| P1 | FRONTEND_THIN_TEST_BACKLOG | Frontend | Frontend\PlantProcess.Web\e2e\critical-shell-regression.spec.ts | Expand or remove after review | Thin frontend tests should not be counted as strong coverage. |
| P1 | FRONTEND_THIN_TEST_BACKLOG | Frontend | Frontend\PlantProcess.Web\e2e\dimension7-brand-identity.spec.ts | Expand or remove after review | Thin frontend tests should not be counted as strong coverage. |
| P1 | FRONTEND_THIN_TEST_BACKLOG | Frontend | Frontend\PlantProcess.Web\e2e\full-stack-smoke.spec.ts | Expand or remove after review | Thin frontend tests should not be counted as strong coverage. |
| P1 | FRONTEND_THIN_TEST_BACKLOG | Frontend | Frontend\PlantProcess.Web\e2e\license-gate-ux.spec.ts | Expand or remove after review | Thin frontend tests should not be counted as strong coverage. |
| P1 | FRONTEND_THIN_TEST_BACKLOG | Frontend | Frontend\PlantProcess.Web\e2e\p1-safety-net.spec.ts | Expand or remove after review | Thin frontend tests should not be counted as strong coverage. |
| P1 | FRONTEND_THIN_TEST_BACKLOG | Frontend | Frontend\PlantProcess.Web\e2e\p4-advanced-analysis.spec.ts | Expand or remove after review | Thin frontend tests should not be counted as strong coverage. |
| P1 | FRONTEND_THIN_TEST_BACKLOG | Frontend | Frontend\PlantProcess.Web\e2e\phase56-primary-flows.spec.ts | Expand or remove after review | Thin frontend tests should not be counted as strong coverage. |
| P1 | FRONTEND_THIN_TEST_BACKLOG | Frontend | Frontend\PlantProcess.Web\e2e\phase78-workflow-widget.spec.ts | Expand or remove after review | Thin frontend tests should not be counted as strong coverage. |
| P1 | FRONTEND_THIN_TEST_BACKLOG | Frontend | Frontend\PlantProcess.Web\e2e\phase9-action-matrix.spec.ts | Expand or remove after review | Thin frontend tests should not be counted as strong coverage. |
| P1 | FRONTEND_THIN_TEST_BACKLOG | Frontend | Frontend\PlantProcess.Web\e2e\route-smoke.spec.ts | Expand or remove after review | Thin frontend tests should not be counted as strong coverage. |
| P1 | FRONTEND_THIN_TEST_BACKLOG | Frontend | Frontend\PlantProcess.Web\src\components\__tests__\AsyncState.test.tsx | Expand or remove after review | Thin frontend tests should not be counted as strong coverage. |
| P1 | FRONTEND_THIN_TEST_BACKLOG | Frontend | Frontend\PlantProcess.Web\src\components\__tests__\LockedFeatureOverlay.test.tsx | Expand or remove after review | Thin frontend tests should not be counted as strong coverage. |
| P1 | FRONTEND_THIN_TEST_BACKLOG | Frontend | Frontend\PlantProcess.Web\src\components\__tests__\StatusBadge.test.tsx | Expand or remove after review | Thin frontend tests should not be counted as strong coverage. |
| P1 | FRONTEND_THIN_TEST_BACKLOG | Frontend | Frontend\PlantProcess.Web\src\pages\Acquisition\__tests__\EdgeCollectorManagementPage.test.tsx | Expand or remove after review | Thin frontend tests should not be counted as strong coverage. |
| P1 | FRONTEND_THIN_TEST_BACKLOG | Frontend | Frontend\PlantProcess.Web\src\pages\Assistant\__tests__\GroundedAssistantPage.test.tsx | Expand or remove after review | Thin frontend tests should not be counted as strong coverage. |
| P1 | FRONTEND_THIN_TEST_BACKLOG | Frontend | Frontend\PlantProcess.Web\src\test\integration\mockedApi.integration.test.ts | Expand or remove after review | Thin frontend tests should not be counted as strong coverage. |
| P1 | SECONDARY_E2E_REVIEW | E2E | Frontend\PlantProcess.Web\e2e\dimension2-dimension6-readiness.spec.ts | Review after stable core is green | Generic E2E spec not yet classified as core or legacy. |
| P1 | SECONDARY_E2E_REVIEW | E2E | Frontend\PlantProcess.Web\e2e\page-builder-v7.spec.ts | Review after stable core is green | Generic E2E spec not yet classified as core or legacy. |
