# PPIQ T-040 - BROWSER AUTH STATE. READ ONLY. REVISION 01.
#
# Why this exists. The Golden Gate convergence run drives a REAL browser, and
# Playwright always opens a clean profile with no cookies. This product has no
# login page: AuthContext bootstraps by trying a refresh cookie first and, when
# there is none, logging in with the VITE_SMOKE_ credentials that Vite compiled
# into the bundle from Frontend\PlantProcess.Web\.env.local.
#
# Measurement, not assumption: scripts\env\use-profile.ps1 line 50 writes the
# LITERAL string change-me-before-production into .env.local instead of the
# profile's own VITE_SMOKE_PASSWORD, while line 49 interpolates the username
# correctly. If that literal is what the bundle carries, a clean browser cannot
# authenticate and the convergence run produces no evidence at all - while your
# own browser stays signed in on its existing refresh cookie and shows nothing
# wrong.
#
# This script writes nothing. It reads two files and asks the running API which
# password it accepts.
#
#   .\tools\run\Show-PpiqT040BrowserAuthState.ps1

$ErrorActionPreference = "Continue"

function Say([string]$m) { Write-Host $m }
function Line() { Write-Host "------------------------------------------------------------" }

function ReadEnvFile([string]$path) {
    $map = @{}
    if (-not (Test-Path $path)) { return $map }
    foreach ($raw in (Get-Content -Path $path)) {
        $l = $raw.Trim()
        if ($l.Length -eq 0) { continue }
        if ($l.StartsWith("#")) { continue }
        $i = $l.IndexOf("=")
        if ($i -lt 1) { continue }
        $map[$l.Substring(0, $i).Trim()] = $l.Substring($i + 1).Trim()
    }
    return $map
}

function Describe([string]$v) {
    if ([string]::IsNullOrEmpty($v)) { return "<absent>" }
    if ($v -eq "change-me-before-production") { return "change-me-before-production  <-- the literal from use-profile.ps1" }
    return ("<" + $v.Length + " characters, starts " + $v.Substring(0, [Math]::Min(2, $v.Length)) + ">")
}

$RepoRoot = (Get-Location).Path
$ProfilePath = Join-Path $RepoRoot "env\profiles\presentation.env"
$LocalPath   = Join-Path $RepoRoot "Frontend\PlantProcess.Web\.env.local"

Line
Say "PPIQ T-040 BROWSER AUTH STATE"
Line

if (-not (Test-Path $ProfilePath)) { Say "REFUSED. Not at the repository root - $ProfilePath is not there."; exit 1 }

$prof  = ReadEnvFile $ProfilePath
$local = ReadEnvFile $LocalPath

$apiUser = $prof["PlantProcess__Auth__Users__0__UserName"]
$apiPass = $prof["PlantProcess__Auth__Users__0__Password"]
$profUser = $prof["VITE_SMOKE_USERNAME"]
$profPass = $prof["VITE_SMOKE_PASSWORD"]
$webUser = $local["VITE_SMOKE_USERNAME"]
$webPass = $local["VITE_SMOKE_PASSWORD"]
$apiBase = $local["VITE_API_BASE_URL"]
if ([string]::IsNullOrEmpty($apiBase)) { $apiBase = "http://localhost:5063" }

Say "WHAT THE API WILL ACCEPT   (env\profiles\presentation.env)"
Say ("  user 0 name              : " + $apiUser)
Say ("  user 0 password          : " + (Describe $apiPass))
Say ("  profile VITE_SMOKE_USER  : " + $profUser)
Say ("  profile VITE_SMOKE_PASS  : " + (Describe $profPass))
Say ""
Say "WHAT THE BROWSER BUNDLE CARRIES   (Frontend\PlantProcess.Web\.env.local)"
Say ("  VITE_SMOKE_USERNAME      : " + $webUser)
Say ("  VITE_SMOKE_PASSWORD      : " + (Describe $webPass))
Say ("  VITE_API_BASE_URL        : " + $apiBase)
Say ""
$drift = ($webPass -ne $profPass)
Say ("  Passwords agree          : " + (-not $drift))

Line
Say "ASKING THE RUNNING API WHICH ONE IT ACCEPTS"
Say "(no writes; two login attempts against $apiBase/auth/login)"

function TryLogin([string]$label, [string]$u, [string]$p) {
    if ([string]::IsNullOrEmpty($u)) { Say ("  " + $label + " : SKIPPED, no user name"); return $null }
    $body = @{ userName = $u; password = $p } | ConvertTo-Json -Compress
    try {
        $r = Invoke-WebRequest -Uri ($apiBase + "/auth/login") -Method Post -Body $body -ContentType "application/json" -UseBasicParsing -TimeoutSec 20
        Say ("  " + $label + " : ACCEPTED, HTTP " + $r.StatusCode)
        return $true
    } catch {
        $code = "no response"
        if ($_.Exception.Response) { $code = "HTTP " + [int]$_.Exception.Response.StatusCode }
        Say ("  " + $label + " : REJECTED, " + $code)
        return $false
    }
}

$profileOk = TryLogin "profile credentials    " $profUser $profPass
$browserOk = TryLogin "browser bundle values  " $webUser $webPass

Line
Say "IS THE WEB SERVING"
try {
    $w = Invoke-WebRequest -Uri "http://localhost:5173" -UseBasicParsing -TimeoutSec 15
    Say ("  http://localhost:5173  : HTTP " + $w.StatusCode)
} catch {
    Say "  http://localhost:5173  : NOT RESPONDING. Start it with .\scripts\run\start-web.ps1 -Profile presentation"
}

Line
Say "VERDICT"
if ($profileOk -eq $null) {
    Say "  UNDECIDED. The API did not answer at all - start it with .\scripts\run\start-api.ps1 -Profile presentation and run this again."
} elseif ($browserOk -eq $true) {
    Say "  CLEAR. A fresh browser can authenticate on its own. The convergence run can proceed as it is."
} elseif ($profileOk -eq $true) {
    Say "  BLOCKED, AND THE CAUSE IS NAMED. The API accepts the profile password and rejects the one"
    Say "  compiled into the browser bundle. Your own browser stays signed in on its existing refresh"
    Say "  cookie, so nothing looks wrong until something opens a clean profile - Playwright, an"
    Say "  incognito window, or a customer's laptop."
    Say ""
    Say "  THE ONE-LINE CAUSE: scripts\env\use-profile.ps1 line 50 writes"
    Say "      VITE_SMOKE_PASSWORD=change-me-before-production"
    Say "  as a literal, while line 49 above it interpolates the user name correctly. Every run of"
    Say "  start-web.ps1 rewrites .env.local and reinstates the wrong password."
    Say ""
    Say "  Do NOT hand-edit .env.local - the next start-web run overwrites it. The fix belongs in"
    Say "  use-profile.ps1 and it is a pack, not a hand edit. Send me this output and it follows."
} else {
    Say "  NEITHER PASSWORD WAS ACCEPTED. Something other than this drift is wrong - most likely the"
    Say "  API is running on a different profile than presentation. Check which database it opened."
}
Line
Say "Nothing was written by this script."
