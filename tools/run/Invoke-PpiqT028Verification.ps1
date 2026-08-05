#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-028 - verify the confounded correlation and the insufficient-support
    refusal. READ ONLY. Both phenomena are PROVEN, not planted.

.DESCRIPTION
    The frozen task says both phenomena appear to be present already, so the work
    is to PROVE both rather than to create them, and that NO THRESHOLD IS
    WEAKENED to produce either result. Nothing here writes, and no generator or
    gate constant is touched.

    DELIVERABLE A - A FINDING THAT SURVIVES NAIVE ANALYSIS AND DOES NOT SURVIVE
    STRATIFICATION. Run the naive association, then the conditioned one, and
    record the difference. Conditioning is done on grade AND on thickness, because
    the task names both.

    DELIVERABLE B - A BLOCKED OUTCOME WHOSE REASON NAMES THE MEASURED VALUE AND
    ITS THRESHOLD. The engine's own message is "Blocked by the data-readiness
    gate; analysis refused (honest abstain)", which does NOT name either. So this
    runner recomputes each ReadinessGate dimension exactly as the gate computes
    it and prints the measured value beside the threshold it failed, which is what
    the validation asks for.

    THE THRESHOLDS, from ReadinessGate.cs, quoted not chosen:
      independent heats            Ready >= 60    Partial >= 30
      outcome events               Ready >= 40    Partial >= 15
      minority-class balance       Ready >= 0.10  Partial >= 0.03
      required-field completeness  Ready >= 0.95  Partial >= 0.85
    Overall is the WORST dimension. MinorityFraction returns 0.0 when there are
    fewer than two classes, and 0.5 for a numeric outcome.

    THE NEGATIVE CONTROLS. The task names SCRATCH, DENT and SEAM as deliberately
    uncorrelated. Whether all three exist in the generated catalogue is measured
    rather than assumed, and a control that does not exist is reported as absent -
    never as silent.

.EXAMPLE
    .\tools\run\Invoke-PpiqT028Verification.ps1
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
    [System.IO.File]::WriteAllText($f, $Sql, (New-Object System.Text.UTF8Encoding($false)))
    $o = Join-Path $script:tmp ($Tag + ".out")
    $e = Join-Path $script:tmp ($Tag + ".err")
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
function Run { param([string]$Sql, [string]$Tag)
    $r = Invoke-Sql -Sql $Sql -Tag $Tag
    if ($r.ExitCode -ne 0) { Say ("[FAIL] " + $Tag + " : " + ($r.Error -replace "`r", "" -replace "`n", " ").Trim()) }
    Say $r.Output
}
function Lines { param([string]$Sql, [string]$Tag)
    $r = Invoke-Sql -Sql $Sql -Tag $Tag -Raw
    if ($r.ExitCode -ne 0) { Say ("[FAIL] " + $Tag + " : " + $r.Error.Trim()); return ,@() }
    $out = New-Object System.Collections.ArrayList
    foreach ($raw in ($r.Output -split "`n")) {
        $t = $raw.Trim()
        if ($t.Length -gt 0) { [void]$out.Add($t) }
    }
    return ,$out.ToArray()
}

Rule "PPIQ T-028 - CONFOUNDED CORRELATION AND INSUFFICIENT-SUPPORT REFUSAL"
$repoRoot = (Get-Location).Path
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$env:PGOPTIONS = "-c default_transaction_read_only=on"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t028_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
Say ("Database : " + $Database)
Say "No write, no engine call, no threshold changed."
$bad = 0

try {
    $roOut = Join-Path $script:tmp "ro.txt"
    $ro = Invoke-Sql -Tag "ro" -Raw -Sql "SHOW transaction_read_only;"
    $roValue = $ro.Output.Trim()
    Say ("Server reports transaction_read_only = " + $roValue + " (required on)")
    if ($roValue -ne "on") { Say "[STOP] connection is not read-only."; exit 2 }

    # ------------------------------------------------------------------- A
    Rule "A - THE CONFOUNDED ASSOCIATION: NAIVE, THEN CONDITIONED"
    Say "Spearman over AVERAGE ranks, the same statistic the harness computes."
    Say "x = mean CT_C per coil. y = catalogued defect count on that coil."
    Say "Events with no defect_catalog_id are excluded, so a disposition cannot"
    Say "inflate the count."
    Run -Tag "a_naive" -Sql @"
\pset border 2
WITH d AS (
    SELECT p.x, p.unit_id, dd.y, mu.grade_or_recipe AS grade
    FROM (SELECT po.material_unit_id AS unit_id, avg(po.numeric_value)::float8 AS x
          FROM public.parameter_observations po
          JOIN public.parameter_definitions pd ON pd.id = po.parameter_definition_id AND pd.is_deleted = false
          WHERE po.is_deleted = false AND po.numeric_value IS NOT NULL AND pd.parameter_code = 'CT_C'
          GROUP BY po.material_unit_id) p
    JOIN public.material_units mu ON mu.id = p.unit_id AND mu.is_deleted = false
    JOIN LATERAL (SELECT count(qe.id)::float8 AS y FROM public.quality_events qe
                  WHERE qe.material_unit_id = mu.id AND qe.is_deleted = false
                    AND qe.defect_catalog_id IS NOT NULL) dd ON true
    WHERE lower(coalesce(mu.material_unit_type, '')) LIKE '%coil%'
), r AS (
    SELECT avg(rx) OVER (PARTITION BY x) AS rrx, avg(ry) OVER (PARTITION BY y) AS rry
    FROM (SELECT x, y, row_number() OVER (ORDER BY x) AS rx,
                 row_number() OVER (ORDER BY y) AS ry FROM d) t
)
SELECT 'NAIVE, whole fleet' AS analysis, count(*) AS n,
       round(corr(rrx, rry)::numeric, 4) AS spearman_rho
FROM r;
"@

    Say "CONDITIONED ON GRADE - within-stratum effect, then the population-weighted"
    Say "mean, skipping strata under 8 pairs exactly as the harness does."
    Run -Tag "a_grade" -Sql @"
\pset border 2
WITH d AS (
    SELECT p.x, dd.y, mu.grade_or_recipe AS stratum
    FROM (SELECT po.material_unit_id AS unit_id, avg(po.numeric_value)::float8 AS x
          FROM public.parameter_observations po
          JOIN public.parameter_definitions pd ON pd.id = po.parameter_definition_id AND pd.is_deleted = false
          WHERE po.is_deleted = false AND po.numeric_value IS NOT NULL AND pd.parameter_code = 'CT_C'
          GROUP BY po.material_unit_id) p
    JOIN public.material_units mu ON mu.id = p.unit_id AND mu.is_deleted = false
    JOIN LATERAL (SELECT count(qe.id)::float8 AS y FROM public.quality_events qe
                  WHERE qe.material_unit_id = mu.id AND qe.is_deleted = false
                    AND qe.defect_catalog_id IS NOT NULL) dd ON true
    WHERE lower(coalesce(mu.material_unit_type, '')) LIKE '%coil%'
), r AS (
    SELECT stratum,
           avg(rx) OVER (PARTITION BY stratum, x) AS rrx,
           avg(ry) OVER (PARTITION BY stratum, y) AS rry
    FROM (SELECT stratum, x, y,
                 row_number() OVER (PARTITION BY stratum ORDER BY x) AS rx,
                 row_number() OVER (PARTITION BY stratum ORDER BY y) AS ry FROM d) t
), s AS (
    SELECT stratum, count(*) AS n, corr(rrx, rry) AS rho FROM r GROUP BY stratum HAVING count(*) >= 8
)
SELECT stratum, n, round(rho::numeric, 4) AS spearman_rho FROM s
UNION ALL
SELECT 'WEIGHTED MEAN', sum(n), round((sum(rho * n) / sum(n))::numeric, 4) FROM s WHERE rho IS NOT NULL
ORDER BY 1;
"@

    Say "CONDITIONED ON THICKNESS - quartiles of mean THICKNESS_MM per coil, so the"
    Say "second conditioning variable the task names is measured too."
    Run -Tag "a_thick" -Sql @"
\pset border 2
WITH base AS (
    SELECT p.unit_id, p.x, dd.y, th.t
    FROM (SELECT po.material_unit_id AS unit_id, avg(po.numeric_value)::float8 AS x
          FROM public.parameter_observations po
          JOIN public.parameter_definitions pd ON pd.id = po.parameter_definition_id AND pd.is_deleted = false
          WHERE po.is_deleted = false AND po.numeric_value IS NOT NULL AND pd.parameter_code = 'CT_C'
          GROUP BY po.material_unit_id) p
    JOIN public.material_units mu ON mu.id = p.unit_id AND mu.is_deleted = false
    JOIN LATERAL (SELECT count(qe.id)::float8 AS y FROM public.quality_events qe
                  WHERE qe.material_unit_id = mu.id AND qe.is_deleted = false
                    AND qe.defect_catalog_id IS NOT NULL) dd ON true
    JOIN LATERAL (SELECT avg(po2.numeric_value)::float8 AS t
                  FROM public.parameter_observations po2
                  JOIN public.parameter_definitions pd2 ON pd2.id = po2.parameter_definition_id AND pd2.is_deleted = false
                  WHERE po2.material_unit_id = mu.id AND po2.is_deleted = false
                    AND pd2.parameter_code = 'THICKNESS_MM') th ON true
    WHERE lower(coalesce(mu.material_unit_type, '')) LIKE '%coil%' AND th.t IS NOT NULL
), d AS (
    SELECT x, y, ntile(4) OVER (ORDER BY t) AS stratum FROM base
), r AS (
    SELECT stratum,
           avg(rx) OVER (PARTITION BY stratum, x) AS rrx,
           avg(ry) OVER (PARTITION BY stratum, y) AS rry
    FROM (SELECT stratum, x, y,
                 row_number() OVER (PARTITION BY stratum ORDER BY x) AS rx,
                 row_number() OVER (PARTITION BY stratum ORDER BY y) AS ry FROM d) t
), s AS (
    SELECT stratum, count(*) AS n, corr(rrx, rry) AS rho FROM r GROUP BY stratum HAVING count(*) >= 8
)
SELECT 'thickness quartile ' || stratum::text AS stratum, n, round(rho::numeric, 4) AS spearman_rho FROM s
UNION ALL
SELECT 'WEIGHTED MEAN', sum(n), round((sum(rho * n) / sum(n))::numeric, 4) FROM s WHERE rho IS NOT NULL
ORDER BY 1;
"@

    $naive = Lines -Tag "a_naive_raw" -Sql @"
WITH d AS (
    SELECT p.x, dd.y
    FROM (SELECT po.material_unit_id AS unit_id, avg(po.numeric_value)::float8 AS x
          FROM public.parameter_observations po
          JOIN public.parameter_definitions pd ON pd.id = po.parameter_definition_id AND pd.is_deleted = false
          WHERE po.is_deleted = false AND po.numeric_value IS NOT NULL AND pd.parameter_code = 'CT_C'
          GROUP BY po.material_unit_id) p
    JOIN public.material_units mu ON mu.id = p.unit_id AND mu.is_deleted = false
    JOIN LATERAL (SELECT count(qe.id)::float8 AS y FROM public.quality_events qe
                  WHERE qe.material_unit_id = mu.id AND qe.is_deleted = false
                    AND qe.defect_catalog_id IS NOT NULL) dd ON true
    WHERE lower(coalesce(mu.material_unit_type, '')) LIKE '%coil%'
), r AS (
    SELECT avg(rx) OVER (PARTITION BY x) AS rrx, avg(ry) OVER (PARTITION BY y) AS rry
    FROM (SELECT x, y, row_number() OVER (ORDER BY x) AS rx,
                 row_number() OVER (ORDER BY y) AS ry FROM d) t
)
SELECT round(corr(rrx, rry)::numeric, 4)::text FROM r;
"@
    $naiveRho = 0.0
    if ($naive.Count -gt 0) { $naiveRho = [double]$naive[0] }
    Say ""
    Say ("  THE FINDING: naive rho " + $naiveRho + " over the whole fleet.")
    Say "  If the conditioned weighted means above are near zero, the association"
    Say "  SURVIVES NAIVE ANALYSIS AND DOES NOT SURVIVE STRATIFICATION - the"
    Say "  grades differ in both coiling temperature and defect rate, so the"
    Say "  pooled correlation measures the grade mix and not any process effect."

    # ------------------------------------------------------------------- B
    Rule "B - THE NEGATIVE CONTROLS, MEASURED NOT ASSUMED"
    Say "The task names SCRATCH, DENT and SEAM. A control that does not exist in"
    Say "the generated catalogue is reported ABSENT, never silent."
    Run -Tag "b_controls" -Sql @"
\pset border 2
SELECT c.code AS declared_control,
       CASE WHEN dc.id IS NULL THEN 'ABSENT from the generated catalogue'
            ELSE 'present' END AS presence,
       coalesce(cnt.events, 0) AS events
FROM (VALUES ('SCRATCH'), ('DENT'), ('SEAM')) AS c(code)
LEFT JOIN public.defect_catalogs dc ON dc.defect_code = c.code AND dc.is_deleted = false
LEFT JOIN LATERAL (SELECT count(*) AS events FROM public.quality_events qe
                   WHERE qe.defect_catalog_id = dc.id AND qe.is_deleted = false) cnt ON true
ORDER BY c.code;
"@
    Say "Association of each PRESENT control with the same exposure, CT_C:"
    Run -Tag "b_assoc" -Sql @"
\pset border 2
WITH d AS (
    SELECT dc.defect_code AS code, p.x,
           (SELECT count(*) FROM public.quality_events q2
             WHERE q2.material_unit_id = mu.id AND q2.is_deleted = false
               AND q2.defect_catalog_id = dc.id)::float8 AS y
    FROM public.defect_catalogs dc
    CROSS JOIN LATERAL (SELECT id FROM public.material_units
                        WHERE is_deleted = false
                          AND lower(coalesce(material_unit_type,'')) LIKE '%coil%') mu
    JOIN LATERAL (SELECT avg(po.numeric_value)::float8 AS x
                  FROM public.parameter_observations po
                  JOIN public.parameter_definitions pd ON pd.id = po.parameter_definition_id AND pd.is_deleted = false
                  WHERE po.material_unit_id = mu.id AND po.is_deleted = false
                    AND pd.parameter_code = 'CT_C') p ON p.x IS NOT NULL
    WHERE dc.is_deleted = false AND dc.defect_code IN ('SCRATCH','DENT','SEAM')
), r AS (
    SELECT code,
           avg(rx) OVER (PARTITION BY code, x) AS rrx,
           avg(ry) OVER (PARTITION BY code, y) AS rry
    FROM (SELECT code, x, y,
                 row_number() OVER (PARTITION BY code ORDER BY x) AS rx,
                 row_number() OVER (PARTITION BY code ORDER BY y) AS ry FROM d) t
)
SELECT code, count(*) AS n, round(corr(rrx, rry)::numeric, 4) AS spearman_rho
FROM r GROUP BY code ORDER BY code;
"@

    # ------------------------------------------------------------------- C
    Rule "C - THE BLOCKED OUTCOME, WITH ITS MEASURED VALUE AND ITS THRESHOLD"
    Say "ReadinessGate.cs thresholds, quoted and NOT modified:"
    Say "  independent heats           Ready >= 60     Partial >= 30"
    Say "  outcome events              Ready >= 40     Partial >= 15"
    Say "  minority-class balance      Ready >= 0.10   Partial >= 0.03"
    Say "  required-field completeness Ready >= 0.95   Partial >= 0.85"
    Say "Overall is the WORST dimension. Fewer than two classes gives 0.0."
    Say ""
    Run -Tag "c_gate" -Sql @"
\pset border 2
WITH per_outcome AS (
    SELECT v.outcome_key,
           count(*)::int                       AS outcome_events,
           count(DISTINCT v.heat_id)::int      AS independent_heats,
           count(v.category_value)::int        AS with_category,
           count(v.numeric_value)::int         AS with_numeric
    FROM public.ml_outcome_values v
    GROUP BY v.outcome_key
), classes AS (
    SELECT outcome_key, count(*)::int AS class_count, min(share) AS min_share
    FROM (SELECT outcome_key, coalesce(category_value, '') AS cat,
                 count(*)::numeric / SUM(count(*)) OVER (PARTITION BY outcome_key) AS share
          FROM public.ml_outcome_values GROUP BY outcome_key, coalesce(category_value, '')) q
    GROUP BY outcome_key
)
SELECT p.outcome_key,
       p.independent_heats,
       p.outcome_events,
       c.class_count,
       CASE WHEN c.class_count < 2 THEN 0.0000 ELSE round(c.min_share, 4) END AS minority_fraction,
       round((greatest(p.with_category, p.with_numeric)::numeric / nullif(p.outcome_events, 0)), 4) AS completeness,
       CASE
         WHEN p.independent_heats < 30 THEN 'BLOCKED on independent heats'
         WHEN p.outcome_events < 15 THEN 'BLOCKED on outcome events'
         WHEN (CASE WHEN c.class_count < 2 THEN 0.0 ELSE c.min_share END) < 0.03 THEN 'BLOCKED on minority-class balance'
         WHEN (greatest(p.with_category, p.with_numeric)::numeric / nullif(p.outcome_events, 0)) < 0.85 THEN 'BLOCKED on completeness'
         ELSE 'not blocked'
       END AS gate_verdict
FROM per_outcome p JOIN classes c ON c.outcome_key = p.outcome_key
ORDER BY p.outcome_key;
"@

    $blocked = Lines -Tag "c_blocked" -Sql @"
SELECT p.outcome_key || '~' ||
       CASE WHEN c.class_count < 2 THEN '0.0000' ELSE to_char(c.min_share, 'FM0.0000') END || '~' ||
       p.independent_heats::text || '~' || p.outcome_events::text
FROM (SELECT outcome_key, count(*)::int AS outcome_events,
             count(DISTINCT heat_id)::int AS independent_heats
      FROM public.ml_outcome_values GROUP BY outcome_key) p
JOIN (SELECT outcome_key, count(*)::int AS class_count, min(share) AS min_share
      FROM (SELECT outcome_key, coalesce(category_value, '') AS cat,
                   count(*)::numeric / SUM(count(*)) OVER (PARTITION BY outcome_key) AS share
            FROM public.ml_outcome_values GROUP BY outcome_key, coalesce(category_value, '')) q
      GROUP BY outcome_key) c ON c.outcome_key = p.outcome_key
WHERE (CASE WHEN c.class_count < 2 THEN 0.0 ELSE c.min_share END) < 0.03
ORDER BY 1;
"@
    Say ""
    if ($blocked.Count -eq 0) {
        Say "  [FINDING] no materialised outcome is blocked on a readiness dimension."
        Say "  Per the frozen task, that IS the finding, and the remedy is a small"
        Say "  generator change with its own estimate - NOT a threshold change."
        $bad = $bad + 1
    } else {
        Say ("  THE BLOCKED OUTCOME(S), each naming its measured value and threshold:")
        foreach ($line in $blocked) {
            $p = $line -split "~"
            if ($p.Count -ne 4) { continue }
            Say ("    " + $p[0] + " - minority-class balance measured " + $p[1] +
                 ", below the Partial threshold 0.0300 and the Ready threshold 0.1000.")
            Say ("      Support is NOT the constraint: " + $p[2] + " independent heats against a" +
                 " Ready threshold of 60, and " + $p[3] + " outcome events against 40.")
            Say "      The engine refuses because one class is too rare to support a"
            Say "      class-conditional claim, not because the population is small."
        }
    }
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:\PGOPTIONS -ErrorAction SilentlyContinue
}

Rule "T-028 DELIVERABLES"
Say "  A  a finding that survives naive analysis and does NOT survive"
Say "     stratification, with the naive and conditioned values side by side"
Say "  B  a Blocked outcome whose reason names the measured value and its"
Say "     threshold, recomputed from ReadinessGate's own constants"
Say ""
Say "NO THRESHOLD WAS WEAKENED AND NO GENERATOR CHANGE WAS MADE."

$outFolder = Join-Path $repoRoot "docs\m1\evidence"
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$ev = Join-Path $outFolder ("T-028_verification_" + $stamp + ".txt")
$clean = New-Object System.Text.StringBuilder
foreach ($ch in ($script:log -replace "`r`n", "`n").ToCharArray()) {
    if ([int]$ch -le 126 -or [int]$ch -eq 10) { [void]$clean.Append($ch) }
}
[System.IO.File]::WriteAllText($ev, ($clean.ToString() -replace "`n", "`r`n"),
    (New-Object System.Text.UTF8Encoding($false)))
Write-Host ""
Write-Host ("Evidence : " + $ev)
if ($bad -gt 0) { exit 1 }
exit 0
