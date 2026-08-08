# PPIQ T-040 - BROWSER AUTH STATE. READ ONLY. REVISION 02.
#
# WHAT REVISION 01 GOT WRONG, AND IT IS MY DEFECT: it treated "the API did not
# answer at all" and "the API answered and refused the password" as the same
# outcome, so it printed NEITHER PASSWORD WAS ACCEPTED when the true reading was
# THE API IS NOT LISTENING. A diagnostic that cannot tell a dead socket from a
# 401 is worse than no diagnostic, because it invites a fix to the wrong thing.
# Revision 02 classifies every attempt as ACCEPTED, REFUSED with an HTTP status,
# or NO RESPONSE, and never concludes anything about a credential unless the API
# actually answered.
#
# It also reports what is holding the API down, because the build that failed
# said the output assembly was locked by another process - which means something
# is still alive from an earlier run.
#
# Writes nothing. Kills nothing.
#
#   .\tools\run\Show-PpiqT040BrowserAuthState02.ps1

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
    if ($v -eq "change-me-before-production") { return "change-me-before-production  <-- the literal from use-profile.ps1 line 50" }
    return ("<" + $v.Length + " characters, starts " + $v.Substring(0, [Math]::Min(2, $v.Length)) + ">")
}

$RepoRoot = (Get-Location).Path
$ProfilePath = Join-Path $RepoRoot "env\profiles\presentation.env"
$LocalPath   = Join-Path $RepoRoot "Frontend\PlantProcess.Web\.env.local"

Line
Say "PPIQ T-040 BROWSER AUTH STATE - REVISION 02"
Line

if (-not (Test-Path $ProfilePath)) { Say "REFUSED. Not at the repository root - $ProfilePath is not there."; exit 1 }

$prof  = ReadEnvFile $ProfilePath
$local = ReadEnvFile $LocalPath

$profUser = $prof["VITE_SMOKE_USERNAME"]
$profPass = $prof["VITE_SMOKE_PASSWORD"]
$webUser  = $local["VITE_SMOKE_USERNAME"]
$webPass  = $local["VITE_SMOKE_PASSWORD"]
$apiBase  = $local["VITE_API_BASE_URL"]
if ([string]::IsNullOrEmpty($apiBase)) { $apiBase = "http://localhost:5063" }

Say "CREDENTIAL DRIFT   (read from the two files, no network involved)"
Say ("  API user 0 name          : " + $prof["PlantProcess__Auth__Users__0__UserName"])
Say ("  API user 0 password      : " + (Describe $prof["PlantProcess__Auth__Users__0__Password"]))
Say ("  profile VITE_SMOKE_PASS  : " + (Describe $profPass))
Say ("  bundle  VITE_SMOKE_PASS  : " + (Describe $webPass))
Say ("  Passwords agree          : " + ($webPass -eq $profPass))

Line
Say "IS ANYTHING LISTENING"
$apiPort = 5063
try {
    $u = [System.Uri]$apiBase
    if ($u.Port -gt 0) { $apiPort = $u.Port }
} catch {
    Say "  (could not parse VITE_API_BASE_URL, assuming port 5063)"
}

$listeners = @(Get-NetTCPConnection -LocalPort $apiPort -State Listen -ErrorAction SilentlyContinue)
if ($listeners.Count -eq 0) {
    Say ("  port " + $apiPort + " : NOTHING IS LISTENING")
} else {
    foreach ($c in $listeners) {
        $procId = $c.OwningProcess
        $p = Get-Process -Id $procId -ErrorAction SilentlyContinue
        if ($p) {
            Say ("  port " + $apiPort + " : PID " + $procId + " " + $p.ProcessName + " started " + $p.StartTime)
        } else {
            Say ("  port " + $apiPort + " : PID " + $procId + " (process details unavailable)")
        }
    }
}

Say ""
Say "PROCESSES THAT COULD BE HOLDING THE API OUTPUT ASSEMBLY"
$suspects = @(Get-Process -ErrorAction SilentlyContinue | Where-Object {
    $_.ProcessName -eq "PlantProcess.Api" -or $_.ProcessName -eq "dotnet" -or $_.ProcessName -eq "VBCSCompiler" -or $_.ProcessName -eq "MSBuild"
})
if ($suspects.Count -eq 0) {
    Say "  none found"
} else {
    foreach ($s in $suspects) {
        $path = "unknown path"
        try { if ($s.Path) { $path = $s.Path } } catch { $path = "path not readable" }
        Say ("  PID " + $s.Id + "  " + $s.ProcessName + "  started " + $s.StartTime + "  " + $path)
    }
}

Line
Say "ASKING THE API WHICH CREDENTIAL IT ACCEPTS"
Say ("(two POSTs to " + $apiBase + "/auth/login, nothing is written)")

# Three outcomes, never two: accepted, answered-and-refused, or no answer at all.
function TryLogin([string]$label, [string]$u, [string]$p) {
    if ([string]::IsNullOrEmpty($u)) { Say ("  " + $label + " : SKIPPED, no user name in that file"); return "skipped" }
    $body = @{ userName = $u; password = $p } | ConvertTo-Json -Compress
    try {
        $r = Invoke-WebRequest -Uri ($apiBase + "/auth/login") -Method Post -Body $body -ContentType "application/json" -UseBasicParsing -TimeoutSec 20
        Say ("  " + $label + " : ACCEPTED, HTTP " + $r.StatusCode)
        return "accepted"
    } catch {
        if ($_.Exception.Response) {
            $code = [int]$_.Exception.Response.StatusCode
            Say ("  " + $label + " : REFUSED BY THE API, HTTP " + $code)
            return "refused"
        }
        Say ("  " + $label + " : NO RESPONSE - nothing answered on that socket")
        return "noresponse"
    }
}

$profileResult = TryLogin "profile credentials    " $profUser $profPass
$browserResult = TryLogin "browser bundle values  " $webUser $webPass

Line
Say "IS THE WEB SERVING"
try {
    $w = Invoke-WebRequest -Uri "http://localhost:5173" -UseBasicParsing -TimeoutSec 15
    Say ("  http://localhost:5173  : HTTP " + $w.StatusCode)
} catch {
    Say "  http://localhost:5173  : NOT RESPONDING"
}

Line
Say "VERDICT"
if ($profileResult -eq "noresponse" -or $browserResult -eq "noresponse") {
    Say "  UNDECIDED, AND THE CREDENTIAL QUESTION WAS NOT ANSWERED. The API is not serving on"
    Say ("  port " + $apiPort + ", so nothing here says anything about which password is correct.")
    Say ""
    Say "  The build failed because the API output assembly was locked, which means an earlier run"
    Say "  is still alive. Free the port and the lock, then start the API again:"
    Say ""
    Say "    .\scripts\run\free-ports.ps1 -Ports 5063 -Force"
    Say "    Get-Process PlantProcess.Api -ErrorAction SilentlyContinue | Stop-Process -Force"
    Say "    .\scripts\run\start-api.ps1 -Profile presentation"
    Say ""
    Say "  Do NOT run stop-local.ps1 for this: it only accepts the local and test profiles and it"
    Say "  also kills 5173, which is currently serving and does not need restarting."
    Say "  Then run this script again."
} elseif ($browserResult -eq "accepted") {
    Say "  CLEAR. A fresh browser can authenticate on its own. The convergence run can proceed."
} elseif ($profileResult -eq "accepted") {
    Say "  BLOCKED, CAUSE NAMED. The API accepts the profile password and refuses the one compiled"
    Say "  into the browser bundle. Your own browser stays signed in on its refresh cookie, so this"
    Say "  is invisible until something opens a clean profile - Playwright, an incognito window, or"
    Say "  a customer laptop."
    Say ""
    Say "  scripts\env\use-profile.ps1 line 50 writes VITE_SMOKE_PASSWORD as a hardcoded literal"
    Say "  while line 49 interpolates the user name correctly, and start-web.ps1 rewrites .env.local"
    Say "  on every start - so a hand edit will not survive. The fix belongs in use-profile.ps1."
} else {
    Say "  THE API ANSWERED AND REFUSED BOTH. This is not the .env.local drift. The likeliest reading"
    Say "  is that the running API is on a different profile than presentation, so its configured"
    Say "  users are not the ones in env\profiles\presentation.env. Check which database it opened."
}
Line
Say "Nothing was written and nothing was stopped by this script."
