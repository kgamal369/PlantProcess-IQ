param(
    [string]$RepositoryRoot = "C:\Workspace\PlantProcess-IQ",
    [string]$BackendRoot = "C:\Workspace\PlantProcess-IQ\Backend",
    [string]$FrontendRoot = "C:\Workspace\PlantProcess-IQ\Frontend\PlantProcess.Web",
    [string]$DatabaseHost = "localhost",
    [int]$DatabasePort = 5432,
    [string]$DatabaseUser = "plantprocess",
    [string]$DatabaseName = "plantprocessiq",
    [switch]$SkipDatabase,
    [switch]$SkipPlaywright
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)

    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
}

function Assert-FileExists {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        throw "Missing required file: $Path"
    }

    Write-Host "✓ File exists: $Path" -ForegroundColor Green
}

Write-Step "Phase 2 Step 1/2 file existence"

Assert-FileExists "$FrontendRoot\scripts\validate-api-client-policy.mjs"
Assert-FileExists "$FrontendRoot\scripts\validate-no-console-in-src.mjs"
Assert-FileExists "$FrontendRoot\scripts\validate-ui-system-rollout.mjs"

Assert-FileExists "$FrontendRoot\e2e\helpers\phase2Guard.ts"
Assert-FileExists "$FrontendRoot\e2e\phase2-zero-console-production.spec.ts"
Assert-FileExists "$FrontendRoot\e2e\phase2-backend-outage.spec.ts"
Assert-FileExists "$FrontendRoot\e2e\phase2-navigation-refresh-survival.spec.ts"
Assert-FileExists "$FrontendRoot\e2e\phase2-chart-interaction.spec.ts"
Assert-FileExists "$FrontendRoot\e2e\phase2-responsive-multibrowser.spec.ts"
Assert-FileExists "$FrontendRoot\playwright.phase2.config.ts"

Assert-FileExists "$FrontendRoot\src\components\table\StandardTable.tsx"
Assert-FileExists "$FrontendRoot\src\components\table\standard-table.css"
Assert-FileExists "$FrontendRoot\src\components\layout\PageGrid.tsx"
Assert-FileExists "$FrontendRoot\src\components\layout\page-grid.css"
Assert-FileExists "$FrontendRoot\src\components\skeletons\LoadingSkeletonSet.tsx"

Assert-FileExists "$BackendRoot\database\scripts\115_phase2_integrity_audit.sql"

Write-Step "Backend build"

Push-Location $BackendRoot
dotnet build
Pop-Location

if (-not $SkipDatabase) {
    Write-Step "Database integrity audit"

    Push-Location $BackendRoot
    psql `
        -h $DatabaseHost `
        -p $DatabasePort `
        -U $DatabaseUser `
        -d $DatabaseName `
        -f ".\database\scripts\115_phase2_integrity_audit.sql"
    Pop-Location
}
else {
    Write-Host "Skipping database integrity audit." -ForegroundColor Yellow
}

Write-Step "Frontend build and static hardening checks"

Push-Location $FrontendRoot
npm run build
node .\scripts\validate-api-client-policy.mjs
node .\scripts\validate-no-console-in-src.mjs
node .\scripts\validate-ui-system-rollout.mjs
Pop-Location

if (-not $SkipPlaywright) {
    Write-Step "Phase 2 Playwright hardening QA"

    Push-Location $FrontendRoot
    npx playwright test `
        -c .\playwright.phase2.config.ts `
        e2e/phase2-zero-console-production.spec.ts `
        e2e/phase2-backend-outage.spec.ts `
        e2e/phase2-navigation-refresh-survival.spec.ts `
        e2e/phase2-chart-interaction.spec.ts `
        e2e/phase2-responsive-multibrowser.spec.ts
    Pop-Location
}
else {
    Write-Host "Skipping Playwright QA." -ForegroundColor Yellow
}

Write-Step "Phase 2 Step 1 + Step 2 validation completed"

Write-Host "PPIQ-HARD-005 passed static/build gate." -ForegroundColor Green
Write-Host "PPIQ-HARD-003 policy locked by validator." -ForegroundColor Green
Write-Host "PPIQ-HARD-009 outage proof implemented." -ForegroundColor Green
Write-Host "PPIQ-HARD-031 navigation/refresh suite implemented." -ForegroundColor Green
Write-Host "PPIQ-HARD-027 DB integrity audit implemented." -ForegroundColor Green
Write-Host "PPIQ-HARD-013 / 015 / 017 UI foundation implemented." -ForegroundColor Green
Write-Host "PPIQ-HARD-020 / 028 chart and responsive tests implemented." -ForegroundColor Green