# Phase 0 - Baseline Stabilization

This folder contains scripts used to validate the current PlantProcess IQ Sprint 3 baseline before adding the Application layer.

## Goal

Phase 0 does not add product features. It proves that the current solution:

1. Builds successfully.
2. Has valid EF Core migrations.
3. Can connect to the PostgreSQL database.
4. Can load the current advanced demo seed.
5. Can return valid responses from the core baseline API endpoints.
6. Has a documented Sprint 3 status before new refactoring starts.

## Main Scripts

| Script | Purpose |
|---|---|
| `Run-Phase0-BaselineValidation.ps1` | Runs restore, build, EF database update, optional seed loading, and writes a validation report. |
| `Test-ApiBaseline.ps1` | Calls the baseline API endpoints and validates HTTP success responses. |

## Recommended Execution Order

```powershell
cd C:\Workspace\PlantProcess-IQ

.\tools\phase0\Run-Phase0-BaselineValidation.ps1 `
    -ProjectRoot "C:\Workspace\PlantProcess-IQ" `
    -ApplyDatabaseMigration `
    -LoadSeed `
    -SeedFile "C:\Workspace\PlantProcess-IQ\database\seeds\002_full_feature_demo_seed.sql"

dotnet run --project .\PlantProcess.Api\PlantProcess.Api.csproj

.\tools\phase0\Test-ApiBaseline.ps1 `
    -BaseUrl "https://localhost:7073"

    
---

## 0.3 Add file: `tools/phase0/Run-Phase0-BaselineValidation.ps1`

```powershell
<#
.SYNOPSIS
    Phase 0 baseline validation script for PlantProcess IQ.

.DESCRIPTION
    This script validates the current Sprint 3 baseline before adding the Application layer.

    It can:
    - Verify expected project folders and solution file.
    - Run dotnet restore.
    - Run dotnet build.
    - Apply EF Core migrations.
    - Optionally load the advanced demo seed SQL file.
    - Generate a timestamped validation report.

.NOTES
    Run from anywhere. Pass -ProjectRoot if needed.
#>

[CmdletBinding()]
param(
    [string]$ProjectRoot = "C:\Workspace\PlantProcess-IQ",

    [string]$SolutionFile = "PlantProcessIQ.sln",

    [string]$ApiProject = "PlantProcess.Api\PlantProcess.Api.csproj",

    [string]$InfrastructureProject = "PlantProcess.Infrastructure\PlantProcess.Infrastructure.csproj",

    [string]$DbConnectionString = "Host=localhost;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=SET_LOCAL_POSTGRES_PASSWORD",

    [switch]$ApplyDatabaseMigration,

    [switch]$LoadSeed,

    [string]$SeedFile = "database\seeds\002_full_feature_demo_seed.sql",

    [string]$ReportFolder = "tools\phase0\reports"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)

    Write-Host ""
    Write-Host "================================================================================" -ForegroundColor DarkGray
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "================================================================================" -ForegroundColor DarkGray
}

function Invoke-CheckedCommand {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Command,

        [Parameter(Mandatory = $true)]
        [string[]]$Arguments,

        [string]$WorkingDirectory = $ProjectRoot
    )

    Write-Host ""
    Write-Host "> $Command $($Arguments -join ' ')" -ForegroundColor Yellow

    $process = Start-Process `
        -FilePath $Command `
        -ArgumentList $Arguments `
        -WorkingDirectory $WorkingDirectory `
        -NoNewWindow `
        -Wait `
        -PassThru

    if ($process.ExitCode -ne 0) {
        throw "Command failed with exit code $($process.ExitCode): $Command $($Arguments -join ' ')"
    }
}

function Test-RequiredPath {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path,

        [Parameter(Mandatory = $true)]
        [string]$Description
    )

    if (-not (Test-Path $Path)) {
        throw "$Description was not found: $Path"
    }

    Write-Host "[OK] $Description found: $Path" -ForegroundColor Green
}

$startedAt = Get-Date
$timestamp = $startedAt.ToString("yyyyMMdd_HHmmss")

$ProjectRoot = (Resolve-Path $ProjectRoot).Path
$solutionPath = Join-Path $ProjectRoot $SolutionFile
$apiProjectPath = Join-Path $ProjectRoot $ApiProject
$infrastructureProjectPath = Join-Path $ProjectRoot $InfrastructureProject
$seedPath = Join-Path $ProjectRoot $SeedFile
$reportRoot = Join-Path $ProjectRoot $ReportFolder

if (-not (Test-Path $reportRoot)) {
    New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
}

$reportPath = Join-Path $reportRoot "phase0_validation_$timestamp.md"

Write-Step "PlantProcess IQ - Phase 0 Baseline Validation"
Write-Host "Project root : $ProjectRoot"
Write-Host "Started at   : $startedAt"

Write-Step "1. Validate expected project structure"
Test-RequiredPath -Path $solutionPath -Description "Solution file"
Test-RequiredPath -Path $apiProjectPath -Description "API project"
Test-RequiredPath -Path $infrastructureProjectPath -Description "Infrastructure project"
Test-RequiredPath -Path (Join-Path $ProjectRoot "PlantProcess.Domain\PlantProcess.Domain.csproj") -Description "Domain project"
Test-RequiredPath -Path (Join-Path $ProjectRoot "PlantProcess.Workers\PlantProcess.Workers.csproj") -Description "Workers project"

Write-Step "2. Capture Git status"
$gitStatus = & git -C $ProjectRoot status --short
if ([string]::IsNullOrWhiteSpace($gitStatus)) {
    Write-Host "[OK] Git working tree is clean." -ForegroundColor Green
} else {
    Write-Host "[INFO] Git working tree has changes:" -ForegroundColor Yellow
    Write-Host $gitStatus
}

Write-Step "3. Dotnet information"
Invoke-CheckedCommand -Command "dotnet" -Arguments @("--info")

Write-Step "4. Restore solution"
Invoke-CheckedCommand -Command "dotnet" -Arguments @("restore", $solutionPath)

Write-Step "5. Build solution"
Invoke-CheckedCommand -Command "dotnet" -Arguments @("build", $solutionPath, "--configuration", "Debug", "--no-restore")

if ($ApplyDatabaseMigration) {
    Write-Step "6. Apply EF Core database migrations"

    $env:PLANTPROCESS_DB = $DbConnectionString

    Invoke-CheckedCommand -Command "dotnet" -Arguments @(
        "ef",
        "database",
        "update",
        "--project",
        $infrastructureProjectPath,
        "--startup-project",
        $apiProjectPath
    )
} else {
    Write-Step "6. EF Core database migration skipped"
    Write-Host "Use -ApplyDatabaseMigration to apply migrations." -ForegroundColor Yellow
}

if ($LoadSeed) {
    Write-Step "7. Load advanced full feature demo seed"

    Test-RequiredPath -Path $seedPath -Description "Seed SQL file"

    $psqlCommand = Get-Command "psql" -ErrorAction SilentlyContinue

    if ($null -eq $psqlCommand) {
        throw "psql was not found in PATH. Install PostgreSQL client tools or load the seed manually from pgAdmin/DBeaver."
    }

    $env:PGPASSWORD = "SET_LOCAL_POSTGRES_PASSWORD"

    Invoke-CheckedCommand -Command "psql" -Arguments @(
        "--host", "localhost",
        "--port", "5432",
        "--username", "plantprocess",
        "--dbname", "plantprocessiq",
        "--file", $seedPath
    )
} else {
    Write-Step "7. Seed loading skipped"
    Write-Host "Use -LoadSeed to load the current demo seed." -ForegroundColor Yellow
}

$finishedAt = Get-Date
$duration = $finishedAt - $startedAt

Write-Step "8. Write validation report"

$reportContent = @"
# PlantProcess IQ - Phase 0 Baseline Validation Report

Generated At: $($finishedAt.ToString("yyyy-MM-dd HH:mm:ss"))
Project Root: `$ProjectRoot`

## Result

Baseline validation script completed successfully.

## Actions Executed

| Check | Result |
|---|---|
| Project structure exists | Passed |
| Dotnet restore | Passed |
| Dotnet build | Passed |
| EF Core database migration | $(if ($ApplyDatabaseMigration) { "Executed" } else { "Skipped" }) |
| Advanced seed loading | $(if ($LoadSeed) { "Executed" } else { "Skipped" }) |

## Duration

$($duration.ToString())

## Notes

- This report validates the current Sprint 3 baseline before Phase 1 Application layer implementation.
- After this report, run `Test-ApiBaseline.ps1` while the API is running.
"@

Set-Content -Path $reportPath -Value $reportContent -Encoding UTF8

Write-Host ""
Write-Host "[OK] Phase 0 baseline validation completed." -ForegroundColor Green
Write-Host "[OK] Report written to: $reportPath" -ForegroundColor Green