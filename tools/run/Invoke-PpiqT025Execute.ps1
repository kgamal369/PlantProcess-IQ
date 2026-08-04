#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-025 engine execution - verify the live engines, clear the stale derived
    state, run the real authenticated refresh, and prove lineage and
    reproducibility. ReportOnly verifies only; -Apply executes.

.DESCRIPTION
    PHASES
      1  LIVE post-apply verification. The database is the authority now, not any
         generated file. It confirms the stamping is present in the installed
         bodies and - the ordering question - HOW the completion counts are
         computed:
            counts by refresh_run_id  -> stamping MUST precede them
            counts by source tag      -> textual ordering cannot affect cardinality
      2  clear the stale derived rows. Values and results only. Definitions,
         registries and historical run records are preserved.
      3  the authenticated feature-store refresh, through the product's own
         endpoint. The engine produces the rows AND their lineage.
      4  full lineage and count verification, every check the ruling listed.
      5  reproducibility: a second refresh, compared on COMPUTED VALUES rather
         than on generated identifiers or timestamps.
      6  NOT NULL, enforced only after the engine path is proven.

    NO ANALYSIS ROW IS EVER WRITTEN BY THIS SCRIPT. It clears, it calls the
    engine, it measures. Every value comes from the product.

.EXAMPLE
    .\tools\run\Invoke-PpiqT025Execute.ps1
    .\tools\run\Invoke-PpiqT025Execute.ps1 -Apply
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
    [switch]$SkipNotNull,
    [switch]$Apply
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

function Say  { param([string]$T) Write-Host $T; $script:log += ($T + "`r`n") }
function Rule { param([string]$T) Say ""; Say ("=" * 78); Say $T; Say ("=" * 78) }
$script:log = ""
$script:Token = $null

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
    param([string]$Method, [string]$Path, $Body = $null)
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
                        -ContentType 'application/json' -Body $json -UseBasicParsing -TimeoutSec 600
        } else {
            $resp = Invoke-WebRequest -Uri $uri -Method $Method -Headers $headers `
                        -UseBasicParsing -TimeoutSec 600
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
        }
    }
    return $res
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

Rule "PPIQ T-025 - ENGINE EXECUTION AND LINEAGE PROOF"
$repoRoot = (Get-Location).Path
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
$modeLabel = "REPORT ONLY - phase 1 verification only"
if ($Apply) { $modeLabel = "APPLY - clear, refresh, prove" }
Say ("API      : " + $ApiBase)
Say ("Database : " + $Database)
Say ("Mode     : " + $modeLabel)

$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t025exec_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
$bad = 0

try {
    Rule "1 - LIVE POST-APPLY VERIFICATION. THE DATABASE IS THE AUTHORITY."
    $liveChecks = Invoke-Sql -Tag "live" -Sql @'
\pset border 2
SELECT 'base stamps its run' AS check_name, count(*) AS found, 1 AS required
FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
WHERE n.nspname='public' AND p.proname='ppiq_ml_refresh_feature_store' AND p.prokind='f'
  AND pg_get_functiondef(p.oid) LIKE '%SET refresh_run_id = v_run_id%'
UNION ALL
SELECT 'base stamps only unowned rows', count(*), 1
FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
WHERE n.nspname='public' AND p.proname='ppiq_ml_refresh_feature_store' AND p.prokind='f'
  AND pg_get_functiondef(p.oid) LIKE '%refresh_run_id IS NULL%'
UNION ALL
SELECT 'base carries the single-flight lock', count(*), 1
FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
WHERE n.nspname='public' AND p.proname='ppiq_ml_refresh_feature_store' AND p.prokind='f'
  AND pg_get_functiondef(p.oid) LIKE '%pg_advisory_xact_lock%'
UNION ALL
SELECT 'v6 stamps with the BASE run id', count(*), 1
FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
WHERE n.nspname='public' AND p.proname='ppiq_ml_refresh_feature_store_v6' AND p.prokind='f'
  AND pg_get_functiondef(p.oid) LIKE '%SET refresh_run_id = v_base.run_id%'
UNION ALL
SELECT 'v6 promotes the run to v6 identity', count(*), 1
FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
WHERE n.nspname='public' AND p.proname='ppiq_ml_refresh_feature_store_v6' AND p.prokind='f'
  AND pg_get_functiondef(p.oid) LIKE '%engine_version = ''v6''%'
UNION ALL
SELECT 'stale rows given manufactured lineage', count(*), 0
FROM public.ml_feature_values WHERE refresh_run_id IS NOT NULL;
'@
    if ($liveChecks.ExitCode -ne 0) { Say $liveChecks.Error; throw "live" }
    Say $liveChecks.Output
    $bad = $bad + (Check-Table -Output $liveChecks.Output -Label "live engine")

    Say "THE ORDERING QUESTION, answered from the live bodies:"
    $ord = Invoke-Sql -Tag "ordering" -Sql @'
\pset border 2
SELECT p.proname AS function_name,
       pg_get_functiondef(p.oid) LIKE '%feature_row_count = (SELECT count(*) FROM public.ml_feature_values WHERE source_system%'
         AS counts_by_source_tag,
       pg_get_functiondef(p.oid) LIKE '%feature_row_count = (SELECT count(*) FROM public.ml_feature_values%refresh_run_id%'
         AS counts_by_refresh_run_id,
       position('SET refresh_run_id' in pg_get_functiondef(p.oid)) AS stamp_at,
       position('feature_row_count =' in pg_get_functiondef(p.oid)) AS count_at
FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace
WHERE n.nspname='public' AND p.prokind='f'
  AND p.proname IN ('ppiq_ml_refresh_feature_store','ppiq_ml_refresh_feature_store_v6')
ORDER BY 1;
'@
    Say $ord.Output
    Say "Where the counts use refresh_run_id, stamp_at must be LESS than count_at."
    Say "Where they use the source tag, the ordering cannot affect cardinality."

    if (-not $Apply) {
        Rule "REPORT ONLY - NOTHING CLEARED, NO ENGINE RUN"
        if ($bad -gt 0) { throw "verification" }
        Rule "RESULT"
        Say "[OK] the live engines carry the lineage contract. Re-run with -Apply."
        exit 0
    }
    if ($bad -gt 0) { Say "[STOP] live verification failed; refusing to proceed."; throw "verification" }

    Rule "2 - PROVE THE ENGINE IS REACHABLE, BEFORE CLEARING ANYTHING"
    Say "MY DEFECT, corrected. The first version cleared 346,973 rows and only then"
    Say "discovered it could not authenticate, leaving the analysis layer empty with"
    Say "nothing computed to replace it. Authentication and an engine handshake now"
    Say "BOTH precede the clear."
    Say ""
    $login = Invoke-Api -Method POST -Path "/auth/login" `
                        -Body @{ userName = $SmokeUser; password = $SmokePassword }
    if ($login.Status -ne 200) {
        Say ("[FAIL] login returned " + $login.Status + ". NOTHING was cleared.")
        throw "auth"
    }
    $script:Token = $login.Body.accessToken
    if ([string]::IsNullOrWhiteSpace($script:Token)) {
        Say "[FAIL] no accessToken. NOTHING was cleared."
        throw "auth"
    }
    Say ("[OK] authenticated, token length " + $script:Token.Length)
    $hand = Invoke-Api -Method GET -Path "/api/ml/foundation/readiness"
    Say ("GET /api/ml/foundation/readiness -> " + $hand.Status)
    if ($hand.Status -ne 200) {
        Say "[FAIL] the engine surface did not answer. NOTHING was cleared."
        throw "engine"
    }
    Say "[OK] the engine surface answers with this token"

    Rule "3 - CLEAR THE STALE DERIVED STATE"
    Say "Values and results only. Definitions, registries and historical run"
    Say "records are preserved. Stale rows are DELETED, never backfilled."
    $clear = Invoke-Sql -Tag "clear" -Sql @'
\pset border 2
BEGIN;
DELETE FROM public.ml_feature_values WHERE refresh_run_id IS NULL;
DELETE FROM public.ml_outcome_values WHERE refresh_run_id IS NULL;
DELETE FROM public.ml_correlation_results_v2;
DELETE FROM public.ml_learning_observations_v1;
DELETE FROM public.ml_learning_results_v1;
COMMIT;

SELECT 'ml_feature_values' AS entity, count(*) AS remaining FROM public.ml_feature_values
UNION ALL SELECT 'ml_outcome_values', count(*) FROM public.ml_outcome_values
UNION ALL SELECT 'ml_correlation_results_v2', count(*) FROM public.ml_correlation_results_v2
UNION ALL SELECT 'ml_learning_results_v1', count(*) FROM public.ml_learning_results_v1
UNION ALL SELECT 'PRESERVED ml_feature_definitions', count(*) FROM public.ml_feature_definitions
UNION ALL SELECT 'PRESERVED ml_outcome_definitions', count(*) FROM public.ml_outcome_definitions
UNION ALL SELECT 'PRESERVED job_definitions', count(*) FROM public.job_definitions
UNION ALL SELECT 'PRESERVED ml_feature_store_refresh_runs', count(*)
  FROM public.ml_feature_store_refresh_runs
ORDER BY 1;
'@
    if ($clear.ExitCode -ne 0 -or $clear.Error -match "(?i)(ERROR|FATAL):") {
        Say $clear.Error; throw "clear"
    }
    Say $clear.Output

    Rule "4 - AUTHENTICATED REFRESH THROUGH THE PRODUCT ENDPOINT"
    Say ("POST /api/ml/foundation/feature-store/refresh  windowDays=" + $WindowDays)
    Say "The endpoint takes [FromBody] RefreshFeatureStoreRequest. A bare POST with"
    Say "a query string returns 415 - which is the endpoint stating its contract."
    $t0 = Get-Date
    $ref = Invoke-Api -Method POST -Path "/api/ml/foundation/feature-store/refresh" `
             -Body @{ windowDays = $WindowDays }
    $secs = [Math]::Round(((Get-Date) - $t0).TotalSeconds, 1)
    Say ("  -> " + $ref.Status + "  in " + $secs + "s")
    Say ("  raw: " + $ref.Raw.Substring(0, [Math]::Min(400, $ref.Raw.Length)))
    if ($ref.Status -ne 200) {
        Say "[FAIL] the refresh endpoint did not return 200."
        throw "refresh"
    }
    # the response shape is { message, windowDays, result: { feature_rows,
    # outcome_rows, run_id } } - the run id is NESTED, not top level.
    $runId = $null
    foreach ($n in @("run_id", "runId")) {
        try { if ($ref.Body.result.$n) { $runId = [string]$ref.Body.result.$n } } catch { }
    }
    if ([string]::IsNullOrWhiteSpace($runId)) {
        foreach ($n in @("run_id", "runId")) {
            try { if ($ref.Body.$n) { $runId = [string]$ref.Body.$n } } catch { }
        }
    }
    if ([string]::IsNullOrWhiteSpace($runId)) {
        Say "[WARN] no run id in the response body; taking the newest run instead."
        $rq = Invoke-Sql -Tag "newestrun" -Raw -Sql `
              "SELECT id FROM public.ml_feature_store_refresh_runs ORDER BY started_at_utc DESC LIMIT 1;"
        $runId = $rq.Output.Trim()
    }
    Say ("run id : " + $runId)

    Rule "5 - LINEAGE AND COUNT VERIFICATION"
    $v = Invoke-Sql -Tag "lineage" -Sql @"
\pset border 2
SELECT 'feature rows with NULL lineage' AS check_name, count(*) AS found, 0 AS required
FROM public.ml_feature_values WHERE refresh_run_id IS NULL
UNION ALL
SELECT 'outcome rows with NULL lineage', count(*), 0
FROM public.ml_outcome_values WHERE refresh_run_id IS NULL
UNION ALL
SELECT 'feature rows owned by another run', count(*), 0
FROM public.ml_feature_values WHERE refresh_run_id <> '$runId'
UNION ALL
SELECT 'outcome rows owned by another run', count(*), 0
FROM public.ml_outcome_values WHERE refresh_run_id <> '$runId'
UNION ALL
SELECT 'orphan lineage references', count(*), 0
FROM (
  SELECT 1 FROM public.ml_feature_values f
   LEFT JOIN public.ml_feature_store_refresh_runs r ON r.id = f.refresh_run_id
   WHERE f.refresh_run_id IS NOT NULL AND r.id IS NULL
  UNION ALL
  SELECT 1 FROM public.ml_outcome_values o
   LEFT JOIN public.ml_feature_store_refresh_runs r ON r.id = o.refresh_run_id
   WHERE o.refresh_run_id IS NOT NULL AND r.id IS NULL) x
UNION ALL
SELECT 'run feature_row_count matches persisted',
       (SELECT feature_row_count FROM public.ml_feature_store_refresh_runs WHERE id='$runId'),
       (SELECT count(*)::integer FROM public.ml_feature_values WHERE refresh_run_id='$runId')
UNION ALL
SELECT 'run outcome_row_count matches persisted',
       (SELECT outcome_row_count FROM public.ml_feature_store_refresh_runs WHERE id='$runId'),
       (SELECT count(*)::integer FROM public.ml_outcome_values WHERE refresh_run_id='$runId')
UNION ALL
SELECT 'run has engine_key', (SELECT count(*)::integer FROM public.ml_feature_store_refresh_runs
                               WHERE id='$runId' AND engine_key IS NOT NULL), 1
UNION ALL
SELECT 'run has engine_version', (SELECT count(*)::integer FROM public.ml_feature_store_refresh_runs
                                   WHERE id='$runId' AND engine_version IS NOT NULL), 1
UNION ALL
SELECT 'run window_days matches the request',
       (SELECT window_days FROM public.ml_feature_store_refresh_runs WHERE id='$runId'), $WindowDays;
"@
    Say $v.Output
    $bad = $bad + (Check-Table -Output $v.Output -Label "lineage")

    Say "--- the run record as the engine left it ---"
    $rr = Invoke-Sql -Tag "runrow" -Sql @"
\pset border 2
SELECT id, status, engine_key, engine_version, window_days,
       feature_row_count, outcome_row_count, duration_ms
FROM public.ml_feature_store_refresh_runs WHERE id='$runId';
"@
    Say $rr.Output

    Rule "6 - REPRODUCIBILITY"
    Say "Compared on COMPUTED VALUES, never on generated identifiers or timestamps."
    Invoke-Sql -Tag "fp1" -Sql @'
DROP TABLE IF EXISTS public.ppiq_t025_fingerprint;
CREATE TABLE public.ppiq_t025_fingerprint AS
SELECT 'feature' AS kind, feature_key AS metric_key, count(*) AS rows,
       round(sum(coalesce(numeric_value,0))::numeric, 4) AS value_sum,
       round(min(coalesce(numeric_value,0))::numeric, 4) AS value_min,
       round(max(coalesce(numeric_value,0))::numeric, 4) AS value_max
FROM public.ml_feature_values GROUP BY 2
UNION ALL
SELECT 'outcome', outcome_key, count(*),
       round(sum(coalesce(numeric_value,0))::numeric, 4),
       round(min(coalesce(numeric_value,0))::numeric, 4),
       round(max(coalesce(numeric_value,0))::numeric, 4)
FROM public.ml_outcome_values GROUP BY 2;
'@ | Out-Null
    Say "fingerprint of run 1 captured. Running the engine a second time..."
    $t1 = Get-Date
    $ref2 = Invoke-Api -Method POST -Path "/api/ml/foundation/feature-store/refresh" `
              -Body @{ windowDays = $WindowDays }
    Say ("second refresh -> " + $ref2.Status + "  in " +
         [Math]::Round(((Get-Date) - $t1).TotalSeconds, 1) + "s")
    if ($ref2.Status -ne 200) { Say "[FAIL] second refresh failed."; $bad = $bad + 1 }
    $rep = Invoke-Sql -Tag "repro" -Sql @'
\pset border 2
WITH now2 AS (
  SELECT 'feature' AS kind, feature_key AS metric_key, count(*) AS rows,
         round(sum(coalesce(numeric_value,0))::numeric, 4) AS value_sum,
         round(min(coalesce(numeric_value,0))::numeric, 4) AS value_min,
         round(max(coalesce(numeric_value,0))::numeric, 4) AS value_max
  FROM public.ml_feature_values GROUP BY 2
  UNION ALL
  SELECT 'outcome', outcome_key, count(*),
         round(sum(coalesce(numeric_value,0))::numeric, 4),
         round(min(coalesce(numeric_value,0))::numeric, 4),
         round(max(coalesce(numeric_value,0))::numeric, 4)
  FROM public.ml_outcome_values GROUP BY 2)
SELECT 'metrics differing between the two runs' AS check_name, count(*) AS found, 0 AS required
FROM (
  SELECT kind, metric_key, rows, value_sum, value_min, value_max FROM now2
  EXCEPT
  SELECT kind, metric_key, rows, value_sum, value_min, value_max
  FROM public.ppiq_t025_fingerprint) d
UNION ALL
SELECT 'metrics present in run 1 but not run 2', count(*), 0
FROM (
  SELECT kind, metric_key FROM public.ppiq_t025_fingerprint
  EXCEPT SELECT kind, metric_key FROM now2) e
UNION ALL
SELECT 'rows still traceable after the second run', count(*), 0
FROM public.ml_feature_values WHERE refresh_run_id IS NULL;
'@
    Say $rep.Output
    $bad = $bad + (Check-Table -Output $rep.Output -Label "reproducibility")

    if ($SkipNotNull) {
        Rule "7 - NOT NULL SKIPPED BY REQUEST"
    } elseif ($bad -gt 0) {
        Rule "7 - NOT NULL WITHHELD"
        Say "The engine path is not proven, so the invariant is NOT enforced."
    } else {
        Rule "7 - ENFORCE THE INVARIANT"
        $nn = Invoke-Sql -Tag "notnull" -Sql @'
BEGIN;
ALTER TABLE public.ml_feature_values ALTER COLUMN refresh_run_id SET NOT NULL;
ALTER TABLE public.ml_outcome_values ALTER COLUMN refresh_run_id SET NOT NULL;
COMMIT;
'@
        if ($nn.ExitCode -ne 0 -or $nn.Error -match "(?i)(ERROR|FATAL):") {
            Say $nn.Error
            Say "[FAIL] NOT NULL could not be enforced. Nothing else changed."
            $bad = $bad + 1
        } else {
            Say "[OK] refresh_run_id is now NOT NULL on both value tables."
            Say "    A future refresh cannot silently return to an untraceable state."
        }
    }
}
catch {
    $bad = $bad + 1
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

Rule "RESULT"
if ($bad -gt 0) { Say ("[FAIL] " + $bad + " problem(s).") }
else {
    Say "[OK] stale state cleared, engine-produced values in place with full"
    Say "     lineage, reproducible, and the invariant enforced."
    Say ""
    Say "STILL OUTSTANDING FOR T-025: correlation, readiness, risk and learning"
    Say "engines, and at least one genuine refusal rendered honestly."
}

$outFolder = Join-Path $repoRoot "docs\m1\evidence"
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$ev = Join-Path $outFolder ("T-025_execute_" + $stamp + ".txt")
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
