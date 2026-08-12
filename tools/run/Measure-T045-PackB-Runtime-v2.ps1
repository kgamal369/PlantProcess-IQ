# =============================================================================
# PPIQ T-045 PACK B - RUNTIME MEASUREMENT
#
# READ ONLY. It writes no file, changes no row and commits nothing. Every
# credential is resolved from the environment profile, so it never prompts.
#
# WHAT IT ANSWERS, IN ORDER
#   A  the ml_outcome_definitions census that must precede any analysisReadiness
#      binding decision: active rows, usable outcome keys, their grains, and
#      whether outcome VALUES actually exist for each
#   B  the three Class-2 measures executed through the real API
#   C  the Class-1 control and the D1 regression probe, which together prove the
#      aggregate path is unchanged by the Class-2 seam
#
# IT DOES NOT DECIDE ANYTHING. If no usable outcome exists, that is the finding;
# a fake outcome must never be seeded to make a page look ready.
# =============================================================================

[CmdletBinding()]
param(
    [string]$ApiBase = 'http://localhost:5063',
    [string]$EnvProfile = 'env\profiles\presentation.env',
    [string]$PsqlExe = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0

$RepoRoot = (Get-Location).Path
$script:Findings = New-Object System.Collections.Generic.List[string]
$script:StaleAssembly = $false

function Write-Head([string]$t) {
    Write-Host ''
    Write-Host ('=' * 78)
    Write-Host $t
    Write-Host ('=' * 78)
}

function W([string]$t) { Write-Host $t }
function Note([string]$t) { Write-Host ('      ' + $t) }
function Finding([string]$t) {
    $script:Findings.Add($t) | Out-Null
    Write-Host ('  FINDING  ' + $t)
}

# -----------------------------------------------------------------------------
# CREDENTIALS - resolved from the profile the repository already owns.
# -----------------------------------------------------------------------------
function Get-EnvProfileMap([string]$rel) {
    $map = @{}
    $full = Join-Path $RepoRoot $rel
    if (-not (Test-Path -LiteralPath $full)) { return $map }
    foreach ($line in [System.IO.File]::ReadAllLines($full)) {
        $s = $line.Trim()
        if ($s.Length -eq 0) { continue }
        if ($s.StartsWith('#')) { continue }
        $eq = $s.IndexOf('=')
        if ($eq -lt 1) { continue }
        $map[$s.Substring(0, $eq).Trim()] = $s.Substring($eq + 1).Trim()
    }
    return $map
}

function Get-MapValue($map, [string]$key, [string]$fallback) {
    if ($map.ContainsKey($key)) {
        $v = $map[$key]
        if (-not [string]::IsNullOrWhiteSpace($v)) { return $v }
    }
    return $fallback
}

function Resolve-PsqlExe([string]$explicit) {
    if (-not [string]::IsNullOrWhiteSpace($explicit)) {
        if (Test-Path -LiteralPath $explicit) { return $explicit }
        return ''
    }
    $cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($cmd -ne $null) { return $cmd.Source }
    foreach ($v in @('17', '16', '15', '14')) {
        $c = 'C:\Program Files\PostgreSQL\' + $v + '\bin\psql.exe'
        if (Test-Path -LiteralPath $c) { return $c }
    }
    return ''
}

$EnvMap = Get-EnvProfileMap $EnvProfile
$PgHost = Get-MapValue $EnvMap 'POSTGRES_HOST' '127.0.0.1'
$PgPort = Get-MapValue $EnvMap 'POSTGRES_PORT' '5432'
$PgDb   = Get-MapValue $EnvMap 'POSTGRES_DB'   'ppiq_presentation'
$PgUser = Get-MapValue $EnvMap 'POSTGRES_USER' 'ppiq_dev'
$UserName = Get-MapValue $EnvMap 'PPIQ_SMOKE_USERNAME' 'e2eadmin'
$Password = Get-MapValue $EnvMap 'PPIQ_SMOKE_PASSWORD' ''
$env:PGPASSWORD = Get-MapValue $EnvMap 'POSTGRES_PASSWORD' 'ppiq_dev_local_only'
$env:PGCLIENTENCODING = 'UTF8'
$Psql = Resolve-PsqlExe $PsqlExe

Write-Head 'ENVIRONMENT'
W ('  database : ' + $PgUser + '@' + $PgHost + ':' + $PgPort + '/' + $PgDb)
W ('  api      : ' + $ApiBase + ' as ' + $UserName)
if ($Psql -eq '') { W '  FATAL psql.exe not resolved'; exit 1 }
if (-not ($PgHost -eq '127.0.0.1' -or $PgHost -eq 'localhost' -or $PgHost -eq '::1')) {
    W '  FATAL this instrument only runs against a loopback database'
    exit 1
}
Test-RuntimeFreshness $ApiBase

# =============================================================================
# RUNTIME FRESHNESS GATE.
#
# A build succeeding without a file-lock error means the API is NOT running from
# that output, or is running a stale assembly. v1 of this instrument measured a
# process that predated the code under test and reported the resulting refusals
# as data. The process start time against the assembly write time is the
# documented tell, so it is checked before anything is measured.
# =============================================================================
function Test-RuntimeFreshness([string]$base) {
    $port = 0
    try { $port = ([uri]$base).Port } catch { return }
    $conn = Get-NetTCPConnection -LocalPort $port -State Listen -ErrorAction SilentlyContinue
    if ($conn -eq $null) {
        W ('  WARNING nothing is listening on port ' + $port)
        return
    }
    $procId = @($conn)[0].OwningProcess
    $proc = Get-Process -Id $procId -ErrorAction SilentlyContinue
    if ($proc -eq $null) { return }

    $dll = Get-ChildItem -Path (Join-Path $RepoRoot 'Backend') -Recurse -Filter 'PlantProcess.Application.dll' -ErrorAction SilentlyContinue |
        Sort-Object LastWriteTime -Descending | Select-Object -First 1
    if ($dll -eq $null) { return }

    W ('  api process : pid ' + $procId + ' started ' + $proc.StartTime.ToString('yyyy-MM-dd HH:mm:ss'))
    W ('  newest build: ' + $dll.LastWriteTime.ToString('yyyy-MM-dd HH:mm:ss') + '  ' + $dll.FullName)
    if ($proc.StartTime -lt $dll.LastWriteTime) {
        $script:StaleAssembly = $true
        Finding 'the running API predates the newest build - it is serving a STALE ASSEMBLY'
    } else {
        W '  the running API is newer than the newest build'
    }
}

function Q([string]$sql) {
    # -t -A gives bare pipe-delimited rows; a single-element result must not be
    # allowed to unwrap to a scalar, so the array is always returned by comma.
    $rows = & $Psql -h $PgHost -p $PgPort -U $PgUser -d $PgDb -w -t -A -F '|' -v ON_ERROR_STOP=1 -c $sql
    if ($LASTEXITCODE -ne 0) { throw ('psql failed: ' + $sql) }
    $clean = @()
    foreach ($r in $rows) { if (-not [string]::IsNullOrWhiteSpace($r)) { $clean += $r } }
    return ,$clean
}

# =============================================================================
# A - ml_outcome_definitions CENSUS
# =============================================================================
Write-Head 'A. ml_outcome_definitions CENSUS'

$exists = Q "SELECT to_regclass('public.ml_outcome_definitions') IS NOT NULL"
if ($exists.Count -eq 0 -or $exists[0] -ne 't') {
    Finding 'the ml_outcome_definitions table does not exist in this database'
    $usable = @()
} else {
    $total  = (Q "SELECT count(*) FROM public.ml_outcome_definitions")[0]
    $active = (Q "SELECT count(*) FROM public.ml_outcome_definitions WHERE is_deleted = false")[0]
    W ('  total rows                 : ' + $total)
    W ('  active rows (is_deleted=f) : ' + $active)

    W ''
    W '  status breakdown (active rows):'
    foreach ($r in (Q "SELECT status, count(*) FROM public.ml_outcome_definitions WHERE is_deleted=false GROUP BY status ORDER BY status")) { Note $r }

    W ''
    W '  grain breakdown (active rows):'
    foreach ($r in (Q "SELECT grain, count(*) FROM public.ml_outcome_definitions WHERE is_deleted=false GROUP BY grain ORDER BY grain")) { Note $r }

    W ''
    W '  outcome_type breakdown (active rows):'
    foreach ($r in (Q "SELECT outcome_type, count(*) FROM public.ml_outcome_definitions WHERE is_deleted=false GROUP BY outcome_type ORDER BY outcome_type")) { Note $r }

    # USABLE means more than registered. A definition with no outcome VALUES at
    # its own grain cannot produce a readiness verdict about anything, so it is
    # reported separately rather than counted as available.
    W ''
    W '  candidate definitions, highest version per key, with their value counts:'
    $sql = @"
WITH latest AS (
    SELECT DISTINCT ON (lower(outcome_key))
           outcome_key, grain, outcome_type, status, version
    FROM public.ml_outcome_definitions
    WHERE is_deleted = false
    ORDER BY lower(outcome_key), version DESC
)
SELECT l.outcome_key, l.grain, l.outcome_type, l.status,
       COALESCE(v.n, 0) AS value_rows
FROM latest l
LEFT JOIN (
    SELECT lower(outcome_key) AS k, grain AS g, count(*) AS n
    FROM public.ml_outcome_values
    GROUP BY lower(outcome_key), grain
) v ON v.k = lower(l.outcome_key) AND v.g = l.grain
ORDER BY COALESCE(v.n, 0) DESC, l.outcome_key ASC
"@
    $rows = Q $sql
    if ($rows.Count -eq 0) { Note '(none)' }
    foreach ($r in $rows) { Note $r }

    $usable = @()
    foreach ($r in $rows) {
        $p = $r.Split('|')
        if ($p.Count -ge 5) {
            $n = 0
            [void][int]::TryParse($p[4], [ref]$n)
            if ($n -gt 0 -and $p[3] -eq 'Active') { $usable += $r }
        }
    }

    W ''
    W ('  USABLE outcome keys (Active status AND outcome values present): ' + $usable.Count)
    foreach ($u in $usable) { Note $u }
}

# DETERMINISTIC CHOICE. Most values first, then outcome_key ascending - the same
# shape as the T-045 Pack A parameter tie-break, so a rerun cannot silently
# rebind the page. No fallback literal: if there is nothing usable there is
# nothing to bind, and that is the answer.
$ChosenOutcome = $null
$ChosenGrain = $null
if ($usable.Count -gt 0) {
    $p = $usable[0].Split('|')
    $ChosenOutcome = $p[0]
    $ChosenGrain = $p[1]
    W ''
    W ('  DETERMINISTIC PRESENTATION OUTCOME: ' + $ChosenOutcome + ' at grain ' + $ChosenGrain)
} else {
    Finding 'no usable active outcome exists, so analysisReadiness has no target to evaluate'
    Note 'the correct outcome is the truthful unresolved state, NOT a seeded outcome'
}

# =============================================================================
# B - SESSION
# =============================================================================
Write-Head 'B. SESSION'
$token = $null
try {
    $login = Invoke-RestMethod -Method Post -Uri ($ApiBase + '/auth/login') -ContentType 'application/json' `
        -Body (@{ userName = $UserName; password = $Password } | ConvertTo-Json)
    $token = $login.accessToken
} catch {
    W ('  FATAL login failed: ' + $_.Exception.Message)
    W '  is the API running with -Profile presentation on this port?'
    exit 1
}
if ([string]::IsNullOrWhiteSpace($token)) { W '  FATAL login returned no access token'; exit 1 }
W '  authenticated'
$Headers = @{ Authorization = ('Bearer ' + $token) }

function Invoke-Widget($body, [string]$label) {
    $json = $body | ConvertTo-Json -Depth 6
    try {
        $r = Invoke-RestMethod -Method Post -Uri ($ApiBase + '/analytics/dashboard/widgets/query') `
            -Headers $Headers -ContentType 'application/json' -Body $json
        return @{ Ok = $true; Result = $r; Error = $null }
    } catch {
        # Invoke-RestMethod discards the error body; without it a refusal is
        # indistinguishable from a fault, and this whole task is about telling
        # those two apart.
        $detail = $_.Exception.Message
        try {
            $stream = $_.Exception.Response.GetResponseStream()
            $reader = New-Object System.IO.StreamReader($stream)
            $detail = $reader.ReadToEnd()
        } catch { }
        return @{ Ok = $false; Result = $null; Error = $detail }
    }
}

function Show-Result($outcome, [string]$label, [bool]$mustExecute) {
    W ''
    W ('  --- ' + $label + ' ---')
    if (-not $outcome.Ok) {
        W ('      REFUSED or FAILED')
        $d = $outcome.Error
        if ($d.Length -gt 700) { $d = $d.Substring(0, 700) }
        W ('      ' + $d)

        # A PASS PRINTED BESIDE A FAIL IS A DEFECT. v1 of this instrument
        # reported "no findings" while all three Class-2 measures were refused,
        # because every check it made was guarded on a SUCCESSFUL result. A
        # measure that must execute and did not IS the finding.
        if ($mustExecute) {
            Finding ($label + ' did not execute')
            if ($outcome.Error -match 'Unsupported measure code') { $script:StaleAssembly = $true }
        }
        return $null
    }
    $r = $outcome.Result
    $cols = @()
    foreach ($c in $r.columns) { $cols += $c.code }
    W ('      columns (' + $cols.Count + '): ' + ($cols -join ', '))
    $rowCount = 0
    if ($r.rows -ne $null) { $rowCount = @($r.rows).Count }
    W ('      rows   : ' + $rowCount)
    $i = 0
    foreach ($row in @($r.rows)) {
        if ($i -ge 6) { W '      ... (truncated)'; break }
        $pairs = @()
        foreach ($c in $cols) {
            $v = $row.$c
            if ($v -eq $null) { $v = '(null)' }
            $s = [string]$v
            if ($s.Length -gt 60) { $s = $s.Substring(0, 60) + '...' }
            $pairs += ($c + '=' + $s)
        }
        W ('      [' + $i + '] ' + ($pairs -join '  '))
        $i = $i + 1
    }
    return $r
}

# =============================================================================
# C - THE THREE CLASS-2 MEASURES
# =============================================================================
Write-Head 'C. CLASS-2 MEASURES THROUGH THE REAL RUNTIME'

$fs = Invoke-Widget @{ widgetType = 'table'; chartType = 'table'; measureCode = 'findingStatus'; dimensionCode = $null; parameterCode = $null; filters = $null; options = @{ maxRows = 50 } } 'findingStatus'
$fsR = Show-Result $fs 'findingStatus (correlation_results)' $true
if ($fsR -ne $null) {
    $state = $null
    foreach ($row in @($fsR.rows)) { $state = $row.state; break }
    if ($state -eq 'NO_CORRELATION_EXISTS') { Finding 'findingStatus returned the forbidden NO_CORRELATION_EXISTS state' }
    if ($state -eq $null) { Finding 'findingStatus returned no renderable row; zero findings must still render' }
}

$sc = Invoke-Widget @{ widgetType = 'table'; chartType = 'table'; measureCode = 'scoringCoverage'; dimensionCode = $null; parameterCode = $null; filters = $null; options = @{ maxRows = 50 } } 'scoringCoverage'
$scR = Show-Result $sc 'scoringCoverage (risk_scores + material_units)' $true
if ($scR -ne $null) {
    foreach ($row in @($scR.rows)) {
        if ($row.scoringSource -eq 'SCORING_SOURCE_RECORDED' -or $row.modelState -eq 'MODEL_VERSION_RECORDED') {
            Note ('provenance IS recorded for scope ' + $row.scope + ' - report it, do not overwrite it')
        }
    }
}

$arBody = @{ widgetType = 'table'; chartType = 'table'; measureCode = 'analysisReadiness'; dimensionCode = $null; parameterCode = $ChosenOutcome; filters = $null; options = @{ maxRows = 50 } }
$ar = Invoke-Widget $arBody 'analysisReadiness'
$arR = Show-Result $ar ('analysisReadiness (parameterCode = ' + $(if ($ChosenOutcome) { $ChosenOutcome } else { '(none)' }) + ')') $true
if ($arR -ne $null) {
    $overall = $null
    foreach ($row in @($arR.rows)) { $overall = $row.overall; break }
    W ('      overall: ' + $overall)
    if ($ChosenOutcome -eq $null -and $overall -ne 'READINESS_TARGET_NOT_RESOLVED') {
        Finding 'no usable outcome exists yet readiness did not report the unresolved state'
    }
}

# Second readiness call with NO parameter, always. The unresolved path must be
# proven to render even when a usable outcome does exist, because that is the
# state a fresh customer installation starts in.
$ar0 = Invoke-Widget @{ widgetType = 'table'; chartType = 'table'; measureCode = 'analysisReadiness'; dimensionCode = $null; parameterCode = $null; filters = $null; options = @{ maxRows = 50 } } 'analysisReadiness-unbound'
Show-Result $ar0 'analysisReadiness with no target (the empty-install state)' $true | Out-Null

# =============================================================================
# D - CLASS-1 CONTROL AND THE D1 REGRESSION
# =============================================================================
Write-Head 'D. CLASS-1 CONTROL AND D1 REGRESSION'

$c1 = Invoke-Widget @{ widgetType = 'chart'; chartType = 'donut'; measureCode = 'materialCount'; dimensionCode = 'materialUnitType'; parameterCode = $null; filters = $null; options = @{ maxRows = 50 } } 'materialCount'
$c1R = Show-Result $c1 'CONTROL materialCount by materialUnitType (Class 1, migrated)' $true
if ($c1R -ne $null) {
    $cols = @()
    foreach ($c in $c1R.columns) { $cols += $c.code }
    $expected = @('materialUnitType', 'dimensionLabel', 'value', 'observationCount', 'secondaryCount')
    $same = $true
    foreach ($e in $expected) { if ($cols -notcontains $e) { $same = $false } }
    if ($same) {
        W '      Class-1 envelope unchanged: all five aggregate columns present'
    } else {
        Finding ('Class-1 envelope changed; columns are ' + ($cols -join ', '))
    }
}

$d1 = Invoke-Widget @{ widgetType = 'kpi'; chartType = 'kpi'; measureCode = 'observationCount'; dimensionCode = $null; parameterCode = $null; filters = $null; options = @{ maxRows = 500 } } 'observationCount'
$d1R = Show-Result $d1 'D1 REGRESSION observationCount (exact, must never equal the 50000 cap)' $true
if ($d1R -ne $null) {
    $total = 0
    foreach ($row in @($d1R.rows)) { $total = $total + [double]$row.value }
    W ('      summed value: ' + $total)
    if ($total -eq 50000) { Finding 'observationCount summed to exactly the raw row cap; the D1 remediation has regressed' }
}

$trusted = (Q "SELECT count(*) FROM parameter_observations WHERE is_deleted = false")[0]
W ('      trusted SQL population: ' + $trusted)

$c1blocked = Invoke-Widget @{ widgetType = 'chart'; chartType = 'bar'; measureCode = 'processStepDuration'; dimensionCode = 'equipment'; parameterCode = $null; filters = $null; options = @{ maxRows = 50 } } 'processStepDuration'
Show-Result $c1blocked 'CONTROL processStepDuration (unmigrated, expected containment refusal)' $false | Out-Null

# =============================================================================
Write-Head 'SUMMARY'
if ($script:Findings.Count -eq 0) {
    W '  no findings'
} else {
    foreach ($f in $script:Findings) { W ('  - ' + $f) }
}
W ''
W '  This instrument decided nothing and changed nothing.'

if ($script:StaleAssembly) {
    W ''
    W '  STALE ASSEMBLY. The API is serving code older than the repository, so the'
    W '  refusals above describe the running process and NOT the committed code.'
    W '  Stop the API, rebuild, restart with -Profile presentation, and rerun.'
}

if ($script:Findings.Count -gt 0) { exit 1 }
exit 0
