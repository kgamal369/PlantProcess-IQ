# PlantProcess IQ Phase 5B Refactor Preflight

Generated: 2026-06-06T08:36:29.182Z

## Purpose

This report prepares the actual Phase 5B god-file decomposition. It does not modify source files.
It records current hashes, line counts, imports, top-level declarations, and proposed module groups.

## Summary

| Task | File | Lines | Declarations | Imports | Risk |
|---|---:|---:|---:|---:|---|
| T-036A | `Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizardContent.implementation.tsx` | 1710 | 28 | 11 | requires-split |
| T-036B | `Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizard.implementation.tsx` | 1512 | 26 | 10 | requires-split |
| T-037A | `Frontend/PlantProcess.Web/src/api/productCoreApiClient.implementation.ts` | 599 | 4 | 3 | requires-split |
| T-037B | `Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.implementation.tsx` | 1128 | 8 | 9 | requires-split |
| T-037C | `Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/MaterialAnalyticsPages.implementation.tsx` | 1103 | 24 | 13 | requires-split |

## Target details

### T-036A — Widget builder content wizard

- File: `Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizardContent.implementation.tsx`
- Current lines: 1710
- Target max lines per resulting file: 400
- Current SHA-256: `4cee6593ba91d16e6b2df871b42b85955563ecff314661143325a64999da8f18`
- Strategy: Split by wizard sections/components, keep wrapper/orchestrator thin, extract PreviewTable/WizardSection/helper components.

#### Proposed module groups

| Group | Suggested file | Approx lines | Declarations |
|---|---|---:|---:|
| content-orchestration | `Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/content/content-orchestration.tsx` | 1189 | 22 |
| preview-components | `Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/content/preview-components.tsx` | 188 | 3 |
| step-sections | `Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/content/step-sections.tsx` | 294 | 3 |

#### Top-level declaration inventory

| Declaration | Kind | Lines | Group | Exported |
|---|---|---:|---|---|
| `WidgetBuilderWizardProps` | interface | 40-47 | content-orchestration | no |
| `WizardStep` | type | 48-55 | content-orchestration | no |
| `RelativeDateUnit` | type | 56-57 | content-orchestration | no |
| `WidgetBuilderState` | interface | 58-75 | content-orchestration | no |
| `ValidationIssue` | interface | 76-80 | content-orchestration | no |
| `stepOrder` | component-or-const | 81-89 | content-orchestration | no |
| `stepLabels` | component-or-const | 90-98 | content-orchestration | no |
| `defaultState` | component-or-const | 99-111 | content-orchestration | no |
| `generateWidgetCode` | function | 112-121 | content-orchestration | no |
| `parseJson` | function | 122-131 | content-orchestration | no |
| `toInputDateTime` | function | 132-140 | content-orchestration | no |
| `fromInputDateTime` | function | 141-149 | content-orchestration | no |
| `relativeFromUtc` | function | 150-167 | content-orchestration | no |
| `formatError` | function | 168-172 | content-orchestration | no |
| `mapValidationIssues` | function | 173-211 | content-orchestration | no |
| `isCompatible` | function | 212-232 | content-orchestration | no |
| `inferCategoryKey` | function | 233-247 | content-orchestration | no |
| `inferValueKey` | function | 248-260 | content-orchestration | no |
| `selectFieldForDimension` | function | 261-297 | step-sections | no |
| `WidgetBuilderWizardContent` | function | 298-1022 | content-orchestration | yes |
| `PurposeStep` | function | 1023-1056 | content-orchestration | no |
| `ChartTypeStep` | function | 1057-1090 | preview-components | no |
| `DataStep` | function | 1091-1223 | content-orchestration | no |
| `FilterStep` | function | 1224-1453 | step-sections | no |
| `ScriptStep` | function | 1454-1529 | content-orchestration | no |
| `PreviewStep` | function | 1530-1654 | preview-components | no |
| `PreviewTable` | function | 1655-1683 | preview-components | no |
| `WizardSection` | function | 1684-1710 | step-sections | no |

### T-036B — Widget builder wizard shell

- File: `Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/WidgetBuilderWizard.implementation.tsx`
- Current lines: 1512
- Target max lines per resulting file: 400
- Current SHA-256: `e3ff26bff6da32d772f6755a0d36751ce871a0447b35cc3981c6135e4766b592`
- Strategy: Split wizard shell into state hook, step navigation, preview orchestration, save/publish actions.

#### Proposed module groups

| Group | Suggested file | Approx lines | Declarations |
|---|---|---:|---:|
| wizard-hooks-actions | `Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/wizard/wizard-hooks-actions.tsx` | 27 | 2 |
| wizard-orchestrator | `Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/wizard/wizard-orchestrator.tsx` | 870 | 16 |
| wizard-preview | `Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/wizard/wizard-preview.tsx` | 29 | 1 |
| wizard-shell-components | `Frontend/PlantProcess.Web/src/components/dashboard/widget-builder/wizard/wizard-shell-components.tsx` | 547 | 7 |

#### Top-level declaration inventory

| Declaration | Kind | Lines | Group | Exported |
|---|---|---:|---|---|
| `WidgetBuilderWizardProps` | interface | 40-47 | wizard-orchestrator | no |
| `WizardStep` | type | 48-48 | wizard-shell-components | no |
| `RelativeDateUnit` | type | 49-50 | wizard-orchestrator | no |
| `WidgetBuilderState` | interface | 51-66 | wizard-hooks-actions | no |
| `ValidationIssue` | interface | 67-71 | wizard-orchestrator | no |
| `stepOrder` | component-or-const | 72-79 | wizard-shell-components | no |
| `defaultState` | component-or-const | 80-90 | wizard-hooks-actions | no |
| `generateWidgetCode` | function | 91-100 | wizard-orchestrator | no |
| `parseJson` | function | 101-110 | wizard-orchestrator | no |
| `toInputDateTime` | function | 111-119 | wizard-orchestrator | no |
| `fromInputDateTime` | function | 120-128 | wizard-orchestrator | no |
| `relativeFromUtc` | function | 129-146 | wizard-orchestrator | no |
| `formatError` | function | 147-151 | wizard-orchestrator | no |
| `mapValidationIssues` | function | 152-190 | wizard-orchestrator | no |
| `isCompatible` | function | 191-211 | wizard-orchestrator | no |
| `inferCategoryKey` | function | 212-226 | wizard-orchestrator | no |
| `inferValueKey` | function | 227-236 | wizard-orchestrator | no |
| `selectFieldForDimension` | function | 237-273 | wizard-orchestrator | no |
| `WidgetBuilderWizardContent` | function | 274-919 | wizard-orchestrator | yes |
| `PurposeStep` | function | 920-953 | wizard-shell-components | no |
| `ChartTypeStep` | function | 954-987 | wizard-shell-components | no |
| `DataStep` | function | 988-1120 | wizard-shell-components | no |
| `FilterStep` | function | 1121-1342 | wizard-shell-components | no |
| `PreviewStep` | function | 1343-1457 | wizard-shell-components | no |
| `PreviewTable` | function | 1458-1486 | wizard-preview | no |
| `WizardSection` | function | 1487-1512 | wizard-orchestrator | no |

### T-037A — Product core API client

- File: `Frontend/PlantProcess.Web/src/api/productCoreApiClient.implementation.ts`
- Current lines: 599
- Target max lines per resulting file: 450
- Current SHA-256: `90275ced37dda390e07a1b0880a4c1edc2c8b952c88bc66c61c5786d50ba12ee`
- Strategy: Split by endpoint domain: dashboard, materials, analytics, admin, mapping, workflow, license/demo/read-models.

#### Proposed module groups

| Group | Suggested file | Approx lines | Declarations |
|---|---|---:|---:|
| analytics-api | `Frontend/PlantProcess.Web/src/api/product-core/analytics-api.tsx` | 12 | 1 |
| dashboard-api | `Frontend/PlantProcess.Web/src/api/product-core/dashboard-api.tsx` | 37 | 2 |
| shared-api-types | `Frontend/PlantProcess.Web/src/api/product-core/shared-api-types.tsx` | 424 | 1 |

#### Top-level declaration inventory

| Declaration | Kind | Lines | Group | Exported |
|---|---|---:|---|---|
| `dashboardQuery` | function | 127-145 | dashboard-api | no |
| `dashboardBody` | function | 146-163 | dashboard-api | no |
| `createClientCorrelationId` | function | 164-175 | analytics-api | no |
| `productApi` | component-or-const | 176-599 | shared-api-types | yes |

### T-037B — Admin DB configuration tab

- File: `Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.implementation.tsx`
- Current lines: 1128
- Target max lines per resulting file: 450
- Current SHA-256: `08605b1e62d0231a50e38c6a74e505f8f2780c5fd70f21b969194423c4e55ba3`
- Strategy: Split into connection profile list, form/editor, validation/test panel, schema preview, helper hooks.

#### Proposed module groups

| Group | Suggested file | Approx lines | Declarations |
|---|---|---:|---:|
| admin-db-shared | `Frontend/PlantProcess.Web/src/pages/Admin/db-configuration/admin-db-shared.tsx` | 279 | 3 |
| connection-profile-section | `Frontend/PlantProcess.Web/src/pages/Admin/db-configuration/connection-profile-section.tsx` | 500 | 4 |
| schema-preview-section | `Frontend/PlantProcess.Web/src/pages/Admin/db-configuration/schema-preview-section.tsx` | 289 | 1 |

#### Top-level declaration inventory

| Declaration | Kind | Lines | Group | Exported |
|---|---|---:|---|---|
| `ConnectionTestResult` | interface | 61-67 | connection-profile-section | no |
| `ViewMode` | type | 68-69 | admin-db-shared | no |
| `PROVIDER_DEFAULTS` | component-or-const | 70-80 | connection-profile-section | no |
| `DbConfigurationTab` | function | 81-258 | admin-db-shared | yes |
| `ConnectionProfileList` | function | 259-400 | connection-profile-section | no |
| `ConnectionProfileForm` | function | 401-740 | connection-profile-section | no |
| `TableBrowser` | function | 741-1029 | schema-preview-section | no |
| `ImportJobSchedulePanel` | function | 1030-1128 | admin-db-shared | no |

### T-037C — Material analytics pages

- File: `Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/MaterialAnalyticsPages.implementation.tsx`
- Current lines: 1103
- Target max lines per resulting file: 450
- Current SHA-256: `e8e3c2182ff748e837c5b9ee7cbe269b1e7c9e3a761053d31b0bb12a9f6e5765`
- Strategy: Split by page section: overview, filters, KPI cards, charts, detail panels, helper formatters.

#### Proposed module groups

| Group | Suggested file | Approx lines | Declarations |
|---|---|---:|---:|
| chart-section | `Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/sections/chart-section.tsx` | 27 | 1 |
| material-analytics-shared | `Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/sections/material-analytics-shared.tsx` | 1007 | 22 |
| overview-section | `Frontend/PlantProcess.Web/src/pages/MaterialAnalytics/sections/overview-section.tsx` | 21 | 1 |

#### Top-level declaration inventory

| Declaration | Kind | Lines | Group | Exported |
|---|---|---:|---|---|
| `Row` | type | 49-50 | material-analytics-shared | no |
| `AsyncState` | type | 51-57 | material-analytics-shared | no |
| `useResource` | function | 58-96 | material-analytics-shared | no |
| `text` | function | 97-126 | material-analytics-shared | no |
| `number` | function | 127-135 | material-analytics-shared | no |
| `percent` | function | 136-141 | material-analytics-shared | no |
| `rows` | function | 142-154 | material-analytics-shared | no |
| `cssKind` | function | 155-171 | material-analytics-shared | no |
| `Chip` | function | 172-176 | material-analytics-shared | no |
| `PageShell` | function | 177-205 | material-analytics-shared | no |
| `Metric` | function | 206-226 | overview-section | no |
| `StandardDataState` | function | 227-263 | material-analytics-shared | no |
| `ChartPlaceholder` | function | 264-290 | chart-section | no |
| `materialColumns` | function | 291-332 | material-analytics-shared | no |
| `MaterialAnalyticsCommandDashboardPage` | function | 333-408 | material-analytics-shared | yes |
| `MaterialAnalyticsMaterialInvestigationPage` | function | 409-550 | material-analytics-shared | yes |
| `MaterialAnalyticsRiskIntelligencePage` | function | 551-601 | material-analytics-shared | yes |
| `MaterialAnalyticsDataQualityPage` | function | 602-662 | material-analytics-shared | yes |
| `MaterialAnalyticsCorrelationPage` | function | 663-747 | material-analytics-shared | yes |
| `MaterialAnalyticsMlReadinessPage` | function | 748-814 | material-analytics-shared | yes |
| `MaterialAnalyticsDemoLifecyclePage` | function | 815-923 | material-analytics-shared | yes |
| `MaterialAnalyticsAdminPreviewPage` | function | 924-978 | material-analytics-shared | yes |
| `MaterialAnalyticsAdministratorPage` | function | 979-1064 | material-analytics-shared | yes |
| `MaterialAnalyticsBrandIdentityPage` | function | 1065-1103 | material-analytics-shared | yes |

## Safe next implementation order

1. Split `productCoreApiClient.implementation.ts` first because API-domain split is usually easiest to build-verify.
2. Split `MaterialAnalyticsPages.implementation.tsx` by section after API imports are stable.
3. Split `AdminDbConfigurationTab.implementation.tsx` by editor/list/validation/schema preview.
4. Split widget-builder files last, because wizard behavior must be preserved exactly.

## Required gate after each split

```powershell
powershell -ExecutionPolicy Bypass -File .\tools\phase56\Invoke-Phase5Phase6Validation.ps1 -ProjectRoot "C:\Workspace\PlantProcess-IQ" -RunFrontendBuild
```

