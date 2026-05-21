param(
    [string]$BackendRoot = "C:\Workspace\PlantProcess-IQ\Backend",
    [string]$FrontendRoot = "C:\Workspace\PlantProcess-IQ\Frontend\PlantProcess.Web",
    [string]$ApiBaseUrl = "http://localhost:5063",
    [switch]$SkipApi
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)
    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
}

function Assert-FileContains {
    param(
        [string]$Path,
        [string]$Pattern,
        [string]$Message
    )

    if (!(Test-Path $Path)) {
        throw "Missing file: $Path"
    }

    $content = Get-Content $Path -Raw

    if ($content -notmatch [regex]::Escape($Pattern)) {
        throw "Validation failed: $Message. File: $Path. Missing: $Pattern"
    }

    Write-Host "OK: $Message" -ForegroundColor Green
}

function Assert-HttpOk {
    param([string]$Url)

    $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 20
    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
        throw "HTTP validation failed: $Url returned $($response.StatusCode)"
    }

    Write-Host "OK: $Url" -ForegroundColor Green
    return $response.Content
}

Write-Step "Dimension 5 / Dimension 8 static code validation"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Licensing\Contracts\LicenseTier.cs" `
    -Pattern "public enum LicenseTier" `
    -Message "D5-01 LicenseTier exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Licensing\Interfaces\ILicenseService.cs" `
    -Pattern "EnsureFeatureEnabled" `
    -Message "D5-02 License service interface exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Licensing\Services\LicenseService.cs" `
    -Pattern "EnsureConnectorAllowed" `
    -Message "D5-03 Connector gate exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Licensing\Services\LicenseService.cs" `
    -Pattern "EnsureRefreshIntervalAllowed" `
    -Message "D5-04 Refresh interval gate exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Licensing\Services\LicenseService.cs" `
    -Pattern "EnsureSourceCountAllowed" `
    -Message "D5-05 Source count gate exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Licensing\Services\LicenseService.cs" `
    -Pattern "SchemaSqlViewBuilder" `
    -Message "D5-06 SQL/schema gate exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Licensing\Services\LicenseService.cs" `
    -Pattern "EnsureDashboardCountAllowed" `
    -Message "D5-07 Dashboard/page gate exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Licensing\Services\LicenseService.cs" `
    -Pattern "InvestigationWorkflow" `
    -Message "D5-08 Premium investigation gate exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Api\Endpoints\Demo\DemoLifecycleEndpoints.cs" `
    -Pattern "/lifecycle" `
    -Message "D8-01 Demo lifecycle endpoint exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Demo\Services\DemoLifecycleService.cs" `
    -Pattern "BuildConnectorTruth" `
    -Message "D8-02 Connector truth model exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Demo\Services\DemoLifecycleService.cs" `
    -Pattern "BuildStagingSummaryAsync" `
    -Message "D8-03 Staging summary exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Demo\Services\DemoLifecycleService.cs" `
    -Pattern "BuildSchemaMappingSummaryAsync" `
    -Message "D8-04 Schema mapping summary exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Demo\Services\DemoLifecycleService.cs" `
    -Pattern "BuildJobChainAsync" `
    -Message "D8-05 Jobs chain summary exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Demo\Services\DemoLifecycleService.cs" `
    -Pattern "BuildDashboardOutputSummaryAsync" `
    -Message "D8-06 Dashboard output summary exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Demo\Services\DemoLifecycleService.cs" `
    -Pattern "NoTrainedProductionModelActive" `
    -Message "D8-07 Honest ML readiness exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Demo\Services\DemoLifecycleService.cs" `
    -Pattern "BuildReportCloseSummary" `
    -Message "D8-08 Final report close exists"

Assert-FileContains `
    -Path "$FrontendRoot\src\pages\CommercialLicense\CommercialLicensePage.tsx" `
    -Pattern "DimensionCompletionPanel" `
    -Message "Frontend commercial completion page exists"

Assert-FileContains `
    -Path "$FrontendRoot\src\pages\DemoLifecycle\DemoLifecyclePage.tsx" `
    -Pattern "DemoEnvironmentBanner" `
    -Message "Frontend demo environment banner is visible"

Assert-FileContains `
    -Path "$FrontendRoot\src\pages\DemoLifecycle\DemoLifecyclePage.tsx" `
    -Pattern "ConnectorTruthPanel" `
    -Message "Frontend connector truth panel is visible"

Assert-FileContains `
    -Path "$FrontendRoot\src\state\LicenseContext.tsx" `
    -Pattern "hasFeature" `
    -Message "Frontend license context exposes feature gates"

Write-Step "Build validation"

Push-Location $BackendRoot
dotnet build
dotnet test
Pop-Location

Push-Location $FrontendRoot
npm run build
npm run language:audit
Pop-Location

if (-not $SkipApi) {
    Write-Step "Runtime API validation"

    Assert-HttpOk "$ApiBaseUrl/health" | Out-Null
    Assert-HttpOk "$ApiBaseUrl/admin/license/current" | Out-Null
    Assert-HttpOk "$ApiBaseUrl/admin/license/features" | Out-Null
    Assert-HttpOk "$ApiBaseUrl/admin/license/limits" | Out-Null
    Assert-HttpOk "$ApiBaseUrl/admin/license/usage" | Out-Null
    Assert-HttpOk "$ApiBaseUrl/admin/license/commercial-readiness" | Out-Null
    Assert-HttpOk "$ApiBaseUrl/demo/lifecycle" | Out-Null
}

Write-Step "Dimension 5 + Dimension 8 validation completed"

Write-Host "Dimension 5 implementation can be marked 100% after this script is green." -ForegroundColor Green
Write-Host "Dimension 8 implementation can be marked 100% after this script is green." -ForegroundColor Green