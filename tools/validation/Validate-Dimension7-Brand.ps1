param(
    [string]$RepositoryRoot = "C:\Workspace\PlantProcess-IQ",
    [string]$FrontendRoot = "C:\Workspace\PlantProcess-IQ\Frontend\PlantProcess.Web",
    [string]$WebsiteRoot = "C:\Workspace\PlantProcess-IQ\Website\PlantProcess.Website"
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
        throw "Validation failed: $Message. Missing: $Pattern. File: $Path"
    }

    Write-Host "OK: $Message" -ForegroundColor Green
}

function Assert-FileExists {
    param(
        [string]$Path,
        [string]$Message
    )

    if (!(Test-Path $Path)) {
        throw "Validation failed: $Message. Missing file: $Path"
    }

    Write-Host "OK: $Message" -ForegroundColor Green
}

Write-Step "Dimension 7 — Brand Identity static validation"

Assert-FileContains `
    -Path "$FrontendRoot\src\brand\plantProcessBrand.ts" `
    -Pattern "read-only manufacturing intelligence layer" `
    -Message "In-app brand source of truth exists"

Assert-FileContains `
    -Path "$FrontendRoot\src\pages\BrandIdentity\BrandIdentityPage.tsx" `
    -Pattern "Dimension 7 — Brand Identity" `
    -Message "In-app brand identity page exists"

Assert-FileContains `
    -Path "$FrontendRoot\src\components\brand\BrandProofPanel.tsx" `
    -Pattern "Brand Identity & Market Positioning" `
    -Message "In-app brand proof panel exists"

Assert-FileContains `
    -Path "$FrontendRoot\src\components\AppLayout.tsx" `
    -Pattern "/brand" `
    -Message "In-app navigation contains Brand page"

Assert-FileExists `
    -Path "$FrontendRoot\public\brand\plantprocess-iq-architecture.svg" `
    -Message "In-app architecture diagram asset exists"

Assert-FileExists `
    -Path "$FrontendRoot\public\brand\plantprocess-iq-engineer-brief.html" `
    -Message "In-app engineer brief asset exists"

Assert-FileContains `
    -Path "$WebsiteRoot\src\brand\plantProcessBrand.ts" `
    -Pattern "Generic manufacturing quality-intelligence" `
    -Message "Website brand source of truth exists"

Assert-FileContains `
    -Path "$WebsiteRoot\src\components\BrandProofSection.tsx" `
    -Pattern "Brand proof" `
    -Message "Website brand proof section exists"

Assert-FileContains `
    -Path "$WebsiteRoot\src\App.tsx" `
    -Pattern "BrandProofSection" `
    -Message "Website renders brand proof section"

Assert-FileExists `
    -Path "$WebsiteRoot\public\brand\plantprocess-iq-architecture.svg" `
    -Message "Website architecture diagram asset exists"

Assert-FileExists `
    -Path "$WebsiteRoot\public\brand\plantprocess-iq-engineer-brief.html" `
    -Message "Website engineer brief asset exists"

Write-Step "Forbidden brand language scan"

$filesToScan = @(
    "$FrontendRoot\src\brand\plantProcessBrand.ts",
    "$FrontendRoot\src\pages\BrandIdentity\BrandIdentityPage.tsx",
    "$FrontendRoot\src\components\brand\BrandProofPanel.tsx",
    "$WebsiteRoot\src\brand\plantProcessBrand.ts",
    "$WebsiteRoot\src\App.tsx",
    "$WebsiteRoot\src\components\BrandProofSection.tsx"
)

$forbidden = @(
    "guarantees root cause",
    "guaranteed root cause detection",
    "production-ready ai model",
    "production-ready ai prediction",
    "replaces mes",
    "replaces scada",
    "replaces level 2",
    "replaces l2"
)

foreach ($file in $filesToScan) {
    if (!(Test-Path $file)) {
        continue
    }

    $content = (Get-Content $file -Raw).ToLowerInvariant()

    foreach ($phrase in $forbidden) {
        if ($content.Contains($phrase)) {
            throw "Forbidden brand claim found in $file : $phrase"
        }
    }

    Write-Host "OK: Forbidden language scan passed for $file" -ForegroundColor Green
}

Write-Step "Build validation"

Push-Location $FrontendRoot
npm run build
npm run language:audit
Pop-Location

Push-Location $WebsiteRoot
npm run build
npm run validate
Pop-Location

Write-Step "Dimension 7 validation completed"

Write-Host "Dimension 7 — Brand Identity & Market Positioning can be marked 100% implementation-complete after this script is green." -ForegroundColor Green