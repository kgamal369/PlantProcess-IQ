param(
    [string]$RepositoryRoot = "C:\Workspace\PlantProcess-IQ",
    [string]$BackendRoot = "C:\Workspace\PlantProcess-IQ\Backend",
    [string]$FrontendRoot = "C:\Workspace\PlantProcess-IQ\Frontend\PlantProcess.Web",
    [string]$WebsiteRoot = "C:\Workspace\PlantProcess-IQ\Website\PlantProcess.Website",
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

function Assert-FileExists {
    param([string]$Path)

    if (!(Test-Path $Path)) {
        throw "Missing required file: $Path"
    }

    Write-Host "OK: File exists -> $Path" -ForegroundColor Green
}

function Assert-FileContains {
    param(
        [string]$Path,
        [string]$Pattern,
        [string]$Message
    )

    Assert-FileExists -Path $Path

    $content = Get-Content $Path -Raw

    if ($content -notmatch [regex]::Escape($Pattern)) {
        throw "Validation failed: $Message. Missing pattern: $Pattern. File: $Path"
    }

    Write-Host "OK: $Message" -ForegroundColor Green
}

function Assert-HttpOk {
    param([string]$Url)

    $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec 30

    if ($response.StatusCode -lt 200 -or $response.StatusCode -ge 300) {
        throw "HTTP validation failed: $Url returned $($response.StatusCode)"
    }

    Write-Host "OK: $Url" -ForegroundColor Green
    return $response.Content
}

Write-Step "Dimension 6 static code validation"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Analytics\Contracts\MlReadinessDtos.cs" `
    -Pattern "MlWorkspaceReadinessDto" `
    -Message "D6-01 ML workspace DTO exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Analytics\Interfaces\IQualityLabelBuilderService.cs" `
    -Pattern "BuildPreviewAsync" `
    -Message "D6-03 Quality label builder interface exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Analytics\Services\QualityLabelBuilderService.cs" `
    -Pattern "QualityTrainingLabelDto" `
    -Message "D6-03 Quality label builder service exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Analytics\Services\MlReadinessService.cs" `
    -Pattern "PARAMETER_OBSERVATIONS" `
    -Message "D6-04 ML readiness scoring exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Analytics\Services\MlReadinessService.cs" `
    -Pattern "SYSTEM_ML_PARAMS_VS_DEFECTS" `
    -Message "D6-05 ML job definitions exist"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Analytics\Services\MlReadinessService.cs" `
    -Pattern "ModelRegistryLifecycleDto" `
    -Message "D6-07 ModelRegistry lifecycle projection exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Application\Analytics\Services\MlReadinessService.cs" `
    -Pattern "CorrelationLifecycleDto" `
    -Message "D6-07 CorrelationResult lifecycle projection exists"

Assert-FileContains `
    -Path "$BackendRoot\PlantProcess.Api\Endpoints\Analytics\MlReadinessEndpoints.cs" `
    -Pattern "/workspace" `
    -Message "D6 ML readiness workspace endpoint exists"

Assert-FileContains `
    -Path "$FrontendRoot\src\pages\MlReadiness\MlReadinessPage.tsx" `
    -Pattern "ML readiness before training" `
    -Message "D6 frontend ML readiness page exists"

Assert-FileContains `
    -Path "$FrontendRoot\src\pages\MlReadiness\MlReadinessPage.tsx" `
    -Pattern "No trained production ML model is active" `
    -Message "D6 frontend honest ML language exists"

Write-Step "Dimension 2 static website validation"

Assert-FileContains `
    -Path "$WebsiteRoot\src\App.tsx" `
    -Pattern "connect → stage → map → monitor → analyze → report" `
    -Message "D2-02 How it works flow exists"

Assert-FileContains `
    -Path "$WebsiteRoot\src\App.tsx" `
    -Pattern "Screenshot gallery" `
    -Message "D2-03 Screenshot gallery exists"

Assert-FileContains `
    -Path "$WebsiteRoot\src\App.tsx" `
    -Pattern "Demo lifecycle" `
    -Message "D2-04 Demo lifecycle section exists"

Assert-FileContains `
    -Path "$WebsiteRoot\src\App.tsx" `
    -Pattern "Connector status honesty" `
    -Message "D2-05 Connector honesty block exists"

Assert-FileContains `
    -Path "$WebsiteRoot\src\App.tsx" `
    -Pattern "Not MES. Not SCADA. Not Level 2. Not BI-only." `
    -Message "D2-06 Non-replacement positioning exists"

Assert-FileContains `
    -Path "$WebsiteRoot\src\App.tsx" `
    -Pattern "Data Diagnostic offer" `
    -Message "D2-07 Data Diagnostic offer exists"

Assert-FileContains `
    -Path "$WebsiteRoot\src\App.tsx" `
    -Pattern "mailto:info@plantprocessiq.com" `
    -Message "D2-08 Real CTA mailto exists"

Assert-FileContains `
    -Path "$WebsiteRoot\scripts\validate-website-content.mjs" `
    -Pattern "Website content validation passed." `
    -Message "Website validation script exists"

Write-Step "Build validation"

Push-Location $BackendRoot
dotnet build
dotnet test
Pop-Location

Push-Location $FrontendRoot
npm run build
npm run language:audit
Pop-Location

Push-Location $WebsiteRoot
npm run validate
Pop-Location

if (-not $SkipApi) {
    Write-Step "Runtime API validation"

    Assert-HttpOk "$ApiBaseUrl/health" | Out-Null
    Assert-HttpOk "$ApiBaseUrl/analytics/ml-readiness/score" | Out-Null
    Assert-HttpOk "$ApiBaseUrl/analytics/ml-readiness/labels/preview?limit=5" | Out-Null
    Assert-HttpOk "$ApiBaseUrl/analytics/ml-readiness/jobs" | Out-Null
    Assert-HttpOk "$ApiBaseUrl/analytics/ml-readiness/workspace?labelPreviewLimit=5" | Out-Null
}

Write-Step "Dimension 2 + Dimension 6 validation completed"

Write-Host "Dimension 6 — ML Engine readiness can be marked implementation-complete after this script is green." -ForegroundColor Green
Write-Host "Dimension 2 — Website Publish / Go-Live V2 content readiness can be marked implementation-complete after this script is green." -ForegroundColor Green
Write-Host "Public go-live still requires DNS, real domain, real screenshot files, and public smoke test." -ForegroundColor Yellow