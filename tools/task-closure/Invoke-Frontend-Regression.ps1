[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunE2E,
    [switch]$RunVisual
)

$ErrorActionPreference = "Stop"

function Run-Step([string]$Name, [scriptblock]$Block) {
    Write-Host ""
    Write-Host "---- $Name" -ForegroundColor Cyan
    & $Block
    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
}

Push-Location $ProjectRoot
try {
    Push-Location ".\Frontend\PlantProcess.Web"
    try {
        Run-Step "npm run build" {
            npm run build
        }

        $packageJson = Get-Content ".\package.json" -Raw | ConvertFrom-Json

        if ($packageJson.scripts.PSObject.Properties.Name -contains "test") {
            Run-Step "npm run test" {
                npm run test
            }
        } else {
            Write-Host "No npm test script found. Build remains the mandatory baseline." -ForegroundColor Yellow
        }

        if ($RunE2E) {
            if ($packageJson.scripts.PSObject.Properties.Name -contains "e2e") {
                Run-Step "npm run e2e" {
                    npm run e2e
                }
            } else {
                Write-Host "No npm e2e script found. Skipping E2E." -ForegroundColor Yellow
            }
        }

        if ($RunVisual) {
            if ($packageJson.scripts.PSObject.Properties.Name -contains "test:visual:phase56") {
                Run-Step "npm run test:visual:phase56" {
                    npm run test:visual:phase56
                }
            } else {
                Write-Host "No visual phase56 script found. Skipping visual run." -ForegroundColor Yellow
            }
        }
    }
    finally {
        Pop-Location
    }

    Write-Host ""
    Write-Host "Frontend regression wrapper completed." -ForegroundColor Green
}
finally {
    Pop-Location
}
