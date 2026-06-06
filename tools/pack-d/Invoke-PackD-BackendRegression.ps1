[CmdletBinding()]
param(
    [string]$ProjectRoot = (Resolve-Path ".").Path,
    [switch]$RunTests
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
    Run-Step "dotnet build Backend" {
        dotnet build ".\Backend"
    }

    Run-Step "Pack D route-contract snapshot validation" {
        node ".\tools\pack-d\validate-pack-d-route-contract-snapshot.cjs"
    }

    if ($RunTests) {
        Push-Location ".\Backend"
        try {
            Run-Step "dotnet test" {
                dotnet test
            }
        }
        finally {
            Pop-Location
        }
    }

    Write-Host ""
    Write-Host "Pack D backend regression wrapper completed." -ForegroundColor Green
}
finally {
    Pop-Location
}
