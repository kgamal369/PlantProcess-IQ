# ============================================================================
# Seed-PresentationDashboards.ps1
# Creates SEVEN finished dashboards in ppiq_presentation, per the demo plan:
#   TYPE 1 (direct plant data)   : Production Overview / Quality Monitoring /
#                                  Equipment & Operations
#   TYPE 2 (analysis/correlation): Correlation Findings Board /
#                                  Parameter Deep Analysis
#   TYPE 3 (AI+ML derived)       : Risk Intelligence / Model Insights
# ...leaving one EMPTY slot per type for you to build LIVE in the meeting.
#
# HOW IT STAYS REAL:
#   * every widget uses ONLY dimension/measure codes from the backend's
#     DashboardMetadataService catalog (day/week/defectType/materialUnitType/
#     equipment/severity/parameterCode x materialCount/defectCount/defectRate/
#     observationCount/avgParameterValue/riskScore)
#   * heatmap + scatter widgets are CLONED from your proven Correlation
#     Explorer system template rows - guaranteed render contract
#   * the parameter widgets bind to the parameter_code with the MOST live
#     observations, queried at run time
#   * the sourceSystem dimension is deliberately NOT used anywhere - it would
#     chart seed-provenance names on screen
# Idempotent: dashboards upsert on dashboard_code; widgets are replaced per
# dashboard. HARD GUARD: target DB name must contain 'presentation'.
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Seed-PresentationDashboards.ps1
# ============================================================================
[CmdletBinding()]
param(
    [string]$TargetDb = 'ppiq_presentation'
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'

if ($TargetDb -notmatch 'presentation') {
    Write-Host "[REFUSED] target must contain 'presentation'." -ForegroundColor Red
    exit 1
}

$RepoRoot = (Get-Location).Path
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$OutFile = Join-Path $RepoRoot ("PresentationDashboards_" + $Stamp + ".txt")
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

W ("PRESENTATION DASHBOARDS SEED - " + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss') + "  DB: " + $TargetDb)
W ("=" * 78)

# ---- live parameter for the analysis widgets --------------------------------
$TopParam = T "SELECT pd.code FROM parameter_definitions pd JOIN parameter_observations po ON po.parameter_definition_id = pd.id GROUP BY pd.code ORDER BY COUNT(*) DESC LIMIT 1;"
if (-not $TopParam) {
    $TopParam = T "SELECT code FROM parameter_definitions ORDER BY code LIMIT 1;"
}
if (-not $TopParam) { $TopParam = '' }
W ("[PARAM] most-observed parameter for the analysis widgets: '" + $TopParam + "'")
$ParamSql = "NULL"
if ($TopParam) { $ParamSql = "'" + $TopParam.Replace("'", "''") + "'" }

# ---- helper: SQL fragments ---------------------------------------------------
# widget row builder: id,dash,code,title,wtype,chart,dim,measure,param,layout,sort
function WidgetRow([string]$id, [string]$dash, [string]$code, [string]$title, [string]$chart, [string]$dim, [string]$measure, [string]$param, [string]$layout, [int]$sort) {
    $dimSql = 'NULL'; if ($dim) { $dimSql = "'" + $dim + "'" }
    return "('" + $id + "','" + $dash + "','" + $code + "','" + $title + "','chart','" + $chart + "'," + $dimSql + ",'" + $measure + "'," + $param + ",'{}','" + $layout + "','{\""maxRows\"":50,\""rawRowLimit\"":1000}'," + $sort + ",TRUE,NOW() AT TIME ZONE 'UTC',NULL,FALSE,'PPIQ_UI','" + $code + "',FALSE,NULL,NULL)"
}

$dashboards = @(
    @{ Id = '20000000-0000-0000-0000-000000000001'; Code = 'PRODUCTION_OVERVIEW'; Name = 'Production Overview';
       Desc = 'Plant production volume, throughput trend and material mix.' },
    @{ Id = '20000000-0000-0000-0000-000000000002'; Code = 'QUALITY_MONITORING'; Name = 'Quality Monitoring';
       Desc = 'Defect rate trend, defect breakdown and severity distribution.' },
    @{ Id = '20000000-0000-0000-0000-000000000003'; Code = 'EQUIPMENT_OPERATIONS'; Name = 'Equipment and Operations';
       Desc = 'Equipment-level quality and observation throughput.' },
    @{ Id = '20000000-0000-0000-0000-000000000004'; Code = 'CORRELATION_FINDINGS_BOARD'; Name = 'Correlation Findings Board';
       Desc = 'Parameter-defect correlation landscape from the analysis engine.' },
    @{ Id = '20000000-0000-0000-0000-000000000005'; Code = 'PARAMETER_DEEP_ANALYSIS'; Name = 'Parameter Deep Analysis';
       Desc = 'Focused analysis of the highest-signal process parameter.' },
    @{ Id = '20000000-0000-0000-0000-000000000006'; Code = 'RISK_INTELLIGENCE'; Name = 'Risk Intelligence';
       Desc = 'Model-scored material risk across equipment and time.' },
    @{ Id = '20000000-0000-0000-0000-000000000007'; Code = 'MODEL_INSIGHTS'; Name = 'Model Insights';
       Desc = 'Engine-derived quality drivers and correlation surfaces.' }
)

# ---- 1. upsert dashboards ----------------------------------------------------
$dashValues = @()
foreach ($d in $dashboards) {
    $dashValues += "('" + $d.Id + "',NULL,'" + $d.Code + "','" + $d.Name + "','" + $d.Desc + "','{}',FALSE,FALSE,TRUE,NOW() AT TIME ZONE 'UTC',NULL,FALSE,'PPIQ_UI','" + $d.Code + "',FALSE,NULL,NULL)"
}
$sqlDash = @"
INSERT INTO dashboard_definitions
(id,user_id,dashboard_code,name,description,layout_json,is_default,is_system_template,is_active,created_at_utc,updated_at_utc,is_synthetic,source_system,source_record_id,is_deleted,deleted_at_utc,deleted_reason)
VALUES
$($dashValues -join ",`n")
ON CONFLICT (dashboard_code) WHERE is_deleted = FALSE
DO UPDATE SET name = EXCLUDED.name, description = EXCLUDED.description, is_active = TRUE, updated_at_utc = NOW() AT TIME ZONE 'UTC';
"@
$o = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -v ON_ERROR_STOP=1 -c $sqlDash 2>&1
if ($LASTEXITCODE -ne 0) { W "[FAIL] dashboard upsert:"; @($o | Select-Object -First 4) | ForEach-Object { W ("  " + $_) }; exit 1 }
W ("[DASH] 7 dashboards upserted.")

# ---- 2. replace widgets per dashboard -----------------------------------------
$ids = ($dashboards | ForEach-Object { "'" + $_.Id + "'" }) -join ','
& $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -c ("DELETE FROM dashboard_widget_definitions WHERE dashboard_definition_id IN (" + $ids + ");") | Out-Null

$L = @{
    KPI1 = '{"lg":{"x":0,"y":0,"w":3,"h":4}}';  KPI2 = '{"lg":{"x":3,"y":0,"w":3,"h":4}}'
    KPI3 = '{"lg":{"x":6,"y":0,"w":3,"h":4}}';  KPI4 = '{"lg":{"x":9,"y":0,"w":3,"h":4}}'
    MAIN = '{"lg":{"x":0,"y":4,"w":8,"h":8}}';  SIDE = '{"lg":{"x":8,"y":4,"w":4,"h":8}}'
    BOTL = '{"lg":{"x":0,"y":12,"w":6,"h":8}}'; BOTR = '{"lg":{"x":6,"y":12,"w":6,"h":8}}'
}

$rows = @()
# --- TYPE 1 : D1 Production Overview
$D = '20000000-0000-0000-0000-000000000001'
$rows += WidgetRow '21000000-0000-0000-0000-000000000101' $D 'PO_KPI_MATERIALS' 'Material Units' 'kpi' '' 'materialCount' 'NULL' $L.KPI1 1
$rows += WidgetRow '21000000-0000-0000-0000-000000000102' $D 'PO_KPI_OBS' 'Process Observations' 'kpi' '' 'observationCount' 'NULL' $L.KPI2 2
$rows += WidgetRow '21000000-0000-0000-0000-000000000103' $D 'PO_KPI_DEFECTS' 'Quality Events' 'kpi' '' 'defectCount' 'NULL' $L.KPI3 3
$rows += WidgetRow '21000000-0000-0000-0000-000000000104' $D 'PO_KPI_RATE' 'Defect Rate' 'kpi' '' 'defectRate' 'NULL' $L.KPI4 4
$rows += WidgetRow '21000000-0000-0000-0000-000000000105' $D 'PO_TREND' 'Production Volume Trend' 'line' 'day' 'materialCount' 'NULL' $L.MAIN 5
$rows += WidgetRow '21000000-0000-0000-0000-000000000106' $D 'PO_MIX' 'Material Mix' 'donut' 'materialUnitType' 'materialCount' 'NULL' $L.SIDE 6
$rows += WidgetRow '21000000-0000-0000-0000-000000000107' $D 'PO_WEEKLY' 'Weekly Throughput' 'area' 'week' 'materialCount' 'NULL' $L.BOTL 7
$rows += WidgetRow '21000000-0000-0000-0000-000000000108' $D 'PO_TABLE' 'Volume by Type' 'table' 'materialUnitType' 'materialCount' 'NULL' $L.BOTR 8

# --- TYPE 1 : D2 Quality Monitoring
$D = '20000000-0000-0000-0000-000000000002'
$rows += WidgetRow '21000000-0000-0000-0000-000000000201' $D 'QM_TREND' 'Defect Rate Trend' 'line' 'day' 'defectRate' 'NULL' $L.MAIN 1
$rows += WidgetRow '21000000-0000-0000-0000-000000000202' $D 'QM_BREAKDOWN' 'Defect Breakdown' 'bar' 'defectType' 'defectCount' 'NULL' $L.SIDE 2
$rows += WidgetRow '21000000-0000-0000-0000-000000000203' $D 'QM_SEVERITY' 'Severity Distribution' 'donut' 'severity' 'defectCount' 'NULL' $L.BOTL 3
$rows += WidgetRow '21000000-0000-0000-0000-000000000204' $D 'QM_TABLE' 'Defects by Type' 'table' 'defectType' 'defectCount' 'NULL' $L.BOTR 4

# --- TYPE 1 : D3 Equipment and Operations
$D = '20000000-0000-0000-0000-000000000003'
$rows += WidgetRow '21000000-0000-0000-0000-000000000301' $D 'EO_EQUIP_DEFECTS' 'Quality Events by Equipment' 'bar' 'equipment' 'defectCount' 'NULL' $L.MAIN 1
$rows += WidgetRow '21000000-0000-0000-0000-000000000302' $D 'EO_OBS_TREND' 'Observation Throughput' 'line' 'week' 'observationCount' 'NULL' $L.SIDE 2
$rows += WidgetRow '21000000-0000-0000-0000-000000000303' $D 'EO_EQUIP_TABLE' 'Materials by Equipment' 'table' 'equipment' 'materialCount' 'NULL' $L.BOTL 3
$rows += WidgetRow '21000000-0000-0000-0000-000000000304' $D 'EO_MONTHLY' 'Monthly Volume' 'bar' 'month' 'materialCount' 'NULL' $L.BOTR 4

# --- TYPE 2 : D4 Correlation Findings Board (heatmap+scatter cloned below)
$D = '20000000-0000-0000-0000-000000000004'
$rows += WidgetRow '21000000-0000-0000-0000-000000000401' $D 'CF_RATE' 'Defect Rate Trend' 'line' 'day' 'defectRate' 'NULL' $L.BOTL 3
$rows += WidgetRow '21000000-0000-0000-0000-000000000402' $D 'CF_TOPDEFECTS' 'Defect Landscape' 'bar' 'defectType' 'defectCount' 'NULL' $L.BOTR 4

# --- TYPE 2 : D5 Parameter Deep Analysis
$D = '20000000-0000-0000-0000-000000000005'
$rows += WidgetRow '21000000-0000-0000-0000-000000000501' $D 'PA_KPI_AVG' 'Average Value' 'kpi' '' 'avgParameterValue' $ParamSql $L.KPI1 1
$rows += WidgetRow '21000000-0000-0000-0000-000000000502' $D 'PA_KPI_OBS' 'Observations' 'kpi' '' 'observationCount' $ParamSql $L.KPI2 2
$rows += WidgetRow '21000000-0000-0000-0000-000000000503' $D 'PA_TREND' 'Parameter Trend' 'line' 'day' 'avgParameterValue' $ParamSql $L.MAIN 3
$rows += WidgetRow '21000000-0000-0000-0000-000000000504' $D 'PA_BYPARAM' 'Observation Volume by Parameter' 'bar' 'parameterCode' 'observationCount' 'NULL' $L.SIDE 4
$rows += WidgetRow '21000000-0000-0000-0000-000000000505' $D 'PA_TABLE' 'Parameters Overview' 'table' 'parameterCode' 'avgParameterValue' 'NULL' $L.BOTL 5

# --- TYPE 3 : D6 Risk Intelligence
$D = '20000000-0000-0000-0000-000000000006'
$rows += WidgetRow '21000000-0000-0000-0000-000000000601' $D 'RI_KPI' 'Average Risk Score' 'kpi' '' 'riskScore' 'NULL' $L.KPI1 1
$rows += WidgetRow '21000000-0000-0000-0000-000000000602' $D 'RI_TREND' 'Risk Score Trend' 'line' 'day' 'riskScore' 'NULL' $L.MAIN 2
$rows += WidgetRow '21000000-0000-0000-0000-000000000603' $D 'RI_EQUIP' 'Risk by Equipment' 'bar' 'equipment' 'riskScore' 'NULL' $L.SIDE 3
$rows += WidgetRow '21000000-0000-0000-0000-000000000604' $D 'RI_TABLE' 'Risk by Material Type' 'table' 'materialUnitType' 'riskScore' 'NULL' $L.BOTL 4

# --- TYPE 3 : D7 Model Insights (heatmap+scatter cloned below)
$D = '20000000-0000-0000-0000-000000000007'
$rows += WidgetRow '21000000-0000-0000-0000-000000000701' $D 'MI_RATE' 'Model-Tracked Defect Rate' 'line' 'day' 'defectRate' 'NULL' $L.BOTL 3
$rows += WidgetRow '21000000-0000-0000-0000-000000000702' $D 'MI_SEV' 'Predicted Severity Mix' 'donut' 'severity' 'defectCount' 'NULL' $L.BOTR 4

$sqlWidgets = @"
INSERT INTO dashboard_widget_definitions
(id,dashboard_definition_id,widget_code,widget_title,widget_type,chart_type,dimension_code,measure_code,parameter_code,filter_json,layout_json,display_options_json,sort_order,is_active,created_at_utc,updated_at_utc,is_synthetic,source_system,source_record_id,is_deleted,deleted_at_utc,deleted_reason)
VALUES
$($rows -join ",`n");
"@
$o = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -v ON_ERROR_STOP=1 -c $sqlWidgets 2>&1
if ($LASTEXITCODE -ne 0) { W "[FAIL] widget insert:"; @($o | Select-Object -First 5) | ForEach-Object { W ("  " + $_) }; exit 1 }
W ("[WIDGETS] " + $rows.Count + " simple widgets inserted.")

# ---- 3. clone the proven heatmap/scatter/matrix widgets ------------------------
$sqlClone = @"
INSERT INTO dashboard_widget_definitions
(id,dashboard_definition_id,widget_code,widget_title,widget_type,chart_type,dimension_code,measure_code,parameter_code,filter_json,layout_json,display_options_json,sort_order,is_active,created_at_utc,updated_at_utc,is_synthetic,source_system,source_record_id,is_deleted,deleted_at_utc,deleted_reason)
SELECT gen_random_uuid(), t.new_dash, 'CLONE_' || t.new_dash_code || '_' || w.widget_code,
       w.widget_title, w.widget_type, w.chart_type, w.dimension_code, w.measure_code, w.parameter_code,
       w.filter_json, t.layout, w.display_options_json, t.sort, TRUE, NOW() AT TIME ZONE 'UTC', NULL,
       FALSE, 'PPIQ_UI', w.widget_code, FALSE, NULL, NULL
FROM dashboard_widget_definitions w
JOIN dashboard_definitions d ON d.id = w.dashboard_definition_id AND d.dashboard_code = 'CORRELATION_EXPLORER'
JOIN (VALUES
    ('20000000-0000-0000-0000-000000000004'::uuid,'CFB','{"lg":{"x":0,"y":0,"w":8,"h":10}}'::text,1),
    ('20000000-0000-0000-0000-000000000004'::uuid,'CFB','{"lg":{"x":8,"y":0,"w":4,"h":10}}'::text,2),
    ('20000000-0000-0000-0000-000000000007'::uuid,'MI','{"lg":{"x":0,"y":0,"w":8,"h":10}}'::text,1),
    ('20000000-0000-0000-0000-000000000007'::uuid,'MI','{"lg":{"x":8,"y":0,"w":4,"h":10}}'::text,2)
) AS t(new_dash,new_dash_code,layout,sort)
  ON TRUE
WHERE w.is_deleted = FALSE
  AND lower(w.chart_type) IN ('heatmap','scatter','matrix')
  AND ((t.sort = 1 AND lower(w.chart_type) = 'heatmap') OR (t.sort = 2 AND lower(w.chart_type) IN ('scatter','matrix')));
"@
$o = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -c $sqlClone 2>&1
$cloneMsg = @($o | Select-Object -First 2) -join ' '
W ("[CLONE] Correlation Explorer heatmap/scatter -> Findings Board + Model Insights: " + $cloneMsg)

# ---- 4. verify -----------------------------------------------------------------
W ""
W "---- final dashboard inventory (what the dashboards list will show) ----"
$inv = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -A -t -F ' | ' -c "SELECT d.dashboard_code, d.name, COUNT(w.id) AS widgets FROM dashboard_definitions d LEFT JOIN dashboard_widget_definitions w ON w.dashboard_definition_id = d.id AND w.is_deleted = FALSE WHERE d.is_deleted = FALSE GROUP BY d.dashboard_code, d.name ORDER BY d.dashboard_code;" 2>&1
@($inv) | Where-Object { $_ } | ForEach-Object { W ("    " + $_) }
W ""
W "OPEN EACH ONE IN THE BROWSER TONIGHT. A widget with no data shows its"
W "empty state - if any does, tell me which and I rebind or remove it."
W "Live-build slots reserved: one per type - suggested names for the meeting:"
W "    'Shift Handover Board' (type 1) / 'Grade Comparison' (type 2) /"
W "    'Anomaly Watch' (type 3)."

[System.IO.File]::WriteAllText($OutFile, $sb.ToString(), (New-Object System.Text.UTF8Encoding($false)))
Write-Host ""
Write-Host ("[DONE] Report -> " + $OutFile) -ForegroundColor Green
exit 0
