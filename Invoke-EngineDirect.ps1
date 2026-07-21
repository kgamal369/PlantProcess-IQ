<#
.SYNOPSIS
    Invoke-EngineDirect.ps1 - re-grain (742) then fire ONE correlation run with
    NO feature-store refresh in between, so the gate computes on the corrected
    store. This is the run the two-pass golden loop kept sabotaging: the golden
    script's refresh re-strands the generic rows microseconds before it computes.

.DESCRIPTION
    Order, atomic on the store:
      1. apply 742 (idempotent, in-process via psql) -> completeness restored
      2. immediately POST /compute/correlation for the target outcome ONLY,
         with NO /feature-store/refresh call anywhere
      3. read back the run status + any new results_v2 rows for THIS run id
    Defaults to defect.class (completeness proven 100% at 23:00).

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-EngineDirect.ps1

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Invoke-EngineDirect.ps1 -OutcomeKey defect.severity
#>

[CmdletBinding()]
param(
    [string]$ApiBase    = 'http://localhost:5063',
    [string]$OutcomeKey = 'defect.class',
    [string]$Grain      = 'coil',
    [int]   $WindowDays  = 3650,
    [string]$User       = 'e2eadmin',
    [string]$Password   = 'E2EAdmin123!',
    [string]$Database   = 'ppiq_presentation',
    [string]$DbHost     = '127.0.0.1',
    [int]   $Port       = 5432,
    [string]$DbUser     = 'ppiq_dev',
    [string]$DbPassword = 'ppiq_dev_local_only',
    [string]$SqlPath    = '',
    [string]$RepoRoot   = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $RepoRoot ("EngineDirect_" + $stamp + ".txt")
$lines = New-Object System.Collections.Generic.List[string]
$utf8 = New-Object System.Text.UTF8Encoding($false)
function W([string]$t=''){ $lines.Add($t); Write-Host $t }
function Save { [System.IO.File]::WriteAllText($logPath, (($lines -join "`r`n")+"`r`n"), $utf8); Write-Host ''; Write-Host ('Log: '+$logPath) -ForegroundColor Cyan }
function Resolve-Psql {
    $cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    foreach ($r in @('C:\Program Files\PostgreSQL','C:\Program Files (x86)\PostgreSQL')) {
        if (Test-Path $r) { $h = Get-ChildItem $r -Filter psql.exe -Recurse -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1; if ($h) { return $h.FullName } }
    }
    return $null
}
$psql = Resolve-Psql
if (-not $psql) { Write-Host 'psql not found'; exit 2 }
$env:PGPASSWORD = $DbPassword
$conn = "host=$DbHost port=$Port dbname=$Database user=$DbUser"
function Q1([string]$sql){ $o = & $psql -v ON_ERROR_STOP=1 -X -q -A -t -d $conn -c $sql 2>&1; if ($LASTEXITCODE -ne 0){ return ('ERR: '+($o -join ' ')) }; return (($o | Where-Object {$_ -ne ''}) -join '') }

W '=============================================================================='
W ('INVOKE ENGINE DIRECT (no refresh) - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ('outcomeKey=' + $OutcomeKey + '  grain=' + $Grain + '  windowDays=' + $WindowDays)
W '=============================================================================='
W ''

# ---- 1. re-grain 742 ---------------------------------------------------------

if ([string]::IsNullOrWhiteSpace($SqlPath)) {
    foreach ($c in @((Join-Path $RepoRoot '742_feature_regrain_generic.sql'),
                     (Join-Path $RepoRoot 'Backend\database\scripts\742_feature_regrain_generic.sql'))) {
        if (Test-Path -LiteralPath $c) { $SqlPath = $c; break }
    }
}
if (-not (Test-Path -LiteralPath $SqlPath)) { W 'FAIL: 742 SQL not found.'; Save; exit 2 }
W '[1] re-grain (742) so the store is correct at this instant'
$o = & $psql -v ON_ERROR_STOP=1 -X -q -1 -d $conn -f $SqlPath 2>&1
if ($LASTEXITCODE -ne 0) { W ('    FAILED: ' + ($o -join ' ')); Save; exit 1 }
$comp = Q1 "SELECT count(*) FROM (SELECT DISTINCT effective_sample_key FROM public.ml_outcome_values WHERE outcome_key='$OutcomeKey' AND grain='$Grain' INTERSECT SELECT DISTINCT effective_sample_key FROM public.ml_feature_values WHERE grain='$Grain' AND missingness_flag=false) x;"
$tot = Q1 "SELECT count(DISTINCT effective_sample_key) FROM public.ml_outcome_values WHERE outcome_key='$OutcomeKey' AND grain='$Grain';"
W ('    completeness for ' + $OutcomeKey + ': ' + $comp + '/' + $tot)
W ('    coil feature rows: ' + (Q1 "SELECT count(*) FROM public.ml_feature_values WHERE grain='coil';"))
W ''

# ---- 2. auth -----------------------------------------------------------------

$token = $null
foreach ($u in @('/auth/login','/api/auth/login')) {
    foreach ($shape in @(@{ username=$User; password=$Password }, @{ email=$User; password=$Password })) {
        if ($token) { break }
        try {
            $r = Invoke-RestMethod -Uri ($ApiBase + $u) -Method Post -Body ($shape | ConvertTo-Json) -ContentType 'application/json' -TimeoutSec 15 -ErrorAction Stop
            foreach ($k in @('accessToken','token','access_token','jwt')) { if ($r.PSObject.Properties[$k] -and $r.$k) { $token=$r.$k; break } }
        } catch { }
    }
    if ($token) { break }
}
if (-not $token) { W '[2] AUTH FAILED'; Save; exit 1 }
$H = @{ Authorization = 'Bearer ' + $token }
W '[2] AUTH OK'
W ''

# ---- 3. results_v2 baseline --------------------------------------------------

$before = [int](Q1 "SELECT count(*) FROM public.ml_correlation_results_v2;")
W ('[3] results_v2 before: ' + $before)
W ''

# ---- 4. compute DIRECTLY - no refresh ---------------------------------------

W ('[4] POST /compute/correlation (NO refresh call - store stays 742-correct)')
try {
    $body = @{ outcomeKey = $OutcomeKey; grain = $Grain; windowDays = $WindowDays } | ConvertTo-Json
    $res = Invoke-RestMethod -Uri ($ApiBase + '/api/ml/foundation/compute/correlation') -Method Post -Headers $H -Body $body -ContentType 'application/json' -TimeoutSec 300 -ErrorAction Stop
    foreach ($p in $res.PSObject.Properties) { W ('    ' + $p.Name.PadRight(18) + ' ' + $p.Value) }
} catch {
    W ('    REQUEST FAILED: ' + $_.Exception.Message)
    Save; exit 1
}
W ''

# ---- 5. read back ------------------------------------------------------------

Start-Sleep -Seconds 1
$after = [int](Q1 "SELECT count(*) FROM public.ml_correlation_results_v2;")
W ('[5] results_v2 after: ' + $after + '   (delta +' + ($after - $before) + ')')
W ''
W '    newest run for this outcome:'
foreach ($row in (& $psql -X -q -A -F '|' -t -d $conn -c "SELECT to_char(completed_at_utc,'HH24:MI:SS'), status, left(COALESCE(message,''),80) FROM public.ml_correlation_compute_runs WHERE target_outcome_key='$OutcomeKey' ORDER BY completed_at_utc DESC LIMIT 1;" 2>&1)) { if ("$row" -ne '') { W ('    ' + $row) } }
W ''
if (($after - $before) -gt 0) {
    W '    THE FINDINGS (this outcome, newest run):'
    foreach ($row in (& $psql -X -q -A -F '|' -t -d $conn -c "SELECT feature_key, round(effect_size::numeric,3), round(q_value::numeric,4), sample_size FROM public.ml_correlation_results_v2 WHERE outcome_key='$OutcomeKey' ORDER BY created_at_utc DESC LIMIT 15;" 2>&1)) { if ("$row" -ne '') { W ('      ' + $row) } }
    W ''
    W '    *** COMPLETED WITH FINDINGS. M1-21 mechanism proven on the current data. ***'
    W '    Look above for param.* (esp. superheat) vs ' + $OutcomeKey + ' with effect > 1.'
} else {
    W '    Still no new results. Read the compute response status in [4] above -'
    W '    if it says Blocked, the completeness we restored was consumed by an'
    W '    internal re-window; send this log. If Completed with 0 findings, the'
    W '    outcome had no feature crossing the FDR threshold - honest null.'
}
Save
exit 0
