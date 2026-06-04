param(
    [ValidateSet("local", "test", "server")]
    [string]$Profile = "local"
)

$ErrorActionPreference = "Stop"

& (Join-Path $PSScriptRoot "use-profile.ps1") -Profile $Profile -WriteAppEnvFiles

@(
    [pscustomobject]@{ Area="Profile"; Key="PPIQ_PROFILE"; Value=$env:PPIQ_PROFILE },
    [pscustomobject]@{ Area="Backend"; Key="ASPNETCORE_URLS"; Value=$env:ASPNETCORE_URLS },
    [pscustomobject]@{ Area="Backend"; Key="PLANTPROCESS_ALLOWED_ORIGINS"; Value=$env:PLANTPROCESS_ALLOWED_ORIGINS },
    [pscustomobject]@{ Area="Database"; Key="POSTGRES_DB"; Value=$env:POSTGRES_DB },
    [pscustomobject]@{ Area="Database"; Key="POSTGRES_HOST_PORT"; Value=$env:POSTGRES_HOST_PORT },
    [pscustomobject]@{ Area="Frontend"; Key="VITE_API_BASE_URL"; Value=$env:VITE_API_BASE_URL },
    [pscustomobject]@{ Area="Frontend"; Key="VITE_PORT"; Value=$env:VITE_PORT },
    [pscustomobject]@{ Area="Smoke"; Key="PPIQ_SMOKE_USERNAME"; Value=$env:PPIQ_SMOKE_USERNAME }
) | Format-Table -AutoSize
