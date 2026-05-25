param(
    [string]$RepositoryRoot = "C:\Workspace\PlantProcess-IQ",
    [string]$BackendRoot = "C:\Workspace\PlantProcess-IQ\Backend",
    [string]$FrontendRoot = "C:\Workspace\PlantProcess-IQ\Frontend\PlantProcess.Web",
    [string]$WebsiteRoot = "C:\Workspace\PlantProcess-IQ\Website\PlantProcess.Website",
    [string]$ApiBaseUrl = "http://localhost:5063",
    [switch]$SkipRuntime
)

$ErrorActionPreference = "Stop"

function Write-Step {
    param([string]$Message)

    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host $Message -ForegroundColor Cyan
    Write-Host "================================================================" -ForegroundColor Cyan
}

function Assert-FileExists {
    param([string]$Path)

    if (-not (Test-Path $Path)) {
        throw "Missing required file: $Path"
    }

    Write-Host "✓ File exists: $Path" -ForegroundColor Green
}

function Assert-FileContains {
    param(
        [string]$Path,
        [string]$Pattern,
        [string]$Message
    )

    if (-not (Test-Path $Path)) {
        throw "Missing file: $Path"
    }

    $content = Get-Content $Path -Raw

    if ($content -notmatch [regex]::Escape($Pattern)) {
        throw "$Message. Missing pattern '$Pattern' in $Path"
    }

    Write-Host "✓ $Message" -ForegroundColor Green
}

function Assert-HttpOk {
    param([string]$Url)

    try {
        $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 20
        if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
            throw "Unexpected status code $($response.StatusCode)"
        }

        Write-Host "✓ HTTP OK: $Url" -ForegroundColor Green
    }
    catch {
        throw "HTTP validation failed for $Url. $($_.Exception.Message)"
    }
}

Write-Step "Phase 1 hardening files"

Assert-FileExists "$FrontendRoot\src\hardening\routeContracts.ts"
Assert-FileExists "$FrontendRoot\src\hardening\actionMatrix.ts"
Assert-FileExists "$FrontendRoot\src\components\hardening\DataFetchBoundary.tsx"
Assert-FileExists "$FrontendRoot\src\components\hardening\StandardButton.tsx"
Assert-FileExists "$FrontendRoot\src\components\hardening\RouteErrorBoundary.tsx"
Assert-FileExists "$FrontendRoot\src\hooks\useCustomerSafeAction.ts"
Assert-FileExists "$FrontendRoot\e2e\phase1-route-refresh.spec.ts"
Assert-FileExists "$FrontendRoot\e2e\phase1-button-action-matrix.spec.ts"
Assert-FileExists "$FrontendRoot\e2e\phase1-toast-mapping.spec.ts"

Assert-FileContains `
    -Path "$FrontendRoot\src\App.tsx" `
    -Pattern "RouteErrorBoundary" `
    -Message "PPIQ-HARD-001 route error boundary is integrated"

Assert-FileContains `
    -Path "$FrontendRoot\src\index.css" `
    -Pattern "hardening.css" `
    -Message "Hardening CSS imported"

Assert-FileContains `
    -Path "$FrontendRoot\src\notifications\toast.ts" `
    -Pattern "loading" `
    -Message "PPIQ-HARD-002 toast wrapper includes loading state"

Write-Step "Phase 1 website proof files"

Assert-FileExists "$WebsiteRoot\src\content\phase1WebsiteProof.ts"
Assert-FileExists "$WebsiteRoot\src\components\proof\ProductScreenshotShowcase.tsx"
Assert-FileExists "$WebsiteRoot\src\components\proof\PricingLicenseMatrix.tsx"
Assert-FileExists "$WebsiteRoot\src\components\proof\PositioningTruthBlock.tsx"
Assert-FileExists "$WebsiteRoot\src\components\proof\ConnectorHonestyBlock.tsx"
Assert-FileExists "$WebsiteRoot\src\components\proof\RequestDemoForm.tsx"
Assert-FileExists "$WebsiteRoot\src\styles\phase1-proof.css"

Assert-FileContains `
    -Path "$WebsiteRoot\src\App.tsx" `
    -Pattern "ProductScreenshotShowcase" `
    -Message "PPIQ-WEB-002 screenshot proof component is rendered"

Assert-FileContains `
    -Path "$WebsiteRoot\src\App.tsx" `
    -Pattern "PricingLicenseMatrix" `
    -Message "PPIQ-WEB-004 pricing matrix component is rendered"

Assert-FileContains `
    -Path "$WebsiteRoot\src\App.tsx" `
    -Pattern "PositioningTruthBlock" `
    -Message "PPIQ-WEB-005 positioning block component is rendered"

Assert-FileContains `
    -Path "$WebsiteRoot\src\App.tsx" `
    -Pattern "ConnectorHonestyBlock" `
    -Message "PPIQ-WEB-006 connector honesty component is rendered"

Assert-FileContains `
    -Path "$WebsiteRoot\src\App.tsx" `
    -Pattern "RequestDemoForm" `
    -Message "PPIQ-WEB-008 request demo form component is rendered"

Write-Step "Build validation"

Push-Location $BackendRoot
dotnet build
Pop-Location

Push-Location $FrontendRoot
npm run build
Pop-Location

Push-Location $WebsiteRoot
npm run validate
Pop-Location

if (-not $SkipRuntime) {
    Write-Step "Runtime API validation"

    Assert-HttpOk "$ApiBaseUrl/health"
    Assert-HttpOk "$ApiBaseUrl/admin/phase1/connector-truth"
    Assert-HttpOk "$ApiBaseUrl/reports/customer-demo/phase1-summary"
}

Write-Step "Phase 1 hardening + website validation completed"

Write-Host "PPIQ-HARD-001 / 002 / 004 / 006 / 012 / 016 implementation checks passed." -ForegroundColor Green
Write-Host "PPIQ-WEB-002 / 004 / 005 / 006 / 008 implementation checks passed." -ForegroundColor Green
Write-Host "Final customer-demo release still requires live browser test, screenshot asset, and Playwright green run." -ForegroundColor Yellow