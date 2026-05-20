#requires -Version 5.1
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "============================================================" -ForegroundColor Cyan
    Write-Host $Title -ForegroundColor Cyan
    Write-Host "============================================================" -ForegroundColor Cyan
}

$Root = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$Backend = Join-Path $Root "Backend"
$Frontend = Join-Path $Root "Frontend\PlantProcess.Web"
$Website = Join-Path $Root "Website\PlantProcess.Website"
$Deploy = Join-Path $Root "Infrastructure\deploy"

Write-Section "PlantProcess IQ — Local Full Validation"
Write-Host "Root: $Root"

Write-Section "Backend Restore"
Push-Location $Backend
dotnet restore PlantProcessIQ.sln

Write-Section "Backend Build"
dotnet build PlantProcessIQ.sln -c Release --no-restore

Write-Section "Backend Tests"
dotnet test PlantProcessIQ.sln `
    -c Release `
    --no-build `
    --settings coverlet.runsettings `
    --collect:"XPlat Code Coverage" `
    --logger "trx;LogFileName=backend-tests.trx" `
    --results-directory ./TestResults
Pop-Location

Write-Section "Frontend App Validate"
Push-Location $Frontend
npm ci
npm run build
npm run lint
npm run test
npm run language:audit
Pop-Location

Write-Section "Website Build"
Push-Location $Website
npm ci
npm run build
Pop-Location

Write-Section "Docker Compose Config"
Push-Location $Deploy
docker compose -f docker-compose.demo.yml config | Out-Null
Pop-Location

Write-Section "Validation Completed"
Write-Host "PlantProcess IQ local full validation PASSED." -ForegroundColor Green