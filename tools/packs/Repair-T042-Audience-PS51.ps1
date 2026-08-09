#requires -Version 5.1
<#
PlantProcess IQ - T-042 Audience repair
Windows PowerShell 5.1 compatible.

Purpose:
  Repair ONLY the broken PageBuilder audience StandardSelect whose options
  currently render without labels.

It forces the canonical StandardSelect contract:
  { value: "...", label: "..." }

Then runs:
  - targeted PageBuilder + workspace projection tests
  - TypeScript build
  - architecture gates
  - T-042 lifecycle browser acceptance

It does NOT stage or commit.
#>

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Stop"

$RepoRoot = "C:\Workspace\PlantProcess-IQ"
$FrontendRoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web"

$Impl = Join-Path $FrontendRoot "src\pages\PageBuilder\PageBuilderPage.implementation.tsx"
$BridgeTest = Join-Path $FrontendRoot "src\pages\PageBuilder\__tests__\pageBuilderBridge.test.tsx"
$LayoutTest = Join-Path $FrontendRoot "src\pages\PageBuilder\__tests__\pageBuilderLayout.test.tsx"
$E2E = Join-Path $FrontendRoot "e2e\t041-page-builder.spec.ts"

$Stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$BackupRoot = Join-Path $RepoRoot ("tools\backups\T042-audience-repair-" + $Stamp)
$TempJs = Join-Path $env:TEMP ("ppiq-t042-audience-repair-" + $Stamp + ".js")

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

Section "T-042 AUDIENCE REPAIR - PREFLIGHT"

Require-File $Impl
Require-File $BridgeTest
Require-File $LayoutTest
Require-File $E2E

if (-not (Test-Path -LiteralPath (Join-Path $FrontendRoot "node_modules"))) {
    throw "Frontend node_modules not found."
}

if ($null -eq (Get-Command node -ErrorAction SilentlyContinue)) {
    throw "Node.js is not available in PATH."
}

Write-Host "[OK] PowerShell $($PSVersionTable.PSVersion)" -ForegroundColor Green

Section "BACKUP CURRENT STATE"

New-Item -ItemType Directory -Force -Path $BackupRoot | Out-Null

Copy-Item -LiteralPath $Impl       -Destination (Join-Path $BackupRoot "PageBuilderPage.implementation.tsx") -Force
Copy-Item -LiteralPath $BridgeTest -Destination (Join-Path $BackupRoot "pageBuilderBridge.test.tsx") -Force
Copy-Item -LiteralPath $LayoutTest -Destination (Join-Path $BackupRoot "pageBuilderLayout.test.tsx") -Force
Copy-Item -LiteralPath $E2E        -Destination (Join-Path $BackupRoot "t041-page-builder.spec.ts") -Force

Write-Host "Backup: $BackupRoot" -ForegroundColor Green

$js = @'
const fs = require("fs");

const implPath = process.argv[2];

function must(condition, message) {
  if (!condition) throw new Error(message);
}

let text = fs.readFileSync(implPath, "utf8");

const audienceRe =
  /<fieldset\b(?=[^>]*data-testid=["']page-audience["'])[^>]*>[\s\S]*?<\/fieldset>/m;

const matches = text.match(
  /<fieldset\b(?=[^>]*data-testid=["']page-audience["'])[^>]*>[\s\S]*?<\/fieldset>/gm
);

must(
  matches && matches.length === 1,
  `Expected exactly one page-audience fieldset; found ${matches ? matches.length : 0}.`
);

// Force the KNOWN-GOOD StandardSelect option contract.
// Do not "skip" an existing StandardSelect, because the current failure is
// precisely an existing StandardSelect with unusable/blank option labels.
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

text = text.replace(audienceRe, replacement);

// Mechanical verification before write.
const newBlock = text.match(audienceRe);
must(newBlock, "Audience block disappeared.");
must(/label=["']Roles["']/.test(newBlock[0]), "Roles label missing.");
must(/\bmultiple\b/.test(newBlock[0]), "multiple missing.");
must(/value=\{state\.audienceRoles\}/.test(newBlock[0]), "audienceRoles binding missing.");

for (const role of ["Admin", "DataManager", "Engineer", "Viewer"]) {
  const re = new RegExp(`\\{\\s*value:\\s*["']${role}["']\\s*,\\s*label:\\s*["']${role}["']\\s*\\}`);
  must(re.test(newBlock[0]), `Canonical { value, label } option missing for ${role}.`);
}

must(!/<input\b/i.test(newBlock[0]), "Raw input remains in page-audience.");

fs.writeFileSync(implPath, text, "utf8");

console.log("[PASS] Audience StandardSelect forced to canonical { value, label } options.");
'@

[System.IO.File]::WriteAllText(
    $TempJs,
    $js,
    (New-Object System.Text.UTF8Encoding($false))
)

try {
    Section "REPAIR PAGEBUILDER AUDIENCE"

    & node $TempJs $Impl

    if ($LASTEXITCODE -ne 0) {
        throw "Audience repair failed with exit code $LASTEXITCODE"
    }
}
catch {
    Write-Host ""
    Write-Host "REPAIR FAILED - restoring PageBuilder implementation." -ForegroundColor Red

    Copy-Item `
        -LiteralPath (Join-Path $BackupRoot "PageBuilderPage.implementation.tsx") `
        -Destination $Impl `
        -Force

    throw
}
finally {
    Remove-Item -LiteralPath $TempJs -Force -ErrorAction SilentlyContinue
}

Section "SHOW REPAIR DIFF ONLY"

Push-Location $RepoRoot
try {
    git --no-pager diff -- `
        "Frontend/PlantProcess.Web/src/pages/PageBuilder/PageBuilderPage.implementation.tsx"
}
finally {
    Pop-Location
}

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
    Write-Host "T-042 REPAIR VALIDATION FAILED" -ForegroundColor Red
    Write-Host "============================================================" -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "Current repair was kept for inspection." -ForegroundColor Yellow
    Write-Host "Backup:" -ForegroundColor Yellow
    Write-Host "  $BackupRoot" -ForegroundColor Yellow
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
Write-Host "Backup: $BackupRoot" -ForegroundColor DarkGray
