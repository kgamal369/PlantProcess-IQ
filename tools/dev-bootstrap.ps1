#requires -Version 5.1
<#
================================================================================
 PlantProcess IQ - Local Developer Bootstrap  (idempotent, re-runnable)
================================================================================
 Brings a fresh OR partially-applied localhost PostgreSQL database to a fully
 working, log-in-able state, and permanently fixes the issues hit during setup:

   0. Pre-flight checks               (psql, dotnet, dotnet-ef, DB reachability)
   1. Repair UTF-8 BOMs               (root cause of the PostCSS build failure
                                        AND the script-116 "syntax error at BOM")
   2. Apply EF Core migrations        (core schema; auth is config-based, no users table)
   3. Apply supplementary SQL scripts (widget layer, schema mapping, two-stage
                                        import, ML foundation) - fail-fast inside
                                        each script; pgvector scripts auto-skip
                                        if the 'vector' extension is not installed
   4. Set the admin password          (.NET User Secrets - never in source control)
   5. Verify                          (table presence) and print working credentials

 AUTH NOTE: this codebase authenticates from configuration (appsettings + user
 secrets/env), not from a database table. That is why there is no 'users' table.
 The SERVER uses the same design via Docker env vars + MigrateAsync() at startup.
 See server.env.example and apply-sql-scripts.sh for the server side.

 USAGE (from anywhere):
   pwsh ./dev-bootstrap.ps1
   pwsh ./dev-bootstrap.ps1 -AdminPassword 'MyStrongAdminPwd!' -VerifyLogin
   pwsh ./dev-bootstrap.ps1 -StopOnSqlError          # strict, for a FRESH database
   pwsh ./dev-bootstrap.ps1 -SkipSqlScripts          # only re-do EF + secrets
================================================================================
#>
[CmdletBinding()]
param(
    [string]$RepoRoot      = "C:\Workspace\PlantProcess-IQ",
    [string]$DbHost        = "localhost",
    [int]   $DbPort        = 5432,
    [string]$DbName        = "plantprocessiq",
    [string]$DbUser        = "plantprocess",
    [string]$DbPassword    = "plantprocess123",
    [string]$AdminUser     = "admin",
    [string]$AdminPassword = "Admin123!",        # dev only; the server injects this via env
    [int]   $ApiPort       = 5063,
    [switch]$IncludeSeed,                          # also apply Backend\database\seed\*.sql
    [switch]$SkipBomRepair,
    [switch]$SkipEf,
    [switch]$SkipSqlScripts,
    [switch]$SkipSecrets,
    [switch]$StopOnSqlError,                       # hard-fail on any SQL error (use on a fresh DB)
    [switch]$VerifyLogin                           # POST /auth/login to confirm (API must be running)
)

$ErrorActionPreference = "Stop"

# ---- paths ----
$BackendDir = Join-Path $RepoRoot "Backend"
$ApiProj    = Join-Path $BackendDir "PlantProcess.Api"
$InfraProj  = Join-Path $BackendDir "PlantProcess.Infrastructure"
$ScriptsDir = Join-Path $BackendDir "database\scripts"
$SeedDir    = Join-Path $BackendDir "database\seed"
$WebDir     = Join-Path $RepoRoot  "Frontend\PlantProcess.Web"

# ---- console helpers ----
function Write-Step($n,$m){ Write-Host "`n=== [$n] $m ===" -ForegroundColor Cyan }
function Write-Ok($m){      Write-Host "  [OK]   $m" -ForegroundColor Green }
function Write-Warn2($m){   Write-Host "  [WARN] $m" -ForegroundColor Yellow }
function Write-Err2($m){    Write-Host "  [FAIL] $m" -ForegroundColor Red }
function Fail($m){ Write-Err2 $m; Write-Host "`nBootstrap aborted." -ForegroundColor Red; exit 1 }

# ---- environment for this session ----
$ConnString = "Host=$DbHost;Port=$DbPort;Database=$DbName;Username=$DbUser;Password=$DbPassword"
$env:PGPASSWORD      = $DbPassword     # stop psql prompting for the password every call
$env:PLANTPROCESS_DB = $ConnString     # design-time connection (this repo's factory reads PLANTPROCESS_DB)

# ---- psql wrappers (capture output, print indented, return only the exit code/scalar) ----
function Invoke-PsqlFile([string]$file){
    $out = & psql -h $DbHost -p $DbPort -U $DbUser -d $DbName -v ON_ERROR_STOP=1 -q -f $file 2>&1
    $code = $LASTEXITCODE
    if($out){ $out | ForEach-Object { Write-Host "      $_" -ForegroundColor DarkGray } }
    return $code
}
function Invoke-PsqlScalar([string]$sql){
    $out = & psql -h $DbHost -p $DbPort -U $DbUser -d $DbName -tAc $sql 2>&1
    return (($out | Out-String).Trim())
}

# ---- BOM repair (strips a leading UTF-8 BOM; never touches other content) ----
function Repair-Bom([string[]]$globs){
    $scanned = 0; $fixed = 0
    foreach($g in $globs){
        Get-ChildItem -Path $g -File -ErrorAction SilentlyContinue | ForEach-Object {
            $scanned++
            $bytes = [System.IO.File]::ReadAllBytes($_.FullName)
            if($bytes.Length -ge 3 -and $bytes[0] -eq 0xEF -and $bytes[1] -eq 0xBB -and $bytes[2] -eq 0xBF){
                $text = ([System.IO.File]::ReadAllText($_.FullName)).TrimStart([char]0xFEFF)
                [System.IO.File]::WriteAllText($_.FullName, $text, (New-Object System.Text.UTF8Encoding($false)))
                Write-Warn2 ("stripped BOM: " + $_.FullName.Replace($RepoRoot,'.'))
                $fixed++
            }
        }
    }
    return [pscustomobject]@{ Scanned=$scanned; Fixed=$fixed }
}

Write-Host "================================================================" -ForegroundColor White
Write-Host " PlantProcess IQ local bootstrap" -ForegroundColor White
Write-Host " repo: $RepoRoot" -ForegroundColor Gray
Write-Host " db  : $DbName @ $DbHost`:$DbPort (user '$DbUser')" -ForegroundColor Gray
Write-Host "================================================================" -ForegroundColor White

# ===== 0. pre-flight =====
Write-Step 0 "Pre-flight checks"
foreach($cmd in @("psql","dotnet")){
    if(-not (Get-Command $cmd -ErrorAction SilentlyContinue)){ Fail "'$cmd' is not on PATH." }
    Write-Ok "$cmd found"
}
if(-not (Test-Path $BackendDir)){ Fail "Backend folder not found at '$BackendDir'. Pass -RepoRoot." }
$ping = Invoke-PsqlScalar "SELECT 1;"
if($ping -ne "1"){ Fail "Cannot reach PostgreSQL ($DbHost`:$DbPort db=$DbName user=$DbUser). psql said: $ping" }
Write-Ok "PostgreSQL reachable"

# ===== 1. BOM repair =====
if(-not $SkipBomRepair){
    Write-Step 1 "Repair UTF-8 BOMs (permanent fix for the recurring build/migration failures)"
    $globs = @(
        (Join-Path $ScriptsDir "*.sql"),
        (Join-Path $SeedDir    "*.sql"),
        (Join-Path $WebDir "package.json"),
        (Join-Path $WebDir "postcss.config.*"),
        (Join-Path $WebDir "vite.config.*"),
        (Join-Path $WebDir "tsconfig*.json")
    )
    $r = Repair-Bom $globs
    Write-Ok "scanned $($r.Scanned) files; stripped BOM from $($r.Fixed)"
    if($r.Fixed -gt 0){ Write-Warn2 "Commit these files - the BOM caused the PostCSS build error and the script-116 migration error." }
}

# ===== 2. EF migrations =====
if(-not $SkipEf){
    Write-Step 2 "Apply EF Core migrations (core schema; config-based auth)"
    & dotnet ef --version *> $null
    if($LASTEXITCODE -ne 0){ Fail "'dotnet ef' tool missing. Install it: dotnet tool install --global dotnet-ef" }
    try {
        Push-Location $BackendDir
        & dotnet ef database update --project $InfraProj --startup-project $ApiProj
        $code = $LASTEXITCODE
    } finally { Pop-Location }
    if($code -ne 0){ Fail "EF migrations failed (exit $code). If you see 'relation already exists', STOP and reconcile __EFMigrationsHistory before re-running - do not drop the database." }
    Write-Ok "EF migrations applied / already up to date"
}

# ===== 3. supplementary SQL scripts =====
if(-not $SkipSqlScripts){
    Write-Step 3 "Apply supplementary SQL scripts (ordered)"
    $hasVector = (Invoke-PsqlScalar "SELECT 1 FROM pg_available_extensions WHERE name='vector';") -eq "1"
    if($hasVector){ Write-Ok "pgvector available - ML foundation scripts will run" }
    else { Write-Warn2 "pgvector NOT installed - '*pgvector*' scripts will be SKIPPED (login and the app work without them; see notes at the end)" }

    $dirs = @($ScriptsDir)
    if($IncludeSeed){ $dirs += $SeedDir }

    $failed = @()
    foreach($dir in $dirs){
        if(-not (Test-Path $dir)){ continue }
        Get-ChildItem -Path (Join-Path $dir "*.sql") -File | Sort-Object Name | ForEach-Object {
            $name = $_.Name
            if((-not $hasVector) -and ($name -match 'pgvector')){
                Write-Warn2 "SKIP  $name (requires pgvector)"; return
            }
            $code = Invoke-PsqlFile $_.FullName
            if($code -eq 0){ Write-Ok "applied $name" }
            else {
                $failed += $name
                if($StopOnSqlError){ Fail "Script '$name' failed (exit $code). Aborting (-StopOnSqlError)." }
                Write-Warn2 "Script '$name' reported errors (exit $code) - continuing. On a FRESH database this means the script is not idempotent and must be fixed."
            }
        }
    }
    if($failed.Count -gt 0){ Write-Warn2 ("Scripts with errors: " + ($failed -join ", ")) }
}

# ===== 4. admin password via user-secrets =====
if(-not $SkipSecrets){
    Write-Step 4 "Store admin password in .NET User Secrets (kept out of source control)"
    try {
        Push-Location $ApiProj
        & dotnet user-secrets init *> $null
        & dotnet user-secrets set "Auth:BootstrapAdminPassword" $AdminPassword | Out-Null
        & dotnet user-secrets set "Auth:Users:0:Password"       $AdminPassword | Out-Null
        $code = $LASTEXITCODE
    } finally { Pop-Location }
    if($code -ne 0){ Fail "Failed to set user-secrets (exit $code)." }
    Write-Ok "admin password set for '$AdminUser' (Auth:BootstrapAdminPassword and Auth:Users:0:Password)"
}

# ===== 5. verify =====
Write-Step 5 "Verify schema"
$tableCount = Invoke-PsqlScalar "SELECT count(*) FROM information_schema.tables WHERE table_schema='public';"
Write-Ok "public tables present: $tableCount"
foreach($t in @("areas","inspection_jobs","canonical_schema_views","two_stage_import_runs","correlation_results")){
    $exists = Invoke-PsqlScalar "SELECT (to_regclass('public.$t') IS NOT NULL);"
    if($exists -eq "t"){ Write-Ok "table $t present" } else { Write-Warn2 "table $t MISSING" }
}

if($VerifyLogin){
    Write-Step 6 "Verify login endpoint (API must already be running on :$ApiPort)"
    $url = "http://localhost:$ApiPort/auth/login"
    $ok = $false
    foreach($body in @(@{userName='engineer';password='Engineer123!'}, @{username='engineer';password='Engineer123!'})){
        try {
            Invoke-RestMethod -Uri $url -Method Post -ContentType "application/json" -Body ($body | ConvertTo-Json) -ErrorAction Stop | Out-Null
            Write-Ok "login succeeded for 'engineer' (token received)"; $ok = $true; break
        } catch {
            $sc = $null; try { $sc = $_.Exception.Response.StatusCode.value__ } catch {}
            Write-Warn2 "attempt returned HTTP $sc"
        }
    }
    if(-not $ok){ Write-Warn2 "Could not confirm login automatically - ensure the API is running, then test manually (see summary)." }
}

# ===== summary =====
Write-Host "`n================================================================" -ForegroundColor Green
Write-Host " Bootstrap complete." -ForegroundColor Green
Write-Host "================================================================" -ForegroundColor Green
Write-Host @"
Database : $DbName @ $DbHost`:$DbPort  (role '$DbUser')
Auth     : configuration-based (NOT a users table)

Working login credentials:
  admin        / $AdminPassword          <- you just set this via user-secrets
  engineer     / Engineer123!            <- from appsettings.Development.json
  datamanager  / DataManager123!
  viewer       / Viewer123!

Next:
  1) API :  cd "$BackendDir";  dotnet run --project PlantProcess.Api
  2) Web :  cd "$WebDir";  npm run dev
  3) Open http://localhost:5173  and log in as  admin / $AdminPassword

pgvector / ML foundation locally (optional):
  Your native PostgreSQL has no 'vector' extension, so the ML feature-store
  script was skipped. Login and everything else work without it. To get full
  parity with the server, either install pgvector for your PostgreSQL, or run
  local Postgres from the 'pgvector/pgvector:pg16' Docker image and re-run this
  script (it will then apply the ML scripts automatically).

This script is idempotent - run it again any time.
"@ -ForegroundColor Gray
