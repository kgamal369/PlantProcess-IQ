<#
.SYNOPSIS
    Diagnose-ReadinessBlock.ps1 - proves WHY every governed correlation run returns
    status=Blocked, by reconstructing the five readiness dimensions the engine
    computes and then discards.

.DESCRIPTION
    Evidence chain:

      ReadinessGate.cs                  five dimensions, thresholds
      NpgsqlFeatureVectorLoader.cs      builds the dataset with  WHERE grain = @g
      AdvancedCorrelationComputeService.cs:210   builds "reasons" from the gate
      DotNetAdvancedCorrelationEngine.cs:44      DROPS the reasons at the DTO
                                                 boundary (CorrelationComputeResult
                                                 has no ReadinessReasons member)

    Because the reasons never reach the caller and are never persisted, a blocked
    run is indistinguishable from any other blocked run. This script recovers the
    same numbers straight from the database, per grain, so the blocking dimension
    is named.

    Read-only. Executes no writes. Does not touch the API unless -Execute is given.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Diagnose-ReadinessBlock.ps1

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Diagnose-ReadinessBlock.ps1 -Execute -Grain coil
#>

[CmdletBinding()]
param(
    [string]$Database    = 'ppiq_presentation',
    [string]$DbHost      = '127.0.0.1',
    [int]   $Port        = 5432,
    [string]$DbUser      = 'ppiq_dev',
    [string]$DbPassword  = 'ppiq_dev_local_only',
    [string]$ApiBase     = 'http://localhost:5063',
    [string]$OutcomeKey  = 'kpi.prime_yield',
    [int]   $WindowDays  = 120,
    [string]$Grain       = 'coil',
    [string]$User        = 'e2eadmin',
    [string]$Password    = 'E2EAdmin123!',
    [switch]$Execute
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$stamp   = Get-Date -Format 'yyyyMMdd_HHmmss'
$report  = Join-Path (Get-Location) ("ReadinessBlock_" + $stamp + ".txt")
$lines   = New-Object System.Collections.Generic.List[string]

function W([string]$t = '') {
    $lines.Add($t)
    Write-Host $t
}
function Save {
    $enc = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($report, (($lines -join "`r`n") + "`r`n"), $enc)
    Write-Host ''
    Write-Host ("Report: " + $report) -ForegroundColor Cyan
}

# ---- preflight: locate psql -------------------------------------------------

function Resolve-Psql {
    $cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    $roots = @(
        'C:\Program Files\PostgreSQL',
        'C:\Program Files (x86)\PostgreSQL'
    )
    foreach ($r in $roots) {
        if (-not (Test-Path $r)) { continue }
        $hit = Get-ChildItem -Path $r -Filter psql.exe -Recurse -ErrorAction SilentlyContinue |
               Sort-Object FullName -Descending | Select-Object -First 1
        if ($hit) { return $hit.FullName }
    }
    return $null
}

$psql = Resolve-Psql
if (-not $psql) {
    Write-Host 'PREFLIGHT FAIL: psql.exe not found on PATH or under C:\Program Files\PostgreSQL.' -ForegroundColor Red
    exit 2
}

$conninfo = "host=$DbHost port=$Port dbname=$Database user=$DbUser password=$DbPassword"

function Q([string]$sql) {
    $out = & $psql -v ON_ERROR_STOP=1 -X -q -A -F '|' -t -d $conninfo -c $sql 2>&1
    if ($LASTEXITCODE -ne 0) { return @("QUERY FAILED: " + ($out -join ' ')) }
    return @($out | Where-Object { $_ -ne '' })
}

W '=============================================================================='
W ("READINESS BLOCK DIAGNOSIS - " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ("DB: " + $Database + "   psql: " + $psql)
W '=============================================================================='
W ''

# ---- preflight: connectivity ------------------------------------------------

$ping = Q 'SELECT 1;'
if ($ping -match 'QUERY FAILED') {
    W ("PREFLIGHT FAIL: cannot reach " + $Database + " -> " + ($ping -join ' '))
    Save; exit 2
}
W ('[PREFLIGHT] connection to ' + $Database + ' OK')
W ''

# ---- 1. the grain key space -------------------------------------------------

W '[1] GRAIN KEY SPACE - ml_outcome_values'
W '    (the loader filters WHERE grain = @g exactly; a grain with no rows'
W '     yields an empty dataset and blocks all five gate dimensions)'
W ''
W ('    ' + 'grain'.PadRight(14) + 'rows'.PadLeft(10) + 'heat_id'.PadLeft(10) + '  distinct_outcome_keys')
foreach ($r in (Q @'
SELECT grain, count(*), count(heat_id), count(DISTINCT outcome_key)
FROM public.ml_outcome_values GROUP BY grain ORDER BY 2 DESC;
'@)) {
    $c = $r -split '\|'
    if ($c.Count -ge 4) { W ('    ' + $c[0].PadRight(14) + $c[1].PadLeft(10) + $c[2].PadLeft(10) + '  ' + $c[3]) }
    else { W ('    ' + $r) }
}
W ''
W '    ml_feature_values'
W ('    ' + 'grain'.PadRight(14) + 'rows'.PadLeft(10) + '  distinct_feature_keys')
foreach ($r in (Q @'
SELECT grain, count(*), count(DISTINCT feature_key)
FROM public.ml_feature_values GROUP BY grain ORDER BY 2 DESC;
'@)) {
    $c = $r -split '\|'
    if ($c.Count -ge 3) { W ('    ' + $c[0].PadRight(14) + $c[1].PadLeft(10) + '  ' + $c[2]) }
    else { W ('    ' + $r) }
}
W ''

$materialRows = (Q "SELECT count(*) FROM public.ml_outcome_values WHERE grain = 'material';")[0]
W ('    grain=material rows: ' + $materialRows)
if ($materialRows -eq '0') {
    W '    >>> Run-GoldenAnalysis.ps1:47 defaults -Grain to "material".'
    W '    >>> No row in the feature store carries that grain. Every governed run'
    W '    >>> therefore loads zero outcomes and zero features, and the gate blocks'
    W '    >>> on all five dimensions. This is a harness defect, not an engine defect.'
}
W ''

# ---- 2. outcome catalog vs populated keys -----------------------------------

W '[2] CATALOG vs POPULATED - outcome keys defined but never materialised'
foreach ($r in (Q @'
SELECT od.outcome_key,
       od.outcome_type,
       COALESCE((SELECT count(*) FROM public.ml_outcome_values ov
                 WHERE lower(ov.outcome_key) = lower(od.outcome_key)), 0)
FROM public.ml_outcome_definitions od
WHERE od.is_deleted = false ORDER BY 1;
'@)) {
    $c = $r -split '\|'
    if ($c.Count -ge 3) {
        $flag = ''
        if ($c[2] -eq '0') { $flag = '   <-- NO VALUES' }
        W ('    ' + $c[0].PadRight(26) + $c[1].PadRight(14) + $c[2].PadLeft(9) + $flag)
    } else { W ('    ' + $r) }
}
W ''

# ---- 3. reconstruct the five gate dimensions --------------------------------

W ('[3] READINESS DIMENSIONS reconstructed for outcomeKey=' + $OutcomeKey + '  grain=' + $Grain + '  windowDays=' + $WindowDays)
W '    thresholds from ReadinessGate.cs (Ready / Partial):'
W '      Independent heats        60 / 30'
W '      Outcome events           40 / 15'
W '      Minority-class balance   10% / 3%   (continuous outcomes short-circuit to 50% = Ready)'
W '      Freshness factor         hardcoded 0.0 by the loader = always Ready'
W '      Required completeness    95% / 85%'
W ''

$dim = Q ("
WITH w AS (
  SELECT COALESCE((SELECT max(observed_at_utc) FROM public.ml_outcome_values
                   WHERE lower(outcome_key)=lower('$OutcomeKey') AND grain='$Grain'),
                  'epoch'::timestamptz) - make_interval(days => $WindowDays) AS lo
),
o AS (
  SELECT effective_sample_key, heat_id
  FROM public.ml_outcome_values, w
  WHERE lower(outcome_key)=lower('$OutcomeKey') AND grain='$Grain'
    AND ($WindowDays >= 3650 OR observed_at_utc >= w.lo)
),
f AS (
  SELECT DISTINCT effective_sample_key
  FROM public.ml_feature_values
  WHERE grain='$Grain' AND missingness_flag=false
)
SELECT (SELECT count(DISTINCT heat_id) FROM o),
       (SELECT count(*) FROM o),
       (SELECT count(DISTINCT effective_sample_key) FROM o),
       (SELECT count(DISTINCT o.effective_sample_key) FROM o JOIN f USING (effective_sample_key));
")

if (@($dim | Where-Object { $_ -match 'QUERY FAILED' }).Count -gt 0) {
    W ('    ' + ($dim -join ' '))
} elseif (@($dim).Count -eq 0 -or [string]::IsNullOrWhiteSpace(($dim -join ''))) {
    W ('    no dataset rows returned for outcomeKey=' + $OutcomeKey + ' at grain=' + $Grain + '.')
    W ('    That itself is the block: the loader found zero outcomes for this pair,')
    W ('    so Independent-heats=0, Outcome-events=0, Completeness=0 - BLOCKED on all.')
    W ('    Try a populated pair, e.g. -OutcomeKey defect.rate_per_m2 -Grain coil.')
} else {
    $c = @($dim[0] -split '\|')
    while ($c.Count -lt 4) { $c += '0' }
    $heats = [int]$c[0]; $events = [int]$c[1]; $okeys = [int]$c[2]; $matched = [int]$c[3]
    $complete = 0.0
    if ($okeys -gt 0) { $complete = $matched / [double]$okeys }

    function Verdict([double]$v, [double]$ready, [double]$partial) {
        if ($v -ge $ready) { return 'Ready' }
        if ($v -ge $partial) { return 'Partial' }
        return 'BLOCKED'
    }

    W ('    Independent heats        ' + ([string]$heats).PadLeft(8)  + '   ' + (Verdict $heats 60 30))
    W ('    Outcome events           ' + ([string]$events).PadLeft(8) + '   ' + (Verdict $events 40 15))
    W ('    Required completeness    ' + ($complete.ToString('P1')).PadLeft(8) + '   ' + (Verdict $complete 0.95 0.85))
    W ('      (outcome sample keys ' + $okeys + ', of which ' + $matched + ' have >=1 feature sample)')
    W ('    Freshness factor             0.00   Ready   (loader hardcodes 0.0)')
    W ('    Minority-class balance    not reconstructable in SQL for this outcome type;')
    W ('                              continuous outcomes return 0.5 = Ready')
    W ''
    if ($heats -lt 30 -or $events -lt 15 -or $complete -lt 0.85) {
        W '    VERDICT: this grain/outcome pair WOULD BLOCK. Named dimensions above.'
    } else {
        W '    VERDICT: this grain/outcome pair would NOT block on the reconstructable dimensions.'
    }
}
W ''

# ---- 4. what the blocked runs recorded --------------------------------------

W '[4] WHAT THE BLOCKED RUNS ACTUALLY PERSISTED'
foreach ($r in (Q @'
SELECT status, count(*), max(completed_at_utc)::text, min(COALESCE(message,'(null)'))
FROM public.ml_correlation_compute_runs GROUP BY status ORDER BY 2 DESC;
'@)) {
    $c = $r -split '\|'
    if ($c.Count -ge 4) { W ('    ' + $c[0].PadRight(10) + $c[1].PadLeft(6) + '  last=' + $c[2] + '  msg="' + $c[3] + '"') }
    else { W ('    ' + $r) }
}
W ''
W '    Note: NpgsqlAdvancedResultWriter.cs:40 inserts message + request_json only.'
W '    The five readiness dimensions are never written. A blocked run cannot be'
W '    explained after the fact from the database alone. That is a product defect'
W '    independent of this diagnosis, and it contradicts the honest-abstain claim:'
W '    the gate refuses but cannot say why.'
W ''

# ---- 5. optional: re-run through the API at the correct grain ---------------

if (-not $Execute) {
    W '[5] DRY-RUN. Add -Execute to POST one governed run at the grain above and'
    W '    print the resulting status.'
    Save; exit 0
}

W ('[5] LIVE RUN via ' + $ApiBase)
try {
    $login = Invoke-RestMethod -Uri ($ApiBase + '/api/auth/login') -Method Post -TimeoutSec 20 `
        -ContentType 'application/json' -Body (@{ username = $User; password = $Password } | ConvertTo-Json)
    $tok = $null
    foreach ($k in @('accessToken','token','access_token')) {
        if ($login.PSObject.Properties[$k]) { $tok = $login.$k; break }
    }
    if (-not $tok) { throw 'no token in login response' }
    $H = @{ Authorization = 'Bearer ' + $tok }
    W '    [AUTH] OK'
} catch {
    W ('    [AUTH] FAILED: ' + $_.Exception.Message)
    Save; exit 1
}

try {
    $body = @{ outcomeKey = $OutcomeKey; grain = $Grain; windowDays = $WindowDays } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri ($ApiBase + '/api/ml/foundation/compute/correlation') -Method Post `
        -Headers $H -ContentType 'application/json' -Body $body -TimeoutSec 300
    W ('    outcomeKey     ' + $OutcomeKey)
    W ('    grain          ' + $Grain)
    foreach ($p in $res.PSObject.Properties) { W ('    ' + $p.Name.PadRight(15) + ' ' + $p.Value) }
    W ''
    if ("$($res.status)" -match '(?i)block') {
        W '    STILL BLOCKED at this grain. The dimension named in [3] is the cause.'
    } else {
        W '    NOT BLOCKED. The grain was the whole story - fix Run-GoldenAnalysis.ps1:47.'
    }
} catch {
    W ('    RUN FAILED: ' + $_.Exception.Message)
}

Save
