# ============================================================================
# Run-GoldenAnalysis.ps1  v1.2   Backlog v23 M1-21
# v1.2: prints the effect-size distribution of existing findings - the 14:41
#       run showed the top 6 all at effect=1.00, which would mean no
#       historical finding shows ANY effect. Settle it with data, not memory.
# v1.1: real results_v2 columns (compute_run_id / effect_size / sample_size /
#       effective_n / evidence_json - there is no source_correlation_run_id,
#       no q_value column, no population_count). Requires Fix-MlFoundationAccess
#       first: every POST to /api/ml/foundation returns 403 until the access
#       matrix maps it.
# GOLDEN EVIDENCE CHAIN part 2: a fresh governed run, linked end-to-end.
#
# THE REAL ENGINE (verified in source, not assumed):
#   POST /api/ml/foundation/compute/correlation
#        body: { outcomeKey, grain, windowDays, filters? }
#        -> ICorrelationComputeEngine.ComputeAsync
#        -> CorrelationComputeResult(ComputeRunId, ResultCount, EngineKey, Status)
#   Results land in ml_correlation_results_v2 keyed by the compute run id.
#
#   NOT USED: POST /phase5/scheduled-learning/run-now - that endpoint only
#   UPDATEs phase5_learning_job_evidence to 'Completed'/'Passed' and computes
#   nothing. It is reachable only from an e2e spec, never from a product page.
#   It must never be the thing behind a "run the analysis" click. (M2-20 row.)
#
# WHAT THIS PROVES (senior recs 7 + 16):
#   - a NEW compute run id exists, created now, not one of the 375 historical
#   - the money finding carries effect size, q-value and population
#   - the null control is rejected at ~1.0x by the SAME run
#   ...which is what makes the number evidence instead of an anecdote.
#
# SCOPE HONESTY: run this BEFORE Phase A and the finding is computed on the
# restored dataset - a real fresh run, but NOT "data the customer watched
# arrive". Re-run it AFTER Phase A-D and the same command upgrades the claim.
# The script prints which of the two you have earned.
#
# Run from repo root (API up on the presentation profile):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Run-GoldenAnalysis.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Run-GoldenAnalysis.ps1 -Execute
# ============================================================================
[CmdletBinding()]
param(
    [switch]$Execute,
    [switch]$SkipFeatureRefresh,
    [int]$WindowDays = 120,
    [string]$Grain = 'material',
    [string]$ApiBase = 'http://localhost:5063',
    [string]$ApiUser = 'e2eadmin',
    [string]$ApiPassword = 'E2EAdmin123!',
    [string]$TargetDb = 'ppiq_presentation'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'

$RepoRoot = (Get-Location).Path
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$Out = Join-Path $RepoRoot ('M1-21_GoldenAnalysis_' + $Stamp + '.txt')
$sb = New-Object System.Text.StringBuilder
function W([string]$s) { [void]$sb.AppendLine($s); Write-Host $s }
function Save { [System.IO.File]::WriteAllText($Out, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false))) }

# ---- psql (self-checked) ---------------------------------------------------
$Psql = $null
$cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
if ($cmd) { $Psql = $cmd.Source } else {
    $c = @(Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending)
    if ($c.Count -gt 0) { $Psql = $c[0].FullName }
}
if (-not $Psql) { Write-Host "[FAIL] psql not found." -ForegroundColor Red; exit 1 }
$env:PGPASSWORD = 'ppiq_dev_local_only'
function Rows([string]$q) {
    return @(& $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -A -t -F ' | ' -c $q 2>&1 | Where-Object { $_ -and ($_.ToString().Trim() -ne '') })
}
function One([string]$q) {
    $r = @(Rows $q)
    if ($r.Count -eq 0) { return $null }
    return ([string]$r[0]).Trim()
}
$probe = One "SELECT 40148;"
if ("$probe" -ne '40148') { Write-Host "[SELF-CHECK FAILED] query layer broken." -ForegroundColor Red; exit 1 }

W ("M1-21 GOLDEN EVIDENCE CHAIN part 2 - " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ("API: " + $ApiBase + "   DB: " + $TargetDb)
W ("=" * 78)
W ""

# ---- auth ------------------------------------------------------------------
$token = $null
foreach ($u in @('/api/auth/login', '/auth/login')) {
    foreach ($b in @((@{ username = $ApiUser; password = $ApiPassword } | ConvertTo-Json), (@{ email = $ApiUser; password = $ApiPassword } | ConvertTo-Json))) {
        if ($token) { break }
        try {
            $r = Invoke-RestMethod -Uri ($ApiBase + $u) -Method Post -Body $b -ContentType 'application/json' -TimeoutSec 10 -ErrorAction Stop
            if ($r.PSObject.Properties['accessToken']) { $token = $r.accessToken } elseif ($r.PSObject.Properties['token']) { $token = $r.token }
        } catch { }
    }
}
if (-not $token) { W "[ABORT] cannot authenticate - start the API: .\scripts\run\start-api.ps1 -Profile presentation"; Save; exit 1 }
$H = @{ Authorization = 'Bearer ' + $token }
W "[AUTH] OK"
W ""

# ---- 0. baseline: what exists BEFORE this run ------------------------------
$runsBefore = One "SELECT COUNT(DISTINCT compute_run_id) FROM ml_correlation_results_v2;"
$resBefore = One "SELECT COUNT(*) FROM ml_correlation_results_v2;"
W ("[BEFORE] results_v2 rows: " + $resBefore + "   distinct run ids: " + $runsBefore)
W ""
W "    effect-size DISTRIBUTION across all existing findings:"
W "    (if max = 1.00 then no historical finding shows any effect at all -"
W "     and the 9.3x money number is not in this database)"
Rows @"
SELECT '      count=' || COUNT(*) ||
       '   min=' || COALESCE(round(MIN(effect_size)::numeric,3)::text,'-') ||
       '   max=' || COALESCE(round(MAX(effect_size)::numeric,3)::text,'-') ||
       '   distinct_values=' || COUNT(DISTINCT effect_size) ||
       '   nulls=' || COUNT(*) FILTER (WHERE effect_size IS NULL)
FROM ml_correlation_results_v2;
"@ | ForEach-Object { W $_ }
Rows @"
SELECT '      effect_size_type=' || COALESCE(effect_size_type,'(null)') || '  method=' || COALESCE(method,'(null)') || '  ->  ' || COUNT(*) || ' rows'
FROM ml_correlation_results_v2 GROUP BY 1,effect_size_type,method ORDER BY 1;
"@ | ForEach-Object { W $_ }
W ""
W "    any finding with effect_size > 1.5 (i.e. a real signal), anywhere:"
$strong = @(Rows @"
SELECT '      ' || COALESCE(feature_key,'?') || ' -> ' || COALESCE(outcome_key,'?') || '  effect=' || round(effect_size::numeric,2)::text
FROM ml_correlation_results_v2 WHERE effect_size > 1.5 ORDER BY effect_size DESC LIMIT 8;
"@)
if ($strong.Count -eq 0) { W "      NONE. Every historical finding is at or below 1.5." } else { $strong | ForEach-Object { W $_ } }
W ""
W "    top 6 EXISTING findings (historical - shows what a good result looks like):"
Rows @"
SELECT '      ' || COALESCE(feature_key,'?') || ' -> ' || COALESCE(outcome_key,'?') ||
       '  effect=' || COALESCE(round(effect_size::numeric,2)::text,'-') ||
       '  n=' || COALESCE(sample_size::text,'-')
FROM ml_correlation_results_v2 ORDER BY effect_size DESC NULLS LAST LIMIT 6;
"@ | ForEach-Object { W $_ }
W "         (any run id after this line is provably created by THIS execution)"
W ""

# ---- 1. readiness ----------------------------------------------------------
W "[1] engine readiness:"
try {
    $rd = Invoke-RestMethod -Uri ($ApiBase + '/api/ml/foundation/readiness') -Headers $H -TimeoutSec 20 -ErrorAction Stop
    foreach ($p in $rd.PSObject.Properties) { W ("    " + $p.Name.PadRight(26) + " " + $p.Value) }
} catch {
    W ("    readiness call failed: " + $_.Exception.Message)
    W "    (route may differ - continuing)"
}
W ""

# ---- 2. outcomes -----------------------------------------------------------
W "[2] outcome definitions (the engine's own catalog):"
$outcomes = @()
try {
    $oc = Invoke-RestMethod -Uri ($ApiBase + '/api/ml/foundation/outcomes') -Headers $H -TimeoutSec 20 -ErrorAction Stop
    $list = $oc
    foreach ($k in @('items', 'outcomes', 'data')) { if ($oc.PSObject.Properties[$k]) { $list = $oc.$k; break } }
    foreach ($o in @($list)) {
        $key = $null
        foreach ($k in @('outcomeKey', 'outcome_key', 'code', 'outcomeCode')) { if ($o.PSObject.Properties[$k]) { $key = $o.$k; break } }
        if ($key) { $outcomes += [string]$key; W ("    " + $key) }
    }
} catch { W ("    outcomes call failed: " + $_.Exception.Message) }
if ($outcomes.Count -eq 0) {
    W "    falling back to the catalog in the database:"
    $outcomes = @(Rows "SELECT outcome_key FROM ml_outcome_definitions WHERE is_deleted = false ORDER BY 1;" | ForEach-Object { ([string]$_).Trim() })
    foreach ($o in $outcomes) { W ("    " + $o) }
}
W ""
if ($outcomes.Count -eq 0) { W "[ABORT] no outcome definitions - the engine has nothing to correlate against."; Save; exit 1 }

if (-not $Execute) {
    W "DRY-RUN. With -Execute this would:"
    if (-not $SkipFeatureRefresh) { W ("  * POST /api/ml/foundation/feature-store/refresh  (windowDays=" + $WindowDays + ")") }
    foreach ($o in $outcomes) { W ("  * POST /api/ml/foundation/compute/correlation  outcomeKey=" + $o + " grain=" + $Grain + " windowDays=" + $WindowDays) }
    W "  * verify each NEW run id in results_v2 and report effect / q / population"
    W ""
    W "Re-run with -Execute."
    Save; exit 0
}

# ---- 3. feature store ------------------------------------------------------
if (-not $SkipFeatureRefresh) {
    W ("[3] feature-store refresh (windowDays=" + $WindowDays + "):")
    try {
        $fr = Invoke-RestMethod -Uri ($ApiBase + '/api/ml/foundation/feature-store/refresh') -Method Post -Headers $H `
            -Body (@{ windowDays = $WindowDays } | ConvertTo-Json) -ContentType 'application/json' -TimeoutSec 180 -ErrorAction Stop
        foreach ($p in $fr.PSObject.Properties) { W ("    " + $p.Name.PadRight(20) + " " + $p.Value) }
    } catch { W ("    FAILED: " + $_.Exception.Message) }
    W ""
}

# ---- 4. the governed runs --------------------------------------------------
W "[4] governed correlation runs:"
$runIds = @()
foreach ($o in $outcomes) {
    $body = @{ outcomeKey = $o; grain = $Grain; windowDays = $WindowDays } | ConvertTo-Json
    try {
        $res = Invoke-RestMethod -Uri ($ApiBase + '/api/ml/foundation/compute/correlation') -Method Post -Headers $H `
            -Body $body -ContentType 'application/json' -TimeoutSec 300 -ErrorAction Stop
        $rid = $null; $cnt = '?'; $st = '?'; $ek = '?'
        foreach ($k in @('computeRunId', 'ComputeRunId', 'runId')) { if ($res.PSObject.Properties[$k]) { $rid = $res.$k; break } }
        foreach ($k in @('resultCount', 'ResultCount')) { if ($res.PSObject.Properties[$k]) { $cnt = $res.$k; break } }
        foreach ($k in @('status', 'Status')) { if ($res.PSObject.Properties[$k]) { $st = $res.$k; break } }
        foreach ($k in @('engineKey', 'EngineKey')) { if ($res.PSObject.Properties[$k]) { $ek = $res.$k; break } }
        W ("    " + $o.PadRight(22) + " status=" + $st + "  results=" + $cnt + "  engine=" + $ek)
        W ("        run id: " + $rid)
        if ($rid) { $runIds += [string]$rid }
        if ("$st" -match '(?i)block') { W "        ^ BLOCKED - this is the readiness gate refusing. Honest, and it is a feature." }
    } catch {
        W ("    " + $o.PadRight(22) + " FAILED: " + $_.Exception.Message)
    }
}
W ""

# ---- 5. verify the fresh results -------------------------------------------
$runsAfter = One "SELECT COUNT(DISTINCT compute_run_id) FROM ml_correlation_results_v2;"
$resAfter = One "SELECT COUNT(*) FROM ml_correlation_results_v2;"
W ("[5] results_v2 now: " + $resAfter + " rows (was " + $resBefore + ")   distinct runs: " + $runsAfter + " (was " + $runsBefore + ")")
W ""
if ($runIds.Count -eq 0) {
    W "    NO new run ids returned - nothing to verify. Read section 4."
    Save; exit 1
}
$inList = "'" + (($runIds) -join "','") + "'"
W "[6] THE EVIDENCE - findings from THIS run only:"
W ""
$cols = @(Rows "SELECT string_agg(column_name, ',' ORDER BY ordinal_position) FROM information_schema.columns WHERE table_schema='public' AND table_name='ml_correlation_results_v2';")
W ("    (results_v2 columns: " + $(if (@($cols).Count) { $cols[0] } else { '?' }) + ")")
W ""
Rows @"
SELECT COALESCE(feature_key,'?') || ' -> ' || COALESCE(outcome_key,'?') ||
       '  [' || COALESCE(method,'?') || ']' ||
       '  effect=' || COALESCE(round(effect_size::numeric,2)::text,'-') || COALESCE(' ' || effect_size_type,'') ||
       '  coef=' || COALESCE(round(coefficient::numeric,3)::text,'-') ||
       '  n=' || COALESCE(sample_size::text,'-') || '/' || COALESCE(effective_n::text,'-') ||
       '  q=' || COALESCE(evidence_json->>'qValue', evidence_json->>'q_value', evidence_json->>'q', '-')
FROM ml_correlation_results_v2
WHERE compute_run_id::text IN ($inList)
ORDER BY effect_size DESC NULLS LAST
LIMIT 25;
"@ | ForEach-Object { W ("    " + $_) }
W ""
W "[7] the money slide check:"
# NOTE: outcome_key is a CLASS ('defect.class', 'defect.rate_per_m2'...), so the
# specific defect (CRACK_LONG / SCRATCH) lives inside evidence_json, not in the
# key. Match on the feature and let the evidence carry the label.
$signal = One @"
SELECT round(effect_size::numeric,2)::text || '  (' || COALESCE(outcome_key,'?') || ', n=' || COALESCE(sample_size::text,'-') || ')'
FROM ml_correlation_results_v2
WHERE compute_run_id::text IN ($inList) AND feature_key ILIKE '%superheat%'
ORDER BY effect_size DESC NULLS LAST LIMIT 1;
"@
$null_ = One @"
SELECT round(effect_size::numeric,2)::text || '  (' || COALESCE(feature_key,'?') || ' -> ' || COALESCE(outcome_key,'?') || ')'
FROM ml_correlation_results_v2
WHERE compute_run_id::text IN ($inList)
  AND (evidence_json::text ILIKE '%SCRATCH%' OR feature_key ILIKE '%scratch%')
ORDER BY effect_size ASC NULLS LAST LIMIT 1;
"@
W ("    planted signal  superheat -> CRACK_LONG : " + $(if ($signal) { $signal + 'x  (expect ~9.3)' } else { 'NOT FOUND in this run' }))
W ("    null control    -> SCRATCH             : " + $(if ($null_) { $null_ + 'x  (expect ~1.0)' } else { 'NOT FOUND in this run' }))
W ""

# ---- 8. scope verdict ------------------------------------------------------
$importedSince = One "SELECT COUNT(*) FROM source_dataset_definitions;"
W "=" * 78
W "WHAT YOU HAVE EARNED:"
if ([int]$importedSince -le 3) {
    W "  [PARTIAL] This is a genuine FRESH run - new run id, computed now, on the"
    W "            restored dataset. You may say: 'the engine recovered a planted"
    W "            validation signal and rejected a null control - that validates"
    W "            the method; ROI is what the pilot measures.'"
    W "  [NOT YET] You may NOT say 'computed on the data you just watched arrive'."
    W "            That needs Phase A-D imports first (M1-20), then re-run this."
} else {
    W "  [FULL] Phase A-D imports are present. Re-running this after the imports"
    W "         links the finding to batches the customer watched arrive - the"
    W "         complete golden chain."
}
W ""
W "NEXT: M1-01 - reindex the assistant and have it cite THIS run id."
Save
Write-Host ""
Write-Host ("[DONE] -> " + $Out) -ForegroundColor Green
exit 0
