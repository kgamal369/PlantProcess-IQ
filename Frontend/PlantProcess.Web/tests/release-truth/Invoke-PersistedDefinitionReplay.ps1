<#
Persisted Definition Replay - canonical runner.

Backlog origin: T-202   Release: M2   Owner: Worker 2 (Release Truth)

CurrentRelease (default)  canonical generic M2 application database. AUTHORITATIVE.
HistoricalBaseline        ppiq_presentation, frozen M1 definitions. Informational only.

Reuse policy: the API is NOT reused unless this runner started it, or -ReuseRunningApi
is passed explicitly. Both the local and presentation profiles bind the same API port
and /db-health reports a hardcoded literal, so a running API's database cannot be
verified from outside. Silently reusing it is exactly the wrong-database trap.
#>
[CmdletBinding()]
param(
    [string]$RepoRoot = "C:\Workspace\PlantProcess-IQ",
    [ValidateSet("CurrentRelease","HistoricalBaseline")]
    [string]$ReleaseMode = "CurrentRelease",
    [string]$Profile = "",
    [int]$HealthTimeoutSeconds = 180,
    [switch]$Falsify,
    [switch]$ReuseRunningApi,
    [switch]$NoLaunch
)

$ErrorActionPreference = "Stop"

$UseProfile = Join-Path $RepoRoot "scripts\env\use-profile.ps1"
$StartApi   = Join-Path $RepoRoot "scripts\run\start-api.ps1"
$WebRoot    = Join-Path $RepoRoot "Frontend\PlantProcess.Web"

if (-not (Test-Path $UseProfile)) { throw "Canonical profile loader missing: $UseProfile" }
if (-not (Test-Path $WebRoot))    { throw "Frontend root missing: $WebRoot" }

if ([string]::IsNullOrWhiteSpace($Profile)) {
    if ($ReleaseMode -eq "HistoricalBaseline") { $Profile = "presentation" } else { $Profile = "local" }
}

Write-Host "[REPLAY] Mode    : $ReleaseMode" -ForegroundColor Cyan
if ($ReleaseMode -eq "CurrentRelease") {
    Write-Host "[REPLAY] This run is AUTHORITATIVE for M2 closure." -ForegroundColor Green
} else {
    Write-Host "[REPLAY] Informational only. Cannot close an M2 task." -ForegroundColor Yellow
}
Write-Host "[REPLAY] Loading profile '$Profile' via the canonical loader"
& $UseProfile -Profile $Profile

$ApiBase = $env:VITE_API_BASE_URL
if ([string]::IsNullOrWhiteSpace($ApiBase)) { $ApiBase = ($env:ASPNETCORE_URLS -split ';')[0] }
if ([string]::IsNullOrWhiteSpace($ApiBase)) { throw "Profile '$Profile' declares no API base URL." }
$ApiBase = $ApiBase.TrimEnd('/')

$Db = $env:POSTGRES_DB
Write-Host "[REPLAY] API base: $ApiBase"
Write-Host "[REPLAY] Database: $Db"

if ($ReleaseMode -eq "CurrentRelease" -and $Db -eq "ppiq_presentation") {
    throw "CurrentRelease refuses ppiq_presentation. That is the frozen M1 baseline, not M2 " +
          "product authority. Use -ReleaseMode HistoricalBaseline for informational regression, " +
          "or pass -Profile <tech-lead-authorised M2 validation profile>."
}
if ($ReleaseMode -eq "HistoricalBaseline" -and $Db -ne "ppiq_presentation") {
    throw "HistoricalBaseline requires ppiq_presentation but profile '$Profile' resolves '$Db'."
}

function Test-ApiHealthy {
    param([string]$Base)
    try {
        $r = Invoke-WebRequest -Uri "$Base/health" -UseBasicParsing -TimeoutSec 5
        return ($r.StatusCode -ge 200 -and $r.StatusCode -lt 300)
    } catch { return $false }
}

$WeStartedIt = $false

if (Test-ApiHealthy -Base $ApiBase) {
    # Best-effort identity evidence: the launcher's -Profile argument is visible on the
    # owning process command line when start-api.ps1 was used.
    $foreignProfile = $null
    try {
        Get-CimInstance Win32_Process -Filter "Name='powershell.exe' OR Name='pwsh.exe'" |
            ForEach-Object {
                if ($_.CommandLine -and $_.CommandLine -match 'start-api\.ps1.*-Profile\s+(\w+)') {
                    $foreignProfile = $Matches[1]
                }
            }
    } catch { }

    if ($foreignProfile) { Write-Host "[REPLAY] A running API appears to have been started with -Profile $foreignProfile" -ForegroundColor Yellow }

    if ($foreignProfile -and $foreignProfile -eq $Profile) {
        Write-Host "[REPLAY] Running API matches the requested profile. Reusing it." -ForegroundColor Green
    } elseif ($ReuseRunningApi) {
        Write-Host "[REPLAY] -ReuseRunningApi given. Reusing the running API on the operator's assurance." -ForegroundColor Yellow
    } else {
        throw "An API is already healthy at $ApiBase but this runner did not start it and could " +
              "not confirm it is running profile '$Profile'. Both profiles bind this port and " +
              "/db-health reports a hardcoded literal, so its database cannot be verified. " +
              "Stop that API and re-run, or pass -ReuseRunningApi if you are certain."
    }
} elseif ($NoLaunch) {
    throw "API is not healthy at $ApiBase and -NoLaunch was specified."
} else {
    if (-not (Test-Path $StartApi)) { throw "Canonical API launcher missing: $StartApi" }
    Write-Host "[REPLAY] Starting API via $StartApi -Profile $Profile" -ForegroundColor Yellow
    $log = Join-Path $env:TEMP "ppiq-replay-api-$Profile.log"
    $proc = Start-Process powershell `
        -ArgumentList @("-NoProfile","-ExecutionPolicy","Bypass","-File",$StartApi,"-Profile",$Profile) `
        -RedirectStandardOutput $log -RedirectStandardError "$log.err" -PassThru
    $WeStartedIt = $true

    $deadline = (Get-Date).AddSeconds($HealthTimeoutSeconds)
    while ((Get-Date) -lt $deadline) {
        if (Test-ApiHealthy -Base $ApiBase) { break }
        if ($proc.HasExited) { break }
        Start-Sleep -Seconds 3
    }

    if (-not (Test-ApiHealthy -Base $ApiBase)) {
        Write-Host "[REPLAY] API LAUNCH FAILED" -ForegroundColor Red
        Write-Host "  launcher : $StartApi -Profile $Profile"
        Write-Host "  exited   : $($proc.HasExited)"
        if ($proc.HasExited) { Write-Host "  exit code: $($proc.ExitCode)" }
        foreach ($f in @($log, "$log.err")) {
            if (Test-Path $f) {
                Write-Host "  --- last 30 lines of $f ---" -ForegroundColor Yellow
                Get-Content $f -Tail 30 | ForEach-Object { Write-Host "    $_" }
            }
        }
        throw "Aborted: API never became healthy at $ApiBase within $HealthTimeoutSeconds s."
    }
    Write-Host "[REPLAY] API healthy (started by this runner, profile '$Profile')" -ForegroundColor Green
}

$env:PPIQ_REPLAY_MODE = $ReleaseMode

Push-Location $WebRoot
try {
    if ($Falsify) {
        Write-Host "[REPLAY] Running falsification (validation probe + isolated stub)" -ForegroundColor Cyan
        & node "tests\release-truth\definition-replay-falsification.mjs"
    } else {
        Write-Host "[REPLAY] Running persisted definition replay gate" -ForegroundColor Cyan
        & node "tests\release-truth\persisted-definition-replay.mjs"
    }
    $code = $LASTEXITCODE
} finally { Pop-Location }

$reportName = if ($ReleaseMode -eq "HistoricalBaseline") {
    "persisted_definition_replay.historical-baseline.json"
} else { "persisted_definition_replay.json" }

Write-Host ""
Write-Host "[REPLAY] Manifest: $WebRoot\reports\release-truth\$reportName"
if ($ReleaseMode -eq "HistoricalBaseline") {
    Write-Host "[REPLAY] Classification: M1 Frozen Baseline Regression Evidence (informational)." -ForegroundColor Yellow
}
if ($WeStartedIt) { Write-Host "[REPLAY] Note: this runner started the API. Stop it when finished." -ForegroundColor Yellow }
exit $code