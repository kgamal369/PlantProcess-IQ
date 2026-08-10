#requires -Version 5.1
<#
  PPIQ T-044 - certification measurement for the three operational dashboards.

  READ ONLY. This script issues GET and POST /widgets/query calls and writes
  NOTHING: no file, no database row, no repository change. There is no -Apply
  switch because there is nothing to apply.

  What it measures, per widget, against the v2.9.2 hardening in the frozen task:
    - row count
    - the bound dimension, and its USABLE cardinality (distinct non-empty,
      non-placeholder values actually returned)
    - whether the dimension values are raw GUIDs where a label should resolve
    - whether "unknown" is being promoted into a business category
    - whether the chart type is analytically degenerate for what came back
    - whether the title claims something the binding does not plot

  A verdict of FAIL means the widget does not meet acceptance. ADVISORY means a
  human has to look: the check is a heuristic and says so rather than pretending
  to be a measurement.

  Usage:
    # 1. get a token
    $body  = @{ userName = "e2eadmin"; password = "E2EAdmin123!" } | ConvertTo-Json
    $login = Invoke-RestMethod -Method Post -Uri "http://localhost:5063/auth/login" -ContentType "application/json" -Body $body

    # 2. inventory (no codes given: prints the dashboards and stops)
    .\Measure-T044-OperationalDashboards.ps1 -Token $login.accessToken

    # 3. certify the three operational pages
    .\Measure-T044-OperationalDashboards.ps1 -Token $login.accessToken -DashboardCodes CODE1,CODE2,CODE3
#>
[CmdletBinding()]
param(
    [Parameter(Mandatory = $true)]
    [string]$Token,

    [string]$BaseUrl = "http://localhost:5063",

    [string[]]$DashboardCodes
)

$ErrorActionPreference = 'Stop'

$headers = @{ Authorization = "Bearer $Token" }

function Say([string]$text) { Write-Host $text }
function Rule() { Write-Host ("-" * 78) }

function Get-Json([string]$path) {
    return Invoke-RestMethod -Uri ($BaseUrl + $path) -Headers $headers
}

function Post-Json([string]$path, $bodyObject) {
    $json = $bodyObject | ConvertTo-Json -Depth 10
    return Invoke-RestMethod -Method Post -Uri ($BaseUrl + $path) -Headers $headers -ContentType "application/json" -Body $json
}

# A value that is a bare identifier rather than something a plant engineer reads.
$guidPattern = '^[0-9a-fA-F]{8}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{4}-[0-9a-fA-F]{12}$'
$placeholders = @("unknown", "n/a", "na", "none", "null", "-", "")

Say ''
Say '================================================================'
Say ' T-044 CERTIFICATION MEASUREMENT (read only)'
Say '================================================================'

# ---------------------------------------------------------------------------
# Registry, so every judgement below is made against declared vocabulary rather
# than against a list written into this script.
# ---------------------------------------------------------------------------
$meta = Get-Json "/analytics/dashboard/metadata"

Say ''
Say ('  metadata generated at : ' + $meta.generatedAtUtc)
Say ('  chart types           : ' + (($meta.chartTypes | ForEach-Object { $_.code }) -join ", "))
if ($meta.PSObject.Properties.Name -contains 'widgetKinds') {
    Say ('  widget kinds          : ' + (($meta.widgetKinds | ForEach-Object { $_.code }) -join ", "))
} else {
    Say '  widget kinds          : NOT RETURNED by this endpoint'
}
if ($meta.PSObject.Properties.Name -contains 'filters') {
    Say ('  registry filters      : ' + $meta.filters.Count)
}
Say ('  dimensions            : ' + $meta.dimensions.Count + '   measures: ' + $meta.measures.Count)

$dimensionLabels = @{}
foreach ($d in $meta.dimensions) { $dimensionLabels[$d.code] = $d.label }

# ---------------------------------------------------------------------------
# Inventory
# ---------------------------------------------------------------------------
$definitions = Get-Json "/analytics/dashboard/definitions"
if ($definitions.PSObject.Properties.Name -contains 'items') { $definitions = $definitions.items }

Say ''
Rule
Say ' DASHBOARD INVENTORY'
Rule
$definitions |
    Select-Object @{n='code';e={$_.dashboardCode}},
                  @{n='name';e={$_.name}},
                  @{n='widgets';e={ if ($_.widgets) { $_.widgets.Count } else { 0 } }},
                  @{n='system';e={$_.isSystemTemplate}},
                  @{n='active';e={$_.isActive}} |
    Format-Table -AutoSize |
    Out-String |
    Write-Host

if (-not $DashboardCodes -or $DashboardCodes.Count -eq 0) {
    Say ''
    Say ' No -DashboardCodes given, so nothing was certified.'
    Say ' T-044 covers the three OPERATIONAL pages only. Re-run with their codes,'
    Say ' for example:'
    Say '   -DashboardCodes CODE1,CODE2,CODE3'
    Say ''
    Say ' Codes are not guessed here on purpose: which three are operational is a'
    Say ' product decision, not something this script should infer from a name.'
    exit 0
}

# ---------------------------------------------------------------------------
# Certification
# ---------------------------------------------------------------------------
$findings = New-Object System.Collections.Generic.List[object]

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
        $verdicts = New-Object System.Collections.Generic.List[string]
        $rowCount = 0
        $usable = 0
        $sample = ""
        $labelSource = ""
        $guidCount = 0
        $placeholderCount = 0
        $queryError = ""

        $request = @{
            widgetType    = $w.widgetType
            chartType     = $w.chartType
            dimensionCode = $w.dimensionCode
            measureCode   = $w.measureCode
            parameterCode = $w.parameterCode
        }

        try {
            $result = Post-Json "/analytics/dashboard/widgets/query" $request

            $rowCount = @($result.rows).Count
            $dimCode = $result.widget.dimensionCode
            if (-not $dimCode) { $dimCode = $w.dimensionCode }

            # The column the chart uses for its category, resolved from the
            # RESULT rather than assumed from the definition.
            $dimColumn = $null
            foreach ($c in $result.columns) {
                if ($c.code -eq $dimCode) { $dimColumn = $c.code }
            }
            if (-not $dimColumn) {
                foreach ($c in $result.columns) {
                    if ($c.code -ne "value" -and -not $dimColumn) { $dimColumn = $c.code }
                }
            }

            $values = New-Object System.Collections.Generic.List[string]
            foreach ($row in $result.rows) {
                if ($dimColumn -and ($row.PSObject.Properties.Name -contains $dimColumn)) {
                    $raw = [string]$row.$dimColumn
                    $values.Add($raw)
                }
            }

            $distinct = $values | Sort-Object -Unique
            foreach ($v in $distinct) {
                $trimmed = ([string]$v).Trim()
                if ($trimmed -match $guidPattern) { $guidCount = $guidCount + 1 }
                if ($placeholders -contains $trimmed.ToLower()) { $placeholderCount = $placeholderCount + 1 }
            }

            $usable = @($distinct | Where-Object {
                $t = ([string]$_).Trim().ToLower()
                ($placeholders -notcontains $t)
            }).Count

            $sample = (($distinct | Select-Object -First 3) -join " | ")
            if ($sample.Length -gt 46) { $sample = $sample.Substring(0, 43) + "..." }

            if ($guidCount -gt 0) {
                $labelSource = "RAW IDENTIFIER"
            } elseif ($dimensionLabels.ContainsKey($dimCode)) {
                $labelSource = "registry: " + $dimensionLabels[$dimCode]
            } else {
                $labelSource = "unregistered dimension"
            }

            if ($result.warnings -and @($result.warnings).Count -gt 0) {
                $verdicts.Add("WARN " + (@($result.warnings) -join "; "))
            }
        }
        catch {
            $queryError = $_.Exception.Message
            $verdicts.Add("FAIL query did not execute: " + $queryError)
        }

        $chart = ([string]$w.chartType).ToLower()
        $title = [string]$w.widgetTitle
        $titleLower = $title.ToLower()

        if (-not $queryError) {
            if ($rowCount -eq 0) {
                $verdicts.Add("FAIL returns no rows")
            }

            if ($guidCount -gt 0) {
                $verdicts.Add("FAIL presents " + $guidCount + " raw identifier value(s) where a label should resolve")
            }

            if ($placeholderCount -gt 0 -and $usable -le 1) {
                $verdicts.Add("FAIL placeholder category carries the chart")
            } elseif ($placeholderCount -gt 0) {
                $verdicts.Add("ADVISORY placeholder category present; confirm the source genuinely has an unknown bucket")
            }

            if (($chart -eq "pie" -or $chart -eq "donut") -and $usable -le 1) {
                $verdicts.Add("FAIL single effective category on a " + $chart)
            }

            if ($chart -eq "bar" -and $rowCount -le 1) {
                $verdicts.Add("FAIL single-row bar chart: a binding defect, not a data shortage")
            }

            if ($chart -eq "heatmap" -and $usable -le 1) {
                $verdicts.Add("FAIL heatmap with only one meaningful axis")
            }

            if (($chart -eq "line" -or $chart -eq "area") -and $usable -le 2) {
                $verdicts.Add("ADVISORY " + $chart + " over " + $usable + " point(s); confirm the x-axis is genuinely temporal")
            }

            if ($chart -eq "kpi" -and $w.dimensionCode) {
                $verdicts.Add("ADVISORY kpi carries a dimension; the registry says kpi does not support one")
            }

            # Title-versus-binding heuristics. Advisory by construction: these
            # read English, and English is not a measurement.
            if (($titleLower -match "trend|over time|by day|by hour|history") -and ($chart -ne "line" -and $chart -ne "area")) {
                $verdicts.Add("ADVISORY title claims a trend, chart type is " + $chart)
            }
            if (($titleLower -match "distribution|breakdown|share|mix") -and ($chart -eq "kpi")) {
                $verdicts.Add("ADVISORY title claims a distribution, chart type is kpi")
            }
        }

        if ($verdicts.Count -eq 0) { $verdicts.Add("PASS") }

        $findings.Add([PSCustomObject]@{
            Dashboard = $dashboard.dashboardCode
            Widget    = $w.widgetCode
            Title     = $title
            Chart     = $chart
            Dimension = $w.dimensionCode
            Measure   = $w.measureCode
            Rows      = $rowCount
            Usable    = $usable
            Labels    = $labelSource
            Sample    = $sample
            Verdict   = ($verdicts -join " / ")
        })
    }
}

Say ''
Rule
Say ' PER-WIDGET MEASUREMENT'
Rule
$findings |
    Select-Object Widget, Chart, Dimension, Rows, Usable, Labels, Sample |
    Format-Table -AutoSize |
    Out-String -Width 200 |
    Write-Host

Say ''
Rule
Say ' VERDICTS'
Rule
foreach ($f in $findings) {
    $marker = "     "
    if ($f.Verdict -like "*FAIL*") { $marker = "FAIL " } elseif ($f.Verdict -like "*ADVISORY*" -or $f.Verdict -like "*WARN*") { $marker = "LOOK " } else { $marker = "PASS " }
    Say ($marker + $f.Widget.PadRight(28) + " " + $f.Verdict)
}

$failed = @($findings | Where-Object { $_.Verdict -like "*FAIL*" }).Count
$advisory = @($findings | Where-Object { $_.Verdict -like "*ADVISORY*" -or $_.Verdict -like "*WARN*" }).Count
$passed = @($findings | Where-Object { $_.Verdict -eq "PASS" }).Count

Say ''
Rule
Say (' widgets measured : ' + $findings.Count)
Say (' PASS             : ' + $passed)
Say (' need a look      : ' + $advisory)
Say (' FAIL             : ' + $failed)
Rule
Say ''
Say ' Nothing was written. Remedy order for every FAIL, per the frozen task:'
Say '   1. correct the widget definition in the seed script'
Say '   2. then the chart type'
Say '   3. only then the code'
Say ''
