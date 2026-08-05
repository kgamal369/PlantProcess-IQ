#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-030 - verify the source-shaped staging representation. READ ONLY.

.DESCRIPTION
    THE SMALLEST PERMANENT CORRECT IMPLEMENTATION IS A VERIFICATION, NOT A BUILD.
    110_phase1_demo_source_shapes.sql already creates five source-shaped schemas
    with ten tables, and generate_fleet_v2_donor.py already references them. So
    T-030's question is not "materialise it" but "is what exists actually the
    staging representation the task describes". Building a second one would be the
    scope absorption the speed ruling forbids.

    WHAT THE FROZEN VALIDATION ASKS, AND HOW EACH CLAUSE IS OPERATIONALISED:

      "populated from the generator alone"
          every source table carries rows.

      "genuinely unprepared: no derived column, no pre-joined view, no canonical
       vocabulary leaking into a source-shaped table"
          zero views or materialised views in the source schemas; zero generated
          columns; zero foreign keys from a source schema into public, because a
          declared link IS a pre-join; and zero canonical vocabulary columns.
          Canonical vocabulary is tested by NAME - material_unit_id, material_code,
          product_family, grade_or_recipe, defect_catalog_id, parameter_definition_id,
          site_id, is_deleted, created_at_utc - because those are the words a
          finished model uses and a customer system does not.

      "identities match the canonical layer exactly - a coil visible here is the
       same coil there"
          src_hsm_oracle_shape.hsm_coils.coil_id against material_units.material_code
          for Coil, and src_meltshop_pg.heats.heat_no against material_code for
          Heat, counted in BOTH directions.

      "row counts differ as expected and the difference is recorded rather than
       treated as a defect"
          counts are printed side by side and the difference is REPORTED, never
          asserted equal. The task says explicitly that a test asserting equality
          would be wrong, so this runner does not contain one.

      "the schema tree, canvas, SQL editor and preview all read it successfully"
          a frontend clause. It is recorded honestly rather than claimed from a
          database check, because a row count proves nothing about a surface.

    NOTHING IS WRITTEN. If a clause cannot be verified from the database, it is
    recorded as unverified rather than assumed.

.EXAMPLE
    .\tools\run\Invoke-PpiqT030StagingVerification.ps1
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
$script:clause = @{}
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
function Judge {
    param([string]$Clause, [string]$Sql, [string]$Tag)
    $r = Invoke-Sql -Sql $Sql -Tag $Tag -Raw
    if ($r.ExitCode -ne 0) {
        Say ("  [FAIL] " + $Clause + " query error: " + ($r.Error -replace "`r", "" -replace "`n", " ").Trim())
        $script:clause[$Clause] = 999
        return
    }
    $seen = 0; $off = 0
    foreach ($raw in ($r.Output -split "`n")) {
        $line = $raw.Trim()
        if ($line -eq "") { continue }
        $p = $line -split "~"
        if ($p.Count -ne 2) { continue }
        $seen = $seen + 1
        $n = 0
        if ($p[1] -match "^-?\d+$") { $n = [int]$p[1] }
        $mark = "ok"
        if ($n -ne 0) { $mark = "OFFENDING"; $off = $off + $n }
        Say ("    " + $p[0].PadRight(58) + $p[1].PadLeft(8) + "  " + $mark)
    }
    if ($seen -eq 0) {
        Say ("  [FAIL] " + $Clause + " produced no parsable check. Refusing to report zero.")
        $script:clause[$Clause] = 999
        return
    }
    Say ("  checks parsed " + $seen + ", offending " + $off)
    $script:clause[$Clause] = $off
}

Rule "PPIQ T-030 - SOURCE-SHAPED STAGING VERIFICATION"
$repoRoot = (Get-Location).Path
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$env:PGOPTIONS = "-c default_transaction_read_only=on"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t030_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
Say ("Database : " + $Database)

try {
    $ro = Invoke-Sql -Tag "ro" -Raw -Sql "SHOW transaction_read_only;"
    Say ("Read-only : " + $ro.Output.Trim() + " (required on)")
    if ($ro.Output.Trim() -ne "on") { Say "[STOP] not read-only."; exit 2 }

    Rule "1 - WHAT THE STAGING REPRESENTATION CONTAINS"
    Show -Tag "tables" -Sql @"
SELECT n.nspname AS source_schema, c.relname AS table_name,
       (SELECT count(*) FROM information_schema.columns ic
         WHERE ic.table_schema = n.nspname AND ic.table_name = c.relname) AS columns,
       c.reltuples::bigint AS estimated_rows
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname LIKE 'src_%' AND c.relkind = 'r'
ORDER BY n.nspname, c.relname;
"@
    Say "Exact row counts, because reltuples is an estimate:"
    Show -Tag "counts" -Sql @"
SELECT 'src_meltshop_pg.heats' AS source_table, count(*) FROM src_meltshop_pg.heats
UNION ALL SELECT 'src_meltshop_pg.lf_treatment', count(*) FROM src_meltshop_pg.lf_treatment
UNION ALL SELECT 'src_caster_oracle_shape.cast_sequence', count(*) FROM src_caster_oracle_shape.cast_sequence
UNION ALL SELECT 'src_caster_oracle_shape.cast_pieces', count(*) FROM src_caster_oracle_shape.cast_pieces
UNION ALL SELECT 'src_hsm_oracle_shape.hsm_coils', count(*) FROM src_hsm_oracle_shape.hsm_coils
UNION ALL SELECT 'src_hsm_oracle_shape.hsm_pass_measurements', count(*) FROM src_hsm_oracle_shape.hsm_pass_measurements
UNION ALL SELECT 'src_pkl_mssql_shape.pickle_orders', count(*) FROM src_pkl_mssql_shape.pickle_orders
UNION ALL SELECT 'src_pkl_mssql_shape.qa_lab_results', count(*) FROM src_pkl_mssql_shape.qa_lab_results
UNION ALL SELECT 'src_inspection_mysql_shape.parsytec_surface_defects', count(*) FROM src_inspection_mysql_shape.parsytec_surface_defects
UNION ALL SELECT 'src_inspection_mysql_shape.downtime_events', count(*) FROM src_inspection_mysql_shape.downtime_events
ORDER BY 1;
"@

    Rule "2 - POPULATED FROM THE GENERATOR"
    Judge -Clause "POPULATED" -Tag "populated" -Sql @"
SELECT 'source tables with zero rows~' || count(*)::text FROM (
  SELECT 1 FROM src_meltshop_pg.heats HAVING count(*) = 0
  UNION ALL SELECT 1 FROM src_meltshop_pg.lf_treatment HAVING count(*) = 0
  UNION ALL SELECT 1 FROM src_caster_oracle_shape.cast_sequence HAVING count(*) = 0
  UNION ALL SELECT 1 FROM src_caster_oracle_shape.cast_pieces HAVING count(*) = 0
  UNION ALL SELECT 1 FROM src_hsm_oracle_shape.hsm_coils HAVING count(*) = 0
  UNION ALL SELECT 1 FROM src_hsm_oracle_shape.hsm_pass_measurements HAVING count(*) = 0
  UNION ALL SELECT 1 FROM src_pkl_mssql_shape.pickle_orders HAVING count(*) = 0
  UNION ALL SELECT 1 FROM src_pkl_mssql_shape.qa_lab_results HAVING count(*) = 0
  UNION ALL SELECT 1 FROM src_inspection_mysql_shape.parsytec_surface_defects HAVING count(*) = 0
  UNION ALL SELECT 1 FROM src_inspection_mysql_shape.downtime_events HAVING count(*) = 0
) q;
"@

    Rule "3 - GENUINELY UNPREPARED"
    Say "A view is a pre-join. A foreign key into public is a declared link. A"
    Say "canonical column name is finished-model vocabulary in a customer table."
    Judge -Clause "UNPREPARED" -Tag "unprepared" -Sql @"
SELECT 'views or matviews in a source schema~' || count(*)::text
FROM pg_class c JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE n.nspname LIKE 'src_%' AND c.relkind IN ('v','m')
UNION ALL
SELECT 'generated columns in a source table~' || count(*)::text
FROM information_schema.columns
WHERE table_schema LIKE 'src\_%' AND is_generated <> 'NEVER'
UNION ALL
SELECT 'foreign keys from a source schema into public~' || count(*)::text
FROM pg_constraint con
JOIN pg_class c  ON c.oid  = con.conrelid
JOIN pg_namespace n  ON n.oid  = c.relnamespace
JOIN pg_class rc ON rc.oid = con.confrelid
JOIN pg_namespace rn ON rn.oid = rc.relnamespace
WHERE con.contype = 'f' AND n.nspname LIKE 'src\_%' AND rn.nspname = 'public'
UNION ALL
SELECT 'canonical vocabulary leaking into a source table~' || count(*)::text
FROM information_schema.columns
WHERE table_schema LIKE 'src\_%'
  AND column_name IN ('material_unit_id','material_code','material_unit_type',
                      'product_family','grade_or_recipe','defect_catalog_id',
                      'parameter_definition_id','site_id','is_deleted',
                      'created_at_utc','is_synthetic','source_record_id');
"@
    Say "The vocabulary a source table DOES use, for contrast:"
    Show -Tag "vocab" -Sql @"
SELECT table_schema, table_name, string_agg(column_name, ', ' ORDER BY ordinal_position) AS columns
FROM information_schema.columns
WHERE table_schema IN ('src_meltshop_pg','src_hsm_oracle_shape')
GROUP BY table_schema, table_name ORDER BY 1, 2;
"@

    Rule "4 - IDENTITIES MATCH CANONICAL EXACTLY"
    Say "A coil visible in staging is the same coil in canonical. Counted BOTH ways."
    Judge -Clause "IDENTITY" -Tag "identity" -Sql @"
SELECT 'staging coils with no canonical match~' || count(*)::text
FROM src_hsm_oracle_shape.hsm_coils s
WHERE NOT EXISTS (SELECT 1 FROM public.material_units mu
                  WHERE mu.is_deleted = false AND mu.material_code = s.coil_id
                    AND lower(coalesce(mu.material_unit_type,'')) LIKE '%coil%')
UNION ALL
SELECT 'canonical coils with no staging match~' || count(*)::text
FROM public.material_units mu
WHERE mu.is_deleted = false AND lower(coalesce(mu.material_unit_type,'')) LIKE '%coil%'
  AND NOT EXISTS (SELECT 1 FROM src_hsm_oracle_shape.hsm_coils s WHERE s.coil_id = mu.material_code)
UNION ALL
SELECT 'staging heats with no canonical match~' || count(*)::text
FROM src_meltshop_pg.heats s
WHERE NOT EXISTS (SELECT 1 FROM public.material_units mu
                  WHERE mu.is_deleted = false AND mu.material_code = s.heat_no
                    AND lower(coalesce(mu.material_unit_type,'')) LIKE '%heat%')
UNION ALL
SELECT 'canonical heats with no staging match~' || count(*)::text
FROM public.material_units mu
WHERE mu.is_deleted = false AND lower(coalesce(mu.material_unit_type,'')) LIKE '%heat%'
  AND NOT EXISTS (SELECT 1 FROM src_meltshop_pg.heats s WHERE s.heat_no = mu.material_code);
"@

    Rule "5 - THE ROW-COUNT DIFFERENCE, RECORDED NOT ASSERTED"
    Say "The frozen task says row counts are NOT expected to be equal and that a"
    Say "test asserting equality would be wrong. So there is no such test here."
    Show -Tag "diff" -Sql @"
SELECT 'coils'  AS entity,
       (SELECT count(*) FROM src_hsm_oracle_shape.hsm_coils) AS staging_rows,
       (SELECT count(*) FROM public.material_units WHERE is_deleted = false
          AND lower(coalesce(material_unit_type,'')) LIKE '%coil%') AS canonical_rows
UNION ALL
SELECT 'heats',
       (SELECT count(*) FROM src_meltshop_pg.heats),
       (SELECT count(*) FROM public.material_units WHERE is_deleted = false
          AND lower(coalesce(material_unit_type,'')) LIKE '%heat%')
UNION ALL
SELECT 'surface defects vs quality events',
       (SELECT count(*) FROM src_inspection_mysql_shape.parsytec_surface_defects),
       (SELECT count(*) FROM public.quality_events WHERE is_deleted = false)
UNION ALL
SELECT 'source downtime vs canonical downtime',
       (SELECT count(*) FROM src_inspection_mysql_shape.downtime_events),
       (SELECT count(*) FROM public.downtime_events WHERE is_deleted = false)
UNION ALL
SELECT 'pass measurements vs parameter observations',
       (SELECT count(*) FROM src_hsm_oracle_shape.hsm_pass_measurements),
       (SELECT count(*) FROM public.parameter_observations WHERE is_deleted = false)
ORDER BY 1;
"@
    Say "Any difference above is EXPECTED. One layer is source-shaped and one is"
    Say "canonical; a coil row carrying target and actual pairs becomes several"
    Say "parameter observations, so the counts cannot agree."

    Rule "6 - A CROSS-REFERENCE TO THE T-029 DENSITY GAP"
    Say "T-029 recorded the density cross-check as NOT COMPUTABLE because canonical"
    Say "has no WEIGHT_KG or LENGTH_MM parameter code. Staging is where they live:"
    Show -Tag "weight" -Sql @"
SELECT 'src_hsm_oracle_shape.hsm_coils.coil_weight_kg' AS source_column,
       count(coil_weight_kg) AS populated_rows,
       round(min(coil_weight_kg)::numeric, 1) AS min_kg,
       round(max(coil_weight_kg)::numeric, 1) AS max_kg
FROM src_hsm_oracle_shape.hsm_coils;
"@
    Say "So the weight EXISTS at the source and is not projected into canonical."
    Say "That is a T-029 emission finding, recorded here for cross-reference only."
    Say "T-030 does not change the emission - that is not its scope."
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:\PGOPTIONS -ErrorAction SilentlyContinue
}

Rule "T-030 CLAUSE GATE"
$bad = 0
foreach ($cl in @("POPULATED", "UNPREPARED", "IDENTITY")) {
    $v = "NOT REACHED"
    if ($script:clause.ContainsKey($cl)) {
        $n = $script:clause[$cl]
        if ($n -eq 999) { $v = "ERROR" }
        elseif ($n -eq 0) { $v = "PASS - zero offending" }
        else { $v = "FAIL - " + $n + " offending" }
    }
    Say ("  " + $cl.PadRight(14) + $v)
    if (-not $script:clause.ContainsKey($cl)) { $bad = $bad + 1 }
    elseif ($script:clause[$cl] -ne 0) { $bad = $bad + 1 }
}
Say ("  " + "ROW COUNTS".PadRight(14) + "RECORDED - difference is expected, not a defect")
Say ("  " + "SURFACES".PadRight(14) + "NOT VERIFIABLE HERE - schema tree, canvas, SQL editor")
Say ("  " + "".PadRight(14) + "and preview are frontend surfaces. A row count proves")
Say ("  " + "".PadRight(14) + "nothing about them, so no claim is made.")

$outFolder = Join-Path $repoRoot "docs\m1\evidence"
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$ev = Join-Path $outFolder ("T-030_staging_verification_" + $stamp + ".txt")
$clean = New-Object System.Text.StringBuilder
foreach ($ch in ($script:log -replace "`r`n", "`n").ToCharArray()) {
    if ([int]$ch -le 126 -or [int]$ch -eq 10) { [void]$clean.Append($ch) }
}
[System.IO.File]::WriteAllText($ev, ($clean.ToString() -replace "`n", "`r`n"),
    (New-Object System.Text.UTF8Encoding($false)))
Write-Host ""
Write-Host ("Evidence : " + $ev)
if ($bad -gt 0) { exit 1 }
exit 0
