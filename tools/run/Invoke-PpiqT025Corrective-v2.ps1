#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-025 corrective execution - refresh A, verify, correlate, refresh B,
    row-level reproducibility. TWO REFRESHES. NO THIRD CYCLE.

.DESCRIPTION
    Runs only after apply-T-025b-outcome-producer-correction-v2.ps1 is green. The
    first thing this script does is ASSERT THE BUILD: it reads the live function
    definition and refuses to start unless the T-025b correction marker is there.
    A runner that depends on a build must prove the build before doing anything.

    WHAT IS EXPECTED, STATED BEFORE THE RUN SO THE RUN CAN REFUTE IT:
      defect.severity     - was 5,961 rows with category_value NULL on every one.
                            Should now carry the graded levels. Three levels across
                            5,961 rows should clear the 0.10 minority floor, so the
                            gate should let it run against 26 parameters that all
                            carry real variance. THIS IS THE ONLY OUTCOME THAT CAN
                            PRODUCE A CURRENT RESULT POPULATION.
      defect.class        - Disposition rows should be 0 and the population should
                            fall 7,844 -> 5,961. PREDICTED smallest class 119/5961
                            = 1.996 percent, still under the 0.03 floor, so it
                            should block again and supply the genuine analytical
                            refusal on corrected data. MEASURED, NOT ASSUMED.
      defect.rate_per_m2  - should be 0 rows. No authoritative m2 denominator
                            exists, so the false constant is no longer written.

    IF defect.severity DOES NOT PRODUCE FINDINGS, that is the finding to bring
    back. It is not something to work around with a third refresh.

    A ROW IS NOT A FINDING. NpgsqlAdvancedResultWriter emits one row per EXCLUDED
    feature as well - method 'NotApplicable', sample_size 0, and an evidence_json
    that marks it excluded. v1 counted rows in ml_correlation_results_v2 and
    reported PASS on 26 exclusion records. The gate now requires a CURRENT compute
    run plus method <> 'NotApplicable', sample_size > 0, coefficient populated and
    no excluded flag. Exclusion rows remain valid engine evidence of what was
    refused; they are counted and reported separately and never as findings.

    THE JULY ROWS ARE QUARANTINED BY DEFAULT, as approved. Result rows whose
    compute run predates this execution are deleted; the compute-run records are
    preserved so the history stays clearly historical. -NoQuarantine opts out, in
    which case every reading surface must filter by compute run before closure.

    FROZEN AND NOT TOUCHED: refresh_run_id, run ownership, FK constraints, engine
    identity and version, the NOT NULL invariant, the authenticated execution
    path, and NpgsqlFeatureVectorLoader. No learning job is enabled. Risk is NOT
    re-run - the existing 500 scores are reported against the eligible population.

    EXPECT ROUGHLY 10 TO 15 MINUTES. Two refreshes at 2 to 5 minutes each. Ask the
    parallel T-033 worker to hold off on full builds and full suites while it runs.

.EXAMPLE
    .\tools\run\Invoke-PpiqT025Corrective-v2.ps1
    .\tools\run\Invoke-PpiqT025Corrective-v2.ps1 -NoQuarantine
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
    [int]   $RefreshTimeoutSec = 1800,
    [switch]$NoQuarantine,
    [switch]$SkipVacuum
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

$MarkerB     = "PPIQ T-025b outcome producer correction"
$MarkerC     = "PPIQ T-025c insert-time lineage"
$SnapTable   = "ppiq_t025_repro_snapshot_a"
$RunStartUtc = (Get-Date).ToUniversalTime().ToString("yyyy-MM-dd HH:mm:ss")

$script:log = ""
$script:Token = $null
$script:Gate = @{}
$script:Refusals = New-Object System.Collections.ArrayList

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
function Get-Scalar {
    param([string]$Sql, [string]$Tag)
    $l = Get-SqlLines -Sql $Sql -Tag $Tag
    if ($l.Count -eq 0) { return $null }
    return $l[0]
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
function Short { param([string]$T, [int]$N = 200)
    if ($null -eq $T) { return "" }
    $t = ($T -replace "`r", "" -replace "`n", " ")
    if ($t.Length -le $N) { return $t }
    return $t.Substring(0, $N)
}
function Invoke-Refresh {
    param([string]$Label)
    Say ("  POST /api/ml/foundation/feature-store/refresh  windowDays=" + $WindowDays)
    $t0 = Get-Date
    $r = Invoke-Api -Method POST -Path "/api/ml/foundation/feature-store/refresh" `
                    -Body @{ windowDays = $WindowDays } -TimeoutSec $RefreshTimeoutSec
    $secs = [math]::Round(((Get-Date) - $t0).TotalSeconds, 1)
    Say ("  -> " + $r.Status + " in " + $secs + "s")
    Say ("     " + (Short $r.Raw 260))
    return $r
}

Rule "PPIQ T-025 CORRECTIVE EXECUTION v2"
$repoRoot = (Get-Location).Path
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t025corr_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
Say ("API      : " + $ApiBase)
Say ("Database : " + $Database)
Say ("Started  : " + $RunStartUtc + " UTC - results at or after this are CURRENT")
$bad = 0

try {
    # -------------------------------------------------------------- 0
    Rule "0 - ASSERT THE BUILD BEFORE DOING ANYTHING"
    $marked = Get-Scalar -Tag "marker" -Sql @"
SELECT count(*)::text
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname = 'public' AND p.proname = 'ppiq_ml_refresh_feature_store'
  AND p.prokind = 'f'
  AND pg_get_functiondef(p.oid) LIKE '%$MarkerB%'
  AND pg_get_functiondef(p.oid) LIKE '%$MarkerC%';
"@
    Say ("  base carrying BOTH T-025b and T-025c : " + $marked + " (required 1)")
    if ($marked -ne "1") {
        Say "[STOP] the corrected producer is not installed. Apply T-025b v2 then T-025c."
        throw "build assertion failed"
    }
    $markedV6 = Get-Scalar -Tag "markerv6" -Sql @"
SELECT count(*)::text
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname = 'public' AND p.proname = 'ppiq_ml_refresh_feature_store_v6'
  AND p.prokind = 'f'
  AND pg_get_functiondef(p.oid) LIKE '%$MarkerC%';
"@
    Say ("  v6 carrying T-025c                   : " + $markedV6 + " (required 1)")
    if ($markedV6 -ne "1") {
        Say "[STOP] the v6 producer still lacks insert-time lineage."
        throw "build assertion failed"
    }

    Rule "0b - AUTHENTICATE"
    $login = Invoke-Api -Method POST -Path "/auth/login" `
                        -Body @{ userName = $SmokeUser; password = $SmokePassword }
    if ($login.Status -ne 200) { Say ("[FAIL] login " + $login.Status); throw "auth" }
    $script:Token = $login.Body.accessToken
    Say ("[OK] token length " + $script:Token.Length)
    $script:Gate["Authenticated product path"] = "PASS"

    # -------------------------------------------------------------- A
    Rule "A - CORRECTED REFRESH A"
    Say "The producer self-clears both value tables, so this is idempotent."
    $ra = Invoke-Refresh -Label "A"
    if ($ra.Status -ne 200) {
        Say "[FAIL] refresh A did not return 200. Stopping rather than starting a"
        Say "       third cycle. The function may still have committed - the"
        Say "       populations below will show what actually landed."
        $bad = $bad + 1
    }

    # -------------------------------------------------------------- B
    Rule "B - THE THREE OUTCOME POPULATIONS, MEASURED"
    $pop = Invoke-Sql -Tag "pop" -Sql @"
SELECT outcome_key,
       grain,
       count(*)               AS rows,
       count(numeric_value)   AS numeric_not_null,
       count(category_value)  AS category_not_null,
       count(severity_value)  AS severity_not_null,
       count(DISTINCT heat_id) AS independent_heats
FROM public.ml_outcome_values
GROUP BY outcome_key, grain
ORDER BY outcome_key;
"@
    Say $pop.Output

    Say ""
    Say "B1 - defect.severity must now be loader-visible"
    $sevRows = Get-Scalar -Tag "sevrows" -Sql @"
SELECT count(*)::text FROM public.ml_outcome_values
WHERE lower(outcome_key) = 'defect.severity' AND grain = 'coil';
"@
    $sevCat = Get-Scalar -Tag "sevcat" -Sql @"
SELECT count(category_value)::text FROM public.ml_outcome_values
WHERE lower(outcome_key) = 'defect.severity' AND grain = 'coil';
"@
    $sevClasses = Get-Scalar -Tag "sevclasses" -Sql @"
SELECT count(*)::text FROM (
  SELECT DISTINCT category_value FROM public.ml_outcome_values
  WHERE lower(outcome_key) = 'defect.severity' AND grain = 'coil'
    AND category_value IS NOT NULL) q;
"@
    Say ("  severity rows                 : " + $sevRows + " (required above 0)")
    Say ("  category_value populated      : " + $sevCat + " (required above 0)")
    Say ("  real category count           : " + $sevClasses + " (required at least 2)")
    $sevDist = Invoke-Sql -Tag "sevdist" -Sql @"
SELECT COALESCE(category_value, '(null)') AS category, count(*) AS rows,
       round(100.0 * count(*) / SUM(count(*)) OVER (), 3) AS pct
FROM public.ml_outcome_values
WHERE lower(outcome_key) = 'defect.severity' AND grain = 'coil'
GROUP BY category_value ORDER BY count(*) ASC;
"@
    Say $sevDist.Output
    if ([int]$sevRows -le 0 -or [int]$sevCat -le 0 -or [int]$sevClasses -lt 2) { $bad = $bad + 1 }

    Say "B2 - defect.class must be free of the event_type fallback"
    $dispo = Get-Scalar -Tag "dispo" -Sql @"
SELECT count(*)::text FROM public.ml_outcome_values
WHERE lower(outcome_key) = 'defect.class' AND grain = 'coil'
  AND category_value = 'Disposition';
"@
    Say ("  Disposition rows in defect.class : " + $dispo + " (required 0)")
    if ($dispo -ne "0") { $bad = $bad + 1 }
    $clsDist = Invoke-Sql -Tag "clsdist" -Sql @"
SELECT COALESCE(category_value, '(null)') AS category, count(*) AS rows,
       round(100.0 * count(*) / SUM(count(*)) OVER (), 3) AS pct
FROM public.ml_outcome_values
WHERE lower(outcome_key) = 'defect.class' AND grain = 'coil'
GROUP BY category_value ORDER BY count(*) ASC;
"@
    Say $clsDist.Output
    Say "  Minority fraction recomputed the way MinorityFraction computes it"
    Say "  (a single class returns 0.0 via its g.Count < 2 branch):"
    $minShare = Get-SqlLines -Tag "minshare" -Sql @"
SELECT outcome_key || '~' || classes::text || '~' ||
       CASE WHEN classes < 2 THEN '0.00000' ELSE to_char(min_share, 'FM0.00000') END
FROM (
  SELECT outcome_key, count(*)::int AS classes, min(share) AS min_share
  FROM (
    SELECT outcome_key, COALESCE(category_value, '') AS cat,
           count(*)::numeric / SUM(count(*)) OVER (PARTITION BY outcome_key) AS share
    FROM public.ml_outcome_values
    WHERE lower(outcome_key) IN ('defect.class','defect.severity') AND grain = 'coil'
    GROUP BY outcome_key, COALESCE(category_value, '')) s
  GROUP BY outcome_key) t
ORDER BY outcome_key;
"@
    Say ("  " + "outcome_key".PadRight(22) + "classes".PadRight(10) + "min share".PadRight(13) + "predicted gate")
    foreach ($line in $minShare) {
        $p = $line -split "~"
        if ($p.Count -ne 3) { continue }
        $share = [double]$p[2]
        if ($share -ge 0.10) { $v = "Ready" } elseif ($share -ge 0.03) { $v = "Partial - runs" } else { $v = "BLOCKED" }
        Say ("  " + $p[0].PadRight(22) + $p[1].PadRight(10) + $p[2].PadRight(13) + $v)
    }
    Say "  This is a PREDICTION of the gate. Section C is the engine's actual verdict."

    Say "B3 - defect.rate_per_m2 must no longer be materialised"
    $rate = Get-Scalar -Tag "rate" -Sql @"
SELECT count(*)::text FROM public.ml_outcome_values
WHERE lower(outcome_key) = 'defect.rate_per_m2';
"@
    Say ("  rate_per_m2 rows : " + $rate + " (required 0)")
    Say "  No authoritative m2 denominator exists in the canonical inputs, so the"
    Say "  false constant is no longer written. The definition row remains and the"
    Say "  outcome reports as NOT MATERIALISED, like the other five."
    if ($rate -ne "0") { $bad = $bad + 1 }

    $script:Gate["Outcome populations"] = "PASS"
    if ($bad -gt 0) { $script:Gate["Outcome populations"] = "FAIL" }

    # -------------------------------------------------------------- C
    Rule "C - CURRENT CORRELATION, ONE CALL PER ACTIVE DEFINITION"
    $defs = Get-SqlLines -Tag "defs" -Sql @"
SELECT outcome_key || '|' || grain
FROM public.ml_outcome_definitions
WHERE is_deleted = false AND status = 'Active'
ORDER BY outcome_key;
"@
    $withResults = 0
    foreach ($d in $defs) {
        $parts = $d -split "\|"
        $key = $parts[0]
        $grain = "coil"
        if ($parts.Count -gt 1 -and -not [string]::IsNullOrWhiteSpace($parts[1])) { $grain = $parts[1] }
        $r = Invoke-Api -Method POST -Path "/api/ml/foundation/compute/correlation" `
                        -Body @{ outcomeKey = $key; grain = $grain; windowDays = $WindowDays }
        $st = ""; $cnt = ""; $msg = ""
        if ($null -ne $r.Body) {
            if ($r.Body.PSObject.Properties.Name -contains "status")      { $st = [string]$r.Body.status }
            if ($r.Body.PSObject.Properties.Name -contains "resultCount") { $cnt = [string]$r.Body.resultCount }
            if ($r.Body.PSObject.Properties.Name -contains "message")     { $msg = [string]$r.Body.message }
        }
        Say ("  " + $key.PadRight(28) + $grain.PadRight(10) + "-> " + $r.Status + "  " +
             $st.PadRight(10) + " results " + $cnt)
        if ($msg) { Say ("      " + (Short $msg 150)) }
        if ($r.Status -eq 200 -and ($st -eq "Blocked" -or $st -eq "NoData")) {
            [void]$script:Refusals.Add($key + " -> " + $st + " : " + (Short $msg 140))
        }
        if ($r.Status -eq 200 -and $st -eq "Ok") { $withResults = $withResults + 1 }
    }
    Say ""
    Say ("  outcomes producing results : " + $withResults)

    # -------------------------------------------------------------- D
    Rule "D - CURRENT VERSUS HISTORICAL RESULT POPULATION"
    Say "The 26 July results do not count toward T-025. Current means a compute run"
    Say ("completed at or after " + $RunStartUtc + " UTC.")
    $split = Invoke-Sql -Tag "split" -Sql @"
SELECT CASE WHEN c.completed_at_utc >= TIMESTAMPTZ '$RunStartUtc+00' THEN 'CURRENT' ELSE 'historical' END AS era,
       c.status,
       count(DISTINCT c.id) AS runs,
       count(r.id)          AS results
FROM public.ml_correlation_compute_runs c
LEFT JOIN public.ml_correlation_results_v2 r ON r.compute_run_id = c.id
GROUP BY 1, 2
ORDER BY 1 DESC, 2;
"@
    Say $split.Output
    # A ROW IN ml_correlation_results_v2 IS NOT A FINDING. The writer emits one
    # row per EXCLUDED feature too - method 'NotApplicable', sample_size 0,
    # evidence_json {"excluded": true}. Counting the table was the wrong
    # assertion in v1 and reported PASS on 26 exclusion records.
    $curFindings = Get-Scalar -Tag "curfindings" -Sql @"
SELECT count(*)::text
FROM public.ml_correlation_results_v2 r
JOIN public.ml_correlation_compute_runs c ON c.id = r.compute_run_id
WHERE c.completed_at_utc >= TIMESTAMPTZ '$RunStartUtc+00'
  AND r.method IS DISTINCT FROM 'NotApplicable'
  AND r.sample_size > 0
  AND r.coefficient IS NOT NULL
  AND COALESCE((r.evidence_json->>'excluded')::boolean, false) = false;
"@
    $curExclusions = Get-Scalar -Tag "curexcl" -Sql @"
SELECT count(*)::text
FROM public.ml_correlation_results_v2 r
JOIN public.ml_correlation_compute_runs c ON c.id = r.compute_run_id
WHERE c.completed_at_utc >= TIMESTAMPTZ '$RunStartUtc+00'
  AND (r.method = 'NotApplicable' OR r.sample_size = 0
       OR COALESCE((r.evidence_json->>'excluded')::boolean, false) = true);
"@
    $histRows = Get-Scalar -Tag "histrows" -Sql @"
SELECT count(r.id)::text
FROM public.ml_correlation_results_v2 r
JOIN public.ml_correlation_compute_runs c ON c.id = r.compute_run_id
WHERE c.completed_at_utc < TIMESTAMPTZ '$RunStartUtc+00';
"@
    Say ""
    Say ("  CURRENT analytical FINDINGS        : " + $curFindings + " (required above 0)")
    Say ("  CURRENT exclusion/refusal records  : " + $curExclusions + " (valid evidence, not findings)")
    Say ("  historical rows of any kind        : " + $histRows)
    Say "  A finding carries a method, a coefficient and a sample size above zero."
    Say "  NotApplicable / sample_size 0 / excluded=true rows are engine evidence of"
    Say "  what was refused, and they never count toward the current population."
    if ([int]$curFindings -le 0) {
        $script:Gate["Current analytical findings"] = "FAIL - zero current findings"
        $bad = $bad + 1
    } else {
        $script:Gate["Current analytical findings"] = ("PASS - " + $curFindings + " findings")
    }

    if ($NoQuarantine) {
        Say ""
        Say "  -NoQuarantine WAS PASSED. Historical rows are left in place and must be"
        Say "  filtered by compute run on every reading surface before closure."
    } else {
        Say ""
        Say "  QUARANTINING the historical result rows, as approved. Compute-run"
        Say "  metadata is preserved so the history remains clearly historical."
        $q = Invoke-Sql -Tag "quarantine" -Sql @"
BEGIN;
DELETE FROM public.ml_correlation_results_v2 r
USING public.ml_correlation_compute_runs c
WHERE c.id = r.compute_run_id
  AND c.completed_at_utc < TIMESTAMPTZ '$RunStartUtc+00';
COMMIT;
"@
        Say $q.Output
        $left = Get-Scalar -Tag "histleft" -Sql @"
SELECT count(r.id)::text
FROM public.ml_correlation_results_v2 r
JOIN public.ml_correlation_compute_runs c ON c.id = r.compute_run_id
WHERE c.completed_at_utc < TIMESTAMPTZ '$RunStartUtc+00';
"@
        Say ("  historical result rows remaining   : " + $left + " (required 0)")
        if ($left -ne "0") { $bad = $bad + 1 }
        $runsLeft = Get-Scalar -Tag "runsleft" -Sql @"
SELECT count(*)::text FROM public.ml_correlation_compute_runs
WHERE completed_at_utc < TIMESTAMPTZ '$RunStartUtc+00';
"@
        Say ("  historical compute runs preserved  : " + $runsLeft + " (history, not results)")
    }

    # -------------------------------------------------------------- E
    Rule "E - RISK AND LEARNING, REPORTED NOT RE-RUN"
    $riskRows = Get-Scalar -Tag "riskrows" -Sql @"
SELECT count(*)::text FROM public.risk_scores WHERE is_deleted = false;
"@
    $eligible = Get-Scalar -Tag "eligible" -Sql @"
SELECT count(*)::text FROM public.material_units WHERE is_deleted = false;
"@
    Say ("  risk scores evaluated              : " + $riskRows)
    Say ("  eligible / current population      : " + $eligible)
    Say "  THIS IS A BOUNDED ENGINE-EXECUTION PROOF, NOT FULL RISK COVERAGE."
    Say "  No customer-visible surface may claim full-population risk coverage on"
    Say "  the strength of this number."
    $script:Gate["Risk"] = ("BOUNDED PROOF - " + $riskRows + " of " + $eligible)

    $learn = Invoke-Sql -Tag "learn" -Sql @"
SELECT is_enabled, count(*) AS jobs FROM public.ml_learning_job_catalog_v1
GROUP BY is_enabled ORDER BY is_enabled;
"@
    Say $learn.Output
    Say "  The catalogue exists and no job is enabled. That is a configuration"
    Say "  state. No job was enabled by this script."
    $script:Gate["Learning"] = "NOT CONFIGURED - catalogue present, 0 enabled"

    # -------------------------------------------------------------- F
    Rule "F - SNAPSHOT A FOR THE REPRODUCIBILITY PROOF"
    Say "Row identity + categorical value + ROUND(numeric_value, 6). An aggregate"
    Say "can hide offsetting row changes; EXCEPT in both directions cannot."
    $snap = Invoke-Sql -Tag "snap" -Sql @"
DROP TABLE IF EXISTS public.$SnapTable;
CREATE UNLOGGED TABLE public.$SnapTable AS
SELECT 'F'::text AS side, feature_key AS k, grain, effective_sample_key AS sk,
       observed_at_utc, category_value, round(numeric_value::numeric, 6) AS nv
FROM public.ml_feature_values
UNION ALL
SELECT 'O'::text, outcome_key, grain, effective_sample_key,
       observed_at_utc, category_value, round(numeric_value::numeric, 6)
FROM public.ml_outcome_values;
SELECT count(*) AS snapshot_a_rows FROM public.$SnapTable;
"@
    Say $snap.Output

    if (-not $SkipVacuum) {
        Rule "F2 - VACUUM THE TWO VALUE TABLES"
        Say "Several DELETE/INSERT cycles leave these tables bloated. This is cheap"
        Say "maintenance before the second refresh. It is NOT claimed as the cause of"
        Say "any earlier timing drift - that was never proven."
        $v = Invoke-Sql -Tag "vacuum" -Sql @"
VACUUM (ANALYZE) public.ml_feature_values;
VACUUM (ANALYZE) public.ml_outcome_values;
"@
        if ($v.ExitCode -eq 0) { Say "  [OK] vacuumed" } else { Say ("  [WARN] " + $v.Error.Trim()) }
    }

    # -------------------------------------------------------------- G
    Rule "G - REFRESH B AND THE ROW-LEVEL REPRODUCIBILITY PROOF"
    $rb = Invoke-Refresh -Label "B"
    if ($rb.Status -ne 200) {
        Say "[FAIL] refresh B did not return 200. The reproducibility proof cannot be"
        Say "       completed and this is NOT retried - that would be a third cycle."
        $script:Gate["Reproducibility"] = ("FAIL - refresh B HTTP " + $rb.Status)
        $bad = $bad + 1
    } else {
        $cmp = Invoke-Sql -Tag "compare" -Sql @"
WITH b AS (
    SELECT 'F'::text AS side, feature_key AS k, grain, effective_sample_key AS sk,
           observed_at_utc, category_value, round(numeric_value::numeric, 6) AS nv
    FROM public.ml_feature_values
    UNION ALL
    SELECT 'O'::text, outcome_key, grain, effective_sample_key,
           observed_at_utc, category_value, round(numeric_value::numeric, 6)
    FROM public.ml_outcome_values
)
SELECT 'A EXCEPT B' AS check_name, count(*)::int AS found, 0 AS required
FROM (SELECT * FROM public.$SnapTable EXCEPT SELECT * FROM b) x
UNION ALL
SELECT 'B EXCEPT A', count(*)::int, 0
FROM (SELECT * FROM b EXCEPT SELECT * FROM public.$SnapTable) y;
"@
        Say $cmp.Output
        $rows = Get-SqlLines -Tag "compareraw" -Sql @"
SELECT check_name || '~' || found::text FROM (
WITH b AS (
    SELECT 'F'::text AS side, feature_key AS k, grain, effective_sample_key AS sk,
           observed_at_utc, category_value, round(numeric_value::numeric, 6) AS nv
    FROM public.ml_feature_values
    UNION ALL
    SELECT 'O'::text, outcome_key, grain, effective_sample_key,
           observed_at_utc, category_value, round(numeric_value::numeric, 6)
    FROM public.ml_outcome_values
)
SELECT 'A EXCEPT B' AS check_name, count(*)::int AS found
FROM (SELECT * FROM public.$SnapTable EXCEPT SELECT * FROM b) x
UNION ALL
SELECT 'B EXCEPT A', count(*)::int
FROM (SELECT * FROM b EXCEPT SELECT * FROM public.$SnapTable) y
) q;
"@
        $seen = 0; $diff = 0
        foreach ($line in $rows) {
            $p = $line -split "~"
            if ($p.Count -ne 2) { continue }
            $seen = $seen + 1
            if ([int]$p[1] -ne 0) { $diff = $diff + [int]$p[1] }
        }
        Say ("  parsed comparison rows : " + $seen + " (required 2)")
        if ($seen -ne 2) {
            Say "[FAIL] the comparison did not parse. Refusing to report PASS on unparsed output."
            $script:Gate["Reproducibility"] = "FAIL - unparsed"
            $bad = $bad + 1
        } elseif ($diff -ne 0) {
            $script:Gate["Reproducibility"] = ("FAIL - " + $diff + " differing rows")
            $bad = $bad + 1
        } else {
            $script:Gate["Reproducibility"] = "PASS - 0 in both directions"
        }
    }

    # -------------------------------------------------------------- H
    Rule "H - THE FROZEN INVARIANTS STILL HOLD"
    $inv = Invoke-Sql -Tag "invariants" -Sql @"
SELECT 'feature values without a run' AS check_name, count(*)::int AS found, 0 AS required
FROM public.ml_feature_values WHERE refresh_run_id IS NULL
UNION ALL SELECT 'outcome values without a run', count(*)::int, 0
FROM public.ml_outcome_values WHERE refresh_run_id IS NULL
UNION ALL SELECT 'correlation results without a compute run', count(*)::int, 0
FROM public.ml_correlation_results_v2 r
LEFT JOIN public.ml_correlation_compute_runs c ON c.id = r.compute_run_id
WHERE c.id IS NULL
UNION ALL SELECT 'refresh_run_id nullable on feature values', count(*)::int, 0
FROM information_schema.columns
WHERE table_schema='public' AND table_name='ml_feature_values'
  AND column_name='refresh_run_id' AND is_nullable='YES'
UNION ALL SELECT 'refresh_run_id nullable on outcome values', count(*)::int, 0
FROM information_schema.columns
WHERE table_schema='public' AND table_name='ml_outcome_values'
  AND column_name='refresh_run_id' AND is_nullable='YES';
"@
    Say $inv.Output
    $invRows = Get-SqlLines -Tag "invraw" -Sql @"
SELECT check_name || '~' || found::text FROM (
SELECT 'a' AS check_name, count(*)::int AS found FROM public.ml_feature_values WHERE refresh_run_id IS NULL
UNION ALL SELECT 'b', count(*)::int FROM public.ml_outcome_values WHERE refresh_run_id IS NULL
UNION ALL SELECT 'c', count(*)::int FROM public.ml_correlation_results_v2 r
  LEFT JOIN public.ml_correlation_compute_runs c ON c.id = r.compute_run_id WHERE c.id IS NULL
UNION ALL SELECT 'd', count(*)::int FROM information_schema.columns
  WHERE table_schema='public' AND table_name='ml_feature_values' AND column_name='refresh_run_id' AND is_nullable='YES'
UNION ALL SELECT 'e', count(*)::int FROM information_schema.columns
  WHERE table_schema='public' AND table_name='ml_outcome_values' AND column_name='refresh_run_id' AND is_nullable='YES'
) q;
"@
    $invSeen = 0; $invBad = 0
    foreach ($line in $invRows) {
        $p = $line -split "~"
        if ($p.Count -ne 2) { continue }
        $invSeen = $invSeen + 1
        if ([int]$p[1] -ne 0) { $invBad = $invBad + 1 }
    }
    Say ("  parsed invariant rows : " + $invSeen + " (required 5)")
    if ($invSeen -ne 5 -or $invBad -gt 0) {
        $script:Gate["Frozen invariants"] = "FAIL"
        $bad = $bad + 1
    } else {
        $script:Gate["Frozen invariants"] = "PASS"
    }

    # -------------------------------------------------------------- I
    Rule "I - THE GENUINE REFUSAL, ON CORRECTED DATA"
    if ($script:Refusals.Count -gt 0) {
        Say ("  " + $script:Refusals.Count + " engine refusal(s) on the corrected population:")
        foreach ($r in $script:Refusals) { Say ("    " + $r) }
        $script:Gate["Genuine refusal"] = ("PASS - " + $script:Refusals.Count + " on corrected data")
    } else {
        Say "  No engine refused. If every outcome now runs, the honest-refusal"
        Say "  requirement has no evidence and that is a finding, not a pass."
        $script:Gate["Genuine refusal"] = "NONE - requirement unmet"
    }
}
catch {
    Say ("[ERROR] " + $_.Exception.Message)
    $bad = $bad + 1
}
finally {
    Invoke-Sql -Tag "cleanup" -Sql "DROP TABLE IF EXISTS public.$SnapTable;" | Out-Null
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

Rule "T-025 CORRECTIVE GATE"
foreach ($k in @("Authenticated product path", "Outcome populations",
                 "Current analytical findings", "Risk", "Learning",
                 "Reproducibility", "Frozen invariants", "Genuine refusal")) {
    $v = "NOT REACHED"
    if ($script:Gate.ContainsKey($k)) { $v = $script:Gate[$k] }
    Say ("  " + $k.PadRight(32) + $v)
}
Say ""
Say "TWO REFRESHES RAN. NO THIRD CYCLE. The outcome producer is frozen again."

$outFolder = Join-Path $repoRoot "docs\m1\evidence"
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$ev = Join-Path $outFolder ("T-025_corrective_v2_" + $stamp + ".txt")
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
