#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-029 - five-layer realism audit of the emulated plant. One check set per
    layer, offending rows counted mechanically. READ ONLY except the trigger
    confirmation, which rolls back.

.DESCRIPTION
    LAYERS, IN THE ORDER THE FROZEN TASK GIVES THEM:
      1 STRUCTURAL   key continuity, heat to slab to coil to inspection
      2 PHYSICAL     plausibility AND cross-field consistency, incl. the density
                     cross-check the validation names explicitly
      3 TEMPORAL     no step preceding its predecessor
      4 STATISTICAL  natural variation rather than uniform random
      5 ANALYTICAL   every phenomenon in the manifest discoverable through the
                     product rather than asserted

    VERDICTS ARE PARSED FROM RAW DELIMITED OUTPUT, never from psql table borders.
    A regex that quietly matched nothing would otherwise report zero offending
    rows, which is the one failure mode this audit cannot afford.

    TWO THINGS ARE DISCOVERED, NOT ASSUMED, because the live database has already
    been shown to carry objects that no tracked script contains:

      THE TRIGGER. The validation says the genealogy weight check is already
      enforced by a database trigger. This runner ENUMERATES pg_trigger on the
      live database and exercises whatever weight-related trigger it finds. If
      none exists, it lists every trigger it did find and records the clause as
      unmet - it does not conclude from the repository.

      THE DENSITY INPUTS. The cross-check needs width, thickness, length and
      weight. The runner reports which of the four are present as parameter codes
      and computes the check only if all are. If any is missing it reports NOT
      COMPUTABLE naming the missing inputs, rather than substituting a proxy and
      calling the result a density test.

    THE TRIGGER TEST NEEDS A WRITABLE TRANSACTION. A read-only connection would
    reject the invalid insert with a read-only error rather than with the trigger,
    which would prove nothing. So layers 1-5 run read-only and the trigger section
    opens a separate writable session that ends in ROLLBACK, then verifies nothing
    was left behind.

.EXAMPLE
    .\tools\run\Invoke-PpiqT029RealismAudit.ps1
#>

[CmdletBinding()]
param(
    [string]$PgHost     = "127.0.0.1",
    [int]   $PgPort     = 5432,
    [string]$PgUser     = "ppiq_dev",
    [string]$PgPassword = "ppiq_dev_local_only",
    [string]$Database   = "ppiq_presentation",
    [string]$PsqlPath   = "",
    [double]$DensityTolerancePct = 15.0
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

$script:log = ""
$script:layerFail = @{}
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
    param([string]$Sql, [string]$Tag, [switch]$Raw, [switch]$Writable)
    $f = Join-Path $script:tmp ($Tag + ".sql")
    [System.IO.File]::WriteAllText($f, $Sql, (New-Object System.Text.UTF8Encoding($false)))
    $o = Join-Path $script:tmp ($Tag + ".out")
    $e = Join-Path $script:tmp ($Tag + ".err")
    if ($Writable) { $env:PGOPTIONS = "" } else { $env:PGOPTIONS = "-c default_transaction_read_only=on" }
    $a = @("-X", "-w", "-h", $PgHost, "-p", "$PgPort", "-U", $PgUser, "-d", $Database)
    if (-not $Writable) { $a += @("-v", "ON_ERROR_STOP=1") }
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
# Every layer verdict comes through here: raw "name~count" lines, counted, and a
# layer that produces no parsable line FAILS rather than passing on silence.
function Judge {
    param([string]$Layer, [string]$Sql, [string]$Tag)
    $r = Invoke-Sql -Sql $Sql -Tag $Tag -Raw
    if ($r.ExitCode -ne 0) {
        Say ("  [FAIL] " + $Layer + " query error: " + ($r.Error -replace "`r", "" -replace "`n", " ").Trim())
        $script:layerFail[$Layer] = 999
        return
    }
    $seen = 0; $offending = 0
    foreach ($raw in ($r.Output -split "`n")) {
        $line = $raw.Trim()
        if ($line -eq "") { continue }
        $p = $line -split "~"
        if ($p.Count -ne 2) { continue }
        $seen = $seen + 1
        $n = 0
        if ($p[1] -match "^-?\d+$") { $n = [int]$p[1] }
        $mark = "ok"
        if ($n -ne 0) { $mark = "OFFENDING"; $offending = $offending + $n }
        Say ("    " + $p[0].PadRight(56) + $p[1].PadLeft(8) + "  " + $mark)
    }
    if ($seen -eq 0) {
        Say ("  [FAIL] " + $Layer + " produced no parsable check. Refusing to report zero.")
        $script:layerFail[$Layer] = 999
        return
    }
    Say ("  checks parsed " + $seen + ", total offending rows " + $offending)
    $script:layerFail[$Layer] = $offending
}

Rule "PPIQ T-029 v2 - FIVE-LAYER REALISM AUDIT"
$repoRoot = (Get-Location).Path
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Write-Host "[FAIL] psql.exe not found."; exit 2 }
$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t029_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
Say ("Database : " + $Database)

try {
    $ro = Invoke-Sql -Tag "ro" -Raw -Sql "SHOW transaction_read_only;"
    Say ("Read-only for layers 1-5 : " + $ro.Output.Trim() + " (required on)")
    if ($ro.Output.Trim() -ne "on") { Say "[STOP] not read-only."; exit 2 }

    # ---------------------------------------------------------------- 0
    Rule "0 - WHAT EXISTS, DISCOVERED NOT ASSUMED"
    Say "The eight declared sources are Meltshop PostgreSQL, Caster Oracle, HSM"
    Say "Oracle, PKL MSSQL, Downtime MySQL, Parsytec MySQL, Yard file, QA file."
    Say "Whether a source-shaped representation exists in this database decides"
    Say "whether the structural layer can span them or only the canonical chain."
    Show -Tag "schemas" -Sql @"
SELECT n.nspname AS schema_name, count(c.oid) AS tables
FROM pg_namespace n
LEFT JOIN pg_class c ON c.relnamespace = n.oid AND c.relkind = 'r'
WHERE n.nspname NOT IN ('pg_catalog','information_schema','pg_toast')
  AND n.nspname NOT LIKE 'pg_temp%' AND n.nspname NOT LIKE 'pg_toast%'
GROUP BY n.nspname ORDER BY n.nspname;
"@
    Say "genealogy_edges columns, ASSERTED BEFORE ANY LAYER USES THEM. v1 of this"
    Say "runner guessed parent_unit_id and child_unit_id, which do not exist, and"
    Say "two layers errored mid-run. A preflight turns that into a refusal that"
    Say "prints the real names."
    Show -Tag "gecols" -Sql @"
SELECT column_name, data_type, is_nullable
FROM information_schema.columns
WHERE table_schema = 'public' AND table_name = 'genealogy_edges'
ORDER BY ordinal_position;
"@
    $ge = Invoke-Sql -Tag "gecheck" -Raw -Sql @"
SELECT count(*)::text FROM (VALUES ('parent_material_unit_id'), ('child_material_unit_id'),
                                   ('contribution_weight')) AS c(needed)
WHERE NOT EXISTS (SELECT 1 FROM information_schema.columns
                  WHERE table_schema = 'public' AND table_name = 'genealogy_edges'
                    AND column_name = c.needed);
"@
    $geMissing = 9
    foreach ($line in ($ge.Output -split "`n")) {
        $tk = $line.Trim()
        if ($tk -match "^\d+$") { $geMissing = [int]$tk; break }
    }
    Say ("  expected genealogy columns missing : " + $geMissing + " (required 0)")
    if ($geMissing -ne 0) {
        Say "[STOP] the genealogy edge columns are not the ones this runner expects."
        Say "       The real names are listed above. Refusing to run layers against"
        Say "       columns that do not exist."
        exit 2
    }

    Say "Material unit types present, which is what heat/slab/coil continuity runs on:"
    Show -Tag "unittypes" -Sql @"
SELECT material_unit_type, count(*) AS units
FROM public.material_units WHERE is_deleted = false
GROUP BY material_unit_type ORDER BY count(*) DESC;
"@

    # ---------------------------------------------------------------- 1
    Rule "1 - STRUCTURAL: KEY CONTINUITY"
    Judge -Layer "STRUCTURAL" -Tag "l1" -Sql @"
SELECT 'coils with no genealogy parent'                 || '~' || count(*)::text FROM public.material_units mu
  WHERE mu.is_deleted = false AND lower(coalesce(mu.material_unit_type,'')) LIKE '%coil%'
    AND NOT EXISTS (SELECT 1 FROM public.genealogy_edges ge WHERE ge.child_material_unit_id = mu.id)
UNION ALL
SELECT 'slabs with no genealogy parent'                 || '~' || count(*)::text FROM public.material_units mu
  WHERE mu.is_deleted = false AND lower(coalesce(mu.material_unit_type,'')) LIKE '%slab%'
    AND NOT EXISTS (SELECT 1 FROM public.genealogy_edges ge WHERE ge.child_material_unit_id = mu.id)
UNION ALL
SELECT 'genealogy edges with a dangling parent'         || '~' || count(*)::text FROM public.genealogy_edges ge
  LEFT JOIN public.material_units p ON p.id = ge.parent_material_unit_id WHERE p.id IS NULL
UNION ALL
SELECT 'genealogy edges with a dangling child'          || '~' || count(*)::text FROM public.genealogy_edges ge
  LEFT JOIN public.material_units c ON c.id = ge.child_material_unit_id WHERE c.id IS NULL
UNION ALL
SELECT 'quality events with an unresolved material'     || '~' || count(*)::text FROM public.quality_events qe
  LEFT JOIN public.material_units mu ON mu.id = qe.material_unit_id
  WHERE qe.is_deleted = false AND mu.id IS NULL
UNION ALL
SELECT 'parameter observations with unresolved material'|| '~' || count(*)::text FROM public.parameter_observations po
  LEFT JOIN public.material_units mu ON mu.id = po.material_unit_id
  WHERE po.is_deleted = false AND mu.id IS NULL
UNION ALL
SELECT 'material units with no site'                    || '~' || count(*)::text FROM public.material_units
  WHERE is_deleted = false AND site_id IS NULL
UNION ALL
SELECT 'coils that do not resolve to a heat'            || '~' || count(*)::text FROM public.material_units mu
  WHERE mu.is_deleted = false AND lower(coalesce(mu.material_unit_type,'')) LIKE '%coil%'
    AND NOT EXISTS (SELECT 1 FROM public.ppiq_ml_unit_heat_lineage lin WHERE lin.unit_id = mu.id);
"@

    # ---------------------------------------------------------------- 2
    Rule "2 - PHYSICAL: PLAUSIBILITY AND CROSS-FIELD CONSISTENCY"
    Say "Plausibility uses each parameter's OWN declared expected_min_value and"
    Say "expected_max_value. No range is invented here."
    Judge -Layer "PHYSICAL" -Tag "l2" -Sql @"
SELECT 'observations outside their declared range' || '~' || count(*)::text
FROM public.parameter_observations po
JOIN public.parameter_definitions pd ON pd.id = po.parameter_definition_id AND pd.is_deleted = false
WHERE po.is_deleted = false AND po.numeric_value IS NOT NULL
  AND pd.expected_min_value IS NOT NULL AND pd.expected_max_value IS NOT NULL
  AND (po.numeric_value < pd.expected_min_value OR po.numeric_value > pd.expected_max_value)
UNION ALL
SELECT 'numeric observations that are NaN or infinite' || '~' || count(*)::text
FROM public.parameter_observations po
WHERE po.is_deleted = false AND po.numeric_value IS NOT NULL
  AND (po.numeric_value::text IN ('NaN','Infinity','-Infinity'))
UNION ALL
SELECT 'negative values on a strictly positive quantity' || '~' || count(*)::text
FROM public.parameter_observations po
JOIN public.parameter_definitions pd ON pd.id = po.parameter_definition_id AND pd.is_deleted = false
WHERE po.is_deleted = false AND po.numeric_value < 0
  AND pd.parameter_code IN ('THICKNESS_MM','WIDTH_MM','ROLL_FORCE_KN','LINE_SPEED_MPM','POWER_KWH')
UNION ALL
SELECT 'downtime ending before it started' || '~' || count(*)::text
FROM public.downtime_events
WHERE is_deleted = false AND ended_at_utc IS NOT NULL AND ended_at_utc < started_at_utc
UNION ALL
SELECT 'negative stopped or impact minutes' || '~' || count(*)::text
FROM public.downtime_events
WHERE is_deleted = false AND (stopped_minutes < 0 OR production_impact_minutes < 0);
"@

    Say ""
    Say "THE DENSITY CROSS-CHECK. Inputs required: width, thickness, length, weight."
    Show -Tag "densinputs" -Sql @"
SELECT c.needed AS required_input,
       CASE WHEN pd.parameter_code IS NULL THEN 'ABSENT - no such parameter code'
            ELSE 'present' END AS availability
FROM (VALUES ('WIDTH_MM'), ('THICKNESS_MM'), ('LENGTH_MM'), ('WEIGHT_KG')) AS c(needed)
LEFT JOIN public.parameter_definitions pd
       ON upper(pd.parameter_code) = c.needed AND pd.is_deleted = false
ORDER BY c.needed;
"@
    $dens = Invoke-Sql -Tag "densraw" -Raw -Sql @"
SELECT count(*)::text FROM (VALUES ('WIDTH_MM'), ('THICKNESS_MM'), ('LENGTH_MM'), ('WEIGHT_KG')) AS c(needed)
WHERE NOT EXISTS (SELECT 1 FROM public.parameter_definitions pd
                  WHERE upper(pd.parameter_code) = c.needed AND pd.is_deleted = false);
"@
    $missing = 9
    foreach ($line in ($dens.Output -split "`n")) {
        $t = $line.Trim()
        if ($t -match "^\d+$") { $missing = [int]$t; break }
    }
    if ($missing -eq 0) {
        Say ("All four inputs present. Computing derived volume x 7850 kg/m3 against")
        Say ("stated weight, tolerance " + $DensityTolerancePct + " percent.")
        Judge -Layer "DENSITY" -Tag "l2b" -Sql @"
WITH dims AS (
  SELECT po.material_unit_id AS unit_id,
         max(CASE WHEN upper(pd.parameter_code)='WIDTH_MM'     THEN po.numeric_value END) AS w,
         max(CASE WHEN upper(pd.parameter_code)='THICKNESS_MM' THEN po.numeric_value END) AS t,
         max(CASE WHEN upper(pd.parameter_code)='LENGTH_MM'    THEN po.numeric_value END) AS l,
         max(CASE WHEN upper(pd.parameter_code)='WEIGHT_KG'    THEN po.numeric_value END) AS kg
  FROM public.parameter_observations po
  JOIN public.parameter_definitions pd ON pd.id = po.parameter_definition_id AND pd.is_deleted = false
  WHERE po.is_deleted = false GROUP BY po.material_unit_id)
SELECT 'units whose implied density breaches tolerance' || '~' || count(*)::text
FROM dims
WHERE w IS NOT NULL AND t IS NOT NULL AND l IS NOT NULL AND kg IS NOT NULL AND kg > 0
  AND abs(((w/1000.0)*(t/1000.0)*(l/1000.0)*7850.0 - kg) / kg) * 100.0 > $DensityTolerancePct;
"@
    } else {
        Say ("  NOT COMPUTABLE - " + $missing + " of the four required inputs are absent")
        Say "  from the canonical parameter set. The check is recorded as unmet rather"
        Say "  than substituting a proxy and calling the result a density test."
        Say "  The generator's own header records FAULT-1: weight_kg is drawn"
        Say "  independently of width, thickness and length, so the inconsistency this"
        Say "  check exists to catch is a KNOWN donor-side fault that canonical does"
        Say "  not currently carry the columns to expose."
        $script:layerFail["DENSITY"] = -1
    }

    # ---------------------------------------------------------------- 3
    Rule "3 - TEMPORAL: NO STEP PRECEDING ITS PREDECESSOR"
    Judge -Layer "TEMPORAL" -Tag "l3" -Sql @"
SELECT 'child produced before its genealogy parent' || '~' || count(*)::text
FROM public.genealogy_edges ge
JOIN public.material_units p ON p.id = ge.parent_material_unit_id AND p.is_deleted = false
JOIN public.material_units c ON c.id = ge.child_material_unit_id AND c.is_deleted = false
WHERE p.production_start_utc IS NOT NULL AND c.production_start_utc IS NOT NULL
  AND c.production_start_utc < p.production_start_utc
UNION ALL
SELECT 'material unit ending before it started' || '~' || count(*)::text
FROM public.material_units
WHERE is_deleted = false AND production_end_utc IS NOT NULL
  AND production_start_utc IS NOT NULL AND production_end_utc < production_start_utc
UNION ALL
SELECT 'quality event before its material was produced' || '~' || count(*)::text
FROM public.quality_events qe
JOIN public.material_units mu ON mu.id = qe.material_unit_id AND mu.is_deleted = false
WHERE qe.is_deleted = false AND mu.production_start_utc IS NOT NULL
  AND qe.event_at_utc < mu.production_start_utc
UNION ALL
SELECT 'parameter observed before its material was produced' || '~' || count(*)::text
FROM public.parameter_observations po
JOIN public.material_units mu ON mu.id = po.material_unit_id AND mu.is_deleted = false
WHERE po.is_deleted = false AND mu.production_start_utc IS NOT NULL
  AND po.observed_at_utc < mu.production_start_utc
UNION ALL
SELECT 'process step ending before it started' || '~' || count(*)::text
FROM public.process_step_executions
WHERE is_deleted = false AND ended_at_utc IS NOT NULL AND started_at_utc IS NOT NULL
  AND ended_at_utc < started_at_utc;
"@

    # ---------------------------------------------------------------- 4
    Rule "4 - STATISTICAL: NATURAL VARIATION, NOT UNIFORM RANDOM"
    Say "A uniform variable has an interquartile range of almost exactly half its"
    Say "full range. A naturally varying one is materially narrower. Offending =="
    Say "a parameter whose IQR/range sits in 0.48..0.52, the uniform signature."
    Show -Tag "l4detail" -Sql @"
WITH q AS (
  SELECT pd.parameter_code,
         count(*) AS n,
         percentile_cont(0.25) WITHIN GROUP (ORDER BY po.numeric_value) AS p25,
         percentile_cont(0.75) WITHIN GROUP (ORDER BY po.numeric_value) AS p75,
         min(po.numeric_value) AS lo, max(po.numeric_value) AS hi
  FROM public.parameter_observations po
  JOIN public.parameter_definitions pd ON pd.id = po.parameter_definition_id AND pd.is_deleted = false
  WHERE po.is_deleted = false AND po.numeric_value IS NOT NULL
  GROUP BY pd.parameter_code HAVING count(*) >= 200)
SELECT parameter_code, n,
       round(((p75 - p25) / nullif(hi - lo, 0))::numeric, 4) AS iqr_over_range,
       CASE WHEN ((p75 - p25) / nullif(hi - lo, 0)) BETWEEN 0.48 AND 0.52
            THEN 'UNIFORM SIGNATURE' ELSE 'natural' END AS shape
FROM q ORDER BY 3 DESC;
"@
    Judge -Layer "STATISTICAL" -Tag "l4" -Sql @"
WITH q AS (
  SELECT pd.parameter_code, count(*) AS n,
         percentile_cont(0.25) WITHIN GROUP (ORDER BY po.numeric_value) AS p25,
         percentile_cont(0.75) WITHIN GROUP (ORDER BY po.numeric_value) AS p75,
         min(po.numeric_value) AS lo, max(po.numeric_value) AS hi
  FROM public.parameter_observations po
  JOIN public.parameter_definitions pd ON pd.id = po.parameter_definition_id AND pd.is_deleted = false
  WHERE po.is_deleted = false AND po.numeric_value IS NOT NULL
  GROUP BY pd.parameter_code HAVING count(*) >= 200)
SELECT 'parameters with a uniform-random signature' || '~' || count(*)::text FROM q
WHERE ((p75 - p25) / nullif(hi - lo, 0)) BETWEEN 0.48 AND 0.52
UNION ALL
SELECT 'parameters with zero spread' || '~' || count(*)::text FROM q WHERE hi = lo;
"@

    # ---------------------------------------------------------------- 5
    Rule "5 - ANALYTICAL: DISCOVERABLE THROUGH THE PRODUCT"
    Say "Every phenomenon in the manifest must be reachable by querying the product's"
    Say "own data rather than asserted anywhere. The manifest rows are exercised by"
    Say "Invoke-PpiqPhenomenonHarness.ps1; this layer checks the populations they"
    Say "depend on are present and non-empty in the canonical layer."
    Judge -Layer "ANALYTICAL" -Tag "l5" -Sql @"
SELECT 'coils with a CT_C observation missing' || '~' ||
       CASE WHEN count(*) > 0 THEN 0 ELSE 1 END::text
FROM public.parameter_observations po
JOIN public.parameter_definitions pd ON pd.id = po.parameter_definition_id AND pd.is_deleted = false
WHERE po.is_deleted = false AND pd.parameter_code = 'CT_C'
UNION ALL
SELECT 'downtime pair population missing' || '~' ||
       CASE WHEN count(*) > 0 THEN 0 ELSE 1 END::text
FROM public.downtime_events
WHERE is_deleted = false AND stopped_minutes IS NOT NULL AND production_impact_minutes IS NOT NULL
UNION ALL
SELECT 'SENSOR_ARTEFACT population missing' || '~' ||
       CASE WHEN count(*) > 0 THEN 0 ELSE 1 END::text
FROM public.quality_events qe
JOIN public.defect_catalogs dc ON dc.id = qe.defect_catalog_id AND dc.is_deleted = false
WHERE qe.is_deleted = false AND dc.defect_code = 'SENSOR_ARTEFACT'
UNION ALL
SELECT 'analysis rows asserted without a compute run' || '~' || count(*)::text
FROM public.ml_correlation_results_v2 r
LEFT JOIN public.ml_correlation_compute_runs c ON c.id = r.compute_run_id
WHERE c.id IS NULL;
"@

    # ---------------------------------------------------------------- 6
    Rule "6 - THE GENEALOGY WEIGHT TRIGGER, ENUMERATED THEN EXERCISED"
    Say "The validation says this check is already enforced by a database trigger."
    Say "Every trigger on the material and genealogy tables is listed from"
    Say "pg_trigger, because the live database has been shown to carry objects that"
    Say "no tracked script contains."
    Show -Tag "triggers" -Sql @"
SELECT c.relname AS table_name, t.tgname AS trigger_name,
       pg_get_triggerdef(t.oid) AS definition
FROM pg_trigger t
JOIN pg_class c ON c.oid = t.tgrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE NOT t.tgisinternal AND n.nspname = 'public'
  AND c.relname IN ('genealogy_edges','material_units','process_step_executions')
ORDER BY c.relname, t.tgname;
"@
    $wt = Invoke-Sql -Tag "wtrig" -Raw -Sql @"
SELECT count(*)::text FROM pg_trigger t
JOIN pg_class c ON c.oid = t.tgrelid
JOIN pg_namespace n ON n.oid = c.relnamespace
WHERE NOT t.tgisinternal AND n.nspname = 'public'
  AND (lower(t.tgname) LIKE '%weight%' OR lower(pg_get_triggerdef(t.oid)) LIKE '%weight%');
"@
    $wcount = 0
    foreach ($line in ($wt.Output -split "`n")) {
        $t = $line.Trim()
        if ($t -match "^\d+$") { $wcount = [int]$t; break }
    }
    Say ("  triggers mentioning weight : " + $wcount)
    if ($wcount -eq 0) {
        Say ""
        Say "  NO WEIGHT-CHECK TRIGGER EXISTS on these tables. The validation clause"
        Say "  'confirm it fires by attempting an invalid insert' therefore cannot be"
        Say "  satisfied - there is nothing to fire. Recorded as UNMET with the full"
        Say "  trigger inventory above as the evidence. No trigger was created to"
        Say "  make the clause pass: inventing the guard and then proving it fires"
        Say "  would prove only that this script can write a trigger."
        $script:layerFail["TRIGGER"] = -1
    } else {
        Say "  Exercising it inside a transaction that rolls back."
        Say "  THE GUARD, read from its own definition: contribution weights must sum"
        Say "  to 1.0 per child within 0.015, otherwise it raises."
        Say "  It is DEFERRABLE INITIALLY DEFERRED, so it normally fires at COMMIT -"
        Say "  and a transaction that rolls back would never reach that point. SET"
        Say "  CONSTRAINTS ... IMMEDIATE makes it fire on the statement, which is the"
        Say "  only way to confirm it fires AND still roll back."
        Say "  An UPDATE is used rather than an INSERT because it needs no knowledge"
        Say "  of the table's other NOT NULL columns, and it exercises the same"
        Say "  trigger - the guard fires on INSERT, UPDATE and DELETE alike."
        $r = Invoke-Sql -Tag "trigtest" -Writable -Raw -Sql @"
BEGIN;
SET CONSTRAINTS public.ppiq_genealogy_edge_weight_guard_after_change IMMEDIATE;
DO `$t`$
BEGIN
  UPDATE public.genealogy_edges SET contribution_weight = 0.5
  WHERE ctid = (SELECT ctid FROM public.genealogy_edges
                WHERE COALESCE(is_deleted, false) = false LIMIT 1);
  RAISE NOTICE 'PPIQ-T029-TRIGGER-DID-NOT-FIRE';
EXCEPTION WHEN others THEN
  RAISE NOTICE 'PPIQ-T029-TRIGGER-FIRED: %', SQLERRM;
END
`$t`$;
ROLLBACK;
SELECT 'children whose weights do not sum to 1.0 after rollback~' || count(*)::text
FROM (SELECT child_material_unit_id FROM public.genealogy_edges
      WHERE COALESCE(is_deleted, false) = false
      GROUP BY child_material_unit_id
      HAVING abs(sum(contribution_weight) - 1.0) > 0.015) q;
"@
        Say $r.Output
        Say ($r.Error.Trim())
        $fired = 0
        if ($r.Error -match "PPIQ-T029-TRIGGER-FIRED") {
            if ($r.Error -match "contribution weights must sum") {
                Say "  [OK] the WEIGHT GUARD fired, named by its own message."
                $fired = 1
            } else {
                Say "  [FAIL] something raised, but NOT the weight guard. v1 of this"
                Say "         runner recorded exactly this kind of false positive - a"
                Say "         column error read as the trigger firing."
            }
        }
        $left = 0
        foreach ($line in ($r.Output -split "`n")) {
            $pp = $line.Trim() -split "~"
            if ($pp.Count -eq 2 -and $pp[1] -match "^\d+$") { $left = [int]$pp[1] }
        }
        Say ("  children off the 1.0 weight sum after rollback : " + $left + " (required 0)")
        if ($fired -eq 1 -and $left -eq 0) {
            $script:layerFail["TRIGGER"] = 0
        } else {
            $script:layerFail["TRIGGER"] = 1
        }
    }
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
    Remove-Item Env:\PGOPTIONS -ErrorAction SilentlyContinue
}

Rule "T-029 LAYER GATE - ZERO OFFENDING ROWS REQUIRED PER LAYER"
$bad = 0
foreach ($layer in @("STRUCTURAL", "PHYSICAL", "DENSITY", "TEMPORAL", "STATISTICAL", "ANALYTICAL", "TRIGGER")) {
    $v = "NOT REACHED"
    if ($script:layerFail.ContainsKey($layer)) {
        $n = $script:layerFail[$layer]
        if ($n -eq -1) { $v = "NOT COMPUTABLE - recorded as unmet, not passed" }
        elseif ($n -eq 999) { $v = "ERROR" }
        elseif ($n -eq 0) { $v = "PASS - zero offending rows" }
        else { $v = "FAIL - " + $n + " offending row(s)" }
    }
    Say ("  " + $layer.PadRight(14) + $v)
    if ($script:layerFail.ContainsKey($layer)) {
        if ($script:layerFail[$layer] -ne 0) { $bad = $bad + 1 }
    } else { $bad = $bad + 1 }
}
Say ""
Say "A layer that could not be computed is NOT a pass. It is recorded as unmet so"
Say "that a later reader cannot mistake an absent check for a clean one."

$outFolder = Join-Path $repoRoot "docs\m1\evidence"
if (-not (Test-Path -LiteralPath $outFolder)) {
    New-Item -ItemType Directory -Path $outFolder -Force | Out-Null
}
$ev = Join-Path $outFolder ("T-029_realism_audit_v2_" + $stamp + ".txt")
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
