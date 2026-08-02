# ============================================================================
# Probe-PpiqPresentationProfile.ps1        Backlog v2 task T-003 acceptance
#
# SUPERSEDES the -Probe switch inside Apply-PpiqM1PresentationProfileLock.ps1,
# which had two defects. Both were mine:
#
#   DEFECT 1 - it sent an anonymous GET. /api/ml/foundation/readiness sits
#              behind RequireAuthorization, so it answered 401 Unauthorized.
#              This script logs in first and sends a bearer token.
#
#   DEFECT 2 - and this one mattered more. It could not tell WHICH API answered.
#              Both profiles bind http://localhost:5063. If a stale API from an
#              earlier launch is still holding the port, the new launch dies with
#              AddressInUseException while the OLD process keeps answering - and
#              the probe would have cheerfully reported numbers from the wrong
#              profile and been recorded as green. A zero produced by a failed
#              match is worse than a red; so is a number produced by the wrong
#              process. This script identifies the listening process and refuses
#              if it started before the profile files were last edited.
#
# RUN FROM REPO ROOT. See the bottom of this file for the exact sequence.
# ============================================================================
[CmdletBinding()]
param(
    [ValidateSet("presentation", "local", "test", "server")]
    [string]$Profile = "presentation",

    [string]$ApiBase  = "http://localhost:5063",
    [int]   $ApiPort  = 5063,
    [string]$UserName = "e2eadmin",
    [string]$Password = "E2EAdmin123!",
    [string]$RequestedRole = ""
)

Set-StrictMode -Version 2.0
$ErrorActionPreference = "Continue"

$RepoRoot    = (Get-Location).Path
$EvidenceDir = Join-Path $RepoRoot "docs\m1\evidence"
$Stamp       = Get-Date -Format "yyyyMMdd_HHmmss"
$LocalEnv    = Join-Path $RepoRoot "env\profiles\local.env"
$PresEnv     = Join-Path $RepoRoot "env\profiles\presentation.env"

$Lines = New-Object System.Collections.ArrayList

function Say([string]$Text) {
    Write-Host $Text
    [void]$Lines.Add($Text)
}

function Head([string]$Text) {
    Say ""
    Say ("=" * 78)
    Say $Text
    Say ("=" * 78)
}

function Write-Utf8NoBom([string]$Path, [string]$Text) {
    $enc = New-Object System.Text.UTF8Encoding($false)
    [System.IO.File]::WriteAllText($Path, $Text, $enc)
}

function Save-Evidence([string]$Verdict) {
    New-Item -ItemType Directory -Path $EvidenceDir -Force | Out-Null
    $out = Join-Path $EvidenceDir ("T-003_profile_probe_" + $Profile + "_" + $Stamp + ".txt")
    $head = @()
    $head += "T-003 presentation profile probe"
    $head += ("Profile claimed : " + $Profile)
    $head += ("Timestamp       : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))
    $head += ("Verdict         : " + $Verdict)
    $head += ""
    Write-Utf8NoBom $out ((($head + $Lines.ToArray()) -join "`r`n"))
    Write-Host ""
    Write-Host ("[EVIDENCE] " + $out)
    return $out
}

Head ("T-003 PROBE - claimed profile: " + $Profile)

# ------------------------------------------------- 1. WHO OWNS THE PORT? ----
Head "1. PORT OWNERSHIP - which process will answer?"

$Owner       = $null
$OwnerPid    = 0
$OwnerStart  = $null

try {
    $conns = Get-NetTCPConnection -LocalPort $ApiPort -State Listen -ErrorAction SilentlyContinue
}
catch {
    $conns = $null
}

if ($null -eq $conns) {
    Say ("[FAIL] nothing is listening on port " + $ApiPort + ".")
    Say "       The API is not running, or it failed to start."
    Say "       Start it first:"
    Say ("         powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run\start-api.ps1 -Profile " + $Profile + " -FreePort")
    Save-Evidence "NO LISTENER" | Out-Null
    exit 1
}

foreach ($c in $conns) {
    $OwnerPid = [int]$c.OwningProcess
    try { $Owner = Get-Process -Id $OwnerPid -ErrorAction Stop } catch { $Owner = $null }
    break
}

if ($null -eq $Owner) {
    Say ("[WARN] port " + $ApiPort + " is held by PID " + $OwnerPid + " but the process could not be read.")
} else {
    try { $OwnerStart = $Owner.StartTime } catch { $OwnerStart = $null }
    Say ("[INFO] PID          : " + $OwnerPid)
    Say ("[INFO] Process      : " + $Owner.ProcessName)
    try { Say ("[INFO] Path         : " + $Owner.Path) } catch { Say "[INFO] Path         : (unavailable)" }
    if ($null -ne $OwnerStart) {
        Say ("[INFO] Started at   : " + $OwnerStart.ToString("yyyy-MM-dd HH:mm:ss"))
    }
}

# The stale-listener guard. If the API started before the env profiles were last
# edited, it is running configuration that no longer exists on disk.
$Stale = $false
if ($null -ne $OwnerStart) {
    foreach ($f in @($LocalEnv, $PresEnv)) {
        if (Test-Path $f) {
            $mod = (Get-Item $f).LastWriteTime
            Say ("[INFO] " + (Split-Path $f -Leaf).PadRight(20) + " last edited " + $mod.ToString("yyyy-MM-dd HH:mm:ss"))
            if ($OwnerStart -lt $mod) { $Stale = $true }
        }
    }
}

if ($Stale) {
    Say ""
    Say "[REFUSED] the process holding this port started BEFORE the profile files were last edited."
    Say "          It is serving configuration that no longer exists on disk, and any number it"
    Say "          returns would be attributed to the wrong profile. This is the false-green case"
    Say "          this probe exists to catch."
    Say ""
    Say "          Kill it and relaunch:"
    Say ("            powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run\free-ports.ps1 -Ports " + $ApiPort + " -Force")
    Say ("            powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run\start-api.ps1 -Profile " + $Profile + " -FreePort")
    Save-Evidence "REFUSED - STALE LISTENER" | Out-Null
    exit 1
}

Say "[OK] the listening process is newer than the profile files."

# --------------------------------------------------------- 2. AUTHENTICATE --
Head "2. AUTHENTICATE"

$LoginUrl = $ApiBase.TrimEnd("/") + "/auth/login"
Say ("POST " + $LoginUrl + "   userName=" + $UserName)

$body = @{ userName = $UserName; password = $Password }
if ($RequestedRole -ne "") { $body["requestedRole"] = $RequestedRole }

$Token = ""
try {
    $login = Invoke-RestMethod -Uri $LoginUrl -Method Post -ContentType "application/json" `
                               -Body (ConvertTo-Json $body -Compress) -TimeoutSec 20
    $Token = [string]$login.accessToken
    Say ("[OK] login succeeded. Role=" + $login.plantRole + " Tenant=" + $login.tenantCode)
}
catch {
    Say "[FAIL] login failed."
    Say ("       " + $_.Exception.Message)
    Say ""
    Say "       Check the credentials for this machine. The known working local admin is"
    Say "       e2eadmin / E2EAdmin123!. Override with -UserName and -Password."
    Save-Evidence "LOGIN FAILED" | Out-Null
    exit 1
}

if ($Token -eq "") {
    Say "[FAIL] login returned 200 but no accessToken field was present."
    Save-Evidence "NO TOKEN" | Out-Null
    exit 1
}

# ------------------------------------------------------------ 3. READINESS --
Head "3. READINESS"

$Url = $ApiBase.TrimEnd("/") + "/api/ml/foundation/readiness"
Say ("GET " + $Url)

$Raw = ""
try {
    $resp = Invoke-WebRequest -Uri $Url -Headers @{ Authorization = ("Bearer " + $Token) } `
                              -UseBasicParsing -TimeoutSec 30
    $Raw = $resp.Content
    Say ("[OK] HTTP " + [int]$resp.StatusCode)
}
catch {
    Say "[FAIL] the readiness endpoint did not answer."
    Say ("       " + $_.Exception.Message)
    Save-Evidence "READINESS FAILED" | Out-Null
    exit 1
}

Say ""
Say "--- response ---"
Say $Raw
Say "--- end ---"

# Pull the two headline counts out without assuming the whole shape.
$OutcomeValues = "not found"
$CorrelationResults = "not found"
$m = [regex]::Match($Raw, '"outcome[_]?values"\s*:\s*(\d+)', "IgnoreCase")
if ($m.Success) { $OutcomeValues = $m.Groups[1].Value }
$m = [regex]::Match($Raw, '"correlation[_]?results"\s*:\s*(\d+)', "IgnoreCase")
if ($m.Success) { $CorrelationResults = $m.Groups[1].Value }

Head "4. VERDICT"
Say ("Claimed profile     : " + $Profile)
Say ("Answering PID       : " + $OwnerPid)
Say ("outcome_values      : " + $OutcomeValues)
Say ("correlation_results : " + $CorrelationResults)
Say ""
Say "HOW TO READ THIS:"
Say "  outcome_values near 195,221 and correlation_results near 320"
Say "     -> the API is on ppiq_presentation."
Say "  materially different numbers"
Say "     -> the API is on ppiq_app."
Say ""
Say "T-003 is accepted only when TWO evidence files exist, one per profile,"
Say "carrying DIFFERENT counts AND DIFFERENT answering PIDs. Same PID in both"
Say "means the API was never actually restarted and the second run is worthless."

Save-Evidence "COMPLETED" | Out-Null
exit 0

# ============================================================================
# HOW TO RUN - copy the whole block, one profile at a time
#
#   cd C:\Workspace\PlantProcess-IQ
#
#   # --- run A: presentation ---
#   # window 1
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run\start-api.ps1 -Profile presentation -FreePort
#   # window 2, once window 1 says it is listening
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Probe-PpiqPresentationProfile.ps1 -Profile presentation
#
#   # --- run B: local ---
#   # stop window 1 with Ctrl+C, then in window 1
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run\start-api.ps1 -Profile local -FreePort
#   # window 2
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Probe-PpiqPresentationProfile.ps1 -Profile local
#
#   # --- compare ---
#   Get-ChildItem .\docs\m1\evidence\T-003_profile_probe_*.txt |
#       Sort-Object LastWriteTime |
#       ForEach-Object { Write-Host ""; Write-Host $_.Name; Get-Content $_.FullName | Select-String "Answering PID|outcome_values|correlation_results|Verdict" }
#
#   git add -A
#   git commit -m "T-003: authenticated profile probe with stale-listener guard, both evidence runs"
# ============================================================================
