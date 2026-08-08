param(
    [ValidateSet("local", "test", "server", "presentation")]
    [string]$Profile = "local",

    [switch]$WriteAppEnvFiles
)

$ErrorActionPreference = "Stop"

$RepoRoot = Split-Path -Parent (Split-Path -Parent $PSScriptRoot)
$ProfilePath = Join-Path $RepoRoot "env\profiles\$Profile.env"

if (-not (Test-Path $ProfilePath)) {
    $ExamplePath = "$ProfilePath.example"
    if (Test-Path $ExamplePath) {
        Copy-Item $ExamplePath $ProfilePath -Force
        Write-Host "[S1A] Created $ProfilePath from example." -ForegroundColor Yellow
    } else {
        throw "Profile not found: $ProfilePath"
    }
}

foreach ($Line in Get-Content $ProfilePath) {
    $Trimmed = $Line.Trim()
    if ($Trimmed.Length -eq 0 -or $Trimmed.StartsWith("#")) { continue }

    $Index = $Trimmed.IndexOf("=")
    if ($Index -lt 1) { continue }

    $Name = $Trimmed.Substring(0, $Index).Trim()
    $Value = $Trimmed.Substring($Index + 1).Trim()

    [System.Environment]::SetEnvironmentVariable($Name, $Value, "Process")
}

Write-Host "[S1A] Loaded profile '$Profile' from $ProfilePath" -ForegroundColor Green
Write-Host "[S1A] API=$env:VITE_API_BASE_URL | WEB_PORT=$env:VITE_PORT | DB=$env:POSTGRES_DB@$env:POSTGRES_HOST:$env:POSTGRES_HOST_PORT" -ForegroundColor Cyan

if ($WriteAppEnvFiles) {
    $FrontendRoot = Join-Path $RepoRoot "Frontend\PlantProcess.Web"
    $WebsiteRoot = Join-Path $RepoRoot "Website\PlantProcess.Website"

    if (Test-Path $FrontendRoot) {
        @"
VITE_HOST=$env:VITE_HOST
VITE_PORT=$env:VITE_PORT
VITE_PREVIEW_PORT=$env:VITE_PREVIEW_PORT
VITE_API_BASE_URL=$env:VITE_API_BASE_URL
VITE_SMOKE_USERNAME=$env:VITE_SMOKE_USERNAME
VITE_SMOKE_PASSWORD=$env:VITE_SMOKE_PASSWORD
"@ | Set-Content (Join-Path $FrontendRoot ".env.local") -Encoding utf8
        Write-Host "[S1A] Wrote Frontend .env.local" -ForegroundColor Green
    }

    if (Test-Path $WebsiteRoot) {
        @"
VITE_WEBSITE_API_BASE_URL=$env:VITE_WEBSITE_API_BASE_URL
"@ | Set-Content (Join-Path $WebsiteRoot ".env.local") -Encoding utf8
        Write-Host "[S1A] Wrote Website .env.local" -ForegroundColor Green
    }
}
