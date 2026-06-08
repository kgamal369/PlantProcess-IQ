$ErrorActionPreference = "Stop"

$Root = "C:\Workspace\PlantProcess-IQ"
$Frontend = Join-Path $Root "Frontend\PlantProcess.Web"

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

Step "PPIQ Phase 08 Rollup - preflight"

Require-File (Join-Path $Root "tools\phase8\validate-t042-vif-multicollinearity.cjs")
Require-File (Join-Path $Root "tools\phase8\validate-t043-golden-signal-recovery.cjs")
Require-File (Join-Path $Root "tools\phase8\validate-t044-finding-transparency.cjs")
Require-File (Join-Path $Root "tools\phase8\validate-t045-readiness-gates.cjs")
Require-File (Join-Path $Root "tools\phase8\validate-t046-correlation-certification-rollup.cjs")

Write-Host "Root     : $Root"
Write-Host "Frontend : $Frontend"
Write-Host "Scope    : Phase 08 correlation engine certification"

Run-Cmd "T-042 validator" $Root "node" @(".\tools\phase8\validate-t042-vif-multicollinearity.cjs")
Run-Cmd "T-043 validator" $Root "node" @(".\tools\phase8\validate-t043-golden-signal-recovery.cjs")
Run-Cmd "T-044 validator" $Root "node" @(".\tools\phase8\validate-t044-finding-transparency.cjs")
Run-Cmd "T-045 validator" $Root "node" @(".\tools\phase8\validate-t045-readiness-gates.cjs")
Run-Cmd "T-046 validator" $Root "node" @(".\tools\phase8\validate-t046-correlation-certification-rollup.cjs")

Run-Cmd "Backend build" $Root "dotnet" @("build", ".\Backend")

Run-Cmd "Phase 08 backend tests - T042" $Root "dotnet" @(
    "test",
    ".\Backend\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj",
    "--filter",
    "FullyQualifiedName~Phase8_T042VifMulticollinearityTests",
    "--no-build"
)

Run-Cmd "Phase 08 backend tests - T043" $Root "dotnet" @(
    "test",
    ".\Backend\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj",
    "--filter",
    "FullyQualifiedName~Phase8_T043GoldenSignalRecoveryTests",
    "--no-build"
)

Run-Cmd "Phase 08 backend tests - T044" $Root "dotnet" @(
    "test",
    ".\Backend\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj",
    "--filter",
    "FullyQualifiedName~Phase8_T044FindingTransparencyTests",
    "--no-build"
)

Run-Cmd "Phase 08 backend tests - T045" $Root "dotnet" @(
    "test",
    ".\Backend\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj",
    "--filter",
    "FullyQualifiedName~Phase8_T045ReadinessGateSurfaceTests",
    "--no-build"
)

Run-Cmd "Frontend build" $Frontend "npm" @("run", "build")

Run-Cmd "Phase 08 frontend tests - T045 readiness gates" $Frontend "npx" @(
    "vitest",
    "run",
    "src/pages/Analytics/advancedReadinessGateView.test.ts",
    "--config",
    "vitest.config.ts"
)

$FinishedAt = Get-Date
$Duration = New-TimeSpan -Start $StartedAt -End $FinishedAt

$ReportDir = Join-Path $Root "docs\phase8"
New-Item -ItemType Directory -Force -Path $ReportDir | Out-Null

$ReportPath = Join-Path $ReportDir "T046_PHASE8_CORRELATION_CERTIFICATION_GREEN.md"

$Report = @"
# T-046 Phase 08 Correlation Certification Green

Marker: PPIQ_REALIZATION_T046_PHASE8_CORRELATION_CERTIFICATION_GREEN

## Status

GREEN

## Completed Phase 08 tasks

- T-042 VIF / multicollinearity handling
- T-043 golden-dataset signal-recovery fixture
- T-044 finding transparency evidence
- T-045 Ready / Partial / Blocked readiness gates in API + HMI
- T-046 Phase 08 rollup certification

## Certification proof

- VIF/multicollinearity validator passed
- Golden signal-recovery validator passed
- Finding transparency validator passed
- Readiness-gates validator passed
- Phase 08 rollup validator passed
- Backend build passed
- T-042 backend tests passed
- T-043 backend tests passed
- T-044 backend tests passed
- T-045 backend tests passed
- Frontend build passed
- T-045 frontend tests passed

## Statistical guardrails

- VIF handles multicollinearity before ranking and FDR.
- Golden dataset recovers known true drivers.
- Injected spurious features are rejected under FDR.
- Findings expose population, sample size, exclusions, stratification, provenance and honesty caveat.
- Readiness is surfaced as Ready / Partial / Blocked.
- Blocked analysis must abstain.
- The engine reports diagnostic association, not guaranteed root cause.

Generated at: $($FinishedAt.ToString("yyyy-MM-dd HH:mm:ss"))
Duration: $([math]::Round($Duration.TotalSeconds, 1)) seconds
"@

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($ReportPath, $Report, $utf8)

Step "PPIQ Phase 08 Rollup Result"

Write-Host "PPIQ-T046 passed: Phase 08 correlation certification is green." -ForegroundColor Green
Write-Host "Report: $ReportPath" -ForegroundColor Green