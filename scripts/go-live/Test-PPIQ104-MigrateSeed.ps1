param(
  [string]$RepoRoot = "C:\Workspace\PlantProcess-IQ",
  [switch]$KeepDatabase
)
Set-StrictMode -Version Latest
$ErrorActionPreference = "Stop"
$container = "ppiq-p104-proof-db"
$port = 55432
$password = "PpiqP104_" + [Guid]::NewGuid().ToString("N")
$compose1 = Join-Path $RepoRoot "deploy\compose\docker-compose.demo-sources.yml"
$compose2 = Join-Path $RepoRoot "deploy\compose\docker-compose.demo-sources.ports.yml"
$migrate = Join-Path $RepoRoot "deploy\scripts\migrate-and-seed.sh"
if (-not (Test-Path $migrate)) { throw "Missing $migrate" }

function Run([scriptblock]$Command, [string]$Name) {
  & $Command
  if ($LASTEXITCODE -ne 0) { throw "$Name failed with exit code $LASTEXITCODE" }
}

try {
  & docker rm -f $container 2>$null | Out-Null
  Run { docker run -d --name $container -e POSTGRES_DB=plantprocessiq -e POSTGRES_USER=plantprocess -e "POSTGRES_PASSWORD=$password" -p "127.0.0.1:${port}:5432" postgres:16-alpine } "Start isolated Postgres"
  $ready = $false
  for ($i=0; $i -lt 60; $i++) {
    & docker exec $container pg_isready -U plantprocess -d plantprocessiq *> $null
    if ($LASTEXITCODE -eq 0) { $ready = $true; break }
    Start-Sleep -Seconds 1
  }
  if (-not $ready) { throw "Isolated PostgreSQL did not become ready." }

  if ((Test-Path $compose1) -and (Test-Path $compose2)) {
    Run { docker compose -f $compose1 -f $compose2 up -d } "Start eight demo sources"
  } else { throw "Demo source compose files are missing." }

  $env:PGHOST = "127.0.0.1"; $env:PGPORT = "$port"; $env:PGDATABASE = "plantprocessiq"; $env:PGUSER = "plantprocess"; $env:PGPASSWORD = $password
  $env:PPIQ_DB_HOST = $env:PGHOST; $env:PPIQ_DB_PORT = $env:PGPORT; $env:PPIQ_DB_NAME = $env:PGDATABASE; $env:PPIQ_DB_USER = $env:PGUSER; $env:PPIQ_DB_PASSWORD = $env:PGPASSWORD
  $env:ConnectionStrings__PlantProcess = "Host=127.0.0.1;Port=$port;Database=plantprocessiq;Username=plantprocess;Password=$password"
  $env:PLANTPROCESS_CONNECTION_STRING = $env:ConnectionStrings__PlantProcess

  $bash = Get-Command bash.exe -ErrorAction SilentlyContinue
  if (-not $bash) { $bash = Get-Command bash -ErrorAction SilentlyContinue }
  if ($bash) { Run { & $bash.Source $migrate } "First migrate-and-seed" }
  else {
    $wsl = Get-Command wsl.exe -ErrorAction SilentlyContinue
    if (-not $wsl) { throw "bash.exe or wsl.exe is required." }
    $wslPath = $migrate.Replace('C:','/mnt/c').Replace('\','/')
    Run { & $wsl.Source bash -lc "'$wslPath'" } "First migrate-and-seed"
  }

  $count1 = (& docker exec -e PGPASSWORD=$password $container psql -U plantprocess -d plantprocessiq -Atc "SELECT COUNT(*) FROM schema_migrations;").Trim()
  if ([int]$count1 -lt 77) { throw "Expected at least 77 schema_migrations rows; found $count1." }

  if ($bash) { Run { & $bash.Source $migrate } "Second idempotency migrate-and-seed" }
  else { Run { & $wsl.Source bash -lc "'$wslPath'" } "Second idempotency migrate-and-seed" }
  $count2 = (& docker exec -e PGPASSWORD=$password $container psql -U plantprocess -d plantprocessiq -Atc "SELECT COUNT(*) FROM schema_migrations;").Trim()
  if ($count1 -ne $count2) { throw "Idempotency failed: first=$count1 second=$count2" }

  $view = (& docker exec -e PGPASSWORD=$password $container psql -U plantprocess -d plantprocessiq -Atc "SELECT schemaname||'.'||viewname FROM pg_views WHERE schemaname NOT IN ('pg_catalog','information_schema') ORDER BY schemaname,viewname LIMIT 1;").Trim()
  if (-not $view) { throw "No canonical/application view exists after migration." }
  $viewCount = (& docker exec -e PGPASSWORD=$password $container psql -U plantprocess -d plantprocessiq -Atc "SELECT COUNT(*) FROM $view;").Trim()
  Write-Host "[GREEN] PPIQ-104: migrations=$count2, view=$view, rows=$viewCount" -ForegroundColor Green
}
finally {
  if (-not $KeepDatabase) { & docker rm -f $container 2>$null | Out-Null }
}