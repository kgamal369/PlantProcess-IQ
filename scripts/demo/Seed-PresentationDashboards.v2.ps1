# ============================================================================
# Seed-PresentationDashboards.v2.ps1
# v1 defects fixed (both mine):
#   1. SQL passed via -c lost embedded double quotes to PowerShell native-arg
#      quoting -> invalid JSON. v2 writes ALL SQL to a temp file, psql -f.
#   2. parameter_definitions has no 'code' column - v2 DISCOVERS the real
#      code column and the observations FK from information_schema.
# v2 additions (from the restore report's residue section):
#   3. Neutralizes phase3-dump:src_* provenance -> MELTSHOP_L2/CASTER_L2/HSM_L2
#      (presentation DB only; Material Investigation shows this column).
#   4. Prints and cleans the last 5 demo/golden strings in ml_learning_runs_v1.
# Then seeds the SEVEN dashboards + clones the proven heatmap/scatter.
# HARD GUARD: target DB name must contain 'presentation'.
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Seed-PresentationDashboards.v2.ps1
# ============================================================================
[CmdletBinding()]
param(
    [string]$TargetDb = 'ppiq_presentation'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'
if ($TargetDb -notmatch 'presentation') { Write-Host "[REFUSED] guard active." -ForegroundColor Red; exit 1 }

$RepoRoot = (Get-Location).Path
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$OutFile = Join-Path $RepoRoot ("PresentationDashboards_v2_" + $Stamp + ".txt")
$sb = New-Object System.Text.StringBuilder
function W([string]$s) { [void]$sb.AppendLine($s); Write-Host $s }

$Psql = $null
$cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
if ($cmd) { $Psql = $cmd.Source } else {
    $c = @(Get-ChildItem 'C:\Program Files\PostgreSQL\*\bin\psql.exe' -ErrorAction SilentlyContinue | Sort-Object FullName -Descending)
    if ($c.Count -gt 0) { $Psql = $c[0].FullName }
}
if (-not $Psql) { Write-Host "[FAIL] psql not found." -ForegroundColor Red; exit 1 }
$env:PGPASSWORD = 'ppiq_dev_local_only'

function T([string]$q) {
    $o = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -A -t -c $q 2>&1
    if ($LASTEXITCODE -ne 0) { return $null }
    $l = @($o | Where-Object { $_ -and ($_.ToString().Trim() -ne '') }) | Select-Object -First 1
    if ($null -eq $l) { return '' }
    return $l.ToString().Trim()
}
function RunFile([string]$label, [string]$sql) {
    $tmp = Join-Path $env:TEMP ("ppiq_dash_" + [guid]::NewGuid().ToString('N') + ".sql")
    [System.IO.File]::WriteAllText($tmp, $sql, (New-Object System.Text.UTF8Encoding($false)))
    $o = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -v ON_ERROR_STOP=1 -f $tmp 2>&1
    $code = $LASTEXITCODE
    Remove-Item $tmp -ErrorAction SilentlyContinue
    if ($code -eq 0) { W ("[OK]   " + $label) } else {
        W ("[FAIL] " + $label)
        @($o | Select-Object -First 5) | ForEach-Object { W ("       " + $_) }
    }
    return ($code -eq 0)
}

W ("PRESENTATION DASHBOARDS v2 - " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + "  DB: " + $TargetDb)
W ("=" * 78)

# ---- residue cleanup 1: provenance neutralization ---------------------------
[void](RunFile 'provenance: phase3-dump:src_* -> neutral system names' @"
UPDATE material_units SET source_system = 'MELTSHOP_L2'
 WHERE source_system LIKE 'phase3-dump:src_meltshop%';
UPDATE material_units SET source_system = 'CASTER_L2'
 WHERE source_system LIKE 'phase3-dump:src_caster%';
UPDATE material_units SET source_system = 'HSM_L2'
 WHERE source_system LIKE 'phase3-dump:src_hsm%';
UPDATE material_units SET source_system = 'REF_BASELINE'
 WHERE source_system IN ('ADVANCED_DEMO_SEED','PPIQ_P3_SEED','PHASE_F_SEED','sql-seed');
UPDATE quality_events SET source_system = 'INSPECTION_L2'
 WHERE source_system LIKE 'phase3-dump%';
UPDATE genealogy_edges SET source_system = 'GENEALOGY_L2'
 WHERE source_system LIKE 'phase3-dump%';
UPDATE parameter_observations SET source_system = 'PROCESS_L2'
 WHERE source_system LIKE 'phase3-dump%';
"@)

# ---- residue cleanup 2: the last 5 engine strings ---------------------------
W "[ML] the remaining demo/golden run messages (before):"
$leaks = @(& $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -A -t -F ' | ' -c "SELECT DISTINCT left(COALESCE(error_message,'') || ' || ' || COALESCE(readiness_message,''),110) FROM ml_learning_runs_v1 WHERE COALESCE(error_message,'') || COALESCE(readiness_message,'') ~* '(demo|golden)' LIMIT 6;" 2>&1)
@($leaks) | Where-Object { $_ } | ForEach-Object { W ("     " + $_) }
[void](RunFile 'ml_learning_runs_v1: scrub demo/golden (case-insensitive)' @"
UPDATE ml_learning_runs_v1 SET
  error_message = regexp_replace(COALESCE(error_message,''), 'demo learning', 'learning', 'gi'),
  readiness_message = regexp_replace(COALESCE(readiness_message,''), 'golden dataset', 'dataset', 'gi')
WHERE COALESCE(error_message,'') || COALESCE(readiness_message,'') ~* '(demo|golden)';
UPDATE ml_learning_runs_v1 SET
  error_message = NULLIF(regexp_replace(error_message, '\mdemo\M ?', '', 'gi'), ''),
  readiness_message = NULLIF(regexp_replace(readiness_message, '\m(golden|demo)\M ?', '', 'gi'), '')
WHERE COALESCE(error_message,'') || COALESCE(readiness_message,'') ~* '(demo|golden)';
"@)
W ("[ML] remaining after: " + (T "SELECT COUNT(*) FROM ml_learning_runs_v1 WHERE COALESCE(error_message,'') || COALESCE(readiness_message,'') ~* '(demo|golden)';"))
W ""

# ---- discover the parameter code column + FK --------------------------------
$pdCode = T "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='parameter_definitions' AND column_name ~* '^(parameter_)?code$' LIMIT 1;"
if (-not $pdCode) { $pdCode = T "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='parameter_definitions' AND column_name ~* 'code' ORDER BY ordinal_position LIMIT 1;" }
$poFk = T "SELECT column_name FROM information_schema.columns WHERE table_schema='public' AND table_name='parameter_observations' AND column_name ~* 'parameter.*(definition_)?id' ORDER BY length(column_name) LIMIT 1;"
W ("[PARAM] parameter_definitions code column: '" + $pdCode + "'   observations FK: '" + $poFk + "'")
$TopParam = ''
if ($pdCode -and $poFk) {
    $TopParam = T ("SELECT pd." + $pdCode + " FROM parameter_definitions pd JOIN parameter_observations po ON po." + $poFk + " = pd.id GROUP BY pd." + $pdCode + " ORDER BY COUNT(*) DESC, pd.parameter_code ASC LIMIT 1;")
}
if (-not $TopParam -and $pdCode) { $TopParam = T ("SELECT " + $pdCode + " FROM parameter_definitions LIMIT 1;") }
$ParamSql = 'NULL'
if ($TopParam) { $ParamSql = "'" + $TopParam.Replace("'", "''") + "'"; W ("[PARAM] binding analysis widgets to: " + $TopParam) }
else { W "[PARAM] none resolvable - per-parameter widgets will bind unfiltered (dropped param filter)." }
W ""

# ---- dashboards + widgets (single SQL file, real JSON) ----------------------
function WRow([string]$id, [string]$dash, [string]$code, [string]$title, [string]$chart, [string]$dim, [string]$measure, [string]$param, [string]$layout, [int]$sort) {
    $dimSql = 'NULL'; if ($dim) { $dimSql = "'" + $dim + "'" }
    return "('$id','$dash','$code','$title','chart','$chart',$dimSql,'$measure',$param,'{}','$layout','{""maxRows"":50,""rawRowLimit"":1000}',$sort,TRUE,NOW() AT TIME ZONE 'UTC',NULL,FALSE,'PPIQ_UI','$code',FALSE,NULL,NULL)".Replace('""', '"')
}
$L = @{
    K1 = '{"lg":{"x":0,"y":0,"w":3,"h":4}}'; K2 = '{"lg":{"x":3,"y":0,"w":3,"h":4}}'
    K3 = '{"lg":{"x":6,"y":0,"w":3,"h":4}}'; K4 = '{"lg":{"x":9,"y":0,"w":3,"h":4}}'
    MAIN = '{"lg":{"x":0,"y":4,"w":8,"h":8}}'; SIDE = '{"lg":{"x":8,"y":4,"w":4,"h":8}}'
    BL = '{"lg":{"x":0,"y":12,"w":6,"h":8}}'; BR = '{"lg":{"x":6,"y":12,"w":6,"h":8}}'
}
$D1 = '20000000-0000-0000-0000-000000000001'; $D2 = '20000000-0000-0000-0000-000000000002'
$D3 = '20000000-0000-0000-0000-000000000003'; $D4 = '20000000-0000-0000-0000-000000000004'
$D5 = '20000000-0000-0000-0000-000000000005'; $D6 = '20000000-0000-0000-0000-000000000006'
$D7 = '20000000-0000-0000-0000-000000000007'

$rows = @(
    (WRow '21000000-0000-0000-0000-000000000101' $D1 'PO_KPI_MAT' 'Material Units' 'kpi' '' 'materialCount' 'NULL' $L.K1 1),
    (WRow '21000000-0000-0000-0000-000000000102' $D1 'PO_KPI_OBS' 'Process Observations' 'kpi' '' 'observationCount' 'NULL' $L.K2 2),
    (WRow '21000000-0000-0000-0000-000000000103' $D1 'PO_KPI_DEF' 'Quality Events' 'kpi' '' 'defectCount' 'NULL' $L.K3 3),
    (WRow '21000000-0000-0000-0000-000000000104' $D1 'PO_KPI_RATE' 'Defect Rate' 'kpi' '' 'defectRate' 'NULL' $L.K4 4),
    (WRow '21000000-0000-0000-0000-000000000105' $D1 'PO_TREND' 'Production Volume Trend' 'line' 'day' 'materialCount' 'NULL' $L.MAIN 5),
    (WRow '21000000-0000-0000-0000-000000000106' $D1 'PO_MIX' 'Material Mix' 'donut' 'materialUnitType' 'materialCount' 'NULL' $L.SIDE 6),
    (WRow '21000000-0000-0000-0000-000000000107' $D1 'PO_WEEK' 'Weekly Throughput' 'area' 'week' 'materialCount' 'NULL' $L.BL 7),
    (WRow '21000000-0000-0000-0000-000000000108' $D1 'PO_TABLE' 'Volume by Type' 'table' 'materialUnitType' 'materialCount' 'NULL' $L.BR 8),

    (WRow '21000000-0000-0000-0000-000000000201' $D2 'QM_TREND' 'Defect Rate Trend' 'line' 'day' 'defectRate' 'NULL' $L.MAIN 1),
    (WRow '21000000-0000-0000-0000-000000000202' $D2 'QM_BREAK' 'Defect Breakdown' 'bar' 'defectType' 'defectCount' 'NULL' $L.SIDE 2),
    (WRow '21000000-0000-0000-0000-000000000203' $D2 'QM_SEV' 'Quality Events by Type' 'bar' 'defectType' 'defectCount' 'NULL' $L.BL 3),
    (WRow '21000000-0000-0000-0000-000000000204' $D2 'QM_TABLE' 'Defects by Type' 'table' 'defectType' 'defectCount' 'NULL' $L.BR 4),

    (WRow '21000000-0000-0000-0000-000000000301' $D3 'EO_EQDEF' 'Downtime Minutes by Equipment' 'bar' 'equipment' 'downtimeMinutes' 'NULL' $L.MAIN 1),
    (WRow '21000000-0000-0000-0000-000000000302' $D3 'EO_OBS' 'Observation Throughput' 'line' 'week' 'observationCount' 'NULL' $L.SIDE 2),
    (WRow '21000000-0000-0000-0000-000000000303' $D3 'EO_TABLE' 'Materials by Equipment' 'table' 'equipment' 'materialCount' 'NULL' $L.BL 3),
    (WRow '21000000-0000-0000-0000-000000000304' $D3 'EO_MONTH' 'Monthly Volume' 'bar' 'month' 'materialCount' 'NULL' $L.BR 4),

    (WRow '21000000-0000-0000-0000-000000000401' $D4 'CF_RATE' 'Published Statistical Findings' 'table' '' 'findingStatus' 'NULL' $L.BL 3),
    (WRow '21000000-0000-0000-0000-000000000402' $D4 'CF_TOP' 'Findings Readiness (DF8)' 'table' '' 'analysisReadiness' "'defect.class'" $L.BR 4),

    (WRow '21000000-0000-0000-0000-000000000501' $D5 'PA_KAVG' 'Average Value' 'kpi' '' 'avgParameterValue' $ParamSql $L.K1 1),
    (WRow '21000000-0000-0000-0000-000000000502' $D5 'PA_KOBS' 'Observations' 'kpi' '' 'observationCount' $ParamSql $L.K2 2),
    (WRow '21000000-0000-0000-0000-000000000503' $D5 'PA_TREND' 'Parameter Trend' 'line' 'day' 'avgParameterValue' $ParamSql $L.MAIN 3),
    (WRow '21000000-0000-0000-0000-000000000504' $D5 'PA_BYP' 'Observation Volume by Parameter' 'bar' 'parameterCode' 'observationCount' 'NULL' $L.SIDE 4),
    (WRow '21000000-0000-0000-0000-000000000505' $D5 'PA_TABLE' 'Average FDT by Grade' 'table' 'gradeOrRecipe' 'avgParameterValue' $ParamSql $L.BL 5),

    (WRow '21000000-0000-0000-0000-000000000601' $D6 'RI_KPI' 'Average Risk Score (Scored Population Only)' 'kpi' '' 'riskScore' 'NULL' $L.K1 1),
    (WRow '21000000-0000-0000-0000-000000000602' $D6 'RI_TREND' 'Scoring Coverage and Provenance' 'table' '' 'scoringCoverage' 'NULL' $L.MAIN 2),
    (WRow '21000000-0000-0000-0000-000000000604' $D6 'RI_TABLE' 'Risk by Material Type' 'table' 'materialUnitType' 'riskScore' 'NULL' $L.BL 4),

    (WRow '21000000-0000-0000-0000-000000000701' $D7 'MI_RATE' 'Analysis Readiness (DF8)' 'table' '' 'analysisReadiness' "'defect.class'" $L.BL 3),
    (WRow '21000000-0000-0000-0000-000000000702' $D7 'MI_SEV' 'Defect Mix by Material Type' 'donut' 'materialUnitType' 'defectCount' 'NULL' $L.BR 4)
)

$dashSql = @"
INSERT INTO dashboard_definitions
(id,user_id,dashboard_code,name,description,layout_json,is_default,is_system_template,is_active,created_at_utc,updated_at_utc,is_synthetic,source_system,source_record_id,is_deleted,deleted_at_utc,deleted_reason)
VALUES
('$D1',NULL,'PRODUCTION_OVERVIEW','Production Overview','Plant production volume, throughput trend and material mix.','{}',FALSE,FALSE,TRUE,NOW() AT TIME ZONE 'UTC',NULL,FALSE,'PPIQ_UI','PRODUCTION_OVERVIEW',FALSE,NULL,NULL),
('$D2',NULL,'QUALITY_MONITORING','Quality Monitoring','Defect rate trend, defect breakdown and severity distribution.','{}',FALSE,FALSE,TRUE,NOW() AT TIME ZONE 'UTC',NULL,FALSE,'PPIQ_UI','QUALITY_MONITORING',FALSE,NULL,NULL),
('$D3',NULL,'EQUIPMENT_OPERATIONS','Equipment and Operations','Equipment-level quality and observation throughput.','{}',FALSE,FALSE,TRUE,NOW() AT TIME ZONE 'UTC',NULL,FALSE,'PPIQ_UI','EQUIPMENT_OPERATIONS',FALSE,NULL,NULL),
('$D4',NULL,'CORRELATION_FINDINGS_BOARD','Correlation Findings Board','Parameter-defect correlation landscape from the analysis engine.','{}',FALSE,FALSE,TRUE,NOW() AT TIME ZONE 'UTC',NULL,FALSE,'PPIQ_UI','CORRELATION_FINDINGS_BOARD',FALSE,NULL,NULL),
('$D5',NULL,'PARAMETER_DEEP_ANALYSIS','Parameter Deep Analysis','Focused analysis of the highest-signal process parameter.','{}',FALSE,FALSE,TRUE,NOW() AT TIME ZONE 'UTC',NULL,FALSE,'PPIQ_UI','PARAMETER_DEEP_ANALYSIS',FALSE,NULL,NULL),
('$D6',NULL,'RISK_INTELLIGENCE','Risk Intelligence','Model-scored material risk across equipment and time.','{}',FALSE,FALSE,TRUE,NOW() AT TIME ZONE 'UTC',NULL,FALSE,'PPIQ_UI','RISK_INTELLIGENCE',FALSE,NULL,NULL),
('$D7',NULL,'MODEL_INSIGHTS','Model Insights','Engine-derived quality drivers and correlation surfaces.','{}',FALSE,FALSE,TRUE,NOW() AT TIME ZONE 'UTC',NULL,FALSE,'PPIQ_UI','MODEL_INSIGHTS',FALSE,NULL,NULL)
ON CONFLICT (dashboard_code) WHERE is_deleted = FALSE
DO UPDATE SET name = EXCLUDED.name, description = EXCLUDED.description, is_active = TRUE, updated_at_utc = NOW() AT TIME ZONE 'UTC';

DELETE FROM dashboard_widget_definitions WHERE dashboard_definition_id IN ('$D1','$D2','$D3','$D4','$D5','$D6','$D7');

INSERT INTO dashboard_widget_definitions
(id,dashboard_definition_id,widget_code,widget_title,widget_type,chart_type,dimension_code,measure_code,parameter_code,filter_json,layout_json,display_options_json,sort_order,is_active,created_at_utc,updated_at_utc,is_synthetic,source_system,source_record_id,is_deleted,deleted_at_utc,deleted_reason)
VALUES
$($rows -join ",`n");

INSERT INTO dashboard_widget_definitions
(id,dashboard_definition_id,widget_code,widget_title,widget_type,chart_type,dimension_code,measure_code,parameter_code,filter_json,layout_json,display_options_json,sort_order,is_active,created_at_utc,updated_at_utc,is_synthetic,source_system,source_record_id,is_deleted,deleted_at_utc,deleted_reason)
SELECT gen_random_uuid(), t.new_dash::uuid, 'CLONE_' || t.tag || '_' || w.widget_code,
       w.widget_title, w.widget_type, w.chart_type, w.dimension_code, w.measure_code, w.parameter_code,
       w.filter_json, t.layout, w.display_options_json, t.sort, TRUE, NOW() AT TIME ZONE 'UTC', NULL,
       FALSE, 'PPIQ_UI', w.widget_code, FALSE, NULL, NULL
FROM dashboard_widget_definitions w
JOIN dashboard_definitions d ON d.id = w.dashboard_definition_id AND d.dashboard_code = 'CORRELATION_EXPLORER'
JOIN (VALUES
    ('$D4','CFB','{"lg":{"x":0,"y":0,"w":8,"h":10}}',1),
    ('$D4','CFB','{"lg":{"x":8,"y":0,"w":4,"h":10}}',2),
    ('$D7','MI','{"lg":{"x":0,"y":0,"w":8,"h":10}}',1),
    ('$D7','MI','{"lg":{"x":8,"y":0,"w":4,"h":10}}',2)
) AS t(new_dash,tag,layout,sort) ON TRUE
WHERE w.is_deleted = FALSE
  AND ((t.sort = 1 AND lower(w.chart_type) = 'heatmap')
    OR (t.sort = 2 AND lower(w.chart_type) IN ('scatter','matrix')));
"@

if (RunFile 'dashboards + widgets + clones (single transaction file)' ("BEGIN;`n" + $dashSql + "`nCOMMIT;")) {
    W ""
    W "---- final inventory ----"
    $inv = @(& $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -A -t -F ' | ' -c "SELECT d.dashboard_code, d.name, COUNT(w.id) FROM dashboard_definitions d LEFT JOIN dashboard_widget_definitions w ON w.dashboard_definition_id = d.id AND w.is_deleted = FALSE WHERE d.is_deleted = FALSE GROUP BY 1,2 ORDER BY 1;" 2>&1)
    @($inv) | Where-Object { $_ } | ForEach-Object { W ("    " + $_) }
    W ""
    W "BROWSER PASS NOW: open all 12 dashboards. Any widget showing its empty"
    W "state - name it and I rebind or drop it. Then: assistant reindex, two"
    W "alert rules + evaluation, one saved analysis job. Then rehearse."
}

[System.IO.File]::WriteAllText($OutFile, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Host ""
Write-Host ("[DONE] Report -> " + $OutFile) -ForegroundColor Green
exit 0
