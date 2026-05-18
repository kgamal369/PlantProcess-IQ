param(
    [string]$Root = "C:\Workspace\PlantProcess-IQ",
    [string]$ApiBaseUrl = "http://localhost:5063",
    [string]$FrontendRoot = "C:\Workspace\PlantProcess-IQ\Frontend\PlantProcess.Web",
    [string]$SolutionPath = "C:\Workspace\PlantProcess-IQ\Backend\PlantProcessIQ.sln"
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Cyan
}

function Invoke-Checked {
    param(
        [string]$Command,
        [string]$WorkingDirectory = $Root
    )

    Write-Host ">> $Command" -ForegroundColor Yellow
    Push-Location $WorkingDirectory

    try {
        cmd.exe /c $Command
        if ($LASTEXITCODE -ne 0) {
            throw "Command failed with exit code $LASTEXITCODE : $Command"
        }
    }
    finally {
        Pop-Location
    }
}

Write-Step "Phase 0 Validation — Backend Build"
Invoke-Checked "dotnet build `"$SolutionPath`" --configuration Debug --no-incremental" $Root

Write-Step "Phase 0 Validation — Frontend Build"
Invoke-Checked "npm install" $FrontendRoot
Invoke-Checked "npm run build" $FrontendRoot

Write-Step "Phase 0 Validation — API Health"
try {
    $health = Invoke-RestMethod -Uri "$ApiBaseUrl/health" -Method Get
    Write-Host "Health OK:" ($health | ConvertTo-Json -Depth 10) -ForegroundColor Green
}
catch {
    throw "Health endpoint failed: $($_.Exception.Message)"
}

Write-Step "Phase 0 Validation — DB Health"
try {
    $dbHealth = Invoke-RestMethod -Uri "$ApiBaseUrl/db-health" -Method Get
    Write-Host "DB Health OK:" ($dbHealth | ConvertTo-Json -Depth 10) -ForegroundColor Green
}
catch {
    throw "DB health endpoint failed: $($_.Exception.Message)"
}

Write-Step "Phase 0 Validation — Critical Endpoint Smoke"
$endpoints = @(
    "/analytics/dashboard/metadata",
    "/analytics/dashboard/definitions?includeInactive=false&includeSystemTemplates=true",
    "/analytics/dashboard/data-quality",
    "/analytics/dashboard/risk",
    "/admin/connectors/provider-types",
    "/admin/jobs-monitor"
)

foreach ($endpoint in $endpoints) {
    try {
        Write-Host "GET $endpoint"
        $response = Invoke-RestMethod -Uri "$ApiBaseUrl$endpoint" -Method Get
        Write-Host "  OK" -ForegroundColor Green
    }
    catch {
        Write-Host "  FAILED: $($_.Exception.Message)" -ForegroundColor Red
        throw
    }
}

Write-Step "Phase 0 Validation Complete"
Write-Host "All Phase 0 build and smoke checks passed." -ForegroundColor Green