const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();
const now = new Date().toISOString();

function toPath(relativePath) {
  return path.join(root, relativePath.replaceAll("\\", path.sep).replaceAll("/", path.sep));
}

function exists(relativePath) {
  return fs.existsSync(toPath(relativePath));
}

function mkdirFor(relativePath) {
  fs.mkdirSync(path.dirname(toPath(relativePath)), { recursive: true });
}

function read(relativePath) {
  return fs.readFileSync(toPath(relativePath), "utf8");
}

function write(relativePath, content) {
  mkdirFor(relativePath);
  fs.writeFileSync(toPath(relativePath), content.replace(/\r\n/g, "\n"), "utf8");
}

function backupPathFor(relativePath) {
  const safeName = relativePath
    .replaceAll("\\", "__")
    .replaceAll("/", "__")
    .replaceAll(":", "")
    .replaceAll(" ", "_");

  return `Documentation/RetiredTests/P00A/${safeName}`;
}

function commentPrefix(relativePath) {
  const ext = path.extname(relativePath).toLowerCase();

  if (ext === ".ps1" || ext === ".sh" || ext === ".yml" || ext === ".yaml") return "#";
  if (ext === ".sql") return "--";
  if (ext === ".md") return "<!--";
  return "//";
}

function banner(relativePath, disposition, replacement) {
  const prefix = commentPrefix(relativePath);

  if (prefix === "<!--") {
    return `<!--
P00A-TEST-REGISTER: ${disposition}
Date: ${now}
Replacement: ${replacement}
Reason: This file is tracked by the P00A Test Register and should not be treated as a final behavioural test.
-->
`;
  }

  return `${prefix} P00A-TEST-REGISTER: ${disposition}
${prefix} Date: ${now}
${prefix} Replacement: ${replacement}
${prefix} Reason: This file is tracked by the P00A Test Register and should not be treated as a final behavioural test.

`;
}

function prependMarker(relativePath, disposition, replacement) {
  if (!exists(relativePath)) {
    return { path: relativePath, status: "missing" };
  }

  const current = read(relativePath);
  if (current.includes("P00A-TEST-REGISTER:")) {
    return { path: relativePath, status: "already-marked" };
  }

  write(relativePath, banner(relativePath, disposition, replacement) + current);
  return { path: relativePath, status: "marked" };
}

function archiveAndDelete(relativePath, reason) {
  if (!exists(relativePath)) {
    return { path: relativePath, status: "already-absent" };
  }

  const backupRelativePath = backupPathFor(relativePath);
  mkdirFor(backupRelativePath);

  const original = read(relativePath);
  const archived = `// P00A-TEST-REGISTER: DELETE-ARCHIVED
// ArchivedAtUtc: ${now}
// OriginalPath: ${relativePath}
// Reason: ${reason}

${original}`;

  write(backupRelativePath, archived);
  fs.unlinkSync(toPath(relativePath));

  return {
    path: relativePath,
    backup: backupRelativePath,
    status: "archived-and-deleted"
  };
}

const deleteActions = [
  {
    path: "Backend/tests/PlantProcess.Api.IntegrationTests/ApiTestEnvironmentTests.cs",
    reason: "No-op integration smoke test; superseded by real API integration tests."
  },
  {
    path: "Backend/tests/PlantProcess.Application.UnitTests/ApplicationTestEnvironmentTests.cs",
    reason: "Assembly-load smoke only; superseded by real application unit tests."
  },
  {
    path: "Backend/tests/PlantProcess.Domain.Tests/DomainTestEnvironmentTests.cs",
    reason: "Assembly-load smoke only; superseded by real domain tests."
  },
  {
    path: "Backend/tests/PlantProcess.PerformanceTests/PerformanceTestEnvironmentTests.cs",
    reason: "No real performance assertion; replaced later by actual performance tests."
  },
  {
    path: "Backend/tests/PlantProcess.Infrastructure.IntegrationTests/InfrastructureTestEnvironmentTests.cs",
    reason: "Assembly-load smoke only; superseded by real infrastructure integration tests."
  },
  {
    path: "Frontend/PlantProcess.Web/src/test/smoke/frontendSmoke.test.ts",
    reason: "Seven-line frontend smoke test; superseded by route/full-stack E2E and real unit tests."
  },
  {
    path: "Frontend/PlantProcess.Web/e2e/phase2-navigation-refresh-survival.spec.ts",
    reason: "Duplicate refresh-survival coverage; consolidate into nav-graph-refresh-survival journey."
  },
  {
    path: "Frontend/PlantProcess.Web/e2e/phase1-route-refresh.spec.ts",
    reason: "Duplicate route refresh coverage; consolidate into refresh-survival journey."
  }
];

const retireActions = [
  {
    path: "tools/validation/validate-phase01-phase02-gates.mjs",
    replacement: "Auth/data lifecycle tests + CI gating"
  },
  {
    path: "tools/validation/validate-phase01-phase02-v5-gates.mjs",
    replacement: "Current behavioural tests"
  },
  {
    path: "tools/validation/validate-phase03-gates.mjs",
    replacement: "Delta import integration tests"
  },
  {
    path: "tools/validation/validate-v6-phase01-phase02-completion.cjs",
    replacement: "Auth/data lifecycle tests"
  },
  {
    path: "tools/validation/validate-v7-phase01.cjs",
    replacement: "Auth/deploy behavioural tests"
  },
  {
    path: "tools/validation/validate-v7-phase01-acceptance.cjs",
    replacement: "Auth/deploy behavioural tests"
  },
  {
    path: "tools/validation/validate-v7-phase02-phase03-acceptance.cjs",
    replacement: "Data lifecycle tests"
  },
  {
    path: "tools/phase78/validate-phase7-phase8-acceptance.cjs",
    replacement: "Widget/page-builder E2E tests"
  },
  {
    path: "Frontend/PlantProcess.Web/tools/phase3/validate-phase3-phase4-acceptance.cjs",
    replacement: "Consolidated E2E journeys"
  },
  {
    path: "Frontend/PlantProcess.Web/tools/phase56/validate-phase5-phase6-acceptance.cjs",
    replacement: "Analytics E2E tests"
  },
  {
    path: "Backend/tools/validate-sprint6-tasks-4-8.ps1",
    replacement: "Backend behavioural tests"
  }
];

const transferActions = [
  {
    path: "tools/validation/validate-v7-phase04-phase05-acceptance.cjs",
    replacement: "Backend/tests/PlantProcess.Api.IntegrationTests/Analytics/MlLearningCoreIntegrationTests.cs"
  },
  {
    path: "tools/validation/validate-v7-phase04-phase05-completion.cjs",
    replacement: "Backend/tests/PlantProcess.Api.IntegrationTests/Analytics/MlLearningCoreIntegrationTests.cs"
  },
  {
    path: "tools/validation/validate-t208-exposure.cjs",
    replacement: "Deployment exposure integration test / deploy check"
  },
  {
    path: "tools/validation/validate-sql-script-hygiene.cjs",
    replacement: "Backend/tests/PlantProcess.Infrastructure.IntegrationTests/Database/SqlScriptHygieneApplyTests.cs"
  },
  {
    path: "Frontend/PlantProcess.Web/scripts/validate-api-client-policy.mjs",
    replacement: "Frontend Vitest apiClient policy test + ESLint rule"
  }
];

const keepGateActions = [
  {
    path: "Frontend/PlantProcess.Web/scripts/validate-standard-imports.mjs",
    replacement: "Keep as structural CI gate"
  },
  {
    path: "Frontend/PlantProcess.Web/scripts/validate-forbidden-copy.mjs",
    replacement: "Keep as forbidden-copy CI gate"
  },
  {
    path: "Frontend/PlantProcess.Web/scripts/validate-no-console-in-src.mjs",
    replacement: "Keep as no-console CI gate"
  },
  {
    path: "Frontend/PlantProcess.Web/scripts/validate-ui-system-rollout.mjs",
    replacement: "Keep as UI rollout structural gate"
  },
  {
    path: "Frontend/PlantProcess.Web/tools/ui/validate-ui-standards.mjs",
    replacement: "Keep as UI standards structural gate"
  },
  {
    path: "Frontend/PlantProcess.Web/tools/ui/validate-phase2-full-ui-standards.mjs",
    replacement: "Keep as full UI standards structural gate"
  },
  {
    path: "tools/validation/prove-standard-import-gate.cjs",
    replacement: "Keep as meta-gate proving import-gate enforcement"
  },
  {
    path: "Website/PlantProcess.Website/scripts/validate-website-content.mjs",
    replacement: "Keep as website content structural gate"
  }
];

const addActions = [
  "Backend/tests/PlantProcess.Application.UnitTests/Analytics/RiskScoreServiceTests.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Analytics/FeatureEngineeringServiceTests.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Analytics/MlReadinessServiceTests.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Analytics/QualityLabelBuilderServiceTests.cs",
  "Backend/tests/PlantProcess.Api.IntegrationTests/Analytics/MlLearningCoreIntegrationTests.cs",
  "Backend/tests/PlantProcess.Api.IntegrationTests/Security/AuthGateMatrixTests.cs",
  "Backend/tests/PlantProcess.Infrastructure.IntegrationTests/Database/SqlScriptHygieneApplyTests.cs",
  "Frontend/PlantProcess.Web/src/api/__tests__/apiClient.retry-backoff.test.ts",
  "Frontend/PlantProcess.Web/src/state/__tests__/AuthContext.bootstrap.test.tsx",
  "Frontend/PlantProcess.Web/src/state/__tests__/LicenseContext.gating.test.tsx",
  "Frontend/PlantProcess.Web/src/components/standard/__tests__/DataFetchBoundary.test.tsx",
  "Frontend/PlantProcess.Web/src/pages/PageBuilder/__tests__/pageBuilderReducer.test.ts",
  "Frontend/PlantProcess.Web/e2e/journeys/inspection-to-generated-page.spec.ts",
  "Frontend/PlantProcess.Web/e2e/journeys/assistant-conversation.spec.ts"
];

const modifyActions = [
  "Backend/tests/PlantProcess.Application.UnitTests/Dashboarding/WidgetQueryExpressionServiceTests.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Licensing/LicenseServiceTests.cs",
  "Backend/tests/PlantProcess.Application.UnitTests/Security/SafeSqlPolicyTests.cs",
  "Backend/tests/PlantProcess.Api.IntegrationTests/Import/DeltaImportResumabilityTests.cs",
  "Backend/tests/PlantProcess.Api.IntegrationTests/OpenApi/OpenApiContractTests.cs",
  "Backend/tests/PlantProcess.Api.IntegrationTests/Security/AuthEndpointTests.cs",
  "Backend/tests/PlantProcess.Api.IntegrationTests/Security/SchemaConfigurationSafetyEndpointTests.cs",
  "Backend/tests/PlantProcess.Api.IntegrationTests/Smoke/ApiSmokeEndpointTests.cs",
  "Backend/tests/PlantProcess.Infrastructure.IntegrationTests/Connectors/CsvConnectorSmokeTests.cs",
  "Backend/tests/PlantProcess.Infrastructure.IntegrationTests/Connectors/ExcelConnectorSmokeTests.cs",
  "Frontend/PlantProcess.Web/src/api/legacy/__tests__/plantProcessApi.contract.test.ts",
  "Frontend/PlantProcess.Web/src/components/__tests__/AsyncState.test.tsx",
  "Frontend/PlantProcess.Web/src/components/__tests__/LockedFeatureOverlay.test.tsx",
  "Frontend/PlantProcess.Web/src/components/standard/__tests__/StandardButton.test.tsx",
  "Frontend/PlantProcess.Web/src/components/standard/__tests__/StandardTable.test.tsx",
  "Frontend/PlantProcess.Web/src/components/standard/__tests__/StandardTabs.test.tsx",
  "Frontend/PlantProcess.Web/src/test/integration/mockedApi.integration.test.ts",
  "Frontend/PlantProcess.Web/e2e/phase1-golden-demo.spec.ts",
  "Frontend/PlantProcess.Web/e2e/phase1-security-hardening.spec.ts",
  "Frontend/PlantProcess.Web/e2e/phase2-chart-interaction.spec.ts",
  "Frontend/PlantProcess.Web/e2e/phase2-backend-outage.spec.ts",
  "Frontend/PlantProcess.Web/e2e/p1-risk-dataquality-contract.spec.ts",
  "Frontend/PlantProcess.Web/e2e/dimension2-dimension6-readiness.spec.ts",
  "Frontend/PlantProcess.Web/e2e/phase1-button-action-matrix.spec.ts",
  "Frontend/PlantProcess.Web/e2e/nav-graph-refresh-survival.spec.ts",
  "Frontend/PlantProcess.Web/e2e/p0-auth-pages-contract.spec.ts",
  "Frontend/PlantProcess.Web/e2e/phase1-toast-mapping.spec.ts",
  "Frontend/PlantProcess.Web/e2e/page-builder-v7.spec.ts",
  "Frontend/PlantProcess.Web/e2e/license-gate-ux.spec.ts",
  "Frontend/PlantProcess.Web/e2e/phase56-primary-flows.spec.ts",
  "Frontend/PlantProcess.Web/e2e/phase78-workflow-widget.spec.ts",
  "Frontend/PlantProcess.Web/e2e/api/phase03-two-stage-import.spec.ts",
  "Frontend/PlantProcess.Web/e2e/api/phase02-data-lifecycle-contract.spec.ts"
];

const deleteResults = deleteActions.map((item) => archiveAndDelete(item.path, item.reason));
const retireResults = retireActions.map((item) => prependMarker(item.path, "RETIRE-PENDING-REPLACEMENT", item.replacement));
const transferResults = transferActions.map((item) => prependMarker(item.path, "TRANSFER-TO-REAL-TEST", item.replacement));
const keepGateResults = keepGateActions.map((item) => prependMarker(item.path, "KEEP-AS-STRUCTURAL-CI-GATE", item.replacement));

const registerDoc = `# PlantProcess IQ — P00A Test Register v1

Generated: ${now}

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

${deleteActions.map((x) => `- \`${x.path}\` — ${x.reason}`).join("\n")}

## Retire-Pending-Replacement Items

${retireActions.map((x) => `- \`${x.path}\` → ${x.replacement}`).join("\n")}

## Transfer-To-Real-Test Items

${transferActions.map((x) => `- \`${x.path}\` → ${x.replacement}`).join("\n")}

## Keep-As-Gate Items

${keepGateActions.map((x) => `- \`${x.path}\` — ${x.replacement}`).join("\n")}
`;

write("docs/testing/PlantProcessIQ_Test_Register_v1.md", registerDoc);

const backlogDoc = `# PlantProcess IQ — P00A 77-Item Implementation Backlog

Generated: ${now}

## What is already implemented by the maintenance pack

- 8 DELETE actions archived and removed.
- 11 RETIRE scripts marked.
- 5 TRANSFER→TEST scripts marked.
- 8 KEEP-AS-GATE scripts marked.
- Official Test Register created.

## Remaining behavioural implementation

These are the items that must be implemented as real tests, not as fake/placeholder assertions.

## ADD — 14 new tests

${addActions.map((x, i) => `${i + 1}. \`${x}\``).join("\n")}

## MODIFY — 31 existing tests / journeys

${modifyActions.map((x, i) => `${i + 1}. \`${x}\``).join("\n")}

## Recommended implementation packs

### Pack B — Backend critical behavioural tests

1. MlLearningCoreIntegrationTests.cs
2. AuthGateMatrixTests.cs
3. SqlScriptHygieneApplyTests.cs
4. RiskScoreServiceTests.cs
5. FeatureEngineeringServiceTests.cs
6. MlReadinessServiceTests.cs

### Pack C — Frontend unit behavioural tests

1. apiClient.retry-backoff.test.ts
2. AuthContext.bootstrap.test.tsx
3. LicenseContext.gating.test.tsx
4. DataFetchBoundary.test.tsx
5. StandardButton / StandardTable / StandardTabs strengthening

### Pack D — E2E consolidation

1. Consolidate refresh-survival specs
2. Upgrade page-builder-v7 journey
3. Merge license-gate-ux into license-and-demo-lifecycle
4. Rename phase-number journeys to behaviour journeys
5. Add inspection-to-generated-page after P06
6. Add assistant-conversation after P09

## Rule

Do not mark P00A as complete only because this document exists. P00A is complete only after:

- Maintenance pack validates
- Backend build passes
- Frontend build passes
- Critical Pack B tests are implemented and passing
`;

write("docs/testing/P00A_77_Implementation_Backlog.md", backlogDoc);

const structuralGateRunner = `$ErrorActionPreference = "Stop"

$repo = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
Set-Location $repo

$gates = @(
  "Frontend/PlantProcess.Web/scripts/validate-standard-imports.mjs",
  "Frontend/PlantProcess.Web/scripts/validate-forbidden-copy.mjs",
  "Frontend/PlantProcess.Web/scripts/validate-no-console-in-src.mjs",
  "Frontend/PlantProcess.Web/scripts/validate-ui-system-rollout.mjs",
  "Frontend/PlantProcess.Web/tools/ui/validate-ui-standards.mjs",
  "Frontend/PlantProcess.Web/tools/ui/validate-phase2-full-ui-standards.mjs",
  "tools/validation/prove-standard-import-gate.cjs",
  "Website/PlantProcess.Website/scripts/validate-website-content.mjs"
)

foreach ($gate in $gates) {
  if (Test-Path $gate) {
    Write-Host "Running P00A structural gate: $gate" -ForegroundColor Cyan
    node $gate
  } else {
    Write-Host "Skipping missing P00A structural gate: $gate" -ForegroundColor Yellow
  }
}

Write-Host "P00A structural gates completed." -ForegroundColor Green
`;

write("tools/validation/Run-P00A-StructuralGates.ps1", structuralGateRunner);

const validator = `const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

function p(relativePath) {
  return path.join(root, relativePath.replaceAll("\\\\", path.sep).replaceAll("/", path.sep));
}

function exists(relativePath) {
  return fs.existsSync(p(relativePath));
}

function read(relativePath) {
  return fs.readFileSync(p(relativePath), "utf8");
}

const deleted = ${JSON.stringify(deleteActions.map((x) => x.path), null, 2)};
const retired = ${JSON.stringify(retireActions.map((x) => x.path), null, 2)};
const transfer = ${JSON.stringify(transferActions.map((x) => x.path), null, 2)};
const gates = ${JSON.stringify(keepGateActions.map((x) => x.path), null, 2)};

const failures = [];

for (const item of deleted) {
  if (exists(item)) {
    failures.push("DELETE item still exists: " + item);
  }
}

for (const item of retired) {
  if (exists(item) && !read(item).includes("P00A-TEST-REGISTER: RETIRE-PENDING-REPLACEMENT")) {
    failures.push("RETIRE item missing marker: " + item);
  }
}

for (const item of transfer) {
  if (exists(item) && !read(item).includes("P00A-TEST-REGISTER: TRANSFER-TO-REAL-TEST")) {
    failures.push("TRANSFER item missing marker: " + item);
  }
}

for (const item of gates) {
  if (exists(item) && !read(item).includes("P00A-TEST-REGISTER: KEEP-AS-STRUCTURAL-CI-GATE")) {
    failures.push("KEEP-AS-GATE item missing marker: " + item);
  }
}

if (!exists("docs/testing/PlantProcessIQ_Test_Register_v1.md")) {
  failures.push("Missing docs/testing/PlantProcessIQ_Test_Register_v1.md");
}

if (!exists("docs/testing/P00A_77_Implementation_Backlog.md")) {
  failures.push("Missing docs/testing/P00A_77_Implementation_Backlog.md");
}

if (!exists("tools/validation/Run-P00A-StructuralGates.ps1")) {
  failures.push("Missing tools/validation/Run-P00A-StructuralGates.ps1");
}

if (failures.length > 0) {
  console.error("P00A maintenance validation failed:");
  for (const failure of failures) {
    console.error(" - " + failure);
  }
  process.exit(1);
}

console.log("P00A maintenance validation passed.");
console.log("DELETE actions archived/removed, RETIRE marked, TRANSFER marked, KEEP-AS-GATE marked, docs created.");
`;

write("tools/p00/validate-p00a-test-register-maintenance.cjs", validator);

console.log("");
console.log("P00A Test Register maintenance pack applied.");
console.log("");
console.log("Summary:");
console.log(`DELETE archived/removed: ${deleteResults.filter(x => x.status === "archived-and-deleted").length}`);
console.log(`DELETE already absent: ${deleteResults.filter(x => x.status === "already-absent").length}`);
console.log(`RETIRE marked/already: ${retireResults.filter(x => x.status !== "missing").length}`);
console.log(`TRANSFER marked/already: ${transferResults.filter(x => x.status !== "missing").length}`);
console.log(`KEEP-AS-GATE marked/already: ${keepGateResults.filter(x => x.status !== "missing").length}`);
console.log("");
console.log("Missing files are acceptable only if they were not present in this branch/snapshot.");
console.log("");
console.log("Next commands:");
console.log("node tools/p00/validate-p00a-test-register-maintenance.cjs");
console.log("dotnet build Backend/PlantProcess.sln");
