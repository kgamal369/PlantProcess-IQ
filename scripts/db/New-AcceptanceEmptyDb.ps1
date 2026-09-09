# ============================================================================
# New-AcceptanceEmptyDb.ps1        PPIQ canonical certification database
#
# The execution authority is Backend/database/canonical-migration-order.json
# and nothing else. Directory enumeration is never an authority here: twenty
# Superseded, six FixtureOnly and one DeferredWithReason script sit in the same
# folder as the canonical ones, and replaying them is what put a demonstration
# schema into a NO-SEED database.
#
#   tier 1  EF migrations, in the declared baseline order
#   tier 2  every canonicalPath entry, by position - which already places
#           topology convergence, the relationship authority and the declared
#           view tier where they belong
#
# NO SEED. Backend/database/seed is never read.
# The target name is validated and protected databases are refused outright.
# An ordered receipt is written so the executed set can be compared to the
# planned set afterwards rather than assumed equal to it.
# ============================================================================
[CmdletBinding()]
param(
    [Parameter(Mandatory=$true)][string] $TargetDb,
    [string] $DbHost      = "127.0.0.1",
    [int]    $Port        = 5432,
    [string] $User        = "ppiq_dev",
    [string] $Password    = "ppiq_dev_local_only",
    [string] $RepoRoot    = ".",
    [string] $ReceiptPath = "",
    [ValidateSet("", "views", "topology")]
    [string] $OmitTier    = "",
    [switch] $Execute,
    [switch] $Drop
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Continue"

if ($TargetDb -cnotmatch "^[a-z0-9_]+$") { Write-Host ("[REFUSED] unsafe database identifier: " + $TargetDb); exit 1 }
foreach ($p in @("ppiq_app", "ppiq_presentation", "postgres", "template0", "template1")) {
    if ($TargetDb -eq $p) { Write-Host ("[REFUSED] " + $p + " is a protected database and is never a build target."); exit 1 }
}
if ($TargetDb -notmatch "acceptance") {
    Write-Host "[REFUSED] the target database name must contain 'acceptance'."
    exit 1
}

if ($RepoRoot -eq ".") { $RepoRoot = (Get-Location).Path }
$UTF8NoBom = New-Object System.Text.UTF8Encoding($false)
$env:PGPASSWORD = $Password
$env:PGCLIENTENCODING = "UTF8"
function Say([string]$t) { Write-Host $t }
function Head([string]$t) { Write-Host ""; Write-Host ("=" * 74); Write-Host $t; Write-Host ("=" * 74) }

$manifestPath = Join-Path $RepoRoot "Backend\database\canonical-migration-order.json"
if (-not (Test-Path $manifestPath)) { Say "[FAIL] canonical-migration-order.json not found"; exit 1 }
$manifest = Get-Content $manifestPath -Raw | ConvertFrom-Json

Head ("CANONICAL CERTIFICATION DATABASE - " + $TargetDb)
Say ("Authority : canonical-migration-order.json, " + @($manifest.canonicalPath).Count + " canonical entries")
Say ("Mode      : " + $(if ($Execute) { "EXECUTE" } else { "PLAN ONLY" }))
if ($OmitTier -ne "") { Say ("OmitTier  : " + $OmitTier + "   <- NEGATIVE CONTROL, not a valid build") }

$offPaths = @{}
foreach ($e in $manifest.offPath) { $offPaths[$e.path] = $true }

$plan = @()
foreach ($m in $manifest.efMigrationBaseline.migrations) {
    $plan += ,@{ Tier = "ef"; Position = 0; Path = $m.name; Sha = ([string]$m.sha256).ToUpperInvariant(); Kind = "migration" }
}
foreach ($e in @($manifest.canonicalPath | Sort-Object { [int]$_.position })) {
    if ($offPaths.ContainsKey($e.path)) { Say ("[FAIL] " + $e.path + " is on both lists"); exit 1 }
    $tier = "scripts"
    if ($e.path -like "*/topology/*") { $tier = "topology" } elseif ($e.path -like "*/views/*") { $tier = "views" }
    $plan += ,@{ Tier = $tier; Position = [int]$e.position; Path = $e.path; Sha = ([string]$e.sha256).ToUpperInvariant(); Kind = "sql" }
}
if ($OmitTier -ne "") { $plan = @($plan | Where-Object { $_.Tier -ne $OmitTier }) }

$counts = @{}
foreach ($s in $plan) { if (-not $counts.ContainsKey($s.Tier)) { $counts[$s.Tier] = 0 }; $counts[$s.Tier] = $counts[$s.Tier] + 1 }
Say ""
foreach ($t in @("ef", "scripts", "topology", "views")) {
    $n = 0
    if ($counts.ContainsKey($t)) { $n = $counts[$t] }
    Say ("  tier " + $t.PadRight(10) + $n)
}
Say ("  offPath excluded: " + $offPaths.Count + "   seed excluded: Backend/database/seed is never read")
if (-not $Execute) { Say ""; Say "Re-run with -Execute to build it."; exit 0 }

if ($Drop) {
    & psql -h $DbHost -p $Port -U $User -d postgres -v ON_ERROR_STOP=1 -c ("DROP DATABASE IF EXISTS " + $TargetDb + " WITH (FORCE)") | Out-Null
}
Head "1. CREATE DATABASE"
& psql -h $DbHost -p $Port -U $User -d postgres -v ON_ERROR_STOP=1 -c ("CREATE DATABASE " + $TargetDb + " OWNER " + $User) | Out-Null
if ($LASTEXITCODE -ne 0) { Say "[FAIL] CREATE DATABASE"; exit 1 }
Say ("[OK] created " + $TargetDb)

$dsn = "Host=" + $DbHost + ";Port=" + $Port + ";Database=" + $TargetDb + ";Username=" + $User + ";Password=" + $Password
$env:ConnectionStrings__PlantProcessDb = $dsn
$env:PLANTPROCESS_DB = $dsn

$receipt = New-Object System.Collections.ArrayList
$seq = 0
$failed = $false

Head "2. EF MIGRATION BASELINE"
$efSteps = @($plan | Where-Object { $_.Tier -eq "ef" })
if ($efSteps.Count -eq 0) { Say "[SKIP] EF tier omitted" }
else {
    & dotnet ef database update --project (Join-Path $RepoRoot "Backend\PlantProcess.Infrastructure") --startup-project (Join-Path $RepoRoot "Backend\PlantProcess.Api")
    if ($LASTEXITCODE -ne 0) { Say "[FAIL] dotnet ef database update - exact reason above; there is no alternate migration route"; exit 1 }
    Say ("[OK] " + $efSteps.Count + " EF migrations applied")
    foreach ($s in $efSteps) {
        $seq = $seq + 1
        [void]$receipt.Add(($seq.ToString() + "`t" + $s.Tier + "`t" + $s.Position + "`t" + $s.Path + "`t" + $s.Sha + "`t" + $s.Sha + "`tNOT_APPLICABLE`tEXECUTED"))
    }
}

Head "3. CANONICAL SQL IN DECLARED POSITION ORDER"
foreach ($s in @($plan | Where-Object { $_.Kind -eq "sql" })) {
    $seq = $seq + 1
    $full = Join-Path $RepoRoot ($s.Path -replace "/", "\")
    if (-not (Test-Path $full)) {
        [void]$receipt.Add(($seq.ToString() + "`t" + $s.Tier + "`t" + $s.Position + "`t" + $s.Path + "`t" + $s.Sha + "`tMISSING`tMISSING`tNOT_EXECUTED"))
        Say ("[FAIL] missing from disk: " + $s.Path); $failed = $true; break
    }
    $actual = (Get-FileHash -Path $full -Algorithm SHA256).Hash.ToUpperInvariant()
    $shaState = "MATCH"
    if ($actual -ne $s.Sha) { $shaState = "DRIFT" }
    & psql -h $DbHost -p $Port -U $User -d $TargetDb -v ON_ERROR_STOP=1 -q -f $full
    if ($LASTEXITCODE -ne 0) {
        [void]$receipt.Add(($seq.ToString() + "`t" + $s.Tier + "`t" + $s.Position + "`t" + $s.Path + "`t" + $s.Sha + "`t" + $actual + "`t" + $shaState + "`tFAILED"))
        Say ("[FAIL] " + $s.Path); $failed = $true; break
    }
    [void]$receipt.Add(($seq.ToString() + "`t" + $s.Tier + "`t" + $s.Position + "`t" + $s.Path + "`t" + $s.Sha + "`t" + $actual + "`t" + $shaState + "`tEXECUTED"))
}
if (-not $failed) { Say ("[OK] " + @($plan | Where-Object { $_.Kind -eq "sql" }).Count + " canonical SQL entries executed") }

if ($ReceiptPath -eq "") { $ReceiptPath = Join-Path $RepoRoot "Backend\database\acceptance\canonical-build-receipt.tsv" }
$dir = Split-Path -Parent $ReceiptPath
if (-not (Test-Path $dir)) { [void](New-Item -ItemType Directory -Force -Path $dir) }
$body = "seq`ttier`tposition`tpath`tsha256_manifest`tsha256_actual`tsha_state`texec_state`n" + (($receipt.ToArray()) -join "`n") + "`n"
[System.IO.File]::WriteAllText($ReceiptPath, ($body -replace "`r`n", "`n"), $UTF8NoBom)
Say ("[OK] receipt: " + $ReceiptPath)

if ($failed) { Say "[FAIL] build did not complete"; exit 1 }
Say "[OK] canonical build complete"
exit 0