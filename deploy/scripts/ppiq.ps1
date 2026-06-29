#requires -Version 5.1
# ==================================================================================================
# deploy/scripts/ppiq.ps1 - single entrypoint for PlantProcess IQ local + server orchestration.
#
#   ppiq.ps1 <command> [-Server] [-NoSources]
#
#   up           start API + web   (local: dotnet/npm on host ; -Server: full compose stack)
#   up-sources   start the demo source containers
#   migrate      apply Backend/database/scripts/*.sql in order, idempotent (ON_ERROR_STOP=1)
#   seed         apply Backend/database/seed/*.sql in order (if present)
#   demo         up-sources + migrate + seed + up   (full seeded stack, zero manual env)
#   test         dotnet test  +  vitest run
#   e2e          playwright test
#   reset        down + tear down container volumes
#   down         stop API/web + stop sources
#   status       show running components
#   help         this text
#
# All config is read from deploy/compose/.env.dev. No verb requires a hand-set environment variable.
# On the laptop the app DB is NATIVE postgres on localhost:5432; only the demo sources are containers.
# ==================================================================================================
[CmdletBinding()]
param(
  [Parameter(Position=0)]
  [ValidateSet('up','up-sources','migrate','seed','test','e2e','demo','reset','down','status','init-db','help')]
  [string]$Command = 'help',
  [switch]$Server,
  [switch]$NoSources
)

$ErrorActionPreference = 'Continue'   # stream native output; we check $LASTEXITCODE ourselves
$env:PGCLIENTENCODING = 'UTF8'   # psql must read the UTF-8 .sql files; Windows default client_encoding is WIN1252

# ---- paths ---------------------------------------------------------------------------------------
$ScriptDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$RepoRoot  = $ScriptDir
while($RepoRoot -and -not (Test-Path (Join-Path $RepoRoot '.git'))){
  $p = Split-Path -Parent $RepoRoot
  if(-not $p -or $p -eq $RepoRoot){ break }
  $RepoRoot = $p
}
Set-Location $RepoRoot

$ComposeBase           = 'deploy/compose/docker-compose.yml'
$ComposeLocal          = 'deploy/compose/docker-compose.local.yml'
$ComposeServer         = 'deploy/compose/docker-compose.server.yml'
$ComposeSources        = 'deploy/compose/docker-compose.sources.yml'
$EnvFile               = 'deploy/compose/.env.dev'
$Project               = 'plantprocessiq'
$ApiProj               = 'Backend/PlantProcess.Api'
$WebDir                = 'Frontend/PlantProcess.Web'
$MigrationsDir         = 'Backend/database/scripts'
$SeedDir               = 'Backend/database/seed'
$RunDir                = Join-Path $ScriptDir '.ppiq-run'

# ---- helpers -------------------------------------------------------------------------------------
function Say($m){ Write-Host ("ppiq> " + $m) -ForegroundColor Cyan }
function Die($m){ Write-Host ("ppiq: " + $m) -ForegroundColor Red; exit 1 }
function Assert-Exit($what){ if($LASTEXITCODE -ne 0){ Die ($what + " failed (exit " + $LASTEXITCODE + ")") } }

function Import-DotEnv {
  $f = Join-Path $RepoRoot $EnvFile
  if(-not (Test-Path $f)){ Die ($EnvFile + " not found - run the M1-T01/T02 setup first") }
  foreach($line in (Get-Content $f)){
    $t = ("" + $line).Trim()
    if($t -eq '' -or $t.StartsWith('#')){ continue }
    $i = $t.IndexOf('=')
    if($i -lt 1){ continue }
    $k = $t.Substring(0,$i).Trim()
    $v = $t.Substring($i+1).Trim()
    if($v.Length -ge 2){
      $a = $v.Substring(0,1); $b = $v.Substring($v.Length-1,1)
      if(($a -eq '"' -and $b -eq '"') -or ($a -eq "'" -and $b -eq "'")){ $v = $v.Substring(1,$v.Length-2) }
    }
    Set-Item -Path ("Env:" + $k) -Value $v
  }
}

function Get-Pg {
  $h = $env:POSTGRES_HOST; if([string]::IsNullOrEmpty($h)){ $h = 'localhost' }
  $port = $env:POSTGRES_PORT; if([string]::IsNullOrEmpty($port)){ $port = '5432' }
  $u = $env:POSTGRES_USER; if([string]::IsNullOrEmpty($u)){ $u = 'ppiq_dev' }
  $d = $env:POSTGRES_DB;   if([string]::IsNullOrEmpty($d)){ $d = 'ppiq_app' }
  return [pscustomobject]@{ Host=$h; Port=$port; User=$u; Db=$d; Pass=$env:POSTGRES_PASSWORD }
}

function Compose-Sources {
  if(Test-Path (Join-Path $RepoRoot $ComposeSources)){ return $ComposeSources }
  Die ($ComposeSources + ' not found - the canonical demo sources compose is required (one canonical sources file)')
}

# ---- verbs ---------------------------------------------------------------------------------------
function Do-UpSources {
  $src = Compose-Sources
  Say ("starting demo sources (" + $src + ")")
  & docker compose -p $Project --env-file $EnvFile -f $src up -d
  Assert-Exit "sources up"
  Say "sources up"
}

function Ensure-AppDb {
  # Align the native app role + database to .env.dev via a superuser connection.
  # Requires PPIQ_PG_SUPERPASSWORD (superuser defaults to 'postgres'; override with PPIQ_PG_SUPERUSER).
  # Assumes standard lowercase identifiers for POSTGRES_USER / POSTGRES_DB (the dev defaults).
  $pg = Get-Pg
  $suUser = $env:PPIQ_PG_SUPERUSER; if([string]::IsNullOrEmpty($suUser)){ $suUser = 'postgres' }
  $suPass = $env:PPIQ_PG_SUPERPASSWORD
  if([string]::IsNullOrEmpty($suPass)){ Die "init-db needs the Postgres superuser password: set `$env:PPIQ_PG_SUPERPASSWORD='<postgres password>' (and PPIQ_PG_SUPERUSER if not 'postgres'), then re-run" }
  if([string]::IsNullOrEmpty($pg.Pass)){ Die "POSTGRES_PASSWORD is not set in .env.dev - cannot align the app role password" }
  $env:PGPASSWORD = $suPass
  $roleEsc = ($pg.User -replace "'","''")
  $dbEsc   = ($pg.Db -replace "'","''")
  $passEsc = ($pg.Pass -replace "'","''")
  Say ("init-db: superuser " + $suUser + " -> ensuring role '" + $pg.User + "' + database '" + $pg.Db + "' on " + $pg.Host + ":" + $pg.Port)
  $roleExists = ("" + (& psql -h $pg.Host -p $pg.Port -U $suUser -d postgres -tAc ("SELECT 1 FROM pg_roles WHERE rolname='" + $roleEsc + "'"))).Trim()
  if($LASTEXITCODE -ne 0){ Die ("cannot connect as superuser '" + $suUser + "' (check PPIQ_PG_SUPERPASSWORD and pg_hba.conf)") }
  if($roleExists -eq '1'){
    & psql -h $pg.Host -p $pg.Port -U $suUser -d postgres -v ON_ERROR_STOP=1 -c ("ALTER ROLE " + $pg.User + " WITH LOGIN PASSWORD '" + $passEsc + "'") | Out-Null
    Assert-Exit "align role password"
    Say ("role '" + $pg.User + "' password aligned to .env.dev")
  } else {
    & psql -h $pg.Host -p $pg.Port -U $suUser -d postgres -v ON_ERROR_STOP=1 -c ("CREATE ROLE " + $pg.User + " WITH LOGIN PASSWORD '" + $passEsc + "'") | Out-Null
    Assert-Exit "create role"
    Say ("role '" + $pg.User + "' created")
  }
  $dbExists = ("" + (& psql -h $pg.Host -p $pg.Port -U $suUser -d postgres -tAc ("SELECT 1 FROM pg_database WHERE datname='" + $dbEsc + "'"))).Trim()
  if($dbExists -ne '1'){
    & psql -h $pg.Host -p $pg.Port -U $suUser -d postgres -v ON_ERROR_STOP=1 -c ("CREATE DATABASE " + $pg.Db + " OWNER " + $pg.User) | Out-Null
    Assert-Exit "create database"
    Say ("database '" + $pg.Db + "' created (owner " + $pg.User + ")")
  } else {
    Say ("database '" + $pg.Db + "' present")
  }
}

function Do-InitDb { Import-DotEnv; Ensure-AppDb; Say "init-db complete" }

function Register-DevLicenseKey {
  # DEV-ONLY: registers the committed development Ed25519 license public key so the signed
  # dev tokens (deploy/fixtures/license/*.token) verify and activate. Production registers
  # the customer signing key via the ops path and never ships this dev key. Idempotent.
  if($env:ASPNETCORE_ENVIRONMENT -eq 'Production'){ Say "skipping dev Ed25519 key registration (ASPNETCORE_ENVIRONMENT=Production)"; return }
  $pubFile = Join-Path $RepoRoot 'deploy/fixtures/license/dev_public.b64'
  $keySql  = Join-Path $RepoRoot 'Backend/database/seed/dev_ed25519_public_key.sql'
  if(-not (Test-Path $pubFile)){ Say "dev_public.b64 absent - skipping dev license key registration"; return }
  if(-not (Test-Path $keySql)){ Say "dev_ed25519_public_key.sql absent - skipping dev license key registration"; return }
  $pg = Get-Pg
  $env:PGPASSWORD = $pg.Pass
  $pub = ([System.IO.File]::ReadAllText($pubFile)).Trim()
  if([string]::IsNullOrEmpty($pub)){ Die "dev_public.b64 is empty" }
  $tenant = '00000000-0000-0000-0000-000000000001'
  $kid = 'ppiq-dev-ed25519'
  Say ("registering dev Ed25519 license public key (kid=" + $kid + ", tenant=" + $tenant + ")")
  $psqlArgs = @('-h', $pg.Host, '-p', $pg.Port, '-U', $pg.User, '-d', $pg.Db, '-v', 'ON_ERROR_STOP=1', '-q', '-v', ('tenant_id=' + $tenant), '-v', ('key_id=' + $kid), '-v', ('public_key_b64=' + $pub), '-f', $keySql)
  & psql @psqlArgs
  Assert-Exit "register dev Ed25519 public key"
  Say "dev Ed25519 license public key registered"
}

function Do-Migrate {
  # >>> PPIQ-V1-01 ef-first (idempotent) >>>
  if (-not $env:ConnectionStrings__PlantProcessDb) {
    $efCfg = Join-Path $PSScriptRoot '..\..\Backend\PlantProcess.Api\appsettings.Development.json'
    if (Test-Path $efCfg) {
      try {
        $efObj = Get-Content $efCfg -Raw | ConvertFrom-Json
        if ($efObj.ConnectionStrings -and $efObj.ConnectionStrings.PlantProcessDb) { $env:ConnectionStrings__PlantProcessDb = $efObj.ConnectionStrings.PlantProcessDb }
      } catch { }
    }
  }
  if (-not $env:ConnectionStrings__PlantProcessDb) {
    $env:ConnectionStrings__PlantProcessDb = 'Host=localhost;Port=5432;Database=ppiq_app;Username=ppiq_dev;Password=ppiq_dev_local_only'
  }
  Push-Location (Join-Path $PSScriptRoot '..\..\Backend\PlantProcess.Api')
  try { dotnet ef database update --project ..\PlantProcess.Infrastructure --startup-project . } finally { Pop-Location }
  if ($LASTEXITCODE -ne 0) { throw 'EF database update failed' }
  # <<< PPIQ-V1-01 ef-first <
  Import-DotEnv
  $pg = Get-Pg
  if(-not [string]::IsNullOrEmpty($env:PPIQ_PG_SUPERPASSWORD)){ Ensure-AppDb }
  $env:PGPASSWORD = $pg.Pass
  # EF schema FIRST: model-first migrations create the canonical tables before the ordered SQL layers on top.
  $dsn = ("Host=" + $pg.Host + ";Port=" + $pg.Port + ";Database=" + $pg.Db + ";Username=" + $pg.User + ";Password=" + $pg.Pass)
  $env:PLANTPROCESS_DB = $dsn
  $env:ConnectionStrings__PlantProcessDb = $dsn
  $infra = Join-Path $RepoRoot "Backend/PlantProcess.Infrastructure"
  $apip  = Join-Path $RepoRoot "Backend/PlantProcess.Api"
  Say "applying EF migrations (dotnet ef database update)"
  & dotnet ef database update --project $infra --startup-project $apip --no-build
  if($LASTEXITCODE -ne 0){ & dotnet ef database update --project $infra --startup-project $apip; Assert-Exit "ef database update" }
  
$dir = Join-Path $RepoRoot $MigrationsDir
  if(-not (Test-Path $dir)){ Die ($MigrationsDir + " not found") }
  $files = Get-ChildItem $dir -Filter *.sql | Sort-Object Name
  if(-not $files -or $files.Count -eq 0){ Die ("no .sql migrations in " + $MigrationsDir) }
  Say ("applying " + $files.Count + " migrations to " + $pg.Host + ":" + $pg.Port + "/" + $pg.Db + " as " + $pg.User)
  foreach($f in $files){
    $psqlArgs = @('-h', $pg.Host, '-p', $pg.Port, '-U', $pg.User, '-d', $pg.Db, '-v', 'ON_ERROR_STOP=1', '-q', '-f', $f.FullName)
    if(-not [string]::IsNullOrEmpty($env:PPIQ_APP_RUNTIME_PASSWORD)){ $psqlArgs = @('-v', ('plantprocess_app_password=' + $env:PPIQ_APP_RUNTIME_PASSWORD)) + $psqlArgs }
    & psql @psqlArgs
    Assert-Exit ("migration " + $f.Name)
  }
  Say "migrations applied"
  Register-DevLicenseKey
}

function Do-Seed {
  Import-DotEnv
  $pg = Get-Pg
  $env:PGPASSWORD = $pg.Pass
  $dir = Join-Path $RepoRoot $SeedDir
  if(-not (Test-Path $dir)){ Say ($SeedDir + " not present - nothing to seed"); return }
  $files = Get-ChildItem $dir -Filter *.sql | Sort-Object Name
  if(-not $files -or $files.Count -eq 0){ Say "no seed files - nothing to seed"; return }
  Say ("applying " + $files.Count + " seed files to " + $pg.Db)
  foreach($f in $files){
    $psqlArgs = @('-h', $pg.Host, '-p', $pg.Port, '-U', $pg.User, '-d', $pg.Db, '-v', 'ON_ERROR_STOP=1', '-q', '-f', $f.FullName)
    if(-not [string]::IsNullOrEmpty($env:PPIQ_APP_RUNTIME_PASSWORD)){ $psqlArgs = @('-v', ('plantprocess_app_password=' + $env:PPIQ_APP_RUNTIME_PASSWORD)) + $psqlArgs }
    & psql @psqlArgs
    Assert-Exit ("seed " + $f.Name)
  }
  Say "seeds applied"
}

function Do-Up {
  Import-DotEnv
  if($Server){
    Say "starting full compose stack (-Server)"
    & docker compose -p $Project --env-file $EnvFile -f $ComposeBase -f $ComposeServer up -d --build
    Assert-Exit "stack up"
    Say "stack up"
    return
  }
  New-Item -ItemType Directory -Force -Path $RunDir | Out-Null
  if([string]::IsNullOrEmpty($env:ASPNETCORE_ENVIRONMENT)){ $env:ASPNETCORE_ENVIRONMENT = 'Development' }
  if([string]::IsNullOrEmpty($env:ASPNETCORE_URLS)){ $env:ASPNETCORE_URLS = 'http://0.0.0.0:5063' }
  if([string]::IsNullOrEmpty($env:ConnectionStrings__PlantProcessDb)){
    $pg = Get-Pg
    $env:ConnectionStrings__PlantProcessDb = ('Host=' + $pg.Host + ';Port=' + $pg.Port + ';Database=' + $pg.Db + ';Username=' + $pg.User + ';Password=' + $pg.Pass)
  }
  Say ('starting API (dotnet run, Development, ' + $ApiProj + ') - a console window will open with Kestrel logs')
  # >>> PPIQ-V1-17 dev-sysadmin (idempotent) >>>
  # Provision the permanent local sysadmin (SOU support account) via the env-var form
  # the rest of the product uses (ensure-runtime-env.sh / Playwright). The nested JSON
  # Users array in appsettings.Development.json does not bind reliably; env vars do.
  # Development-only: ResolveDevelopmentUser is gated by IWebHostEnvironment.IsDevelopment().
  if ($env:ASPNETCORE_ENVIRONMENT -eq 'Development') {
    if (-not $env:PlantProcess__Auth__Users__0__UserName) { $env:PlantProcess__Auth__Users__0__UserName = 'sysadmin' }
    if (-not $env:PlantProcess__Auth__Users__0__Password) { $env:PlantProcess__Auth__Users__0__Password = 'PpiqLocalDev_Sysadmin_2026!' }
    if (-not $env:PlantProcess__Auth__Users__0__Role) { $env:PlantProcess__Auth__Users__0__Role = 'Admin' }
    if (-not $env:PlantProcess__Auth__Users__0__DisplayName) { $env:PlantProcess__Auth__Users__0__DisplayName = 'PPIQ System Administrator (local dev)' }
    if (-not $env:PlantProcess__Auth__Users__0__IsBootstrapAdmin) { $env:PlantProcess__Auth__Users__0__IsBootstrapAdmin = 'false' }
  }
  # <<< PPIQ-V1-17 dev-sysadmin <<<
  $api = Start-Process -FilePath 'dotnet' -ArgumentList @('run','--project',(Join-Path $RepoRoot $ApiProj),'--no-launch-profile') -PassThru -WindowStyle Normal
  $api.Id | Set-Content (Join-Path $RunDir 'api.pid')
  Say ("starting web (npm run dev, " + $WebDir + ")")
  Push-Location (Join-Path $RepoRoot $WebDir)
  $web = Start-Process -FilePath 'npm' -ArgumentList @('run','dev') -PassThru -WindowStyle Hidden
  Pop-Location
  $web.Id | Set-Content (Join-Path $RunDir 'web.pid')
  Say ("API pid " + $api.Id + " -> http://localhost:5063   web pid " + $web.Id + " -> http://localhost:5173")
}

function Do-Demo {
  if(-not $NoSources){ Do-UpSources }
  Do-Migrate
  Do-Seed
  Do-Up
  Say "demo is up. API http://localhost:5063   web http://localhost:5173"
}

function Stop-Tracked {
  foreach($n in @('api','web')){
    $pf = Join-Path $RunDir ($n + '.pid')
    if(Test-Path $pf){
      $procId = (Get-Content $pf | Select-Object -First 1)
      if($procId){ try { Stop-Process -Id ([int]$procId) -Force -ErrorAction SilentlyContinue } catch {} }
      Remove-Item $pf -Force -ErrorAction SilentlyContinue
    }
  }
}

function Do-Down {
  if($Server){ & docker compose -p $Project --env-file $EnvFile -f $ComposeBase -f $ComposeServer down }
  Stop-Tracked
  $src = Compose-Sources
  & docker compose -p $Project --env-file $EnvFile -f $src down
  Say "down"
}

function Do-Reset {
  Say "stopping running components"
  Stop-Tracked
  $src = Compose-Sources
  Say "tearing down source volumes"
  & docker compose -p $Project --env-file $EnvFile -f $src down -v
  if($Server){
    Say "tearing down app stack volumes (-Server)"
    & docker compose -p $Project --env-file $EnvFile -f $ComposeBase -f $ComposeServer down -v
  } else {
    Say "local mode: native app DB is left intact (migrations are idempotent; use -Server for a full volume wipe)"
  }
  Say "reset complete - run 'ppiq.ps1 demo' to rebuild"
}

function Do-Test {
  # >>> PPIQ-V1-03 test-env (idempotent) >>>
  if (-not $env:PGCLIENTENCODING) { $env:PGCLIENTENCODING = 'UTF8' }
  if (-not $env:PPIQ_FORCE_EXTERNAL_API_TEST_HOST) { $env:PPIQ_FORCE_EXTERNAL_API_TEST_HOST = '1' }
  # <<< PPIQ-V1-03 test-env <<<
  Import-DotEnv
  if([string]::IsNullOrEmpty($env:PPIQ_TEST_CONNECTION_STRING)){
    $pg = Get-Pg
    $env:PPIQ_TEST_CONNECTION_STRING = ("Host=" + $pg.Host + ";Port=" + $pg.Port + ";Database=plantprocess_test_db;Username=" + $pg.User + ";Password=" + $pg.Pass)
    Say "PPIQ_TEST_CONNECTION_STRING defaulted to plantprocess_test_db (must exist)"
  }
  $tp = if([string]::IsNullOrEmpty($env:PPIQ_TEST_API_PORT)){ '15063' } else { $env:PPIQ_TEST_API_PORT }
  Get-NetTCPConnection -LocalPort ([int]$tp) -State Listen -ErrorAction SilentlyContinue | Select-Object -ExpandProperty OwningProcess -Unique | ForEach-Object { Stop-Process -Id $_ -Force -ErrorAction SilentlyContinue }
  Do-Migrate
  # >>> PPIQ-V1-03 api-host-guard (idempotent) >>>
  # Kill any stray PPIQ API host (demo or prior external test host) so dotnet test
  # cannot wedge on a locked bin or wait forever on a non-terminating external host.
  Get-CimInstance Win32_Process -Filter "Name='PlantProcess.Api.exe'" -ErrorAction SilentlyContinue | ForEach-Object { & taskkill /PID $_.ProcessId /T /F 2>$null | Out-Null }
  Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -match 'run .*PlantProcess\.Api' } | ForEach-Object { & taskkill /PID $_.ProcessId /T /F 2>$null | Out-Null }
  Start-Sleep -Seconds 2
  # <<< PPIQ-V1-03 api-host-guard <<<
  Say "backend: dotnet test"
  Push-Location (Join-Path $RepoRoot 'Backend')
  & dotnet test --nologo
  $bt = $LASTEXITCODE
  Pop-Location
  Say "frontend: vitest run"
  Push-Location (Join-Path $RepoRoot $WebDir); & npx vitest run; $ft = $LASTEXITCODE; Pop-Location
  # >>> PPIQ-V1-03 api-host-cleanup (idempotent) >>>
  Get-CimInstance Win32_Process -Filter "Name='PlantProcess.Api.exe'" -ErrorAction SilentlyContinue | ForEach-Object { & taskkill /PID $_.ProcessId /T /F 2>$null | Out-Null }
  # <<< PPIQ-V1-03 api-host-cleanup <<<
  if($bt -ne 0 -or $ft -ne 0){ Die ("tests failed (backend=" + $bt + " frontend=" + $ft + ")") }
  Say "tests passed"
}

function Do-E2e {
  Import-DotEnv
  # >>> PPIQ-V1-12 e2e-api-host-guard (idempotent) >>>
  # Playwright webServer launches its own API via dotnet run; kill any stray API host
  # first so the e2e build cannot wedge on a locked bin\Debug (same failure as Do-Test).
  Get-CimInstance Win32_Process -Filter "Name='PlantProcess.Api.exe'" -ErrorAction SilentlyContinue | ForEach-Object { & taskkill /PID $_.ProcessId /T /F 2>$null | Out-Null }
  Get-CimInstance Win32_Process -Filter "Name='dotnet.exe'" -ErrorAction SilentlyContinue | Where-Object { $_.CommandLine -match 'run .*PlantProcess\.Api' } | ForEach-Object { & taskkill /PID $_.ProcessId /T /F 2>$null | Out-Null }
  Start-Sleep -Seconds 2
  # <<< PPIQ-V1-12 e2e-api-host-guard <<<
  Do-Migrate
  Push-Location (Join-Path $RepoRoot $WebDir); & npx playwright test; $e = $LASTEXITCODE; Pop-Location
  if($e -ne 0){ Die "e2e failed" }
  Say "e2e passed"
}

function Do-Status {
  $src = Compose-Sources
  & docker compose -p $Project --env-file $EnvFile -f $src ps
  foreach($n in @('api','web')){
    $pf = Join-Path $RunDir ($n + '.pid')
    if(Test-Path $pf){ Say ($n + " pid " + (Get-Content $pf | Select-Object -First 1)) }
  }
}

function Do-Help {
  Write-Host ""
  Write-Host "ppiq.ps1 <command> [-Server] [-NoSources]" -ForegroundColor White
  Write-Host "  up           start API + web (local: on host ; -Server: full compose stack)"
  Write-Host "  up-sources   start the demo source containers"
  Write-Host "  init-db      create/align the ppiq_dev role + ppiq_app DB from .env.dev (needs PPIQ_PG_SUPERPASSWORD)"
  Write-Host "  migrate      apply Backend/database/scripts/*.sql in order (idempotent)"
  Write-Host "  seed         apply Backend/database/seed/*.sql in order"
  Write-Host "  demo         up-sources + migrate + seed + up  (full seeded stack)"
  Write-Host "  test         dotnet test + vitest"
  Write-Host "  e2e          playwright"
  Write-Host "  reset        down + tear down container volumes"
  Write-Host "  down         stop API/web + sources"
  Write-Host "  status       show running components"
  Write-Host ""
}

switch($Command){
  'up'         { Do-Up }
  'up-sources' { Do-UpSources }
  'migrate'    { Do-Migrate }
  'init-db'    { Do-InitDb }
  'seed'       { Do-Seed }
  'test'       { Do-Test }
  'e2e'        { Do-E2e }
  'demo'       { Do-Demo }
  'reset'      { Do-Reset }
  'down'       { Do-Down }
  'status'     { Do-Status }
  default      { Do-Help }
}
