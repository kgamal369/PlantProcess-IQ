#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-031 step 1b - the dependency dimensions the first pass missed.
    READ ONLY. NOTHING IS DELETED.

.DESCRIPTION
    WHY THIS EXISTS. Step 1's object-dependency query errored:
    pg_get_functiondef throws on aggregate functions, so it needs prokind = 'f'.
    That lesson was already recorded in the worker-1 handover and applied
    correctly in T-025d, and I forgot it here. The result is that the single
    dependency dimension that matters most before an irreversible deletion -
    which views and functions read src_ - is the one that did not run.

    IT ALSO ANSWERS THE QUESTION STEP 1 RAISED RATHER THAN SETTLED. dump_store is
    populated, but not as a clean mirror: three tables match src_* exactly, several
    sit near 3x, and parsytec_surface_defects is 51,987 against 1,987 - which is
    1,987 plus exactly 50,000. That is an accumulating delta-import store carrying
    earlier runs, not a regenerated copy of the current plant. So this pass asks
    where each layer's rows actually came from, and what the connection profiles
    point at, because that decides whether src_* has a certified replacement.

    NOTHING HERE DELETES, DROPS OR WRITES.

.EXAMPLE
    .\tools\run\Invoke-PpiqT031DependencyCheckB.ps1
#>

[CmdletBinding()]
param(
    [string]$PgHost     = "127.0.0.1",
    [int]   $PgPort     = 5432,
    [string]$PgUser     = "ppiq_dev",
    [string]$PgPassword = "ppiq_dev_local_only",
    [string]$Database   = "ppiq_presentation",
    [string]$PsqlPath   = ""
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

$script:log = ""
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
    [System.IO.File]::WriteAllText($f, $Sql, (New-Object System.Text.UTF8Encoding($false)))
    $o = Join-Path $script:tmp ($Tag + ".out")
    $e = Join-Path $script:tmp ($Tag + ".err")
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
function Show { param([string]$Sql, [string]$Tag)
    $r = Invoke-Sql -Sql ("\pset border 2`n" + $Sql) -Tag $Tag
    if ($r.ExitCode -ne 0) { Say ("[FAIL] " + $Tag + " : " + ($r.Error -replace "`r", "" -replace "`n", " ").Trim()) }
    Say $r.Output
}

Rule "PPIQ T-031 STEP 1b - THE DEPENDENCIES THAT DECIDE THE DELETION"
$repoRoot = (Get-Location).Path
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$env:PGOPTIONS = "-c default_transaction_read_only=on"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t031b_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
Say ("Database : " + $Database)
Say "READ ONLY. NOTHING IS DELETED."

try {
    $ro = Invoke-Sql -Tag "ro" -Raw -Sql "SHOW transaction_read_only;"
    Say ("Read-only : " + $ro.Output.Trim() + " (required on)")
    if ($ro.Output.Trim() -ne "on") { Say "[STOP] not read-only."; exit 2 }

    Rule "1 - VIEWS AND FUNCTIONS THAT READ src_  (the query that errored before)"
    Say "prokind = 'f' excludes aggregates and window functions, which"
    Say "pg_get_functiondef refuses to render. That omission is what broke step 1."
    Show -Tag "objs" -Sql @"
SELECT 'view' AS object_kind, n.nspname AS schema_name, c.relname AS object_name
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind IN ('v','m') AND lower(pg_get_viewdef(c.oid)) LIKE '%src\_%'
UNION ALL
SELECT 'function', n.nspname, p.proname
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE p.prokind = 'f'
  AND n.nspname NOT IN ('pg_catalog','information_schema')
  AND lower(pg_get_functiondef(p.oid)) LIKE '%src\_%'
ORDER BY 1, 2, 3;
"@

    Rule "2 - WHAT THE CONNECTION PROFILES ACTUALLY POINT AT"
    Say "If the schema tree, canvas and SQL editor reach data through these, then"
    Say "whatever they name is what the authoring shell reads."
    Show -Tag "profiles" -Sql @"
SELECT id, name,
       CASE WHEN lower(t::text) LIKE '%src\_%' THEN 'names a src_ schema' ELSE 'does not' END AS src_reference,
       CASE WHEN lower(t::text) LIKE '%dump_store%' THEN 'names dump_store' ELSE 'does not' END AS dump_reference
FROM public.connection_profiles t
ORDER BY name;
"@

    Rule "3 - THE DUMP REGISTRY - WHICH SOURCE TABLE MAPS TO WHICH DUMP TABLE"
    Show -Tag "registry" -Sql @"
SELECT * FROM public.source_table_dump_registry ORDER BY 1 LIMIT 20;
"@

    Rule "4 - WHERE dump_store ROWS CAME FROM, AND WHETHER THEY ARE CURRENT"
    Say "Three dump tables match src_* exactly, several sit near 3x, and"
    Say "parsytec_surface_defects is 1,987 plus exactly 50,000. If the import runs"
    Say "span several dates, dump_store is an accumulation rather than a mirror."
    Show -Tag "batches" -Sql @"
SELECT id, status,
       to_char(created_at_utc, 'YYYY-MM-DD HH24:MI') AS created,
       CASE WHEN lower(t::text) LIKE '%src\_%' THEN 'src_' ELSE '' END AS mentions
FROM public.import_batches t
ORDER BY created_at_utc DESC
LIMIT 20;
"@
    Say "Two-stage import run history, if the telemetry table carries it:"
    Show -Tag "runs" -Sql @"
SELECT count(*) AS two_stage_runs,
       min(started_at_utc)::date AS first_run,
       max(started_at_utc)::date AS last_run
FROM public.two_stage_import_runs;
"@

    Rule "5 - DOES dump_store STILL DESCRIBE THE CURRENT PLANT"
    Say "Identity test, not a row-count test. A coil in the dump must be a coil in"
    Say "canonical, and the reverse tells us whether the dump is complete."
    Show -Tag "identity" -Sql @"
SELECT 'dump coils with no canonical match' AS check_name, count(*) AS rows
FROM dump_store.src_hsm_oracle_shape_hsm_coils d
WHERE NOT EXISTS (SELECT 1 FROM public.material_units mu
                  WHERE mu.is_deleted = false AND mu.material_code = d.coil_id
                    AND lower(coalesce(mu.material_unit_type,'')) LIKE '%coil%')
UNION ALL
SELECT 'canonical coils with no dump match', count(*)
FROM public.material_units mu
WHERE mu.is_deleted = false AND lower(coalesce(mu.material_unit_type,'')) LIKE '%coil%'
  AND NOT EXISTS (SELECT 1 FROM dump_store.src_hsm_oracle_shape_hsm_coils d
                  WHERE d.coil_id = mu.material_code)
UNION ALL
SELECT 'dump coils also present in src_*', count(*)
FROM dump_store.src_hsm_oracle_shape_hsm_coils d
WHERE EXISTS (SELECT 1 FROM src_hsm_oracle_shape.hsm_coils s WHERE s.coil_id = d.coil_id);
"@
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:\PGOPTIONS -ErrorAction SilentlyContinue
}

Rule "WHAT THIS SETTLES"
Say "If no view or function reads src_, and the connection profiles that name it"
Say "are stale, then src_* has no live reader and retirement is a registry"
Say "cleanup plus a schema drop - once the four preconditions are evidenced."
Say ""
Say "If a live profile or object reads src_, then src_* IS what the authoring"
Say "shell reads, and T-031 cannot delete it until a certified replacement is"
Say "pointed at - which its own rule requires anyway."
Say ""
Say "NOTHING WAS DELETED. NOTHING WAS WRITTEN."

$outFolder = Join-Path $repoRoot "docs\m1\evidence"
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$ev = Join-Path $outFolder ("T-031_dependency_check_b_" + $stamp + ".txt")
$clean = New-Object System.Text.StringBuilder
foreach ($ch in ($script:log -replace "`r`n", "`n").ToCharArray()) {
    if ([int]$ch -le 126 -or [int]$ch -eq 10) { [void]$clean.Append($ch) }
}
[System.IO.File]::WriteAllText($ev, ($clean.ToString() -replace "`n", "`r`n"),
    (New-Object System.Text.UTF8Encoding($false)))
Write-Host ""
Write-Host ("Evidence : " + $ev)
exit 0
