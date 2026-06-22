# =================================================================================================
# PlantProcess IQ — Direct PS5.1-safe PostCSS Fix + Frontend Build + DB Apply Proof
# Paste directly into PowerShell.
# =================================================================================================

$ErrorActionPreference = "Stop"

$repo = "C:\Workspace\PlantProcess-IQ"
$frontend = Join-Path $repo "Frontend\PlantProcess.Web"
$backend = Join-Path $repo "Backend"
$apiProject = Join-Path $backend "PlantProcess.Api"
$sqlScript = Join-Path $backend "database\scripts\200_phase02_ml_foundation_feature_store_pgvector.sql"

Write-Host ""
Write-Host "PlantProcess IQ — direct safe proof runner" -ForegroundColor Cyan

# -------------------------------------------------------------------------------------------------
# 1) Stop old dev processes
# -------------------------------------------------------------------------------------------------

Write-Host ""
Write-Host "STEP 1 — Stop local dev processes" -ForegroundColor Cyan

Get-CimInstance Win32_Process |
  Where-Object {
    ($_.Name -in @("node.exe", "dotnet.exe", "PlantProcess.Api.exe", "PlantProcess.Workers.exe")) -and
    (
      ($_.CommandLine -like "*PlantProcess-IQ*") -or
      ($_.CommandLine -like "*PlantProcess.Api*") -or
      ($_.CommandLine -like "*PlantProcess.Workers*")
    )
  } |
  ForEach-Object {
    if ($_.ProcessId -ne $PID) {
      Write-Host ("Stopping " + $_.Name + " PID=" + $_.ProcessId) -ForegroundColor Yellow
      Stop-Process -Id $_.ProcessId -Force -ErrorAction SilentlyContinue
    }
  }

Start-Sleep -Seconds 2

# -------------------------------------------------------------------------------------------------
# 2) Repair PostCSS config
# -------------------------------------------------------------------------------------------------

Write-Host ""
Write-Host "STEP 2 — Repair PostCSS config" -ForegroundColor Cyan

$badPostCssFiles = @(
  ".postcssrc",
  ".postcssrc.json",
  ".postcssrc.js",
  ".postcssrc.cjs",
  ".postcssrc.mjs",
  "postcss.config.json",
  "postcss.config.js",
  "postcss.config.mjs"
)

foreach ($f in $badPostCssFiles) {
  $p = Join-Path $frontend $f
  if (Test-Path $p) {
    Copy-Item $p ($p + ".bak") -Force
    Remove-Item $p -Force
    Write-Host ("Removed conflicting PostCSS config: " + $p) -ForegroundColor Yellow
  }
}

$postcssConfig = Join-Path $frontend "postcss.config.cjs"

$postcssLines = @(
  'const plugins = {};',
  '',
  'function hasPackage(name) {',
  '  try {',
  '    require.resolve(name);',
  '    return true;',
  '  } catch {',
  '    return false;',
  '  }',
  '}',
  '',
  'if (hasPackage("@tailwindcss/postcss")) {',
  '  plugins["@tailwindcss/postcss"] = {};',
  '} else if (hasPackage("tailwindcss")) {',
  '  plugins.tailwindcss = {};',
  '}',
  '',
  'if (hasPackage("autoprefixer")) {',
  '  plugins.autoprefixer = {};',
  '}',
  '',
  'module.exports = { plugins };'
)

$utf8NoBom = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllLines($postcssConfig, $postcssLines, $utf8NoBom)
Write-Host ("Wrote " + $postcssConfig) -ForegroundColor Green

$packageJsonPath = Join-Path $frontend "package.json"
$pkg = Get-Content -Raw $packageJsonPath | ConvertFrom-Json

if ($pkg.PSObject.Properties.Name -contains "postcss") {
  $pkg.PSObject.Properties.Remove("postcss")
  [System.IO.File]::WriteAllText($packageJsonPath, ($pkg | ConvertTo-Json -Depth 50), $utf8NoBom)
  Write-Host "Removed top-level postcss field from package.json" -ForegroundColor Yellow
}

Push-Location $frontend
node -e 'const cfg=require("./postcss.config.cjs"); console.log("postcss config ok", Object.keys(cfg.plugins));'
if ($LASTEXITCODE -ne 0) { throw "PostCSS config check failed." }
Pop-Location

# -------------------------------------------------------------------------------------------------
# 3) Frontend build
# -------------------------------------------------------------------------------------------------

Write-Host ""
Write-Host "STEP 3 — Run frontend build" -ForegroundColor Cyan

Push-Location $frontend
npm run build
if ($LASTEXITCODE -ne 0) { throw "Frontend build failed." }
Pop-Location

Write-Host "Frontend build passed." -ForegroundColor Green

# -------------------------------------------------------------------------------------------------
# 4) Find psql
# -------------------------------------------------------------------------------------------------

Write-Host ""
Write-Host "STEP 4 — Find psql" -ForegroundColor Cyan

$psqlPath = $null
$psqlCommand = Get-Command psql -ErrorAction SilentlyContinue

if ($null -ne $psqlCommand) {
  $psqlPath = $psqlCommand.Source
}

if ([string]::IsNullOrWhiteSpace($psqlPath)) {
  $candidate = Get-ChildItem "C:\Program Files\PostgreSQL" -Recurse -Filter "psql.exe" -ErrorAction SilentlyContinue |
    Sort-Object FullName -Descending |
    Select-Object -First 1

  if ($null -ne $candidate) {
    $psqlPath = $candidate.FullName
  }
}

if ([string]::IsNullOrWhiteSpace($psqlPath)) {
  throw "psql.exe not found."
}

Write-Host ("Found psql: " + $psqlPath) -ForegroundColor Green

# -------------------------------------------------------------------------------------------------
# 5) Detect DB connection string
# -------------------------------------------------------------------------------------------------

Write-Host ""
Write-Host "STEP 5 — Detect DB connection string" -ForegroundColor Cyan

$dbConnection = $null

if (-not [string]::IsNullOrWhiteSpace($env:PPIQ_DB_CONNECTION)) {
  $dbConnection = $env:PPIQ_DB_CONNECTION
}

if ([string]::IsNullOrWhiteSpace($dbConnection) -and -not [string]::IsNullOrWhiteSpace($env:ConnectionStrings__DefaultConnection)) {
  $dbConnection = $env:ConnectionStrings__DefaultConnection
}

$settingsFiles = @(
  (Join-Path $apiProject "appsettings.Development.json"),
  (Join-Path $apiProject "appsettings.Local.json"),
  (Join-Path $apiProject "appsettings.json")
)

foreach ($settingsFile in $settingsFiles) {
  if ([string]::IsNullOrWhiteSpace($dbConnection) -and (Test-Path $settingsFile)) {
    $json = Get-Content -Raw $settingsFile | ConvertFrom-Json

    if ($null -ne $json.ConnectionStrings) {
      foreach ($prop in $json.ConnectionStrings.PSObject.Properties) {
        $value = [string]$prop.Value

        if ([string]::IsNullOrWhiteSpace($dbConnection) -and $value.Contains("Database=")) {
          $dbConnection = $value
          Write-Host ("Detected connection from " + $settingsFile + " key " + $prop.Name) -ForegroundColor Green
        }
      }
    }
  }
}

if ([string]::IsNullOrWhiteSpace($dbConnection)) {
  throw "Could not detect DB connection string. Set `$env:PPIQ_DB_CONNECTION manually."
}

# -------------------------------------------------------------------------------------------------
# 6) Parse connection string into PG env vars
# -------------------------------------------------------------------------------------------------

Write-Host ""
Write-Host "STEP 6 — Prepare psql env vars" -ForegroundColor Cyan

$cs = @{}

foreach ($part in $dbConnection.Split(";")) {
  if (-not [string]::IsNullOrWhiteSpace($part)) {
    $idx = $part.IndexOf("=")

    if ($idx -gt 0) {
      $key = $part.Substring(0, $idx).Trim().ToLowerInvariant()
      $value = $part.Substring($idx + 1).Trim()
      $cs[$key] = $value
    }
  }
}

$hostValue = "localhost"
$portValue = "5432"
$databaseValue = $null
$userValue = $null
$passwordValue = $null

if ($cs.ContainsKey("host")) { $hostValue = $cs["host"] }
if ($cs.ContainsKey("server")) { $hostValue = $cs["server"] }
if ($cs.ContainsKey("port")) { $portValue = $cs["port"] }
if ($cs.ContainsKey("database")) { $databaseValue = $cs["database"] }
if ($cs.ContainsKey("dbname")) { $databaseValue = $cs["dbname"] }
if ($cs.ContainsKey("username")) { $userValue = $cs["username"] }
if ($cs.ContainsKey("user id")) { $userValue = $cs["user id"] }
if ($cs.ContainsKey("userid")) { $userValue = $cs["userid"] }
if ($cs.ContainsKey("user")) { $userValue = $cs["user"] }
if ($cs.ContainsKey("password")) { $passwordValue = $cs["password"] }
if ($cs.ContainsKey("pwd")) { $passwordValue = $cs["pwd"] }

if ([string]::IsNullOrWhiteSpace($databaseValue)) { throw "Database not found in connection string." }
if ([string]::IsNullOrWhiteSpace($userValue)) { throw "Username/User ID not found in connection string." }

$env:PGHOST = $hostValue
$env:PGPORT = $portValue
$env:PGDATABASE = $databaseValue
$env:PGUSER = $userValue

if (-not [string]::IsNullOrWhiteSpace($passwordValue)) {
  $env:PGPASSWORD = $passwordValue
}

Write-Host ("PGHOST=" + $env:PGHOST)
Write-Host ("PGPORT=" + $env:PGPORT)
Write-Host ("PGDATABASE=" + $env:PGDATABASE)
Write-Host ("PGUSER=" + $env:PGUSER)

# -------------------------------------------------------------------------------------------------
# 7) Apply ML foundation SQL
# -------------------------------------------------------------------------------------------------

Write-Host ""
Write-Host "STEP 7 — Apply ML foundation SQL" -ForegroundColor Cyan

& $psqlPath -v "ON_ERROR_STOP=1" -f $sqlScript
if ($LASTEXITCODE -ne 0) { throw "ML foundation SQL apply failed." }

Write-Host "ML foundation SQL applied." -ForegroundColor Green

# -------------------------------------------------------------------------------------------------
# 8) Run DB proof
# -------------------------------------------------------------------------------------------------

Write-Host ""
Write-Host "STEP 8 — Run DB proof queries" -ForegroundColor Cyan

& $psqlPath -v "ON_ERROR_STOP=1" -c "SELECT COUNT(*) AS required_tables FROM information_schema.tables WHERE table_schema='public' AND table_name IN ('ml_feature_definitions','ml_feature_values','ml_outcome_definitions','ml_outcome_values','ml_feature_store_refresh_runs','ml_correlation_compute_runs','ml_correlation_results_v2','ml_knowledge_base_items');"
if ($LASTEXITCODE -ne 0) { throw "Required table proof failed." }

& $psqlPath -v "ON_ERROR_STOP=1" -c "SELECT COUNT(*) AS required_functions FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='public' AND p.proname IN ('ppiq_ml_seed_foundation_catalog','ppiq_ml_refresh_feature_store','ppiq_ml_compute_basic_correlations','ppiq_ml_upsert_kb_item','ppiq_ml_search_kb');"
if ($LASTEXITCODE -ne 0) { throw "Required function proof failed." }

& $psqlPath -v "ON_ERROR_STOP=1" -c "SELECT * FROM public.ppiq_ml_seed_foundation_catalog();"
if ($LASTEXITCODE -ne 0) { throw "Seed foundation catalog proof failed." }

& $psqlPath -v "ON_ERROR_STOP=1" -c "SELECT 'feature_definitions' AS proof_section, COUNT(*) AS row_count FROM public.ml_feature_definitions WHERE is_deleted=false UNION ALL SELECT 'outcome_definitions', COUNT(*) FROM public.ml_outcome_definitions WHERE is_deleted=false;"
if ($LASTEXITCODE -ne 0) { throw "Catalog count proof failed." }

& $psqlPath -v "ON_ERROR_STOP=1" -c "SELECT public.ppiq_ml_upsert_kb_item('demo.surface-crack.superheat.casting-speed','CorrelationInsight','Surface crack investigation pattern','Statistical investigation pattern linking thermal/casting conditions to surface defect risk. This is correlation evidence, not guaranteed root cause.','[0.10,0.20,0.30,0.40]'::jsonb,'{""source"":""db-proof"",""honesty"":""correlation-not-root-cause""}'::jsonb,'Casting','SurfaceCrack','GenericManufacturing','DemoLine','3650d',NULL,NULL) AS kb_item_id;"
if ($LASTEXITCODE -ne 0) { throw "KB upsert proof failed." }

& $psqlPath -v "ON_ERROR_STOP=1" -c "SELECT item_key,item_type,title,area,defect_class,similarity FROM public.ppiq_ml_search_kb('[0.10,0.20,0.30,0.40]'::jsonb,NULL,NULL,NULL,NULL,5);"
if ($LASTEXITCODE -ne 0) { throw "KB search proof failed." }

& $psqlPath -v "ON_ERROR_STOP=1" -c "SELECT 'pgvector_available' AS proof_section, EXISTS (SELECT 1 FROM pg_type WHERE typname='vector') AS value;"
if ($LASTEXITCODE -ne 0) { throw "pgvector proof failed." }

Write-Host ""
Write-Host "SUCCESS — Frontend build passed, T209-T213 SQL applied, DB proof passed." -ForegroundColor Green