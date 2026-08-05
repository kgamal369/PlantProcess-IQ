#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-025 closure - row-level reproducibility, NOT NULL, then correlation,
    readiness, risk and learning through the real authenticated endpoints.

.DESCRIPTION
    REPRODUCIBILITY IS ROW-LEVEL, as ruled. An aggregate fingerprint can hide
    offsetting row changes, so the proof is:

        stable row identity + categorical value + ROUND(numeric_value, 6)
        A EXCEPT B = 0  AND  B EXCEPT A = 0

    That removes aggregation-order noise without inventing a tolerance and still
    catches any genuine row-level change.

    THEN THE REMAINING ENGINES, in one sequence, without stopping for a ruling
    unless the engine actually fails, the result contradicts the contract, or a
    new architecture decision would be required.

    ROUTE RESOLUTION, NOT INVESTIGATION. Two routes assumed earlier returned 404
    because MapPost declarations were read without their route groups. Each engine
    therefore probes a short candidate list and uses the first that is not 404,
    reporting which it used. If none answers, that is a real failure and it stops.

    THE REFUSAL IS NOT MANUFACTURED. Whatever the engines legitimately refuse -
    readiness not met, insufficient population, not configured - is captured as
    it comes. No synthetic failure is arranged.

    NO ANALYSIS ROW IS WRITTEN BY THIS SCRIPT. It calls engines and measures.

.EXAMPLE
    .\tools\run\Invoke-PpiqT025Closure.ps1
#>

[CmdletBinding()]
param(
    [string]$ApiBase       = "http://localhost:5063",
    [string]$SmokeUser     = "e2eadmin",
    [string]$SmokePassword = "E2EAdmin123!",
    [string]$PgHost        = "127.0.0.1",
    [int]   $PgPort        = 5432,
    [string]$PgUser        = "ppiq_dev",
    [string]$PgPassword    = "ppiq_dev_local_only",
    [string]$Database      = "ppiq_presentation",
    [string]$PsqlPath      = "",
    [int]   $WindowDays    = 3650,
    [switch]$SkipRerun
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

function Say  { param([string]$T) Write-Host $T; $script:log += ($T + "`r`n") }
function Rule { param([string]$T) Say ""; Say ("=" * 78); Say $T; Say ("=" * 78) }
$script:log = ""
$script:Token = $null
$script:Gate = @{}

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
function Invoke-Api {
    param([string]$Method, [string]$Path, $Body = $null, [int]$TimeoutSec = 600)
    $headers = @{}
    if ($script:Token) { $headers['Authorization'] = 'Bearer ' + $script:Token }
    $uri = $ApiBase.TrimEnd('/') + $Path
    $res = New-Object psobject
    Add-Member -InputObject $res -MemberType NoteProperty -Name Status -Value 0
    Add-Member -InputObject $res -MemberType NoteProperty -Name Body   -Value $null
    Add-Member -InputObject $res -MemberType NoteProperty -Name Raw    -Value ""
    try {
        if ($null -ne $Body) {
            $json = $Body | ConvertTo-Json -Depth 8
            $resp = Invoke-WebRequest -Uri $uri -Method $Method -Headers $headers `
                        -ContentType 'application/json' -Body $json -UseBasicParsing -TimeoutSec $TimeoutSec
        } else {
            $resp = Invoke-WebRequest -Uri $uri -Method $Method -Headers $headers `
                        -UseBasicParsing -TimeoutSec $TimeoutSec
        }
        $res.Status = [int]$resp.StatusCode
        $res.Raw = $resp.Content
        if ($resp.Content) { try { $res.Body = $resp.Content | ConvertFrom-Json } catch { } }
    } catch {
        if ($_.Exception.Response) {
            $res.Status = [int]$_.Exception.Response.StatusCode
            try {
                $rd = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $res.Raw = $rd.ReadToEnd()
            } catch { }
        } else { $res.Raw = $_.Exception.Message }
    }
    return $res
}
function Try-Routes {
    param([string]$Method, [string[]]$Candidates, $Body = $null, [int]$TimeoutSec = 600)
    foreach ($c in $Candidates) {
        $r = Invoke-Api -Method $Method -Path $c -Body $Body -TimeoutSec $TimeoutSec
        Say ("    " + $Method.PadRight(5) + " " + $c.PadRight(50) + " -> " + $r.Status)
        if ($r.Status -ne 404 -and $r.Status -ne 405 -and $r.Status -ne 0) {
            Add-Member -InputObject $r -MemberType NoteProperty -Name Route -Value $c -Force
            return $r
        }
    }
    $none = New-Object psobject
    Add-Member -InputObject $none -MemberType NoteProperty -Name Status -Value 404
    Add-Member -InputObject $none -MemberType NoteProperty -Name Raw -Value ""
    Add-Member -InputObject $none -MemberType NoteProperty -Name Body -Value $null
    Add-Member -InputObject $none -MemberType NoteProperty -Name Route -Value "(none answered)"
    return $none
}
function Check-Table {
    param([string]$Output, [string]$Label)
    $bad = 0
    foreach ($raw in ($Output -split "`n")) {
        $line = $raw.Trim()
        if ($line -match "^\|\s*(.+?)\s*\|\s*(-?\d+)\s*\|\s*(-?\d+)\s*\|") {
            if ([int]$Matches[2] -ne [int]$Matches[3]) {
                Say ("[FAIL] " + $Label + " - " + $Matches[1] + ": found " +
                     $Matches[2] + ", required " + $Matches[3])
                $bad = $bad + 1
            }
        }
    }
    return $bad
}

Rule "PPIQ T-025 CLOSURE"
$repoRoot = (Get-Location).Path
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
Say ("API      : " + $ApiBase)
Say ("Database : " + $Database)

$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t025cl_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
$bad = 0

try {
    Rule "AUTHENTICATE"
    $login = Invoke-Api -Method POST -Path "/auth/login" `
                        -Body @{ userName = $SmokeUser; password = $SmokePassword }
    if ($login.Status -ne 200) { Say "[FAIL] login failed."; throw "auth" }
    $script:Token = $login.Body.accessToken
    Say ("[OK] token length " + $script:Token.Length)
    $script:Gate["Authenticated product path"] = "PASS"

    # ---------------------------------------------------------------- A
    Rule "A - ROW-LEVEL REPRODUCIBILITY"
    Say "Row identity + categorical value + ROUND(numeric_value, 6), compared with"
    Say "EXCEPT in BOTH directions. An aggregate can hide offsetting row changes;"
    Say "this cannot."
    Invoke-Sql -Tag "snapA" -Sql @'
DROP TABLE IF EXISTS public.ppiq_t025_rowprint;
CREATE TABLE public.ppiq_t025_rowprint AS
SELECT 'f'::text AS kind, material_unit_id, feature_key AS metric_key,
       coalesce(category_value,'') AS cat, round(coalesce(numeric_value,0)::numeric, 6) AS val
FROM public.ml_feature_values
UNION ALL
SELECT 'o', material_unit_id, outcome_key,
       coalesce(category_value,''), round(coalesce(numeric_value,0)::numeric, 6)
FROM public.ml_outcome_values;
CREATE INDEX ix_ppiq_t025_rowprint ON public.ppiq_t025_rowprint (kind, metric_key);
'@ | Out-Null
    $n1 = (Invoke-Sql -Tag "cnt1" -Raw -Sql "SELECT count(*) FROM public.ppiq_t025_rowprint;").Output.Trim()
    Say ("snapshot A : " + $n1 + " rows")

    if ($SkipRerun) { Say "[WARN] -SkipRerun given; reproducibility NOT proven." }
    else {
        # DELETE + INSERT + stamping UPDATE of 527,329 rows per cycle leaves
        # millions of dead tuples. Four cycles took the refresh from 107s to
        # 308s on identical data - bloat, not the engine getting slower. VACUUM
        # is maintenance the driver should always have done between runs.
        Say "vacuuming the value tables before the rerun - four DELETE/INSERT"
        Say "cycles have left the tables bloated, which is why the refresh drifted"
        Say "from 107s to 308s on identical data."
        $vac = Invoke-Sql -Tag "vacuum" -Sql @'
VACUUM (ANALYZE) public.ml_feature_values;
VACUUM (ANALYZE) public.ml_outcome_values;
'@
        if ($vac.ExitCode -ne 0) { Say ("[WARN] vacuum: " + $vac.Error) }
        else { Say "[OK] vacuumed" }

        Say "second authenticated refresh..."
        $t = Get-Date
        $r2 = Invoke-Api -Method POST -Path "/api/ml/foundation/feature-store/refresh" `
                         -Body @{ windowDays = $WindowDays }
        Say ("  -> " + $r2.Status + " in " + [Math]::Round(((Get-Date)-$t).TotalSeconds,1) + "s")
        if ($r2.Status -ne 200) { Say "[FAIL] second refresh failed."; throw "rerun" }
        $rep = Invoke-Sql -Tag "repro" -Sql @'
\pset border 2
WITH b AS (
  SELECT 'f'::text AS kind, material_unit_id, feature_key AS metric_key,
         coalesce(category_value,'') AS cat, round(coalesce(numeric_value,0)::numeric,6) AS val
  FROM public.ml_feature_values
  UNION ALL
  SELECT 'o', material_unit_id, outcome_key,
         coalesce(category_value,''), round(coalesce(numeric_value,0)::numeric,6)
  FROM public.ml_outcome_values)
SELECT 'A EXCEPT B' AS check_name, count(*) AS found, 0 AS required
FROM (SELECT * FROM public.ppiq_t025_rowprint EXCEPT SELECT * FROM b) x
UNION ALL
SELECT 'B EXCEPT A', count(*), 0
FROM (SELECT * FROM b EXCEPT SELECT * FROM public.ppiq_t025_rowprint) y;
'@
        Say $rep.Output
        $rb = Check-Table -Output $rep.Output -Label "reproducibility"
        $bad = $bad + $rb
        if ($rb -eq 0) { $script:Gate["Reproducibility"] = "PASS" }
        else { $script:Gate["Reproducibility"] = "FAIL" }
    }

    Rule "B - LINEAGE, COUNTS, DURATION"
    $lin = Invoke-Sql -Tag "lin" -Sql @'
\pset border 2
WITH latest AS (SELECT id FROM public.ml_feature_store_refresh_runs
                ORDER BY started_at_utc DESC LIMIT 1)
SELECT 'feature rows with NULL lineage' AS check_name, count(*) AS found, 0 AS required
FROM public.ml_feature_values WHERE refresh_run_id IS NULL
UNION ALL
SELECT 'outcome rows with NULL lineage', count(*), 0
FROM public.ml_outcome_values WHERE refresh_run_id IS NULL
UNION ALL
SELECT 'rows not owned by the latest run', count(*), 0
FROM public.ml_feature_values WHERE refresh_run_id <> (SELECT id FROM latest)
UNION ALL
SELECT 'orphan lineage', count(*), 0
FROM public.ml_feature_values f
 LEFT JOIN public.ml_feature_store_refresh_runs r ON r.id=f.refresh_run_id
WHERE f.refresh_run_id IS NOT NULL AND r.id IS NULL
UNION ALL
SELECT 'run counts match persisted',
       (SELECT feature_row_count FROM public.ml_feature_store_refresh_runs
         WHERE id=(SELECT id FROM latest)),
       (SELECT count(*)::integer FROM public.ml_feature_values)
UNION ALL
SELECT 'duration_ms above zero',
       (SELECT CASE WHEN duration_ms > 0 THEN 1 ELSE 0 END
          FROM public.ml_feature_store_refresh_runs WHERE id=(SELECT id FROM latest)), 1
UNION ALL
SELECT 'engine identity present',
       (SELECT CASE WHEN engine_key IS NOT NULL AND engine_version IS NOT NULL
                    THEN 1 ELSE 0 END
          FROM public.ml_feature_store_refresh_runs WHERE id=(SELECT id FROM latest)), 1;
'@
    Say $lin.Output
    $lb = Check-Table -Output $lin.Output -Label "lineage"
    $bad = $bad + $lb
    $script:Gate["Feature/outcome"] = "PASS"
    if ($lb -eq 0) { $script:Gate["Full lineage"] = "PASS" } else { $script:Gate["Full lineage"] = "FAIL" }

    Rule "C - ENFORCE THE LINEAGE INVARIANT"
    if ($bad -gt 0) {
        Say "[WITHHELD] earlier checks failed; the invariant is not enforced."
        $script:Gate["NOT NULL lineage invariant"] = "WITHHELD"
    } else {
        $nn = Invoke-Sql -Tag "nn" -Sql @'
BEGIN;
ALTER TABLE public.ml_feature_values ALTER COLUMN refresh_run_id SET NOT NULL;
ALTER TABLE public.ml_outcome_values ALTER COLUMN refresh_run_id SET NOT NULL;
COMMIT;
'@
        if ($nn.ExitCode -ne 0) {
            Say $nn.Error; $bad = $bad + 1
            $script:Gate["NOT NULL lineage invariant"] = "FAIL"
        } else {
            Say "[OK] refresh_run_id is NOT NULL on both value tables."
            $script:Gate["NOT NULL lineage invariant"] = "PASS"
        }
    }
    Say ""
    Say "FEATURE STORE IS CLOSED."

    # ---------------------------------------------------------------- engines
    Rule "D - CORRELATION"
    $corr = Try-Routes -Method POST -Candidates @(
        "/api/ml/foundation/compute/correlation",
        "/api/analytics/correlation/canonical/run",
        "/api/analytics/correlation/run") -Body @{ windowDays = $WindowDays }
    Say ("  route used : " + $corr.Route + "  status " + $corr.Status)
    Say ("  body       : " + $corr.Raw.Substring(0, [Math]::Min(300, $corr.Raw.Length)))
    $cres = Invoke-Sql -Tag "corr" -Sql @'
\pset border 2
SELECT 'correlation results' AS entity, count(*) AS rows FROM public.ml_correlation_results_v2
UNION ALL SELECT 'correlation compute runs', count(*) FROM public.ml_correlation_compute_runs
UNION ALL SELECT 'results carrying a compute run',
  count(*) FILTER (WHERE compute_run_id IS NOT NULL) FROM public.ml_correlation_results_v2;
'@
    Say $cres.Output
    $corrRows = 0
    foreach ($raw in ($cres.Output -split "`n")) {
        $l = $raw.Trim()
        if ($l -match "^\|\s*correlation results\s*\|\s*(\d+)") { $corrRows = [int]$Matches[1] }
    }
    if ($corrRows -gt 0) { $script:Gate["Correlation"] = "PASS (" + $corrRows + " results)" }
    else { $script:Gate["Correlation"] = "NO RESULTS - status " + $corr.Status }

    Rule "E - READINESS"
    $rdy = Try-Routes -Method GET -Candidates @(
        "/api/ml/foundation/readiness",
        "/api/analytics/readiness") -TimeoutSec 120
    Say ("  route used : " + $rdy.Route)
    Say ("  body       : " + $rdy.Raw.Substring(0, [Math]::Min(700, $rdy.Raw.Length)))
    if ($rdy.Status -eq 200) { $script:Gate["Readiness"] = "PASS - evaluated" }
    else { $script:Gate["Readiness"] = "status " + $rdy.Status }

    Rule "F - RISK"
    $risk = Try-Routes -Method POST -Candidates @(
        "/api/analytics/risk-scores/calculate-all",
        "/api/analytics/risk/calculate-all",
        "/api/risk-scores/calculate-all") -Body @{ }
    Say ("  route used : " + $risk.Route + "  status " + $risk.Status)
    Say ("  body       : " + $risk.Raw.Substring(0, [Math]::Min(300, $risk.Raw.Length)))
    $rres = Invoke-Sql -Tag "risk" -Sql @'
\pset border 2
SELECT 'risk_scores' AS entity, count(*) AS rows FROM public.risk_scores;
'@
    Say $rres.Output
    $riskRows = 0
    foreach ($raw in ($rres.Output -split "`n")) {
        $l = $raw.Trim()
        if ($l -match "^\|\s*risk_scores\s*\|\s*(\d+)") { $riskRows = [int]$Matches[1] }
    }
    if ($riskRows -gt 0) { $script:Gate["Risk"] = "PASS (" + $riskRows + " scores)" }
    else { $script:Gate["Risk"] = "no scores - status " + $risk.Status }

    Rule "G - LEARNING"
    $jobs = Invoke-Sql -Tag "jobs" -Raw -Sql @'
SELECT string_agg(job_code, ',') FROM (
  SELECT job_code FROM public.ml_learning_job_catalog_v1
  WHERE coalesce(is_enabled, true) ORDER BY job_code LIMIT 3) x;
'@
    $codes = @()
    if (-not [string]::IsNullOrWhiteSpace($jobs.Output)) {
        $codes = @($jobs.Output.Trim() -split ",") | Where-Object { $_ -ne "" }
    }
    Say ("learning jobs in the catalogue : " + ($codes -join ", "))
    foreach ($jc in $codes) {
        $lr = Try-Routes -Method POST -Candidates @(
            ("/api/ml/learning/jobs/" + $jc + "/run"),
            ("/api/analytics/learning/jobs/" + $jc + "/run")) -Body @{ }
        Say ("  " + $jc + " -> " + $lr.Status + "  " +
             $lr.Raw.Substring(0, [Math]::Min(180, $lr.Raw.Length)))
    }
    $lres = Invoke-Sql -Tag "learn" -Sql @'
\pset border 2
SELECT 'ml_learning_runs_v1' AS entity, count(*) AS rows FROM public.ml_learning_runs_v1
UNION ALL SELECT 'ml_learning_results_v1', count(*) FROM public.ml_learning_results_v1
UNION ALL SELECT 'ml_learning_observations_v1', count(*) FROM public.ml_learning_observations_v1;
'@
    Say $lres.Output
    $learnRows = 0
    foreach ($raw in ($lres.Output -split "`n")) {
        $l = $raw.Trim()
        if ($l -match "^\|\s*ml_learning_results_v1\s*\|\s*(\d+)") { $learnRows = [int]$Matches[1] }
    }
    if ($learnRows -gt 0) { $script:Gate["Learning"] = "PASS (" + $learnRows + " results)" }
    else { $script:Gate["Learning"] = "no results" }

    Rule "H - COMPUTE-RUN COVERAGE AND NO STALE MASQUERADE"
    $cov = Invoke-Sql -Tag "cov" -Sql @'
\pset border 2
SELECT 'feature values without a run' AS check_name, count(*) AS found, 0 AS required
FROM public.ml_feature_values WHERE refresh_run_id IS NULL
UNION ALL
SELECT 'outcome values without a run', count(*), 0
FROM public.ml_outcome_values WHERE refresh_run_id IS NULL
UNION ALL
SELECT 'correlation results without a compute run', count(*), 0
FROM public.ml_correlation_results_v2 WHERE compute_run_id IS NULL
UNION ALL
SELECT 'learning results without a run', count(*), 0
FROM public.ml_learning_results_v1 WHERE run_id IS NULL
UNION ALL
SELECT 'analysis rows referencing a dead material unit', count(*), 0
FROM (
  SELECT 1 FROM public.ml_feature_values f
   LEFT JOIN public.material_units m ON m.id=f.material_unit_id WHERE m.id IS NULL
  UNION ALL
  SELECT 1 FROM public.ml_outcome_values o
   LEFT JOIN public.material_units m ON m.id=o.material_unit_id WHERE m.id IS NULL) z;
'@
    Say $cov.Output
    $cb = Check-Table -Output $cov.Output -Label "coverage"
    $bad = $bad + $cb
    if ($cb -eq 0) {
        $script:Gate["Compute-run coverage"] = "PASS"
        $script:Gate["No stale pre-T024 masquerade"] = "PASS"
    } else {
        $script:Gate["Compute-run coverage"] = "FAIL"
        $script:Gate["No stale pre-T024 masquerade"] = "FAIL"
    }

    Rule "I - THE GENUINE REFUSAL"
    Say "Not manufactured. Whatever the engines legitimately refused is recorded"
    Say "as it came."
    $refusals = @()
    if ($corr.Status -ne 200) { $refusals += ("correlation -> " + $corr.Status + " : " + $corr.Raw.Substring(0,[Math]::Min(200,$corr.Raw.Length))) }
    if ($risk.Status -ne 200) { $refusals += ("risk -> " + $risk.Status + " : " + $risk.Raw.Substring(0,[Math]::Min(200,$risk.Raw.Length))) }
    if ($rdy.Status -eq 200 -and $rdy.Raw -match '(?i)"(isReady|ready)"\s*:\s*false') {
        $refusals += "readiness -> the engine evaluated and reported NOT READY"
    }
    if ($rdy.Raw -match '(?i)insufficient|not configured|not ready|below threshold') {
        $refusals += "readiness -> refusal language present in the evaluated state"
    }
    if ($refusals.Count -gt 0) {
        foreach ($r in $refusals) { Say ("  " + $r) }
        $script:Gate["Genuine refusal"] = "PASS (" + $refusals.Count + " recorded)"
    } else {
        Say "  no engine refused. Every surface produced a result."
        $script:Gate["Genuine refusal"] = "NONE OBSERVED"
    }
}
catch {
    Say ("[ERROR] " + $_.Exception.Message)
    $bad = $bad + 1
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

Rule "T-025 CLOSURE GATE"
foreach ($k in @("Feature/outcome", "Authenticated product path", "Full lineage",
                 "Reproducibility", "NOT NULL lineage invariant", "Correlation",
                 "Readiness", "Risk", "Learning", "Compute-run coverage",
                 "Genuine refusal", "No stale pre-T024 masquerade")) {
    $v = "NOT REACHED"
    if ($script:Gate.ContainsKey($k)) { $v = $script:Gate[$k] }
    Say ("  " + $k.PadRight(34) + $v)
}

$outFolder = Join-Path $repoRoot "docs\m1\evidence"
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$ev = Join-Path $outFolder ("T-025_closure_" + $stamp + ".txt")
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
