const fs = require("node:fs");
const path = require("node:path");

const root = process.cwd();

function p(...parts) {
  return path.join(root, ...parts);
}

function write(file, content) {
  fs.mkdirSync(path.dirname(file), { recursive: true });
  fs.writeFileSync(file, content.replace(/\r\n/g, "\n"), "utf8");
}

// ============================================================
// 1. Replace P00A structural gate runner with cwd-safe runner
// ============================================================

write(
  p("tools", "validation", "Run-P00A-StructuralGates.ps1"),
  String.raw`$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$FrontendRoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web"
$WebsiteRoot = Join-Path $RepoRoot "Website\PlantProcess.Website"

function Invoke-P00AGate {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][string]$Command,
        [string[]]$Arguments = @()
    )

    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "Running P00A structural gate: $Name" -ForegroundColor Cyan
    Write-Host "Working directory: $WorkingDirectory" -ForegroundColor DarkCyan
    Write-Host "================================================================" -ForegroundColor Cyan

    if (-not (Test-Path $WorkingDirectory)) {
        throw "Working directory not found for gate '$Name': $WorkingDirectory"
    }

    Push-Location $WorkingDirectory
    try {
        & $Command @Arguments
        $exitCode = $LASTEXITCODE

        if ($null -ne $exitCode -and $exitCode -ne 0) {
            throw "P00A structural gate failed: $Name. ExitCode=$exitCode"
        }

        Write-Host "✅ P00A structural gate passed: $Name" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}

Invoke-P00AGate `
    -Name "Frontend standard imports" `
    -WorkingDirectory $FrontendRoot `
    -Command "node" `
    -Arguments @("scripts\validate-standard-imports.mjs")

Invoke-P00AGate `
    -Name "Frontend forbidden copy" `
    -WorkingDirectory $FrontendRoot `
    -Command "node" `
    -Arguments @("scripts\validate-forbidden-copy.mjs")

Invoke-P00AGate `
    -Name "Frontend no console in src" `
    -WorkingDirectory $FrontendRoot `
    -Command "node" `
    -Arguments @("scripts\validate-no-console-in-src.mjs")

Invoke-P00AGate `
    -Name "Frontend UI system rollout" `
    -WorkingDirectory $FrontendRoot `
    -Command "node" `
    -Arguments @("scripts\validate-ui-system-rollout.mjs")

Invoke-P00AGate `
    -Name "Frontend UI standards" `
    -WorkingDirectory $FrontendRoot `
    -Command "node" `
    -Arguments @("tools\ui\validate-ui-standards.mjs")

Invoke-P00AGate `
    -Name "Frontend Phase 2 full UI standards" `
    -WorkingDirectory $FrontendRoot `
    -Command "node" `
    -Arguments @("tools\ui\validate-phase2-full-ui-standards.mjs")

Invoke-P00AGate `
    -Name "Standard import negative proof" `
    -WorkingDirectory $RepoRoot `
    -Command "node" `
    -Arguments @("tools\validation\prove-standard-import-gate.cjs")

Invoke-P00AGate `
    -Name "Website content" `
    -WorkingDirectory $WebsiteRoot `
    -Command "node" `
    -Arguments @("scripts\validate-website-content.mjs")

Write-Host ""
Write-Host "✅ P00A structural gates completed successfully." -ForegroundColor Green
`
);

// ============================================================
// 2. Fix stale frontend UI rollout gate to current standard paths
// ============================================================

write(
  p("Frontend", "PlantProcess.Web", "scripts", "validate-ui-system-rollout.mjs"),
  String.raw`// P00A-TEST-REGISTER: KEEP-AS-STRUCTURAL-CI-GATE
// Purpose: Current-architecture UI system rollout gate.
// This gate validates the canonical Standard* component layer used by the current frontend.

import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

function fail(message) {
  console.error(`❌ ${message}`);
  process.exit(1);
}

function pass(message) {
  console.log(`✓ ${message}`);
}

function exists(relativePath) {
  return fs.existsSync(path.join(root, relativePath));
}

function requireFile(relativePath) {
  if (!exists(relativePath)) {
    fail(`Missing required UI system file: ${relativePath}`);
  }

  pass(`Exists: ${relativePath}`);
}

const requiredFiles = [
  "src/components/standard/tokens.ts",
  "src/components/standard/standard-components.css",
  "src/components/standard/StandardButton.tsx",
  "src/components/standard/StandardFields.tsx",
  "src/components/standard/StandardTabs.tsx",
  "src/components/standard/StandardTable.tsx",
  "src/components/standard/StandardSurface.tsx",
  "src/components/standard/DataFetchBoundary.tsx",
  "src/components/standard/index.ts",
  "src/components/layout/PageGrid.tsx",
  "src/components/layout/page-grid.css",
  "src/components/skeletons/LoadingSkeletonSet.tsx",
  "src/components/skeletons/Skeleton.css",
];

for (const file of requiredFiles) {
  requireFile(file);
}

const indexCss = fs.readFileSync(path.join(root, "src", "index.css"), "utf8");
const globalCss = exists("src/styles/global.css")
  ? fs.readFileSync(path.join(root, "src/styles/global.css"), "utf8")
  : "";

const cssCorpus = `${indexCss}\n${globalCss}`;

for (const token of ["global.css", "standard-components", "layout.css", "reset.css", "tokens.css"]) {
  if (!cssCorpus.includes(token)) {
    console.warn(`⚠ UI CSS token not found directly in index/global css corpus: ${token}`);
  } else {
    pass(`CSS reference found: ${token}`);
  }
}

function walk(dir) {
  const files = [];

  if (!fs.existsSync(dir)) {
    return files;
  }

  for (const entry of fs.readdirSync(dir, { withFileTypes: true })) {
    const full = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      if (["node_modules", "dist", "coverage", "storybook-static"].includes(entry.name)) {
        continue;
      }

      files.push(...walk(full));
    } else if (/\.(ts|tsx)$/.test(entry.name)) {
      files.push(full);
    }
  }

  return files;
}

const srcFiles = walk(path.join(root, "src"));
const standardImports = srcFiles.filter((file) =>
  fs.readFileSync(file, "utf8").includes("components/standard") ||
  fs.readFileSync(file, "utf8").includes("@/components/standard")
);

if (standardImports.length === 0) {
  fail("No imports/usages of canonical standard components were found.");
}

pass(`Canonical standard component references found in ${standardImports.length} source file(s).`);
console.log("✅ Current UI system rollout validation passed.");
`
);

// ============================================================
// 3. Normalize lightweight UI standards gate to current architecture
// ============================================================

write(
  p("Frontend", "PlantProcess.Web", "tools", "ui", "validate-ui-standards.mjs"),
  String.raw`// P00A-TEST-REGISTER: KEEP-AS-STRUCTURAL-CI-GATE
// Purpose: Lightweight current-architecture UI standards gate.

import fs from "node:fs";
import path from "node:path";

const root = process.cwd();

const requiredFiles = [
  "src/components/standard/tokens.ts",
  "src/components/standard/standard-components.css",
  "src/components/standard/StandardButton.tsx",
  "src/components/standard/StandardFields.tsx",
  "src/components/standard/StandardTabs.tsx",
  "src/components/standard/StandardTable.tsx",
  "src/components/standard/StandardSurface.tsx",
  "src/components/standard/DataFetchBoundary.tsx",
  "src/components/standard/index.ts",
  "src/components/standard/__tests__/StandardButton.test.tsx",
  "src/components/standard/__tests__/StandardTable.test.tsx",
  "src/components/standard/__tests__/StandardTabs.test.tsx",
  "docs/ui-standards/component-specification.md",
  "docs/ui-standards/button-inventory.csv",
  "docs/ui-standards/input-inventory.csv",
  "docs/ui-standards/table-inventory.csv",
  "docs/ui-standards/tabs-inventory.csv",
  "docs/ui-standards/inventory-summary.md",
];

const missing = requiredFiles.filter((file) => !fs.existsSync(path.join(root, file)));

if (missing.length > 0) {
  console.error("Missing current UI standards files:");
  for (const file of missing) {
    console.error(`- ${file}`);
  }
  process.exit(1);
}

const pkg = JSON.parse(fs.readFileSync(path.join(root, "package.json"), "utf8"));

const usefulScripts = [
  "ui:inventory",
  "ui:validate",
  "ui:validate:phase2-full",
  "test:ui-standards",
  "validate:phase2:ui-standards-full",
];

const missingScripts = usefulScripts.filter((script) => !pkg.scripts?.[script]);

if (missingScripts.length > 0) {
  console.error("Missing UI standards npm scripts:");
  for (const script of missingScripts) {
    console.error(`- ${script}`);
  }
  process.exit(1);
}

console.log("✅ Current UI standards structural validation passed.");
`
);

console.log("P00A structural gate cwd + current-architecture validator fixes applied.");
