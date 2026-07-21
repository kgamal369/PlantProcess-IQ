<#
.SYNOPSIS
    Confirm-FeatureStoreFunction.ps1 - proves whether migration 740's heat-lineage
    logic is actually IN the function the API calls, and whether the lineage view
    resolves coil->heat right now. Read-only except an optional forced re-apply.

.DESCRIPTION
    The 14:25 API refresh returned feature_rows=14433 (pre-740 shape). Migration
    740's version emits far more and sets heat_id on coil rows via lineage. This
    script settles which body is installed:
      [1] does ppiq_ml_unit_heat_lineage (the 740 view) exist and resolve rows?
      [2] does the function source contain the lineage JOIN (740 marker)?
      [3] live heat_id coverage at coil grain in ml_outcome_values right now
      [4] with -Reapply: re-run 740 SQL, then call the refresh, then re-measure
#>

[CmdletBinding()]
param(
    [string]$Database   = 'ppiq_presentation',
    [string]$DbHost     = '127.0.0.1',
    [int]   $Port       = 5432,
    [string]$DbUser     = 'ppiq_dev',
    [string]$DbPassword = 'ppiq_dev_local_only',
    [string]$SqlPath    = '',
    [string]$RepoRoot   = (Get-Location).Path,
    [switch]$Reapply
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $RepoRoot ("ConfirmFn_" + $stamp + ".txt")
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
W ('CONFIRM FEATURE-STORE FUNCTION - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ('DB: ' + $Database)
W '=============================================================================='
W ''

W '[1] lineage view'
$viewExists = Q1 "SELECT to_regclass('public.ppiq_ml_unit_heat_lineage') IS NOT NULL;"
W ('    ppiq_ml_unit_heat_lineage exists: ' + $viewExists)
if ($viewExists -eq 't') {
    W ('    units resolving to a heat:        ' + (Q1 "SELECT count(*) FROM public.ppiq_ml_unit_heat_lineage WHERE heat_code IS NOT NULL;"))
}
W ''

W '[2] is 740 logic in the installed function body?'
$src = Q1 "SELECT pg_get_functiondef('public.ppiq_ml_refresh_feature_store(integer)'::regprocedure);"
$has740 = $src -match 'ppiq_ml_unit_heat_lineage'
W ('    function references the lineage view: ' + $has740)
if (-not $has740) {
    W '    >>> The installed function is the PRE-740 body. migration 740 did not'
    W '    >>> stick (older definition still resident). Re-apply with -Reapply.'
}
W ''

W '[3] live heat_id coverage at coil grain (ml_outcome_values)'
W ('    coil outcome rows:           ' + (Q1 "SELECT count(*) FROM public.ml_outcome_values WHERE grain='coil';"))
W ('    ... with heat_id:            ' + (Q1 "SELECT count(*) FROM public.ml_outcome_values WHERE grain='coil' AND heat_id IS NOT NULL;"))
W ('    distinct heats at coil:      ' + (Q1 "SELECT count(DISTINCT heat_id) FROM public.ml_outcome_values WHERE grain='coil' AND heat_id IS NOT NULL;") + '   (gate needs >= 30)')
W ('    defect.class rows:           ' + (Q1 "SELECT count(*) FROM public.ml_outcome_values WHERE outcome_key='defect.class';"))
W ''

if ($Reapply) {
    W '[4] RE-APPLY 740 + refresh + re-measure'
    if ([string]::IsNullOrWhiteSpace($SqlPath)) {
        foreach ($c in @((Join-Path $RepoRoot '740_feature_store_heat_lineage_and_defect_outcomes.sql'),
                         (Join-Path $RepoRoot 'Backend\database\scripts\740_feature_store_heat_lineage_and_defect_outcomes.sql'))) {
            if (Test-Path -LiteralPath $c) { $SqlPath = $c; break }
        }
    }
    if (-not (Test-Path -LiteralPath $SqlPath)) { W '    740 SQL not found; pass -SqlPath'; Save; exit 1 }
    $o = & $psql -v ON_ERROR_STOP=1 -X -q -1 -d $conn -f $SqlPath 2>&1
    if ($LASTEXITCODE -ne 0) { W ('    re-apply FAILED: ' + ($o -join ' ')); Save; exit 1 }
    W '    740 re-applied (function redefined + backfill run)'
    $rf = Q1 "SELECT outcome_rows FROM public.ppiq_ml_refresh_feature_store(3650);"
    W ('    refresh via 740 body returned outcome_rows = ' + $rf)
    W ('    distinct heats at coil now: ' + (Q1 "SELECT count(DISTINCT heat_id) FROM public.ml_outcome_values WHERE grain='coil' AND heat_id IS NOT NULL;"))
    W ('    defect.class rows now:      ' + (Q1 "SELECT count(*) FROM public.ml_outcome_values WHERE outcome_key='defect.class';"))
    W ''
    W '    >>> IMPORTANT: the API caches nothing here - the function lives in the DB.'
    W '    >>> But if the API refresh endpoint is hit AGAIN it will re-run THIS body'
    W '    >>> (now correct). Do NOT call /feature-store/refresh from the golden'
    W '    >>> script again - run correlation DIRECTLY so the store stays 740-shaped.'
}
Save
exit 0
