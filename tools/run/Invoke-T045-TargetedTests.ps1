# =============================================================================
# PPIQ T-045 - TARGETED TEST RUN
#
# WHY THIS EXISTS. A full `dotnet test` reports the D1 regression suite as
# SKIPPED, not passed: PlantProcess.Infrastructure.IntegrationTests refuses to
# run without PPIQ_TEST_PG_CONNSTRING. A suite that compiles and skips is not
# evidence, and reading "715 succeeded" as coverage of the aggregate engine
# would be exactly the mistake this task exists to remove.
#
# It runs ONLY the suites T-045 affected, plus the D1 regression, per the
# closure directive. Nothing else.
#
# THE DATABASE. Defaults to the DEV database, not the presentation database.
# The fixtures are self-contained with unique probe codes and a finally cleanup,
# so they are safe - but a demonstration database is not a place to find that
# out. Pass -Database ppiq_presentation deliberately if you want it there.
# =============================================================================

[CmdletBinding()]
param(
    [string]$EnvProfile = 'env\profiles\presentation.env',
    [string]$Database = 'ppiq_app'
)

$ErrorActionPreference = 'Stop'
Set-StrictMode -Version 2.0
$RepoRoot = (Get-Location).Path
$script:Failed = 0

function Write-Head([string]$t) {
    Write-Host ''
    Write-Host ('=' * 78)
    Write-Host $t
    Write-Host ('=' * 78)
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
$pgHost = Get-MapValue $m 'POSTGRES_HOST' '127.0.0.1'
$pgPort = Get-MapValue $m 'POSTGRES_PORT' '5432'
$pgUser = Get-MapValue $m 'POSTGRES_USER' 'ppiq_dev'
$pgPass = Get-MapValue $m 'POSTGRES_PASSWORD' 'ppiq_dev_local_only'

$env:PPIQ_TEST_PG_CONNSTRING = 'Host=' + $pgHost + ';Port=' + $pgPort + ';Database=' + $Database +
    ';Username=' + $pgUser + ';Password=' + $pgPass

Write-Head 'TARGET'
Write-Host ('  connection : ' + $pgUser + '@' + $pgHost + ':' + $pgPort + '/' + $Database)
Write-Host '  credentials resolved from the profile, never prompted'

$apiProc = Get-Process -Name 'PlantProcess.Api' -ErrorAction SilentlyContinue
if ($apiProc -ne $null) {
    Write-Host ('  FATAL the API is running (pid ' + @($apiProc)[0].Id + ') and locks the build output')
    Write-Host ('        Stop-Process -Id ' + @($apiProc)[0].Id + ' -Force')
    exit 1
}

function Invoke-Suite([string]$project, [string]$filter, [string]$label) {
    Write-Head $label
    if ([string]::IsNullOrWhiteSpace($filter)) {
        & dotnet test $project --nologo -v minimal | Out-Host
    } else {
        & dotnet test $project --nologo -v minimal --filter $filter | Out-Host
    }
    if ($LASTEXITCODE -ne 0) {
        $script:Failed = $script:Failed + 1
        Write-Host ('  RED   ' + $label)
    } else {
        Write-Host ('  GREEN ' + $label)
    }
}

# The D1 regression and the downtime semantics suite: the two that Pack B left
# uncompilable and Pack F repaired. These are the ones that must EXECUTE.
Invoke-Suite 'Backend\tests\PlantProcess.Infrastructure.IntegrationTests\PlantProcess.Infrastructure.IntegrationTests.csproj' `
    'FullyQualifiedName~GenericAggregateEngineTests|FullyQualifiedName~DowntimeMinutesMeasureExecutionTests' `
    'D1 REGRESSION AND DOWNTIME SEMANTICS (must execute, not skip)'

# Every architecture guard T-045 added or depends on.
Invoke-Suite 'Backend\tests\PlantProcess.Architecture.Tests\PlantProcess.Architecture.Tests.csproj' `
    '' `
    'ARCHITECTURE GUARDS'

Write-Head 'RESULT'
if ($script:Failed -eq 0) {
    Write-Host '  targeted suites green'
} else {
    Write-Host ('  ' + $script:Failed + ' targeted suite(s) RED')
}

Write-Host ''
Write-Host '  A SKIPPED TEST IS NOT A PASS. Read the skipped count above: if the'
Write-Host '  D1 regression reports Skipped rather than Passed, the connection'
Write-Host '  string did not reach it and this run proves nothing about the'
Write-Host '  aggregate engine.'

if ($script:Failed -gt 0) { exit 1 }
exit 0
