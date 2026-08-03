#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-014 step B - read-only evidence for timestamp lag structure and
    grade-conditioned chemistry in the captured donor schemas.

.DESCRIPTION
    THIS SCRIPT CHANGES NOTHING AND PROVES NOTHING ABOUT THE GENERATOR.
    It exists because two model corrections are pending and neither may be
    inferred from the failing comparison:

      1  The 33 timestamp differences suggest the lags are deterministic per
         position rather than random. That must be MEASURED per position, not
         inferred from the first and last timestamp of a column. Two endpoints
         are consistent with many different interiors.

      2  silicon_pct and carbon_pct miss on every quantile IN THE SAME
         DIRECTION, which noise does not do. The hypothesis is that chemistry is
         drawn per steel grade and the six bands mix into something that only
         looks uniform. If that relationship exists it is a property of the
         captured donor and the generator must reproduce it. If it does not
         exist, the answer is to keep looking, NOT to invent one.

    Sections:
      A  Tap, LF, sequence and rolling durations - are they constant?
      B  Rolling lag from tap_start BY COIL POSITION within its heat.
      C  Slab cut lag from sequence start BY slab_no.
      D  Pickling lag from rolling_end BY coil position, and the QA lag.
      E  Pass sample offsets within a coil BY stand_no.
      F  Defect observation lag from rolling_start BY defect ordinal.
      G  Chemistry BY STEEL GRADE - count, min, quartiles, max, mean, stddev.
      H  Other heat numerics by grade, to see whether the conditioning is
         chemistry-only or wider.
      I  Coil geometry by grade, same question one stage downstream.

    HOW TO READ IT. In sections A to F a stddev of 0 with distinct_values of 1
    means the lag is DETERMINISTIC for that position. Anything else is a real
    distribution and must be modelled as one.

    Read-only: catalogue and aggregate queries only, no INSERT, UPDATE, DELETE
    or DDL. Hardened to the same contract as the capture script.

.EXAMPLE
    .\tools\measure\Measure-PpiqT014Structure.ps1
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
        Write-Host "[FAIL] -PsqlPath given but not found: $Explicit"
        return $null
    }
    $c = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($null -ne $c) { return $c.Source }
    foreach ($p in @("C:\Program Files\PostgreSQL\16\bin\psql.exe",
                     "C:\Program Files\PostgreSQL\17\bin\psql.exe",
                     "C:\Program Files\PostgreSQL\15\bin\psql.exe")) {
        if (Test-Path -LiteralPath $p) { return $p }
    }
    Write-Host "[FAIL] psql.exe not found. Re-run with -PsqlPath."
    return $null
}

Write-Head "PPIQ T-014 STEP B - STRUCTURE EVIDENCE (READ-ONLY)"

$repoRoot = (Get-Location).Path
Write-Host ("Repo root : " + $repoRoot)
Write-Host ("Database  : " + $Database + " on " + $PgHost + ":" + $PgPort)

$psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $psql) { exit 2 }
Write-Host ("psql      : " + $psql)

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outFolder = $OutDir
if (-not [System.IO.Path]::IsPathRooted($OutDir)) {
    $outFolder = Join-Path $repoRoot $OutDir
}
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$evidencePath = Join-Path $outFolder ("T-014_structure_evidence_" + $stamp + ".txt")

$tmpDir = Join-Path $env:TEMP ("ppiq_t014_struct_" + $stamp)
New-Item -ItemType Directory -Path $tmpDir -Force | Out-Null
$sqlFile = Join-Path $tmpDir "structure.sql"
$resFile = Join-Path $tmpDir "result.txt"
$errFile = Join-Path $tmpDir "stderr.txt"

$sql = @'
\pset pager off
\pset border 2
\timing off

\qecho
\qecho ================================================================
\qecho SECTION A - FIXED DURATIONS. stddev 0 AND distinct 1 MEANS CONSTANT.
\qecho ================================================================
SELECT 'tap_end - tap_start' AS interval_name, count(*) AS rows,
       min(s)::bigint AS min_s, round(avg(s))::bigint AS avg_s, max(s)::bigint AS max_s,
       round(coalesce(stddev_samp(s), 0), 3) AS sd_s, count(DISTINCT s) AS distinct_values
FROM (SELECT EXTRACT(epoch FROM (tap_end_utc - tap_start_utc)) s FROM src_meltshop_pg.heats) x
UNION ALL
SELECT 'lf_start - tap_start', count(*), min(s)::bigint, round(avg(s))::bigint, max(s)::bigint,
       round(coalesce(stddev_samp(s), 0), 3), count(DISTINCT s)
FROM (SELECT EXTRACT(epoch FROM (l.treatment_start_utc - h.tap_start_utc)) s
      FROM src_meltshop_pg.lf_treatment l
      JOIN src_meltshop_pg.heats h ON h.heat_no = l.heat_no) x
UNION ALL
SELECT 'lf_end - lf_start', count(*), min(s)::bigint, round(avg(s))::bigint, max(s)::bigint,
       round(coalesce(stddev_samp(s), 0), 3), count(DISTINCT s)
FROM (SELECT EXTRACT(epoch FROM (treatment_end_utc - treatment_start_utc)) s
      FROM src_meltshop_pg.lf_treatment) x
UNION ALL
SELECT 'seq_start - tap_start', count(*), min(s)::bigint, round(avg(s))::bigint, max(s)::bigint,
       round(coalesce(stddev_samp(s), 0), 3), count(DISTINCT s)
FROM (SELECT EXTRACT(epoch FROM (q.start_time - h.tap_start_utc)) s
      FROM src_caster_oracle_shape.cast_sequence q
      JOIN src_caster_oracle_shape.cast_pieces p ON p.sequence_no = q.sequence_no AND p.slab_no = 1
      JOIN src_meltshop_pg.heats h ON h.heat_no = p.heat_no) x
UNION ALL
SELECT 'seq_end - seq_start', count(*), min(s)::bigint, round(avg(s))::bigint, max(s)::bigint,
       round(coalesce(stddev_samp(s), 0), 3), count(DISTINCT s)
FROM (SELECT EXTRACT(epoch FROM (end_time - start_time)) s
      FROM src_caster_oracle_shape.cast_sequence) x
UNION ALL
SELECT 'rolling_end - rolling_start', count(*), min(s)::bigint, round(avg(s))::bigint, max(s)::bigint,
       round(coalesce(stddev_samp(s), 0), 3), count(DISTINCT s)
FROM (SELECT EXTRACT(epoch FROM (rolling_end_time - rolling_start_time)) s
      FROM src_hsm_oracle_shape.hsm_coils) x
UNION ALL
SELECT 'pkl_exit - pkl_entry', count(*), min(s)::bigint, round(avg(s))::bigint, max(s)::bigint,
       round(coalesce(stddev_samp(s), 0), 3), count(DISTINCT s)
FROM (SELECT EXTRACT(epoch FROM (exit_time_utc - entry_time_utc)) s
      FROM src_pkl_mssql_shape.pickle_orders) x
UNION ALL
SELECT 'qa_sample - pkl_exit', count(*), min(s)::bigint, round(avg(s))::bigint, max(s)::bigint,
       round(coalesce(stddev_samp(s), 0), 3), count(DISTINCT s)
FROM (SELECT EXTRACT(epoch FROM (r.sample_time_utc - o.exit_time_utc)) s
      FROM src_pkl_mssql_shape.qa_lab_results r
      JOIN src_pkl_mssql_shape.pickle_orders o ON o.coil_id = r.coil_id) x
ORDER BY 1;

\qecho
\qecho ================================================================
\qecho SECTION B - ROLLING LAG FROM tap_start, BY COIL POSITION IN HEAT
\qecho ================================================================
WITH pos AS (
  SELECT c.heat_no,
         row_number() OVER (PARTITION BY c.heat_no ORDER BY c.coil_id) AS coil_pos,
         EXTRACT(epoch FROM (c.rolling_start_time - h.tap_start_utc)) AS lag_s
  FROM src_hsm_oracle_shape.hsm_coils c
  JOIN src_meltshop_pg.heats h ON h.heat_no = c.heat_no
)
SELECT coil_pos, count(*) AS coils,
       min(lag_s)::bigint AS min_s, round(avg(lag_s))::bigint AS avg_s,
       max(lag_s)::bigint AS max_s, round(coalesce(stddev_samp(lag_s), 0), 2) AS sd_s,
       count(DISTINCT lag_s) AS distinct_values
FROM pos GROUP BY coil_pos ORDER BY coil_pos;

\qecho
\qecho --- the same lag ignoring position, for comparison ---
SELECT count(*) AS coils, min(lag_s)::bigint AS min_s, round(avg(lag_s))::bigint AS avg_s,
       max(lag_s)::bigint AS max_s, round(coalesce(stddev_samp(lag_s), 0), 2) AS sd_s,
       count(DISTINCT lag_s) AS distinct_values
FROM (SELECT EXTRACT(epoch FROM (c.rolling_start_time - h.tap_start_utc)) lag_s
      FROM src_hsm_oracle_shape.hsm_coils c
      JOIN src_meltshop_pg.heats h ON h.heat_no = c.heat_no) x;

\qecho
\qecho ================================================================
\qecho SECTION C - SLAB CUT LAG FROM SEQUENCE START, BY slab_no
\qecho ================================================================
SELECT p.slab_no, count(*) AS slabs,
       min(s)::bigint AS min_s, round(avg(s))::bigint AS avg_s, max(s)::bigint AS max_s,
       round(coalesce(stddev_samp(s), 0), 2) AS sd_s, count(DISTINCT s) AS distinct_values
FROM (SELECT p.slab_no, EXTRACT(epoch FROM (p.cut_time - q.start_time)) s
      FROM src_caster_oracle_shape.cast_pieces p
      JOIN src_caster_oracle_shape.cast_sequence q ON q.sequence_no = p.sequence_no) p
GROUP BY p.slab_no ORDER BY p.slab_no;

\qecho
\qecho ================================================================
\qecho SECTION D - PICKLING LAG FROM rolling_end, BY COIL POSITION
\qecho ================================================================
WITH pos AS (
  SELECT row_number() OVER (PARTITION BY c.heat_no ORDER BY c.coil_id) AS coil_pos,
         EXTRACT(epoch FROM (o.entry_time_utc - c.rolling_end_time)) AS lag_s
  FROM src_pkl_mssql_shape.pickle_orders o
  JOIN src_hsm_oracle_shape.hsm_coils c ON c.coil_id = o.coil_id
)
SELECT coil_pos, count(*) AS orders,
       min(lag_s)::bigint AS min_s, round(avg(lag_s))::bigint AS avg_s,
       max(lag_s)::bigint AS max_s, round(coalesce(stddev_samp(lag_s), 0), 2) AS sd_s,
       count(DISTINCT lag_s) AS distinct_values
FROM pos GROUP BY coil_pos ORDER BY coil_pos;

\qecho
\qecho ================================================================
\qecho SECTION E - PASS SAMPLE OFFSET FROM rolling_start, BY stand_no
\qecho ================================================================
SELECT m.stand_no, count(*) AS passes,
       min(s)::bigint AS min_s, round(avg(s))::bigint AS avg_s, max(s)::bigint AS max_s,
       round(coalesce(stddev_samp(s), 0), 2) AS sd_s, count(DISTINCT s) AS distinct_values
FROM (SELECT m.stand_no, EXTRACT(epoch FROM (m.sample_time - c.rolling_start_time)) s
      FROM src_hsm_oracle_shape.hsm_pass_measurements m
      JOIN src_hsm_oracle_shape.hsm_coils c ON c.coil_id = m.coil_id) m
GROUP BY m.stand_no ORDER BY m.stand_no;

\qecho
\qecho ================================================================
\qecho SECTION F - DEFECT LAG FROM rolling_start, BY DEFECT ORDINAL ON THE COIL
\qecho ================================================================
WITH ord AS (
  SELECT row_number() OVER (PARTITION BY d.coil_id ORDER BY d.defect_row_id) AS defect_ordinal,
         EXTRACT(epoch FROM (d.event_time_utc - c.rolling_start_time)) AS lag_s
  FROM src_inspection_mysql_shape.parsytec_surface_defects d
  JOIN src_hsm_oracle_shape.hsm_coils c ON c.coil_id = d.coil_id
)
SELECT defect_ordinal, count(*) AS defects,
       min(lag_s)::bigint AS min_s, round(avg(lag_s))::bigint AS avg_s,
       max(lag_s)::bigint AS max_s, round(coalesce(stddev_samp(lag_s), 0), 2) AS sd_s,
       count(DISTINCT lag_s) AS distinct_values
FROM ord GROUP BY defect_ordinal ORDER BY defect_ordinal;

\qecho
\qecho ================================================================
\qecho SECTION G - CHEMISTRY BY STEEL GRADE. THE DIRECTIONAL BIAS QUESTION.
\qecho IF EACH GRADE HAS ITS OWN BAND, THE GENERATOR MUST REPRODUCE THAT.
\qecho ================================================================
SELECT steel_grade, 'carbon_pct' AS element, count(*) AS heats,
       min(carbon_pct) AS min_v,
       round(percentile_cont(0.25) WITHIN GROUP (ORDER BY carbon_pct)::numeric, 5) AS p25,
       round(percentile_cont(0.50) WITHIN GROUP (ORDER BY carbon_pct)::numeric, 5) AS p50,
       round(percentile_cont(0.75) WITHIN GROUP (ORDER BY carbon_pct)::numeric, 5) AS p75,
       max(carbon_pct) AS max_v,
       round(avg(carbon_pct)::numeric, 5) AS mean_v,
       round(coalesce(stddev_samp(carbon_pct), 0)::numeric, 5) AS sd_v
FROM src_meltshop_pg.heats GROUP BY steel_grade
UNION ALL
SELECT steel_grade, 'manganese_pct', count(*), min(manganese_pct),
       round(percentile_cont(0.25) WITHIN GROUP (ORDER BY manganese_pct)::numeric, 5),
       round(percentile_cont(0.50) WITHIN GROUP (ORDER BY manganese_pct)::numeric, 5),
       round(percentile_cont(0.75) WITHIN GROUP (ORDER BY manganese_pct)::numeric, 5),
       max(manganese_pct), round(avg(manganese_pct)::numeric, 5),
       round(coalesce(stddev_samp(manganese_pct), 0)::numeric, 5)
FROM src_meltshop_pg.heats GROUP BY steel_grade
UNION ALL
SELECT steel_grade, 'silicon_pct', count(*), min(silicon_pct),
       round(percentile_cont(0.25) WITHIN GROUP (ORDER BY silicon_pct)::numeric, 5),
       round(percentile_cont(0.50) WITHIN GROUP (ORDER BY silicon_pct)::numeric, 5),
       round(percentile_cont(0.75) WITHIN GROUP (ORDER BY silicon_pct)::numeric, 5),
       max(silicon_pct), round(avg(silicon_pct)::numeric, 5),
       round(coalesce(stddev_samp(silicon_pct), 0)::numeric, 5)
FROM src_meltshop_pg.heats GROUP BY steel_grade
ORDER BY 2, 1;

\qecho
\qecho ================================================================
\qecho SECTION H - OTHER HEAT NUMERICS BY GRADE. IS THE CONDITIONING WIDER?
\qecho ================================================================
SELECT steel_grade, count(*) AS heats,
       round(avg(heat_weight_ton)::numeric, 3) AS mean_weight,
       round(coalesce(stddev_samp(heat_weight_ton), 0)::numeric, 3) AS sd_weight,
       round(avg(actual_temp_c)::numeric, 2) AS mean_temp,
       round(coalesce(stddev_samp(actual_temp_c), 0)::numeric, 2) AS sd_temp,
       round(avg(oxygen_nm3)::numeric, 1) AS mean_oxygen,
       round(avg(power_kwh)::numeric, 1) AS mean_power
FROM src_meltshop_pg.heats GROUP BY steel_grade ORDER BY steel_grade;

\qecho
\qecho ================================================================
\qecho SECTION I - COIL GEOMETRY BY GRADE, ONE STAGE DOWNSTREAM
\qecho ================================================================
SELECT h.steel_grade, count(*) AS coils,
       round(avg(c.target_thickness_mm)::numeric, 4) AS mean_target_thk,
       round(coalesce(stddev_samp(c.target_thickness_mm), 0)::numeric, 4) AS sd_target_thk,
       count(DISTINCT c.target_thickness_mm) AS distinct_thk,
       round(avg(c.target_width_mm)::numeric, 2) AS mean_target_wid,
       count(DISTINCT c.target_width_mm) AS distinct_wid
FROM src_hsm_oracle_shape.hsm_coils c
JOIN src_meltshop_pg.heats h ON h.heat_no = c.heat_no
GROUP BY h.steel_grade ORDER BY h.steel_grade;

\qecho
\qecho --- distinct target thickness and width values overall ---
SELECT 'target_thickness_mm' AS column_name, count(DISTINCT target_thickness_mm) AS distinct_values
FROM src_hsm_oracle_shape.hsm_coils
UNION ALL
SELECT 'target_width_mm', count(DISTINCT target_width_mm) FROM src_hsm_oracle_shape.hsm_coils
UNION ALL
SELECT 'cast_pieces.width_mm', count(DISTINCT width_mm) FROM src_caster_oracle_shape.cast_pieces
UNION ALL
SELECT 'cast_pieces.thickness_mm', count(DISTINCT thickness_mm) FROM src_caster_oracle_shape.cast_pieces
ORDER BY 1;

\qecho
\qecho ================================================================
\qecho END OF STRUCTURE EVIDENCE
\qecho ================================================================
'@

[System.IO.File]::WriteAllText($sqlFile, $sql, (New-Object System.Text.UTF8Encoding($false)))
Write-Host ("SQL file  : " + $sqlFile)

$prevC = [Console]::OutputEncoding
$prevO = $OutputEncoding
$exit = 1
try {
    [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false)
    $OutputEncoding           = New-Object System.Text.UTF8Encoding($false)
    $env:PGPASSWORD           = $PgPassword
    $env:PGCLIENTENCODING     = "UTF8"

    Write-Head "RUNNING STRUCTURE MEASUREMENT"
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

if (-not (Test-Path -LiteralPath $resFile)) {
    Write-Host "[FAIL] psql produced no result file."
    exit 3
}
$result = [System.IO.File]::ReadAllText($resFile)
if ([string]::IsNullOrWhiteSpace($result)) {
    Write-Host "[FAIL] the result file is empty."
    exit 3
}

$result = $result -replace "`r`n", "`n"
$result = [regex]::Replace($result, "\x1B\[[0-9;?]*[A-Za-z]", "")
$clean = New-Object System.Text.StringBuilder
foreach ($ch in $result.ToCharArray()) {
    if ([int]$ch -le 126 -or [int]$ch -eq 10) { [void]$clean.Append($ch) }
}
$result = $clean.ToString()

$header = @(
    "================================================================",
    "PPIQ T-014 STEP B - STRUCTURE EVIDENCE (READ-ONLY)",
    "================================================================",
    ("Generated At : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss")),
    ("Database     : " + $Database),
    ("psql exit    : " + $exit),
    "",
    "This file exists so two pending model corrections are DERIVED FROM",
    "MEASUREMENT rather than inferred from a failing comparison.",
    "",
    "HOW TO READ SECTIONS A to F: sd_s of 0 with distinct_values of 1 means",
    "the interval is DETERMINISTIC. Anything else is a real distribution.",
    "",
    "SECTION G decides whether chemistry is conditioned on steel grade. If",
    "each grade holds its own band, that is a captured property and the",
    "generator must reproduce it. If the bands overlap completely, the",
    "directional bias has another cause and the search continues.",
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
if ($len -lt 1024) {
    Write-Host "[FAIL] the evidence file is under 1 KB. Nothing useful was captured."
    exit 5
}
$nonAscii = Count-NonAscii ([System.IO.File]::ReadAllText($evidencePath))

Write-Head "RESULT"
Write-Host ("Evidence  : " + $evidencePath)
Write-Host ("Bytes     : " + $len)
Write-Host ("Non-ASCII : " + $nonAscii)
if ($nonAscii -gt 0) {
    Write-Host "[FAIL] non-ASCII in the evidence file."
    exit 4
}
Write-Host ""
Write-Host "[OK] Structure evidence written. NOTHING was changed."
Write-Host ""
Write-Host "Read section B first: if sd_s is 0 for every coil position, the"
Write-Host "rolling lag is deterministic and the generator's uniform draw is"
Write-Host "simply wrong. Then read section G for the chemistry question."
exit 0
