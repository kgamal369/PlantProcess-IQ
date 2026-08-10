#requires -Version 5.1
<#
  PPIQ T-044 - certification measurement, version 2. READ ONLY.

  WHY THERE IS A VERSION 2. Version 1 read only the raw dimension-key column and
  reported nine GUIDs on EO_TABLE as a label failure. It was wrong. BuildResult
  returns TWO category columns: the dimension code carrying the raw key, and
  "dimensionLabel" carrying the resolved name that LoadDimensionLabelsAsync
  fetched from the database. The instrument, not the product, was at fault, and
  a corrected instrument is the first thing this file is.

  WHAT IT NOW SEPARATES, because these three were being read as one thing:
    - raw key "unknown" with a fallback label ("No equipment"): the fact carries
      NO attribution. A null, not a category.
    - a label that is itself a GUID: label resolution genuinely failed.
    - a source category actually named unknown: real data, and legitimate.

  STABILITY. Every widget's IDENTICAL request is issued five times. Nothing
  sleeps, nothing retries, nothing widens an assertion. Three fingerprints per
  run separate the three ways a result can move:
    - raw order        : row order as returned
    - key set          : sorted dimension keys
    - key and value    : sorted key=value pairs
  Sorted fingerprints equal while raw order differs is ordering instability
  only. Key set equal while key+value differs is value instability. Key set
  differing is population instability, which against an unchanged dataset is a
  data-truth defect and is reported as one.

  Usage:
    $body  = @{ userName = "e2eadmin"; password = "E2EAdmin123!" } | ConvertTo-Json
    $login = Invoke-RestMethod -Method Post -Uri "http://localhost:5063/auth/login" -ContentType "application/json" -Body $body
    .\Measure-T044-v2.ps1 -Token $login.accessToken -DashboardCodes PRODUCTION_OVERVIEW,QUALITY_MONITORING,EQUIPMENT_OPERATIONS
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Token,

    [string]$BaseUrl = "http://localhost:5063",

    [Parameter(Mandatory = $true)]
    [string[]]$DashboardCodes,

    [int]$Runs = 5
)

$ErrorActionPreference = 'Stop'

$headers = @{ Authorization = "Bearer $Token" }

function Say([string]$text) { Write-Host $text }
function Rule() { Write-Host ("-" * 100) }

function Get-Json([string]$path) {
    return Invoke-RestMethod -Uri ($BaseUrl + $path) -Headers $headers
}

function Post-Json([string]$path, $bodyObject) {
    $json = $bodyObject | ConvertTo-Json -Depth 10 -Compress
    return Invoke-RestMethod -Method Post -Uri ($BaseUrl + $path) -Headers $headers -ContentType "application/json" -Body $json
}

function Fingerprint([string]$text) {
    $sha = [System.Security.Cryptography.SHA256]::Create()
    $bytes = [System.Text.Encoding]::UTF8.GetBytes($text)
    $hash = $sha.ComputeHash($bytes)
    $sha.Dispose()
    return (($hash | ForEach-Object { $_.ToString("x2") }) -join "").Substring(0, 10)
}

$guidPattern = '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'

Say ''
Say '===================================================================================================='
Say ' T-044 CERTIFICATION MEASUREMENT v2 (read only, five identical runs per widget)'
Say '===================================================================================================='

$meta = Get-Json "/analytics/dashboard/metadata"
$dimensionLabels = @{}
foreach ($d in $meta.dimensions) { $dimensionLabels[$d.code] = $d.label }

Say ''
Say ('  metadata generated at : ' + $meta.generatedAtUtc)
Say ('  runs per widget       : ' + $Runs)
Say ('  NOTE: the request carries the widget binding only. No page-level selection is applied,')
Say ('        so every run is the same request by construction.')

$definitions = Get-Json "/analytics/dashboard/definitions"
if ($definitions.PSObject.Properties.Name -contains 'items') { $definitions = $definitions.items }

$findings = New-Object System.Collections.Generic.List[object]
$stability = New-Object System.Collections.Generic.List[object]

foreach ($code in $DashboardCodes) {
    $dashboard = $definitions | Where-Object { $_.dashboardCode -eq $code } | Select-Object -First 1
    if (-not $dashboard) {
        Say ''
        Say ("  NOT FOUND: no dashboard with code " + $code)
        continue
    }

    Say ''
    Rule
    Say (' ' + $dashboard.name + '   [' + $dashboard.dashboardCode + ']')
    Rule

    $full = Get-Json ("/analytics/dashboard/definitions/" + $dashboard.id)
    $widgets = @($full.widgets) | Where-Object { $_.isActive -ne $false }

    foreach ($w in $widgets) {

        $request = @{
            widgetType    = $w.widgetType
            chartType     = $w.chartType
            dimensionCode = $w.dimensionCode
            measureCode   = $w.measureCode
            parameterCode = $w.parameterCode
        }
        $payloadText = ($request | ConvertTo-Json -Depth 10 -Compress)

        Say ''
        Say (' WIDGET ' + $w.widgetCode + '   "' + $w.widgetTitle + '"')
        Say ('   saved filterJson : ' + $w.filterJson)
        Say ('   request payload  : ' + $payloadText)

        $runRows = New-Object System.Collections.Generic.List[object]
        $firstResult = $null
        $queryError = ""

        for ($i = 1; $i -le $Runs; $i++) {
            try {
                $result = Post-Json "/analytics/dashboard/widgets/query" $request
            }
            catch {
                $queryError = $_.Exception.Message
                break
            }

            if ($i -eq 1) { $firstResult = $result }

            $dimCode = $result.widget.dimensionCode
            if (-not $dimCode) { $dimCode = "kpi" }

            $keys = New-Object System.Collections.Generic.List[string]
            $pairs = New-Object System.Collections.Generic.List[string]
            $rawOrder = New-Object System.Collections.Generic.List[string]

            foreach ($row in $result.rows) {
                $k = ""
                if ($row.PSObject.Properties.Name -contains $dimCode) { $k = [string]$row.$dimCode }
                $v = ""
                if ($row.PSObject.Properties.Name -contains "value") { $v = [string]$row.value }
                $keys.Add($k)
                $pairs.Add($k + "=" + $v)
                $rawOrder.Add($k + "=" + $v)
            }

            $keyFp = Fingerprint (($keys | Sort-Object) -join "|")
            $valFp = Fingerprint (($pairs | Sort-Object) -join "|")
            $ordFp = Fingerprint (($rawOrder) -join "|")

            $runRows.Add([PSCustomObject]@{
                Run       = $i
                Rows      = @($result.rows).Count
                MaxRows   = $result.widget.maxRows
                RawLimit  = $result.widget.rawRowLimit
                Sort      = $result.widget.sortDirection
                FromUtc   = $result.widget.fromUtc
                ToUtc     = $result.widget.toUtc
                Generated = $result.generatedAtUtc
                KeyFp     = $keyFp
                ValFp     = $valFp
                OrderFp   = $ordFp
            })
        }

        if ($queryError -ne "") {
            Say ('   QUERY FAILED    : ' + $queryError)
            $findings.Add([PSCustomObject]@{
                Widget = $w.widgetCode; Chart = $w.chartType; Dimension = $w.dimensionCode
                Rows = 0; Usable = 0; LabelState = "n/a"; Sample = ""; Verdict = "FAIL query did not execute: " + $queryError
            })
            continue
        }

        $runRows | Format-Table Run, Rows, MaxRows, Sort, FromUtc, ToUtc, KeyFp, ValFp, OrderFp -AutoSize |
            Out-String -Width 200 | Write-Host

        $distinctKeyFp = @($runRows | Select-Object -ExpandProperty KeyFp -Unique).Count
        $distinctValFp = @($runRows | Select-Object -ExpandProperty ValFp -Unique).Count
        $distinctOrdFp = @($runRows | Select-Object -ExpandProperty OrderFp -Unique).Count
        $distinctWindow = @($runRows | ForEach-Object { [string]$_.FromUtc + ".." + [string]$_.ToUtc } | Select-Object -Unique).Count

        $class = "DETERMINISTIC"
        if ($distinctKeyFp -gt 1) {
            $class = "POPULATION INSTABILITY"
        } elseif ($distinctValFp -gt 1) {
            $class = "VALUE INSTABILITY"
        } elseif ($distinctOrdFp -gt 1) {
            $class = "ORDERING ONLY"
        }

        $windowNote = ""
        if ($distinctWindow -gt 1) { $windowNote = " (the resolved window MOVED between runs)" }

        Say ('   stability        : ' + $class + $windowNote)

        $stability.Add([PSCustomObject]@{
            Widget    = $w.widgetCode
            Class     = $class
            RowCounts = (($runRows | ForEach-Object { $_.Rows }) -join ",")
            KeyFps    = $distinctKeyFp
            ValFps    = $distinctValFp
            OrderFps  = $distinctOrdFp
            Windows   = $distinctWindow
        })

        # -------------------------------------------------------------------
        # Label and category analysis, from run 1, reading BOTH columns.
        # -------------------------------------------------------------------
        $dimCode = $firstResult.widget.dimensionCode
        if (-not $dimCode) { $dimCode = "kpi" }

        $pairsSeen = @{}
        foreach ($row in $firstResult.rows) {
            $k = ""
            if ($row.PSObject.Properties.Name -contains $dimCode) { $k = ([string]$row.$dimCode).Trim() }
            $l = ""
            if ($row.PSObject.Properties.Name -contains "dimensionLabel") { $l = ([string]$row.dimensionLabel).Trim() }
            if (-not $pairsSeen.ContainsKey($k)) { $pairsSeen[$k] = $l }
        }

        $labelGuidCount = 0
        $nullAttributionCount = 0
        $genuineUnknownCount = 0
        $usable = 0

        foreach ($k in $pairsSeen.Keys) {
            $l = [string]$pairsSeen[$k]

            if ($l -match $guidPattern) { $labelGuidCount = $labelGuidCount + 1 }

            if ($k -eq "unknown") {
                if ($l -eq "unknown" -or $l -eq "Unknown") {
                    $genuineUnknownCount = $genuineUnknownCount + 1
                } else {
                    $nullAttributionCount = $nullAttributionCount + 1
                }
            } else {
                $usable = $usable + 1
            }
        }

        $labelState = "resolved"
        if ($labelGuidCount -gt 0) { $labelState = "GUID LABEL x" + $labelGuidCount } elseif ($nullAttributionCount -gt 0) { $labelState = "NULL ATTRIBUTION" } elseif ($genuineUnknownCount -gt 0) { $labelState = "source category 'unknown'" }

        $sampleParts = New-Object System.Collections.Generic.List[string]
        foreach ($k in (@($pairsSeen.Keys) | Sort-Object | Select-Object -First 3)) {
            $sampleParts.Add($k + " -> " + [string]$pairsSeen[$k])
        }
        $sample = ($sampleParts -join " ; ")
        if ($sample.Length -gt 70) { $sample = $sample.Substring(0, 67) + "..." }

        Say ('   labels           : ' + $labelState + '   |  ' + $sample)

        # -------------------------------------------------------------------
        # Verdict
        # -------------------------------------------------------------------
        $verdicts = New-Object System.Collections.Generic.List[string]
        $chart = ([string]$w.chartType).ToLower()
        $titleLower = ([string]$w.widgetTitle).ToLower()
        $rowCount = $runRows[0].Rows

        if ($class -eq "POPULATION INSTABILITY") { $verdicts.Add("FAIL identical requests returned different populations") }
        if ($class -eq "VALUE INSTABILITY") { $verdicts.Add("FAIL identical requests returned different values for the same categories") }
        if ($class -eq "ORDERING ONLY") { $verdicts.Add("ADVISORY row order varies between identical runs") }

        if ($rowCount -eq 0) { $verdicts.Add("FAIL returns no rows") }
        if ($labelGuidCount -gt 0) { $verdicts.Add("FAIL " + $labelGuidCount + " resolved label(s) are raw identifiers") }

        if ($nullAttributionCount -gt 0 -and $usable -eq 0) {
            $verdicts.Add("FAIL every fact lacks attribution for this dimension; the chart plots a null")
        } elseif ($nullAttributionCount -gt 0) {
            $verdicts.Add("ADVISORY " + $nullAttributionCount + " unattributed bucket present beside " + $usable + " real categor(y/ies)")
        }

        if (($chart -eq "pie" -or $chart -eq "donut") -and $usable -le 1) { $verdicts.Add("FAIL single effective category on a " + $chart) }
        if ($chart -eq "bar" -and $usable -le 1) { $verdicts.Add("FAIL single effective category on a bar: a binding defect, not a data shortage") }
        if ($chart -eq "heatmap" -and $usable -le 1) { $verdicts.Add("FAIL heatmap with only one meaningful axis") }
        if ($chart -eq "kpi" -and $w.dimensionCode) { $verdicts.Add("ADVISORY kpi persists a dimension; the registry says kpi does not support one") }
        if (($chart -eq "line" -or $chart -eq "area") -and $usable -le 2) { $verdicts.Add("ADVISORY " + $chart + " over " + $usable + " point(s)") }
        if (($titleLower -match "trend|over time|by day|by hour|history") -and ($chart -ne "line" -and $chart -ne "area")) { $verdicts.Add("ADVISORY title claims a trend, chart type is " + $chart) }

        if ($firstResult.warnings -and @($firstResult.warnings).Count -gt 0) { $verdicts.Add("WARN " + (@($firstResult.warnings) -join "; ")) }

        if ($verdicts.Count -eq 0) { $verdicts.Add("PASS") }

        $findings.Add([PSCustomObject]@{
            Widget     = $w.widgetCode
            Chart      = $chart
            Dimension  = $w.dimensionCode
            Rows       = $rowCount
            Usable     = $usable
            LabelState = $labelState
            Stability  = $class
            Sample     = $sample
            Verdict    = ($verdicts -join " / ")
        })
    }
}

Say ''
Rule
Say ' PER-WIDGET MEASUREMENT'
Rule
$findings | Select-Object Widget, Chart, Dimension, Rows, Usable, LabelState, Stability |
    Format-Table -AutoSize | Out-String -Width 200 | Write-Host

Say ''
Rule
Say ' FIVE-RUN STABILITY'
Rule
$stability | Format-Table Widget, Class, RowCounts, KeyFps, ValFps, OrderFps, Windows -AutoSize |
    Out-String -Width 200 | Write-Host

Say ''
Rule
Say ' VERDICTS'
Rule
foreach ($f in $findings) {
    $marker = "PASS "
    if ($f.Verdict -like "*FAIL*") { $marker = "FAIL " } elseif ($f.Verdict -like "*ADVISORY*" -or $f.Verdict -like "*WARN*") { $marker = "LOOK " }
    Say ($marker + $f.Widget.PadRight(14) + " " + $f.Verdict)
}

$failed = @($findings | Where-Object { $_.Verdict -like "*FAIL*" }).Count
$advisory = @($findings | Where-Object { ($_.Verdict -notlike "*FAIL*") -and (($_.Verdict -like "*ADVISORY*") -or ($_.Verdict -like "*WARN*")) }).Count
$passed = @($findings | Where-Object { $_.Verdict -eq "PASS" }).Count

Say ''
Rule
Say (' widgets measured : ' + $findings.Count)
Say (' PASS             : ' + $passed)
Say (' need a look      : ' + $advisory)
Say (' FAIL             : ' + $failed)
Rule
Say ''
Say ' Nothing was written. No retry, no sleep, no widened assertion.'
Say ''
