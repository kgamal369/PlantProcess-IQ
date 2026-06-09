[CmdletBinding()]
param(
    [string]$PsqlPath = "C:\Program Files\PostgreSQL\16\bin\psql.exe",
    [string]$DbHost = "127.0.0.1",
    [int]$DbPort = 5432,
    [string]$DbName = "plantprocessiq",
    [string]$DbUser = "plantprocess",
    [string]$DbPassword = "plantprocess123",
    [switch]$SkipDotnetTest
)

$ErrorActionPreference = "Stop"

if (-not (Test-Path $PsqlPath)) {
    throw "psql not found at $PsqlPath"
}

$env:PGPASSWORD = $DbPassword

Write-Host "=================================================================================================" -ForegroundColor Cyan
Write-Host "PPIQ T-011 Clean Apply + Full Backend Test" -ForegroundColor Cyan
Write-Host "Database: ${DbHost}:${DbPort}/${DbName}" -ForegroundColor Cyan
Write-Host "=================================================================================================" -ForegroundColor Cyan

$scriptRoot = Join-Path (Get-Location) "Backend\database\scripts"
$seedRoot = Join-Path (Get-Location) "Backend\database\test-seeds"

if (-not (Test-Path $scriptRoot)) {
    throw "Missing script root: $scriptRoot"
}

$scripts = Get-ChildItem $scriptRoot -Filter "*.sql" -File |
    Where-Object {
        $_.Name -match "^[0-9].*\.sql$" -and
        $_.FullName -notmatch "DO_NOT_DEPLOY|_DO_NOT_DEPLOY|manual|legacy|backup"
    } |
    Sort-Object Name

foreach ($script in $scripts) {
    Write-Host "Applying $($script.Name)" -ForegroundColor Yellow
    & $PsqlPath -h $DbHost -p $DbPort -U $DbUser -d $DbName -v ON_ERROR_STOP=1 -f $script.FullName
}

$seed = Join-Path $seedRoot "900_clean_test_auth_seed.sql"
if (-not (Test-Path $seed)) {
    throw "Missing test seed: $seed"
}

Write-Host "Applying test seed 900_clean_test_auth_seed.sql" -ForegroundColor Yellow
& $PsqlPath -h $DbHost -p $DbPort -U $DbUser -d $DbName -v ON_ERROR_STOP=1 -f $seed

Write-Host "Checking AuthStore lineage" -ForegroundColor Yellow
& $PsqlPath -h $DbHost -p $DbPort -U $DbUser -d $DbName -v ON_ERROR_STOP=1 -c "SELECT * FROM public.ppiq_assert_authstore_runtime_lineage();"

$connectionString = "Host=$DbHost;Port=$DbPort;Database=$DbName;Username=$DbUser;Password=$DbPassword"

$env:ASPNETCORE_ENVIRONMENT = "Development"
$env:DOTNET_ENVIRONMENT = "Development"
$env:ConnectionStrings__PlantProcessDb = $connectionString
$env:PPIQ_TEST_CONNECTION_STRING = $connectionString
$env:PLANTPROCESS_ALLOWED_ORIGINS = "http://localhost:5173,http://localhost:3000"

Remove-Item Env:PPIQ_FORCE_EXTERNAL_API_TEST_HOST -ErrorAction SilentlyContinue
Remove-Item Env:PPIQ_USE_WEBAPPLICATION_FACTORY_TEST_HOST -ErrorAction SilentlyContinue
Remove-Item Env:PPIQ_TEST_API_PORT -ErrorAction SilentlyContinue

Write-Host "Building Backend" -ForegroundColor Cyan
dotnet build .\Backend

if (-not $SkipDotnetTest) {
    Write-Host "Running full Backend dotnet test --no-build" -ForegroundColor Cyan
    Push-Location .\Backend
    try {
        dotnet test --no-build
    }
    finally {
        Pop-Location
    }
}

Write-Host "PPIQ T-011 clean apply gate completed." -ForegroundColor Green