#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-014 - exact value histograms for the minute-quantised process
    intervals. Read-only.

.DESCRIPTION
    The v2.1 proof run left 26 differences, all generator model defects. Five of
    them are the SHAPE of a quantised interval:

      B01_lf_start_offset    captured sd 341, a uniform over that range gives 433
      C01_seq_start_offset   captured sd 553, a uniform gives 848
      C04_cut_step_per_slab  right four values, wrong weights
      H01_defect_lag         IQR/sd is 1.63, neither normal 1.35 nor uniform 1.73
      F02_pkl_duration       one minute of quantile drift

    I have already guessed a shape twice in this task and been wrong twice. These
    intervals are quantised to the minute and every one has fewer than fifty
    distinct values, so THE EXACT DISTRIBUTION IS MEASURABLE. Reproducing a
    measured empirical distribution is capture. Fitting a named distribution to
    five summary numbers is modelling, and modelling is T-015's business.

    This script emits, for every interval with at most 60 distinct values, the
    complete value list with its exact count and share. The generator then draws
    from those weights, with the evidence file cited at the constant.

    Intervals with more distinct values - the rolling lag at about 1,650 and the
    downtime span at 204 - are left as distributions and are not histogrammed
    here; they are handled by their own rules.

    NOTHING IS CHANGED BY THIS SCRIPT.

.EXAMPLE
    .\tools\measure\Measure-PpiqT014IntervalHistograms.ps1
#>

[CmdletBinding()]
param(
    [string]$PgHost     = "127.0.0.1",
    [int]   $PgPort     = 5432,
    [string]$PgUser     = "ppiq_dev",
    [string]$PgPassword = "ppiq_dev_local_only",
    [string]$Database   = "ppiq_presentation",
    [string]$OutDir     = "docs\m1\evidence",
    [string]$PsqlPath   = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

function Write-Head {
    param([string]$Text)
    Write-Host ""
    Write-Host ("=" * 78)
    Write-Host $Text
    Write-Host ("=" * 78)
}

function Count-NonAscii {
    param([string]$Text)
    if ([string]::IsNullOrEmpty($Text)) { return 0 }
    $n = 0
    foreach ($ch in $Text.ToCharArray()) { if ([int]$ch -gt 126) { $n = $n + 1 } }
    return $n
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

Write-Head "PPIQ T-014 - EXACT INTERVAL HISTOGRAMS (READ-ONLY)"

$repoRoot = (Get-Location).Path
Write-Host ("Repo root : " + $repoRoot)
Write-Host ("Database  : " + $Database)

$psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $psql) { Write-Host "[FAIL] psql.exe not found. Re-run with -PsqlPath."; exit 2 }
Write-Host ("psql      : " + $psql)

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outFolder = $OutDir
if (-not [System.IO.Path]::IsPathRooted($OutDir)) { $outFolder = Join-Path $repoRoot $OutDir }
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$evidencePath = Join-Path $outFolder ("T-014_interval_histograms_" + $stamp + ".txt")

$tmpDir = Join-Path $env:TEMP ("ppiq_t014_hist_" + $stamp)
New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
$sqlFile = Join-Path $tmpDir "hist.sql"
$resFile = Join-Path $tmpDir "result.txt"
$errFile = Join-Path $tmpDir "stderr.txt"

$sql = @'
\pset pager off
\pset border 2
\timing off

\qecho
\qecho ================================================================
\qecho SECTION K - EXACT VALUE HISTOGRAMS FOR QUANTISED INTERVALS
\qecho EVERY INTERVAL WITH 60 OR FEWER DISTINCT VALUES, IN FULL.
\qecho THE GENERATOR DRAWS FROM THESE WEIGHTS. NO SHAPE IS ASSUMED.
\qecho ================================================================
WITH iv AS (
    SELECT 'A02_tap_duration' AS interval_name,
           EXTRACT(epoch FROM (tap_end_utc - tap_start_utc)) AS s
    FROM src_meltshop_pg.heats
  UNION ALL
    SELECT 'A03_heat_update_lag', EXTRACT(epoch FROM (source_updated_at_utc - tap_end_utc))
    FROM src_meltshop_pg.heats
  UNION ALL
    SELECT 'B01_lf_start_offset', EXTRACT(epoch FROM (l.treatment_start_utc - h.tap_start_utc))
    FROM src_meltshop_pg.lf_treatment l
    JOIN src_meltshop_pg.heats h ON h.heat_no = l.heat_no
  UNION ALL
    SELECT 'B02_lf_duration', EXTRACT(epoch FROM (treatment_end_utc - treatment_start_utc))
    FROM src_meltshop_pg.lf_treatment
  UNION ALL
    SELECT 'B03_lf_update_lag', EXTRACT(epoch FROM (source_updated_at_utc - treatment_end_utc))
    FROM src_meltshop_pg.lf_treatment
  UNION ALL
    SELECT 'C01_seq_start_offset', EXTRACT(epoch FROM (q.start_time - h.tap_start_utc))
    FROM src_caster_oracle_shape.cast_sequence q
    JOIN src_caster_oracle_shape.cast_pieces p ON p.sequence_no = q.sequence_no AND p.slab_no = 1
    JOIN src_meltshop_pg.heats h ON h.heat_no = p.heat_no
  UNION ALL
    SELECT 'C02_seq_duration', EXTRACT(epoch FROM (end_time - start_time))
    FROM src_caster_oracle_shape.cast_sequence
  UNION ALL
    SELECT 'C03_seq_update_lag', EXTRACT(epoch FROM (last_update_ts - end_time))
    FROM src_caster_oracle_shape.cast_sequence
  UNION ALL
    SELECT 'C04_cut_step_per_slab',
           EXTRACT(epoch FROM (p.cut_time - q.start_time)) / p.slab_no
    FROM src_caster_oracle_shape.cast_pieces p
    JOIN src_caster_oracle_shape.cast_sequence q ON q.sequence_no = p.sequence_no
  UNION ALL
    SELECT 'C05_piece_update_lag', EXTRACT(epoch FROM (last_update_ts - cut_time))
    FROM src_caster_oracle_shape.cast_pieces
  UNION ALL
    SELECT 'D02_rolling_duration', EXTRACT(epoch FROM (rolling_end_time - rolling_start_time))
    FROM src_hsm_oracle_shape.hsm_coils
  UNION ALL
    SELECT 'D03_coil_update_lag', EXTRACT(epoch FROM (last_update_ts - rolling_end_time))
    FROM src_hsm_oracle_shape.hsm_coils
  UNION ALL
    SELECT 'E02_pass_update_lag', EXTRACT(epoch FROM (last_update_ts - sample_time))
    FROM src_hsm_oracle_shape.hsm_pass_measurements
  UNION ALL
    SELECT 'F02_pkl_duration', EXTRACT(epoch FROM (exit_time_utc - entry_time_utc))
    FROM src_pkl_mssql_shape.pickle_orders
  UNION ALL
    SELECT 'F03_pkl_update_lag', EXTRACT(epoch FROM (modified_at_utc - exit_time_utc))
    FROM src_pkl_mssql_shape.pickle_orders
  UNION ALL
    SELECT 'G02_qa_update_lag', EXTRACT(epoch FROM (modified_at_utc - sample_time_utc))
    FROM src_pkl_mssql_shape.qa_lab_results
  UNION ALL
    SELECT 'H01_defect_lag_from_rolling', EXTRACT(epoch FROM (d.event_time_utc - c.rolling_start_time))
    FROM src_inspection_mysql_shape.parsytec_surface_defects d
    JOIN src_hsm_oracle_shape.hsm_coils c ON c.coil_id = d.coil_id
  UNION ALL
    SELECT 'H02_defect_update_lag', EXTRACT(epoch FROM (updated_at_utc - event_time_utc))
    FROM src_inspection_mysql_shape.parsytec_surface_defects
  UNION ALL
    SELECT 'I02_downtime_update_lag', EXTRACT(epoch FROM (updated_at_utc - end_time_utc))
    FROM src_inspection_mysql_shape.downtime_events
), keep AS (
    SELECT interval_name FROM iv WHERE s IS NOT NULL
    GROUP BY interval_name HAVING count(DISTINCT s) <= 60
)
SELECT iv.interval_name,
       iv.s::bigint AS value_s,
       count(*) AS occurrences,
       round(100.0 * count(*) / sum(count(*)) OVER (PARTITION BY iv.interval_name), 4) AS pct
FROM iv
JOIN keep k ON k.interval_name = iv.interval_name
WHERE iv.s IS NOT NULL
GROUP BY iv.interval_name, iv.s
ORDER BY iv.interval_name, iv.s;

\qecho
\qecho ================================================================
\qecho SECTION K2 - THE INTERVALS TOO WIDE TO HISTOGRAM
\qecho These keep their distribution rules. Listed so the omission is
\qecho visible rather than silent.
\qecho ================================================================
WITH iv AS (
    SELECT 'D01_rolling_lag_from_tap' AS interval_name,
           EXTRACT(epoch FROM (c.rolling_start_time - h.tap_start_utc)) AS s
    FROM src_hsm_oracle_shape.hsm_coils c
    JOIN src_meltshop_pg.heats h ON h.heat_no = c.heat_no
  UNION ALL
    SELECT 'F01_pkl_entry_lag', EXTRACT(epoch FROM (o.entry_time_utc - c.rolling_end_time))
    FROM src_pkl_mssql_shape.pickle_orders o
    JOIN src_hsm_oracle_shape.hsm_coils c ON c.coil_id = o.coil_id
  UNION ALL
    SELECT 'I01_downtime_span', EXTRACT(epoch FROM (end_time_utc - start_time_utc))
    FROM src_inspection_mysql_shape.downtime_events
)
SELECT interval_name, count(*) AS observations, count(DISTINCT s) AS distinct_values,
       min(s)::bigint AS min_s, max(s)::bigint AS max_s,
       round(coalesce(stddev_samp(s), 0)::numeric, 3) AS sd_s,
       round((max(s) - min(s))::numeric / sqrt(12.0), 3) AS sd_if_uniform
FROM iv WHERE s IS NOT NULL GROUP BY interval_name ORDER BY interval_name;

\qecho
\qecho ================================================================
\qecho SECTION K3 - THE DOWNTIME EVENT WINDOW
\qecho The captured extremes are ORDER STATISTICS of the true window,
\qecho not the window itself. With n draws the expected gap between the
\qecho true bound and the sample extreme is range / (n + 1).
\qecho ================================================================
SELECT count(*) AS events,
       min(start_time_utc)::text AS min_start,
       max(start_time_utc)::text AS max_start,
       EXTRACT(epoch FROM (max(start_time_utc) - min(start_time_utc)))::bigint AS observed_range_s,
       round(EXTRACT(epoch FROM (max(start_time_utc) - min(start_time_utc)))::numeric
             / (count(*) - 1), 1) AS expected_gap_per_side_s
FROM src_inspection_mysql_shape.downtime_events;

\qecho
\qecho ================================================================
\qecho END OF INTERVAL HISTOGRAMS
\qecho ================================================================
'@

[System.IO.File]::WriteAllText($sqlFile, $sql, (New-Object System.Text.UTF8Encoding($false)))

$prevC = [Console]::OutputEncoding
$prevO = $OutputEncoding
$exit = 1
try {
    [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
    $OutputEncoding           = New-Object System.Text.UTF8Encoding($false)
    $env:PGPASSWORD           = $PgPassword
    $env:PGCLIENTENCODING     = "UTF8"
    Write-Head "RUNNING"
    $a = @("-X", "-h", $PgHost, "-p", "$PgPort", "-U", $PgUser, "-d", $Database,
           "-v", "ON_ERROR_STOP=0", "-f", $sqlFile, "-o", $resFile)
    $p = Start-Process -FilePath $psql -ArgumentList $a -NoNewWindow -Wait -PassThru `
                       -RedirectStandardError $errFile
    $exit = $p.ExitCode
    Write-Host ("psql exit : " + $exit)
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    [Console]::OutputEncoding = $prevC
    $OutputEncoding           = $prevO
}

$errText = ""
if (Test-Path -LiteralPath $errFile) { $errText = [System.IO.File]::ReadAllText($errFile) }
if (-not [string]::IsNullOrWhiteSpace($errText)) {
    Write-Head "PSQL STDERR - READ THIS"
    Write-Host $errText
}
if (-not (Test-Path -LiteralPath $resFile)) { Write-Host "[FAIL] no result file."; exit 3 }
$result = [System.IO.File]::ReadAllText($resFile)
if ([string]::IsNullOrWhiteSpace($result)) { Write-Host "[FAIL] empty result."; exit 3 }

$result = $result -replace "`r`n", "`n"
$clean = New-Object System.Text.StringBuilder
foreach ($ch in $result.ToCharArray()) {
    if ([int]$ch -le 126 -or [int]$ch -eq 10) { [void]$clean.Append($ch) }
}
$result = $clean.ToString()

$header = @(
    "================================================================",
    "PPIQ T-014 - EXACT INTERVAL HISTOGRAMS (READ-ONLY)",
    "================================================================",
    ("Generated At : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss")),
    ("Database     : " + $Database),
    ("psql exit    : " + $exit),
    "",
    "The v2.1 proof left 26 differences, all generator model defects. Five are",
    "the SHAPE of a quantised interval. A shape has been guessed twice in this",
    "task and been wrong twice, so it is measured instead.",
    "",
    "Section K is the COMPLETE value list with exact counts for every interval",
    "of 60 or fewer distinct values. The generator draws from these weights and",
    "assumes no named distribution.",
    "",
    "Section K2 lists the intervals too wide to histogram, so the omission is",
    "visible. Section K3 gives the downtime window's expected extreme gap, which",
    "is why a draw over the captured extremes lands inside them.",
    "================================================================",
    ""
) -join "`r`n"

$final = $header + "`r`n" + ($result -replace "`n", "`r`n")
[System.IO.File]::WriteAllText($evidencePath, $final, (New-Object System.Text.UTF8Encoding($false)))

if (-not (Test-Path -LiteralPath $evidencePath)) {
    Write-Host "[FAIL] the evidence file does not exist at the path reported."
    exit 5
}
$len = (Get-Item -LiteralPath $evidencePath).Length
if ($len -lt 1024) { Write-Host "[FAIL] evidence under 1 KB."; exit 5 }
$nonAscii = Count-NonAscii ([System.IO.File]::ReadAllText($evidencePath))

Write-Head "RESULT"
Write-Host ("Evidence  : " + $evidencePath)
Write-Host ("Bytes     : " + $len)
Write-Host ("Non-ASCII : " + $nonAscii)
if ($nonAscii -gt 0) { Write-Host "[FAIL] non-ASCII."; exit 4 }
Write-Host ""
Write-Host "[OK] Histograms written. NOTHING was changed."
exit 0
