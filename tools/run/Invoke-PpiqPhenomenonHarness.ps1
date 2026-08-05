#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-026 phenomenon test harness - runner. Manifest in, verdicts out.

.DESCRIPTION
    THE POINT OF THE HARNESS. The fleet will change. Re-hand-checking every
    phenomenon after each change is not affordable, so the expectations are
    written down once and re-measured on demand. This runner walks the manifest,
    executes each population_query through psql, and hands the result sets to
    Backend\tools\phenomenon_harness.py, which computes the verdicts.

    WHY THE WORK IS SPLIT. psql is proven present on this machine; a Python
    database driver is not. So SQL execution stays here, where the credentials
    already live, and the statistics live in Python, where they can be tested.
    The verdict engine ships with a self-test that demonstrates PASS, FAIL,
    INSUFFICIENT, a correlating negative control, and an undefined statistic.

    MANIFEST COLUMNS - FROZEN BY THE BACKLOG:
      phenomenon_id, population_query, expected_direction, minimum_population,
      expected_effect_band, conditioning_variable, expected_after_conditioning,
      negative_control
    A ninth column is rejected rather than absorbed. Widening the contract needs
    a ruling, not a quiet edit.

    RESULT-SHAPE CONTRACT. Every population_query returns a column x and a column
    y, plus a column named exactly the conditioning_variable when one is set.
    Rows with a null x or y are dropped before the population is counted, so a
    query returning ten thousand nulls reports INSUFFICIENT rather than passing
    on volume.

    -Describe PRINTS WHAT A QUERY MAY LEGALLY REFERENCE. Author manifest rows
    against that inventory, never against memory. Canonical column names are not
    guessable from the entity names - MaterialUnitConfiguration maps
    material_units with material_code, material_unit_type, product_family,
    grade_or_recipe and production_start_utc, and carries no campaign column at
    all.

    READ ONLY. The connection sets default_transaction_read_only, so a manifest
    row cannot write, delete or alter anything even if someone puts DML in it.
    It is set on the connection rather than with a BEGIN wrapper because psql
    prints a command tag per statement and that tag would land in the CSV.
.EXAMPLE
    .\tools\run\Invoke-PpiqPhenomenonHarness.ps1 -Describe
    .\tools\run\Invoke-PpiqPhenomenonHarness.ps1
    .\tools\run\Invoke-PpiqPhenomenonHarness.ps1 -SelfTest
#>

[CmdletBinding()]
param(
    [switch]$Describe,
    [switch]$SelfTest,
    [string]$Manifest   = "docs\m1\phenomena\manifest.csv",
    [string]$HarnessPy  = "Backend\tools\phenomenon_harness.py",
    [string]$PgHost     = "127.0.0.1",
    [int]   $PgPort     = 5432,
    [string]$PgUser     = "ppiq_dev",
    [string]$PgPassword = "ppiq_dev_local_only",
    [string]$Database   = "ppiq_presentation",
    [string]$PsqlPath   = "",
    [string]$PythonPath = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

$script:log = ""
function Say  { param([string]$T) Write-Host $T; $script:log += ($T + "`r`n") }
function Rule { param([string]$T) Say ""; Say ("=" * 78); Say $T; Say ("=" * 78) }

function Read-IfExists {
    param([string]$Path)
    if (Test-Path -LiteralPath $Path) { return [System.IO.File]::ReadAllText($Path) }
    return ""
}
function Resolve-Psql {
    param([string]$Explicit)
    if (-not [string]::IsNullOrWhiteSpace($Explicit)) {
        if (Test-Path -LiteralPath $Explicit) { return $Explicit }
        return $null
    }
    $c = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($null -ne $c) { return $c.Source }
    foreach ($p in @("C:\Program Files\PostgreSQL\16\bin\psql.exe",
                     "C:\Program Files\PostgreSQL\17\bin\psql.exe",
                     "C:\Program Files\PostgreSQL\15\bin\psql.exe")) {
        if (Test-Path -LiteralPath $p) { return $p }
    }
    return $null
}
function Resolve-Python {
    param([string]$Explicit)
    if (-not [string]::IsNullOrWhiteSpace($Explicit)) {
        if (Test-Path -LiteralPath $Explicit) { return $Explicit }
        return $null
    }
    foreach ($n in @("python.exe", "python3.exe", "py.exe")) {
        $c = Get-Command $n -ErrorAction SilentlyContinue
        if ($null -ne $c) { return $c.Source }
    }
    return $null
}
function Invoke-Psql {
    param([string[]]$ExtraArgs, [string]$Tag)
    $e = Join-Path $script:tmp ($Tag + ".err")
    $a = @("-X", "-w", "-h", $PgHost, "-p", "$PgPort", "-U", $PgUser, "-d", $Database,
           "-v", "ON_ERROR_STOP=1") + $ExtraArgs
    $p = Start-Process -FilePath $script:psql -ArgumentList $a -NoNewWindow -Wait `
                       -PassThru -RedirectStandardError $e
    $r = New-Object psobject
    Add-Member -InputObject $r -MemberType NoteProperty -Name ExitCode -Value $p.ExitCode
    Add-Member -InputObject $r -MemberType NoteProperty -Name Error    -Value (Read-IfExists $e)
    return $r
}

Rule "PPIQ T-026 PHENOMENON HARNESS"
$repoRoot = (Get-Location).Path
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
$python = Resolve-Python -Explicit $PythonPath
if ($null -eq $python) { Write-Host "[FAIL] python not found on PATH."; exit 2 }

$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
# READ ONLY IS SET ON THE CONNECTION, NOT WITH BEGIN/COMMIT AROUND THE QUERY.
# psql prints a command tag for every statement, so a BEGIN wrapper put the word
# BEGIN on the first line of the CSV and it became the header row. Setting
# default_transaction_read_only through PGOPTIONS gives the same guarantee and
# emits nothing at all, so the result set is the only thing in the file.
$env:PGOPTIONS = "-c default_transaction_read_only=on"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t026_" + $stamp)
$dataDir = Join-Path $script:tmp "data"
New-Item -ItemType Directory -Path $dataDir -Force | Out-Null
Say ("Database : " + $Database)
Say ("psql     : " + $script:psql)
Say ("python   : " + $python)

# Initialised HERE, not inside the try. An abort inside the try used to leave
# this unset, and the exit lines at the bottom then threw on an undefined
# variable - two false errors burying the real one. It starts at 3, meaning
# "the harness did not reach a verdict", so an abort can never read as success.
$code = 3

try {
    # ------------------------------------------------------------- DESCRIBE
    if ($Describe) {
        Rule "DESCRIBE - WHAT A population_query MAY REFERENCE"
        Say "Author manifest rows against this inventory, not against memory."
        $out = Join-Path $script:tmp "describe.txt"
        $sql = Join-Path $script:tmp "describe.sql"
        [System.IO.File]::WriteAllText($sql, @"
\pset border 2
SELECT c.table_name, count(*) AS columns
FROM information_schema.columns c
JOIN information_schema.tables t
  ON t.table_schema = c.table_schema AND t.table_name = c.table_name
WHERE c.table_schema = 'public' AND t.table_type = 'BASE TABLE'
  AND c.table_name NOT LIKE 'ml/_%' ESCAPE '/'
  AND c.table_name NOT LIKE '%_audit%'
GROUP BY c.table_name
HAVING count(*) > 3
ORDER BY c.table_name;

\echo ''
\echo 'COLUMNS OF THE ENTITIES A PHENOMENON IS MOST LIKELY TO NEED'
SELECT table_name, column_name, data_type
FROM information_schema.columns
WHERE table_schema = 'public'
  AND table_name IN ('material_units','quality_events','defect_catalogs',
                     'parameter_observations','parameter_definitions',
                     'downtime_events','equipment','sites','production_lines')
ORDER BY table_name, ordinal_position;

\echo ''
\echo 'ROW COUNTS OF THOSE ENTITIES'
SELECT 'material_units' AS entity, count(*) FROM public.material_units WHERE is_deleted = false
UNION ALL SELECT 'quality_events', count(*) FROM public.quality_events WHERE is_deleted = false
UNION ALL SELECT 'parameter_observations', count(*) FROM public.parameter_observations WHERE is_deleted = false
ORDER BY 1;
"@, (New-Object System.Text.UTF8Encoding($false)))
        $r = Invoke-Psql -Tag "describe" -ExtraArgs @("-f", $sql, "-o", $out)
        if ($r.ExitCode -ne 0) { Say ("[FAIL] " + $r.Error.Trim()) }
        Say (Read-IfExists $out)
        Say ""
        Say "Send this inventory and the three seed phenomena will be authored"
        Say "against it. Nothing here is written from memory."
        exit 0
    }

    # ------------------------------------------------------------- SELF TEST
    if ($SelfTest) {
        Rule "SELF-TEST - THE VERDICT ENGINE, NO DATABASE"
        Say "Proves the runner can produce every verdict before it is trusted with"
        Say "real phenomena: a pass, a fail, a refusal, a correlating negative"
        Say "control, and an undefined statistic reported as undefined."
        & $python $HarnessPy "--selftest"
        exit $LASTEXITCODE
    }

    # ------------------------------------------------------------- RUN
    Rule "1 - MANIFEST"
    if (-not (Test-Path -LiteralPath $Manifest)) {
        Say ("[FAIL] manifest not found: " + $Manifest)
        exit 2
    }
    $rows = Import-Csv -LiteralPath $Manifest
    $frozen = @("phenomenon_id","population_query","expected_direction",
                "minimum_population","expected_effect_band","conditioning_variable",
                "expected_after_conditioning","negative_control")
    $present = @()
    if ($rows.Count -gt 0) {
        $present = $rows[0].PSObject.Properties.Name
    } else {
        $header = (Get-Content -LiteralPath $Manifest -TotalCount 1)
        $present = $header -split ","
    }
    $missing = @()
    foreach ($c in $frozen) { if ($present -notcontains $c) { $missing += $c } }
    $extra = @()
    foreach ($c in $present) { if ($frozen -notcontains $c) { $extra += $c } }
    Say ("  manifest        : " + $Manifest)
    Say ("  rows            : " + $rows.Count)
    Say ("  missing columns : " + $missing.Count + " (required 0)")
    Say ("  extra columns   : " + $extra.Count + " (required 0 - the eight are frozen)")
    if ($missing.Count -gt 0) { Say ("  missing: " + ($missing -join ", ")); exit 2 }
    if ($extra.Count -gt 0)   { Say ("  extra: " + ($extra -join ", ")); exit 2 }
    if ($rows.Count -lt 1) {
        Say ""
        Say "  The manifest has no phenomenon rows yet. T-026 requires at least"
        Say "  three hand-checked phenomena demonstrating a pass, a fail and a"
        Say "  refusal. Run -Describe first and author them against the inventory."
        exit 2
    }

    Rule "2 - EXECUTE EACH population_query"
    Say "The connection is default_transaction_read_only, so a manifest row cannot"
    Say "write, delete or alter anything even if it tries."

    # PROVEN, NOT ASSERTED. Ask the server what the connection actually is. An
    # environment variable the server ignored would otherwise leave every query
    # running writable while the banner claimed otherwise.
    $roOut = Join-Path $script:tmp "readonly.txt"
    $roSql = Join-Path $script:tmp "readonly.sql"
    [System.IO.File]::WriteAllText($roSql, "SHOW transaction_read_only;",
        (New-Object System.Text.UTF8Encoding($false)))
    $ro = Invoke-Psql -Tag "readonly" -ExtraArgs @("-A", "-t", "-f", $roSql, "-o", $roOut)
    $roValue = (Read-IfExists $roOut).Trim()
    Say ("  server reports transaction_read_only = " + $roValue + " (required on)")
    if ($roValue -ne "on") {
        Say "[STOP] the connection is NOT read-only. Refusing to run manifest SQL"
        Say "       that could write. Check that PGOPTIONS reached the server."
        exit 2
    }
    $failedQueries = 0
    foreach ($row in $rows) {
        $phenId = $row.phenomenon_id.Trim()
        if ($phenId -eq "") { continue }
        $qf = Join-Path $script:tmp ($phenId + ".sql")
        $body = $row.population_query + "`r`n"
        [System.IO.File]::WriteAllText($qf, $body, (New-Object System.Text.UTF8Encoding($false)))
        $csv = Join-Path $dataDir ($phenId + ".csv")
        $t0 = Get-Date
        $r = Invoke-Psql -Tag ("q_" + $phenId) -ExtraArgs @("-q", "--csv", "-f", $qf, "-o", $csv)
        $secs = [math]::Round(((Get-Date) - $t0).TotalSeconds, 1)
        if ($r.ExitCode -ne 0) {
            Say ("  " + $phenId.PadRight(34) + " QUERY FAILED  " + $secs + "s")
            Say ("      " + (($r.Error -replace "`r", "" -replace "`n", " ").Trim()))
            $failedQueries = $failedQueries + 1
            if (Test-Path -LiteralPath $csv) { Remove-Item $csv -Force }
        } else {
            $n = 0
            if (Test-Path -LiteralPath $csv) {
                $n = (Get-Content -LiteralPath $csv | Measure-Object -Line).Lines - 1
                if ($n -lt 0) { $n = 0 }
            }
            Say ("  " + $phenId.PadRight(34) + " ok, " + $n + " row(s), " + $secs + "s")
        }
    }
    if ($failedQueries -gt 0) {
        Say ""
        Say ("  " + $failedQueries + " query(ies) failed. Those phenomena will be")
        Say "  reported as ERROR rather than silently skipped."
    }

    Rule "3 - VERDICTS"
    $jsonOut = Join-Path $repoRoot ("docs\m1\evidence\T-026_harness_" + $stamp + ".json")
    $evFolder = Split-Path $jsonOut -Parent
    if (-not (Test-Path -LiteralPath $evFolder)) {
        New-Item -ItemType Directory -Path $evFolder -Force | Out-Null
    }
    & $python $HarnessPy "--manifest" $Manifest "--datadir" $dataDir "--json-out" $jsonOut
    $code = $LASTEXITCODE
    Say ""
    Say ("  harness exit code : " + $code)
    Say ("  machine-readable  : " + $jsonOut)
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:\PGOPTIONS -ErrorAction SilentlyContinue
}

$outFolder = Join-Path $repoRoot "docs\m1\evidence"
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$ev = Join-Path $outFolder ("T-026_harness_" + $stamp + ".txt")
$clean = New-Object System.Text.StringBuilder
foreach ($ch in ($script:log -replace "`r`n", "`n").ToCharArray()) {
    if ([int]$ch -le 126 -or [int]$ch -eq 10) { [void]$clean.Append($ch) }
}
[System.IO.File]::WriteAllText($ev, ($clean.ToString() -replace "`n", "`r`n"),
    (New-Object System.Text.UTF8Encoding($false)))
Write-Host ""
Write-Host ("Evidence : " + $ev)
exit $code
