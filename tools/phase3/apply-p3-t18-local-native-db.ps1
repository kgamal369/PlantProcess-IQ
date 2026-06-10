param(
    [string]$RepoRoot = "C:\Workspace\PlantProcess-IQ",
    [string]$PsqlPath = "C:\Program Files\PostgreSQL\16\bin\psql.exe",
    [string]$HostName = "127.0.0.1",
    [int]$Port = 5432,
    [string]$Database = "plantprocessiq",
    [string]$AdminUser = "postgres"
)

$ErrorActionPreference = "Stop"
Set-StrictMode -Off

Set-Location $RepoRoot

if (-not (Test-Path $PsqlPath)) {
    throw "psql.exe not found at $PsqlPath"
}

function New-HexSecret {
    param([int]$ByteCount = 48)

    $bytes = New-Object byte[] $ByteCount

    $rng = [System.Security.Cryptography.RNGCryptoServiceProvider]::Create()
    try {
        $rng.GetBytes($bytes)
    }
    finally {
        $rng.Dispose()
    }

    return (($bytes | ForEach-Object { $_.ToString("x2") }) -join "")
}

$secret = New-HexSecret -ByteCount 48
$sqlPath = Join-Path $RepoRoot "Backend\database\scripts\426_p3_t18_readonly_query_preview_role.sql"

if (-not (Test-Path $sqlPath)) {
    throw "P3-T18 SQL script not found: $sqlPath"
}

$tempSql = Join-Path $env:TEMP "p3-t18-readonly-role-apply.sql"
$escapedSecret = $secret.Replace("'", "''")
$normalizedSqlPath = $sqlPath.Replace("\", "/")

$sqlLines = @(
    "SET ppiq.query_preview_password = '$escapedSecret';",
    "\i '$normalizedSqlPath'"
)

[System.IO.File]::WriteAllLines($tempSql, $sqlLines, [System.Text.UTF8Encoding]::new($false))

Write-Host "[P3-T18] Applying read-only query-preview role to native local PostgreSQL..." -ForegroundColor Cyan
Write-Host "[P3-T18] Host=$HostName Port=$Port Database=$Database AdminUser=$AdminUser" -ForegroundColor Cyan
Write-Host "[P3-T18] Password will not be printed." -ForegroundColor Cyan

& $PsqlPath `
    -h $HostName `
    -p $Port `
    -U $AdminUser `
    -d $Database `
    -v ON_ERROR_STOP=1 `
    -f $tempSql

if ($LASTEXITCODE -ne 0) {
    throw "P3-T18 DB apply failed."
}

Write-Host "[P3-T18] Checking role status..." -ForegroundColor Cyan

& $PsqlPath `
    -h $HostName `
    -p $Port `
    -U $AdminUser `
    -d $Database `
    -v ON_ERROR_STOP=1 `
    -c "SELECT * FROM public.ppiq_p3_t18_readonly_preview_role_status();"

if ($LASTEXITCODE -ne 0) {
    throw "P3-T18 DB status check failed."
}

$privateDir = Join-Path $RepoRoot ".local-secrets"
New-Item -ItemType Directory -Force -Path $privateDir | Out-Null

$secretFile = Join-Path $privateDir "p3-t18-query-preview-readonly.env"

$secretLines = @(
    "# Generated locally. Do not commit.",
    "PPIQ_QUERY_PREVIEW_LOGIN=ppiq_query_preview_login",
    "PPIQ_QUERY_PREVIEW_PASSWORD=$secret",
    "PPIQ_QUERY_PREVIEW_CONNECTION_STRING=Host=$HostName;Port=$Port;Database=$Database;Username=ppiq_query_preview_login;Password=$secret;Include Error Detail=false"
)

[System.IO.File]::WriteAllLines($secretFile, $secretLines, [System.Text.UTF8Encoding]::new($false))

Write-Host "[GREEN] P3-T18 local native DB proof executed." -ForegroundColor Green
Write-Host "Private local secret file: $secretFile"
