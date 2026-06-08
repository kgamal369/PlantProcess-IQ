
$ErrorActionPreference = "Stop"

$Root = "C:\Workspace\PlantProcess-IQ"
$StartedAt = Get-Date

function Step($Name) {
    Write-Host ""
    Write-Host "=================================================================================================" -ForegroundColor DarkCyan
    Write-Host $Name -ForegroundColor Cyan
    Write-Host "=================================================================================================" -ForegroundColor DarkCyan
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

Step "PPIQ Phase 09 Rollup - preflight"

Write-Host "Root  : $Root"
Write-Host "Scope : Phase 09 Suggestions + Assistant Grounding Certification"

Run-Cmd "T-047 validator" $Root "node" @(".\tools\phase9\validate-t047-deterministic-suggestion-workflow.cjs")
Run-Cmd "T-048 validator" $Root "node" @(".\tools\phase9\validate-t048-assistant-grounding-eval.cjs")
Run-Cmd "T-049 validator" $Root "node" @(".\tools\phase9\validate-t049-model-gateway-serving-modes.cjs")
Run-Cmd "T-050 validator" $Root "node" @(".\tools\phase9\validate-t050-ai-regression-sweep.cjs")

Run-Cmd "Backend build" $Root "dotnet" @("build", ".\Backend")

Run-Cmd "T-047 suggestion workflow tests" $Root "dotnet" @(
    "test",
    ".\Backend\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj",
    "--filter",
    "FullyQualifiedName~Phase9_T047SuggestionWorkflowCertificationTests",
    "--no-build"
)

Run-Cmd "T-048 assistant grounding eval tests" $Root "dotnet" @(
    "test",
    ".\Backend\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj",
    "--filter",
    "FullyQualifiedName~Phase9_T048AssistantGroundingEvalGateTests",
    "--no-build"
)

Run-Cmd "T-049 model gateway serving mode tests" $Root "dotnet" @(
    "test",
    ".\Backend\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj",
    "--filter",
    "FullyQualifiedName~Phase9_T049ModelGatewayServingModesTests",
    "--no-build"
)

Run-Cmd "T-050 assistant regression sweep tests" $Root "dotnet" @(
    "test",
    ".\Backend\tests\PlantProcess.Application.UnitTests\PlantProcess.Application.UnitTests.csproj",
    "--filter",
    "FullyQualifiedName~Phase9_T050AssistantRegressionSweepTests",
    "--no-build"
)

$FinishedAt = Get-Date
$Duration = New-TimeSpan -Start $StartedAt -End $FinishedAt

$ReportDir = Join-Path $Root "docs\phase9"
New-Item -ItemType Directory -Force -Path $ReportDir | Out-Null
$ReportPath = Join-Path $ReportDir "T050_PHASE9_AI_REGRESSION_SWEEP_GREEN.md"

$Report = @"
# T-050 Phase 09 AI Regression Sweep Green

Marker: PPIQ_REALIZATION_T050_PHASE9_AI_REGRESSION_SWEEP

## Status

GREEN

## Completed Phase 09 tasks

- T-047 deterministic suggestion workflow
- T-048 assistant grounding eval gate
- T-049 model gateway serving modes and no-egress toggle
- T-050 AI regression sweep and deploy certification

## Certification proof

- T-047 validator passed
- T-048 validator passed
- T-049 validator passed
- T-050 validator passed
- Backend build passed
- T-047 suggestion workflow tests passed
- T-048 assistant grounding eval tests passed
- T-049 model gateway serving-mode tests passed
- T-050 assistant regression sweep tests passed

## Guardrails certified

- Suggestions are deterministic and evidence-backed.
- Assistant blocks invented numbers.
- Assistant blocks unsupported causal/value overclaims.
- Assistant answers demo question with citation.
- Self-hosted model mode makes zero outbound calls.
- Private/BYO modes send only scoped evidence.
- Tenant no-egress blocks external model calls.

Generated at: $($FinishedAt.ToString("yyyy-MM-dd HH:mm:ss"))
Duration: $([math]::Round($Duration.TotalSeconds, 1)) seconds
"@

$utf8 = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($ReportPath, $Report, $utf8)

Step "PPIQ Phase 09 Rollup Result"

Write-Host "PPIQ-T050 passed: Phase 09 AI regression sweep is green." -ForegroundColor Green
Write-Host "Report: $ReportPath" -ForegroundColor Green
