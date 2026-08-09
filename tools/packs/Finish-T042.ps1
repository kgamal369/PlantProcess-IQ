#requires -Version 7.0

<#
.SYNOPSIS
    PlantProcess IQ - T-042 FINAL architecture closure pack.

.DESCRIPTION
    Applies ONLY the final known T-042 architecture corrections:

      1. PageBuilder audience raw controls
         -> StandardSelect multiple.

      2. pageBuilderBridge.test.tsx direct implementation import
         -> public PageBuilderPage facade.

      3. pageBuilderLayout.test.tsx direct implementation import
         -> public PageBuilderPage facade.

      4. Unit + Playwright audience interactions
         -> StandardSelect "Roles" / role="option" interaction.

    Then validates:

      - targeted PageBuilder + workspaceProjection tests
      - TypeScript build
      - UI conformance + large-file architecture gates
      - T-042 lifecycle Playwright

    This script:
      - creates backups
      - does NOT git add
      - does NOT commit
      - does NOT touch backend
      - does NOT touch T-046 / T-053 / T-040
#>

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

# ---------------------------------------------------------------------------
# Configuration
# ---------------------------------------------------------------------------

$RepoRoot = "C:\Workspace\PlantProcess-IQ"
$FrontendRoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web"

$PageBuilderImpl = Join-Path $FrontendRoot "src\pages\PageBuilder\PageBuilderPage.implementation.tsx"
$ReducerFile     = Join-Path $FrontendRoot "src\pages\PageBuilder\pageBuilderReducer.ts"
$BridgeTest      = Join-Path $FrontendRoot "src\pages\PageBuilder\__tests__\pageBuilderBridge.test.tsx"
$LayoutTest      = Join-Path $FrontendRoot "src\pages\PageBuilder\__tests__\pageBuilderLayout.test.tsx"
$E2ETest         = Join-Path $FrontendRoot "e2e\t041-page-builder.spec.ts"

$Timestamp = Get-Date -Format "yyyyMMdd_HHmmss"
$BackupRoot = Join-Path $RepoRoot "tools\backups\T042-final-$Timestamp"

$Utf8NoBom = New-Object System.Text.UTF8Encoding($false)

# ---------------------------------------------------------------------------
# Helpers
# ---------------------------------------------------------------------------

function Write-Step {
    param([string]$Message)

    Write-Host ""
    Write-Host "================================================================" -ForegroundColor DarkCyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor DarkCyan
}

function Assert-File {
    param([string]$Path)

    if (-not (Test-Path -LiteralPath $Path -PathType Leaf)) {
        throw "Required file does not exist: $Path"
    }
}

function Read-Text {
    param([string]$Path)

    return [System.IO.File]::ReadAllText($Path)
}

function Write-Text {
    param(
        [string]$Path,
        [string]$Content
    )

    [System.IO.File]::WriteAllText($Path, $Content, $Utf8NoBom)
}

function Backup-File {
    param([string]$Path)

    $relative = [System.IO.Path]::GetRelativePath($RepoRoot, $Path)
    $dest = Join-Path $BackupRoot $relative
    $destDir = Split-Path $dest -Parent

    New-Item -ItemType Directory -Force -Path $destDir | Out-Null
    Copy-Item -LiteralPath $Path -Destination $dest -Force
}

function Restore-Backups {
    Write-Host ""
    Write-Host "Restoring files from backup..." -ForegroundColor Yellow

    foreach ($source in Get-ChildItem $BackupRoot -Recurse -File) {
        $relative = [System.IO.Path]::GetRelativePath($BackupRoot, $source.FullName)
        $dest = Join-Path $RepoRoot $relative

        Copy-Item -LiteralPath $source.FullName -Destination $dest -Force
    }

    Write-Host "Backup restoration completed." -ForegroundColor Yellow
}

function Replace-Literal-All {
    param(
        [string]$Text,
        [string]$Old,
        [string]$New,
        [string]$Description,
        [switch]$AllowZero
    )

    $count = 0
    $position = 0

    while (($index = $Text.IndexOf($Old, $position, [System.StringComparison]::Ordinal)) -ge 0) {
        $count++
        $position = $index + $Old.Length
    }

    if ($count -eq 0) {
        if ($AllowZero) {
            Write-Host "[SKIP] $Description - already clean or not present." -ForegroundColor DarkGray
            return @{
                Text  = $Text
                Count = 0
            }
        }

        throw "Precondition failed: expected text not found for '$Description'."
    }

    $updated = $Text.Replace($Old, $New)

    Write-Host "[PATCH] $Description : $count replacement(s)" -ForegroundColor Green

    return @{
        Text  = $updated
        Count = $count
    }
}

function Invoke-NativeChecked {
    param(
        [string]$Title,
        [string]$Executable,
        [string[]]$Arguments
    )

    Write-Step $Title

    & $Executable @Arguments

    if ($LASTEXITCODE -ne 0) {
        throw "$Title failed with exit code $LASTEXITCODE."
    }

    Write-Host "[PASS] $Title" -ForegroundColor Green
}

# ---------------------------------------------------------------------------
# Preflight
# ---------------------------------------------------------------------------

Write-Step "T-042 FINAL CLOSURE - PREFLIGHT"

if (-not (Test-Path (Join-Path $RepoRoot ".git"))) {
    throw "Repo root does not look correct: $RepoRoot"
}

foreach ($file in @(
    $PageBuilderImpl,
    $ReducerFile,
    $BridgeTest,
    $LayoutTest,
    $E2ETest
)) {
    Assert-File $file
}

if (-not (Test-Path (Join-Path $FrontendRoot "node_modules"))) {
    throw "Frontend node_modules is missing: $FrontendRoot\node_modules"
}

$reducer = Read-Text $ReducerFile

if ($reducer -notmatch 'audienceRoles') {
    throw @"
pageBuilderReducer.ts does not contain audienceRoles.

STOPPING SAFELY.

T-042 S6/S7 audience state is expected before this final pack.
No files have been changed.
"@
}

if ($reducer -notmatch 'type:\s*"updateMeta"') {
    throw @"
pageBuilderReducer.ts does not expose the expected updateMeta action.

STOPPING SAFELY.

No files have been changed.
"@
}

$pageText = Read-Text $PageBuilderImpl

if ($pageText -notmatch 'StandardSelect') {
    throw "PageBuilderPage.implementation.tsx does not import/use StandardSelect."
}

if ($pageText -notmatch 'data-testid="page-audience"') {
    throw "Could not find page-audience fieldset in PageBuilderPage.implementation.tsx."
}

Write-Host "[OK] Repo and expected T-042 source shape confirmed." -ForegroundColor Green

# ---------------------------------------------------------------------------
# Backup
# ---------------------------------------------------------------------------

Write-Step "CREATE BACKUPS"

New-Item -ItemType Directory -Force -Path $BackupRoot | Out-Null

foreach ($file in @(
    $PageBuilderImpl,
    $BridgeTest,
    $LayoutTest,
    $E2ETest
)) {
    Backup-File $file
}

Write-Host "Backup created:" -ForegroundColor Green
Write-Host "  $BackupRoot"

# ---------------------------------------------------------------------------
# PATCH PHASE
# ---------------------------------------------------------------------------

try {

    # =======================================================================
    # 1. PageBuilder audience -> StandardSelect multiple
    # =======================================================================

    Write-Step "PATCH 1/4 - PAGEBUILDER AUDIENCE CONTROL"

    $pageText = Read-Text $PageBuilderImpl

    # -----------------------------------------------------------------------
    # Add canonical audience options if they do not already exist.
    # -----------------------------------------------------------------------

    if ($pageText -notmatch 'const\s+audienceRoleOptions\s*=') {

        $visibilityPattern = '(?s)(const\s+visibilityOptions\s*=\s*\[.*?\]\s+as\s+const;)'

        $visibilityMatch = [regex]::Match($pageText, $visibilityPattern)

        if (-not $visibilityMatch.Success) {
            throw "Could not locate visibilityOptions insertion point."
        }

        $audienceOptions = @'

const audienceRoleOptions = [
  { value: "Admin", label: "Admin" },
  { value: "DataManager", label: "DataManager" },
  { value: "Engineer", label: "Engineer" },
  { value: "Viewer", label: "Viewer" },
] as const;
'@

        $pageText =
            $pageText.Substring(0, $visibilityMatch.Index + $visibilityMatch.Length) +
            $audienceOptions +
            $pageText.Substring($visibilityMatch.Index + $visibilityMatch.Length)

        Write-Host "[PATCH] Added audienceRoleOptions." -ForegroundColor Green
    }
    else {
        Write-Host "[SKIP] audienceRoleOptions already exists." -ForegroundColor DarkGray
    }

    # -----------------------------------------------------------------------
    # Replace ONLY the PageBuilder audience fieldset.
    # -----------------------------------------------------------------------

    $audiencePattern =
        '(?s)<fieldset(?=[^>]*data-testid="page-audience")[^>]*>.*?</fieldset>'

    $audienceMatches = [regex]::Matches($pageText, $audiencePattern)

    if ($audienceMatches.Count -ne 1) {
        throw "Expected exactly ONE page-audience fieldset; found $($audienceMatches.Count)."
    }

    $currentAudienceBlock = $audienceMatches[0].Value

    if (
        $currentAudienceBlock -match '<StandardSelect' -and
        $currentAudienceBlock -match 'label="Roles"' -and
        $currentAudienceBlock -match '\bmultiple\b'
    ) {
        Write-Host "[SKIP] Audience already uses StandardSelect multiple." -ForegroundColor DarkGray
    }
    else {

        # The reducer already carries T-042 audienceRoles.
        # updateMeta remains the canonical metadata update action.
        $newAudienceBlock = @'
<fieldset
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
              options={audienceRoleOptions}
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
          </fieldset>
'@

        $pageText =
            $pageText.Substring(0, $audienceMatches[0].Index) +
            $newAudienceBlock +
            $pageText.Substring(
                $audienceMatches[0].Index + $audienceMatches[0].Length
            )

        Write-Host "[PATCH] Replaced raw audience controls with StandardSelect multiple." -ForegroundColor Green
    }

    Write-Text $PageBuilderImpl $pageText

    # =======================================================================
    # 2. Unit tests -> public facade imports + StandardSelect interactions
    # =======================================================================

    Write-Step "PATCH 2/4 - PAGEBUILDER UNIT TESTS"

    foreach ($testFile in @($BridgeTest, $LayoutTest)) {

        $text = Read-Text $testFile

        # Direct implementation import -> public facade.
        $result = Replace-Literal-All `
            -Text $text `
            -Old '../PageBuilderPage.implementation' `
            -New '../PageBuilderPage' `
            -Description "$(Split-Path $testFile -Leaf): public facade import" `
            -AllowZero

        $text = $result.Text

        # Double-quoted form.
        $result = Replace-Literal-All `
            -Text $text `
            -Old 'fireEvent.click(screen.getByLabelText("Engineer"));' `
            -New @'
fireEvent.click(screen.getByLabelText("Roles"));
  fireEvent.click(screen.getByRole("option", { name: "Engineer" }));
'@ `
            -Description "$(Split-Path $testFile -Leaf): Engineer StandardSelect interaction" `
            -AllowZero

        $text = $result.Text

        # Single-quoted form if any exists.
        $result = Replace-Literal-All `
            -Text $text `
            -Old "fireEvent.click(screen.getByLabelText('Engineer'));" `
            -New @"
fireEvent.click(screen.getByLabelText("Roles"));
  fireEvent.click(screen.getByRole("option", { name: "Engineer" }));
"@ `
            -Description "$(Split-Path $testFile -Leaf): Engineer StandardSelect interaction (single quote)" `
            -AllowZero

        $text = $result.Text

        Write-Text $testFile $text
    }

    # =======================================================================
    # 3. E2E audience interactions -> StandardSelect
    # =======================================================================

    Write-Step "PATCH 3/4 - T-041/T-042 E2E AUDIENCE INTERACTION"

    $e2e = Read-Text $E2ETest

    function Replace-E2EAudienceRole {
        param(
            [string]$Text,
            [string]$Role
        )

        # Handles the compact form:
        # await page.getByTestId("page-audience").getByLabel("Admin").check();
        #
        # and the multi-line chained form.
        $pattern =
            '(?ms)await\s+page\s*' +
            '\.getByTestId\("page-audience"\)\s*' +
            '\.getByLabel\("' + [regex]::Escape($Role) + '"\)\s*' +
            '\.check\(\);'

        $replacement = @"
await page
      .getByTestId("page-audience")
      .getByLabel("Roles")
      .click();

    await page
      .getByRole("option", { name: "$Role", exact: true })
      .click();
"@

        $matches = [regex]::Matches($Text, $pattern)

        if ($matches.Count -gt 0) {
            Write-Host "[PATCH] E2E $Role audience: $($matches.Count) replacement(s)" -ForegroundColor Green
            return [regex]::Replace($Text, $pattern, $replacement)
        }

        if (
            $Text -match 'getByLabel\("Roles"\)' -and
            $Text -match ('name:\s*"' + [regex]::Escape($Role) + '"')
        ) {
            Write-Host "[SKIP] E2E $Role already uses StandardSelect interaction." -ForegroundColor DarkGray
            return $Text
        }

        Write-Host "[WARN] No E2E $Role checkbox interaction found." -ForegroundColor Yellow
        return $Text
    }

    $e2e = Replace-E2EAudienceRole -Text $e2e -Role "Admin"
    $e2e = Replace-E2EAudienceRole -Text $e2e -Role "Engineer"

    Write-Text $E2ETest $e2e

    # =======================================================================
    # 4. Mechanical source verification
    # =======================================================================

    Write-Step "PATCH 4/4 - MECHANICAL SOURCE VERIFICATION"

    $pageCheck   = Read-Text $PageBuilderImpl
    $bridgeCheck = Read-Text $BridgeTest
    $layoutCheck = Read-Text $LayoutTest
    $e2eCheck    = Read-Text $E2ETest

    $problems = New-Object System.Collections.Generic.List[string]

    if ($pageCheck -notmatch 'label="Roles"') {
        $problems.Add("PageBuilder does not contain StandardSelect label=Roles.")
    }

    if ($pageCheck -notmatch '\bmultiple\b') {
        $problems.Add("PageBuilder audience StandardSelect is not multiple.")
    }

    if ($pageCheck -notmatch 'value=\{state\.audienceRoles\}') {
        $problems.Add("PageBuilder audience is not bound to state.audienceRoles.")
    }

    if ($bridgeCheck -match 'PageBuilderPage\.implementation') {
        $problems.Add("pageBuilderBridge.test.tsx still imports implementation directly.")
    }

    if ($layoutCheck -match 'PageBuilderPage\.implementation') {
        $problems.Add("pageBuilderLayout.test.tsx still imports implementation directly.")
    }

    if ($bridgeCheck -match 'getByLabelText\(["'']Engineer["'']\)') {
        $problems.Add("pageBuilderBridge.test.tsx still uses Engineer checkbox semantics.")
    }

    if ($layoutCheck -match 'getByLabelText\(["'']Engineer["'']\)') {
        $problems.Add("pageBuilderLayout.test.tsx still uses Engineer checkbox semantics.")
    }

    if ($e2eCheck -match 'getByLabel\(["''](?:Admin|Engineer)["'']\)\s*\.check') {
        $problems.Add("T-041/T-042 E2E still contains raw audience checkbox interaction.")
    }

    if ($problems.Count -gt 0) {
        throw (
            "Mechanical verification failed:`n - " +
            ($problems -join "`n - ")
        )
    }

    Write-Host "[PASS] All four source corrections are present." -ForegroundColor Green
}
catch {
    Write-Host ""
    Write-Host "PATCH PHASE FAILED." -ForegroundColor Red
    Write-Host $_.Exception.Message -ForegroundColor Red

    Restore-Backups

    throw "No partial patch was kept. Source files were restored."
}

# ---------------------------------------------------------------------------
# Show diff BEFORE validation
# ---------------------------------------------------------------------------

Write-Step "SOURCE DIFF SUMMARY"

Push-Location $RepoRoot

git diff -- `
    "Frontend/PlantProcess.Web/src/pages/PageBuilder/PageBuilderPage.implementation.tsx" `
    "Frontend/PlantProcess.Web/src/pages/PageBuilder/__tests__/pageBuilderBridge.test.tsx" `
    "Frontend/PlantProcess.Web/src/pages/PageBuilder/__tests__/pageBuilderLayout.test.tsx" `
    "Frontend/PlantProcess.Web/e2e/t041-page-builder.spec.ts"

Pop-Location

# ---------------------------------------------------------------------------
# Validation
#
# IMPORTANT:
# Validation failure DOES NOT rollback automatically.
# We keep the corrected source + backups for inspection.
# ---------------------------------------------------------------------------

try {
    Push-Location $FrontendRoot

    # 1. Targeted PageBuilder regressions.
    Invoke-NativeChecked `
        -Title "Targeted PageBuilder + Workspace Projection Tests" `
        -Executable "node" `
        -Arguments @(
            "node_modules\vitest\vitest.mjs",
            "run",
            "src/pages/PageBuilder",
            "src/components/__tests__/workspaceProjection.test.tsx",
            "--config",
            "vitest.config.ts"
        )

    # 2. TypeScript.
    Invoke-NativeChecked `
        -Title "TypeScript Build" `
        -Executable "node" `
        -Arguments @(
            "node_modules\typescript\bin\tsc",
            "-b"
        )

    # 3. Architecture.
    Invoke-NativeChecked `
        -Title "T-042 Architecture Gates" `
        -Executable "node" `
        -Arguments @(
            "node_modules\vitest\vitest.mjs",
            "run",
            "src/test/architecture/uiConformanceRatchet.test.ts",
            "src/test/architecture/largeFileBoundaries.test.ts",
            "--config",
            "vitest.config.ts"
        )

    # 4. Final T-042 browser acceptance.
    Invoke-NativeChecked `
        -Title "T-042 Lifecycle Browser Acceptance" `
        -Executable "node" `
        -Arguments @(
            "node_modules\@playwright\test\cli.js",
            "test",
            "--config=playwright.t040.config.ts",
            "-g",
            "T-042 the page lifecycle"
        )

    Pop-Location
}
catch {
    if ((Get-Location).Path -eq $FrontendRoot) {
        Pop-Location
    }

    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Red
    Write-Host "T-042 FINAL VALIDATION FAILED" -ForegroundColor Red
    Write-Host "================================================================" -ForegroundColor Red
    Write-Host ""
    Write-Host $_.Exception.Message -ForegroundColor Red
    Write-Host ""
    Write-Host "The patch was NOT automatically rolled back." -ForegroundColor Yellow
    Write-Host "Backups are available at:" -ForegroundColor Yellow
    Write-Host "  $BackupRoot" -ForegroundColor Yellow
    Write-Host ""
    Write-Host "Do NOT start another broad investigation." -ForegroundColor Yellow

    exit 1
}

# ---------------------------------------------------------------------------
# Final source / architecture sanity
# ---------------------------------------------------------------------------

Write-Step "FINAL T-042 SANITY"

Push-Location $FrontendRoot

Write-Host ""
Write-Host "Direct implementation imports remaining:" -ForegroundColor Cyan

$directImports = Get-ChildItem `
    "src\pages\PageBuilder\__tests__" `
    -File `
    -Filter "*.tsx" |
    Select-String -Pattern "PageBuilderPage\.implementation"

if ($directImports) {
    $directImports
    Pop-Location
    throw "Direct implementation imports remain."
}
else {
    Write-Host "  NONE" -ForegroundColor Green
}

Write-Host ""
Write-Host "Raw PageBuilder audience checkbox interactions remaining in E2E:" -ForegroundColor Cyan

$rawAudienceE2E = Select-String `
    -Path "e2e\t041-page-builder.spec.ts" `
    -Pattern 'getByLabel\("(Admin|Engineer)"\)\.check'

if ($rawAudienceE2E) {
    $rawAudienceE2E
    Pop-Location
    throw "Raw E2E audience checkbox interactions remain."
}
else {
    Write-Host "  NONE" -ForegroundColor Green
}

Pop-Location

# ---------------------------------------------------------------------------
# Done
# ---------------------------------------------------------------------------

Write-Host ""
Write-Host "################################################################" -ForegroundColor Green
Write-Host "#                                                              #" -ForegroundColor Green
Write-Host "#                   T-042 FINAL GATES GREEN                    #" -ForegroundColor Green
Write-Host "#                                                              #" -ForegroundColor Green
Write-Host "################################################################" -ForegroundColor Green
Write-Host ""
Write-Host "Targeted PageBuilder tests : PASS" -ForegroundColor Green
Write-Host "TypeScript                : PASS" -ForegroundColor Green
Write-Host "Architecture gates        : PASS" -ForegroundColor Green
Write-Host "T-042 browser lifecycle   : PASS" -ForegroundColor Green
Write-Host ""
Write-Host "T-042 can now be marked DONE / FROZEN." -ForegroundColor Green
Write-Host ""
Write-Host "No files were staged or committed." -ForegroundColor Yellow
Write-Host "Backup:" -ForegroundColor DarkGray
Write-Host "  $BackupRoot" -ForegroundColor DarkGray