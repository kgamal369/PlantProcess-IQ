<#
.SYNOPSIS
    Diagnose-TenantVisibility.ps1 - proves WHY the API session sees only
    40 observations / 1 quality event while psql sees the full 40k fleet.
    Read-only. No writes, no API calls.

.DESCRIPTION
    Hypothesis under test: canonical + ML tables carry tenant_id with RLS;
    the fleet data belongs to tenant X; the API session runs as tenant Y;
    psql as table owner bypasses RLS entirely. If true, every governed run in
    this database's history was computed blind to the fleet dataset.

    Sections:
      [1] tenant_id distribution on the canonical + feature-store tables
      [2] RLS status per table (relrowsecurity / relforcerowsecurity) and the
          policy expressions (which GUC or claim they scope by)
      [3] role posture: does ppiq_dev bypass RLS (owner / rolbypassrls)?
      [4] the application's tenant registry + user->tenant mapping
      [5] the verdict: which tenant the fleet belongs to vs which tenant the
          app session would claim - and the exact, smallest honest fix.
#>

[CmdletBinding()]
param(
    [string]$Database   = 'ppiq_presentation',
    [string]$DbHost     = '127.0.0.1',
    [int]   $Port       = 5432,
    [string]$DbUser     = 'ppiq_dev',
    [string]$DbPassword = 'ppiq_dev_local_only',
    [string]$RepoRoot   = (Get-Location).Path
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest

$stamp   = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $RepoRoot ("TenantVisibility_" + $stamp + ".txt")
$lines   = New-Object System.Collections.Generic.List[string]
$utf8    = New-Object System.Text.UTF8Encoding($false)

function W([string]$t = '') { $lines.Add($t); Write-Host $t }
function Save {
    [System.IO.File]::WriteAllText($logPath, (($lines -join "`r`n") + "`r`n"), $utf8)
    Write-Host ''
    Write-Host ('Log: ' + $logPath) -ForegroundColor Cyan
}
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
if (-not $psql) { Write-Host 'psql.exe not found.' -ForegroundColor Red; exit 2 }
$env:PGPASSWORD = $DbPassword
$conn = "host=$DbHost port=$Port dbname=$Database user=$DbUser"

function QA([string]$sql) {
    $out = & $psql -v ON_ERROR_STOP=1 -X -q -A -F '|' -t -d $conn -c $sql 2>&1
    if ($LASTEXITCODE -ne 0) { return @('ERR: ' + ($out -join ' ')) }
    return @($out | Where-Object { $_ -ne '' })
}
function HasCol([string]$t, [string]$c) {
    $r = QA "SELECT 1 FROM information_schema.columns WHERE table_schema='public' AND table_name='$t' AND column_name='$c' LIMIT 1;"
    return (@($r).Count -gt 0 -and $r[0] -notmatch '^ERR')
}

W '=============================================================================='
W ('TENANT VISIBILITY DIAGNOSIS - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ('DB: ' + $Database + '   role: ' + $DbUser)
W '=============================================================================='
W ''

$tables = @('material_units', 'parameter_observations', 'quality_events',
            'genealogy_edges', 'ml_feature_values', 'ml_outcome_values',
            'ml_correlation_results_v2')

# ---- [1] tenant distribution -------------------------------------------------

W '[1] TENANT_ID DISTRIBUTION (as table owner - RLS bypassed, this is the TRUTH)'
foreach ($t in $tables) {
    if (-not (HasCol $t 'tenant_id')) {
        W ('    ' + $t.PadRight(28) + 'no tenant_id column')
        continue
    }
    W ('    ' + $t + ':')
    foreach ($row in (QA "SELECT COALESCE(tenant_id::text,'(null)'), count(*) FROM public.$t GROUP BY 1 ORDER BY 2 DESC;")) {
        $p = $row -split '\|'
        if ($p.Count -ge 2) { W ('        ' + $p[0].PadRight(44) + $p[1].PadLeft(10)) }
        else { W ('        ' + $row) }
    }
}
W ''

# ---- [2] RLS posture per table ----------------------------------------------

W '[2] ROW-LEVEL SECURITY per table'
foreach ($row in (QA @'
SELECT c.relname,
       c.relrowsecurity::text,
       c.relforcerowsecurity::text,
       pg_get_userbyid(c.relowner)
FROM pg_class c
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname = 'public'
  AND c.relname IN ('material_units','parameter_observations','quality_events',
                    'genealogy_edges','ml_feature_values','ml_outcome_values',
                    'ml_correlation_results_v2')
ORDER BY 1;
'@)) {
    $p = $row -split '\|'
    if ($p.Count -ge 4) {
        W ('    ' + $p[0].PadRight(28) + 'rls=' + $p[1].PadRight(7) + 'force=' + $p[2].PadRight(7) + 'owner=' + $p[3])
    } else { W ('    ' + $row) }
}
W ''
W '    policies (the expression tells us WHICH setting/claim scopes the rows):'
foreach ($row in (QA @'
SELECT tablename, policyname, cmd, COALESCE(qual, '(none)')
FROM pg_policies
WHERE schemaname = 'public'
  AND tablename IN ('material_units','parameter_observations','quality_events',
                    'genealogy_edges','ml_feature_values','ml_outcome_values',
                    'ml_correlation_results_v2')
ORDER BY tablename, policyname;
'@)) {
    W ('    ' + $row)
}
W ''

# ---- [3] role posture --------------------------------------------------------

W '[3] ROLE POSTURE (why psql sees everything)'
foreach ($row in (QA "SELECT rolname, rolsuper::text, rolbypassrls::text FROM pg_roles WHERE rolname IN ('$DbUser','postgres') ORDER BY 1;")) {
    $p = $row -split '\|'
    if ($p.Count -ge 3) { W ('    ' + $p[0].PadRight(16) + 'super=' + $p[1].PadRight(7) + 'bypassrls=' + $p[2]) }
    else { W ('    ' + $row) }
}
W '    (note: a table OWNER also bypasses RLS unless FORCE is set - see [2])'
W ''

# ---- [4] tenants + user mapping ---------------------------------------------

W '[4] APPLICATION TENANTS + USER MAPPING'
if ((QA "SELECT to_regclass('public.ppiq_tenants') IS NOT NULL;")[0] -eq 't') {
    W '    ppiq_tenants:'
    foreach ($row in (QA "SELECT * FROM public.ppiq_tenants LIMIT 10;")) { W ('        ' + $row) }
} else {
    W '    ppiq_tenants: table absent'
}
W ''
if ((QA "SELECT to_regclass('public.tenants') IS NOT NULL;")[0] -eq 't') {
    W '    tenants:'
    foreach ($row in (QA "SELECT * FROM public.tenants LIMIT 10;")) { W ('        ' + $row) }
}
W ''
W '    app_users columns:'
$userCols = QA "SELECT string_agg(column_name, ', ' ORDER BY ordinal_position) FROM information_schema.columns WHERE table_schema='public' AND table_name='app_users';"
W ('        ' + ($userCols -join ''))
if (HasCol 'app_users' 'tenant_id') {
    W '    app_users user -> tenant:'
    foreach ($row in (QA "SELECT username, COALESCE(tenant_id::text,'(null)') FROM public.app_users ORDER BY username LIMIT 15;")) {
        W ('        ' + $row)
    }
} else {
    W '    app_users has NO tenant_id column - tenant may come from a claim,'
    W '    a session GUC (see policy expressions in [2]), or ppiq_user_sessions.'
}
W ''

# ---- [5] verdict -------------------------------------------------------------

W '[5] VERDICT LOGIC'
W '    Read [1]: the tenant holding ~40k material_units is the FLEET tenant.'
W '    Read [2]: the policy expression names the session setting or claim that'
W '              scopes queries (e.g. current_setting(''app.tenant_id'')).'
W '    Read [4]: the tenant the login user maps to is what the API session uses.'
W ''
W '    If fleet-tenant <> user-tenant, the fix is ONE of (smallest first):'
W '      a) point the login user (or the API tenant claim) at the fleet tenant -'
W '         a config/identity change, no data touched; or'
W '      b) normalise tenant_id on the canonical + ML rows to the app tenant -'
W '         data surgery, audited, only if (a) is architecturally wrong.'
W '    NEVER disable RLS to make a run pass - same law as the readiness gate.'
Save
exit 0
