# ============================================================
# PlantProcess IQ — S0 Repo Sanity
#
# Non-destructive.
# Fails only on current known compile-breaking or obvious generated noise.
# S2 will do the full repo cleanup later.
# ============================================================

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

$Errors = New-Object System.Collections.Generic.List[string]

$GatewayPath = Join-Path $RepoRoot "Backend\PlantProcess.Application\Acquisition\OtSafeEdgeCollectorGateway.cs"
if (Test-Path $GatewayPath) {
    $Gateway = Get-Content -Path $GatewayPath -Raw

    if ($Gateway.Contains("public static EdgeCollectorAcceptanceResult Accepted(")) {
        $Errors.Add("P5 compile blocker still exists: static factory Accepted(...) conflicts with record property Accepted.")
    }
}

$ProgramPath = Join-Path $RepoRoot "Backend\PlantProcess.Api\Program.cs"
if (Test-Path $ProgramPath) {
    $Program = Get-Content -Path $ProgramPath -Raw
    $SaveTokenMatches = [regex]::Matches($Program, "options\.SaveToken\s*=")

    if ($SaveTokenMatches.Count -ne 1) {
        $Errors.Add("Program.cs must contain exactly one options.SaveToken assignment. Found $($SaveTokenMatches.Count).")
    }
}

$GeneratedNoise = @(
    "Frontend\PlantProcess.Web\storybook-static"
)

foreach ($Relative in $GeneratedNoise) {
    $Path = Join-Path $RepoRoot $Relative
    if (Test-Path $Path) {
        Write-Host "[S0 WARNING] Generated artifact exists and will be handled in S2: $Relative" -ForegroundColor Yellow
    }
}

if ($Errors.Count -gt 0) {
    Write-Host ""
    Write-Host "S0 repo sanity failed:" -ForegroundColor Red
    foreach ($ErrorItem in $Errors) {
        Write-Host " - $ErrorItem" -ForegroundColor Red
    }

    exit 1
}

Write-Host "[GREEN] S0 repo sanity passed." -ForegroundColor Green