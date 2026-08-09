#requires -Version 5.1
<#
PlantProcess IQ - T-042 final architecture closure
Compatible with Windows PowerShell 5.1.

What it changes:
  1) PageBuilder audience raw controls -> StandardSelect multiple
  2) pageBuilderBridge.test.tsx direct .implementation import -> public facade
  3) pageBuilderLayout.test.tsx direct .implementation import -> public facade
  4) Unit/E2E audience interactions -> StandardSelect Roles/options contract

Then it runs:
  - targeted PageBuilder + workspace projection tests
  - TypeScript build
  - the two architecture gates
  - T-042 lifecycle Playwright

It does NOT stage or commit files.
#>

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$RepoRoot = "C:\Workspace\PlantProcess-IQ"
$FrontendRoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web"

$Impl       = Join-Path $FrontendRoot "src\pages\PageBuilder\PageBuilderPage.implementation.tsx"
$BridgeTest = Join-Path $FrontendRoot "src\pages\PageBuilder\__tests__\pageBuilderBridge.test.tsx"
$LayoutTest = Join-Path $FrontendRoot "src\pages\PageBuilder\__tests__\pageBuilderLayout.test.tsx"
$E2E        = Join-Path $FrontendRoot "e2e\t041-page-builder.spec.ts"

$Stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$BackupRoot = Join-Path $RepoRoot ("tools\backups\T042-final-PS51-" + $Stamp)
$TempJs = Join-Path $env:TEMP ("ppiq-t042-final-" + $Stamp + ".js")

function Section([string]$title) {
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor DarkCyan
    Write-Host $title -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor DarkCyan
}

function Require-File([string]$path) {
    if (-not (Test-Path -LiteralPath $path -PathType Leaf)) {
        throw "Required file not found: $path"
    }
}

function Run-Checked([string]$title, [string]$exe, [string[]]$arguments) {
    Section $title
    & $exe @arguments
    if ($LASTEXITCODE -ne 0) {
        throw "$title failed with exit code $LASTEXITCODE"
    }
    Write-Host "[PASS] $title" -ForegroundColor Green
}

Section "T-042 FINAL CLOSURE - PREFLIGHT"

if (-not (Test-Path -LiteralPath (Join-Path $RepoRoot ".git"))) {
    throw "Repo root is not correct: $RepoRoot"
}

Require-File $Impl
Require-File $BridgeTest
Require-File $LayoutTest
Require-File $E2E

if (-not (Test-Path -LiteralPath (Join-Path $FrontendRoot "node_modules"))) {
    throw "Frontend node_modules not found: $FrontendRoot\node_modules"
}

$node = Get-Command node -ErrorAction SilentlyContinue
if ($null -eq $node) {
    throw "Node.js is not available in PATH."
}

Write-Host "[OK] Windows PowerShell $($PSVersionTable.PSVersion)" -ForegroundColor Green
Write-Host "[OK] Node: $($node.Source)" -ForegroundColor Green

Section "CREATE BACKUP"

New-Item -ItemType Directory -Force -Path $BackupRoot | Out-Null

Copy-Item -LiteralPath $Impl       -Destination (Join-Path $BackupRoot "PageBuilderPage.implementation.tsx") -Force
Copy-Item -LiteralPath $BridgeTest -Destination (Join-Path $BackupRoot "pageBuilderBridge.test.tsx") -Force
Copy-Item -LiteralPath $LayoutTest -Destination (Join-Path $BackupRoot "pageBuilderLayout.test.tsx") -Force
Copy-Item -LiteralPath $E2E        -Destination (Join-Path $BackupRoot "t041-page-builder.spec.ts") -Force

Write-Host "Backup: $BackupRoot" -ForegroundColor Green

# Use Node for deterministic UTF-8 source rewriting.
# This keeps the wrapper fully compatible with Windows PowerShell 5.1.
$js = @'
const fs = require("fs");

const files = {
  impl: process.argv[2],
  bridge: process.argv[3],
  layout: process.argv[4],
  e2e: process.argv[5],
};

function read(p) {
  return fs.readFileSync(p, "utf8");
}

function write(p, s) {
  // UTF-8 without BOM
  fs.writeFileSync(p, s, { encoding: "utf8" });
}

function count(text, regex) {
  const m = text.match(regex);
  return m ? m.length : 0;
}

function must(condition, message) {
  if (!condition) throw new Error(message);
}

const original = {
  impl: read(files.impl),
  bridge: read(files.bridge),
  layout: read(files.layout),
  e2e: read(files.e2e),
};

const next = { ...original };

// ---------------------------------------------------------------------------
// 1) Product: raw PageBuilder audience controls -> StandardSelect multiple.
// ---------------------------------------------------------------------------

const audienceRe =
  /<fieldset\b(?=[^>]*data-testid=["']page-audience["'])[^>]*>[\s\S]*?<\/fieldset>/m;

const audienceMatches = next.impl.match(
  /<fieldset\b(?=[^>]*data-testid=["']page-audience["'])[^>]*>[\s\S]*?<\/fieldset>/gm
);

must(
  audienceMatches && audienceMatches.length === 1,
  `Expected exactly one page-audience fieldset; found ${audienceMatches ? audienceMatches.length : 0}.`
);

const currentAudience = audienceMatches[0];

if (
  !/<StandardSelect\b/.test(currentAudience) ||
  !/label=["']Roles["']/.test(currentAudience) ||
  !/\bmultiple\b/.test(currentAudience)
) {
  const replacement = `<fieldset
            className="page-builder-page__audience"
            data-testid="page-audience"
          >
            <legend>Audience roles</legend>

            <p className="page-builder-page__hint">
              Who this page is authored for. Visibility above answers a different question:
              who may open it.
            </p>

            <StandardSelect
              label="Roles"
              multiple
              value={state.audienceRoles}
              options={[
                { value: "Admin", label: "Admin" },
                { value: "DataManager", label: "DataManager" },
                { value: "Engineer", label: "Engineer" },
                { value: "Viewer", label: "Viewer" },
              ]}
              onChange={(value) =>
                dispatch({
                  type: "updateMeta",
                  patch: {
                    audienceRoles: Array.isArray(value) ? value : [value],
                  },
                })
              }
            />

            {state.audienceRoles.length === 0 ? (
              <p data-testid="page-audience-required" role="status">
                Choose at least one audience role before adding widgets.
              </p>
            ) : null}
          </fieldset>`;

  next.impl = next.impl.replace(audienceRe, replacement);
  console.log("[PATCH] PageBuilder audience -> StandardSelect multiple");
} else {
  console.log("[SKIP] PageBuilder audience is already StandardSelect multiple");
}

// StandardSelect is already used by PageBuilder for Visibility in the current
// implementation. If that contract changes, fail instead of inventing imports.
must(
  /<StandardSelect\b/.test(next.impl),
  "StandardSelect is not available in PageBuilder implementation."
);

// ---------------------------------------------------------------------------
// 2) Direct test imports -> public PageBuilder facade.
// ---------------------------------------------------------------------------

for (const key of ["bridge", "layout"]) {
  let text = next[key];

  text = text.replace(
    /(["'])\.\.\/PageBuilderPage\.implementation\1/g,
    (_m, q) => `${q}../PageBuilderPage${q}`
  );

  next[key] = text;
}

console.log("[PATCH] PageBuilder bridge/layout tests -> public facade imports");

// ---------------------------------------------------------------------------
// 3) Unit-test audience interaction -> StandardSelect contract.
// ---------------------------------------------------------------------------

function replaceTestingLibraryRole(text, role) {
  // Exact common statement from the current tests.
  const oldDouble = `fireEvent.click(screen.getByLabelText("${role}"));`;
  const oldSingle = `fireEvent.click(screen.getByLabelText('${role}'));`;

  const replacement =
`fireEvent.click(screen.getByLabelText("Roles"));
    fireEvent.click(screen.getByRole("option", { name: "${role}" }));`;

  text = text.split(oldDouble).join(replacement);
  text = text.split(oldSingle).join(replacement);

  return text;
}

for (const key of ["bridge", "layout"]) {
  next[key] = replaceTestingLibraryRole(next[key], "Engineer");
  next[key] = replaceTestingLibraryRole(next[key], "Admin");
}

console.log("[PATCH] Unit tests -> Roles + option audience interaction");

// ---------------------------------------------------------------------------
// 4) Playwright audience interaction -> StandardSelect contract.
// ---------------------------------------------------------------------------

function replacePlaywrightCheckbox(text, role) {
  // Handles compact and multiline chains:
  // await page.getByTestId("page-audience").getByLabel("Admin").check();
  const re = new RegExp(
    `await\\s+page\\s*` +
    `\\.getByTestId\\(["']page-audience["']\\)\\s*` +
    `\\.getByLabel\\(["']${role}["']\\)\\s*` +
    `\\.check\\(\\);`,
    "gm"
  );

  const replacement =
`await page
      .getByTestId("page-audience")
      .getByLabel("Roles")
      .click();

    await page
      .getByRole("option", { name: "${role}", exact: true })
      .click();`;

  return text.replace(re, replacement);
}

next.e2e = replacePlaywrightCheckbox(next.e2e, "Admin");
next.e2e = replacePlaywrightCheckbox(next.e2e, "Engineer");

console.log("[PATCH] E2E -> Roles + option audience interaction");

// ---------------------------------------------------------------------------
// Mechanical verification BEFORE writes.
// ---------------------------------------------------------------------------

const newAudience = next.impl.match(audienceRe);
must(newAudience, "page-audience fieldset disappeared after transformation.");
must(/<StandardSelect\b/.test(newAudience[0]), "Audience is not StandardSelect.");
must(/label=["']Roles["']/.test(newAudience[0]), "Audience StandardSelect label is not Roles.");
must(/\bmultiple\b/.test(newAudience[0]), "Audience StandardSelect is not multiple.");
must(/value=\{state\.audienceRoles\}/.test(newAudience[0]), "Audience is not bound to state.audienceRoles.");
must(!/<input\b/i.test(newAudience[0]), "Raw <input> remains inside page-audience.");

for (const key of ["bridge", "layout"]) {
  must(
    !/PageBuilderPage\.implementation/.test(next[key]),
    `${key} still imports PageBuilderPage.implementation directly.`
  );

  must(
    !/getByLabelText\(["'](?:Engineer|Admin)["']\)/.test(next[key]),
    `${key} still uses checkbox-style audience label access.`
  );
}

must(
  !/getByLabel\(["'](?:Admin|Engineer)["']\)\s*\.check\(\)/m.test(next.e2e),
  "E2E still contains checkbox-style Admin/Engineer audience interaction."
);

// Ensure the E2E still contains the already-certified T-042 corrections.
const executableE2E = next.e2e
  .replace(/^\s*\/\/.*$/gm, "")
  .replace(/\/\*[\s\S]*?\*\//g, "");

must(
  !/selectOption\(\{\s*index\s*:/.test(executableE2E),
  "Executable positional selectOption({ index: ... }) returned to the T-042 E2E."
);
must(
  /label:\s*["']Bar["']/.test(next.e2e) &&
  /label:\s*["']Defect Type["']/.test(next.e2e) &&
  /label:\s*["']Defect Count \(defects\)["']/.test(next.e2e),
  "Named Bar / Defect Type / Defect Count acceptance fixture is missing."
);
must(
  /function\s+builderStatus\s*\(/.test(next.e2e),
  "builderStatus helper is missing from the T-042 E2E."
);

// Only now write all four files.
write(files.impl, next.impl);
write(files.bridge, next.bridge);
write(files.layout, next.layout);
write(files.e2e, next.e2e);

console.log("[PASS] Four T-042 source corrections written successfully.");
'@

[System.IO.File]::WriteAllText($TempJs, $js, (New-Object System.Text.UTF8Encoding($false)))

try {
    Section "APPLY FOUR FINAL T-042 SOURCE CORRECTIONS"

    & node $TempJs $Impl $BridgeTest $LayoutTest $E2E

    if ($LASTEXITCODE -ne 0) {
        throw "Source transformation failed with exit code $LASTEXITCODE"
    }
}
catch {
    Write-Host ""
    Write-Host "PATCH FAILED - restoring the four backed-up files." -ForegroundColor Red

    Copy-Item -LiteralPath (Join-Path $BackupRoot "PageBuilderPage.implementation.tsx") -Destination $Impl -Force
    Copy-Item -LiteralPath (Join-Path $BackupRoot "pageBuilderBridge.test.tsx") -Destination $BridgeTest -Force
    Copy-Item -LiteralPath (Join-Path $BackupRoot "pageBuilderLayout.test.tsx") -Destination $LayoutTest -Force
    Copy-Item -LiteralPath (Join-Path $BackupRoot "t041-page-builder.spec.ts") -Destination $E2E -Force

    throw
}
finally {
    Remove-Item -LiteralPath $TempJs -Force -ErrorAction SilentlyContinue
}

Section "SHOW OWNED DIFF"

Push-Location $RepoRoot
try {
    git diff -- `
        "Frontend/PlantProcess.Web/src/pages/PageBuilder/PageBuilderPage.implementation.tsx" `
        "Frontend/PlantProcess.Web/src/pages/PageBuilder/__tests__/pageBuilderBridge.test.tsx" `
        "Frontend/PlantProcess.Web/src/pages/PageBuilder/__tests__/pageBuilderLayout.test.tsx" `
        "Frontend/PlantProcess.Web/e2e/t041-page-builder.spec.ts"
}
finally {
    Pop-Location
}

# ---------------------------------------------------------------------------
# Validation
# ---------------------------------------------------------------------------

Push-Location $FrontendRoot
try {
    Run-Checked `
        "Targeted PageBuilder + Workspace Projection Tests" `
        "node" `
        @(
            "node_modules\vitest\vitest.mjs",
            "run",
            "src/pages/PageBuilder",
            "src/components/__tests__/workspaceProjection.test.tsx",
            "--config",
            "vitest.config.ts"
        )

    Run-Checked `
        "TypeScript Build" `
        "node" `
        @(
            "node_modules\typescript\bin\tsc",
            "-b"
        )

    Run-Checked `
        "T-042 Architecture Gates" `
        "node" `
        @(
            "node_modules\vitest\vitest.mjs",
            "run",
            "src/test/architecture/uiConformanceRatchet.test.ts",
            "src/test/architecture/largeFileBoundaries.test.ts",
            "--config",
            "vitest.config.ts"
        )

    Run-Checked `
        "T-042 Lifecycle Browser Acceptance" `
        "node" `
        @(
            "node_modules\@playwright\test\cli.js",
            "test",
            "--config=playwright.t040.config.ts",
            "-g",
            "T-042 the page lifecycle"
        )
}
catch {
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Red
    Write-Host "T-042 VALIDATION FAILED" -ForegroundColor Red
    Write-Host "============================================================" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "The patch was kept for inspection." -ForegroundColor Yellow
    Write-Host "Backup is available here:" -ForegroundColor Yellow
    Write-Host "  $BackupRoot" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Do not start a broad investigation. Use only the exact failing gate above." -ForegroundColor Yellow
    exit 1
}
finally {
    Pop-Location
}

Section "T-042 FINAL RESULT"

Write-Host "Targeted PageBuilder tests : PASS" -ForegroundColor Green
Write-Host "TypeScript build           : PASS" -ForegroundColor Green
Write-Host "Architecture gates         : PASS" -ForegroundColor Green
Write-Host "T-042 lifecycle browser    : PASS" -ForegroundColor Green
Write-Host ""
Write-Host "T-042 = DONE / FROZEN" -ForegroundColor Green
Write-Host ""
Write-Host "Nothing was staged or committed." -ForegroundColor Yellow
Write-Host "Backup:" -ForegroundColor DarkGray
Write-Host "  $BackupRoot" -ForegroundColor DarkGray
