<#
.SYNOPSIS
    PlantProcess IQ Phase 0 + Phase 1 smoke test.

.DESCRIPTION
    Validates:
    - API health
    - DB health and migrations
    - dashboard metadata
    - dashboard workspace
    - valid widget queries
    - system template ensure/repair
    - frontend build
    - backend build

.NOTES
    Run with API already running.
#>

[CmdletBinding()]
param(
    [string]$ProjectRoot = "C:\Workspace\PlantProcess-IQ",
    [string]$BackendRoot = "C:\Workspace\PlantProcess-IQ\Backend",
    [string]$FrontendRoot = "C:\Workspace\PlantProcess-IQ\Frontend\PlantProcess.Web",
    [string]$BaseUrl = "http://localhost:5063",
    [string]$ReportFolder = "C:\Workspace\PlantProcess-IQ\Documentation\Validation\Phase0_Phase1"
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$startedAt = Get-Date
$timestamp = $startedAt.ToString("yyyyMMdd_HHmmss")

if (-not (Test-Path $ReportFolder)) {
    New-Item -ItemType Directory -Path $ReportFolder -Force | Out-Null
}

$reportPath = Join-Path $ReportFolder "phase0_phase1_smoke_$timestamp.md"
$results = New-Object System.Collections.Generic.List[object]

function Add-Result {
    param(
        [string]$Name,
        [string]$Status,
        [string]$Detail = ""
    )

    $results.Add([PSCustomObject]@{
        Name = $Name
        Status = $Status
        Detail = $Detail
    })

    $color = switch ($Status) {
        "PASS" { "Green" }
        "WARN" { "Yellow" }
        "FAIL" { "Red" }
        default { "White" }
    }

    Write-Host "[$Status] $Name $Detail" -ForegroundColor $color
}

function Invoke-CheckedCommand {
    param(
        [string]$Name,
        [string]$Command,
        [string[]]$Arguments,
        [string]$WorkingDirectory
    )

    Write-Host ""
    Write-Host "================================================================================" -ForegroundColor DarkGray
    Write-Host $Name -ForegroundColor Cyan
    Write-Host "================================================================================" -ForegroundColor DarkGray
    Write-Host "> $Command $($Arguments -join ' ')" -ForegroundColor Yellow

    $process = Start-Process `
        -FilePath $Command `
        -ArgumentList $Arguments `
        -WorkingDirectory $WorkingDirectory `
        -NoNewWindow `
        -Wait `
        -PassThru

    if ($process.ExitCode -eq 0) {
        Add-Result $Name "PASS"
    } else {
        Add-Result $Name "FAIL" "Exit code: $($process.ExitCode)"
        throw "$Name failed with exit code $($process.ExitCode)"
    }
}

function Invoke-JsonGet {
    param(
        [string]$Name,
        [string]$Path,
        [int[]]$AllowedStatusCodes = @(200)
    )

    $url = "$BaseUrl$Path"

    try {
        $response = Invoke-WebRequest -Uri $url -Method GET -UseBasicParsing
        if ($AllowedStatusCodes -contains [int]$response.StatusCode) {
            Add-Result $Name "PASS" "$($response.StatusCode) $Path"
            return $response.Content | ConvertFrom-Json
        }

        Add-Result $Name "FAIL" "Unexpected status $($response.StatusCode) for $Path"
        throw "Unexpected status $($response.StatusCode)"
    }
    catch {
        Add-Result $Name "FAIL" $_.Exception.Message
        throw
    }
}

function Invoke-JsonPost {
    param(
        [string]$Name,
        [string]$Path,
        [object]$Body,
        [int[]]$AllowedStatusCodes = @(200, 201)
    )

    $url = "$BaseUrl$Path"
    $json = $Body | ConvertTo-Json -Depth 20

    try {
        $response = Invoke-WebRequest `
            -Uri $url `
            -Method POST `
            -ContentType "application/json" `
            -Body $json `
            -UseBasicParsing

        if ($AllowedStatusCodes -contains [int]$response.StatusCode) {
            Add-Result $Name "PASS" "$($response.StatusCode) $Path"
            return $response.Content | ConvertFrom-Json
        }

        Add-Result $Name "FAIL" "Unexpected status $($response.StatusCode) for $Path"
        throw "Unexpected status $($response.StatusCode)"
    }
    catch {
        Add-Result $Name "FAIL" $_.Exception.Message
        throw
    }
}

Write-Host ""
Write-Host "PlantProcess IQ Phase 0 + Phase 1 Smoke Test" -ForegroundColor Cyan
Write-Host "BaseUrl: $BaseUrl"
Write-Host "Started: $startedAt"

# ─────────────────────────────────────────────────────────────
# Build gates
# ─────────────────────────────────────────────────────────────
Invoke-CheckedCommand `
    -Name "Backend dotnet build" `
    -Command "dotnet" `
    -Arguments @("build") `
    -WorkingDirectory $BackendRoot

Invoke-CheckedCommand `
    -Name "Frontend npm run build" `
    -Command "npm" `
    -Arguments @("run", "build") `
    -WorkingDirectory $FrontendRoot

# ─────────────────────────────────────────────────────────────
# API health gates
# ─────────────────────────────────────────────────────────────
$health = Invoke-JsonGet -Name "GET /health" -Path "/health"
$dbHealth = Invoke-JsonGet -Name "GET /db-health" -Path "/db-health"
$ready = Invoke-JsonGet -Name "GET /health/ready" -Path "/health/ready"

if ($dbHealth.pendingMigrations -and $dbHealth.pendingMigrations.Count -gt 0) {
    Add-Result "DB migration alignment" "FAIL" "Pending migrations: $($dbHealth.pendingMigrations -join ', ')"
    throw "Pending migrations detected."
} else {
    Add-Result "DB migration alignment" "PASS" "No pending migrations."
}

# ─────────────────────────────────────────────────────────────
# Dashboard system template gates
# ─────────────────────────────────────────────────────────────
Invoke-JsonPost `
    -Name "POST ensure system dashboard templates" `
    -Path "/analytics/dashboard/definitions/system-templates/ensure" `
    -Body @{}

Invoke-JsonPost `
    -Name "POST repair system dashboard templates" `
    -Path "/analytics/dashboard/definitions/system-templates/repair" `
    -Body @{}

# ─────────────────────────────────────────────────────────────
# Dashboard metadata / definitions
# ─────────────────────────────────────────────────────────────
$metadata = Invoke-JsonGet `
    -Name "GET dashboard metadata" `
    -Path "/analytics/dashboard/metadata"

if ($metadata.dimensions.Count -gt 0 -and $metadata.measures.Count -gt 0 -and $metadata.chartTypes.Count -gt 0) {
    Add-Result "Dashboard metadata content" "PASS" "Dimensions=$($metadata.dimensions.Count), Measures=$($metadata.measures.Count), Charts=$($metadata.chartTypes.Count)"
} else {
    Add-Result "Dashboard metadata content" "FAIL" "Metadata missing dimensions/measures/chart types."
    throw "Dashboard metadata incomplete."
}

$definitions = Invoke-JsonGet `
    -Name "GET dashboard definitions" `
    -Path "/analytics/dashboard/definitions?includeInactive=false&includeSystemTemplates=true"

if ($definitions.Count -ge 5) {
    Add-Result "System dashboard template count" "PASS" "Dashboards=$($definitions.Count)"
} else {
    Add-Result "System dashboard template count" "WARN" "Dashboards=$($definitions.Count). Expected at least 5."
}

# ─────────────────────────────────────────────────────────────
# Widget query smoke tests
# ─────────────────────────────────────────────────────────────
$widgetQueries = @(
    @{
        Name = "Widget query: DefectType x DefectCount"
        Body = @{
            widgetType = "chart"
            chartType = "bar"
            dimensionCode = "DefectType"
            measureCode = "DefectCount"
            filters = @{}
            options = @{
                maxRows = 20
                rawRowLimit = 10000
                sortDirection = "desc"
                includeWarnings = $true
            }
        }
    },
    @{
        Name = "Widget query: Day x DefectRate"
        Body = @{
            widgetType = "chart"
            chartType = "line"
            dimensionCode = "Day"
            measureCode = "DefectRate"
            filters = @{}
            options = @{
                maxRows = 30
                rawRowLimit = 10000
                sortDirection = "asc"
                includeWarnings = $true
            }
        }
    },
    @{
        Name = "Widget query: MaterialUnitType x MaterialCount"
        Body = @{
            widgetType = "chart"
            chartType = "bar"
            dimensionCode = "MaterialUnitType"
            measureCode = "MaterialCount"
            filters = @{}
            options = @{
                maxRows = 20
                rawRowLimit = 10000
                sortDirection = "desc"
                includeWarnings = $true
            }
        }
    },
    @{
        Name = "Widget query: RiskClass x RiskScore"
        Body = @{
            widgetType = "chart"
            chartType = "donut"
            dimensionCode = "RiskClass"
            measureCode = "RiskScore"
            filters = @{}
            options = @{
                maxRows = 20
                rawRowLimit = 10000
                sortDirection = "desc"
                includeWarnings = $true
            }
        }
    },
    @{
        Name = "Widget query: SourceSystem x DataQualityIssueCount"
        Body = @{
            widgetType = "chart"
            chartType = "bar"
            dimensionCode = "SourceSystem"
            measureCode = "DataQualityIssueCount"
            filters = @{}
            options = @{
                maxRows = 20
                rawRowLimit = 10000
                sortDirection = "desc"
                includeWarnings = $true
            }
        }
    }
)

foreach ($query in $widgetQueries) {
    $result = Invoke-JsonPost `
        -Name $query.Name `
        -Path "/analytics/dashboard/widgets/query" `
        -Body $query.Body

    if ($null -ne $result.rows) {
        Add-Result "$($query.Name) row check" "PASS" "Rows=$($result.rows.Count)"
    } else {
        Add-Result "$($query.Name) row check" "WARN" "No rows property returned."
    }
}

# ─────────────────────────────────────────────────────────────
# Dashboard workspace
# ─────────────────────────────────────────────────────────────
Invoke-JsonPost `
    -Name "POST dashboard workspace" `
    -Path "/analytics/dashboard/workspace" `
    -Body @{
        page = 1
        pageSize = 25
        sortBy = "latestRiskScore"
        sortDirection = "desc"
    }

# ─────────────────────────────────────────────────────────────
# Write report
# ─────────────────────────────────────────────────────────────
$finishedAt = Get-Date
$duration = $finishedAt - $startedAt

$failed = @($results | Where-Object { $_.Status -eq "FAIL" })
$warnings = @($results | Where-Object { $_.Status -eq "WARN" })

$status = if ($failed.Count -eq 0) { "PASSED" } else { "FAILED" }

$tableRows = $results | ForEach-Object {
    "| $($_.Name) | $($_.Status) | $($_.Detail) |"
}

$report = @"
# PlantProcess IQ — Phase 0 + Phase 1 Smoke Test Report

Generated At: $($finishedAt.ToString("yyyy-MM-dd HH:mm:ss"))
Base URL: $BaseUrl
Duration: $duration

## Overall Status

**$status**

## Summary

| Metric | Count |
|---|---:|
| Total checks | $($results.Count) |
| Failed | $($failed.Count) |
| Warnings | $($warnings.Count) |

## Checks

| Check | Status | Detail |
|---|---|---|
$($tableRows -join "`n")

## Next Step

If all checks pass:
- Capture five clean UI screenshots.
- Validate widget create → preview → save → reload manually in browser.
- Validate Save Layout → refresh → layout survives.
- Move to Phase 2 Admin Area Shell.
"@

Set-Content -Path $reportPath -Value $report -Encoding UTF8

Write-Host ""
Write-Host "================================================================================" -ForegroundColor DarkGray
Write-Host "Smoke test completed: $status" -ForegroundColor Cyan
Write-Host "Report: $reportPath" -ForegroundColor Green
Write-Host "================================================================================" -ForegroundColor DarkGray

if ($failed.Count -gt 0) {
    exit 1
}

exit 0