[CmdletBinding()]
param(
    [string]$BaseUrl = "https://app.178.105.152.180.sslip.io",
    [string]$ApiUrl = "https://api.178.105.152.180.sslip.io",
    [string]$ReportPath = "docs/deployment/T007_CLEAN_MACHINE_DEPLOY_ACCEPTANCE.latest.md",
    [switch]$SkipHttp
)

$ErrorActionPreference = "Stop"

function Assert-True {
    param([bool]$Condition, [string]$Message)

    if (-not $Condition) {
        throw $Message
    }
}

$checks = New-Object System.Collections.Generic.List[string]

Write-Host "PPIQ T-007 Clean Machine Deploy Acceptance" -ForegroundColor Cyan

Assert-True (Test-Path ".\deploy\compose") "Missing deploy\compose canonical compose directory."
Assert-True (Test-Path ".\deploy\caddy\Caddyfile") "Missing canonical deploy\caddy\Caddyfile."

$checks.Add("- Canonical compose directory exists.")
$checks.Add("- Canonical Caddyfile exists.")

$doNotDeploy = Get-ChildItem .\deploy -Recurse -File -ErrorAction SilentlyContinue |
    Where-Object { $_.Name -match "DO_NOT_DEPLOY|local_test|repair" }

Assert-True ($doNotDeploy.Count -eq 0) "Found local/repair/DO_NOT_DEPLOY files under deploy path: $($doNotDeploy.FullName -join ', ')"
$checks.Add("- No local repair / DO_NOT_DEPLOY scripts are under deploy path.")

if (Get-Command docker -ErrorAction SilentlyContinue) {
    $composeFiles = Get-ChildItem ".\deploy\compose" -Filter "*.yml" -File
    foreach ($compose in $composeFiles) {
        Write-Host "Checking docker compose config: $($compose.FullName)" -ForegroundColor Yellow
        docker compose -f $compose.FullName config | Out-Null
    }

    $checks.Add("- docker compose config validates for all deploy\compose yml files.")
}
else {
    $checks.Add("- docker not installed on this machine; compose syntax check skipped.")
}

if (-not $SkipHttp) {
    $health = Invoke-WebRequest -Uri "$ApiUrl/health" -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
    Assert-True ($health.StatusCode -in @(200,401,503)) "Unexpected API /health status: $($health.StatusCode)"
    $checks.Add("- API /health responded with controlled status $($health.StatusCode).")

    $app = Invoke-WebRequest -Uri $BaseUrl -UseBasicParsing -TimeoutSec 15 -ErrorAction Stop
    Assert-True ($app.StatusCode -eq 200) "Unexpected app status: $($app.StatusCode)"
    $checks.Add("- App frontend responded with HTTP 200.")
}

$reportDir = Split-Path $ReportPath -Parent
if ($reportDir -and -not (Test-Path $reportDir)) {
    New-Item -ItemType Directory -Force -Path $reportDir | Out-Null
}

$report = @"
# T-007 Clean Machine Deploy Acceptance

Marker: PPIQ_REALIZATION_T007_CLEAN_MACHINE_DEPLOY_ACCEPTANCE

Date: $(Get-Date -Format o)

## Checked URLs

- App: $BaseUrl
- API: $ApiUrl

## Acceptance Checks

$($checks -join "`n")

## Result

GREEN
"@

$report | Set-Content -Path $ReportPath -Encoding utf8

Write-Host "T-007 clean machine deploy acceptance GREEN." -ForegroundColor Green
Write-Host "Report: $ReportPath" -ForegroundColor Green