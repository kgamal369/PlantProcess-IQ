<#
.SYNOPSIS
    Sprint 6 validation script for tasks 4 to 8.

.DESCRIPTION
    Validates:
    - Task 4: Dashboard workspace API returns valid JSON.
    - Task 5: Frontend dashboard UI is reachable.
    - Task 6: Drag/resize/reflow manual validation checklist.
    - Task 7: Dashboard filter interaction manual validation checklist.
    - Task 8: Theme consistency manual validation checklist.

.NOTES
    Run after:
    1. Backend is running.
    2. Frontend is running.
    3. Database has demo/synthetic data.
#>

[CmdletBinding()]
param(
    [string]$ApiBaseUrl,
    [string]$WebBaseUrl,
    [int]$TimeoutSeconds = 20
)

$ErrorActionPreference = "Stop"

if ([string]::IsNullOrWhiteSpace($ApiBaseUrl)) {
    if (-not [string]::IsNullOrWhiteSpace($env:PLANTPROCESS_SMOKE_API_BASE_URL)) {
        $ApiBaseUrl = $env:PLANTPROCESS_SMOKE_API_BASE_URL
    }
    elseif (-not [string]::IsNullOrWhiteSpace($env:VITE_API_BASE_URL)) {
        $ApiBaseUrl = $env:VITE_API_BASE_URL
    }
    else {
        $ApiBaseUrl = "http://localhost:5063"
    }
}

if ([string]::IsNullOrWhiteSpace($WebBaseUrl)) {
    if (-not [string]::IsNullOrWhiteSpace($env:PLANTPROCESS_SMOKE_WEB_BASE_URL)) {
        $WebBaseUrl = $env:PLANTPROCESS_SMOKE_WEB_BASE_URL
    }
    else {
        $WebBaseUrl = "http://localhost:5173"
    }
}

$ApiBaseUrl = $ApiBaseUrl.TrimEnd("/")
$WebBaseUrl = $WebBaseUrl.TrimEnd("/")

function Write-Section {
    param([string]$Title)
    Write-Host ""
    Write-Host "================================================================================" -ForegroundColor DarkCyan
    Write-Host $Title -ForegroundColor Cyan
    Write-Host "================================================================================" -ForegroundColor DarkCyan
}

function Assert-True {
    param(
        [bool]$Condition,
        [string]$SuccessMessage,
        [string]$FailureMessage
    )

    if (-not $Condition) {
        throw $FailureMessage
    }

    Write-Host "PASS - $SuccessMessage" -ForegroundColor Green
}

function Invoke-JsonRequest {
    param(
        [string]$Method,
        [string]$Url,
        [object]$Body = $null
    )

    $params = @{
        Method      = $Method
        Uri         = $Url
        TimeoutSec  = $TimeoutSeconds
        ErrorAction = "Stop"
        Headers     = @{
            "Accept" = "application/json"
            "X-Correlation-Id" = "sprint6-validation-$([Guid]::NewGuid())"
        }
    }

    if ($null -ne $Body) {
        $params.ContentType = "application/json"
        $params.Body = ($Body | ConvertTo-Json -Depth 20)
    }

    return Invoke-RestMethod @params
}

Write-Section "Sprint 6 Validation - Configuration"
Write-Host "API Base URL : $ApiBaseUrl"
Write-Host "Web Base URL : $WebBaseUrl"

Write-Section "Task 4.1 - API Health"
$health = Invoke-JsonRequest -Method "GET" -Url "$ApiBaseUrl/health"
Assert-True `
    -Condition ($null -ne $health -and $health.status -eq "Healthy") `
    -SuccessMessage "/health returned Healthy." `
    -FailureMessage "/health did not return Healthy."

Write-Section "Task 4.2 - Database Health"
$dbHealth = Invoke-JsonRequest -Method "GET" -Url "$ApiBaseUrl/db-health"
Assert-True `
    -Condition ($null -ne $dbHealth -and $dbHealth.canConnect -eq $true) `
    -SuccessMessage "/db-health can connect to database." `
    -FailureMessage "/db-health cannot connect to database."

Write-Section "Task 4.3 - Dashboard Reference Data"
$referenceData = Invoke-JsonRequest -Method "GET" -Url "$ApiBaseUrl/analytics/dashboard/reference-data"
Assert-True `
    -Condition ($null -ne $referenceData -and $null -ne $referenceData.generatedAtUtc) `
    -SuccessMessage "/analytics/dashboard/reference-data returned valid response." `
    -FailureMessage "/analytics/dashboard/reference-data did not return valid response."

Write-Section "Task 4.4 - Dashboard Workspace API"
$workspaceRequest = @{
    siteId        = $null
    areaId        = $null
    equipmentId   = $null
    materialCode  = $null
    sourceSystem  = $null
    defectType    = $null
    riskClass     = $null
    fromUtc       = $null
    toUtc         = $null
    shiftCode     = $null
    page          = 1
    pageSize      = 10
    sortBy        = "materialCode"
    sortDirection = "asc"
}

$workspace = Invoke-JsonRequest `
    -Method "POST" `
    -Url "$ApiBaseUrl/analytics/dashboard/workspace" `
    -Body $workspaceRequest

Assert-True `
    -Condition ($null -ne $workspace -and $null -ne $workspace.overview -and $null -ne $workspace.quality -and $null -ne $workspace.risk -and $null -ne $workspace.dataQuality) `
    -SuccessMessage "/analytics/dashboard/workspace returned overview, quality, risk and data-quality sections." `
    -FailureMessage "/analytics/dashboard/workspace response is missing required dashboard sections."

Assert-True `
    -Condition ($null -ne $workspace.materials) `
    -SuccessMessage "/analytics/dashboard/workspace returned material table payload." `
    -FailureMessage "/analytics/dashboard/workspace response is missing materials payload."

Write-Section "Task 5 - Frontend Dashboard UI Reachability"
try {
    $webResponse = Invoke-WebRequest -Uri $WebBaseUrl -UseBasicParsing -TimeoutSec $TimeoutSeconds
    Assert-True `
        -Condition ($webResponse.StatusCode -ge 200 -and $webResponse.StatusCode -lt 400) `
        -SuccessMessage "Frontend is reachable." `
        -FailureMessage "Frontend returned non-success HTTP status."
}
catch {
    throw "Frontend is not reachable at $WebBaseUrl. Details: $($_.Exception.Message)"
}

Write-Section "Tasks 6, 7, 8 - Manual UI Validation Checklist"
Write-Host "Open: $WebBaseUrl" -ForegroundColor Yellow
Write-Host ""
Write-Host "Task 6 - Drag / Resize / Reflow:"
Write-Host "  [ ] Drag dashboard widgets to a new position."
Write-Host "  [ ] Resize at least one chart widget."
Write-Host "  [ ] Refresh the page and confirm layout behavior is acceptable."
Write-Host ""
Write-Host "Task 7 - Dashboard Filter Interaction:"
Write-Host "  [ ] Click a chart segment/bar/point."
Write-Host "  [ ] Confirm active filter chip appears."
Write-Host "  [ ] Confirm dashboard data refreshes."
Write-Host "  [ ] Clear filters and confirm dashboard resets."
Write-Host ""
Write-Host "Task 8 - Theme Consistency:"
Write-Host "  [ ] Toggle dark/light theme."
Write-Host "  [ ] Confirm layout does not break."
Write-Host "  [ ] Confirm industrial command-center styling remains consistent."
Write-Host ""

Write-Section "Sprint 6 Validation Result"
Write-Host "Automated validation for tasks 4 and 5 passed." -ForegroundColor Green
Write-Host "Tasks 6, 7 and 8 require manual browser validation using the checklist above." -ForegroundColor Yellow