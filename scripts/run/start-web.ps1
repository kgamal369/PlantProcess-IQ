param(

    [ValidateSet("local", "test", "server", "presentation")]

    [string]$Profile = "local",



    [switch]$FreePort

)



$ErrorActionPreference = "Stop"



$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)

$FrontendRoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web"



& (Join-Path $RepoRoot "scripts\env\use-profile.ps1") -Profile $Profile -WriteAppEnvFiles



# V1-40: resolve host/port with safe defaults so a missing env var never yields

# 'vite --host  --port' (the bug that broke the launcher). Explicit values, validated.

$vHost = if ([string]::IsNullOrWhiteSpace($env:VITE_HOST)) { "localhost" } else { $env:VITE_HOST }

$vPort = 0

if (-not [int]::TryParse($env:VITE_PORT, [ref]$vPort) -or $vPort -le 0) { $vPort = 5173 }



if ($FreePort) {

    & (Join-Path $RepoRoot "scripts\run\free-ports.ps1") -Ports @($vPort) -Force

}



Push-Location $FrontendRoot

try {

    Write-Host ("[start-web] Vite on http://" + $vHost + ":" + $vPort + " (profile " + $Profile + ")")

    $vite = Join-Path $FrontendRoot "node_modules\.bin\vite.cmd"

    if (Test-Path $vite) {

        & $vite --host $vHost --port $vPort

    } else {

        npm run dev -- --host $vHost --port $vPort

    }

}

finally {

    Pop-Location

}

