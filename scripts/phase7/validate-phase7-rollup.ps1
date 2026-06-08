$ErrorActionPreference = "Stop"

$Root = "C:\Workspace\PlantProcess-IQ"
$Backend = Join-Path $Root "Backend"
$Frontend = Join-Path $Root "Frontend\PlantProcess.Web"
$Psql = "C:\Program Files\PostgreSQL\16\bin\psql.exe"

$DbHost = "127.0.0.1"
$DbPort = "5432"
$DbName = "plantprocessiq"
$DbUser = "plantprocess"
$env:PGPASSWORD = "plantprocess123"

$StartedAt = Get-Date

function Step($Name) {
    Write-Host ""
    Write-Host "=================================================================================================" -ForegroundColor DarkCyan
    Write-Host $Name -ForegroundColor Cyan
    Write-Host "=================================================================================================" -ForegroundColor DarkCyan
}

function Require-File($Path) {
    if (-not (Test-Path $Path)) {
        throw "Missing required file: $Path"
    }
}

function Run-Cmd($Name, $WorkingDirectory, $FilePath, $Arguments) {
    Step $Name
    Push-Location $WorkingDirectory
    try {
        & $FilePath @Arguments
        if ($LASTEXITCODE -ne 0) {
            throw "$Name failed with exit code $LASTEXITCODE"
        }
    }
    finally {
        Pop-Location
    }
}

Step "PPIQ Phase 07 Rollup - preflight"

Require-File $Psql
Require-File (Join-Path $Root "Backend\database\scripts\421_phase7_value_realization_ledger.sql")
Require-File (Join-Path $Root "tools\phase7\validate-t037-value-engine-depth.cjs")
Require-File (Join-Path $Root "tools\phase7\validate-t038-eur-28k-56k-worked-case.cjs")
Require-File (Join-Path $Root "tools\phase7\validate-t039-value-realization-ledger.cjs")
Require-File (Join-Path $Root "tools\phase7\validate-t040-value-scenario-page.cjs")

Write-Host "Root      : $Root"
Write-Host "Backend   : $Backend"
Write-Host "Frontend  : $Frontend"
Write-Host "Database  : $($DbHost):$DbPort/$DbName"
Write-Host "User      : $DbUser"

Step "Apply Phase 07 SQL script 421 locally"

& $Psql `
  -h $DbHost `
  -p $DbPort `
  -U $DbUser `
  -d $DbName `
  -v ON_ERROR_STOP=1 `
  -f (Join-Path $Root "Backend\database\scripts\421_phase7_value_realization_ledger.sql")

if ($LASTEXITCODE -ne 0) {
    throw "SQL apply failed with exit code $LASTEXITCODE"
}

Step "Verify canon.value_realization_ledger exists"

$TableCheck = & $Psql `
  -h $DbHost `
  -p $DbPort `
  -U $DbUser `
  -d $DbName `
  -t `
  -A `
  -c "select to_regclass('canon.value_realization_ledger');"

if (($TableCheck -join "").Trim() -ne "canon.value_realization_ledger") {
    throw "canon.value_realization_ledger was not found after SQL apply."
}

Write-Host "canon.value_realization_ledger verified." -ForegroundColor Green

Run-Cmd "T-037 validator" $Root "node" @(".\tools\phase7\validate-t037-value-engine-depth.cjs")
Run-Cmd "T-038 validator" $Root "node" @(".\tools\phase7\validate-t038-eur-28k-56k-worked-case.cjs")
Run-Cmd "T-039 validator" $Root "node" @(".\tools\phase7\validate-t039-value-realization-ledger.cjs")
Run-Cmd "T-040 validator" $Root "node" @(".\tools\phase7\validate-t040-value-scenario-page.cjs")

Run-Cmd "Backend build" $Root "dotnet" @("build", ".\Backend")

Run-Cmd "Phase 07 backend tests - T037" $Root "dotnet" @(
    "test",
    ".\Backend\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj",
    "--filter",
    "FullyQualifiedName~Phase7_ValueImpactEngineDepthTests",
    "--no-build"
)

Run-Cmd "Phase 07 backend tests - T038" $Root "dotnet" @(
    "test",
    ".\Backend\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj",
    "--filter",
    "FullyQualifiedName~Phase7_ValueImpactWorkedCaseTests",
    "--no-build"
)

Run-Cmd "Phase 07 backend tests - T039" $Root "dotnet" @(
    "test",
    ".\Backend\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj",
    "--filter",
    "FullyQualifiedName~Phase7_ValueRealizationTrackingTests",
    "--no-build"
)

Run-Cmd "Frontend build" $Frontend "npm" @("run", "build")

Run-Cmd "T-040 frontend tests" $Frontend "npx" @(
    "vitest",
    "run",
    "src/pages/Phase7ValueScenario/phase7ValueScenarioMath.test.ts",
    "--config",
    "vitest.config.ts"
)

$FinishedAt = Get-Date
$Duration = New-TimeSpan -Start $StartedAt -End $FinishedAt

$ReportDir = Join-Path $Root "docs\phase7"
New-Item -ItemType Directory -Force -Path $ReportDir | Out-Null

$ReportPath = Join-Path $ReportDir "T041_PHASE7_ROLLUP_GREEN.md"

$Report = @"
# T-041 Phase 07 Rollup Green

Marker: PPIQ_REALIZATION_T041_PHASE7_ROLLUP_GREEN

## Status

GREEN

## Completed tasks

- T-037 Value engine depth
- T-038 EUR 28k-56k worked case fixture
- T-039 Value-realization / tracked ROI ledger
- T-040 Value scenario page

## Database deployment

Applied locally:

- Backend/database/scripts/421_phase7_value_realization_ledger.sql

Verified table:

- canon.value_realization_ledger

## Validation proof

- T-037 validator passed
- T-038 validator passed
- T-039 validator passed
- T-040 validator passed
- Backend build passed
- T-037 backend tests passed
- T-038 backend tests passed
- T-039 backend tests passed
- Frontend build passed
- T-040 frontend tests passed

## Guardrail

Projected value is separated from tracked realized value.

Baseline-vs-actual tracked value is not automatic causal attribution.

Generated at: $($FinishedAt.ToString("yyyy-MM-dd HH:mm:ss"))
Duration: $([math]::Round($Duration.TotalSeconds, 1)) seconds
"@

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($ReportPath, $Report, $utf8)

Step "PPIQ Phase 07 Rollup Result"

Write-Host "PPIQ-T041 passed: Phase 07 value engine, worked case, realization ledger, scenario UI, SQL, backend and frontend regression are green." -ForegroundColor Green
Write-Host "Report: $ReportPath" -ForegroundColor Green