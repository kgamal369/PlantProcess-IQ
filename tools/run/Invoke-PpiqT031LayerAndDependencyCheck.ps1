#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-031 step 1 - which layer is the live source-shaped one, and what still
    depends on src_*. READ ONLY. NOTHING IS DELETED.

.DESCRIPTION
    THE CONTRADICTION THIS RESOLVES. T-030 was closed having verified the five
    src_* schemas as the staging representation. T-031 then says to DELETE the
    src_* donor schemas as obsolete, and its own text insists "nothing is deleted
    whose replacement is not already generated and certified". Those two positions
    cannot both be right, and deleting the wrong layer is irreversible.

    WHAT THE ARCHITECTURE SAYS. 130_phase03_two_stage_delta_import_architecture.sql
    creates dump_store as a "dump-copy store preserving each source table shape",
    with stage-1 delta import source -> dump and stage-2 dump -> canonical. So the
    intended chain is:

        src_*      emulated customer systems - the DONOR
        dump_store source-shaped copy inside the product
        public     canonical

    If that is live, T-030's staging representation is dump_store and src_* is
    genuinely retirable. If dump_store is empty, src_* is the only source-shaped
    layer there is and deleting it would destroy what the authoring shell reads.

    A SIGNAL ALREADY IN HAND. T-030 measured src_* at exactly one third of
    canonical on every shared entity - coils 5,670 of 17,010, heats 630 of 1,890,
    downtime 210 of 630. A superseded 1x donor beside a 3x live plant fits that
    exactly.

    THIS RUNNER DELETES NOTHING AND WRITES NOTHING. It answers which layer is
    live, whether dump_store carries the 3x population, and what still references
    src_* - which is T-031's own dependency-check step, not extra scope.

.EXAMPLE
    .\tools\run\Invoke-PpiqT031LayerAndDependencyCheck.ps1
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

Rule "PPIQ T-031 STEP 1 - WHICH LAYER IS LIVE, AND WHAT DEPENDS ON src_*"
$repoRoot = (Get-Location).Path
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$env:PGOPTIONS = "-c default_transaction_read_only=on"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t031_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
Say ("Database : " + $Database)
Say "READ ONLY. NOTHING IS DELETED BY THIS RUNNER."

try {
    $ro = Invoke-Sql -Tag "ro" -Raw -Sql "SHOW transaction_read_only;"
    Say ("Read-only : " + $ro.Output.Trim() + " (required on)")
    if ($ro.Output.Trim() -ne "on") { Say "[STOP] not read-only."; exit 2 }

    Rule "1 - EVERY NON-CANONICAL SCHEMA, WITH REAL ROW COUNTS"
    Say "reltuples is an estimate, so live counts are taken per table."
    Show -Tag "inv" -Sql @"
SELECT n.nspname AS schema_name, c.relname AS table_name,
       (SELECT count(*) FROM information_schema.columns ic
         WHERE ic.table_schema = n.nspname AND ic.table_name = c.relname) AS columns,
       (xpath('/row/cnt/text()',
              query_to_xml(format('SELECT count(*) AS cnt FROM %I.%I', n.nspname, c.relname),
                           false, true, '')))[1]::text::bigint AS live_rows
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'r'
  AND n.nspname IN ('src_meltshop_pg','src_caster_oracle_shape','src_hsm_oracle_shape',
                    'src_pkl_mssql_shape','src_inspection_mysql_shape',
                    'dump_store','acquisition','canon')
ORDER BY n.nspname, c.relname;
"@

    Rule "2 - IS dump_store THE LIVE SOURCE-SHAPED LAYER"
    Say "If dump_store carries the 3x population it is the live layer and src_* is"
    Say "a superseded 1x donor. If it is empty, src_* is the only source-shaped"
    Say "layer there is and deleting it would destroy what the shell reads."
    Show -Tag "totals" -Sql @"
SELECT 'src_* total rows' AS layer,
       sum((xpath('/row/cnt/text()',
            query_to_xml(format('SELECT count(*) AS cnt FROM %I.%I', n.nspname, c.relname),
                         false, true, '')))[1]::text::bigint) AS rows,
       count(*) AS tables
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'r' AND n.nspname LIKE 'src\_%'
UNION ALL
SELECT 'dump_store total rows',
       coalesce(sum((xpath('/row/cnt/text()',
            query_to_xml(format('SELECT count(*) AS cnt FROM %I.%I', n.nspname, c.relname),
                         false, true, '')))[1]::text::bigint), 0),
       count(*)
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind = 'r' AND n.nspname = 'dump_store';
"@

    Rule "3 - WHAT STILL REFERENCES src_* - T-031's OWN DEPENDENCY CHECK"
    Say "Nothing may be deleted whose replacement is not already generated and"
    Say "certified, so every live reference has to be named first."
    Say ""
    Say "3a. Database objects - views, matviews and functions mentioning src_"
    Show -Tag "dbrefs" -Sql @"
SELECT 'view or matview' AS object_kind, n.nspname AS schema_name, c.relname AS object_name
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE c.relkind IN ('v','m')
  AND lower(pg_get_viewdef(c.oid)) LIKE '%src\_%'
UNION ALL
SELECT 'function', n.nspname, p.proname
FROM pg_proc p JOIN pg_namespace n ON n.oid = p.pronamespace
WHERE n.nspname NOT IN ('pg_catalog','information_schema')
  AND lower(pg_get_functiondef(p.oid)) LIKE '%src\_%'
ORDER BY 1, 2, 3;
"@

    Say "3b. Registered datasets, source systems and connection profiles"
    Show -Tag "registry" -Sql @"
SELECT 'source_system_definitions' AS registry, count(*) AS rows FROM public.source_system_definitions
UNION ALL SELECT 'source_dataset_definitions', count(*) FROM public.source_dataset_definitions
UNION ALL SELECT 'source_field_definitions', count(*) FROM public.source_field_definitions
UNION ALL SELECT 'connection_profiles', count(*) FROM public.connection_profiles
UNION ALL SELECT 'mapping_definitions', count(*) FROM public.mapping_definitions
UNION ALL SELECT 'import_batches', count(*) FROM public.import_batches
UNION ALL SELECT 'staging_records', count(*) FROM public.staging_records
UNION ALL SELECT 'source_table_dump_registry', count(*) FROM public.source_table_dump_registry
ORDER BY 1;
"@

    Say "3c. Which of those actually name a src_ schema"
    Show -Tag "named" -Sql @"
SELECT 'source_dataset_definitions' AS registry, count(*) AS naming_src
FROM public.source_dataset_definitions t
WHERE lower(t::text) LIKE '%src\_%'
UNION ALL
SELECT 'connection_profiles', count(*) FROM public.connection_profiles t WHERE lower(t::text) LIKE '%src\_%'
UNION ALL
SELECT 'mapping_definitions', count(*) FROM public.mapping_definitions t WHERE lower(t::text) LIKE '%src\_%'
UNION ALL
SELECT 'source_table_dump_registry', count(*) FROM public.source_table_dump_registry t WHERE lower(t::text) LIKE '%src\_%'
UNION ALL
SELECT 'source_system_definitions', count(*) FROM public.source_system_definitions t WHERE lower(t::text) LIKE '%src\_%'
ORDER BY 1;
"@

    Rule "4 - THE RETIREMENT-GATE PRECONDITIONS, STATED NOT ASSUMED"
    Say "T-031 requires all four, IN ORDER, before anything is deleted:"
    Say "  1 the generator reproduces the captured baseline on all nine dimensions"
    Say "  2 both presentation representations were regenerated from it"
    Say "  3 this certification passed"
    Say "  4 one backup was taken AND RESTORED SUCCESSFULLY"
    Say ""
    Say "None of the four has been evidenced yet. The certification in condition 3"
    Say "must be a TEST THAT FAILS THE BUILD, not a document, and must run in CI."
    Say "No deletion is possible until all four are recorded."
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:\PGOPTIONS -ErrorAction SilentlyContinue
}

Rule "WHAT THIS DECIDES"
Say "If dump_store is POPULATED at the canonical scale:"
Say "  src_* is the superseded donor, T-031's deletion target is correct, and the"
Say "  T-030 closure needs a correction naming dump_store as the staging layer."
Say ""
Say "If dump_store is EMPTY:"
Say "  src_* is the only source-shaped layer in the database. Deleting it would"
Say "  remove what the schema tree, canvas, SQL editor and preview read, and"
Say "  T-031's retirement step cannot proceed as written without first"
Say "  materialising its replacement - which its own rule demands anyway."
Say ""
Say "NOTHING WAS DELETED. NOTHING WAS WRITTEN."

$outFolder = Join-Path $repoRoot "docs\m1\evidence"
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$ev = Join-Path $outFolder ("T-031_layer_dependency_check_" + $stamp + ".txt")
$clean = New-Object System.Text.StringBuilder
foreach ($ch in ($script:log -replace "`r`n", "`n").ToCharArray()) {
    if ([int]$ch -le 126 -or [int]$ch -eq 10) { [void]$clean.Append($ch) }
}
[System.IO.File]::WriteAllText($ev, ($clean.ToString() -replace "`n", "`r`n"),
    (New-Object System.Text.UTF8Encoding($false)))
Write-Host ""
Write-Host ("Evidence : " + $ev)
exit 0
