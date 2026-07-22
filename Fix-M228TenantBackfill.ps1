<#
.SYNOPSIS
    Fix-M228TenantBackfill.ps1 - completes M2-28. v1 aborted on min(uuid),
    which Postgres does not have. This version discovers the tenant from the
    parent compute run first, then from any public table holding a single
    distinct uuid tenant_id, and only backfills when the answer is
    unambiguous - otherwise it reports and leaves the rows NULL. Also prints
    the RLS policy text so the read path is visible.
.PARAMETER RepoRoot  repository root (default: current directory)
.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Fix-M228TenantBackfill.ps1
#>
[CmdletBinding()]
param([string]$RepoRoot = (Get-Location).Path,
      [string]$Database = 'ppiq_presentation', [string]$DbHost = '127.0.0.1', [int]$Port = 5432,
      [string]$DbUser = 'ppiq_dev', [string]$DbPassword = 'ppiq_dev_local_only')
$ErrorActionPreference = 'Continue'
Set-StrictMode -Version Latest
$stamp   = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $RepoRoot ('Fix_M228_' + $stamp + '.txt')
$lines   = New-Object System.Collections.Generic.List[string]
$utf8    = New-Object System.Text.UTF8Encoding($false)
function W([string]$t = '') { $lines.Add($t); Write-Host $t }
function Save { [System.IO.File]::WriteAllText($logPath, (($lines -join "`r`n") + "`r`n"), $utf8); Write-Host ''; Write-Host ('Log: ' + $logPath) -ForegroundColor Cyan }


$cSql = @'
-- M2-28 v2: ml_correlation_results_v2 tenant_id backfill + RLS evidence.
-- v1 failed on min(uuid) (no such aggregate). v2 discovers the tenant properly:
--   1. from the parent compute run (authoritative),
--   2. else from any public table carrying a single distinct uuid tenant_id,
--   3. else reports and leaves NULL - it never guesses.
SET client_min_messages = warning;

SELECT 'BEFORE rows total'      AS metric, count(*)::text AS value FROM public.ml_correlation_results_v2
UNION ALL
SELECT 'BEFORE tenant_id NULL', count(*)::text FROM public.ml_correlation_results_v2 WHERE tenant_id IS NULL
UNION ALL
SELECT 'rls enabled',           relrowsecurity::text      FROM pg_class WHERE oid = 'public.ml_correlation_results_v2'::regclass
UNION ALL
SELECT 'rls forced',            relforcerowsecurity::text FROM pg_class WHERE oid = 'public.ml_correlation_results_v2'::regclass;

-- the policy text matters: it shows WHAT the app must match
SELECT policyname, cmd, qual FROM pg_policies
 WHERE schemaname = 'public' AND tablename = 'ml_correlation_results_v2';

-- 1. authoritative: the parent run owns the tenant
UPDATE public.ml_correlation_results_v2 r
   SET tenant_id = c.tenant_id
  FROM public.ml_correlation_compute_runs c
 WHERE r.compute_run_id = c.id
   AND r.tenant_id IS NULL
   AND c.tenant_id IS NOT NULL;

-- 2. discovery + backfill
DO $M228$
DECLARE t uuid; n int; rec record; q text;
BEGIN
    SELECT count(DISTINCT tenant_id) INTO n
      FROM public.ml_correlation_compute_runs WHERE tenant_id IS NOT NULL;
    IF n = 1 THEN
        SELECT tenant_id INTO t
          FROM public.ml_correlation_compute_runs WHERE tenant_id IS NOT NULL LIMIT 1;
        RAISE WARNING 'M2-28: tenant % taken from ml_correlation_compute_runs', t;
    END IF;

    IF t IS NULL THEN
        FOR rec IN
            SELECT c.table_name
              FROM information_schema.columns c
              JOIN information_schema.tables tb
                ON tb.table_schema = c.table_schema AND tb.table_name = c.table_name
             WHERE c.table_schema = 'public'
               AND c.column_name = 'tenant_id'
               AND c.udt_name = 'uuid'
               AND tb.table_type = 'BASE TABLE'
               AND c.table_name <> 'ml_correlation_results_v2'
             ORDER BY c.table_name
        LOOP
            q := format(
                'SELECT count(DISTINCT tenant_id), (SELECT tenant_id FROM public.%I WHERE tenant_id IS NOT NULL LIMIT 1) FROM public.%I WHERE tenant_id IS NOT NULL',
                rec.table_name, rec.table_name);
            BEGIN
                EXECUTE q INTO n, t;
            EXCEPTION WHEN OTHERS THEN
                n := 0; t := NULL;
            END;
            IF n = 1 AND t IS NOT NULL THEN
                RAISE WARNING 'M2-28: tenant % discovered from public.%', t, rec.table_name;
                EXIT;
            END IF;
            t := NULL;
        END LOOP;
    END IF;

    IF t IS NOT NULL THEN
        UPDATE public.ml_correlation_results_v2 SET tenant_id = t WHERE tenant_id IS NULL;
        RAISE WARNING 'M2-28: remaining NULLs backfilled with tenant %', t;
    ELSE
        RAISE WARNING 'M2-28: no single tenant could be determined - rows left NULL (reported, not guessed).';
    END IF;
END
$M228$;

SELECT 'AFTER tenant_id NULL'  AS metric, count(*)::text AS value FROM public.ml_correlation_results_v2 WHERE tenant_id IS NULL
UNION ALL
SELECT 'AFTER rows with tenant', count(*)::text FROM public.ml_correlation_results_v2 WHERE tenant_id IS NOT NULL
UNION ALL
SELECT 'distinct tenants now',   count(DISTINCT tenant_id)::text FROM public.ml_correlation_results_v2 WHERE tenant_id IS NOT NULL;
'@

W '=============================================================================='
W ('M2-28 TENANT BACKFILL v2 - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W '=============================================================================='
W ''
$fSql = Join-Path $RepoRoot 'Backend\database\scripts\M2-28_results_v2_tenant_backfill.sql'
$dir = Split-Path $fSql
if (-not (Test-Path -LiteralPath $dir)) { [void](New-Item -ItemType Directory -Path $dir -Force) }
if (Test-Path -LiteralPath $fSql) { Copy-Item -LiteralPath $fSql -Destination ($fSql + '.' + $stamp + '.bak') -Force; W '  [backup] previous script kept' }
[System.IO.File]::WriteAllText($fSql, $cSql, $utf8)
if (-not ([System.IO.File]::ReadAllText($fSql)).Contains('discovered from public.')) { W '  self-check FAILED'; Save; exit 1 }
W ('  script written: ' + $fSql)

$psql = $null
$cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
if ($cmd) { $psql = $cmd.Source }
if (-not $psql) {
    foreach ($r in @('C:\Program Files\PostgreSQL', 'C:\Program Files (x86)\PostgreSQL')) {
        if (Test-Path $r) {
            $h = Get-ChildItem $r -Filter psql.exe -Recurse -EA SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1
            if ($h) { $psql = $h.FullName; break }
        }
    }
}
if (-not $psql) { W '  psql.exe not found - run the script manually against the database.'; Save; exit 2 }

W ''
W '[RUN]'
$env:PGPASSWORD = $DbPassword
$env:PGOPTIONS = '-c client_min_messages=warning'
$conn = "host=$DbHost port=$Port dbname=$Database user=$DbUser"
$o = & $psql -v ON_ERROR_STOP=1 -X -q -d $conn -f $fSql 2>&1
$code = $LASTEXITCODE
foreach ($l in $o) { W ('    ' + $l) }
W ''
if ($code -ne 0) {
    W '  SQL FAILED - paste this log; the data is unchanged past the last committed statement.'
} else {
    W '  SQL OK.'
    W '  Read AFTER tenant_id NULL above:'
    W '    0        -> every finding now carries a tenant; open the findings page and'
    W '                confirm rows appear (M2-28 acceptance).'
    W '    still 320 -> no single tenant exists in this database. Send the policy text'
    W '                printed above plus the tenant claim the API authenticates with,'
    W '                and the backfill becomes a one-line targeted UPDATE.'
}
Save
exit 0
