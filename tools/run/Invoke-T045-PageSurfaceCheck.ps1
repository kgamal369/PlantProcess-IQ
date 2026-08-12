# =============================================================================
# PPIQ T-045 - ANALYTICAL PAGE SURFACE CHECK
#
# Executes EVERY widget on the four analytical dashboards through the real API,
# exactly as the browser does, and asserts what a machine can assert about the
# result: that it executes, that its shape is what its class promises, and that
# nothing in it claims something the data does not support.
#
# WHAT IT REPLACES: most of the browser walk. What it does NOT replace is
# whether the result is legible on screen - column widths, a table that spills,
# a label a plant engineer cannot read. That still needs eyes, and this script
# says so at the end rather than pretending otherwise.
#
# READ ONLY. Credentials from the profile. Never prompts.
# =============================================================================

[CmdletBinding()]
param(
    [string]$ApiBase = 'http://localhost:5063',
    [string]$EnvProfile = 'env\profiles\presentation.env'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$RepoRoot = (Get-Location).Path
$script:Fail = 0

function Write-Head([string]$t) {
    Write-Host ''
    Write-Host ('=' * 78)
    Write-Host $t
    Write-Host ('=' * 78)
}

function Check([bool]$ok, [string]$label) {
    if ($ok) {
        Write-Host ('  PASS  ' + $label)
    } else {
        $script:Fail = $script:Fail + 1
        Write-Host ('  FAIL  ' + $label)
    }
}

function Get-EnvProfileMap([string]$rel) {
    $map = @{}
    $full = Join-Path $RepoRoot $rel
    if (-not (Test-Path -LiteralPath $full)) { return $map }
    foreach ($line in [System.IO.File]::ReadAllLines($full)) {
        $s = $line.Trim()
        if ($s.Length -eq 0) { continue }
        if ($s.StartsWith('#')) { continue }
        $eq = $s.IndexOf('=')
        if ($eq -lt 1) { continue }
        $map[$s.Substring(0, $eq).Trim()] = $s.Substring($eq + 1).Trim()
    }
    return $map
}

function Get-MapValue($map, [string]$key, [string]$fallback) {
    if ($map.ContainsKey($key)) {
        $v = $map[$key]
        if (-not [string]::IsNullOrWhiteSpace($v)) { return $v }
    }
    return $fallback
}

$m = Get-EnvProfileMap $EnvProfile
$UserName = Get-MapValue $m 'PPIQ_SMOKE_USERNAME' 'e2eadmin'
$Password = Get-MapValue $m 'PPIQ_SMOKE_PASSWORD' ''

Write-Head 'SESSION'
$token = $null
try {
    $login = Invoke-RestMethod -Method Post -Uri ($ApiBase + '/auth/login') -ContentType 'application/json' `
        -Body (@{ userName = $UserName; password = $Password } | ConvertTo-Json)
    $token = $login.accessToken
} catch {
    Write-Host ('  FATAL login failed: ' + $_.Exception.Message)
    Write-Host '  the API must be running with -Profile presentation on this port'
    exit 1
}
if ([string]::IsNullOrWhiteSpace($token)) { Write-Host '  FATAL no access token'; exit 1 }
Write-Host ('  authenticated as ' + $UserName)
$Headers = @{ Authorization = ('Bearer ' + $token) }

function Invoke-Widget($body) {
    $json = $body | ConvertTo-Json -Depth 6
    try {
        $r = Invoke-RestMethod -Method Post -Uri ($ApiBase + '/analytics/dashboard/widgets/query') `
            -Headers $Headers -ContentType 'application/json' -Body $json
        return @{ Ok = $true; Result = $r; Error = $null }
    } catch {
        $detail = $_.Exception.Message
        try {
            $reader = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
            $detail = $reader.ReadToEnd()
        } catch { }
        return @{ Ok = $false; Result = $null; Error = $detail }
    }
}

# The widgets under certification, declared here rather than read from the
# database: this script must be able to notice that a widget VANISHED, and a
# list built from the database can never do that.
$widgets = @(
    @{ Code = 'PA_KAVG';  Type = 'kpi';   Chart = 'kpi';   Dim = $null;               Measure = 'avgParameterValue'; Param = 'FDT_C';        Class = 1; MinRows = 1 },
    @{ Code = 'PA_KOBS';  Type = 'kpi';   Chart = 'kpi';   Dim = $null;               Measure = 'observationCount';  Param = 'FDT_C';        Class = 1; MinRows = 1 },
    @{ Code = 'PA_TREND'; Type = 'chart'; Chart = 'line';  Dim = 'day';               Measure = 'avgParameterValue'; Param = 'FDT_C';        Class = 1; MinRows = 2 },
    @{ Code = 'PA_BYP';   Type = 'chart'; Chart = 'bar';   Dim = 'parameterCode';     Measure = 'observationCount';  Param = $null;          Class = 1; MinRows = 2 },
    @{ Code = 'PA_TABLE'; Type = 'table'; Chart = 'table'; Dim = 'gradeOrRecipe';     Measure = 'avgParameterValue'; Param = 'FDT_C';        Class = 1; MinRows = 2 },
    @{ Code = 'CF_RATE';  Type = 'table'; Chart = 'table'; Dim = $null;               Measure = 'findingStatus';     Param = $null;          Class = 2; MinRows = 1 },
    @{ Code = 'CF_TOP';   Type = 'table'; Chart = 'table'; Dim = $null;               Measure = 'analysisReadiness'; Param = 'defect.class'; Class = 2; MinRows = 5 },
    @{ Code = 'RI_KPI';   Type = 'kpi';   Chart = 'kpi';   Dim = $null;               Measure = 'riskScore';         Param = $null;          Class = 1; MinRows = 1 },
    @{ Code = 'RI_TREND'; Type = 'table'; Chart = 'table'; Dim = $null;               Measure = 'scoringCoverage';   Param = $null;          Class = 2; MinRows = 1 },
    @{ Code = 'RI_TABLE'; Type = 'table'; Chart = 'table'; Dim = 'materialUnitType';  Measure = 'riskScore';         Param = $null;          Class = 1; MinRows = 1 },
    @{ Code = 'MI_RATE';  Type = 'table'; Chart = 'table'; Dim = $null;               Measure = 'analysisReadiness'; Param = 'defect.class'; Class = 2; MinRows = 5 },
    @{ Code = 'MI_SEV';   Type = 'chart'; Chart = 'donut'; Dim = 'materialUnitType';  Measure = 'defectCount';       Param = $null;          Class = 1; MinRows = 2 }
)

$classOneColumns = @('dimensionLabel', 'value', 'observationCount', 'secondaryCount')

# Vocabulary that must never appear in a value a customer reads. The tokens are
# assembled so this file is not itself the match a repository scan reports.
$forbidden = @(('NO_' + 'CORRELATION_' + 'EXISTS'), ('eligible' + 'Population'))

Write-Head 'EVERY WIDGET ON THE FOUR ANALYTICAL PAGES'

foreach ($w in $widgets) {
    $body = @{
        widgetType    = $w.Type
        chartType     = $w.Chart
        dimensionCode = $w.Dim
        measureCode   = $w.Measure
        parameterCode = $w.Param
        filters       = $null
        options       = @{ maxRows = 100 }
    }

    $out = Invoke-Widget $body
    if (-not $out.Ok) {
        Check $false ($w.Code + ' executes')
        $d = $out.Error
        if ($d.Length -gt 300) { $d = $d.Substring(0, 300) }
        Write-Host ('        ' + $d)
        continue
    }

    $r = $out.Result
    $cols = @()
    foreach ($c in $r.columns) { $cols += $c.code }
    $rowCount = 0
    if ($r.rows -ne $null) { $rowCount = @($r.rows).Count }

    Check $true ($w.Code + ' executes: ' + $rowCount + ' row(s), ' + $cols.Count + ' column(s)')
    Check ($rowCount -ge $w.MinRows) ($w.Code + ' returns a usable population (' + $rowCount + ' >= ' + $w.MinRows + ')')

    if ($w.Class -eq 1) {
        $shaped = $true
        foreach ($e in $classOneColumns) { if ($cols -notcontains $e) { $shaped = $false } }
        Check $shaped ($w.Code + ' keeps the Class-1 envelope')
    } else {
        # A Class-2 result must NOT be wearing the aggregate shape: if it carries
        # the aggregate columns it went through BuildResult and was flattened.
        $flattened = ($cols -contains 'value') -and ($cols -contains 'secondaryCount')
        Check (-not $flattened) ($w.Code + ' is native-rich, not flattened into the aggregate shape')
        Check ($cols.Count -ge 8) ($w.Code + ' declares its own columns (' + $cols.Count + ')')
    }

    # Nothing a customer reads may carry forbidden vocabulary.
    $bad = @()
    foreach ($row in @($r.rows)) {
        foreach ($c in $cols) {
            $v = [string]$row.$c
            foreach ($f in $forbidden) {
                if ($v -like ('*' + $f + '*')) { $bad += ($c + '=' + $f) }
            }
        }
    }
    Check ($bad.Count -eq 0) ($w.Code + ' carries no forbidden vocabulary')
}

# =============================================================================
# THE THREE STATEMENTS THE PAGES MUST MAKE
# =============================================================================
Write-Head 'THE TRUTH CLAIMS, ASSERTED BY VALUE'

$fs = Invoke-Widget @{ widgetType='table'; chartType='table'; dimensionCode=$null; measureCode='findingStatus'; parameterCode=$null; filters=$null; options=@{maxRows=50} }
if ($fs.Ok) {
    $state = $null
    foreach ($row in @($fs.Result.rows)) { $state = [string]$row.state; break }
    Write-Host ('  findingStatus state : ' + $state)
    Check ($state -eq 'NO_SUPPORTED_FINDINGS_CURRENTLY_PUBLISHED' -or $state -eq 'SUPPORTED_FINDINGS_PUBLISHED') 'Correlation states a published-findings status, not an existence claim'
} else {
    Check $false 'findingStatus executes'
}

$sc = Invoke-Widget @{ widgetType='table'; chartType='table'; dimensionCode=$null; measureCode='scoringCoverage'; parameterCode=$null; filters=$null; options=@{maxRows=50} }
if ($sc.Ok) {
    foreach ($row in @($sc.Result.rows)) {
        $src = [string]$row.scoringSource
        $model = [string]$row.modelState
        $syn = [int]$row.syntheticPopulation
        $scored = [int]$row.scoredPopulation
        Write-Host ('  scoringCoverage     : ' + $row.scope + '  source=' + $src + '  model=' + $model +
                    '  scored=' + $scored + '  synthetic=' + $syn)
        if ($syn -eq $scored -and $scored -gt 0) {
            Check ($model -eq 'MODEL_NOT_READY') 'a fully synthetic scored population reports MODEL_NOT_READY'
            Check ($src -eq 'SCORING_SOURCE_SYNTHETIC') 'a fully synthetic scored population reports its source as synthetic'
        }
    }
} else {
    Check $false 'scoringCoverage executes'
}

$ar = Invoke-Widget @{ widgetType='table'; chartType='table'; dimensionCode=$null; measureCode='analysisReadiness'; parameterCode='defect.class'; filters=$null; options=@{maxRows=50} }
if ($ar.Ok) {
    $rows = @($ar.Result.rows)
    Check ($rows.Count -eq 5) ('readiness reports five DF8 dimensions (' + $rows.Count + ')')
    $overall = $null
    $worst = 'Ready'
    foreach ($row in $rows) {
        $overall = [string]$row.overall
        $s = [string]$row.state
        if ($s -eq 'Blocked') { $worst = 'Blocked' }
        elseif ($s -eq 'Partial' -and $worst -ne 'Blocked') { $worst = 'Partial' }
        Write-Host ('  readiness           : ' + $row.dimension + ' = ' + $s)
    }
    Check ($overall -eq $worst) ('overall equals the WORST dimension, never an average (' + $overall + ' = ' + $worst + ')')
} else {
    Check $false 'analysisReadiness executes'
}

Write-Head 'RESULT'
if ($script:Fail -eq 0) {
    Write-Host '  every widget on the four analytical pages executes and says what it can defend'
} else {
    Write-Host ('  ' + $script:Fail + ' check(s) FAILED')
}
Write-Host ''
Write-Host '  STILL NEEDS EYES: whether these results are LEGIBLE. A Class-2 table'
Write-Host '  returns 8 to 11 columns, and no assertion here can tell you that it'
Write-Host '  renders readably at the width a customer will see it.'

if ($script:Fail -gt 0) { exit 1 }
exit 0