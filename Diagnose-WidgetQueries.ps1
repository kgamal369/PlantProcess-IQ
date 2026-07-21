<#
.SYNOPSIS
    Diagnose-WidgetQueries.ps1 - names WHY every widget on a workspace renders
    empty. Logs in, loads the dashboard definition + its widgets, replays each
    widget's /analytics/dashboard/widgets/query call, and prints the server's
    validation errors VERBATIM per widget. Read-only.

.DESCRIPTION
    The UI toast ("Dashboard widget query is invalid.") hides the errors array.
    The validator can emit: Unsupported widget/chart type, Unsupported/missing
    measure code, Unsupported/missing dimension code, measure-requires-parameter,
    chart-measure incompatibility, bad time range. Whichever it is, it is IN the
    400 body - this prints it, per widget, so the fix is named, not guessed.

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Diagnose-WidgetQueries.ps1

.EXAMPLE
    powershell -NoProfile -ExecutionPolicy Bypass -File .\Diagnose-WidgetQueries.ps1 -DashboardCode COMMAND_DASHBOARD
#>

[CmdletBinding()]
param(
    [string]$ApiBase       = 'http://localhost:5063',
    [string]$DashboardCode = 'PRODUCTION_OVERVIEW',
    [string]$User          = 'e2eadmin',
    [string]$Password      = 'E2EAdmin123!',
    [string]$RepoRoot      = (Get-Location).Path,
    [switch]$Repair
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version Latest
$stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$logPath = Join-Path $RepoRoot ("WidgetQueries_" + $DashboardCode + "_" + $stamp + ".txt")
$lines = New-Object System.Collections.Generic.List[string]
$utf8 = New-Object System.Text.UTF8Encoding($false)
function W([string]$t=''){ $lines.Add($t); Write-Host $t }
function Save { [System.IO.File]::WriteAllText($logPath, (($lines -join "`r`n")+"`r`n"), $utf8); Write-Host ''; Write-Host ('Log: '+$logPath) -ForegroundColor Cyan }

W '=============================================================================='
W ('WIDGET QUERY DIAGNOSIS - ' + (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'))
W ('Dashboard: ' + $DashboardCode + '   API: ' + $ApiBase)
W '=============================================================================='
W ''

# ---- auth -------------------------------------------------------------------

$token = $null
$authTried = New-Object System.Collections.Generic.List[string]
foreach ($u in @('/api/auth/login', '/auth/login', '/api/v1/auth/login')) {
    foreach ($shape in @(@{ username = $User; password = $Password }, @{ email = $User; password = $Password }, @{ userName = $User; password = $Password })) {
        if ($token) { break }
        try {
            $b = $shape | ConvertTo-Json
            $r = Invoke-RestMethod -Uri ($ApiBase + $u) -Method Post -Body $b -ContentType 'application/json' -TimeoutSec 15 -ErrorAction Stop
            foreach ($k in @('accessToken', 'token', 'access_token', 'jwt')) {
                if ($r.PSObject.Properties[$k] -and $r.$k) { $token = $r.$k; break }
            }
            if ($token) { W ('[AUTH] via ' + $u + ' field=' + (($shape.Keys | Select-Object -First 1))) }
        } catch {
            $code = ''
            if ($_.Exception.Response) { try { $code = [int]$_.Exception.Response.StatusCode } catch { } }
            $authTried.Add(($u + ' [' + (($shape.Keys | Select-Object -First 1)) + '] -> ' + $code))
        }
    }
    if ($token) { break }
}
if (-not $token) { foreach ($a in $authTried) { W ('    tried: ' + $a) } }
if (-not $token) { W '[ABORT] cannot authenticate.'; Save; exit 1 }
$H = @{ Authorization = 'Bearer ' + $token }
W '[AUTH] OK'
W ''

if ($Repair) {
    W '[REPAIR] invoking the product''s own system-template repair'
    foreach ($ru in @('/analytics/dashboard/definitions/system-templates/ensure',
                      '/analytics/dashboard/definitions/system-templates/repair')) {
        try {
            $rr = Invoke-RestMethod -Uri ($ApiBase + $ru) -Method Post -Headers $H -ContentType 'application/json' -Body '{}' -TimeoutSec 60 -ErrorAction Stop
            W ('    POST ' + $ru + '  -> OK ' + (($rr | ConvertTo-Json -Depth 3 -Compress).Substring(0, [Math]::Min(200, ($rr | ConvertTo-Json -Depth 3 -Compress).Length))))
        } catch {
            $code = ''
            if ($_.Exception.Response) { try { $code = [int]$_.Exception.Response.StatusCode } catch { } }
            W ('    POST ' + $ru + '  -> FAILED ' + $code + ' ' + $_.Exception.Message)
        }
    }
    W ''
}

# ---- load dashboard + widgets ----------------------------------------------

$dash = $null
foreach ($u in @('/analytics/dashboard/definitions', '/api/analytics/dashboard/definitions')) {
    try {
        $list = Invoke-RestMethod -Uri ($ApiBase + $u) -Headers $H -TimeoutSec 20 -ErrorAction Stop
        $items = $list
        foreach ($key in @('items','definitions','dashboards','results','value','data')) {
            if ($list -isnot [array] -and $list.PSObject.Properties[$key]) { $items = $list.$key; break }
        }
        if ($items -isnot [array]) {
            foreach ($pp in $list.PSObject.Properties) {
                if ($pp.Value -is [array] -and @($pp.Value).Count -gt 0) { $items = $pp.Value; break }
            }
        }
        $dash = @($items) | Where-Object {
            ("$($_.dashboardCode)" -eq $DashboardCode) -or ("$($_.dashboard_code)" -eq $DashboardCode) -or ("$($_.code)" -eq $DashboardCode)
        } | Select-Object -First 1
        if ($dash) { W ('[DASHBOARD] found via ' + $u); break }
    } catch { }
}
if (-not $dash) { W ('[ABORT] dashboard ' + $DashboardCode + ' not found via known list endpoints.'); Save; exit 1 }

$widgets = @()
foreach ($p in @('widgets', 'widgetDefinitions', 'dashboardWidgets')) {
    if ($dash.PSObject.Properties[$p]) { $widgets = @($dash.$p); break }
}
if (@($widgets).Count -eq 0) {
    $dashId = $dash.id
    foreach ($u in @("/analytics/dashboard/definitions/$dashId", "/api/analytics/dashboard/definitions/$dashId")) {
        try {
            $full = Invoke-RestMethod -Uri ($ApiBase + $u) -Headers $H -TimeoutSec 20 -ErrorAction Stop
            foreach ($p in @('widgets', 'widgetDefinitions')) {
                if ($full.PSObject.Properties[$p]) { $widgets = @($full.$p); break }
            }
            if (@($widgets).Count -gt 0) { break }
        } catch { }
    }
}
W ('[WIDGETS] ' + @($widgets).Count + ' definitions on ' + $DashboardCode)
W ''

if (@($widgets).Count -eq 0) { W 'No widget definitions - the emptiness is definitional, not a query failure.'; Save; exit 1 }

# ---- replay each widget query ----------------------------------------------

$failCount = 0
$okCount = 0
$errorTally = @{}
foreach ($wd in $widgets) {
    $name = "$($wd.title)"
    if (-not $name) { $name = "$($wd.name)" }
    if (-not $name) { $name = "$($wd.widgetCode)" }
    $payload = @{}
    foreach ($k in @('widgetType', 'chartType', 'dimensionCode', 'measureCode', 'parameterCode', 'maxRows')) {
        if ($wd.PSObject.Properties[$k] -and $null -ne $wd.$k) { $payload[$k] = $wd.$k }
    }
    if ($wd.PSObject.Properties['query'] -and $wd.query) {
        foreach ($p in $wd.query.PSObject.Properties) { $payload[$p.Name] = $p.Value }
    }
    $body = $payload | ConvertTo-Json -Depth 6
    W ('  WIDGET: ' + $name)
    W ('    sent: ' + ($body -replace '\s+', ' '))
    try {
        $resp = Invoke-RestMethod -Uri ($ApiBase + '/analytics/dashboard/widgets/query') -Method Post -Headers $H -ContentType 'application/json' -Body $body -TimeoutSec 30 -ErrorAction Stop
        $rows = 0
        foreach ($k in @('rows', 'data', 'points', 'items')) {
            if ($resp.PSObject.Properties[$k]) { $rows = @($resp.$k).Count; break }
        }
        W ('    OK - rows returned: ' + $rows)
        $okCount++
    } catch {
        $failCount++
        $status = ''
        $bodyText = ''
        if ($_.Exception.Response) {
            try { $status = [int]$_.Exception.Response.StatusCode } catch { }
            try {
                $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $bodyText = $sr.ReadToEnd()
            } catch { }
        }
        W ('    FAIL ' + $status)
        if ($bodyText) {
            try {
                $j = $bodyText | ConvertFrom-Json
                $errObj = $j
                if ($j.PSObject.Properties['errors']) { $errObj = $j.errors }
                $flat = ($errObj | ConvertTo-Json -Depth 6 -Compress)
                W ('    server said: ' + $flat)
                foreach ($m in ([regex]::Matches($flat, '"([^"]{10,120})"'))) {
                    $msg = $m.Groups[1].Value
                    if ($msg -match 'Unsupported|required|not compatible|must be') {
                        if (-not $errorTally.ContainsKey($msg)) { $errorTally[$msg] = 0 }
                        $errorTally[$msg]++
                    }
                }
            } catch { W ('    server said (raw): ' + $bodyText.Substring(0, [Math]::Min(400, $bodyText.Length))) }
        }
    }
    W ''
}

W '=============================================================================='
W ('SUMMARY: ' + $okCount + ' OK, ' + $failCount + ' failing')
if ($errorTally.Count -gt 0) {
    W 'Distinct validation errors across widgets:'
    foreach ($k in ($errorTally.Keys | Sort-Object)) { W ('  ' + $errorTally[$k] + 'x  ' + $k) }
    W ''
    W 'If one error repeats across all widgets, the fix is ONE registry/definition'
    W 'change, not eight. Send this log.'
}
W '=============================================================================='
Save
if ($failCount -eq 0) { exit 0 } else { exit 1 }
