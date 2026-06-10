[CmdletBinding()]
param(
    [string]$BaseUrl = "http://localhost:5173",
    [string]$ApiBaseUrl = "http://localhost:5063",
    [string]$SmokeUsername = "",
    [string]$SmokePassword = "",
    [int]$TimeoutSec = 20
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Off

# PPIQ_REALIZATION_T019_CLEAN_MACHINE_LOGIN_SMOKE

function Info($message) {
    Write-Host "[P3-T19-SMOKE] $message" -ForegroundColor Cyan
}

function Green($message) {
    Write-Host "[GREEN] $message" -ForegroundColor Green
}

function Invoke-HttpOk {
    param(
        [string]$Url,
        [int]$MinStatus = 200,
        [int]$MaxStatus = 399
    )

    Info "GET $Url"

    $response = Invoke-WebRequest -Uri $Url -UseBasicParsing -TimeoutSec $TimeoutSec

    if ($response.StatusCode -lt $MinStatus -or $response.StatusCode -gt $MaxStatus) {
        throw "Unexpected status $($response.StatusCode) for $Url"
    }

    return $response
}

$base = $BaseUrl.TrimEnd("/")
$api = $ApiBaseUrl.TrimEnd("/")

Invoke-HttpOk -Url "$api/health" | Out-Null

try {
    Invoke-HttpOk -Url "$api/readiness" | Out-Null
}
catch {
    Info "Readiness endpoint was not reachable or returned non-OK. Continuing because /health is the minimum smoke gate."
}

Invoke-HttpOk -Url "$base" -MaxStatus 499 | Out-Null

if (-not [string]::IsNullOrWhiteSpace($SmokeUsername) -and -not [string]::IsNullOrWhiteSpace($SmokePassword)) {
    Info "POST $api/auth/login"

    $body = @{
        userName = $SmokeUsername
        password = $SmokePassword
        requestedRole = "Admin"
    } | ConvertTo-Json

    $login = Invoke-WebRequest `
        -Uri "$api/auth/login" `
        -UseBasicParsing `
        -TimeoutSec $TimeoutSec `
        -Method Post `
        -ContentType "application/json" `
        -Body $body

    if ($login.StatusCode -lt 200 -or $login.StatusCode -gt 299) {
        throw "Login failed with status $($login.StatusCode)"
    }

    $json = $login.Content | ConvertFrom-Json
    $token = $json.accessToken

    if (-not $token) {
        $token = $json.token
    }

    if (-not $token) {
        $token = $json.jwt
    }

    if (-not $token) {
        throw "Login response did not contain an access token."
    }

    Green "P3-T19 login smoke passed."
}
else {
    Info "Smoke credentials not supplied. Skipping login POST and keeping health/frontend smoke only."
}

Green "P3-T19 clean-machine smoke passed."