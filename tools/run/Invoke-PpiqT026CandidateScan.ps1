#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-026 candidate scan - the hand-checking step. READ ONLY.

.DESCRIPTION
    T-026 requires "at least three hand-checked phenomena". Hand-checked means
    MEASURED FIRST and declared afterwards. Declaring an expected band and hoping
    the data agrees is the opposite: it makes a PASS meaningless because the band
    was fitted to nothing.

    So this scan measures candidate relationships that CAN be expressed against
    the canonical schema, and the three seed manifest rows are then written from
    what it returns.

    WHAT IT CAN AND CANNOT LOOK AT, established from the 147-column inventory
    rather than from memory:
      material_units          has material_code, material_unit_type, product_family,
                              grade_or_recipe, site_id, production_start_utc
                              and NO campaign key
      quality_events          has material_unit_id, defect_catalog_id, event_at_utc,
                              event_type, severity, decision and NO defect position
      downtime_events         has stopped_minutes AND production_impact_minutes as
                              two separate numeric columns
      parameter_observations  301,560 rows, numeric_value per material unit
    Campaign ageing and defect positioning are therefore NOT expressible here.

    THE MEASURE IS THE SAME ONE THE HARNESS USES. Spearman over AVERAGE ranks,
    computed in SQL exactly as phenomenon_harness.py computes it in Python, so a
    band declared from this scan means the same thing when the harness re-measures
    it. Ties get average ranks in both places.

    NOTHING HERE DECIDES A VERDICT. It reports magnitudes and populations so that
    an expectation can be written down honestly. A strong number found here is a
    candidate, not a finding.

.EXAMPLE
    .\tools\run\Invoke-PpiqT026CandidateScan.ps1
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
    param([string]$Sql, [string]$Tag)
    $f = Join-Path $script:tmp ($Tag + ".sql")
    $o = Join-Path $script:tmp ($Tag + ".out")
    $e = Join-Path $script:tmp ($Tag + ".err")
    [System.IO.File]::WriteAllText($f, $Sql, (New-Object System.Text.UTF8Encoding($false)))
    $a = @("-X", "-w", "-h", $PgHost, "-p", "$PgPort", "-U", $PgUser, "-d", $Database,
           "-v", "ON_ERROR_STOP=1", "-f", $f, "-o", $o)
    $p = Start-Process -FilePath $script:psql -ArgumentList $a -NoNewWindow -Wait `
                       -PassThru -RedirectStandardError $e
    $r = New-Object psobject
    Add-Member -InputObject $r -MemberType NoteProperty -Name ExitCode -Value $p.ExitCode
    Add-Member -InputObject $r -MemberType NoteProperty -Name Output   -Value (Read-IfExists $o)
    Add-Member -InputObject $r -MemberType NoteProperty -Name Error    -Value (Read-IfExists $e)
    return $r
}
function Run {
    param([string]$Sql, [string]$Tag)
    $r = Invoke-Sql -Sql $Sql -Tag $Tag
    if ($r.ExitCode -ne 0) {
        Say ("[FAIL] " + $Tag + " : " + ($r.Error -replace "`r", "" -replace "`n", " ").Trim())
    }
    Say $r.Output
}

Rule "PPIQ T-026 CANDIDATE SCAN - READ ONLY"
$repoRoot = (Get-Location).Path
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t026scan_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
Say ("Database : " + $Database)
Say "No write, no engine call. Spearman over average ranks, as the harness computes it."

try {
    Rule "1 - PARAMETER vs DEFECT COUNT PER COIL"
    Say "One row per coil: the mean of each parameter against the number of"
    Say "CATALOGUED defects on that coil. Events with no defect_catalog_id are"
    Say "excluded, so Disposition does not inflate the count."
    Run -Tag "p_defects" -Sql @"
\pset border 2
WITH coils AS (
    SELECT id FROM public.material_units
    WHERE is_deleted = false AND lower(coalesce(material_unit_type,'')) LIKE '%coil%'
), defects AS (
    SELECT c.id AS unit_id,
           count(qe.id) FILTER (WHERE qe.defect_catalog_id IS NOT NULL) AS y
    FROM coils c
    LEFT JOIN public.quality_events qe
           ON qe.material_unit_id = c.id AND qe.is_deleted = false
    GROUP BY c.id
), params AS (
    SELECT po.material_unit_id AS unit_id,
           pd.parameter_code,
           avg(po.numeric_value) AS x
    FROM public.parameter_observations po
    JOIN public.parameter_definitions pd
      ON pd.id = po.parameter_definition_id AND pd.is_deleted = false
    WHERE po.is_deleted = false AND po.numeric_value IS NOT NULL
    GROUP BY po.material_unit_id, pd.parameter_code
), joined AS (
    SELECT p.parameter_code, p.x, d.y
    FROM params p JOIN defects d ON d.unit_id = p.unit_id
), ranked AS (
    SELECT parameter_code,
           avg(rn_x) OVER (PARTITION BY parameter_code, x) AS rx,
           avg(rn_y) OVER (PARTITION BY parameter_code, y) AS ry
    FROM (
        SELECT parameter_code, x, y,
               row_number() OVER (PARTITION BY parameter_code ORDER BY x) AS rn_x,
               row_number() OVER (PARTITION BY parameter_code ORDER BY y) AS rn_y
        FROM joined
    ) t
)
SELECT parameter_code,
       count(*) AS n,
       round(corr(rx, ry)::numeric, 4) AS spearman_rho
FROM ranked
GROUP BY parameter_code
HAVING count(*) >= 50
ORDER BY abs(coalesce(corr(rx, ry), 0)) DESC
LIMIT 30;
"@

    Rule "2 - THE DOWNTIME PAIR"
    Say "stopped_minutes against production_impact_minutes. A plant engineer would"
    Say "predeclare that a longer stoppage costs proportionally more production."
    Say "Whether this fleet agrees is exactly what a harness is for."
    Run -Tag "downtime" -Sql @"
\pset border 2
WITH d AS (
    SELECT stopped_minutes::double precision AS x,
           production_impact_minutes::double precision AS y
    FROM public.downtime_events
    WHERE is_deleted = false
      AND stopped_minutes IS NOT NULL
      AND production_impact_minutes IS NOT NULL
), ranked AS (
    SELECT avg(rn_x) OVER (PARTITION BY x) AS rx,
           avg(rn_y) OVER (PARTITION BY y) AS ry
    FROM (
        SELECT x, y,
               row_number() OVER (ORDER BY x) AS rn_x,
               row_number() OVER (ORDER BY y) AS rn_y
        FROM d
    ) t
)
SELECT count(*) AS n,
       round(corr(rx, ry)::numeric, 4) AS spearman_rho
FROM ranked;

\echo ''
\echo 'the two quantities side by side, to confirm they are not the same column twice'
SELECT count(*) AS rows,
       round(min(stopped_minutes), 2)            AS stopped_min,
       round(max(stopped_minutes), 2)            AS stopped_max,
       round(avg(stopped_minutes), 2)            AS stopped_avg,
       round(min(production_impact_minutes), 2)  AS impact_min,
       round(max(production_impact_minutes), 2)  AS impact_max,
       round(avg(production_impact_minutes), 2)  AS impact_avg,
       count(*) FILTER (WHERE stopped_minutes = production_impact_minutes) AS identical_rows
FROM public.downtime_events
WHERE is_deleted = false;
"@

    Rule "3 - DEFECT CODE POPULATIONS"
    Say "For choosing a phenomenon whose population genuinely cannot support a"
    Say "claim, so the harness reports INSUFFICIENT rather than judging it."
    Run -Tag "codes" -Sql @"
\pset border 2
SELECT dc.defect_code,
       count(qe.id) AS events,
       count(DISTINCT qe.material_unit_id) AS coils
FROM public.quality_events qe
JOIN public.defect_catalogs dc ON dc.id = qe.defect_catalog_id AND dc.is_deleted = false
WHERE qe.is_deleted = false
GROUP BY dc.defect_code
ORDER BY count(qe.id) ASC;
"@

    Rule "4 - WHAT A CONDITIONING VARIABLE COULD BE"
    Say "conditioning_variable must be a column the population_query returns."
    Say "These are the categorical columns with enough levels to stratify on."
    Run -Tag "strata" -Sql @"
\pset border 2
SELECT 'material_units.grade_or_recipe' AS candidate,
       count(DISTINCT grade_or_recipe) AS levels, count(*) AS rows
FROM public.material_units WHERE is_deleted = false AND grade_or_recipe IS NOT NULL
UNION ALL
SELECT 'material_units.product_family', count(DISTINCT product_family), count(*)
FROM public.material_units WHERE is_deleted = false AND product_family IS NOT NULL
UNION ALL
SELECT 'quality_events.severity', count(DISTINCT severity), count(*)
FROM public.quality_events WHERE is_deleted = false AND severity IS NOT NULL
UNION ALL
SELECT 'downtime_events.reason_code', count(DISTINCT reason_code), count(*)
FROM public.downtime_events WHERE is_deleted = false AND reason_code IS NOT NULL
UNION ALL
SELECT 'downtime_events.downtime_type', count(DISTINCT downtime_type), count(*)
FROM public.downtime_events WHERE is_deleted = false AND downtime_type IS NOT NULL
ORDER BY 1;
"@
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

Rule "HOW THIS IS USED"
Say "Three seed phenomena get authored from these numbers, not from expectation:"
Say "  a PASS  - a relationship the fleet genuinely carries, with a band set"
Say "            around the measured value rather than fitted to it"
Say "  a FAIL  - a natural predeclared expectation the data refutes"
Say "  a REFUSAL - a population that genuinely cannot support its claim, so the"
Say "            harness reports INSUFFICIENT instead of judging"
Say ""
Say "A band written around a measured value is still an expectation: the harness"
Say "re-measures it after every fleet change, and a band that stops holding is"
Say "the signal. What must not happen is a band invented to guarantee a PASS."

$outFolder = Join-Path $repoRoot "docs\m1\evidence"
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$ev = Join-Path $outFolder ("T-026_candidate_scan_" + $stamp + ".txt")
$clean = New-Object System.Text.StringBuilder
foreach ($ch in ($script:log -replace "`r`n", "`n").ToCharArray()) {
    if ([int]$ch -le 126 -or [int]$ch -eq 10) { [void]$clean.Append($ch) }
}
[System.IO.File]::WriteAllText($ev, ($clean.ToString() -replace "`n", "`r`n"),
    (New-Object System.Text.UTF8Encoding($false)))
Write-Host ""
Write-Host ("Evidence : " + $ev)
exit 0
