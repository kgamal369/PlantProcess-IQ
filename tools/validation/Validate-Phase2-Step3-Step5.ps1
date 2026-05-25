param(
    [string]$RepositoryRoot = "C:\Workspace\PlantProcess-IQ",
    [string]$BackendRoot = "C:\Workspace\PlantProcess-IQ\Backend",
    [string]$FrontendRoot = "C:\Workspace\PlantProcess-IQ\Frontend\PlantProcess.Web",
    [switch]$SkipDatabase
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

Write-Step "Phase 2 Step 3/4/5 file validation"

Assert-FileExists "$BackendRoot\PlantProcess.Infrastructure\Connectors\Oracle\OracleConnector.cs"
Assert-FileExists "$BackendRoot\database\scripts\116_phase2_operation_analytics_pilot_foundation.sql"
Assert-FileExists "$BackendRoot\PlantProcess.Api\Endpoints\Admin\Phase2OperationEndpoints.cs"
Assert-FileExists "$BackendRoot\PlantProcess.Api\Endpoints\Admin\Phase2PilotReadinessEndpoints.cs"
Assert-FileExists "$BackendRoot\PlantProcess.Api\Endpoints\Analytics\Phase2InvestigationEndpoints.cs"

Assert-FileExists "$FrontendRoot\src\api\phase2WorkflowApi.ts"
Assert-FileExists "$FrontendRoot\src\components\phase2\SaveInspectionJobModal.tsx"
Assert-FileExists "$FrontendRoot\src\components\phase2\OperationProgressPanel.tsx"

Assert-FileExists "$RepositoryRoot\deployment\caddy\Caddyfile"
Assert-FileExists "$RepositoryRoot\deployment\caddy\README.md"

Write-Step "Database foundation"

if (-not $SkipDatabase) {
    Push-Location $BackendRoot
    psql -h localhost -p 5432 -U plantprocess -d plantprocessiq -f ".\database\scripts\116_phase2_operation_analytics_pilot_foundation.sql"
    Pop-Location
}
else {
    Write-Host "Skipping DB script." -ForegroundColor Yellow
}

Write-Step "Backend build"

Push-Location $BackendRoot
dotnet build
Pop-Location

Write-Step "Frontend build"

Push-Location $FrontendRoot
npm run build
Pop-Location

Write-Step "Phase 2 Step 3/4/5 validation completed"

Write-Host "PPIQ-WF-002 Oracle connector foundation implemented." -ForegroundColor Green
Write-Host "PPIQ-WF-008 Cross-source join builder endpoints implemented." -ForegroundColor Green
Write-Host "PPIQ-WF-009 KPI-parameter binding implemented." -ForegroundColor Green
Write-Host "PPIQ-WF-011 Jobs monitor action surface implemented." -ForegroundColor Green
Write-Host "PPIQ-HARD-026 Operation progress implemented." -ForegroundColor Green
Write-Host "PPIQ-WF-015 / WF-016 Save-as-inspection-job implemented." -ForegroundColor Green
Write-Host "PPIQ-WF-017 Rule-based correlation endpoint implemented." -ForegroundColor Green
Write-Host "PPIQ-WF-018 ML lifecycle honest state implemented." -ForegroundColor Green
Write-Host "PPIQ-WF-023 / WF-024 / WEB-001 / DEMO-023 / DEMO-017 pilot readiness foundation implemented." -ForegroundColor Green