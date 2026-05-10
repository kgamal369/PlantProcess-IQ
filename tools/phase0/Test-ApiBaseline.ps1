<#
.SYNOPSIS
    Smoke tests the current PlantProcess IQ API baseline endpoints.

.DESCRIPTION
    Run this while PlantProcess.Api is running.

.EXAMPLE
    .\tools\phase0\Test-ApiBaseline.ps1 -BaseUrl "https://localhost:7073"
#>

[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$BaseUrl,

    [int]$TimeoutSeconds = 30,

    [string]$ReportFolder = "tools\phase0\reports"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

function Invoke-BaselineGet {
    param(
        [Parameter(Mandatory = $true)]
        [string]$Path
    )

    $url = "$BaseUrl$Path"

    Write-Host ""
    Write-Host "GET $url" -ForegroundColor Yellow

    try {
        $response = Invoke-WebRequest `
            -Uri $url `
            -Method Get `
            -TimeoutSec $TimeoutSeconds `
            -SkipCertificateCheck

        if ($response.StatusCode -lt 200 -or $response.StatusCode -gt 299) {
            throw "Unexpected HTTP status code: $($response.StatusCode)"
        }

        Write-Host "[OK] $Path returned HTTP $($response.StatusCode)" -ForegroundColor Green

        return [PSCustomObject]@{
            Path = $Path
            Url = $url
            Success = $true
            StatusCode = $response.StatusCode
            Error = $null
        }
    }
    catch {
        Write-Host "[FAILED] $Path - $($_.Exception.Message)" -ForegroundColor Red

        return [PSCustomObject]@{
            Path = $Path
            Url = $url
            Success = $false
            StatusCode = $null
            Error = $_.Exception.Message
        }
    }
}

$startedAt = Get-Date
$projectRoot = (Resolve-Path ".").Path
$reportRoot = Join-Path $projectRoot $ReportFolder

if (-not (Test-Path $reportRoot)) {
    New-Item -ItemType Directory -Path $reportRoot -Force | Out-Null
}

$timestamp = $startedAt.ToString("yyyyMMdd_HHmmss")
$reportPath = Join-Path $reportRoot "phase0_api_smoke_$timestamp.md"

Write-Host ""
Write-Host "================================================================================" -ForegroundColor DarkGray
Write-Host "PlantProcess IQ - Phase 0 API Baseline Smoke Test" -ForegroundColor Cyan
Write-Host "================================================================================" -ForegroundColor DarkGray
Write-Host "Base URL: $BaseUrl"

$baselineEndpoints = @(
    "/health",
    "/db-health",
    "/dev/database-summary",
    "/dev/material-sample?take=5",
    "/workflow/overview",
    "/workflow/demo-status",
    "/integration/summary",
    "/configuration/summary",
    "/data-quality/scan-preview?take=20",
    "/validation/sync-report"
)

$results = foreach ($endpoint in $baselineEndpoints) {
    Invoke-BaselineGet -Path $endpoint
}

$failed = $results | Where-Object { -not $_.Success }

$finishedAt = Get-Date

$rows = $results | ForEach-Object {
    "| `$($_.Path)` | $($_.Success) | $($_.StatusCode) | $($_.Error) |"
}

$report = @"
# PlantProcess IQ - Phase 0 API Smoke Test Report

Generated At: $($finishedAt.ToString("yyyy-MM-dd HH:mm:ss"))
Base URL: `$BaseUrl`

## Result

Total endpoints: $($results.Count)  
Successful: $(($results | Where-Object { $_.Success }).Count)  
Failed: $($failed.Count)

## Endpoint Results

| Endpoint | Success | HTTP Status | Error |
|---|---:|---:|---|
$($rows -join "`n")

"@

Set-Content -Path $reportPath -Value $report -Encoding UTF8

if ($failed.Count -gt 0) {
    Write-Host ""
    Write-Host "[FAILED] Some baseline endpoints failed. Report: $reportPath" -ForegroundColor Red
    exit 1
}

Write-Host ""
Write-Host "[OK] All baseline API endpoints passed." -ForegroundColor Green
Write-Host "[OK] Report written to: $reportPath" -ForegroundColor Green