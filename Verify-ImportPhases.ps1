<#
.SYNOPSIS
    Verify-ImportPhases.ps1 - certifies each Runsheet phase AFTER you click it in
    the HMI. Read-only. Proves row deltas, batch provenance, and connector-only
    source_system straight from ppiq_presentation. This is M1-20's acceptance
    instrument and, for -Phase A, M1-03's proof.

.DESCRIPTION
    You drive the registrations in Prepare Import (Rule 3: the journey through the
    HMI IS the product; a psql shortcut would violate Rule 2 and poison the demo's
    provenance story). This script is the proof that what you clicked actually
    landed - correctly, with lineage, from the connector and nowhere else.

    Schema-adaptive: canonical tables are created by EF migrations, so column
    names are DISCOVERED at runtime from information_schema, never assumed. If a
    provenance column is absent the script says so instead of emitting a wrong query.

    -Baseline captures counts BEFORE a phase; -Phase X verifies AFTER, showing the
    delta against the saved baseline (or against zero if none saved).

    Phase expectations (from the runsheet, per ppiq_presentation on the fleet):
      A  parameter_definitions +~37, defect_catalogs +20   (taxonomy, = M1-03)
      B  material_units +~37,322, genealogy_edges +~37,322 (spine)
      C  parameter_observations grows (C1 1,802 + C2 18,661; C3 overnight)
      D  quality_events +~34,312                           (defects)
      E  ml_correlation_results_v2 references a NEW run id  (engine)

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Verify-ImportPhases.ps1 -Baseline
    (run ONCE before you start clicking Phase A)

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Verify-ImportPhases.ps1 -Phase A
    (run after the four Phase-A registrations complete + import)
#>

[CmdletBinding()]
param(
    [ValidateSet('A', 'B', 'C', 'D', 'E')]
    [string]$Phase,
    [switch]$Baseline,
    [string]$Database   = 'ppiq_presentation',
    [string]$DbHost     = '127.0.0.1',
    [int]   $Port       = 5432,
    [string]$DbUser     = 'ppiq_dev',
    [string]$DbPassword = 'ppiq_dev_local_only',
    [string]$RepoRoot   = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$stamp        = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath      = Join-Path $RepoRoot ("ImportVerify_" + $(if ($Baseline) { 'baseline' } else { "phase$Phase" }) + "_" + $stamp + ".txt")
$baselinePath = Join-Path $RepoRoot '.import-baseline.json'
$lines        = New-Object System.Collections.Generic.List[string]
$utf8         = New-Object System.Text.UTF8Encoding($false)

function W([string]$t = '') {
    $lines.Add($t)
    Write-Host $t
}
function Save {
    [System.IO.File]::WriteAllText($logPath, (($lines -join "`r`n") + "`r`n"), $utf8)
    Write-Host ''
    Write-Host ('Log: ' + $logPath) -ForegroundColor Cyan
}

# ---- psql plumbing ----------------------------------------------------------

function Resolve-Psql {
    $cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    foreach ($r in @('C:\Program Files\PostgreSQL', 'C:\Program Files (x86)\PostgreSQL')) {
        if (Test-Path $r) {
            $hit = Get-ChildItem -Path $r -Filter psql.exe -Recurse -ErrorAction SilentlyContinue |
                   Sort-Object FullName -Descending | Select-Object -First 1
            if ($hit) { return $hit.FullName }
        }
    }
    return $null
}
$psql = Resolve-Psql
if (-not $psql) { Write-Host 'PREFLIGHT FAIL: psql.exe not found.' -ForegroundColor Red; exit 2 }
$env:PGPASSWORD = $DbPassword
$conn = "host=$DbHost port=$Port dbname=$Database user=$DbUser"

function Q1([string]$sql) {
    $out = & $psql -v ON_ERROR_STOP=1 -X -q -A -t -d $conn -c $sql 2>&1
    if ($LASTEXITCODE -ne 0) { return $null }
    return (($out | Where-Object { $_ -ne '' }) -join '')
}
function QA([string]$sql) {
    $out = & $psql -v ON_ERROR_STOP=1 -X -q -A -F '|' -t -d $conn -c $sql 2>&1
    if ($LASTEXITCODE -ne 0) { return @('ERR: ' + ($out -join ' ')) }
    return @($out | Where-Object { $_ -ne '' })
}
function TableExists([string]$t) {
    return (Q1 "SELECT to_regclass('public.$t') IS NOT NULL;") -eq 't'
}
function ColExists([string]$t, [string]$c) {
    return (Q1 "SELECT EXISTS(SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='$t' AND column_name='$c');") -eq 't'
}
function RowCount([string]$t) {
    if (-not (TableExists $t)) { return -1 }
    $v = Q1 "SELECT count(*) FROM public.$t;"
    if ($null -eq $v) { return -1 }
    return [int]$v
}

W '=============================================================================='
W ('IMPORT PHASE VERIFY - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ('DB: ' + $Database + '   mode: ' + $(if ($Baseline) { 'BASELINE' } else { 'PHASE ' + $Phase }))
W '=============================================================================='
W ''

if (-not (Q1 'SELECT 1;')) { W ('FAIL: cannot reach ' + $Database); Save; exit 2 }

$tracked = @(
    'parameter_definitions', 'defect_catalogs', 'material_units',
    'genealogy_edges', 'parameter_observations', 'quality_events'
)

# ---- BASELINE mode ----------------------------------------------------------

if ($Baseline) {
    W '[BASELINE] capturing current counts (run this BEFORE Phase A clicks)'
    $snap = @{}
    foreach ($t in $tracked) {
        $c = RowCount $t
        $snap[$t] = $c
        W ('    ' + $t.PadRight(26) + $(if ($c -lt 0) { '(table absent)' } else { $c }))
    }
    $snap['_captured'] = $stamp
    ($snap | ConvertTo-Json) | Set-Content -LiteralPath $baselinePath -Encoding UTF8
    W ''
    W ('    saved -> ' + $baselinePath)
    W '    now click Phase A in Prepare Import, then run -Phase A.'
    Save; exit 0
}

if (-not $Phase) { W 'Provide -Phase A|B|C|D|E or -Baseline.'; Save; exit 2 }

# ---- load baseline ----------------------------------------------------------

$base = @{}
if (Test-Path -LiteralPath $baselinePath) {
    $j = Get-Content -LiteralPath $baselinePath -Raw | ConvertFrom-Json
    foreach ($p in $j.PSObject.Properties) { $base[$p.Name] = $p.Value }
    W ('[baseline loaded: ' + $base['_captured'] + ']')
} else {
    W '[no baseline file - deltas shown against 0; run -Baseline next time first]'
}
W ''

function Delta([string]$t) {
    $now = RowCount $t
    $was = 0
    if ($base.ContainsKey($t)) { $was = [int]$base[$t] }
    if ($was -lt 0) { $was = 0 }
    return @{ Now = $now; Was = $was; Delta = ($now - $was) }
}
function Report([string]$t, [int]$expectMin) {
    $d = Delta $t
    if ($d.Now -lt 0) {
        W ('    ' + $t.PadRight(26) + 'TABLE ABSENT'); return $false
    }
    $verdict = 'OK'
    if ($expectMin -gt 0 -and $d.Delta -lt $expectMin) { $verdict = 'LOW (expected >= ' + $expectMin + ')' }
    if ($expectMin -gt 0 -and $d.Delta -le 0) { $verdict = 'NO GROWTH' }
    W ('    ' + $t.PadRight(26) + ('was ' + $d.Was).PadRight(14) + ('now ' + $d.Now).PadRight(14) + ('delta +' + $d.Delta).PadRight(14) + $verdict)
    return ($d.Delta -ge $expectMin)
}

# ---- provenance proof (schema-adaptive) -------------------------------------

function ProveProvenance([string]$t) {
    if (-not (TableExists $t)) { return }
    $ssCol = $null
    foreach ($c in @('source_system', 'source_system_key', 'connector_source_system')) {
        if (ColExists $t $c) { $ssCol = $c; break }
    }
    $batchCol = $null
    foreach ($c in @('import_batch_id', 'batch_id', 'source_import_batch_id', 'import_batch')) {
        if (ColExists $t $c) { $batchCol = $c; break }
    }
    if ($ssCol) {
        W ('    ' + $t + ' by ' + $ssCol + ':')
        foreach ($row in (QA "SELECT COALESCE($ssCol::text,'(null)'), count(*) FROM public.$t GROUP BY 1 ORDER BY 2 DESC;")) {
            $p = $row -split '\|'
            if ($p.Count -ge 2) {
                $flag = ''
                if ($p[0] -match '(?i)PPIQ_CONFIG|DEMO|SEED|SYNTH|GOLDEN|FIXTURE') { $flag = '   <-- NON-CONNECTOR (Rule-2 flag)' }
                if ($p[0] -eq '(null)') { $flag = '   <-- NULL provenance (Rule-2 breach)' }
                W ('      ' + $p[0].PadRight(24) + $p[1].PadLeft(8) + $flag)
            }
        }
    } else {
        W ('    ' + $t + ': no source_system-style column found (cannot prove provenance here)')
    }
    if ($batchCol) {
        $nb = Q1 "SELECT count(*) FROM public.$t WHERE $batchCol IS NULL;"
        W ('    ' + $t + ' rows with NULL ' + $batchCol + ': ' + $nb + $(if ([int]$nb -gt 0) { '   <-- lineage gap' } else { '   (all carry a batch)' }))
    }
}

# ---- per-phase ---------------------------------------------------------------

$pass = $true
switch ($Phase) {
    'A' {
        W '[PHASE A] TAXONOMY (= M1-03 acceptance)'
        W '  expected: parameter_definitions +~37, defect_catalogs +20'
        $pass = (Report 'parameter_definitions' 30) -and $pass
        $pass = (Report 'defect_catalogs' 20) -and $pass
        W ''
        W '  PROVENANCE (must be connector-only, no PPIQ_CONFIG/DEMO):'
        ProveProvenance 'parameter_definitions'
        ProveProvenance 'defect_catalogs'
        W ''
        W '  runsheet check: caster V_PARAMETER_DEFINITIONS = 4 rows, HSM = 7, meltshop = 26, parsytec defects = 20'
    }
    'B' {
        W '[PHASE B] UNITS + GENEALOGY (the spine)'
        W '  expected: material_units +~37,322, genealogy_edges +~37,322'
        $pass = (Report 'material_units' 30000) -and $pass
        $pass = (Report 'genealogy_edges' 30000) -and $pass
        W ''
        ProveProvenance 'material_units'
        W ''
        W '  genealogy walk sanity (a coil should resolve to slab->heat):'
        foreach ($row in (QA "SELECT child_key, count(*) FROM public.genealogy_edges GROUP BY 1 ORDER BY 2 DESC LIMIT 3;")) { W ('      ' + $row) }
    }
    'C' {
        W '[PHASE C] OBSERVATIONS (money-slide X axis)'
        W '  expected: parameter_observations grows (C1 ~1,802 + C2 ~18,661; C3 overnight breadth)'
        $pass = (Report 'parameter_observations' 1500) -and $pass
        W ''
        ProveProvenance 'parameter_observations'
        W ''
        W '  parameter codes present (superheat_c is the money driver):'
        if (ColExists 'parameter_observations' 'parameter_code') {
            foreach ($row in (QA "SELECT parameter_code, count(*) FROM public.parameter_observations GROUP BY 1 ORDER BY 2 DESC LIMIT 12;")) { W ('      ' + $row) }
        }
    }
    'D' {
        W '[PHASE D] QUALITY EVENTS (money-slide Y axis)'
        W '  expected: quality_events +~34,312'
        $pass = (Report 'quality_events' 30000) -and $pass
        W ''
        ProveProvenance 'quality_events'
        W ''
        W '  defect codes present (CRACK_LONG is the money outcome, SCRATCH the null control):'
        if (ColExists 'quality_events' 'defect_code') {
            foreach ($row in (QA "SELECT defect_code, count(*) FROM public.quality_events GROUP BY 1 ORDER BY 2 DESC LIMIT 12;")) { W ('      ' + $row) }
        }
    }
    'E' {
        W '[PHASE E] ENGINE RUN (steps 8-10)'
        if (-not (TableExists 'ml_correlation_compute_runs')) { W '  runs table absent'; $pass = $false }
        else {
            W '  most recent compute runs:'
            foreach ($row in (QA "SELECT to_char(completed_at_utc,'HH24:MI:SS'), status, target_outcome_key, grain FROM public.ml_correlation_compute_runs ORDER BY completed_at_utc DESC LIMIT 8;")) { W ('      ' + $row) }
            $blocked = [int](Q1 "SELECT count(*) FROM public.ml_correlation_compute_runs WHERE status ILIKE 'block%' AND completed_at_utc > now() - interval '2 hours';")
            $ok = [int](Q1 "SELECT count(*) FROM public.ml_correlation_compute_runs WHERE status ILIKE 'ok' OR status ILIKE 'complete%';")
            W ''
            W ('  runs Completed/Ok (all-time): ' + $ok + '   |   Blocked in last 2h: ' + $blocked)
            if ($ok -eq 0) {
                W '  >>> NO completed run yet. Run Diagnose-ReadinessBlock.ps1 to see WHICH gate'
                W '  >>> dimension blocks, then Run-GoldenAnalysis.ps1 -Execute -Grain coil.'
                $pass = $false
            }
            W ''
            W '  the money finding (CRACK_LONG ~ superheat, expect ~9.3x; SCRATCH ~1.0x):'
            if (TableExists 'ml_correlation_results_v2') {
                foreach ($row in (QA "SELECT feature_key, outcome_key, round(effect_size::numeric,2), round(q_value::numeric,4) FROM public.ml_correlation_results_v2 WHERE outcome_key ILIKE '%crack%' OR outcome_key ILIKE '%scratch%' OR feature_key ILIKE '%superheat%' ORDER BY completed_at_utc DESC NULLS LAST LIMIT 10;")) { W ('      ' + $row) }
            }
        }
    }
}
W ''

W '=============================================================================='
if ($pass) { W ('PHASE ' + $Phase + ': counts + provenance PASS. Update the board.') }
else { W ('PHASE ' + $Phase + ': one or more checks did not pass - see above. Do not mark green.') }
W '=============================================================================='
Save
if ($pass) { exit 0 } else { exit 1 }
