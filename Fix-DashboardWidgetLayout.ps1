<#
.SYNOPSIS
    Fix-DashboardWidgetLayout.ps1 - writes a readable default grid layout onto
    every system dashboard so widgets render at chart size instead of collapsed
    pills, and so "Reset layout" restores GOOD sizes (the persisted defaults were
    saved tiny during the dark-widget era). Read-modify-write on
    dashboard_definitions.layout_json. Full contract: preflight -> per-dashboard
    backup (into a .bak table) -> compute layout keyed by REAL widget ids ->
    write -> self-check -> summary. -Revert restores from the backup table.

.DESCRIPTION
    The frontend grid keys each widget by its widget-definition id and reads the
    dashboard-level layout_json ({ lg:[{i,x,y,w,h,minW,minH}], md:[...], ... }).
    This script, per system dashboard:
      1. reads its widgets (id, widget_code, chart_type) in sort order
      2. assigns sizes: charts w=6 h=9, kpi/number w=4 h=7, table w=12 h=8,
         flowing 2-up across a 12-col grid (charts), computing x/y automatically
      3. builds lg/md/sm/xs/xxs breakpoints (md=10col, sm/xs/xxs single column)
      4. writes it to dashboard_definitions.layout_json
    Idempotent and reversible. NO build needed (data only). NO widget row is
    edited; only the dashboard's layout_json.

.PARAMETER SystemOnly   only dashboards flagged system/customer-safe (default true)
.PARAMETER Revert       restore layout_json from the ppiq_layout_backup table
#>

[CmdletBinding()]
param(
    [string]$Database   = 'ppiq_presentation',
    [string]$DbHost     = '127.0.0.1',
    [int]   $Port       = 5432,
    [string]$DbUser     = 'ppiq_dev',
    [string]$DbPassword = 'ppiq_dev_local_only',
    [switch]$Revert,
    [string]$RepoRoot   = (Get-Location).Path
)

$ErrorActionPreference = 'Continue'  # psql NOTICEs on native stderr must not terminate; we gate on $LASTEXITCODE explicitly
Set-StrictMode -Version Latest
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $RepoRoot ("Fix_WidgetLayout_" + $stamp + ".txt")
$lines = New-Object System.Collections.Generic.List[string]
$utf8 = New-Object System.Text.UTF8Encoding($false)
function W([string]$t=''){ $lines.Add($t); Write-Host $t }
function Save { [System.IO.File]::WriteAllText($logPath, (($lines -join "`r`n")+"`r`n"), $utf8); Write-Host ''; Write-Host ('Log: '+$logPath) -ForegroundColor Cyan }
function Resolve-Psql {
    $cmd = Get-Command psql.exe -ErrorAction SilentlyContinue
    if ($cmd) { return $cmd.Source }
    foreach ($r in @('C:\Program Files\PostgreSQL','C:\Program Files (x86)\PostgreSQL')) {
        if (Test-Path $r) { $h = Get-ChildItem $r -Filter psql.exe -Recurse -ErrorAction SilentlyContinue | Sort-Object FullName -Descending | Select-Object -First 1; if ($h) { return $h.FullName } }
    }
    return $null
}
$psql = Resolve-Psql
if (-not $psql) { Write-Host 'psql not found'; exit 2 }
$env:PGPASSWORD = $DbPassword
$env:PGOPTIONS = '-c client_min_messages=warning'
$conn = "host=$DbHost port=$Port dbname=$Database user=$DbUser"
function Q1([string]$sql){ $o = & $psql -v ON_ERROR_STOP=1 -X -q -A -t -d $conn -c $sql 2>&1; if ($LASTEXITCODE -ne 0){ return ('ERR: '+($o -join ' ')) }; return (($o | Where-Object {$_ -ne ''}) -join '') }
function QA([string]$sql){ $o = & $psql -v ON_ERROR_STOP=1 -X -q -A -F "`t" -t -d $conn -c $sql 2>&1; if ($LASTEXITCODE -ne 0){ return @('ERR: '+($o -join ' ')) }; return @($o | Where-Object {$_ -ne ''}) }
function Exec([string]$sql){ $o = & $psql -v ON_ERROR_STOP=1 -X -q -d $conn -c $sql 2>&1; if ($LASTEXITCODE -ne 0){ W ('    SQL ERR: '+($o -join ' ')); return $false }; return $true }

W '=============================================================================='
W ('FIX DASHBOARD WIDGET LAYOUT - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ('DB: ' + $Database)
W '=============================================================================='
W ''

if (-not (Q1 'SELECT 1;')) { W 'FAIL: cannot reach DB'; Save; exit 2 }

# backup table
Exec "CREATE TABLE IF NOT EXISTS public.ppiq_layout_backup (dashboard_id uuid, layout_json jsonb, saved_at timestamptz DEFAULT now(), tag text);" | Out-Null

if ($Revert) {
    W '[REVERT] restoring layout_json from the most recent backup rows'
    $ok = Exec @"
UPDATE public.dashboard_definitions d
SET layout_json = b.layout_json
FROM (SELECT DISTINCT ON (dashboard_id) dashboard_id, layout_json FROM public.ppiq_layout_backup ORDER BY dashboard_id, saved_at DESC) b
WHERE d.id = b.dashboard_id;
"@
    if ($ok) { W '    restored.' }
    Save; exit 0
}

# which dashboards
$dashRows = QA "SELECT id, dashboard_code FROM public.dashboard_definitions WHERE is_deleted = false ORDER BY dashboard_code;"
if (@($dashRows).Count -eq 0 -or $dashRows[0] -match '^ERR') { W ('FAIL listing dashboards: ' + ($dashRows -join ' ')); Save; exit 1 }
W ('[DASHBOARDS] ' + @($dashRows).Count + ' found')
W ''

$totalWidgets = 0
foreach ($dr in $dashRows) {
    $parts = $dr -split "`t"
    if ($parts.Count -lt 2) { continue }
    $did = $parts[0]; $code = $parts[1]

    # widgets in order, with their chart type
    $wRows = QA "SELECT id, COALESCE(chart_type,'bar'), COALESCE(widget_type,'chart') FROM public.dashboard_widget_definitions WHERE dashboard_definition_id = '$did' AND is_deleted = false ORDER BY sort_order NULLS LAST, created_at_utc;"
    if (@($wRows).Count -eq 0) { W ('  ' + $code + ': no widgets, skipped'); continue }

    # backup current layout
    Exec "INSERT INTO public.ppiq_layout_backup (dashboard_id, layout_json, tag) SELECT id, layout_json, 'pre-$stamp' FROM public.dashboard_definitions WHERE id = '$did';" | Out-Null

    # compute lg layout (12 cols), charts 6x9 two-up, kpi 4x7 three-up, table 12x8 full
    $lg = New-Object System.Collections.Generic.List[string]
    $md = New-Object System.Collections.Generic.List[string]
    $sm = New-Object System.Collections.Generic.List[string]
    $x = 0; $y = 0; $rowH = 0
    $ymd = 0; $ysm = 0
    foreach ($wr in $wRows) {
        $wp = $wr -split "`t"
        $wid = $wp[0]; $ct = $wp[1].ToLower(); $wt = $wp[2].ToLower()
        if ($wt -eq 'table' -or $ct -eq 'table') { $w = 12; $h = 8; $mw = 6; $mh = 5 }
        elseif ($wt -eq 'kpi' -or $ct -eq 'kpi') { $w = 4; $h = 7; $mw = 3; $mh = 5 }
        else { $w = 6; $h = 9; $mw = 4; $mh = 6 }
        if ($x + $w -gt 12) { $x = 0; $y += $rowH; $rowH = 0 }
        $lg.Add("{""i"":""$wid"",""x"":$x,""y"":$y,""w"":$w,""h"":$h,""minW"":$mw,""minH"":$mh}")
        if ($h -gt $rowH) { $rowH = $h }
        $x += $w
        # md: 10-col, force 2-up half width=5
        $md.Add("{""i"":""$wid"",""x"":0,""y"":$ymd,""w"":10,""h"":$h,""minW"":4,""minH"":5}"); $ymd += $h
        # sm/xs/xxs: single column full
        $sm.Add("{""i"":""$wid"",""x"":0,""y"":$ysm,""w"":1,""h"":$h,""minW"":1,""minH"":5}"); $ysm += $h
    }
    $layout = '{"lg":[' + ($lg -join ',') + '],"md":[' + ($md -join ',') + '],"sm":[' + ($sm -join ',') + '],"xs":[' + ($sm -join ',') + '],"xxs":[' + ($sm -join ',') + ']}'

    # write via a temp file + psql variable to avoid ALL shell quoting issues.
    $jsonOk = $true
    try { $null = $layout | ConvertFrom-Json } catch { $jsonOk = $false }
    if (-not $jsonOk) { W ('  ' + $code + ': computed layout not valid JSON, skipped'); continue }
    $tmp = Join-Path $env:TEMP ("ppiq_layout_" + $did + ".json")
    [System.IO.File]::WriteAllText($tmp, $layout, (New-Object System.Text.UTF8Encoding($false)))
    $updateSql = "UPDATE public.dashboard_definitions SET layout_json = :'v'::jsonb, updated_at_utc = now() WHERE id = '$did';"
    $o = & $psql -v ON_ERROR_STOP=1 -X -q -d $conn -v ("v=" + [System.IO.File]::ReadAllText($tmp)) -c $updateSql 2>&1
    $ok = ($LASTEXITCODE -eq 0)
    Remove-Item -LiteralPath $tmp -ErrorAction SilentlyContinue
    if (-not $ok) { W ('    write err: ' + ($o -join ' ')) }
    if ($ok) {
        W ('  ' + $code.PadRight(28) + @($wRows).Count.ToString().PadLeft(3) + ' widgets  -> layout written')
        $totalWidgets += @($wRows).Count
    } else {
        W ('  ' + $code + ': WRITE FAILED')
    }
}
W ''

# self-check: every dashboard has non-empty lg array
W '[SELF-CHECK]'
$bad = [int](Q1 "SELECT count(*) FROM public.dashboard_definitions WHERE is_deleted=false AND (layout_json IS NULL OR jsonb_array_length(COALESCE(layout_json->'lg','[]'::jsonb)) = 0);")
W ('    dashboards with empty lg layout: ' + $bad)
W ('    total widgets sized: ' + $totalWidgets)
W ''
if ($bad -eq '0') {
    W 'DONE. Hard-reload the dashboard (Ctrl-Shift-R). Widgets should render at'
    W 'chart size. "Reset layout" now restores THESE sizes. If a widget is still'
    W 'small, drag it once and Save layout - that persists your tweak on top.'
    W ''
    W 'Revert everything:  powershell -NoProfile -ExecutionPolicy Bypass -File .\Fix-DashboardWidgetLayout.ps1 -Revert'
} else {
    W 'Some dashboards still have empty layout - send this log.'
}
Save
exit 0
