#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-025 remaining engines - correlation, risk, learning, readiness, and the
    corrected genuine-refusal gate. NO feature-store refresh.

.DESCRIPTION
    THE FEATURE STORE IS CLOSED AND FROZEN. 505,680 feature values and 21,649
    outcome values, full lineage, row-level reproducibility zero in both
    directions, NOT NULL enforced. This script does not touch it and does not
    call /feature-store/refresh. Re-running that is a 5-minute cost with nothing
    to prove.

    WHAT THE EARLIER RUNS GOT WRONG, corrected here from the source:

    1. CORRELATION was posted with an empty body and returned 400. The contract is
       CorrelationComputeRequest(OutcomeKey, Grain, WindowDays, Filters?). The
       outcome keys are NOT hardcoded here - they are read from
       ml_outcome_definitions, with each definition's own grain.

    2. RISK was posted to /api/analytics/risk-scores/calculate-all, which is not a
       route. The batch route is POST /risk-scores/calculate-all
       (RiskScoreEndpoints.cs:29, MapGroup "/risk-scores", mapped at Program.cs:969).
       It also needs the T-025a matrix entry, and it is additionally gated by
       RequireLicenseFeature(RiskDashboardView), which is a Pro-tier feature. If the
       licence refuses, that is recorded as a licence refusal, not as an engine result.

    3. LEARNING looked empty because the query filtered coalesce(is_enabled, true)
       while ml_learning_job_catalog_v1.is_enabled is NOT NULL DEFAULT false. Every
       job being disabled is a legitimate NOT CONFIGURED state. This script reports
       enabled and disabled separately and runs only the enabled ones.

    THE REFUSAL DEFINITION, as ruled and now enforced mechanically. A genuine
    refusal is: a VALID authenticated request that REACHES a real engine and is
    declined for an ANALYTICAL reason - readiness gate not met, insufficient
    support, population below minimum. It is recognised as HTTP 200 with an engine
    status of Blocked or NoData carrying a reason. Explicitly NOT a refusal:
    400 (malformed request), 401, 403 (permission or licence), 404 (no route),
    405, and any 5xx. The gate counts only the first kind.

    THE REFUSAL MUST BE PERSISTED, per the T-025 validation text. The advanced
    engine writes the blocked run to ml_correlation_compute_runs with
    status = 'Blocked' before returning, so the check is a database check, not a
    reading of the HTTP body.

    NO ANALYSIS ROW IS WRITTEN BY THIS SCRIPT. It calls engines and measures.

.EXAMPLE
    .\tools\run\Invoke-PpiqT025Engines.ps1
    .\tools\run\Invoke-PpiqT025Engines.ps1 -MaxMaterials 1000
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
    [int]   $MaxMaterials  = 500,
    [int]   $MaxOutcomes   = 8
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

$script:log   = ""
$script:Token = $null
$script:Gate  = @{}
$script:Refusals = New-Object System.Collections.ArrayList
$script:NonRefusals = New-Object System.Collections.ArrayList

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
function Invoke-Api {
    param([string]$Method, [string]$Path, $Body = $null, [int]$TimeoutSec = 900)
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
            if ($res.Raw) { try { $res.Body = $res.Raw | ConvertFrom-Json } catch { } }
        } else { $res.Raw = $_.Exception.Message }
    }
    return $res
}
function Short { param([string]$T, [int]$N = 190)
    if ($null -eq $T) { return "" }
    $t = ($T -replace "`r", "" -replace "`n", " ")
    if ($t.Length -le $N) { return $t }
    return $t.Substring(0, $N)
}
function Classify {
    param([string]$Surface, [int]$Status, $Body, [string]$Raw)
    # THE ONLY PLACE THE REFUSAL DEFINITION IS APPLIED.
    if ($Status -ne 200) {
        [void]$script:NonRefusals.Add(($Surface + " -> HTTP " + $Status + " : " + (Short $Raw 140)))
        return "NOT-A-REFUSAL"
    }
    $engineStatus = ""
    if ($null -ne $Body) {
        if ($Body.PSObject.Properties.Name -contains "status")  { $engineStatus = [string]$Body.status }
        if ($Body.PSObject.Properties.Name -contains "Status")  { $engineStatus = [string]$Body.Status }
    }
    if ($engineStatus -eq "Blocked" -or $engineStatus -eq "NoData") {
        $msg = ""
        if ($Body.PSObject.Properties.Name -contains "message") { $msg = [string]$Body.message }
        if ($Body.PSObject.Properties.Name -contains "Message") { $msg = [string]$Body.Message }
        [void]$script:Refusals.Add(($Surface + " -> 200 " + $engineStatus + " : " + (Short $msg 160)))
        return "REFUSAL"
    }
    return "RESULT"
}

Rule "PPIQ T-025 REMAINING ENGINES"
$repoRoot = (Get-Location).Path
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
Say ("API      : " + $ApiBase)
Say ("Database : " + $Database)
Say ("Mode     : engines only - NO feature-store refresh")

$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t025eng_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
$bad = 0

try {
    # -------------------------------------------------------------- AUTH
    Rule "0 - AUTHENTICATE"
    $login = Invoke-Api -Method POST -Path "/auth/login" `
                        -Body @{ userName = $SmokeUser; password = $SmokePassword }
    if ($login.Status -ne 200) { Say ("[FAIL] login " + $login.Status + " : " + (Short $login.Raw)); throw "auth" }
    $script:Token = $login.Body.accessToken
    Say ("[OK] token length " + $script:Token.Length)
    $script:Gate["Authenticated product path"] = "PASS"

    # -------------------------------------------------------------- A
    Rule "A - THE FEATURE STORE IS NOT TOUCHED"
    Say "Confirming the frozen state only. Nothing is refreshed, cleared or rewritten."
    $frozen = Invoke-Sql -Tag "frozen" -Sql @"
\qecho ''
SELECT 'feature values' AS entity, count(*) AS rows FROM public.ml_feature_values
UNION ALL SELECT 'outcome values', count(*) FROM public.ml_outcome_values
UNION ALL SELECT 'feature values without a run', count(*) FROM public.ml_feature_values WHERE refresh_run_id IS NULL
UNION ALL SELECT 'outcome values without a run', count(*) FROM public.ml_outcome_values WHERE refresh_run_id IS NULL
ORDER BY 1;
"@
    Say $frozen.Output

    # -------------------------------------------------------------- B
    Rule "B - OUTCOME DEFINITIONS, READ NOT ASSUMED"
    Say "Outcome keys and grains come from ml_outcome_definitions. Nothing is hardcoded."
    $defs = Get-SqlLines -Tag "outcomedefs" -Sql @"
SELECT outcome_key || '|' || grain
FROM public.ml_outcome_definitions
WHERE is_deleted = false AND status = 'Active'
ORDER BY outcome_key
LIMIT $MaxOutcomes;
"@
    if ($defs.Count -eq 0) {
        Say "[FAIL] no active outcome definitions. Correlation cannot be invoked."
        $bad = $bad + 1
        $script:Gate["Correlation"] = "NO OUTCOME DEFINITIONS"
    } else {
        Say ("  " + $defs.Count + " active outcome definition(s):")
        foreach ($d in $defs) { Say ("    " + $d) }
    }

    # -------------------------------------------------------------- C
    Rule "C - CORRELATION, ONE CALL PER OUTCOME DEFINITION"
    Say "POST /api/ml/foundation/compute/correlation"
    Say "Body: { outcomeKey, grain, windowDays } - the full CorrelationComputeRequest."
    $corrResults = 0
    $corrRefusals = 0
    $corrErrors = 0
    foreach ($d in $defs) {
        $parts = $d -split "\|"
        $key = $parts[0]
        $grain = "coil"
        if ($parts.Count -gt 1 -and -not [string]::IsNullOrWhiteSpace($parts[1])) { $grain = $parts[1] }
        $t0 = Get-Date
        $r = Invoke-Api -Method POST -Path "/api/ml/foundation/compute/correlation" `
                        -Body @{ outcomeKey = $key; grain = $grain; windowDays = $WindowDays }
        $secs = [math]::Round(((Get-Date) - $t0).TotalSeconds, 1)
        $verdict = Classify -Surface ("correlation " + $key) -Status $r.Status -Body $r.Body -Raw $r.Raw
        $engineKey = ""
        $count = ""
        $msg = ""
        if ($null -ne $r.Body) {
            if ($r.Body.PSObject.Properties.Name -contains "engineKey")   { $engineKey = [string]$r.Body.engineKey }
            if ($r.Body.PSObject.Properties.Name -contains "resultCount") { $count = [string]$r.Body.resultCount }
            if ($r.Body.PSObject.Properties.Name -contains "message")     { $msg = [string]$r.Body.message }
        }
        Say ("  " + $key.PadRight(34) + " grain=" + $grain.PadRight(8) + " -> " + $r.Status +
             "  " + $verdict.PadRight(14) + $secs + "s")
        if ($engineKey) { Say ("      engine " + $engineKey + "  results " + $count) }
        if ($msg)       { Say ("      " + (Short $msg 150)) }
        if ($verdict -eq "RESULT")   { $corrResults = $corrResults + 1 }
        if ($verdict -eq "REFUSAL")  { $corrRefusals = $corrRefusals + 1 }
        if ($verdict -eq "NOT-A-REFUSAL") {
            $corrErrors = $corrErrors + 1
            Say ("      " + (Short $r.Raw 170))
        }
    }
    Say ""
    Say ("  produced results : " + $corrResults)
    Say ("  refused honestly : " + $corrRefusals)
    Say ("  failed outright  : " + $corrErrors)
    if ($corrErrors -gt 0) { $bad = $bad + 1 }
    if ($corrResults -gt 0 -or $corrRefusals -gt 0) {
        $script:Gate["Correlation"] = ("PASS - " + $corrResults + " with results, " + $corrRefusals + " refused")
    } else {
        $script:Gate["Correlation"] = "FAIL - no engine outcome at all"
    }

    Rule "C2 - WHAT THE CORRELATION ENGINE PERSISTED"
    $corrDb = Invoke-Sql -Tag "corrdb" -Sql @"
SELECT status, count(*) AS runs, min(completed_at_utc) AS first_run, max(completed_at_utc) AS last_run
FROM public.ml_correlation_compute_runs
GROUP BY status
ORDER BY status;
"@
    Say $corrDb.Output
    $corrCover = Invoke-Sql -Tag "corrcover" -Sql @"
SELECT 'correlation results' AS check_name, count(*)::int AS found, count(*)::int AS required FROM public.ml_correlation_results_v2
UNION ALL
SELECT 'results without a compute run', count(*)::int, 0
FROM public.ml_correlation_results_v2 r
LEFT JOIN public.ml_correlation_compute_runs c ON c.id = r.compute_run_id
WHERE c.id IS NULL;
"@
    Say $corrCover.Output

    # -------------------------------------------------------------- D
    Rule "D - RISK, ON THE ROUTE THAT ACTUALLY EXISTS"
    Say "POST /risk-scores/calculate-all  (RiskScoreEndpoints MapGroup /risk-scores)."
    Say "NOT /api/analytics/risk-scores/calculate-all - that route does not exist."
    $siteLines = Get-SqlLines -Tag "site" -Sql @"
SELECT site_id::text
FROM public.material_units
WHERE site_id IS NOT NULL
GROUP BY site_id
ORDER BY count(*) DESC
LIMIT 1;
"@
    $siteId = $null
    if ($siteLines.Count -gt 0) { $siteId = $siteLines[0] }
    Say ("  site with the most material units : " + $siteId)
    $riskBody = @{ maxMaterials = $MaxMaterials; storeResult = $true }
    if ($null -ne $siteId) { $riskBody["siteId"] = $siteId }
    $t0 = Get-Date
    $risk = Invoke-Api -Method POST -Path "/risk-scores/calculate-all" -Body $riskBody
    $secs = [math]::Round(((Get-Date) - $t0).TotalSeconds, 1)
    $riskVerdict = Classify -Surface "risk calculate-all" -Status $risk.Status -Body $risk.Body -Raw $risk.Raw
    Say ("  -> " + $risk.Status + "  " + $riskVerdict + "  " + $secs + "s")
    Say ("     " + (Short $risk.Raw 300))
    if ($risk.Status -eq 403) {
        Say ""
        Say "  403 HERE MEANS ONE OF TWO DIFFERENT THINGS. Read the body:"
        Say "    'not mapped in the P01/P02 permission matrix'  -> the T-025a pack was"
        Say "       not applied, or the API was not rebuilt and restarted after it."
        Say "    'Permission denied' / a licence message        -> the matrix entry IS"
        Say "       live and the block is RequireLicenseFeature(RiskDashboardView),"
        Say "       which needs a Pro-or-above licence on this instance."
        Say "  Neither is an analytical refusal and neither counts toward the gate."
    }
    $riskDb = Invoke-Sql -Tag "riskdb" -Sql @"
SELECT 'risk_scores' AS entity, count(*) AS rows FROM public.risk_scores
UNION ALL SELECT 'risk_scores not deleted', count(*) FROM public.risk_scores WHERE is_deleted = false;
"@
    Say $riskDb.Output
    if ($risk.Status -eq 200) {
        $script:Gate["Risk"] = "PASS - engine invoked"
    } else {
        $script:Gate["Risk"] = ("BLOCKED - HTTP " + $risk.Status)
        $bad = $bad + 1
    }

    # -------------------------------------------------------------- E
    Rule "E - LEARNING, WITHOUT THE FILTER THAT HID THE CATALOGUE"
    Say "is_enabled is NOT NULL DEFAULT false. Disabled is NOT CONFIGURED, not missing."
    $cat = Invoke-Sql -Tag "learncat" -Sql @"
SELECT is_enabled, count(*) AS jobs
FROM public.ml_learning_job_catalog_v1
GROUP BY is_enabled
ORDER BY is_enabled;
"@
    Say $cat.Output
    $enabled = Get-SqlLines -Tag "learnenabled" -Sql @"
SELECT job_code
FROM public.ml_learning_job_catalog_v1
WHERE is_enabled = true
ORDER BY job_code;
"@
    if ($enabled.Count -eq 0) {
        Say "  NO JOB IS ENABLED. That is a configuration state, not an engine failure."
        Say "  T-025 does not ask for the learning jobs to be turned on, so none is"
        Say "  enabled here. Recorded as NOT CONFIGURED."
        $script:Gate["Learning"] = "NOT CONFIGURED - 0 jobs enabled"
    } else {
        Say ("  " + $enabled.Count + " enabled job(s). Running each.")
        foreach ($j in $enabled) {
            $r = Invoke-Api -Method POST -Path ("/api/ml/learning/jobs/" + $j + "/run")
            $v = Classify -Surface ("learning " + $j) -Status $r.Status -Body $r.Body -Raw $r.Raw
            Say ("    " + $j.PadRight(34) + " -> " + $r.Status + "  " + $v)
            Say ("      " + (Short $r.Raw 170))
        }
        $script:Gate["Learning"] = ("RAN - " + $enabled.Count + " job(s)")
    }
    $learnDb = Invoke-Sql -Tag "learndb" -Sql @"
SELECT 'ml_learning_runs_v1' AS entity, count(*) AS rows FROM public.ml_learning_runs_v1
UNION ALL SELECT 'ml_learning_results_v1', count(*) FROM public.ml_learning_results_v1
UNION ALL SELECT 'ml_learning_observations_v1', count(*) FROM public.ml_learning_observations_v1
ORDER BY 1;
"@
    Say $learnDb.Output

    # -------------------------------------------------------------- F
    Rule "F - READINESS, RE-EVALUATED AFTER THE ENGINES RAN"
    $ready = Invoke-Api -Method GET -Path "/api/ml/foundation/readiness"
    Say ("  -> " + $ready.Status)
    Say ("     " + (Short $ready.Raw 400))
    if ($ready.Status -eq 200) {
        $script:Gate["Readiness"] = "PASS - re-evaluated after the engines"
    } else {
        $script:Gate["Readiness"] = ("FAIL - HTTP " + $ready.Status)
        $bad = $bad + 1
    }

    # -------------------------------------------------------------- G
    Rule "G - COMPUTE-RUN COVERAGE - NO HAND-AUTHORED ANALYSIS ROW"
    Say "T-025 validation: no analysis row exists without a compute run identity."
    $cover = Invoke-Sql -Tag "coverage" -Sql @"
SELECT 'feature values without a run' AS check_name, count(*)::int AS found, 0 AS required
FROM public.ml_feature_values WHERE refresh_run_id IS NULL
UNION ALL
SELECT 'outcome values without a run', count(*)::int, 0
FROM public.ml_outcome_values WHERE refresh_run_id IS NULL
UNION ALL
SELECT 'correlation results without a compute run', count(*)::int, 0
FROM public.ml_correlation_results_v2 r
LEFT JOIN public.ml_correlation_compute_runs c ON c.id = r.compute_run_id
WHERE c.id IS NULL
UNION ALL
SELECT 'learning results without a run', count(*)::int, 0
FROM public.ml_learning_results_v1 lr
LEFT JOIN public.ml_learning_runs_v1 lu ON lu.id = lr.run_id
WHERE lu.id IS NULL;
"@
    Say $cover.Output

    # THE VERDICT IS PARSED FROM A RAW DELIMITED QUERY, NEVER FROM THE PRETTY TABLE.
    # psql table borders depend on a psqlrc that may not exist on another machine;
    # a regex that quietly matches nothing would report PASS on unparsed output.
    $coverRows = Get-SqlLines -Tag "coverageraw" -Sql @"
SELECT check_name || '~' || found::text || '~' || required::text FROM (
SELECT 'feature values without a run' AS check_name, count(*)::int AS found, 0 AS required
FROM public.ml_feature_values WHERE refresh_run_id IS NULL
UNION ALL
SELECT 'outcome values without a run', count(*)::int, 0
FROM public.ml_outcome_values WHERE refresh_run_id IS NULL
UNION ALL
SELECT 'correlation results without a compute run', count(*)::int, 0
FROM public.ml_correlation_results_v2 r
LEFT JOIN public.ml_correlation_compute_runs c ON c.id = r.compute_run_id
WHERE c.id IS NULL
UNION ALL
SELECT 'learning results without a run', count(*)::int, 0
FROM public.ml_learning_results_v1 lr
LEFT JOIN public.ml_learning_runs_v1 lu ON lu.id = lr.run_id
WHERE lu.id IS NULL
) q;
"@
    $coverBad = 0
    $coverSeen = 0
    foreach ($line in $coverRows) {
        $p = $line -split "~"
        if ($p.Count -ne 3) { continue }
        $coverSeen = $coverSeen + 1
        if ([int]$p[1] -ne [int]$p[2]) {
            Say ("[FAIL] coverage - " + $p[0] + ": found " + $p[1] + ", required " + $p[2])
            $coverBad = $coverBad + 1
        }
    }
    Say ("  parsed rows : " + $coverSeen + " (required 4)")
    if ($coverSeen -ne 4) {
        Say "[FAIL] coverage output did not parse. Refusing to report PASS on unparsed output."
        $coverBad = $coverBad + 1
    }
    if ($coverBad -eq 0) {
        $script:Gate["Compute-run coverage"] = "PASS"
    } else {
        $script:Gate["Compute-run coverage"] = ("FAIL - " + $coverBad)
        $bad = $bad + 1
    }

    # -------------------------------------------------------------- H
    Rule "H - THE GENUINE REFUSAL, UNDER THE CORRECTED DEFINITION"
    Say "A refusal is a VALID authenticated request that REACHED a real engine and was"
    Say "declined for an ANALYTICAL reason, persisted by the engine itself."
    Say "400, 401, 403, 404, 405 and 5xx are excluded by construction."
    Say ""
    if ($script:NonRefusals.Count -gt 0) {
        Say ("  EXCLUDED, correctly - " + $script:NonRefusals.Count + " transport/permission outcome(s):")
        foreach ($n in $script:NonRefusals) { Say ("    " + $n) }
        Say ""
    }
    if ($script:Refusals.Count -gt 0) {
        Say ("  COUNTED - " + $script:Refusals.Count + " analytical refusal(s):")
        foreach ($r in $script:Refusals) { Say ("    " + $r) }
    } else {
        Say "  none. Every engine that was reached produced a result."
    }
    $persisted = Invoke-Sql -Tag "persistedrefusal" -Sql @"
SELECT status, count(*) AS runs, max(left(coalesce(message, ''), 90)) AS sample_message
FROM public.ml_correlation_compute_runs
WHERE status IN ('Blocked', 'NoData')
GROUP BY status
ORDER BY status;
"@
    Say ""
    Say "  PERSISTED refusals in ml_correlation_compute_runs:"
    Say $persisted.Output
    $persistedRows = Get-SqlLines -Tag "persistedraw" -Sql @"
SELECT status || '~' || count(*)::text
FROM public.ml_correlation_compute_runs
WHERE status IN ('Blocked', 'NoData')
GROUP BY status
ORDER BY status;
"@
    $persistedCount = 0
    foreach ($line in $persistedRows) {
        $p = $line -split "~"
        if ($p.Count -eq 2) { $persistedCount = $persistedCount + [int]$p[1] }
    }
    Say ("  persisted refusal runs : " + $persistedCount)
    if ($persistedCount -gt 0) {
        $script:Gate["Genuine refusal"] = ("PASS - " + $persistedCount + " persisted")
    } else {
        $script:Gate["Genuine refusal"] = "NONE PERSISTED"
    }
}
catch {
    Say ("[ERROR] " + $_.Exception.Message)
    $bad = $bad + 1
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

Rule "T-025 ENGINE GATE"
foreach ($k in @("Authenticated product path", "Correlation", "Risk", "Learning",
                 "Readiness", "Compute-run coverage", "Genuine refusal")) {
    $v = "NOT REACHED"
    if ($script:Gate.ContainsKey($k)) { $v = $script:Gate[$k] }
    Say ("  " + $k.PadRight(30) + $v)
}
Say ""
Say "The feature-store half was closed in the 05-Aug 09:46 run and is not re-proved here."

$outFolder = Join-Path $repoRoot "docs\m1\evidence"
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$ev = Join-Path $outFolder ("T-025_engines_" + $stamp + ".txt")
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
