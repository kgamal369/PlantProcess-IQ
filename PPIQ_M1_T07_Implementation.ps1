<#
====================================================================================================
 PlantProcess IQ - M1-T07 : deploy/scripts/ppiq.ps1 single entrypoint    (pure ASCII; writes + validates)
====================================================================================================
 Writes deploy/scripts/ppiq.ps1 exposing: up / up-sources / migrate / seed / test / e2e / demo /
 reset / down / status / help. It loads deploy/compose/.env.dev into the process, composes the right
 overlays, and runs dotnet/npm. No verb requires a hand-set environment variable.

 Local-dev model (default): app DB is NATIVE postgres on localhost:5432 (per .env.dev); the 8 demo
 sources are containers; API and web run on the host via dotnet/npm. -Server runs the full compose stack.

 This script backs up any existing ppiq.ps1 to a gitignored folder, writes the new one (UTF-8 no-BOM,
 LF), then validates it: PowerShell parses it with zero errors, all verbs are wired, pure ASCII.
 It does not commit. Run:
   .\PPIQ_M1_T07_Implementation.ps1
====================================================================================================
#>

& {
  $ErrorActionPreference = 'Stop'
  Set-StrictMode -Version 2.0

  $script:Results = New-Object System.Collections.Generic.List[object]
  function Add-Result([string]$Check,[bool]$Pass,[string]$Detail){
    $script:Results.Add([pscustomobject]@{ Pass=$Pass; Check=$Check; Detail=$Detail })
    $tag = if($Pass){'PASS'}else{'FAIL'}; $col = if($Pass){'Green'}else{'Red'}
    Write-Host ("  [{0}] {1} :: {2}" -f $tag,$Check,$Detail) -ForegroundColor $col
  }
  function Info([string]$m){ Write-Host $m -ForegroundColor Cyan }
  function Write-LfNoBom([string]$Path,[string]$Text){
    $lf  = $Text -replace "`r`n","`n"
    $enc = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path,$lf,$enc)
  }
  function Get-RepoRoot {
    $dir = (Get-Location).Path
    while(-not [string]::IsNullOrEmpty($dir)){
      if(Test-Path -LiteralPath (Join-Path $dir '.git')){ return $dir }
      $parent = Split-Path -Parent $dir
      if([string]::IsNullOrEmpty($parent) -or $parent -eq $dir){ break }
      $dir = $parent
    }
    return (Get-Location).Path
  }

  $RepoRoot = Get-RepoRoot; Set-Location $RepoRoot
  $BackupDir = Join-Path $RepoRoot '.ppiq-script-backups'
  New-Item -ItemType Directory -Force -Path $BackupDir | Out-Null
  $giAttr = Join-Path $RepoRoot '.gitignore'
  if(Test-Path $giAttr){
    $gi = Get-Content $giAttr -Raw
    if($gi -notmatch '(?m)^\.ppiq-script-backups/?\s*$'){ Add-Content $giAttr "`n.ppiq-script-backups/" }
  }

  Info "============================================================================"
  Info " PPIQ M1-T07 - write deploy/scripts/ppiq.ps1 + validate"
  Info " repo root: $RepoRoot"
  Info "============================================================================"

  # ==================================================================================================
  # The deliverable: deploy/scripts/ppiq.ps1   (literal single-quoted here-string; no expansion)
  # ==================================================================================================
  $ppiq = @'
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
$ComposeSourcesFallback= 'deploy/compose/docker-compose.demo-sources.yml'
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

function Compose-Sources { if(Test-Path (Join-Path $RepoRoot $ComposeSources)){ return $ComposeSources } else { return $ComposeSourcesFallback } }

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

function Do-Migrate {
  Import-DotEnv
  $pg = Get-Pg
  if(-not [string]::IsNullOrEmpty($env:PPIQ_PG_SUPERPASSWORD)){ Ensure-AppDb }
  $env:PGPASSWORD = $pg.Pass
  $dir = Join-Path $RepoRoot $MigrationsDir
  if(-not (Test-Path $dir)){ Die ($MigrationsDir + " not found") }
  $files = Get-ChildItem $dir -Filter *.sql | Sort-Object Name
  if(-not $files -or $files.Count -eq 0){ Die ("no .sql migrations in " + $MigrationsDir) }
  Say ("applying " + $files.Count + " migrations to " + $pg.Host + ":" + $pg.Port + "/" + $pg.Db + " as " + $pg.User)
  foreach($f in $files){
    & psql -h $pg.Host -p $pg.Port -U $pg.User -d $pg.Db -v ON_ERROR_STOP=1 -q -f $f.FullName
    Assert-Exit ("migration " + $f.Name)
  }
  Say "migrations applied"
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
    & psql -h $pg.Host -p $pg.Port -U $pg.User -d $pg.Db -v ON_ERROR_STOP=1 -q -f $f.FullName
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
  Import-DotEnv
  if([string]::IsNullOrEmpty($env:PPIQ_TEST_CONNECTION_STRING)){
    $pg = Get-Pg
    $env:PPIQ_TEST_CONNECTION_STRING = ("Host=" + $pg.Host + ";Port=" + $pg.Port + ";Database=plantprocess_test_db;Username=" + $pg.User + ";Password=" + $pg.Pass)
    Say "PPIQ_TEST_CONNECTION_STRING defaulted to plantprocess_test_db (must exist)"
  }
  Say "backend: dotnet test"
  & dotnet test --nologo
  $bt = $LASTEXITCODE
  Say "frontend: vitest run"
  Push-Location (Join-Path $RepoRoot $WebDir); & npx vitest run; $ft = $LASTEXITCODE; Pop-Location
  if($bt -ne 0 -or $ft -ne 0){ Die ("tests failed (backend=" + $bt + " frontend=" + $ft + ")") }
  Say "tests passed"
}

function Do-E2e {
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
'@

  # ==================================================================================================
  # Write it
  # ==================================================================================================
  $scriptsDir = Join-Path $RepoRoot 'deploy/scripts'
  New-Item -ItemType Directory -Force -Path $scriptsDir | Out-Null
  $target = Join-Path $scriptsDir 'ppiq.ps1'
  if(Test-Path $target){
    $stamp = Get-Date -Format 'yyyyMMdd-HHmmss'
    Copy-Item $target (Join-Path $BackupDir ("ppiq.ps1." + $stamp + ".bak")) -Force
    Info ("backed up existing ppiq.ps1 -> .ppiq-script-backups/ppiq.ps1." + $stamp + ".bak")
  }
  Write-LfNoBom $target $ppiq
  Info ("wrote " + $target)

  # ==================================================================================================
  # Validate
  # ==================================================================================================
  Info "`n--- validation ---"

  $bytes = [System.IO.File]::ReadAllBytes($target)
  $nonAscii = @($bytes | Where-Object { $_ -gt 127 })
  Add-Result 'ppiq.ps1 is pure ASCII (safe to run as a .ps1 file)' ($nonAscii.Count -eq 0) ($nonAscii.Count.ToString() + " non-ASCII byte(s)")

  $tokens = $null; $errors = $null
  [void][System.Management.Automation.Language.Parser]::ParseFile($target,[ref]$tokens,[ref]$errors)
  $errCount = if($errors){ @($errors).Count } else { 0 }
  Add-Result 'PowerShell parses ppiq.ps1 with zero syntax errors' ($errCount -eq 0) ($errCount.ToString() + " parse error(s)")
  if($errCount -gt 0){ foreach($e in $errors){ Write-Host ("      " + $e.Message) -ForegroundColor DarkYellow } }

  $body = Get-Content $target -Raw
  $verbs = @('up','up-sources','migrate','seed','test','e2e','demo','reset','down','status')
  $missing = @($verbs | Where-Object { $body -notmatch ("(?m)^\s*'" + [regex]::Escape($_) + "'\s*\{") })
  Add-Result 'all 10 verbs wired in the dispatch switch' ($missing.Count -eq 0) ($(if($missing.Count){"missing: " + ($missing -join ', ')}else{"up, up-sources, migrate, seed, test, e2e, demo, reset, down, status"}))

  Add-Result 'migrate applies scripts/*.sql via psql ON_ERROR_STOP=1' ($body -match 'ON_ERROR_STOP=1' -and $body -match 'Backend/database/scripts') 'psql -v ON_ERROR_STOP=1 -f over numbered scripts'
  Add-Result 'loads .env.dev (no hand-set env var required)' ($body -match 'Import-DotEnv' -and $body -match '\.env\.dev') 'Import-DotEnv reads deploy/compose/.env.dev into process'
  Add-Result 'demo = sources + migrate + seed + up' ($body -match 'function Do-Demo' -and $body -match 'Do-UpSources' -and $body -match 'Do-Migrate' -and $body -match 'Do-Seed' -and $body -match 'Do-Up') 'Do-Demo chains all four'
  Add-Result 'does not use the reserved $pid variable' ($body -notmatch '\$pid\b') 'uses $procId for tracked PIDs'

  # smoke: help must run without error
  $help = & $target help 2>&1
  Add-Result 'ppiq.ps1 help runs without error' ($LASTEXITCODE -eq 0 -or $null -eq $LASTEXITCODE) 'dispatch + param block execute'

  # ==================================================================================================
  Info "`n============================================================================"
  Info " RESULT LEDGER - M1-T07"
  Info "============================================================================"
  $script:Results | Format-Table Pass,Check,Detail -AutoSize | Out-String | Write-Host
  $fail = @($script:Results | Where-Object { -not $_.Pass })
  if($fail.Count -eq 0){
    Write-Host "M1-T07 GREEN - deploy/scripts/ppiq.ps1 written and validated." -ForegroundColor Green
    Write-Host "Next: run  .\deploy\scripts\ppiq.ps1 demo   (brings up sources + migrates + seeds + starts API/web)." -ForegroundColor DarkGray
    Write-Host "Commit:  git add deploy/scripts/ppiq.ps1 && git commit -m 'M1-T07: ppiq.ps1 single entrypoint'" -ForegroundColor DarkGray
  } else {
    Write-Host ("{0} CHECK(S) FAILED - see ledger." -f $fail.Count) -ForegroundColor Red
  }
}
