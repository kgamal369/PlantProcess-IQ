<#
Export Replay Failure Evidence.

Backlog origin: T-202   Release: M2   Owner: Worker 2 (Release Truth)

READ-ONLY. Reads an existing persisted-definition-replay manifest, extracts every
FAILED / UNCLASSIFIED entry with its typed refusal discriminator and definition
provenance, and writes a compact evidence pair OUTSIDE the repository.

Nothing is mutated. No API is contacted. No credential is read.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot     = "C:\Workspace\PlantProcess-IQ",
    [ValidateSet("CurrentRelease","HistoricalBaseline")]
    [string]$ReleaseMode  = "CurrentRelease",
    [string]$EvidenceRoot = "C:\Workspace\_ppiq_evidence\PersistedDefinitionReplay"
)

$ErrorActionPreference = "Stop"

$reportName = if ($ReleaseMode -eq "HistoricalBaseline") {
    "persisted_definition_replay.historical-baseline.json"
} else { "persisted_definition_replay.json" }

$manifestPath = Join-Path $RepoRoot "Frontend\PlantProcess.Web\reports\release-truth\$reportName"
if (-not (Test-Path $manifestPath)) { throw "No manifest at $manifestPath. Run the replay gate first." }

$m = Get-Content -LiteralPath $manifestPath -Raw | ConvertFrom-Json
$bad = @($m.entries | Where-Object { $_.state -eq 'FAILED' -or $_.state -eq 'UNCLASSIFIED' })

$stamp = Get-Date -Format "yyyyMMdd_HHmmss"
$outDir = Join-Path $EvidenceRoot $stamp
New-Item -ItemType Directory -Path $outDir -Force | Out-Null

# ---- discriminator fallback for manifests written before the typed parser ----
function Get-Discriminator {
    param($entry)
    if ($entry.PSObject.Properties.Name -contains 'discriminator' -and $entry.discriminator) {
        return $entry.discriminator
    }
    $text = "$($entry.reason) $($entry.bodyExcerpt)"
    $tagged = [regex]::Match($text, '([a-z][a-z0-9]*(?:_[a-z0-9]+){2,}):')
    if ($tagged.Success) { return $tagged.Groups[1].Value }
    switch -Regex ($text) {
        'requires a selected parameter code' { return 'parameter_required' }
        'Unsupported dimension code'         { return 'unsupported_dimension_code' }
        'Unsupported measure code'           { return 'unsupported_measure_code' }
        'Unsupported chart type'             { return 'unsupported_chart_type' }
        'is not compatible with measure'     { return 'chart_measure_incompatible' }
        'Dimension code is required'         { return 'dimension_required' }
    }
    return "unclassified_$($entry.httpStatus)"
}

$rows = foreach ($e in $bad) {
    [pscustomobject]@{
        Dashboard     = $e.dashboard
        Widget        = $e.id
        Dimension     = $e.dimensionCode
        Measure       = $e.measureCode
        Parameter     = if ($e.parameterCode) { $e.parameterCode } else { '(null)' }
        Http          = $e.httpStatus
        Discriminator = Get-Discriminator -entry $e
        Origin        = if ($e.PSObject.Properties.Name -contains 'origin' -and $e.origin) { $e.origin } else { '(unknown - pre-diagnostic manifest)' }
        Route         = '(decide from discriminator)'
        Body          = $e.bodyExcerpt
    }
}

foreach ($r in $rows) {
    $r.Route = switch ($r.Discriminator) {
        'parameter_required'               { 'Worker 1 - seeded definition stores no parameter binding' }
        'unsupported_measure_code'         { 'Worker 1 - definition binds a code absent from the registry' }
        'unsupported_dimension_code'       { 'Worker 1 - definition binds a code absent from the registry' }
        'dimension_not_registered'         { 'Worker 1 - dimension has no execution projection' }
        'dimension_not_carried_by_source'  { 'DECIDE: seed defect (W1) if the pairing is invalid by design; carriage gap (W3) if it should be supported' }
        'aggregation_family_not_mergeable_at_requested_grain' { 'Worker 3 - aggregation algebra' }
        'aggregate_population_limit_exceeded'                 { 'Worker 3 - population budget' }
        'chart_measure_incompatible'       { 'Worker 1 - seeded chart/measure pairing' }
        default                            { 'UNROUTED - body did not carry a typed discriminator' }
    }
}

$jsonOut = Join-Path $outDir "replay-failure-evidence.json"
$mdOut   = Join-Path $outDir "replay-failure-evidence.md"

$rows | ConvertTo-Json -Depth 6 | Set-Content -LiteralPath $jsonOut -Encoding UTF8

$md = New-Object System.Text.StringBuilder
[void]$md.AppendLine("# Persisted Definition Replay - Failure Evidence")
[void]$md.AppendLine("")
[void]$md.AppendLine("Backlog origin: T-202 | Release: M2 | Mode: $ReleaseMode")
[void]$md.AppendLine("Database: $($m.database) | API: $($m.apiBase)")
[void]$md.AppendLine("Manifest: $manifestPath")
[void]$md.AppendLine("Verdict: $($m.verdict) | Failures: $($rows.Count)")
[void]$md.AppendLine("")
[void]$md.AppendLine("| Dashboard | Widget | Dimension | Measure | Param | HTTP | Discriminator | Origin | Route |")
[void]$md.AppendLine("|---|---|---|---|---|---|---|---|---|")
foreach ($r in $rows) {
    [void]$md.AppendLine("| $($r.Dashboard) | $($r.Widget) | $($r.Dimension) | $($r.Measure) | $($r.Parameter) | $($r.Http) | ``$($r.Discriminator)`` | $($r.Origin) | $($r.Route) |")
}
[void]$md.AppendLine("")
[void]$md.AppendLine("## Response bodies")
foreach ($r in $rows) {
    [void]$md.AppendLine("")
    [void]$md.AppendLine("### $($r.Dashboard) / $($r.Widget)")
    [void]$md.AppendLine('```json')
    [void]$md.AppendLine($r.Body)
    [void]$md.AppendLine('```')
}
$md.ToString() | Set-Content -LiteralPath $mdOut -Encoding UTF8

Write-Host ""
Write-Host "PERSISTED DEFINITION REPLAY - FAILURE EVIDENCE" -ForegroundColor Cyan
Write-Host "  manifest : $manifestPath"
Write-Host "  failures : $($rows.Count)"
Write-Host ""
$rows | Format-Table Dashboard, Widget, Dimension, Measure, Parameter, Http, Discriminator, Origin -AutoSize
Write-Host ""
Write-Host "  evidence : $mdOut"
Write-Host "             $jsonOut"
Write-Host "  (written outside the repository - nothing staged, nothing mutated)" -ForegroundColor Yellow
