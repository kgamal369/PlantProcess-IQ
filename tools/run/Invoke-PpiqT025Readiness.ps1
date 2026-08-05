#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-025 readiness diagnosis - why seven of eight outcomes abstained and why
    the eighth excluded every parameter. READ ONLY. No engine call, no write.

.DESCRIPTION
    THE QUESTION THIS ANSWERS, AND IT IS NOT A SMALL ONE. The 05-Aug 10:41 run
    recorded eight honest abstains and zero findings. An abstain is only honest if
    the data really is insufficient. If the outcome rows were simply never
    materialised for those keys, the same message is a MISSING PIPELINE wearing
    the clothes of product integrity, and shipping it to a customer meeting as
    "the readiness gate working" would be the exact defect T-025 exists to prevent.

    MEASURED AGAINST THE REAL CODE, not against an assumption:

      NpgsqlFeatureVectorLoader.cs
        outcomes : WHERE lower(outcome_key)=lower(@k) AND grain=@g
        features : WHERE grain=@g AND missingness_flag=false
        alignment: outcome.effective_sample_key <-> feature.effective_sample_key
        IndependentHeats = COUNT(DISTINCT heat_id) over the outcome rows

      ReadinessGate.cs   (ReadinessThresholds demo defaults)
        Independent heats          Ready >= 60   Partial >= 30   else Blocked
        Outcome events             Ready >= 40   Partial >= 15   else Blocked
        Minority-class balance     Ready >= 0.10 Partial >= 0.03 else Blocked
        Required-field completeness Ready >= 0.95 Partial >= 0.85 else Blocked
        Overall is the WORST dimension. Blocked on any one dimension refuses.

    This script recomputes the two counting dimensions per outcome definition
    straight from the database, then measures the sample-key overlap that decides
    whether any parameter survives alignment. Freshness and completeness are
    reported as inputs rather than verdicts, because the loader derives them from
    the aligned matrix and this script does not rebuild the matrix.

    THE THREE VERDICTS IT CAN RETURN, per outcome:
      NOT MATERIALISED - zero outcome rows at that key and grain. The abstain is
                         a pipeline gap, not an analytical judgement.
      GENUINELY THIN   - rows exist but fall under the gate thresholds. The
                         abstain is real and T-025's refusal requirement is met.
      SHOULD HAVE RUN  - rows clear both counting thresholds. The block came from
                         a dimension this script does not recompute, or from
                         alignment, and section D is where to look.

.EXAMPLE
    .\tools\run\Invoke-PpiqT025Readiness.ps1
#>

[CmdletBinding()]
param(
    [string]$PgHost     = "127.0.0.1",
    [int]   $PgPort     = 5432,
    [string]$PgUser     = "ppiq_dev",
    [string]$PgPassword = "ppiq_dev_local_only",
    [string]$Database   = "ppiq_presentation",
    [string]$PsqlPath   = ""
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
function Invoke-Sql {
    param([string]$Sql, [string]$Tag, [switch]$Raw)
    $f = Join-Path $script:tmp ($Tag + ".sql")
    $o = Join-Path $script:tmp ($Tag + ".out")
    $e = Join-Path $script:tmp ($Tag + ".err")
    [System.IO.File]::WriteAllText($f, $Sql, (New-Object System.Text.UTF8Encoding($false)))
    $a = @("-X", "-w", "-h", $PgHost, "-p", "$PgPort", "-U", $PgUser, "-d", $Database,
           "-v", "ON_ERROR_STOP=1")
    if ($Raw) { $a += @("-A", "-t") }
    $a += @("-f", $f, "-o", $o)
    $p = Start-Process -FilePath $script:psql -ArgumentList $a -NoNewWindow -Wait `
                       -PassThru -RedirectStandardError $e
    $r = New-Object psobject
    Add-Member -InputObject $r -MemberType NoteProperty -Name ExitCode -Value $p.ExitCode
    Add-Member -InputObject $r -MemberType NoteProperty -Name Output   -Value (Read-IfExists $o)
    Add-Member -InputObject $r -MemberType NoteProperty -Name Error    -Value (Read-IfExists $e)
    return $r
}
function Get-SqlLines {
    param([string]$Sql, [string]$Tag)
    $r = Invoke-Sql -Sql $Sql -Tag $Tag -Raw
    if ($r.ExitCode -ne 0) {
        Say ("[FAIL] psql " + $Tag + " exit " + $r.ExitCode + " : " + $r.Error.Trim())
        return ,@()
    }
    $out = New-Object System.Collections.ArrayList
    foreach ($raw in ($r.Output -split "`n")) {
        $line = $raw.Trim()
        if ($line.Length -gt 0) { [void]$out.Add($line) }
    }
    return ,$out.ToArray()
}

Rule "PPIQ T-025 READINESS DIAGNOSIS - READ ONLY"
$repoRoot = (Get-Location).Path
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
Say ("Database : " + $Database)
Say "Nothing is written. No engine is called."

$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t025rd_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null

try {
    # -------------------------------------------------------------- A
    Rule "A - IS THE ANALYSIS LAYER STALE"
    Say "T-024 replaced the canonical operational population on 04-Aug. Any analysis"
    Say "row computed before that describes material units that no longer exist."
    $stale = Invoke-Sql -Tag "stale" -Sql @"
SELECT c.status,
       count(DISTINCT c.id) AS runs,
       count(r.id)          AS results,
       min(c.completed_at_utc)::date AS first_day,
       max(c.completed_at_utc)::date AS last_day
FROM public.ml_correlation_compute_runs c
LEFT JOIN public.ml_correlation_results_v2 r ON r.compute_run_id = c.id
GROUP BY c.status
ORDER BY c.status;
"@
    Say $stale.Output
    Say "Any row above whose last_day precedes 2026-08-04 was computed against the"
    Say "pre-T-024 population. It is stale opinion, not a current result."

    # -------------------------------------------------------------- B
    Rule "B - OUTCOME MATERIALISATION PER DECLARED DEFINITION"
    Say "The loader filters on (outcome_key, grain). A definition with no rows at its"
    Say "own grain cannot be analysed, and its abstain is a pipeline gap."
    $matB = Invoke-Sql -Tag "outcomes" -Sql @"
SELECT d.outcome_key,
       d.grain,
       d.outcome_type,
       count(v.id)                            AS outcome_rows,
       count(DISTINCT v.heat_id)              AS independent_heats,
       count(DISTINCT v.effective_sample_key) AS sample_keys
FROM public.ml_outcome_definitions d
LEFT JOIN public.ml_outcome_values v
       ON lower(v.outcome_key) = lower(d.outcome_key)
      AND v.grain = d.grain
WHERE d.is_deleted = false AND d.status = 'Active'
GROUP BY d.outcome_key, d.grain, d.outcome_type
ORDER BY d.outcome_key;
"@
    Say $matB.Output

    Say ""
    Say "GATE VERDICT PER OUTCOME, recomputed against ReadinessGate thresholds:"
    Say ("  " + "outcome_key".PadRight(28) + "grain".PadRight(10) + "rows".PadRight(9) +
         "heats".PadRight(8) + "verdict")
    $rows = Get-SqlLines -Tag "outcomesraw" -Sql @"
SELECT d.outcome_key || '~' || d.grain || '~' || count(v.id)::text || '~' ||
       count(DISTINCT v.heat_id)::text
FROM public.ml_outcome_definitions d
LEFT JOIN public.ml_outcome_values v
       ON lower(v.outcome_key) = lower(d.outcome_key)
      AND v.grain = d.grain
WHERE d.is_deleted = false AND d.status = 'Active'
GROUP BY d.outcome_key, d.grain
ORDER BY d.outcome_key;
"@
    $notMaterialised = 0
    $genuinelyThin   = 0
    $shouldHaveRun   = 0
    foreach ($line in $rows) {
        $p = $line -split "~"
        if ($p.Count -ne 4) { continue }
        $key = $p[0]; $grain = $p[1]; $n = [int]$p[2]; $h = [int]$p[3]
        if ($n -eq 0) {
            $verdict = "NOT MATERIALISED - the abstain is a pipeline gap"
            $notMaterialised = $notMaterialised + 1
        } elseif ($n -lt 15 -or $h -lt 30) {
            $verdict = "GENUINELY THIN - the abstain is real"
            $genuinelyThin = $genuinelyThin + 1
        } else {
            $verdict = "SHOULD HAVE RUN - look at alignment in section D"
            $shouldHaveRun = $shouldHaveRun + 1
        }
        Say ("  " + $key.PadRight(28) + $grain.PadRight(10) + ([string]$n).PadRight(9) +
             ([string]$h).PadRight(8) + $verdict)
    }
    Say ""
    Say ("  NOT MATERIALISED : " + $notMaterialised)
    Say ("  GENUINELY THIN   : " + $genuinelyThin)
    Say ("  SHOULD HAVE RUN  : " + $shouldHaveRun)

    # -------------------------------------------------------------- C
    Rule "C - FEATURE MATERIALISATION PER GRAIN"
    Say "The loader takes features by grain alone, filtered on missingness_flag=false."
    $matC = Invoke-Sql -Tag "features" -Sql @"
SELECT grain,
       missingness_flag,
       count(*)                             AS feature_rows,
       count(DISTINCT feature_key)          AS feature_keys,
       count(DISTINCT effective_sample_key) AS sample_keys
FROM public.ml_feature_values
GROUP BY grain, missingness_flag
ORDER BY grain, missingness_flag;
"@
    Say $matC.Output
    Say "A grain present in the outcome table but absent here has no parameters to"
    Say "correlate against, whatever the outcome volume."

    # -------------------------------------------------------------- D
    Rule "D - SAMPLE-KEY ALIGNMENT, THE THING THAT EXCLUDED 26 OF 26"
    Say "defect.rate_per_m2 cleared the gate and still returned 0 findings with 26"
    Say "parameters excluded. Alignment is on effective_sample_key. If the outcome"
    Say "keys and the feature keys do not intersect, every parameter is excluded no"
    Say "matter how many rows each side holds."
    $align = Invoke-Sql -Tag "align" -Sql @"
WITH o AS (
    SELECT DISTINCT effective_sample_key AS k
    FROM public.ml_outcome_values
    WHERE lower(outcome_key) = 'defect.rate_per_m2' AND grain = 'coil'
), f AS (
    SELECT DISTINCT effective_sample_key AS k
    FROM public.ml_feature_values
    WHERE grain = 'coil' AND missingness_flag = false
)
SELECT 'outcome sample keys'      AS side, count(*)::int AS keys FROM o
UNION ALL SELECT 'feature sample keys', count(*)::int FROM f
UNION ALL SELECT 'keys present in BOTH', count(*)::int FROM (SELECT k FROM o INTERSECT SELECT k FROM f) b
UNION ALL SELECT 'outcome keys with no feature', count(*)::int FROM (SELECT k FROM o EXCEPT SELECT k FROM f) x
UNION ALL SELECT 'feature keys with no outcome', count(*)::int FROM (SELECT k FROM f EXCEPT SELECT k FROM o) y;
"@
    Say $align.Output

    Say ""
    Say "Sample-key SHAPE on each side - a format mismatch shows here immediately:"
    $shape = Invoke-Sql -Tag "shape" -Sql @"
SELECT 'outcome' AS side, effective_sample_key AS example
FROM public.ml_outcome_values
WHERE lower(outcome_key) = 'defect.rate_per_m2' AND grain = 'coil'
LIMIT 3;
"@
    Say $shape.Output
    $shape2 = Invoke-Sql -Tag "shape2" -Sql @"
SELECT 'feature' AS side, effective_sample_key AS example
FROM public.ml_feature_values
WHERE grain = 'coil' AND missingness_flag = false
LIMIT 3;
"@
    Say $shape2.Output

    Say ""
    Say "PER-PARAMETER OVERLAP - how many usable pairs each feature actually offers."
    Say "MethodSelector needs at least 4 aligned non-null pairs or the parameter is"
    Say "skipped, and a zero-variance parameter is dropped after that."
    $perFeature = Invoke-Sql -Tag "perfeature" -Sql @"
WITH o AS (
    SELECT effective_sample_key AS k, numeric_value AS y
    FROM public.ml_outcome_values
    WHERE lower(outcome_key) = 'defect.rate_per_m2' AND grain = 'coil'
)
SELECT f.feature_key,
       count(*)                                        AS aligned_pairs,
       count(f.numeric_value)                          AS numeric_pairs,
       count(DISTINCT f.numeric_value)                 AS distinct_values
FROM public.ml_feature_values f
JOIN o ON o.k = f.effective_sample_key
WHERE f.grain = 'coil' AND f.missingness_flag = false
GROUP BY f.feature_key
ORDER BY aligned_pairs DESC, f.feature_key
LIMIT 30;
"@
    Say $perFeature.Output
    Say "distinct_values of 1 is a zero-variance parameter and is dropped by design."
    Say "aligned_pairs under 4 is dropped before any method is chosen."

    # -------------------------------------------------------------- E
    Rule "E - WHERE THE OUTCOME VALUES CAME FROM"
    Say "If the feature-store refresh only materialises one outcome family, the other"
    Say "seven definitions are DECLARED BUT NEVER PRODUCED - a T-025 finding about"
    Say "the refresh function, not about the plant."
    $src = Invoke-Sql -Tag "src" -Sql @"
SELECT outcome_key, grain, count(*) AS rows, min(observed_at_utc)::date AS first_day,
       max(observed_at_utc)::date AS last_day
FROM public.ml_outcome_values
GROUP BY outcome_key, grain
ORDER BY outcome_key, grain;
"@
    Say $src.Output
}
catch {
    Say ("[ERROR] " + $_.Exception.Message)
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

Rule "WHAT TO DO WITH THIS"
Say "NOT MATERIALISED outcomes  -> the refresh function is the defect. The abstain"
Say "                              must NOT be presented as an analytical refusal."
Say "GENUINELY THIN outcomes    -> the abstain is real and satisfies T-025's refusal"
Say "                              requirement. Record and keep."
Say "SHOULD HAVE RUN outcomes   -> section D decides it: no key overlap is an"
Say "                              identity defect between the two value tables."
Say ""
Say "No fix is proposed here. This script measures and stops."

$outFolder = Join-Path $repoRoot "docs\m1\evidence"
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$ev = Join-Path $outFolder ("T-025_readiness_diagnosis_" + $stamp + ".txt")
$clean = New-Object System.Text.StringBuilder
foreach ($ch in ($script:log -replace "`r`n", "`n").ToCharArray()) {
    if ([int]$ch -le 126 -or [int]$ch -eq 10) { [void]$clean.Append($ch) }
}
[System.IO.File]::WriteAllText($ev, ($clean.ToString() -replace "`n", "`r`n"),
    (New-Object System.Text.UTF8Encoding($false)))
Write-Host ""
Write-Host ("Evidence : " + $ev)
exit 0
