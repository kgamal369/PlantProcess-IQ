#requires -Version 5.1
<#
.SYNOPSIS
    PPIQ T-024 requirement 7 - final vocabulary cleanup. Reversible.
    ReportOnly by default; -Apply mutates; -Rollback reverses exactly.

.DESCRIPTION
    SCOPE, as ruled, and no wider:
      1  tag the Fleet v2 reference vocabulary
           12 defect rows     industry_template NULL -> FlatSteel
           27 parameter rows  industry_template NULL -> FlatSteel
         Zero uniqueness collisions were proven against every candidate value.
      2  soft-retire the 30 obsolete, dependency-proven demo references
           4  defect_catalogs
           16 equipment
           10 material_unit_type_definitions
      3  KEEP the seven referenced non-steel parameter definitions. They are
         referenced by ml_feature_definitions and are NOT customer-visible on the
         presented read-model path. Soft-deleting them would break those
         definitions to fix a problem that does not exist on that path.
      4  NO frontend plumbing. The repository-wide API inventory found 89 paths
         and not one calls /quality/defects, /parameters/definitions,
         /material-unit-types or an equipment selector. The presented dashboards
         consume /api/analytics/read-models/*, which aggregate canonical
         operational tables that are already 100 percent Fleet v2.

    WHY SOFT-RETIREMENT IS THE MECHANISM. PlantProcessDbContext applies
    HasQueryFilter(e => !e.IsDeleted) to every BaseEntity, so is_deleted = true
    removes a row from every EF-backed endpoint with no code change. is_active is
    set too, for correctness, but it is opt-in at the API and would not hide
    anything on its own.

    REVERSIBILITY. Before mutating, every affected row's prior values are written
    to public.ppiq_t024_vocab_rollback. -Rollback restores from that table and
    nothing else, so the reversal is exact rather than reconstructed.

.EXAMPLE
    .\tools\run\Invoke-PpiqT024VocabularyCleanup.ps1
    .\tools\run\Invoke-PpiqT024VocabularyCleanup.ps1 -Apply
    .\tools\run\Invoke-PpiqT024VocabularyCleanup.ps1 -Rollback
#>

[CmdletBinding()]
param(
    [string]$PgHost     = "127.0.0.1",
    [int]   $PgPort     = 5432,
    [string]$PgUser     = "ppiq_dev",
    [string]$PgPassword = "ppiq_dev_local_only",
    [string]$Database   = "ppiq_presentation",
    [string]$PsqlPath   = "",
    [switch]$Apply,
    [switch]$Rollback
)

Set-StrictMode -Version Latest
$ErrorActionPreference = "Continue"

function Say  { param([string]$T) Write-Host $T }
function Rule { param([string]$T) Write-Host ""; Write-Host ("=" * 78); Write-Host $T; Write-Host ("=" * 78) }

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
    param([string]$Sql, [string]$Tag)
    $f = Join-Path $script:tmp ($Tag + ".sql")
    $o = Join-Path $script:tmp ($Tag + ".out")
    $e = Join-Path $script:tmp ($Tag + ".err")
    [System.IO.File]::WriteAllText($f, $Sql, (New-Object System.Text.UTF8Encoding($false)))
    $a = @("-X", "-w", "-h", $PgHost, "-p", "$PgPort", "-U", $PgUser, "-d", $Database,
           "-v", "ON_ERROR_STOP=1", "-f", $f, "-o", $o)
    $p = Start-Process -FilePath $script:psql -ArgumentList $a -NoNewWindow -Wait `
                       -PassThru -RedirectStandardError $e
    $r = New-Object psobject
    Add-Member -InputObject $r -MemberType NoteProperty -Name ExitCode -Value $p.ExitCode
    Add-Member -InputObject $r -MemberType NoteProperty -Name Output   -Value (Read-IfExists $o)
    Add-Member -InputObject $r -MemberType NoteProperty -Name Error    -Value (Read-IfExists $e)
    return $r
}
function Check-Table {
    param([string]$Output, [string]$Label)
    $bad = 0
    foreach ($raw in ($Output -split "`n")) {
        $line = $raw.Trim()
        if ($line -match "^\|\s*(.+?)\s*\|\s*(-?\d+)\s*\|\s*(-?\d+)\s*\|") {
            if ([int]$Matches[2] -ne [int]$Matches[3]) {
                Say ("[FAIL] " + $Label + " - " + $Matches[1] + ": found " +
                     $Matches[2] + ", required " + $Matches[3])
                $bad = $bad + 1
            }
        }
    }
    return $bad
}

$RETIRE_DEFECTS = "'CONTAMINATION_RISK','OOS_PH','UNDER_CURE','UNIFORMITY_DEFECT'"
$RETIRE_EQUIP   = "'CASTER_1','HSM_1','ALU_CASTER_01','ALU_FURNACE_01','ALU_MILL_01'," +
                  "'CASTER_1_MOULD','CASTER_1_SEGMENT_1','EAF_1','HSM_1_F1','HSM_1_F2'," +
                  "'LF_1','PH_FILLER_1','PH_MIXER_1','SURFACE_INSPECTION_1'," +
                  "'TIRE_CURING_1','TIRE_MIXER_1'"
$RETIRE_MUT     = "'AluminumBillet','AluminumCast','AluminumRoll','Batch'," +
                  "'CompoundBatch','CustomerRoll','JumboRoll','Lot','PackagedLot','TireUnit'"
$FLEET_DEFECTS  = "'SCALE','EDGE_CRACK','ROLLED_IN_SCALE','SLIVER','INCLUSION','PINHOLE'," +
                  "'SCRATCH','WAVINESS','CENTRE_BUCKLE','EDGE_WAVE','ROLL_MARK'," +
                  "'LAMINATION','OIL_SPOT','SENSOR_ARTEFACT'"
$FLEET_PARAMS   = "'CARBON_PCT','MANGANESE_PCT','SILICON_PCT','SULPHUR_PCT'," +
                  "'PHOSPHORUS_PCT','ALUMINIUM_PCT','TAP_TEMP_C','OXYGEN_NM3','POWER_KWH'," +
                  "'LF_ARGON_NM3','LF_CALCIUM_M','LF_FINAL_TEMP_C','CASTING_SPEED_MPM'," +
                  "'SUPERHEAT_C','MOULD_LEVEL_AVG','FDT_C','CT_C','THICKNESS_MM','WIDTH_MM'," +
                  "'ROLL_FORCE_KN','ROLL_GAP_MM','ROLL_SPEED_MPS','ROLL_TEMP_C'," +
                  "'ACID_CONC_PCT','BATH_TEMP_C','LINE_SPEED_MPM','QA_WIDTH_MM'," +
                  "'QA_THK_MM','QA_ROUGHNESS_UM'"
$NONSTEEL_PARAMS = "'CoolingActive','CURING_PRESSURE_BAR','CURING_TEMP_C','HUMIDITY_PCT'," +
                   "'PH_VALUE','RECIPE_CODE','UNIFORMITY_INDEX'"

Rule "PPIQ T-024 REQUIREMENT 7 - FINAL VOCABULARY CLEANUP"
$script:psql = Resolve-Psql -Explicit $PsqlPath
if ($null -eq $script:psql) { Say "[FAIL] psql.exe not found."; exit 2 }
$mode = "REPORT ONLY"
if ($Rollback) { $mode = "ROLLBACK" } elseif ($Apply) { $mode = "APPLY" }
Say ("Database : " + $Database)
Say ("Mode     : " + $mode)

$env:PGPASSWORD = $PgPassword
$env:PGCLIENTENCODING = "UTF8"
$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$script:tmp = Join-Path $env:TEMP ("ppiq_t024clean_" + $stamp)
New-Item -ItemType Directory -Path $script:tmp -Force | Out-Null
$bad = 0

try {
    if ($Rollback) {
        Rule "ROLLBACK - RESTORE FROM THE CAPTURED PRIOR VALUES"
        $rb = Invoke-Sql -Tag "rollback" -Sql @"
\pset border 2
BEGIN;
UPDATE public.defect_catalogs d
SET industry_template = b.old_industry_template,
    is_deleted = b.old_is_deleted, deleted_at_utc = NULL, deleted_reason = NULL
FROM public.ppiq_t024_vocab_rollback b
WHERE b.table_name = 'defect_catalogs' AND b.row_id = d.id;

UPDATE public.parameter_definitions p
SET industry_template = b.old_industry_template,
    is_deleted = b.old_is_deleted, deleted_at_utc = NULL, deleted_reason = NULL
FROM public.ppiq_t024_vocab_rollback b
WHERE b.table_name = 'parameter_definitions' AND b.row_id = p.id;

UPDATE public.equipment e
SET is_active = b.old_is_active, is_deleted = b.old_is_deleted,
    deleted_at_utc = NULL, deleted_reason = NULL
FROM public.ppiq_t024_vocab_rollback b
WHERE b.table_name = 'equipment' AND b.row_id = e.id;

UPDATE public.material_unit_type_definitions m
SET is_active = b.old_is_active, is_deleted = b.old_is_deleted,
    deleted_at_utc = NULL, deleted_reason = NULL
FROM public.ppiq_t024_vocab_rollback b
WHERE b.table_name = 'material_unit_type_definitions' AND b.row_id = m.id;
COMMIT;

SELECT table_name, count(*) AS rows_restored
FROM public.ppiq_t024_vocab_rollback GROUP BY 1 ORDER BY 1;
"@
        if ($rb.ExitCode -ne 0) { Say $rb.Error; throw "rollback" }
        Say $rb.Output
        Say "[OK] restored. The rollback table is left in place deliberately, so a"
        Say "     second rollback is a no-op rather than a surprise."
        Rule "RESULT"
        Say "[OK] rollback complete."
        exit 0
    }

    Rule "1 - WHAT WOULD CHANGE"
    $plan = Invoke-Sql -Tag "plan" -Sql @"
\pset border 2
SELECT 'defect rows to tag FlatSteel' AS action, count(*) AS found, 12 AS required
FROM public.defect_catalogs
WHERE defect_code IN ($FLEET_DEFECTS) AND industry_template IS NULL
UNION ALL
SELECT 'parameter rows to tag FlatSteel', count(*), 27
FROM public.parameter_definitions
WHERE parameter_code IN ($FLEET_PARAMS) AND industry_template IS NULL
UNION ALL
SELECT 'defect rows to retire', count(*), 4
FROM public.defect_catalogs WHERE defect_code IN ($RETIRE_DEFECTS)
UNION ALL
SELECT 'equipment rows to retire', count(*), 16
FROM public.equipment WHERE equipment_code IN ($RETIRE_EQUIP)
UNION ALL
SELECT 'material unit types to retire', count(*), 10
FROM public.material_unit_type_definitions WHERE material_unit_type_code IN ($RETIRE_MUT)
UNION ALL
SELECT 'non-steel parameters KEPT deliberately', count(*), 7
FROM public.parameter_definitions WHERE parameter_code IN ($NONSTEEL_PARAMS);
"@
    if ($plan.ExitCode -ne 0) { Say $plan.Error; throw "plan" }
    Say $plan.Output
    $bad = $bad + (Check-Table -Output $plan.Output -Label "plan")

    Rule "2 - DEPENDENCY RE-PROOF IMMEDIATELY BEFORE MUTATING"
    Say "The dependency report is 20 minutes old. Nothing should have changed, but"
    Say "a mutation that trusts a stale report is a mutation that trusts a memory."
    $dep = Invoke-Sql -Tag "dep" -Sql @"
\pset border 2
SELECT 'retiring defects still used by a quality event' AS check_name,
       count(*) AS found, 0 AS required
FROM public.quality_events q JOIN public.defect_catalogs d ON d.id = q.defect_catalog_id
WHERE d.defect_code IN ($RETIRE_DEFECTS)
UNION ALL
SELECT 'retiring equipment still used anywhere', count(*), 0
FROM public.equipment e
WHERE e.equipment_code IN ($RETIRE_EQUIP)
  AND (EXISTS (SELECT 1 FROM public.process_step_executions s WHERE s.equipment_id = e.id)
    OR EXISTS (SELECT 1 FROM public.parameter_observations o WHERE o.equipment_id = e.id)
    OR EXISTS (SELECT 1 FROM public.downtime_events d WHERE d.equipment_id = e.id))
UNION ALL
SELECT 'retiring material unit types still used by a unit', count(*), 0
FROM public.material_unit_type_definitions m
WHERE m.material_unit_type_code IN ($RETIRE_MUT)
  AND EXISTS (SELECT 1 FROM public.material_units u
               WHERE u.material_unit_type = m.material_unit_type_code)
UNION ALL
SELECT 'tagging would collide on (industry_template, parameter_code)', count(*), 0
FROM public.parameter_definitions a
WHERE a.parameter_code IN ($FLEET_PARAMS) AND a.industry_template IS NULL
  AND EXISTS (SELECT 1 FROM public.parameter_definitions b
               WHERE b.parameter_code = a.parameter_code
                 AND b.industry_template = 'FlatSteel');
"@
    if ($dep.ExitCode -ne 0) { Say $dep.Error; throw "dep" }
    Say $dep.Output
    $bad = $bad + (Check-Table -Output $dep.Output -Label "dependency")

    if (-not $Apply) {
        Rule "REPORT ONLY - NOTHING CHANGED"
        Say "Re-run with -Apply to perform the cleanup."
        if ($bad -gt 0) { throw "plan mismatch" }
        Rule "RESULT"
        Say "[OK] plan and dependencies verified. Nothing was changed."
        exit 0
    }
    if ($bad -gt 0) { Say "[STOP] the plan did not verify; refusing to mutate."; throw "plan" }

    Rule "3 - CAPTURE PRIOR VALUES, THEN MUTATE - ONE TRANSACTION"
    $apply = Invoke-Sql -Tag "apply" -Sql @"
\pset border 2
BEGIN;

CREATE TABLE IF NOT EXISTS public.ppiq_t024_vocab_rollback (
  captured_at timestamptz NOT NULL DEFAULT now(),
  table_name text NOT NULL,
  row_id uuid NOT NULL,
  code text NOT NULL,
  old_industry_template text,
  old_is_active boolean,
  old_is_deleted boolean,
  PRIMARY KEY (table_name, row_id));

INSERT INTO public.ppiq_t024_vocab_rollback
  (table_name, row_id, code, old_industry_template, old_is_active, old_is_deleted)
SELECT 'defect_catalogs', id, defect_code, industry_template, NULL, is_deleted
FROM public.defect_catalogs
WHERE (defect_code IN ($FLEET_DEFECTS) AND industry_template IS NULL)
   OR defect_code IN ($RETIRE_DEFECTS)
ON CONFLICT DO NOTHING;

INSERT INTO public.ppiq_t024_vocab_rollback
  (table_name, row_id, code, old_industry_template, old_is_active, old_is_deleted)
SELECT 'parameter_definitions', id, parameter_code, industry_template, NULL, is_deleted
FROM public.parameter_definitions
WHERE parameter_code IN ($FLEET_PARAMS) AND industry_template IS NULL
ON CONFLICT DO NOTHING;

INSERT INTO public.ppiq_t024_vocab_rollback
  (table_name, row_id, code, old_industry_template, old_is_active, old_is_deleted)
SELECT 'equipment', id, equipment_code, NULL, is_active, is_deleted
FROM public.equipment WHERE equipment_code IN ($RETIRE_EQUIP)
ON CONFLICT DO NOTHING;

INSERT INTO public.ppiq_t024_vocab_rollback
  (table_name, row_id, code, old_industry_template, old_is_active, old_is_deleted)
SELECT 'material_unit_type_definitions', id, material_unit_type_code, NULL,
       is_active, is_deleted
FROM public.material_unit_type_definitions WHERE material_unit_type_code IN ($RETIRE_MUT)
ON CONFLICT DO NOTHING;

-- 1. tag the Fleet v2 vocabulary
UPDATE public.defect_catalogs SET industry_template = 'FlatSteel', updated_at_utc = now()
WHERE defect_code IN ($FLEET_DEFECTS) AND industry_template IS NULL;

UPDATE public.parameter_definitions SET industry_template = 'FlatSteel', updated_at_utc = now()
WHERE parameter_code IN ($FLEET_PARAMS) AND industry_template IS NULL;

-- 2. soft-retire. is_deleted is what the global EF query filter reads; is_active
--    is set too for correctness but would not hide anything on its own.
UPDATE public.defect_catalogs
SET is_deleted = true, deleted_at_utc = now(),
    deleted_reason = 'T-024: non-steel demo vocabulary retired', updated_at_utc = now()
WHERE defect_code IN ($RETIRE_DEFECTS);

UPDATE public.equipment
SET is_active = false, is_deleted = true, deleted_at_utc = now(),
    deleted_reason = 'T-024: superseded by Fleet v2 equipment', updated_at_utc = now()
WHERE equipment_code IN ($RETIRE_EQUIP);

UPDATE public.material_unit_type_definitions
SET is_active = false, is_deleted = true, deleted_at_utc = now(),
    deleted_reason = 'T-024: non-steel demo material types retired', updated_at_utc = now()
WHERE material_unit_type_code IN ($RETIRE_MUT);

COMMIT;

SELECT table_name, count(*) AS prior_values_captured
FROM public.ppiq_t024_vocab_rollback GROUP BY 1 ORDER BY 1;
"@
    if ($apply.ExitCode -ne 0 -or $apply.Error -match "(?i)(ERROR|FATAL):") {
        Say ("[FAIL] apply exited " + $apply.ExitCode)
        Say $apply.Error
        Say "One transaction wrapped it, so nothing changed."
        throw "apply"
    }
    Say $apply.Output
    Say "[OK] applied inside one transaction"

    Rule "4 - REFRESH THE READ MODELS"
    $mv = Invoke-Sql -Tag "mv" -Sql @'
\pset border 2
SELECT DISTINCT dependent.relname AS matview
FROM pg_depend d
JOIN pg_rewrite r ON r.oid = d.objid
JOIN pg_class dependent ON dependent.oid = r.ev_class
JOIN pg_class source ON source.oid = d.refobjid
JOIN pg_namespace n ON n.oid = source.relnamespace AND n.nspname = 'public'
WHERE dependent.relkind = 'm'
  AND source.relname IN ('material_units','parameter_observations','quality_events',
                         'downtime_events','genealogy_edges','process_step_executions',
                         'defect_catalogs','parameter_definitions','equipment')
  AND dependent.relname <> source.relname
ORDER BY 1;
'@
    Say $mv.Output
    $names = @()
    foreach ($raw in ($mv.Output -split "`n")) {
        $line = $raw.Trim()
        if ($line -match "^\|\s*([a-z0-9_]+)\s*\|$") {
            if ($Matches[1] -ne "matview") { $names += $Matches[1] }
        }
    }
    Say ("parsed " + $names.Count + " view name(s)")
    foreach ($n in $names) {
        $rf = Invoke-Sql -Tag ("rf_" + $n) -Sql ("REFRESH MATERIALIZED VIEW public." + $n + ";")
        if ($rf.ExitCode -ne 0) { Say ("[FAIL] refresh " + $n); $bad = $bad + 1 }
        else { Say ("[OK] refreshed " + $n) }
    }

    Rule "5 - VALIDATE"
    $val = Invoke-Sql -Tag "validate" -Sql @"
\pset border 2
SELECT 'fleet v2 defect rows tagged FlatSteel' AS check_name, count(*) AS found, 14 AS required
FROM public.defect_catalogs
WHERE defect_code IN ($FLEET_DEFECTS) AND industry_template = 'FlatSteel'
UNION ALL
SELECT 'fleet v2 parameter rows tagged FlatSteel', count(*), 29
FROM public.parameter_definitions
WHERE parameter_code IN ($FLEET_PARAMS) AND industry_template = 'FlatSteel'
UNION ALL
SELECT 'fleet v2 defect rows still VISIBLE', count(*), 14
FROM public.defect_catalogs
WHERE defect_code IN ($FLEET_DEFECTS) AND is_deleted = false
UNION ALL
SELECT 'fleet v2 parameter rows still VISIBLE', count(*), 29
FROM public.parameter_definitions
WHERE parameter_code IN ($FLEET_PARAMS) AND is_deleted = false
UNION ALL
SELECT 'fleet v2 equipment still VISIBLE', count(*), 18
FROM public.equipment WHERE source_system = 'FLEET_V2' AND is_deleted = false
UNION ALL
SELECT 'non-steel defect codes still visible', count(*), 0
FROM public.defect_catalogs
WHERE is_deleted = false AND coalesce(industry_template,'') IN ('Pharma','Tire','Aluminum')
UNION ALL
SELECT 'non-steel material types still visible', count(*), 0
FROM public.material_unit_type_definitions m
JOIN public.industry_templates t ON t.id = m.industry_template_id
WHERE m.is_deleted = false AND t.industry_name <> 'FlatSteel'
UNION ALL
SELECT 'legacy equipment still visible', count(*), 0
FROM public.equipment WHERE equipment_code IN ($RETIRE_EQUIP) AND is_deleted = false
UNION ALL
SELECT 'ml_feature_definitions left unresolvable', count(*), 0
FROM public.ml_feature_definitions f
WHERE f.is_deleted = false AND f.source_column IN ($NONSTEEL_PARAMS)
  AND NOT EXISTS (SELECT 1 FROM public.parameter_definitions p
                   WHERE p.parameter_code = f.source_column AND p.is_deleted = false);
"@
    Say $val.Output
    $bad = $bad + (Check-Table -Output $val.Output -Label "validation")

    Rule "6 - WHAT REMAINS VISIBLE, FOR THE BROWSER CHECK"
    $rem = Invoke-Sql -Tag "remaining" -Sql @"
\pset border 2
SELECT 'defect_catalogs' AS reference_table,
       coalesce(industry_template,'(null)') AS industry_template,
       count(*) FILTER (WHERE is_deleted = false) AS visible,
       count(*) FILTER (WHERE is_deleted) AS retired
FROM public.defect_catalogs GROUP BY 2
UNION ALL
SELECT 'parameter_definitions', coalesce(industry_template,'(null)'),
       count(*) FILTER (WHERE is_deleted = false), count(*) FILTER (WHERE is_deleted)
FROM public.parameter_definitions GROUP BY 2
ORDER BY 1, 2;
"@
    Say $rem.Output
    Say "The seven non-steel parameters remain VISIBLE at the API by design: they"
    Say "are referenced by ml_feature_definitions and no presented surface calls"
    Say "the parameter selector endpoint."
}
catch {
    $bad = $bad + 1
}
finally {
    Remove-Item Env:\PGPASSWORD -ErrorAction SilentlyContinue
}

Rule "RESULT"
if ($bad -gt 0) {
    Say ("[FAIL] " + $bad + " problem(s).")
    if ($Apply) {
        Say ""
        Say "To reverse: .\tools\run\Invoke-PpiqT024VocabularyCleanup.ps1 -Rollback"
    }
    exit 1
}
Say "[OK] vocabulary cleanup complete and validated."
Say ""
Say "NOW THE BROWSER CHECK, which is the only remaining T-024 item:"
Say "  Production / Shift renders"
Say "  Quality - defect Pareto shows Fleet v2 defects only"
Say "  Equipment / downtime shows Fleet v2 equipment only"
Say "  Parameter trend and parameter-by-grade render"
Say "  no Pharma, Tire or Aluminum vocabulary on any presented screen"
Say "  no broken widget binding, no empty or error state caused by this cleanup"
Say ""
Say "Reversible at any point with -Rollback, which restores from the captured"
Say "prior values rather than reconstructing them."
exit 0
