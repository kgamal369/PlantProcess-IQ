[CmdletBinding()]
param(
    [string]$ProjectRoot = "C:\Workspace\PlantProcess-IQ",

    [ValidateSet("local-native-main-db", "server-docker-main-db", "customer-template")]
    [string]$Profile = "server-docker-main-db",

    [string]$EnvFile = "",

    [switch]$DryRun,
    [switch]$SkipBuild,
    [switch]$SkipComposeUp,
    [switch]$SkipSmoke,

    [string]$BaseUrl = "http://localhost:5173",
    [string]$ApiBaseUrl = "http://localhost:5063",
    [string]$SmokeUsername = "",
    [string]$SmokePassword = ""
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Off
try { $global:PSNativeCommandUseErrorActionPreference = $false } catch {}

# PPIQ_REALIZATION_T019_CLEAN_MACHINE_TO_LOGIN_V2

function Info($message) {
    Write-Host "[P3-T19] $message" -ForegroundColor Cyan
}

function Green($message) {
    Write-Host "[GREEN] $message" -ForegroundColor Green
}

function Fail($message) {
    throw "[P3-T19] $message"
}

function Assert-Exists($path, $label) {
    if (-not (Test-Path $path)) {
        Fail "$label not found: $path"
    }
}

function Test-CommandExists($name) {
    $cmd = Get-Command $name -ErrorAction SilentlyContinue
    return $null -ne $cmd
}

function Invoke-Checked {
    param(
        [string]$Label,
        [scriptblock]$Command
    )

    Info $Label
    & $Command

    if ($LASTEXITCODE -ne 0) {
        Fail "$Label failed with exit code $LASTEXITCODE"
    }
}

function Get-ComposeFilesForProfile {
    param([string]$SelectedProfile)

    $base = "deploy\compose\docker-compose.demo.yml"

    if (-not (Test-Path $base)) {
        $legacy = "deploy\server\docker-compose.server.yml"

        if (Test-Path $legacy) {
            return @($legacy)
        }

        Fail "No compose base found. Expected deploy\compose\docker-compose.demo.yml or deploy\server\docker-compose.server.yml."
    }

    switch ($SelectedProfile) {
        "local-native-main-db" {
            return @(
                "deploy\compose\docker-compose.demo.yml",
                "deploy\compose\docker-compose.local-native-main-db.yml"
            )
        }
        "server-docker-main-db" {
            return @(
                "deploy\compose\docker-compose.demo.yml",
                "deploy\compose\docker-compose.server-docker-main-db.yml"
            )
        }
        "customer-template" {
            return @(
                "deploy\compose\docker-compose.demo.yml",
                "deploy\compose\docker-compose.customer-template.yml"
            )
        }
        default {
            Fail "Unsupported profile: $SelectedProfile"
        }
    }
}

function Resolve-EnvFile {
    param([string]$SelectedProfile, [string]$ExplicitEnvFile)

    if (-not [string]::IsNullOrWhiteSpace($ExplicitEnvFile)) {
        return $ExplicitEnvFile
    }

    $profileEnv = "deploy\compose\env\.env.$SelectedProfile"

    if (Test-Path $profileEnv) {
        return $profileEnv
    }

    $serverEnv = "deploy\server\.env.production"

    if (Test-Path $serverEnv) {
        return $serverEnv
    }

    return ""
}

function Assert-NoTrackedRuntimeSecrets {
    $git = Get-Command git -ErrorAction SilentlyContinue

    if (-not $git) {
        Info "Git not found. Skipping tracked-runtime-secret check."
        return
    }

    $tracked = & git ls-files 2>$null

    $forbidden = @(
        "deploy/server/.env.production",
        "deploy/server/.env",
        "deploy/server/.env.local",
        "Frontend/PlantProcess.Web/.env.local",
        "Website/.env.local",
        ".local-secrets/p3-t18-query-preview-readonly.env"
    )

    foreach ($item in $forbidden) {
        if ($tracked -contains $item) {
            Fail "Runtime secret file is tracked by git: $item"
        }
    }
}

function Build-ComposeArgs {
    param(
        [string[]]$ComposeFiles,
        [string]$RuntimeEnvFile
    )

    $args = @("compose")

    if (-not [string]::IsNullOrWhiteSpace($RuntimeEnvFile)) {
        $args += @("--env-file", (Resolve-Path $RuntimeEnvFile).Path)
    }

    foreach ($file in $ComposeFiles) {
        $args += @("-f", (Resolve-Path $file).Path)
    }

    return $args
}

Push-Location $ProjectRoot

try {
    Info "Clean-machine-to-login deployment contract"
    Info "Profile=$Profile DryRun=$DryRun SkipBuild=$SkipBuild SkipComposeUp=$SkipComposeUp SkipSmoke=$SkipSmoke"

    Assert-Exists "deploy\caddy\Caddyfile" "Canonical Caddyfile"
    Assert-Exists "docs\deployment\PHASE03_CLEAN_MACHINE_TO_LOGIN_RUNBOOK.md" "Clean-machine runbook"

    $composeFiles = Get-ComposeFilesForProfile -SelectedProfile $Profile

    foreach ($file in $composeFiles) {
        Assert-Exists $file "Compose file"
    }

    Assert-NoTrackedRuntimeSecrets

    if (-not (Test-CommandExists "docker")) {
        Fail "Docker CLI is not available. Install Docker Desktop or Docker Engine + Compose plugin."
    }

    $runtimeEnvFile = Resolve-EnvFile -SelectedProfile $Profile -ExplicitEnvFile $EnvFile

    if ($DryRun) {
        Info "Dry run mode: runtime env file is not required."
        Info "Injecting safe dummy env values for docker compose config parsing only."

        $env:POSTGRES_USER = "plantprocess_admin"
        Set-Item -Path Env:POSTGRES_PASSWORD -Value "CHANGE_ME_DRY_RUN_CONFIG_PARSE_ONLY"
        $env:POSTGRES_DB = "plantprocess_app_db"
        $env:POSTGRES_PORT = "55433"

        $env:PPIQ_MAIN_DB_CONNECTION_STRING = "Host=host.docker.internal;Port=5432;Database=plantprocessiq;Username=plantprocess;Password=CHANGE_ME_DRY_RUN_CONFIG_PARSE_ONLY;Include Error Detail=true"

        $dryRunSigningKey = (([Guid]::NewGuid().ToString("N") + [Guid]::NewGuid().ToString("N") + [Guid]::NewGuid().ToString("N")).Substring(0, 64)); Set-Item -Path Env:PPIQ_SIGNING_KEY -Value $dryRunSigningKey
        Set-Item -Path Env:PPIQ_BOOTSTRAP_ADMIN_PASSWORD -Value "__DISABLED__"

        $env:PPIQ_API_HOST_PORT = "5063"
        $env:PPIQ_APP_HOST_PORT = "5173"
        $env:PPIQ_WEBSITE_HOST_PORT = "5174"

        $env:SITE_HOST = "localhost"
        $env:WEBSITE_HOST = "website.localhost"
        $env:ACME_EMAIL = "admin@example.invalid"
        $env:CADDY_AUTO_HTTPS = "off"
        $env:PPIQ_API_UPSTREAM = "plantprocess-api:5063"
        $env:PPIQ_APP_UPSTREAM = "plantprocess-app-web:80"
        $env:PPIQ_WEBSITE_UPSTREAM = "plantprocess-website:80"
    }
    else {
        if ([string]::IsNullOrWhiteSpace($runtimeEnvFile) -or -not (Test-Path $runtimeEnvFile)) {
            Fail "Missing runtime env file. Create one from deploy\compose\env\.env.$Profile.example or deploy\server\.env.example, then pass -EnvFile."
        }
    }

    # P3-T19: activate Compose profile when the selected environment requires profiled services.
    # Without this, server-docker-main-db hides ppiq-postgres and API depends_on becomes invalid.
    if ($Profile -eq "server-docker-main-db") {
        Set-Item -Path Env:COMPOSE_PROFILES -Value "server-docker-main-db"
    }
    else {
        Remove-Item Env:\COMPOSE_PROFILES -ErrorAction SilentlyContinue
    }

    $composeArgs = Build-ComposeArgs -ComposeFiles $composeFiles -RuntimeEnvFile $runtimeEnvFile

    $configArgs = $composeArgs + @("config")

    Invoke-Checked "Validating docker compose config" {
        & docker @configArgs | Out-Null
    }

    if ($DryRun) {
        Green "P3-T19 dry run passed. Compose config and clean-machine deploy contract are valid."
        return
    }

    if (-not $SkipBuild) {
        if (-not (Test-CommandExists "dotnet")) {
            Fail "dotnet CLI is not available."
        }

        if (-not (Test-CommandExists "npm")) {
            Fail "npm CLI is not available."
        }

        Invoke-Checked "Building Backend" {
            & dotnet build ".\Backend"
        }

        Push-Location ".\Frontend\PlantProcess.Web"
        try {
            Invoke-Checked "Building Frontend app" {
                & npm run build
            }
        }
        finally {
            Pop-Location
        }

        if (Test-Path ".\Website\package.json") {
            Push-Location ".\Website"
            try {
                Invoke-Checked "Building Website" {
                    & npm run build
                }
            }
            finally {
                Pop-Location
            }
        }
    }

    if (-not $SkipComposeUp) {
        $upArgs = $composeArgs + @("up", "-d")
        Invoke-Checked "Starting deployment containers" {
            & docker @upArgs
        }
    }

    if (-not $SkipSmoke) {
        $smoke = "tools\phase3\smoke-p3-t19-clean-machine-login.ps1"
        Assert-Exists $smoke "P3-T19 smoke script"

        $smokeArgs = @(
            "-BaseUrl", $BaseUrl,
            "-ApiBaseUrl", $ApiBaseUrl
        )

        if (-not [string]::IsNullOrWhiteSpace($SmokeUsername)) {
            $smokeArgs += @("-SmokeUsername", $SmokeUsername)
        }

        if (-not [string]::IsNullOrWhiteSpace($SmokePassword)) {
            $smokeArgs += @("-SmokePassword", $SmokePassword)
        }

        Invoke-Checked "Running clean-machine login smoke" {
            & powershell -ExecutionPolicy Bypass -File $smoke @smokeArgs
        }
    }

    Green "P3-T19 clean-machine-to-login deployment completed."
}
finally {
    Pop-Location
}