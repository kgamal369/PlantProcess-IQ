#requires -Version 5.1
<#
  PPIQ T-044 / DATA-TRUTH ARCHITECTURE DEFECT - slice A1.
  CHARACTERISATION AND TRUTH PROOF. READ ONLY.

  Nothing here changes the product. This file establishes what is TRUE before
  anything is re-engineered, so the new server-side aggregation seam can be
  judged against a reference rather than against itself.

  WHAT IT ESTABLISHES

  1. POPULATION CENSUS. The row count of every source population an aggregate
     measure reads, beside the two caps that truncate them:
       DefaultRawRowLimit   50,000   applied inside every measure
       AbsoluteRawRowLimit 250,000   applied to the material id list that every
                                     measure filters through
     A population above its cap is an aggregate that is already a lower bound.
     This is the measurement that turns "two widgets are unstable" into a list
     of which measures are provably truncated on THIS dataset today.

  2. TRUSTED REFERENCE AGGREGATE. observationCount by day, by week and by
     month, computed by PostgreSQL over the whole population with the same
     joins and predicates the engine uses. No cap, no sampling.

  3. ENGINE ANSWER, five times, for the same three groupings, through the API.

  4. THE DELTA. Trusted groups and total beside engine groups and total. A
     negative delta is the truncated arithmetic, quantified.

  WEEK SEMANTICS ARE DELIBERATELY NOT ASSUMED. The engine computes a week key
  in C# as ceil((dayOfYear + firstDayOfYearWeekday) / 7), which is NOT ISO 8601
  and not date_trunc. This harness reports the engine's week keys and the
  database's ISO week keys SIDE BY SIDE without declaring either correct.
  Choosing the canonical week definition is a ruling, not a refactor, and it
  must be made before any SQL projection replaces that C# path.

  REQUIREMENTS
    psql on PATH, and the presentation database reachable. Credentials come
    from env\profiles\presentation.env by default.

  Usage:
    .\Measure-A1-AggregateTruth.ps1 -Token $token
#>
[CmdletBinding()]
param(
    [string]$Token,

    [string]$UserName = "e2eadmin",
    [string]$Password = "E2EAdmin123!",

    [string]$BaseUrl = "http://localhost:5063",

    [string]$DbHost = "127.0.0.1",
    [int]$DbPort = 5432,
    [string]$DbName = "ppiq_presentation",
    [string]$DbUser = "ppiq_dev",
    [string]$DbPassword = "ppiq_dev_local_only",

    [int]$Runs = 5
)

$ErrorActionPreference = 'Stop'

if (-not $Token) {
    $loginBody = @{ userName = $UserName; password = $Password } | ConvertTo-Json
    $login = Invoke-RestMethod -Method Post -Uri ($BaseUrl + "/auth/login") -ContentType "application/json" -Body $loginBody
    $Token = $login.accessToken
}
if (-not $Token) { throw "Could not obtain an access token." }

$headers = @{ Authorization = "Bearer $Token" }

$DefaultRawRowLimit = 50000
$AbsoluteRawRowLimit = 250000

function Say([string]$text) { Write-Host $text }
function Rule() { Write-Host ("-" * 96) }

function Invoke-Sql([string]$sql) {
    $env:PGPASSWORD = $DbPassword
    $output = & psql -h $DbHost -p $DbPort -U $DbUser -d $DbName -At -F "|" -c $sql 2>&1
    $code = $LASTEXITCODE
    $env:PGPASSWORD = ""
    if ($code -ne 0) {
        throw ("psql failed (" + $code + "): " + ($output -join " "))
    }
    # The comma is not decorative. PowerShell UNWRAPS a one-element array on
    # return, so "return @($x)" hands back a bare string and $result[0] then
    # indexes its first CHARACTER. That is how this harness first reported the
    # observation total as 51: the ASCII code of the character "3".
    $rows = @($output | Where-Object { $_ -ne "" })
    return ,$rows
}

function Get-Scalar([string]$sql) {
    $rows = Invoke-Sql $sql
    if ($rows.Count -eq 0) { return "" }
    return [string]$rows[0]
}

function Post-Json([string]$path, $bodyObject) {
    $json = $bodyObject | ConvertTo-Json -Depth 10 -Compress
    try {
        return Invoke-RestMethod -Method Post -Uri ($BaseUrl + $path) -Headers $headers -ContentType "application/json" -Body $json
    }
    catch {
        $message = $_.Exception.Message
        if ($message -like "*401*") {
            throw "The access token was rejected (401). Re-run without -Token so this script logs in itself."
        }
        throw $message
    }
}

Say ''
Say '================================================================================================'
Say ' A1 CHARACTERISATION: TRUSTED SQL versus THE ENGINE (read only)'
Say '================================================================================================'

try {
    $ping = Get-Scalar "SELECT current_database();"
    Say ('  database reachable    : ' + $ping)
} catch {
    Say ''
    Say ('  psql could not run: ' + $_.Exception.Message)
    Say '  Everything below needs the database. Nothing was measured.'
    exit 1
}

# ---------------------------------------------------------------------------
# 1. Population census
# ---------------------------------------------------------------------------
Say ''
Rule
Say ' 1. POPULATION CENSUS versus THE CAPS'
Rule

$censusSql = @"
SELECT 'material_units (the id list every measure filters through)', count(*) FROM material_units WHERE is_deleted = false
UNION ALL SELECT 'parameter_observations (observationCount, parameter aggregates)', count(*) FROM parameter_observations WHERE is_deleted = false
UNION ALL SELECT 'quality_events (defectCount, defectRate)', count(*) FROM quality_events WHERE is_deleted = false
UNION ALL SELECT 'process_step_executions (materialCount by equipment/shift/area, processStepDuration)', count(*) FROM process_step_executions WHERE is_deleted = false
UNION ALL SELECT 'data_quality_issues (dataQualityIssueCount)', count(*) FROM data_quality_issues WHERE is_deleted = false
UNION ALL SELECT 'risk_scores (riskScore)', count(*) FROM risk_scores WHERE is_deleted = false
"@

$census = New-Object System.Collections.Generic.List[object]
foreach ($line in (Invoke-Sql $censusSql)) {
    $parts = $line -split "\|"
    if ($parts.Count -lt 2) { continue }
    $rows = [int]$parts[1]

    $capped = "no"
    $cap = $DefaultRawRowLimit
    if ($parts[0] -like "material_units*") { $cap = $AbsoluteRawRowLimit }
    if ($rows -gt $cap) { $capped = "TRUNCATED" }

    $census.Add([PSCustomObject]@{
        Population = $parts[0]
        Rows       = $rows
        Cap        = $cap
        State      = $capped
    })
}
$census | Format-Table -AutoSize | Out-String -Width 190 | Write-Host

$truncated = @($census | Where-Object { $_.State -eq "TRUNCATED" })
if ($truncated.Count -eq 0) {
    Say '  No population exceeds its cap on THIS dataset. The defect is still real:'
    Say '  it is latent until a customer dataset crosses the boundary.'
} else {
    Say ('  ' + $truncated.Count + ' population(s) already exceed their cap. Every aggregate reading them')
    Say '  returns a lower bound today, not a total.'
}

# ---------------------------------------------------------------------------
# 2. Trusted reference aggregate: observationCount
# ---------------------------------------------------------------------------
Say ''
Rule
Say ' 2. TRUSTED REFERENCE: observationCount over the WHOLE population'
Rule
Say '    Same joins and predicates as ExecuteObservationCountAsync, no cap.'

$trustedTotalSql = @"
SELECT count(*)
FROM parameter_observations o
JOIN material_units m ON m.id = o.material_unit_id
JOIN parameter_definitions p ON p.id = o.parameter_definition_id
WHERE o.is_deleted = false AND m.is_deleted = false;
"@
$trustedTotal = [int](Get-Scalar $trustedTotalSql)
Say ('    trusted observation total : ' + $trustedTotal)

$trustedGroupsSql = @"
SELECT 'day', count(*) FROM (
  SELECT to_char(o.observed_at_utc, 'YYYY-MM-DD') AS k
  FROM parameter_observations o
  JOIN material_units m ON m.id = o.material_unit_id
  JOIN parameter_definitions p ON p.id = o.parameter_definition_id
  WHERE o.is_deleted = false AND m.is_deleted = false
  GROUP BY 1) d
UNION ALL
SELECT 'month', count(*) FROM (
  SELECT to_char(o.observed_at_utc, 'YYYY-MM') AS k
  FROM parameter_observations o
  JOIN material_units m ON m.id = o.material_unit_id
  JOIN parameter_definitions p ON p.id = o.parameter_definition_id
  WHERE o.is_deleted = false AND m.is_deleted = false
  GROUP BY 1) mo
UNION ALL
SELECT 'iso_week', count(*) FROM (
  SELECT to_char(o.observed_at_utc, 'IYYY-IW') AS k
  FROM parameter_observations o
  JOIN material_units m ON m.id = o.material_unit_id
  JOIN parameter_definitions p ON p.id = o.parameter_definition_id
  WHERE o.is_deleted = false AND m.is_deleted = false
  GROUP BY 1) w
"@
$trustedGroups = @{}
foreach ($line in (Invoke-Sql $trustedGroupsSql)) {
    $parts = $line -split "\|"
    if ($parts.Count -lt 2) { continue }
    $trustedGroups[$parts[0]] = [int]$parts[1]
}
Say ('    trusted distinct days     : ' + $trustedGroups["day"])
Say ('    trusted distinct months   : ' + $trustedGroups["month"])
Say ('    trusted distinct ISO weeks: ' + $trustedGroups["iso_week"] + '   (see the week note below)')

# ---------------------------------------------------------------------------
# 3. The engine, five times, same request
# ---------------------------------------------------------------------------
Say ''
Rule
Say ' 3. THE ENGINE, five identical runs per grouping'
Rule

$comparison = New-Object System.Collections.Generic.List[object]

foreach ($dimension in @("day", "week", "month")) {
    $request = @{
        widgetType    = "chart"
        chartType     = "bar"
        dimensionCode = $dimension
        measureCode   = "observationCount"
        parameterCode = $null
    }

    $groupCounts = New-Object System.Collections.Generic.List[int]
    $totals = New-Object System.Collections.Generic.List[double]

    for ($i = 1; $i -le $Runs; $i++) {
        $result = Post-Json "/analytics/dashboard/widgets/query" $request
        $groupCounts.Add(@($result.rows).Count)

        $sum = 0.0
        foreach ($row in $result.rows) {
            if ($row.PSObject.Properties.Name -contains "value") { $sum = $sum + [double]$row.value }
        }
        $totals.Add($sum)
    }

    $trustedGroupCount = 0
    if ($dimension -eq "day") { $trustedGroupCount = $trustedGroups["day"] }
    if ($dimension -eq "month") { $trustedGroupCount = $trustedGroups["month"] }
    if ($dimension -eq "week") { $trustedGroupCount = $trustedGroups["iso_week"] }

    $distinctGroupCounts = @($groupCounts | Select-Object -Unique).Count
    $distinctTotals = @($totals | Select-Object -Unique).Count
    $bestTotal = ($totals | Measure-Object -Maximum).Maximum

    $comparison.Add([PSCustomObject]@{
        Dimension     = $dimension
        TrustedGroups = $trustedGroupCount
        EngineGroups  = ($groupCounts -join ",")
        TrustedTotal  = $trustedTotal
        EngineBest    = $bestTotal
        Missing       = $trustedTotal - $bestTotal
        Stable        = (($distinctGroupCounts -eq 1) -and ($distinctTotals -eq 1))
    })
}

$comparison | Format-Table Dimension, TrustedGroups, EngineGroups, TrustedTotal, EngineBest, Missing, Stable -AutoSize |
    Out-String -Width 190 | Write-Host

Say ''
Say '    TrustedGroups for "week" is the ISO week count. The engine does NOT compute'
Say '    an ISO week: DashboardWidgetQueryService.BuildWeekDimension uses'
Say '      ceil((dayOfYear + (int)firstDayOfYear.DayOfWeek) / 7.0)'
Say '    which drifts from ISO 8601 at year boundaries and is not date_trunc either.'
Say '    A group-count difference on the week row may therefore be calendar semantics'
Say '    rather than truncation. It is reported, not judged. Rule the canonical week'
Say '    definition before any SQL projection replaces that C# path.'

# ---------------------------------------------------------------------------
# 4. Verdict
# ---------------------------------------------------------------------------
Say ''
Rule
Say ' 4. VERDICT'
Rule

$dayRow = $comparison | Where-Object { $_.Dimension -eq "day" } | Select-Object -First 1

if ($trustedTotal -gt $DefaultRawRowLimit) {
    Say ('  The observation population (' + $trustedTotal + ') exceeds the pre-aggregate cap (' + $DefaultRawRowLimit + ').')
    Say ('  Engine best total across ' + $Runs + ' runs: ' + $dayRow.EngineBest + '.  Missing: ' + $dayRow.Missing + '.')
    Say '  CONFIRMED: the aggregate is computed from a capped arbitrary sample.'
} else {
    Say ('  The observation population (' + $trustedTotal + ') is under the cap (' + $DefaultRawRowLimit + ').')
    Say '  If the engine still disagrees with the trusted total, the cause is NOT the cap'
    Say '  and the next measurement must look at the joins and the date filter instead.'
}

Say ''
Say '  Nothing was written. No product code was changed.'
Say ''
