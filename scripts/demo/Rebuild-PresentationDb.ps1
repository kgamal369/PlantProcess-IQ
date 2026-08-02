# ============================================================================
# Rebuild-PresentationDb.ps1        Backlog v23 M2-18 (pulled forward to 17-Jul)
#
# ONE COMMAND. Rebuilds ppiq_presentation from the snapshot fixture, every
# time, in the only order that works. Senior rec 4: the demo database is a
# reproducible artifact, never truth.
#
# WHY THIS EXISTS: three hand-rebuilds, three different failures -
#   16-Jul  NOT NULL on dimension_code        (v2 widget insert)
#   16-Jul  jsonb vs text on the clone join   (v3 widget insert)
#   17-Jul  FK: widgets before dashboards     (restore rewound dashboards)
# ...plus a demo DB that vanished overnight. Hand-sequencing is the defect.
#
# THE ORDER (each step depends on the one above; that is the whole point):
#   0  stop the API process        (holds DB connections AND locks build DLLs)
#   1  pg_restore --clean          (rewinds EVERYTHING incl. dashboards)
#   2  Rule-1 fixes                (dump predates 15-Jul cleanup)
#   3  provenance neutralization   (phase3-dump:src_* -> system names)
#   4  engine-message scrub        (demo/golden -> neutral, incl. functions)
#   5  dashboards (7)              (parents FIRST - the 17-Jul lesson)
#   6  widgets (29 + heatmap/scatter clones)
#   7  verify + honest residue report
#
# HARD GUARD: target DB name must contain 'presentation'. ppiq_app cannot be
# touched by this script under any argument.
#
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Rebuild-PresentationDb.ps1
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Rebuild-PresentationDb.ps1 -Execute
# ============================================================================
[CmdletBinding()]
param(
    [switch]$Execute,
    [switch]$KeepApiRunning,
    [string]$TargetDb = 'ppiq_presentation',
    [string]$DumpPath = 'deploy\.ppiq-snapshots\ppiq_app_20260713_203359.dump'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'
if ($TargetDb -notmatch 'presentation') { Write-Host "[REFUSED] guard: target must contain 'presentation'." -ForegroundColor Red; exit 1 }

$RepoRoot = (Get-Location).Path
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$Report = Join-Path $RepoRoot ('RebuildPresentationDb_' + $Stamp + '.txt')
$sb = New-Object System.Text.StringBuilder
$Script:PpiqFailCount = 0   # every RunSql failure increments this; the tail refuses to say COMPLETE if it is not zero
function W([string]$s) { [void]$sb.AppendLine($s); Write-Host $s }
function Save { [System.IO.File]::WriteAllText($Report, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false))) }

# ---- tools -----------------------------------------------------------------
$PgBin = $null
$cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
if ($cmd) { $PgBin = Split-Path $cmd.Source -Parent } else {
    $c = @(Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending)
    if ($c.Count -gt 0) { $PgBin = Split-Path $c[0].FullName -Parent }
}
if (-not $PgBin) { Write-Host "[FAIL] psql not found." -ForegroundColor Red; exit 1 }
$Psql = Join-Path $PgBin 'psql.exe'
$PgRestore = Join-Path $PgBin 'pg_restore.exe'
$env:PGPASSWORD = 'ppiq_dev_local_only'

function Q1([string]$q) {
    $o = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -A -t -c $q 2>&1
    if ($LASTEXITCODE -ne 0) { return 'n/a' }
    $l = @($o | Where-Object { $_ -and ($_.ToString().Trim() -ne '') }) | Select-Object -First 1
    if ($null -eq $l) { return '0' }
    return $l.ToString().Trim()
}
function Rows([string]$q) {
    return @(& $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -A -t -F ' | ' -c $q 2>&1 | Where-Object { $_ -and ($_.ToString().Trim() -ne '') })
}
function RunSql([string]$label, [string]$sql) {
    $tmp = Join-Path $env:TEMP ("ppiq_rebuild_" + [guid]::NewGuid().ToString('N') + ".sql")
    [System.IO.File]::WriteAllText($tmp, $sql, (New-Object System.Text.UTF8Encoding($false)))
    $o = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -v ON_ERROR_STOP=1 -f $tmp 2>&1
    $code = $LASTEXITCODE
    Remove-Item $tmp -ErrorAction SilentlyContinue
    if ($code -eq 0) { W ("      OK   " + $label) } else {
        $Script:PpiqFailCount = $Script:PpiqFailCount + 1
        W ("      FAIL " + $label)
        @($o | Select-Object -First 4) | ForEach-Object { W ("           " + $_) }
    }
    return ($code -eq 0)
}

$dump = Join-Path $RepoRoot $DumpPath
if (-not (Test-Path $dump)) { $dump = $DumpPath }

W ("REBUILD PRESENTATION DB - " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ("Fixture: " + $dump)
W ("Target : " + $TargetDb + "   (ppiq_app guarded)")
W ("=" * 78)
if (-not (Test-Path $dump)) { W "[ABORT] snapshot fixture not found."; Save; exit 1 }
W ("Fixture date: " + (Get-Item $dump).LastWriteTime + "   " + [Math]::Round((Get-Item $dump).Length / 1MB, 1) + " MB")
W ""

if (-not $Execute) {
    W "DRY-RUN. This would run steps 0-7 (see header). Current state:"
    foreach ($t in @('material_units', 'quality_events', 'genealogy_edges', 'dashboard_definitions', 'dashboard_widget_definitions')) {
        W ("    " + $t.PadRight(30) + " " + (Q1 ("SELECT COUNT(*) FROM " + $t + ";")))
    }
    W ""
    W "Re-run with -Execute. Takes ~2 minutes and stops the API."
    Save; exit 0
}

# ---- 0. stop the API -------------------------------------------------------
W "[0/7] stopping the API (it holds DB connections and locks build DLLs)"
if ($KeepApiRunning) {
    W "      -KeepApiRunning: skipped (pg_restore will likely fail on active connections)"
} else {
    $procs = @(Get-Process -Name 'PlantProcess.Api' -ErrorAction SilentlyContinue)
    if ($procs.Count -eq 0) { W "      no PlantProcess.Api process found." }
    foreach ($p in $procs) {
        W ("      killing PID " + $p.Id)
        Stop-Process -Id $p.Id -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 2
    $active = Q1 "SELECT 1;"
    $conns = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d postgres -w -X -A -t -c ("SELECT COUNT(*) FROM pg_stat_activity WHERE datname='" + $TargetDb + "' AND pid <> pg_backend_pid();") 2>&1
    $n = [int](@($conns | Where-Object { $_ -match '^\d+$' }) | Select-Object -First 1)
    if ($n -gt 0) {
        W ("      " + $n + " connection(s) remain - terminating them")
        & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d postgres -w -X -c ("SELECT pg_terminate_backend(pid) FROM pg_stat_activity WHERE datname='" + $TargetDb + "' AND pid <> pg_backend_pid();") 2>&1 | Out-Null
    }
    W "      clear."
}
W ""

# ---- 1. restore ------------------------------------------------------------
W "[1/7] pg_restore --clean --if-exists (rewinds EVERYTHING, dashboards included)"
& $PgRestore -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w --clean --if-exists --no-owner --no-privileges $dump 2>&1 |
    Select-Object -First 4 | ForEach-Object { W ("      " + $_) }
W "      (missing-object warnings on --clean are normal)"
foreach ($t in @('material_units', 'parameter_observations', 'quality_events', 'genealogy_edges', 'ml_correlation_results_v2')) {
    W ("      " + $t.PadRight(30) + " " + (Q1 ("SELECT COUNT(*) FROM " + $t + ";")))
}
W ""

# ---- 1b. engine migrations (heat lineage + coil projection + defect outcomes)
W "[1b/7] engine migrations 741+742 (or the engine re-blinds on every rebuild)"
foreach ($mig in @('741_feature_store_coil_grain_projection.sql','742_feature_regrain_generic.sql')) {
    $migPath = Join-Path $PSScriptRoot ('..\..\Backend\database\scripts\' + $mig)
    if (Test-Path -LiteralPath $migPath) {
        $mo = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -v ON_ERROR_STOP=1 -X -q -1 -f $migPath 2>&1
        if ($LASTEXITCODE -eq 0) { W ("      applied " + $mig) } else { W ("      FAILED " + $mig + ": " + ($mo -join ' ')) }
    } else { W ("      MISSING " + $migPath) }
}
W ("      lineage view rows: " + (Q1 "SELECT COUNT(*) FROM ppiq_ml_unit_heat_lineage;"))
W ""
# ---- 2. Rule-1 fixes -------------------------------------------------------
W "[2/7] Rule-1 fixes (the fixture predates the 15-Jul cleanup)"
[void](RunSql 'sites -> PLANT-NN' @"
WITH ranked AS (
  SELECT id, ROW_NUMBER() OVER (ORDER BY site_code) AS rn
  FROM sites WHERE site_code ~* '(demo|adv|_p3_|p3_site|test|sample)'
)
UPDATE sites s SET site_code='PLANT-'||lpad(r.rn::text,2,'0'),
       site_name='Standard Manufacturing Plant', company_name='Standard Manufacturing'
FROM ranked r WHERE s.id=r.id;
"@)
[void](RunSql 'connection profile codes -> CP-NN' @"
UPDATE connection_profiles
SET connection_profile_code = regexp_replace(connection_profile_code,'^(DEMO-READY-|DEMO-|ADV-)','','i')
WHERE connection_profile_code ~* '^(DEMO-READY-|DEMO-|ADV-)';
"@)
[void](RunSql 'canonical_schema_views: drop demo-era rows' "DELETE FROM canonical_schema_views WHERE physical_view_name ~ '^v_phase';")
[void](RunSql 'job_log: truncate internal dumps' "UPDATE job_log SET message = left(message,180)||' ... [truncated: internal diagnostics removed]' WHERE length(message) > 600;")
W ""

# ---- 3. provenance ---------------------------------------------------------
W "[3/7] provenance neutralization (Material Investigation shows this column)"
# PROVENANCE PERFORMANCE, 02-Aug: the genealogy UPDATE ran past ten minutes,
# active and unblocked. Cause: ppiq_genealogy_edge_weight_guard_after_change is
# a ROW-LEVEL AFTER trigger (tgtype 29) firing 35,906 times, on a table with ten
# indexes. It is suspended for these four statements ONLY, inside one
# transaction so a failure rolls the suspension back too, and the invariant it
# guards is verified by query immediately afterwards.
# source_system is provenance metadata and cannot affect attribution weights.
[void](RunSql 'phase3-dump:src_* -> system names' @"
BEGIN;
ALTER TABLE genealogy_edges DISABLE TRIGGER ppiq_genealogy_edge_weight_guard_after_change;
UPDATE material_units SET source_system='MELTSHOP_L2' WHERE source_system LIKE 'phase3-dump:src_meltshop%';
UPDATE material_units SET source_system='CASTER_L2'   WHERE source_system LIKE 'phase3-dump:src_caster%';
UPDATE material_units SET source_system='HSM_L2'      WHERE source_system LIKE 'phase3-dump:src_hsm%';
UPDATE material_units SET source_system='REF_BASELINE' WHERE source_system IN ('ADVANCED_DEMO_SEED','PPIQ_P3_SEED','PHASE_F_SEED','sql-seed');
UPDATE quality_events SET source_system='INSPECTION_L2' WHERE source_system LIKE 'phase3-dump%';
UPDATE genealogy_edges SET source_system='GENEALOGY_L2' WHERE source_system LIKE 'phase3-dump%';
UPDATE parameter_observations SET source_system='PROCESS_L2' WHERE source_system LIKE 'phase3-dump%';
ALTER TABLE genealogy_edges ENABLE TRIGGER ppiq_genealogy_edge_weight_guard_after_change;
COMMIT;
"@)
[void](RunSql 'genealogy weight invariant still holds after the suspended update' @"
DO `$`$
DECLARE bad INT;
BEGIN
  SELECT COUNT(*) INTO bad FROM (
    SELECT child_material_unit_id
    FROM genealogy_edges
    WHERE COALESCE(is_deleted,false) = false
    GROUP BY child_material_unit_id
    HAVING ABS(SUM(contribution_weight) - 1.0) > 0.0001
  ) q;
  IF bad > 0 THEN
    RAISE EXCEPTION 'genealogy weight invariant broken for % children', bad;
  END IF;
END
`$`$;
"@)
[void](RunSql 'genealogy weight guard is re-enabled' @"
DO `$`$
DECLARE st CHAR;
BEGIN
  SELECT tgenabled INTO st FROM pg_trigger
   WHERE tgrelid='genealogy_edges'::regclass
     AND tgname='ppiq_genealogy_edge_weight_guard_after_change';
  IF st IS DISTINCT FROM 'O' THEN
    RAISE EXCEPTION 'weight guard left in state %, expected O', st;
  END IF;
END
`$`$;
"@)
W ""

# ---- 4. engine messages + live functions -----------------------------------
W "[4/7] engine-message scrub (history rows AND the functions that regenerate them)"
[void](RunSql 'ml messages: demo/golden -> neutral' @"
UPDATE ml_learning_runs_v1 SET
  error_message = regexp_replace(COALESCE(error_message,''),'demo learning','learning','gi'),
  readiness_message = regexp_replace(COALESCE(readiness_message,''),'golden dataset','dataset','gi')
WHERE COALESCE(error_message,'')||COALESCE(readiness_message,'') ~* '(demo|golden)';
UPDATE ml_learning_runs_v1 SET
  error_message = NULLIF(regexp_replace(error_message,'\mdemo\M ?','','gi'),''),
  readiness_message = NULLIF(regexp_replace(readiness_message,'\m(golden|demo)\M ?','','gi'),'')
WHERE COALESCE(error_message,'')||COALESCE(readiness_message,'') ~* '(demo|golden)';
UPDATE ml_correlation_compute_runs SET message = replace(message,' (golden dataset)','') WHERE message LIKE '%(golden dataset)%';
"@)
foreach ($p in @(
        @{ F = 'for demo learning.'; R = 'for learning.' },
        @{ F = 'Golden dataset contains sufficient'; R = 'Dataset contains sufficient' },
        @{ F = 'deterministic-core (golden dataset): '; R = 'deterministic-core: ' })) {
    $oids = Rows ("SELECT p.oid FROM pg_proc p JOIN pg_namespace n ON n.oid=p.pronamespace WHERE n.nspname='public' AND p.prosrc LIKE '%" + $p.F.Replace("'", "''") + "%';")
    foreach ($oid in $oids) {
        $o = $oid.ToString().Trim()
        if ($o -notmatch '^\d+$') { continue }
        $defLines = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -A -t -c ("SELECT pg_get_functiondef(" + $o + ");") 2>&1
        if ($LASTEXITCODE -ne 0) { continue }
        $newDef = (($defLines -join "`n")).Replace($p.F, $p.R)
        $tmp = Join-Path $env:TEMP ("ppiq_fn_" + $o + ".sql")
        [System.IO.File]::WriteAllText($tmp, $newDef, (New-Object System.Text.UTF8Encoding($false)))
        & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -v ON_ERROR_STOP=1 -f $tmp 2>&1 | Out-Null
        if ($LASTEXITCODE -eq 0) { W ("      REDEFINED function oid " + $o) }
        Remove-Item $tmp -ErrorAction SilentlyContinue
    }
}
W ("      residual demo/golden in ml_learning_runs_v1: " + (Q1 "SELECT COUNT(*) FROM ml_learning_runs_v1 WHERE COALESCE(error_message,'')||COALESCE(readiness_message,'') ~* '(demo|golden)';"))
W ("      residual in ml_correlation_compute_runs:     " + (Q1 "SELECT COUNT(*) FROM ml_correlation_compute_runs WHERE message ~* '(demo|golden)';"))
W ""

# ---- 5 + 6. dashboards THEN widgets (the 17-Jul lesson) --------------------
W "[5/7] dashboards (7) - parents FIRST, or the widget FK fails"
$TopParam = Q1 "SELECT pd.parameter_code FROM parameter_definitions pd JOIN parameter_observations po ON po.parameter_definition_id=pd.id GROUP BY pd.parameter_code ORDER BY COUNT(*) DESC LIMIT 1;"
if (-not $TopParam -or $TopParam -eq 'n/a' -or $TopParam -eq '0') { $TopParam = 'rolling.cooling_rate' }
W ("      analysis widgets bind to: " + $TopParam)
$ParamSql = "'" + $TopParam.Replace("'", "''") + "'"

$D = @('20000000-0000-0000-0000-000000000001', '20000000-0000-0000-0000-000000000002', '20000000-0000-0000-0000-000000000003',
    '20000000-0000-0000-0000-000000000004', '20000000-0000-0000-0000-000000000005', '20000000-0000-0000-0000-000000000006',
    '20000000-0000-0000-0000-000000000007')
$meta = @(
    @('PRODUCTION_OVERVIEW', 'Production Overview', 'Plant production volume, throughput trend and material mix.'),
    @('QUALITY_MONITORING', 'Quality Monitoring', 'Defect rate trend, defect breakdown and severity distribution.'),
    @('EQUIPMENT_OPERATIONS', 'Equipment and Operations', 'Equipment-level quality and observation throughput.'),
    @('CORRELATION_FINDINGS_BOARD', 'Correlation Findings Board', 'Parameter-defect correlation landscape from the analysis engine.'),
    @('PARAMETER_DEEP_ANALYSIS', 'Parameter Deep Analysis', 'Focused analysis of the highest-signal process parameter.'),
    @('RISK_INTELLIGENCE', 'Risk Intelligence', 'Model-scored material risk across equipment and time.'),
    @('MODEL_INSIGHTS', 'Model Insights', 'Engine-derived quality drivers and correlation surfaces.')
)
$dashRows = @()
for ($i = 0; $i -lt 7; $i++) {
    $dashRows += "('" + $D[$i] + "',NULL,'" + $meta[$i][0] + "','" + $meta[$i][1] + "','" + $meta[$i][2] + "','{}',FALSE,FALSE,TRUE,NOW() AT TIME ZONE 'UTC',NULL,FALSE,'PPIQ_UI','" + $meta[$i][0] + "',FALSE,NULL,NULL)"
}
$dashOk = RunSql 'dashboard_definitions upsert (7)' (@"
INSERT INTO dashboard_definitions
(id,user_id,dashboard_code,name,description,layout_json,is_default,is_system_template,is_active,created_at_utc,updated_at_utc,is_synthetic,source_system,source_record_id,is_deleted,deleted_at_utc,deleted_reason)
VALUES
"@ + ($dashRows -join ",`n") + @"

ON CONFLICT (dashboard_code) WHERE is_deleted = FALSE
DO UPDATE SET name=EXCLUDED.name, description=EXCLUDED.description, is_active=TRUE, updated_at_utc=NOW() AT TIME ZONE 'UTC';
"@)
W ("      dashboards now: " + (Q1 "SELECT COUNT(*) FROM dashboard_definitions WHERE is_deleted=FALSE;"))
W ""

W "[6/7] widgets (29 + heatmap/scatter clones)"
if (-not $dashOk) {
    W "      SKIPPED - dashboards failed, widgets would FK-fail (this is the 17-Jul defect)"
} else {
    function WRow([string]$id, [string]$dash, [string]$code, [string]$title, [string]$chart, [string]$dim, [string]$measure, [string]$param, [string]$layout, [int]$sort) {
        if (-not $dim) { $dim = 'day' }
        return "('$id','$dash','$code','$title','chart','$chart','$dim','$measure',$param,'{}','$layout','{""maxRows"":50,""rawRowLimit"":1000}',$sort,TRUE,NOW() AT TIME ZONE 'UTC',NULL,FALSE,'PPIQ_UI','$code',FALSE,NULL,NULL)".Replace('""', '"')
    }
    $L = @{ K1 = '{"lg":{"x":0,"y":0,"w":3,"h":4}}'; K2 = '{"lg":{"x":3,"y":0,"w":3,"h":4}}'; K3 = '{"lg":{"x":6,"y":0,"w":3,"h":4}}'; K4 = '{"lg":{"x":9,"y":0,"w":3,"h":4}}'
        MAIN = '{"lg":{"x":0,"y":4,"w":8,"h":8}}'; SIDE = '{"lg":{"x":8,"y":4,"w":4,"h":8}}'; BL = '{"lg":{"x":0,"y":12,"w":6,"h":8}}'; BR = '{"lg":{"x":6,"y":12,"w":6,"h":8}}' }
    $rows = @(
        (WRow '21000000-0000-0000-0000-000000000101' $D[0] 'PO_KPI_MAT' 'Material Units' 'kpi' '' 'materialCount' 'NULL' $L.K1 1),
        (WRow '21000000-0000-0000-0000-000000000102' $D[0] 'PO_KPI_OBS' 'Process Observations' 'kpi' '' 'observationCount' 'NULL' $L.K2 2),
        (WRow '21000000-0000-0000-0000-000000000103' $D[0] 'PO_KPI_DEF' 'Quality Events' 'kpi' '' 'defectCount' 'NULL' $L.K3 3),
        (WRow '21000000-0000-0000-0000-000000000104' $D[0] 'PO_KPI_RATE' 'Defect Rate' 'kpi' '' 'defectRate' 'NULL' $L.K4 4),
        (WRow '21000000-0000-0000-0000-000000000105' $D[0] 'PO_TREND' 'Production Volume Trend' 'line' 'day' 'materialCount' 'NULL' $L.MAIN 5),
        (WRow '21000000-0000-0000-0000-000000000106' $D[0] 'PO_MIX' 'Material Mix' 'donut' 'materialUnitType' 'materialCount' 'NULL' $L.SIDE 6),
        (WRow '21000000-0000-0000-0000-000000000107' $D[0] 'PO_WEEK' 'Weekly Throughput' 'area' 'week' 'materialCount' 'NULL' $L.BL 7),
        (WRow '21000000-0000-0000-0000-000000000108' $D[0] 'PO_TABLE' 'Volume by Type' 'table' 'materialUnitType' 'materialCount' 'NULL' $L.BR 8),
        (WRow '21000000-0000-0000-0000-000000000201' $D[1] 'QM_TREND' 'Defect Rate Trend' 'line' 'day' 'defectRate' 'NULL' $L.MAIN 1),
        (WRow '21000000-0000-0000-0000-000000000202' $D[1] 'QM_BREAK' 'Defect Breakdown' 'bar' 'defectType' 'defectCount' 'NULL' $L.SIDE 2),
        (WRow '21000000-0000-0000-0000-000000000203' $D[1] 'QM_SEV' 'Severity Distribution' 'donut' 'severity' 'defectCount' 'NULL' $L.BL 3),
        (WRow '21000000-0000-0000-0000-000000000204' $D[1] 'QM_TABLE' 'Defects by Type' 'table' 'defectType' 'defectCount' 'NULL' $L.BR 4),
        (WRow '21000000-0000-0000-0000-000000000301' $D[2] 'EO_EQDEF' 'Quality Events by Equipment' 'bar' 'equipment' 'defectCount' 'NULL' $L.MAIN 1),
        (WRow '21000000-0000-0000-0000-000000000302' $D[2] 'EO_OBS' 'Observation Throughput' 'line' 'week' 'observationCount' 'NULL' $L.SIDE 2),
        (WRow '21000000-0000-0000-0000-000000000303' $D[2] 'EO_TABLE' 'Materials by Equipment' 'table' 'equipment' 'materialCount' 'NULL' $L.BL 3),
        (WRow '21000000-0000-0000-0000-000000000304' $D[2] 'EO_MONTH' 'Monthly Volume' 'bar' 'month' 'materialCount' 'NULL' $L.BR 4),
        (WRow '21000000-0000-0000-0000-000000000401' $D[3] 'CF_RATE' 'Defect Rate Trend' 'line' 'day' 'defectRate' 'NULL' $L.BL 3),
        (WRow '21000000-0000-0000-0000-000000000402' $D[3] 'CF_TOP' 'Defect Landscape' 'bar' 'defectType' 'defectCount' 'NULL' $L.BR 4),
        (WRow '21000000-0000-0000-0000-000000000501' $D[4] 'PA_KAVG' 'Average Value' 'kpi' '' 'avgParameterValue' $ParamSql $L.K1 1),
        (WRow '21000000-0000-0000-0000-000000000502' $D[4] 'PA_KOBS' 'Observations' 'kpi' '' 'observationCount' $ParamSql $L.K2 2),
        (WRow '21000000-0000-0000-0000-000000000503' $D[4] 'PA_TREND' 'Parameter Trend' 'line' 'day' 'avgParameterValue' $ParamSql $L.MAIN 3),
        (WRow '21000000-0000-0000-0000-000000000504' $D[4] 'PA_BYP' 'Observation Volume by Parameter' 'bar' 'parameterCode' 'observationCount' 'NULL' $L.SIDE 4),
        (WRow '21000000-0000-0000-0000-000000000505' $D[4] 'PA_TABLE' 'Parameters Overview' 'table' 'parameterCode' 'avgParameterValue' 'NULL' $L.BL 5),
        (WRow '21000000-0000-0000-0000-000000000601' $D[5] 'RI_KPI' 'Average Risk Score' 'kpi' '' 'riskScore' 'NULL' $L.K1 1),
        (WRow '21000000-0000-0000-0000-000000000602' $D[5] 'RI_TREND' 'Risk Score Trend' 'line' 'day' 'riskScore' 'NULL' $L.MAIN 2),
        (WRow '21000000-0000-0000-0000-000000000603' $D[5] 'RI_EQUIP' 'Risk by Equipment' 'bar' 'equipment' 'riskScore' 'NULL' $L.SIDE 3),
        (WRow '21000000-0000-0000-0000-000000000604' $D[5] 'RI_TABLE' 'Risk by Material Type' 'table' 'materialUnitType' 'riskScore' 'NULL' $L.BL 4),
        (WRow '21000000-0000-0000-0000-000000000701' $D[6] 'MI_RATE' 'Model-Tracked Defect Rate' 'line' 'day' 'defectRate' 'NULL' $L.BL 3),
        (WRow '21000000-0000-0000-0000-000000000702' $D[6] 'MI_SEV' 'Predicted Severity Mix' 'donut' 'severity' 'defectCount' 'NULL' $L.BR 4)
    )
    $wsql = "DELETE FROM dashboard_widget_definitions WHERE dashboard_definition_id IN ('" + ($D -join "','") + "');`n"
    $wsql += "INSERT INTO dashboard_widget_definitions`n(id,dashboard_definition_id,widget_code,widget_title,widget_type,chart_type,dimension_code,measure_code,parameter_code,filter_json,layout_json,display_options_json,sort_order,is_active,created_at_utc,updated_at_utc,is_synthetic,source_system,source_record_id,is_deleted,deleted_at_utc,deleted_reason)`nVALUES`n" + ($rows -join ",`n") + ";`n"
    $wsql += @"
INSERT INTO dashboard_widget_definitions
(id,dashboard_definition_id,widget_code,widget_title,widget_type,chart_type,dimension_code,measure_code,parameter_code,filter_json,layout_json,display_options_json,sort_order,is_active,created_at_utc,updated_at_utc,is_synthetic,source_system,source_record_id,is_deleted,deleted_at_utc,deleted_reason)
SELECT gen_random_uuid(), t.new_dash::uuid, 'CLONE_'||t.tag||'_'||w.widget_code,
       w.widget_title, w.widget_type, w.chart_type, w.dimension_code, w.measure_code, w.parameter_code,
       w.filter_json, t.layout::jsonb, w.display_options_json, t.sort, TRUE, NOW() AT TIME ZONE 'UTC', NULL,
       FALSE, 'PPIQ_UI', w.widget_code, FALSE, NULL, NULL
FROM dashboard_widget_definitions w
JOIN dashboard_definitions d ON d.id=w.dashboard_definition_id AND d.dashboard_code='CORRELATION_EXPLORER'
JOIN (VALUES
    ('$($D[3])','CFB','{"lg":{"x":0,"y":0,"w":8,"h":10}}',1),
    ('$($D[3])','CFB','{"lg":{"x":8,"y":0,"w":4,"h":10}}',2),
    ('$($D[6])','MI','{"lg":{"x":0,"y":0,"w":8,"h":10}}',1),
    ('$($D[6])','MI','{"lg":{"x":8,"y":0,"w":4,"h":10}}',2)
) AS t(new_dash,tag,layout,sort) ON TRUE
WHERE w.is_deleted=FALSE
  AND ((t.sort=1 AND lower(w.chart_type)='heatmap') OR (t.sort=2 AND lower(w.chart_type) IN ('scatter','matrix')));
"@
    [void](RunSql 'widgets + clones' ("BEGIN;`n" + $wsql + "`nCOMMIT;"))
}
W ""

# ---- 7. verify -------------------------------------------------------------
W "[7/7] VERIFY"
W "      data:"
foreach ($t in @('material_units', 'parameter_observations', 'quality_events', 'genealogy_edges', 'ml_correlation_results_v2')) {
    W ("        " + $t.PadRight(30) + " " + (Q1 ("SELECT COUNT(*) FROM " + $t + ";")))
}
W "      dashboards (code | widgets):"
Rows "SELECT d.dashboard_code, COUNT(w.id) FROM dashboard_definitions d LEFT JOIN dashboard_widget_definitions w ON w.dashboard_definition_id=d.id AND w.is_deleted=FALSE WHERE d.is_deleted=FALSE GROUP BY 1 ORDER BY 1;" | ForEach-Object { W ("        " + $_) }
W "      provenance on screen:"
Rows "SELECT source_system, COUNT(*) FROM material_units GROUP BY 1 ORDER BY 2 DESC;" | ForEach-Object { W ("        " + $_) }
W ""
W "=" * 78
if ($Script:PpiqFailCount -gt 0) {
    W ("REBUILD FAILED - " + $Script:PpiqFailCount + " step(s) reported FAIL above.")
    W ""
    W "The database is in an unknown state and must NOT be demonstrated."
    W "Read the FAIL lines, fix the cause, and run this script again from the start."
    W ""
    W "This exit code is deliberate. Before 02-Aug this script printed COMPLETE and"
    W "returned 0 even when every step had failed, which is worse than failing,"
    W "because it teaches everyone to trust the wrong signal."
    Save
    Write-Host ""
    Write-Host ("[FAILED] Report -> " + $Report) -ForegroundColor Red
    exit 1
}
W "REBUILD COMPLETE. Start the API:"
W "    .\scripts\run\start-api.ps1 -Profile presentation"
W ""
W "This command is now the ONLY supported way to rebuild the demo database."
W "Commit it to scripts/demo/ (v23 M2-18) and never hand-sequence again."
Save
Write-Host ""
Write-Host ("[DONE] Report -> " + $Report) -ForegroundColor Green
exit 0
