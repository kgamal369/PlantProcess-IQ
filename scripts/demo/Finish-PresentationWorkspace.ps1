# ============================================================================
# Finish-PresentationWorkspace.ps1  (one run, both fixes, drift-aware)
#
# PART A - widgets v3: your schema declares dimension_code NOT NULL; v2's KPI
#   tiles passed NULL and the whole transaction rolled back. Fix: KPI tiles
#   bind dimension 'day' (renderer aggregates the measure; the dimension just
#   satisfies the contract). Provenance + ML scrubs from v2 already committed.
#
# PART B - workspace wiring, CONVERGENT: your tsc output proved App.tsx
#   already imports RoutedInteractiveWorkspacePage (line 43) - a parallel
#   session wired a workspace since my snapshot. This part DETECTS each piece
#   (page file / shim / App import / App route) and only adds what is missing.
#   It NEVER overwrites an existing InteractiveWorkspacePage.tsx - if one
#   exists, it is kept and reported.
#
# Contract: byte backups of anything modified, tsc -b gate, revert of ONLY
# the files this run modified.
# Run from repo root (presentation branch):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Finish-PresentationWorkspace.ps1
# ============================================================================
[CmdletBinding()]
param(
    [string]$TargetDb = 'ppiq_presentation',
    [switch]$SkipGate
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'
if ($TargetDb -notmatch 'presentation') { Write-Host "[REFUSED] guard active." -ForegroundColor Red; exit 1 }

$RepoRoot = (Get-Location).Path
$Web = Join-Path $RepoRoot 'Frontend\PlantProcess.Web'
$NewPage = Join-Path $Web 'src\pages\Dashboard\InteractiveWorkspacePage.tsx'
$Shim = Join-Path $Web 'src\pages\Dashboard\DashboardPageContent.tsx'
$AppTsx = Join-Path $Web 'src\App.tsx'
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$BackupDir = Join-Path $RepoRoot ("deploy\.ppiq-backups\finish-workspace-" + $Stamp)
New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
$Modified = New-Object System.Collections.ArrayList

function Backup-And-Track([string]$path) {
    Copy-Item -LiteralPath $path -Destination (Join-Path $BackupDir (Split-Path $path -Leaf)) -Force
    [void]$Modified.Add($path)
}
function Restore-Modified {
    foreach ($p in $Modified) {
        $b = Join-Path $BackupDir (Split-Path $p -Leaf)
        if (Test-Path $b) { Copy-Item $b $p -Force } elseif (Test-Path $p) { Remove-Item $p -Force }
    }
    Write-Host ("[REVERT] restored " + $Modified.Count + " file(s). Backup: " + $BackupDir) -ForegroundColor Yellow
}

# =====================  PART A : WIDGETS v3  =====================
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
    if ($LASTEXITCODE -ne 0) { return '' }
    $l = @($o | Where-Object { $_ -and ($_.ToString().Trim() -ne '') }) | Select-Object -First 1
    if ($null -eq $l) { return '' }
    return $l.ToString().Trim()
}

$TopParam = T "SELECT pd.parameter_code FROM parameter_definitions pd JOIN parameter_observations po ON po.parameter_definition_id = pd.id GROUP BY pd.parameter_code ORDER BY COUNT(*) DESC LIMIT 1;"
if (-not $TopParam) { $TopParam = 'rolling.cooling_rate' }
$ParamSql = "'" + $TopParam.Replace("'", "''") + "'"
Write-Host ("[A] parameter widgets bind to: " + $TopParam)

function WRow([string]$id, [string]$dash, [string]$code, [string]$title, [string]$chart, [string]$dim, [string]$measure, [string]$param, [string]$layout, [int]$sort) {
    if (-not $dim) { $dim = 'day' }  # NOT NULL fix: KPI tiles aggregate over day
    return "('$id','$dash','$code','$title','chart','$chart','$dim','$measure',$param,'{}','$layout','{""maxRows"":50,""rawRowLimit"":1000}',$sort,TRUE,NOW() AT TIME ZONE 'UTC',NULL,FALSE,'PPIQ_UI','$code',FALSE,NULL,NULL)".Replace('""', '"')
}
$L = @{ K1 = '{"lg":{"x":0,"y":0,"w":3,"h":4}}'; K2 = '{"lg":{"x":3,"y":0,"w":3,"h":4}}'; K3 = '{"lg":{"x":6,"y":0,"w":3,"h":4}}'; K4 = '{"lg":{"x":9,"y":0,"w":3,"h":4}}'; MAIN = '{"lg":{"x":0,"y":4,"w":8,"h":8}}'; SIDE = '{"lg":{"x":8,"y":4,"w":4,"h":8}}'; BL = '{"lg":{"x":0,"y":12,"w":6,"h":8}}'; BR = '{"lg":{"x":6,"y":12,"w":6,"h":8}}' }
$D1 = '20000000-0000-0000-0000-000000000001'; $D2 = '20000000-0000-0000-0000-000000000002'; $D3 = '20000000-0000-0000-0000-000000000003'; $D4 = '20000000-0000-0000-0000-000000000004'; $D5 = '20000000-0000-0000-0000-000000000005'; $D6 = '20000000-0000-0000-0000-000000000006'; $D7 = '20000000-0000-0000-0000-000000000007'

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
    (WRow '21000000-0000-0000-0000-000000000203' $D2 'QM_SEV' 'Severity Distribution' 'donut' 'severity' 'defectCount' 'NULL' $L.BL 3),
    (WRow '21000000-0000-0000-0000-000000000204' $D2 'QM_TABLE' 'Defects by Type' 'table' 'defectType' 'defectCount' 'NULL' $L.BR 4),
    (WRow '21000000-0000-0000-0000-000000000301' $D3 'EO_EQDEF' 'Quality Events by Equipment' 'bar' 'equipment' 'defectCount' 'NULL' $L.MAIN 1),
    (WRow '21000000-0000-0000-0000-000000000302' $D3 'EO_OBS' 'Observation Throughput' 'line' 'week' 'observationCount' 'NULL' $L.SIDE 2),
    (WRow '21000000-0000-0000-0000-000000000303' $D3 'EO_TABLE' 'Materials by Equipment' 'table' 'equipment' 'materialCount' 'NULL' $L.BL 3),
    (WRow '21000000-0000-0000-0000-000000000304' $D3 'EO_MONTH' 'Monthly Volume' 'bar' 'month' 'materialCount' 'NULL' $L.BR 4),
    (WRow '21000000-0000-0000-0000-000000000401' $D4 'CF_RATE' 'Defect Rate Trend' 'line' 'day' 'defectRate' 'NULL' $L.BL 3),
    (WRow '21000000-0000-0000-0000-000000000402' $D4 'CF_TOP' 'Defect Landscape' 'bar' 'defectType' 'defectCount' 'NULL' $L.BR 4),
    (WRow '21000000-0000-0000-0000-000000000501' $D5 'PA_KAVG' 'Average Value' 'kpi' '' 'avgParameterValue' $ParamSql $L.K1 1),
    (WRow '21000000-0000-0000-0000-000000000502' $D5 'PA_KOBS' 'Observations' 'kpi' '' 'observationCount' $ParamSql $L.K2 2),
    (WRow '21000000-0000-0000-0000-000000000503' $D5 'PA_TREND' 'Parameter Trend' 'line' 'day' 'avgParameterValue' $ParamSql $L.MAIN 3),
    (WRow '21000000-0000-0000-0000-000000000504' $D5 'PA_BYP' 'Observation Volume by Parameter' 'bar' 'parameterCode' 'observationCount' 'NULL' $L.SIDE 4),
    (WRow '21000000-0000-0000-0000-000000000505' $D5 'PA_TABLE' 'Parameters Overview' 'table' 'parameterCode' 'avgParameterValue' 'NULL' $L.BL 5),
    (WRow '21000000-0000-0000-0000-000000000601' $D6 'RI_KPI' 'Average Risk Score' 'kpi' '' 'riskScore' 'NULL' $L.K1 1),
    (WRow '21000000-0000-0000-0000-000000000602' $D6 'RI_TREND' 'Risk Score Trend' 'line' 'day' 'riskScore' 'NULL' $L.MAIN 2),
    (WRow '21000000-0000-0000-0000-000000000603' $D6 'RI_EQUIP' 'Risk by Equipment' 'bar' 'equipment' 'riskScore' 'NULL' $L.SIDE 3),
    (WRow '21000000-0000-0000-0000-000000000604' $D6 'RI_TABLE' 'Risk by Material Type' 'table' 'materialUnitType' 'riskScore' 'NULL' $L.BL 4),
    (WRow '21000000-0000-0000-0000-000000000701' $D7 'MI_RATE' 'Model-Tracked Defect Rate' 'line' 'day' 'defectRate' 'NULL' $L.BL 3),
    (WRow '21000000-0000-0000-0000-000000000702' $D7 'MI_SEV' 'Predicted Severity Mix' 'donut' 'severity' 'defectCount' 'NULL' $L.BR 4)
)

$sql = "BEGIN;`nDELETE FROM dashboard_widget_definitions WHERE dashboard_definition_id IN ('$D1','$D2','$D3','$D4','$D5','$D6','$D7');`nINSERT INTO dashboard_widget_definitions`n(id,dashboard_definition_id,widget_code,widget_title,widget_type,chart_type,dimension_code,measure_code,parameter_code,filter_json,layout_json,display_options_json,sort_order,is_active,created_at_utc,updated_at_utc,is_synthetic,source_system,source_record_id,is_deleted,deleted_at_utc,deleted_reason)`nVALUES`n" + ($rows -join ",`n") + ";`n"
$sql += @"
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
COMMIT;
"@
$tmp = Join-Path $env:TEMP ("ppiq_widgets_v3_" + [guid]::NewGuid().ToString('N') + ".sql")
[System.IO.File]::WriteAllText($tmp, $sql, (New-Object System.Text.UTF8Encoding($false)))
$o = & $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -v ON_ERROR_STOP=1 -f $tmp 2>&1
$aOk = ($LASTEXITCODE -eq 0)
Remove-Item $tmp -ErrorAction SilentlyContinue
if ($aOk) {
    Write-Host "[A] WIDGETS INSERTED." -ForegroundColor Green
    $inv = @(& $Psql -h 127.0.0.1 -p 5432 -U ppiq_dev -d $TargetDb -w -X -A -t -F ' | ' -c "SELECT d.dashboard_code, COUNT(w.id) FROM dashboard_definitions d LEFT JOIN dashboard_widget_definitions w ON w.dashboard_definition_id = d.id AND w.is_deleted = FALSE WHERE d.is_deleted = FALSE GROUP BY 1 ORDER BY 1;" 2>&1)
    @($inv) | Where-Object { $_ } | ForEach-Object { Write-Host ("    " + $_) }
} else {
    Write-Host "[A] WIDGET INSERT FAILED (workspace wiring continues regardless):" -ForegroundColor Red
    @($o | Select-Object -First 4) | ForEach-Object { Write-Host ("    " + $_) }
}

# =====================  PART B : CONVERGENT WIRING  =====================
Write-Host ""
Write-Host "[B] drift-aware workspace wiring:"

$pageExists = Test-Path $NewPage
if ($pageExists) {
    Write-Host "    InteractiveWorkspacePage.tsx ALREADY EXISTS (parallel session's version KEPT, not overwritten)."
} else {
    Write-Host "    InteractiveWorkspacePage.tsx missing - a parallel session wired the App import"
    Write-Host "    without the page, or under a different path. Checking App.tsx for its import path..."
}

$app = [System.IO.File]::ReadAllText($AppTsx, [System.Text.Encoding]::UTF8)
$hasImport = $app.Contains('RoutedInteractiveWorkspacePage')
$hasRoute = ($app -match 'path="/workspace/:dashboardCode"' -or $app -match 'path="/workspace/')
Write-Host ("    App.tsx import present: " + $hasImport + "   route present: " + $hasRoute)

if ($hasImport) {
    $m = [regex]::Match($app, 'import\s*\{[^}]*RoutedInteractiveWorkspacePage[^}]*\}\s*from\s*"([^"]+)"')
    if ($m.Success) {
        Write-Host ("    existing import path: " + $m.Groups[1].Value)
        $resolved = Join-Path $Web ('src\' + ($m.Groups[1].Value -replace '^\./', '' -replace '^@/', '' -replace '/', '\') + '.tsx')
        Write-Host ("    resolves to: " + $resolved + "  exists: " + (Test-Path $resolved))
    }
}

$shimText = [System.IO.File]::ReadAllText($Shim, [System.Text.Encoding]::UTF8)
$shimPointed = $shimText.Contains('InteractiveWorkspacePage')
Write-Host ("    shim already repointed: " + $shimPointed)

# Apply only missing pieces
if (-not $pageExists -and -not $hasImport) {
    Write-Host "    -> nothing exists; this situation contradicts your tsc output. ABORTING for a manual look." -ForegroundColor Red
    Write-Host "       Paste: findstr /n RoutedInteractiveWorkspacePage Frontend\PlantProcess.Web\src\App.tsx"
    exit 1
}

$gateNeeded = $false
if (-not $hasRoute -and $hasImport) {
    $routeAnchor = '<Route path="/data-integration" element={<DataIntegrationLayout />}>'
    $count = 0; $idx = 0
    while (($idx = $app.IndexOf($routeAnchor, $idx, [System.StringComparison]::Ordinal)) -ge 0) { $count++; $idx += $routeAnchor.Length }
    if ($count -eq 1) {
        Backup-And-Track $AppTsx
        $app = $app.Replace($routeAnchor, ('<Route path="/workspace/:dashboardCode" element={<RoutedInteractiveWorkspacePage />} />' + "`r`n                    " + $routeAnchor))
        [System.IO.File]::WriteAllText($AppTsx, $app, (New-Object System.Text.UTF8Encoding($false)))
        Write-Host "    APPLIED /workspace/:dashboardCode route" -ForegroundColor Green
        $gateNeeded = $true
    } else {
        Write-Host ("    route anchor count=" + $count + " - add the route manually inside <Routes>.") -ForegroundColor Yellow
    }
}
if (-not $shimPointed -and ($pageExists -or $hasImport)) {
    Backup-And-Track $Shim
    $shimSource = 'import { InteractiveWorkspacePage } from "./InteractiveWorkspacePage";' + "`r`n`r`n" + 'export function DashboardPageContent() {' + "`r`n" + '  return <InteractiveWorkspacePage dashboardCode="PRODUCTION_OVERVIEW" />;' + "`r`n" + '}' + "`r`n"
    [System.IO.File]::WriteAllText($Shim, $shimSource, (New-Object System.Text.UTF8Encoding($false)))
    Write-Host "    REPOINTED /dashboard shim -> InteractiveWorkspacePage(PRODUCTION_OVERVIEW)" -ForegroundColor Green
    $gateNeeded = $true
}
if (-not $gateNeeded) {
    Write-Host "    nothing to change - the workspace is already fully wired by the parallel session."
}

if ($gateNeeded -and -not $SkipGate) {
    Write-Host "[GATE] npx tsc -b ..."
    Push-Location $Web
    try { & npx tsc -b 2>&1 | Select-Object -Last 12 | ForEach-Object { Write-Host ("    " + $_) }; $code = $LASTEXITCODE } finally { Pop-Location }
    if ($code -ne 0) {
        Write-Host "[GATE RED] reverting this run's changes only." -ForegroundColor Red
        Restore-Modified
        exit 1
    }
    Write-Host "    tsc -b green." -ForegroundColor Green
}

Write-Host ""
Write-Host ("[DONE] Backup: " + $BackupDir)
Write-Host "OPEN:  /dashboard   /workspace/CORRELATION_FINDINGS_BOARD   /workspace/RISK_INTELLIGENCE"
exit 0
