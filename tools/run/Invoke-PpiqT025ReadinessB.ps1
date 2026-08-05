#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-025 readiness diagnosis part B - the two remaining hypotheses, tested so
    they can be REFUTED. READ ONLY. No engine call, no write.

.DESCRIPTION
    Part A established: 5 of 8 outcome definitions have no rows at all, alignment
    between the two value tables is perfect (4,528 of 4,528), and every parameter
    offers thousands of aligned pairs with real variance. Two questions remain, and
    each has a stated prediction so the result decides rather than confirms.

    HYPOTHESIS 1 - THE EMPTY OUTCOME REPRESENTATION.
      AdvancedCorrelationComputeService.BuildOutcomeRepr, numeric branch:
          foreach (var o in ds.Outcomes) if (!double.IsNaN(o.Value)) map[o.SampleKey] = o.Value;
      NpgsqlFeatureVectorLoader turns a SQL NULL numeric_value into double.NaN.
      So if numeric_value is NULL on the defect.rate_per_m2 rows, repr is EMPTY,
      Align returns n=0 for every feature, and all 26 are excluded as
      "Insufficient paired samples (n=0 < 8)" - which is the exact shape observed:
      0 findings, 26 excluded.
      PREDICTS: numeric_value NOT NULL count is 0 (or under 8).
      REFUTED IF: numeric_value is populated on those rows. Then the exclusion has
      another cause and this hypothesis is wrong - do not force it.

    HYPOTHESIS 2 - THE MINORITY-CLASS FLOOR.
      MinorityFraction returns 0.5 for a numeric outcome and the SMALLEST CATEGORY
      SHARE for a categorical one. ReadinessGate blocks below 0.03. Loader hardcodes
      FreshnessFactor 0.0 and completeness measured 1.0, and heats/events both clear
      Ready, so minority balance is the ONLY dimension that can block these two.
      PREDICTS: defect.class smallest category share < 0.03 - consistent with the
      T-015 catalogue placing LAMINATION and SENSOR_ARTEFACT at 2.0 percent.
      For defect.severity the prediction is NOT obvious. Three graded levels should
      clear 0.10 comfortably. If it also blocks, look for a NULL or rare category
      forming a tiny bucket, which section 2b exposes.

    A NULL category_value groups into "" and counts as its own class - that is what
    the service does, so that is what this measures.

.EXAMPLE
    .\tools\run\Invoke-PpiqT025ReadinessB.ps1
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

Rule "PPIQ T-025 READINESS DIAGNOSIS PART B - READ ONLY"
$repoRoot = (Get-Location).Path
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
Say ("Database : " + $Database)
Say "Two hypotheses, each with a stated prediction and a stated refutation."

$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t025rdb_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null

try {
    # -------------------------------------------------------------- 1
    Rule "1 - HYPOTHESIS 1: THE OUTCOME REPRESENTATION IS EMPTY"
    Say "PREDICTS numeric_value is NULL on the defect.rate_per_m2 rows, so repr is"
    Say "empty and every feature aligns 0 pairs. REFUTED if numeric_value is populated."
    $h1 = Invoke-Sql -Tag "h1" -Sql @"
SELECT outcome_key,
       count(*)                     AS rows,
       count(numeric_value)         AS numeric_not_null,
       count(category_value)        AS category_not_null,
       count(*) - count(numeric_value) AS numeric_null,
       min(numeric_value)           AS min_value,
       max(numeric_value)           AS max_value
FROM public.ml_outcome_values
WHERE grain = 'coil'
GROUP BY outcome_key
ORDER BY outcome_key;
"@
    Say $h1.Output

    $verdict1 = Get-SqlLines -Tag "h1raw" -Sql @"
SELECT count(numeric_value)::text
FROM public.ml_outcome_values
WHERE lower(outcome_key) = 'defect.rate_per_m2' AND grain = 'coil';
"@
    $n1 = -1
    if ($verdict1.Count -gt 0) { $n1 = [int]$verdict1[0] }
    Say ""
    Say ("  defect.rate_per_m2 rows with a non-null numeric_value : " + $n1)
    if ($n1 -eq 0) {
        Say "  HYPOTHESIS 1 CONFIRMED. The outcome representation is empty, so the"
        Say "  engine had nothing to correlate against and excluded all 26 parameters"
        Say "  for want of pairs. The defect is in what writes ml_outcome_values, not"
        Say "  in the correlation engine and not in the readiness gate."
    } elseif ($n1 -ge 8) {
        Say "  HYPOTHESIS 1 REFUTED. The values are there. The exclusion has another"
        Say "  cause and this line of reasoning stops here rather than being stretched."
        Say "  Next place to look: ChooseMethod / Measure returning NaN per feature,"
        Say "  which excludes with 'Undefined statistic (constant / zero-variance input)'."
    } else {
        Say "  PARTIAL. Values exist but fall under MinPairs = 8. Treat as confirmed"
        Say "  in effect, and record the actual count rather than rounding it to zero."
    }

    # -------------------------------------------------------------- 2
    Rule "2 - HYPOTHESIS 2: THE MINORITY-CLASS FLOOR IS THE ONLY BLOCKER"
    Say "MinorityFraction takes the SMALLEST category share. ReadinessGate: Ready at"
    Say ">= 0.10, Partial at >= 0.03, Blocked below 0.03. A NULL category groups as \"\"."
    Say ""
    Say "2a - defect.class category distribution"
    $h2a = Invoke-Sql -Tag "h2a" -Sql @"
SELECT COALESCE(category_value, '(null)') AS category,
       count(*) AS rows,
       round(100.0 * count(*) / SUM(count(*)) OVER (), 3) AS pct
FROM public.ml_outcome_values
WHERE lower(outcome_key) = 'defect.class' AND grain = 'coil'
GROUP BY category_value
ORDER BY count(*) ASC;
"@
    Say $h2a.Output

    Say "2b - defect.severity category distribution"
    $h2b = Invoke-Sql -Tag "h2b" -Sql @"
SELECT COALESCE(category_value, '(null)') AS category,
       count(*) AS rows,
       round(100.0 * count(*) / SUM(count(*)) OVER (), 3) AS pct
FROM public.ml_outcome_values
WHERE lower(outcome_key) = 'defect.severity' AND grain = 'coil'
GROUP BY category_value
ORDER BY count(*) ASC;
"@
    Say $h2b.Output

    Say "2c - the gate verdict, computed the way MinorityFraction computes it"
    $h2c = Get-SqlLines -Tag "h2craw" -Sql @"
SELECT outcome_key || '~' || classes::text || '~' || to_char(min_share, 'FM0.00000')
FROM (
  SELECT outcome_key,
         count(*)::int AS classes,
         min(share)    AS min_share
  FROM (
    SELECT outcome_key,
           COALESCE(category_value, '') AS cat,
           count(*)::numeric / SUM(count(*)) OVER (PARTITION BY outcome_key) AS share
    FROM public.ml_outcome_values
    WHERE lower(outcome_key) IN ('defect.class', 'defect.severity') AND grain = 'coil'
    GROUP BY outcome_key, COALESCE(category_value, '')
  ) s
  GROUP BY outcome_key
) t
ORDER BY outcome_key;
"@
    Say ""
    Say ("  " + "outcome_key".PadRight(22) + "classes".PadRight(10) + "min share".PadRight(13) + "gate verdict")
    foreach ($line in $h2c) {
        $p = $line -split "~"
        if ($p.Count -ne 3) { continue }
        $share = [double]$p[2]
        if ($share -ge 0.10) {
            $v = "Ready - minority balance did NOT block this outcome"
        } elseif ($share -ge 0.03) {
            $v = "Partial - would NOT block; overall Partial still runs"
        } else {
            $v = "BLOCKED - below the 0.03 floor. This is the blocker."
        }
        Say ("  " + $p[0].PadRight(22) + $p[1].PadRight(10) + $p[2].PadRight(13) + $v)
    }
    Say ""
    Say "If either outcome shows Ready or Partial above, HYPOTHESIS 2 IS REFUTED for"
    Say "it and the block came from a dimension this analysis said could not block -"
    Say "which would mean the loader is not producing the numbers the code implies."

    # -------------------------------------------------------------- 3
    Rule "3 - THE FIVE OUTCOMES WITH NO PRODUCER, CONFIRMED FROM THE DATABASE"
    Say "Traced in source to 740/741 (defect.class, defect.severity) and 201"
    Say "(defect.rate_per_m2). The other five appear only in 204, which builds them"
    Say "from a synthetic series, not from the canonical population."
    $h3 = Invoke-Sql -Tag "h3" -Sql @"
SELECT d.outcome_key,
       d.grain,
       d.source_view_code,
       d.source_column,
       CASE WHEN v.n IS NULL THEN 'NO ROWS' ELSE v.n::text END AS materialised
FROM public.ml_outcome_definitions d
LEFT JOIN (
    SELECT lower(outcome_key) AS k, grain AS g, count(*) AS n
    FROM public.ml_outcome_values GROUP BY 1, 2
) v ON v.k = lower(d.outcome_key) AND v.g = d.grain
WHERE d.is_deleted = false AND d.status = 'Active'
ORDER BY d.outcome_key;
"@
    Say $h3.Output
}
catch {
    Say ("[ERROR] " + $_.Exception.Message)
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

Rule "WHAT THIS SETTLES"
Say "H1 confirmed -> the correlation engine and the readiness gate are both innocent."
Say "                The producer of ml_outcome_values is the defect."
Say "H1 refuted   -> stop here and look at per-feature method selection instead."
Say "H2 confirmed -> defect.class refuses for a real statistical reason created by"
Say "                the T-015 catalogue. That is a keepable genuine refusal."
Say "H2 refuted   -> a dimension believed unable to block did block, and the loader"
Say "                is not behaving the way the source reads."
Say ""
Say "No fix is proposed here. This script measures and stops."

$outFolder = Join-Path $repoRoot "docs\m1\evidence"
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$ev = Join-Path $outFolder ("T-025_readiness_diagnosis_B_" + $stamp + ".txt")
$clean = New-Object System.Text.StringBuilder
foreach ($ch in ($script:log -replace "`r`n", "`n").ToCharArray()) {
    if ([int]$ch -le 126 -or [int]$ch -eq 10) { [void]$clean.Append($ch) }
}
[System.IO.File]::WriteAllText($ev, ($clean.ToString() -replace "`n", "`r`n"),
    (New-Object System.Text.UTF8Encoding($false)))
Write-Host ""
Write-Host ("Evidence : " + $ev)
exit 0
