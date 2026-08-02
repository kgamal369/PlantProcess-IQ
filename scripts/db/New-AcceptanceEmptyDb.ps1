# ============================================================================
# New-AcceptanceEmptyDb.ps1        PPIQ Rule 2 acceptance database
#
# Creates ppiq_acceptance_empty using THE SAME migration chain as every other
# environment, and runs NO seed. That is the whole point: if this database is
# built any other way, it proves nothing about the product.
#
# The chain, in the order deploy/scripts/ppiq.ps1 uses it:
#   1. CREATE DATABASE
#   2. dotnet ef database update            (EF model-first migrations)
#   3. every Backend/database/scripts/*.sql in name order
#   4. NO SEED. Backend/database/seed is never touched by this script.
#
# HARD GUARD: the target database name must contain 'acceptance'. This script
# cannot touch ppiq_app or ppiq_presentation under any argument.
# ============================================================================
[CmdletBinding()]
param(
    [string]$TargetDb = "ppiq_acceptance_empty",
    [string]$DbHost   = "127.0.0.1",
    [int]   $Port     = 5432,
    [string]$User     = "ppiq_dev",
    [string]$Password = "ppiq_dev_local_only",
    [switch]$Execute,
    [switch]$Drop
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Continue"

if ($TargetDb -notmatch "acceptance") {
    Write-Host "[REFUSED] the target database name must contain 'acceptance'."
    Write-Host "          This script exists to build a throwaway proof database and"
    Write-Host "          must never be pointed at ppiq_app or ppiq_presentation."
    exit 1
}

$RepoRoot = (Get-Location).Path
$env:PGPASSWORD = $Password
$env:PGCLIENTENCODING = "UTF8"

function Say([string]$T) { Write-Host $T }
function Head([string]$T) { Write-Host ""; Write-Host ("=" * 78); Write-Host $T; Write-Host ("=" * 78) }

Head ("RULE 2 ACCEPTANCE DATABASE - " + $TargetDb)
Say ("Host    : " + $DbHost + ":" + $Port)
Say ("User    : " + $User)
Say ("Mode    : " + $(if ($Execute) { "EXECUTE" } else { "DRY RUN - nothing is created" }))

$scriptsDir = Join-Path $RepoRoot "Backend\database\scripts"
$sqlFiles = Get-ChildItem $scriptsDir -Filter *.sql | Sort-Object Name
Say ("Post-EF : " + $sqlFiles.Count + " SQL files")
Say ("Seed    : NOT APPLIED - this is the point of the database")

if (-not $Execute) {
    Say ""
    Say "Re-run with -Execute to create it."
    exit 0
}

if ($Drop) {
    Head "DROP"
    & psql -h $DbHost -p $Port -U $User -d postgres -v ON_ERROR_STOP=1 -c ("DROP DATABASE IF EXISTS " + $TargetDb) | Out-Null
    Say ("[DROP] " + $TargetDb)
}

Head "1. CREATE DATABASE"
$exists = ("" + (& psql -h $DbHost -p $Port -U $User -d postgres -tAc ("SELECT 1 FROM pg_database WHERE datname='" + $TargetDb + "'"))).Trim()
if ($exists -eq "1") {
    Say ("[SKIP] " + $TargetDb + " already exists. Use -Drop to rebuild it from nothing.")
} else {
    & psql -h $DbHost -p $Port -U $User -d postgres -v ON_ERROR_STOP=1 -c ("CREATE DATABASE " + $TargetDb + " OWNER " + $User) | Out-Null
    if ($LASTEXITCODE -ne 0) { Say "[FAIL] CREATE DATABASE"; exit 1 }
    Say ("[OK] created " + $TargetDb)
}

$dsn = "Host=" + $DbHost + ";Port=" + $Port + ";Database=" + $TargetDb + ";Username=" + $User + ";Password=" + $Password
$env:ConnectionStrings__PlantProcessDb = $dsn
$env:PLANTPROCESS_DB = $dsn

Head "2. EF MIGRATIONS"
$infra = Join-Path $RepoRoot "Backend\PlantProcess.Infrastructure"
$api   = Join-Path $RepoRoot "Backend\PlantProcess.Api"
& dotnet ef database update --project $infra --startup-project $api
if ($LASTEXITCODE -ne 0) { Say "[FAIL] dotnet ef database update"; exit 1 }
Say "[OK] EF migrations applied"

Head ("3. POST-EF SQL - " + $sqlFiles.Count + " files")
foreach ($f in $sqlFiles) {
    & psql -h $DbHost -p $Port -U $User -d $TargetDb -v ON_ERROR_STOP=1 -q -f $f.FullName
    if ($LASTEXITCODE -ne 0) { Say ("[FAIL] " + $f.Name); exit 1 }
}
Say "[OK] post-EF SQL applied"

Head "4. NO SEED"
Say "Backend/database/seed was not read. If this database holds plant rows, they"
Say "came from a migration, and that is a Rule 2 defect worth finding."

Head "5. PROOF"
$proof = Join-Path $RepoRoot "Backend\database\acceptance\rule2_proof.sql"
& psql -h $DbHost -p $Port -U $User -d $TargetDb -v ON_ERROR_STOP=1 -f $proof
Say ""
Say "The first number must be 0. If it is not, run the diagnostic companion at"
Say "the bottom of rule2_proof.sql to see which tables hold rows."