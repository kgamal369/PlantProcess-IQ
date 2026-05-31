$ErrorActionPreference = "Stop"

$RepoRoot = Resolve-Path (Join-Path $PSScriptRoot "..\..")
$FrontendRoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web"
$WebsiteRoot = Join-Path $RepoRoot "Website\PlantProcess.Website"

function Invoke-P00AGate {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][string]$WorkingDirectory,
        [Parameter(Mandatory = $true)][string]$ScriptPath
    )

    Write-Host ""
    Write-Host "================================================================" -ForegroundColor Cyan
    Write-Host "Running P00A structural gate: $Name" -ForegroundColor Cyan
    Write-Host "Working directory: $WorkingDirectory" -ForegroundColor DarkCyan
    Write-Host "Script: $ScriptPath" -ForegroundColor DarkCyan
    Write-Host "================================================================" -ForegroundColor Cyan

    if (-not (Test-Path $WorkingDirectory)) {
        throw "Working directory not found for gate '$Name': $WorkingDirectory"
    }

    Push-Location $WorkingDirectory
    try {
        node $ScriptPath

        if ($LASTEXITCODE -ne 0) {
            throw "P00A structural gate failed: $Name. ExitCode=$LASTEXITCODE"
        }

        Write-Host "P00A structural gate passed: $Name" -ForegroundColor Green
    }
    finally {
        Pop-Location
    }
}

$gates = @(
    @{ Name = "Frontend standard imports"; WorkingDirectory = $FrontendRoot; ScriptPath = "scripts\validate-standard-imports.mjs" },
    @{ Name = "Frontend forbidden copy"; WorkingDirectory = $FrontendRoot; ScriptPath = "scripts\validate-forbidden-copy.mjs" },
    @{ Name = "Frontend no console in src"; WorkingDirectory = $FrontendRoot; ScriptPath = "scripts\validate-no-console-in-src.mjs" },
    @{ Name = "Frontend UI system rollout"; WorkingDirectory = $FrontendRoot; ScriptPath = "scripts\validate-ui-system-rollout.mjs" },
    @{ Name = "Frontend UI standards"; WorkingDirectory = $FrontendRoot; ScriptPath = "tools\ui\validate-ui-standards.mjs" },
    @{ Name = "Frontend Phase 2 full UI standards"; WorkingDirectory = $FrontendRoot; ScriptPath = "tools\ui\validate-phase2-full-ui-standards.mjs" },
    @{ Name = "Standard import negative proof"; WorkingDirectory = $RepoRoot; ScriptPath = "tools\validation\prove-standard-import-gate.cjs" },
    @{ Name = "Website content"; WorkingDirectory = $WebsiteRoot; ScriptPath = "scripts\validate-website-content.mjs" }
)

foreach ($gate in $gates) {
    Invoke-P00AGate `
        -Name $gate.Name `
        -WorkingDirectory $gate.WorkingDirectory `
        -ScriptPath $gate.ScriptPath
}

Write-Host ""
Write-Host "P00A structural gates completed successfully." -ForegroundColor Green
