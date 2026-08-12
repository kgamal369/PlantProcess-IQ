# =============================================================================
# PPIQ T-045 - REPLAY AND CONVERGENCE PROOF
#
# THE RULE BEING PROVEN: source truth = rebuild truth = live truth.
#
# A  IDEMPOTENCE. Every numbered correction from 790 to 814 is re-run in order.
#    Each one is written to be idempotent, so a second run must report UPDATE 0
#    on every statement. A non-zero count means the script and the database
#    disagree about what "converged" means, and a replay would move the data.
#
# B  SEEDER CONVERGENCE. The four authoritative writers are parsed and their
#    widget definitions compared to each other. The same-UUID invariant already
#    covers ids and codes; this covers TITLE, CHART, DIMENSION, MEASURE and
#    PARAMETER, which is exactly where T-045 found drift the invariant missed.
#
# C  SEED AGAINST LIVE. What the seeders would write is compared to what the
#    database holds. Where a seeder derives a value at run time the literal is
#    not compared - the derivation is proven separately in D.
#
# D  DERIVED VALUES. The presentation parameter is recomputed by the same rule
#    the seeders carry, and the live rows must already equal it.
#
# READ ONLY EXCEPT FOR THE REPLAY ITSELF, which is the point: an idempotent
# script that changes nothing is the evidence. Credentials come from the
# profile; nothing is prompted.
#
# WHAT THIS DOES NOT PROVE: a rebuild from an EMPTY database. That needs the
# full Rebuild-PresentationDb path against a scratch database with migrations
# applied, and it is destructive. This proves that replaying every correction
# moves nothing and that source and live agree - not that an empty install
# reaches the same place.
# =============================================================================

[CmdletBinding()]
param(
    [string]$EnvProfile = 'env\profiles\presentation.env',
    [string]$PsqlExe = ''
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$RepoRoot = (Get-Location).Path
$script:Fail = 0

function Write-Head([string]$t) {
    Write-Host ''
    Write-Host ('=' * 78)
    Write-Host $t
    Write-Host ('=' * 78)
}

function Check([bool]$ok, [string]$label) {
    if ($ok) {
        Write-Host ('  PASS  ' + $label)
    } else {
        $script:Fail = $script:Fail + 1
        Write-Host ('  FAIL  ' + $label)
    }
}

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

$m = Get-EnvProfileMap $EnvProfile
$PgHost = Get-MapValue $m 'POSTGRES_HOST' '127.0.0.1'
$PgPort = Get-MapValue $m 'POSTGRES_PORT' '5432'
$PgDb   = Get-MapValue $m 'POSTGRES_DB'   'ppiq_presentation'
$PgUser = Get-MapValue $m 'POSTGRES_USER' 'ppiq_dev'
$env:PGPASSWORD = Get-MapValue $m 'POSTGRES_PASSWORD' 'ppiq_dev_local_only'
$env:PGCLIENTENCODING = 'UTF8'
$Psql = Resolve-PsqlExe $PsqlExe

Write-Head 'TARGET'
Write-Host ('  database : ' + $PgUser + '@' + $PgHost + ':' + $PgPort + '/' + $PgDb)
if ($Psql -eq '') { Write-Host '  FATAL psql.exe not resolved'; exit 1 }
if (-not ($PgHost -eq '127.0.0.1' -or $PgHost -eq 'localhost' -or $PgHost -eq '::1')) {
    Write-Host '  FATAL this proof only runs against a loopback database'
    exit 1
}

function Q([string]$sql) {
    $rows = & $Psql -h $PgHost -p $PgPort -U $PgUser -d $PgDb -w -t -A -F '|' -v ON_ERROR_STOP=1 -c $sql
    if ($LASTEXITCODE -ne 0) { throw ('psql failed: ' + $sql) }
    $clean = @()
    foreach ($r in $rows) { if (-not [string]::IsNullOrWhiteSpace($r)) { $clean += $r } }
    return ,$clean
}

# =============================================================================
# A - IDEMPOTENT REPLAY
# =============================================================================
Write-Head 'A. REPLAY EVERY NUMBERED CORRECTION, IN ORDER'

$scripts = @(
    '790_t044_canonical_widget_definitions.sql',
    '800_t045_canonical_widget_definitions.sql',
    '810_t045_packb_mi_sev_convergence.sql',
    '811_t045_packc_mi_sev_title_convergence.sql',
    '812_t045_packd_analytical_page_bindings.sql',
    '813_t045_packe_presentation_parameter_convergence.sql',
    '814_t045_packf_residual_parameter_convergence.sql'
)

foreach ($s in $scripts) {
    $path = Join-Path $RepoRoot ('Backend\database\scripts\' + $s)
    if (-not (Test-Path -LiteralPath $path)) {
        Check $false ('script missing: ' + $s)
        continue
    }

    $output = & $Psql -h $PgHost -p $PgPort -U $PgUser -d $PgDb -w -v ON_ERROR_STOP=1 -f $path 2>&1
    $exit = $LASTEXITCODE

    $moved = 0
    foreach ($line in $output) {
        $text = [string]$line
        if ($text -match '^UPDATE (\d+)$') { $moved = $moved + [int]$Matches[1] }
        if ($text -match '^INSERT \d+ (\d+)$') { $moved = $moved + [int]$Matches[1] }
        if ($text -match '^DELETE (\d+)$') { $moved = $moved + [int]$Matches[1] }
    }

    Check ($exit -eq 0) ('replays without error: ' + $s)
    Check ($moved -eq 0) ('replay changes nothing (' + $moved + ' rows moved): ' + $s)
}

# =============================================================================
# B - THE FOUR AUTHORITATIVE WRITERS AGREE
# =============================================================================
Write-Head 'B. SEEDER CONVERGENCE ON THE FULL DEFINITION, NOT ONLY THE UUID'

$seeders = @(
    'scripts\demo\Rebuild-PresentationDb.ps1',
    'scripts\demo\Seed-PresentationDashboards.v2.ps1',
    'scripts\demo\Insert-Widgets-v4.ps1',
    'scripts\demo\Finish-PresentationWorkspace.ps1'
)

$codes = @('PO_KPI_MAT','PO_KPI_OBS','PO_KPI_DEF','PO_KPI_RATE','PO_TREND','PO_MIX','PO_WEEK','PO_TABLE',
           'QM_TREND','QM_BREAK','QM_SEV','QM_TABLE','EO_EQDEF','EO_OBS','EO_TABLE','EO_MONTH',
           'CF_RATE','CF_TOP','PA_KAVG','PA_KOBS','PA_TREND','PA_BYP','PA_TABLE',
           'RI_KPI','RI_TREND','RI_TABLE','MI_RATE','MI_SEV')

function Get-SeederDefinition([string]$text, [string]$code) {
    $pattern = "'" + $code + "'\s+'([^']*)'\s+'(\w*)'\s+'(\w*)'\s+'(\w+)'\s+(\S+)"
    $mm = [regex]::Match($text, $pattern)
    if (-not $mm.Success) { return $null }
    return ($mm.Groups[1].Value + '|' + $mm.Groups[2].Value + '|' + $mm.Groups[3].Value + '|' +
            $mm.Groups[4].Value + '|' + $mm.Groups[5].Value)
}

$seederText = @{}
foreach ($s in $seeders) {
    $p = Join-Path $RepoRoot $s
    if (-not (Test-Path -LiteralPath $p)) { Check $false ('seeder missing: ' + $s); continue }
    $seederText[$s] = [System.IO.File]::ReadAllText($p)
}

$seedDefinition = @{}
foreach ($code in $codes) {
    $seen = @{}
    $missing = @()
    foreach ($s in $seeders) {
        if (-not $seederText.ContainsKey($s)) { continue }
        $d = Get-SeederDefinition $seederText[$s] $code
        if ($d -eq $null) { $missing += $s; continue }
        $seen[$d] = $true
    }

    if ($seen.Count -eq 0) {
        Check $false ($code + ' appears in no seeder')
        continue
    }

    if ($seen.Count -gt 1) {
        Check $false ($code + ' DRIFT across writers: ' + (($seen.Keys) -join '   VS   '))
        continue
    }

    $only = @($seen.Keys)[0]
    $seedDefinition[$code] = $only
    if ($missing.Count -gt 0) {
        Write-Host ('  NOTE  ' + $code + ' absent from ' + $missing.Count + ' writer(s); present ones agree')
    }
    Check $true ($code + ' converged: ' + $only)
}

# RI_EQUIP was retired. Its absence is part of the contract.
$stillThere = @()
foreach ($s in $seeders) {
    if ($seederText.ContainsKey($s) -and $seederText[$s].Contains("'RI_EQUIP'")) { $stillThere += $s }
}
Check ($stillThere.Count -eq 0) ('RI_EQUIP retired from every writer (' + $stillThere.Count + ' still carry it)')

# =============================================================================
# C - SEED AGAINST LIVE
# =============================================================================
Write-Head 'C. WHAT THE SEEDERS WRITE AGAINST WHAT THE DATABASE HOLDS'

$liveRows = Q @"
SELECT widget_code, widget_title, chart_type, COALESCE(dimension_code,''), measure_code
FROM dashboard_widget_definitions
WHERE is_deleted = FALSE AND is_active = TRUE
ORDER BY widget_code
"@

$live = @{}
foreach ($r in $liveRows) {
    $p = $r.Split('|')
    if ($p.Count -ge 5) { $live[$p[0]] = ($p[1] + '|' + $p[2] + '|' + $p[3] + '|' + $p[4]) }
}

foreach ($code in $codes) {
    if (-not $seedDefinition.ContainsKey($code)) { continue }
    if (-not $live.ContainsKey($code)) {
        Check $false ($code + ' is seeded but not present and active in the database')
        continue
    }

    # The seeder tuple carries a fifth field (parameter) which may be derived at
    # run time, so only the four literal fields are compared here. The parameter
    # is proven in section D.
    $seedFour = ($seedDefinition[$code].Split('|')[0..3]) -join '|'
    Check ($seedFour -eq $live[$code]) ($code + ' seed matches live')
    if ($seedFour -ne $live[$code]) {
        Write-Host ('        seed: ' + $seedFour)
        Write-Host ('        live: ' + $live[$code])
    }
}

$inactive = Q "SELECT widget_code FROM dashboard_widget_definitions WHERE widget_code = 'RI_EQUIP' AND is_deleted = FALSE AND is_active = FALSE"
Check ($inactive.Count -eq 1) 'RI_EQUIP is present and inactive in the database'

# =============================================================================
# D - DERIVED VALUES
# =============================================================================
Write-Head 'D. THE DERIVED PRESENTATION PARAMETER'

$derived = Q @"
SELECT pd.parameter_code
FROM parameter_definitions pd
JOIN parameter_observations po ON po.parameter_definition_id = pd.id
WHERE pd.is_deleted = FALSE AND po.is_deleted = FALSE
GROUP BY pd.parameter_code
ORDER BY (pd.parameter_code = 'FDT_C') DESC, COUNT(*) DESC, pd.parameter_code ASC
LIMIT 1
"@

if ($derived.Count -eq 0) {
    Check $false 'no parameter has observations, so nothing can be derived'
} else {
    $want = $derived[0]
    Write-Host ('  derived by the seeder rule: ' + $want)
    $bound = Q ("SELECT widget_code || '|' || COALESCE(parameter_code,'') FROM dashboard_widget_definitions " +
                "WHERE widget_code IN ('PA_KAVG','PA_KOBS','PA_TREND','PA_TABLE') AND is_deleted = FALSE ORDER BY widget_code")
    foreach ($b in $bound) {
        $p = $b.Split('|')
        Check ($p[1] -eq $want) ($p[0] + ' binds the derived parameter (' + $p[1] + ')')
    }
}

$orphan = Q @"
SELECT count(*)
FROM dashboard_widget_definitions w
WHERE w.is_deleted = FALSE AND w.is_active = TRUE
  AND w.measure_code IN ('avgParameterValue','maxParameterValue','minParameterValue','observationCount')
  AND w.parameter_code IS NOT NULL AND w.parameter_code <> ''
  AND NOT EXISTS (
      SELECT 1 FROM parameter_definitions pd
      JOIN parameter_observations po ON po.parameter_definition_id = pd.id
      WHERE pd.parameter_code = w.parameter_code AND pd.is_deleted = FALSE AND po.is_deleted = FALSE)
"@
Check ($orphan[0] -eq '0') ('no widget binds a parameter without observations (' + $orphan[0] + ')')

# =============================================================================
Write-Head 'RESULT'
if ($script:Fail -eq 0) {
    Write-Host '  replay moves nothing, the four writers agree, and source matches live'
} else {
    Write-Host ('  ' + $script:Fail + ' check(s) FAILED')
}
Write-Host ''
Write-Host '  NOT PROVEN HERE: a rebuild from an empty database. That is the'
Write-Host '  Rebuild-PresentationDb path against a scratch database and it is'
Write-Host '  destructive, so it is a deliberate separate run.'

if ($script:Fail -gt 0) { exit 1 }
exit 0
