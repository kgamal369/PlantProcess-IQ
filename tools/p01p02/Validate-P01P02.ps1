param(
    [switch]$SkipCommands
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"

$Root = Resolve-Path (Join-Path $PSScriptRoot "..\..")

function Read-Text {
    param([Parameter(Mandatory = $true)][string]$Path)

    if (-not (Test-Path $Path)) {
        throw "Required file not found: $Path"
    }

    return [System.IO.File]::ReadAllText((Resolve-Path $Path))
}

function Assert-Contains {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Needle
    )

    $text = Read-Text $Path
    if (-not $text.Contains($Needle)) {
        throw "Validation failed. Expected '$Needle' in $Path"
    }
}

function Assert-NotContains {
    param(
        [Parameter(Mandatory = $true)][string]$Path,
        [Parameter(Mandatory = $true)][string]$Needle
    )

    $text = Read-Text $Path
    if ($text.Contains($Needle)) {
        throw "Validation failed. Forbidden '$Needle' found in $Path"
    }
}

function Assert-AnyRegex {
    param(
        [Parameter(Mandatory = $true)][string]$Text,
        [Parameter(Mandatory = $true)][string[]]$Patterns,
        [Parameter(Mandatory = $true)][string]$Message
    )

    foreach ($pattern in $Patterns) {
        if ($Text -match $pattern) {
            return
        }
    }

    throw "Validation failed. $Message"
}

function Assert-AuthRoute {
    param(
        [Parameter(Mandatory = $true)][string]$AuthText,
        [Parameter(Mandatory = $true)][string]$RouteName,
        [Parameter(Mandatory = $true)][string]$FullRoute,
        [Parameter(Mandatory = $true)][string[]]$ChildRoutePatterns
    )

    $hasFullRoute = $AuthText.Contains($FullRoute)

    $hasAuthGroup =
        $AuthText -match 'MapGroup\s*\(\s*"/auth"\s*\)' -or
        $AuthText -match 'MapGroup\s*\(\s*AuthBaseRoute\s*\)' -or
        $AuthText -match 'const\s+string\s+AuthBaseRoute\s*=\s*"/auth"'

    $hasChildRoute = $false
    foreach ($pattern in $ChildRoutePatterns) {
        if ($AuthText -match $pattern) {
            $hasChildRoute = $true
            break
        }
    }

    if ($hasFullRoute -or ($hasAuthGroup -and $hasChildRoute)) {
        return
    }

    throw "Validation failed. Auth route '$RouteName' is not proven. Expected either literal '$FullRoute' OR MapGroup('/auth') plus matching child route."
}

function Run-Step {
    param(
        [Parameter(Mandatory = $true)][string]$Name,
        [Parameter(Mandatory = $true)][scriptblock]$Step
    )

    Write-Host ""
    Write-Host "==> $Name" -ForegroundColor Cyan

    & $Step

    if ($LASTEXITCODE -ne 0) {
        throw "$Name failed with exit code $LASTEXITCODE"
    }
}

Write-Host "P01/P02 validation started at $Root" -ForegroundColor Cyan

$programPath = Join-Path $Root "Backend\PlantProcess.Api\Program.cs"
$startupValidatorPath = Join-Path $Root "Backend\PlantProcess.Api\Configuration\StartupConfigurationValidator.cs"
$startupGuardPath = Join-Path $Root "Backend\PlantProcess.Api\Security\P01P02StartupGuard.cs"
$authEndpointsPath = Join-Path $Root "Backend\PlantProcess.Api\Security\AuthEndpoints.cs"
$accessControlPath = Join-Path $Root "Backend\PlantProcess.Api\Security\PlantAccessControl.cs"
$sqlPath = Join-Path $Root "Backend\database\scripts\300_p01_p02_security_access_control_spine.sql"
$apiClientPath = Join-Path $Root "Frontend\PlantProcess.Web\src\api\http\apiClient.ts"
$authContextPath = Join-Path $Root "Frontend\PlantProcess.Web\src\state\AuthContext.tsx"
$licenseGatePath = Join-Path $Root "Frontend\PlantProcess.Web\src\components\license\LicenseGate.tsx"

$programText = Read-Text $programPath
$authText = Read-Text $authEndpointsPath
$accessText = Read-Text $accessControlPath

# ============================================================
# Backend startup + middleware wiring
# ============================================================

Assert-Contains $programPath "P01P02StartupGuard.Validate"
Assert-Contains $programPath "MapAuthEndpoints"
Assert-Contains $programPath "UsePlantProcessAccessControl"
Assert-Contains $programPath "options.SaveToken = false"

Assert-AnyRegex `
    -Text $programText `
    -Patterns @(
        'app\.UseAuthentication\(\);\s*app\.UsePlantProcessAccessControl\(\);\s*app\.UseAuthorization\(\);',
        'app\.UseAuthentication\(\);\s*[\r\n ]+app\.UsePlantProcessAccessControl\(\);\s*[\r\n ]+app\.UseAuthorization\(\);'
    ) `
    -Message "Middleware order must be UseAuthentication -> UsePlantProcessAccessControl -> UseAuthorization."

# ============================================================
# Startup secret guard
# ============================================================

Assert-Contains $startupGuardPath "public static void Validate"
Assert-Contains $startupGuardPath "RejectDangerous"
Assert-Contains $startupGuardPath "BootstrapAdminPassword"
Assert-Contains $startupGuardPath "SigningKey"

Assert-Contains $startupValidatorPath "CHANGE_THIS_KEY"
Assert-Contains $startupValidatorPath "Validate"

# ============================================================
# Auth endpoint contract
# Accept both:
# - MapPost('/auth/login')
# - MapGroup('/auth').MapPost('/login')
# ============================================================

Assert-Contains $authEndpointsPath "MapAuthEndpoints"

Assert-AuthRoute `
    -AuthText $authText `
    -RouteName "login" `
    -FullRoute "/auth/login" `
    -ChildRoutePatterns @(
        'MapPost\s*\(\s*"/login"',
        'MapPost\s*\(\s*"login"',
        'const\s+string\s+LoginRoute\s*=\s*"/login"',
        'const\s+string\s+LoginRoute\s*=\s*"login"'
    )

Assert-AuthRoute `
    -AuthText $authText `
    -RouteName "refresh" `
    -FullRoute "/auth/refresh" `
    -ChildRoutePatterns @(
        'MapPost\s*\(\s*"/refresh"',
        'MapPost\s*\(\s*"refresh"',
        'const\s+string\s+RefreshRoute\s*=\s*"/refresh"',
        'const\s+string\s+RefreshRoute\s*=\s*"refresh"'
    )

Assert-AuthRoute `
    -AuthText $authText `
    -RouteName "logout" `
    -FullRoute "/auth/logout" `
    -ChildRoutePatterns @(
        'MapPost\s*\(\s*"/logout"',
        'MapPost\s*\(\s*"logout"',
        'const\s+string\s+LogoutRoute\s*=\s*"/logout"',
        'const\s+string\s+LogoutRoute\s*=\s*"logout"'
    )

Assert-AuthRoute `
    -AuthText $authText `
    -RouteName "provisioning claim" `
    -FullRoute "/auth/provisioning/claim" `
    -ChildRoutePatterns @(
        'MapPost\s*\(\s*"/provisioning/claim"',
        'MapPost\s*\(\s*"provisioning/claim"',
        'const\s+string\s+ProvisioningClaimRoute\s*=\s*"/provisioning/claim"',
        'const\s+string\s+ProvisioningClaimRoute\s*=\s*"provisioning/claim"'
    )

Assert-Contains $authEndpointsPath "HttpOnly"

# ============================================================
# Access-control contract
# ============================================================

Assert-Contains $accessControlPath "UsePlantProcessAccessControl"
Assert-Contains $accessControlPath "TenantOwner"
Assert-Contains $accessControlPath "Deny"

Assert-AnyRegex `
    -Text $accessText `
    -Patterns @(
        '403',
        'Status403Forbidden',
        'ForbidAsync',
        'Results\.Forbid'
    ) `
    -Message "Access-control middleware must produce a forbidden response for denied access."

# ============================================================
# Database security spine
# ============================================================

Assert-Contains $sqlPath "ppiq_auth_users"
Assert-Contains $sqlPath "ppiq_refresh_tokens"
Assert-Contains $sqlPath "ppiq_access_audit_log"
Assert-Contains $sqlPath "v_ppiq_effective_entitlements"

# ============================================================
# Frontend auth/session hardening
# ============================================================

Assert-Contains $apiClientPath 'credentials: "include"'
Assert-Contains $apiClientPath "GET / HEAD / OPTIONS"
Assert-Contains $apiClientPath "automatic retry once"
Assert-Contains $apiClientPath "NEVER retried on 4xx/5xx"
Assert-Contains $apiClientPath "!(err instanceof ApiError)"
Assert-NotContains $apiClientPath "localStorage.setItem"
Assert-NotContains $apiClientPath "localStorage.getItem"

Assert-Contains $authContextPath "MAX_AUTO_BOOTSTRAP_ATTEMPTS"
Assert-Contains $authContextPath "VITE_SMOKE_PASSWORD"
Assert-Contains $authContextPath 'buildAuthMessage("invalid-credentials")'
Assert-Contains $authContextPath 'buildAuthMessage("forbidden")'
Assert-NotContains $authContextPath "ChangeMe123!"

Assert-Contains $licenseGatePath "fallbackTitle"
Assert-Contains $licenseGatePath "fallbackDescription"
Assert-Contains $licenseGatePath "entitlements"

# ============================================================
# Runtime config secret scan only
# Source code is allowed to contain dangerous tokens as deny-list checks.
# Runtime config is not allowed to contain them.
# ============================================================

$runtimeConfigFiles = @()

$backendConfigRoot = Join-Path $Root "Backend"
if (Test-Path $backendConfigRoot) {
    $runtimeConfigFiles += Get-ChildItem $backendConfigRoot -Recurse -File -Include "appsettings*.json" |
        Where-Object {
            $_.FullName -notmatch "\\bin\\" -and
            $_.FullName -notmatch "\\obj\\"
        }
}

$frontendRoot = Join-Path $Root "Frontend\PlantProcess.Web"
if (Test-Path $frontendRoot) {
    $runtimeConfigFiles += Get-ChildItem $frontendRoot -File -Force |
        Where-Object { $_.Name -like ".env*" }
}

$forbiddenRuntimeValues = @(
    "CHANGE_THIS_KEY",
    "CHANGE_ME",
    "__REPLACE_ME__"
)

foreach ($file in $runtimeConfigFiles) {
    $text = [System.IO.File]::ReadAllText($file.FullName)
    foreach ($value in $forbiddenRuntimeValues) {
        if ($text.Contains($value)) {
            throw "Forbidden runtime config value '$value' found in $($file.FullName)"
        }
    }
}

if (-not $SkipCommands) {
    Run-Step "Backend build" {
        Push-Location (Join-Path $Root "Backend")
        try {
            dotnet build
        }
        finally {
            Pop-Location
        }
    }

    Run-Step "Frontend build" {
        Push-Location (Join-Path $Root "Frontend\PlantProcess.Web")
        try {
            npm run build
        }
        finally {
            Pop-Location
        }
    }

    Run-Step "Frontend unit tests" {
        Push-Location (Join-Path $Root "Frontend\PlantProcess.Web")
        try {
            npm run test
        }
        finally {
            Pop-Location
        }
    }
}

Write-Host ""
Write-Host "P01/P02 validation passed." -ForegroundColor Green
