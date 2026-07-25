<#
    Test-PpiqAnalysisReadiness.ps1

    PURPOSE
        The database work is finished. It established that three outcomes carry
        real data and have never been analysed:

            defect.rate_per_m2   91,839 values   grain coil
            defect.class         51,691 values   grain coil
            defect.severity      51,691 values   grain coil

        and that all 320 existing correlation rows are orphaned history from a
        retired engine version, on keys that have no values at all.

        So Scene 8 -> Scene 9 is not a wiring defect. It is a run that has never
        been executed, on an outcome the Toolbox already defaults to.

        This script asks the one remaining question WITHOUT running anything:
        would a run on those outcomes complete, or would it hit the honest-abstain
        gate, and if so which gate and why.

    CONTRACT
        READ ONLY against the engine. Every call is a GET except the login itself.
        NO analysis run is triggered. Nothing is written to the repository or the
        database.

    PREREQUISITE
        The API must be running on the PRESENTATION profile:

            powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\run\start-api.ps1 -Profile presentation

        Section 1 verifies this rather than trusting it. The default -Profile local
        resolves to ppiq_app, where every row is tenant-NULL.

    CREDENTIALS
        The defaults below are the documented LOCAL DEV credentials only. They are
        not server credentials and must never be used against the Hetzner host.

    RUN FROM REPO ROOT
        powershell -NoProfile -ExecutionPolicy Bypass -File .\Test-PpiqAnalysisReadiness.ps1
#>

[CmdletBinding()]
param(
    [string]$ApiBase  = "http://localhost:5063",
    [string]$UserName = "e2eadmin",
    [string]$Password = "E2EAdmin123!",
    [int]$WindowDays  = 3650,
    [string]$Grain    = "coil"
)

$ErrorActionPreference = "Continue"
$script:Token = $null

function Write-Section {
    param([string]$Text)
    Write-Host ""
    Write-Host ("=" * 78)
    Write-Host $Text
    Write-Host ("=" * 78)
}

function Get-AuthHeaders {
    if ([string]::IsNullOrWhiteSpace($script:Token)) { return @{} }
    return @{ Authorization = ("Bearer " + $script:Token) }
}

function Invoke-Get {
    param([string]$Path, [string]$Label)
    $url = $ApiBase + $Path
    Write-Host ""
    Write-Host ("  " + $Label)
    Write-Host ("    GET " + $url)
    try {
        $r = Invoke-RestMethod -Uri $url -Method GET -Headers (Get-AuthHeaders) -TimeoutSec 120
        return $r
    } catch {
        Write-Host ("    REQUEST FAILED: " + $_.Exception.Message)
        if ($null -ne $_.Exception.Response) {
            try {
                $sr = New-Object System.IO.StreamReader($_.Exception.Response.GetResponseStream())
                $body = $sr.ReadToEnd()
                if (-not [string]::IsNullOrWhiteSpace($body)) {
                    Write-Host ("    BODY: " + $body)
                }
            } catch { }
        }
        return $null
    }
}

function Show-Object {
    param($Obj, [int]$Indent = 4)
    if ($null -eq $Obj) {
        Write-Host ((" " * $Indent) + "(null)")
        return
    }
    $json = $Obj | ConvertTo-Json -Depth 8
    foreach ($line in ($json -split "`n")) {
        Write-Host ((" " * $Indent) + $line.TrimEnd())
    }
}

# ------------------------------------------------------------------- LOGIN

Write-Section "PREFLIGHT - AUTHENTICATE"

Write-Host ("API base : " + $ApiBase)
Write-Host ("User     : " + $UserName)
Write-Host ("Run at   : " + (Get-Date -Format "yyyy-MM-dd HH:mm:ss"))

$loginBody = @{ userName = $UserName; password = $Password; requestedRole = $null } | ConvertTo-Json

try {
    $login = Invoke-RestMethod -Uri ($ApiBase + "/auth/login") -Method POST `
                               -ContentType "application/json" -Body $loginBody -TimeoutSec 60
} catch {
    Write-Host ""
    Write-Host ("  LOGIN FAILED: " + $_.Exception.Message)
    Write-Host "  Is the API running? Start it with -Profile presentation and retry."
    exit 1
}

# The token field name is not assumed. Probe the common shapes and report.
foreach ($candidate in @("accessToken", "token", "access_token", "jwt")) {
    if ($null -ne $login.PSObject.Properties[$candidate]) {
        $script:Token = $login.$candidate
        Write-Host ""
        Write-Host ("  Token field found: " + $candidate)
        break
    }
}

if ([string]::IsNullOrWhiteSpace($script:Token)) {
    Write-Host ""
    Write-Host "  Logged in, but no recognised token field. Full response shape:"
    Show-Object -Obj $login
    Write-Host "  Continuing unauthenticated - protected calls will likely 401."
} else {
    Write-Host ("  Token length: " + $script:Token.Length)
}

# ------------------------------- 1. WHICH DATABASE IS THE API ACTUALLY ON

Write-Section "1 - VERIFY THE API IS ON THE PRESENTATION DATABASE"

$mlReady = Invoke-Get -Path "/api/ml/foundation/readiness" -Label "ML foundation readiness counts"
if ($null -ne $mlReady) {
    Show-Object -Obj $mlReady
    Write-Host ""
    Write-Host "  EXPECTED on ppiq_presentation: outcome_values around 195221,"
    Write-Host "  correlation_results 320. If outcome_values reads 0 or the counts"
    Write-Host "  differ materially, the API is on the WRONG PROFILE. Stop here,"
    Write-Host "  restart with -Profile presentation, and re-run this script."
}

# ------------------------------------- 2. WHAT THE REGISTRY ENDPOINT SERVES

Write-Section "2 - WHAT GET /outcomes ACTUALLY RETURNS TO THE UI"

$outcomes = Invoke-Get -Path "/api/ml/foundation/outcomes" -Label "Outcome registry as the frontend would receive it"
if ($null -ne $outcomes) {
    Write-Host ""
    Write-Host "  outcome_key                | grain    | type"
    Write-Host "  ---------------------------+----------+------------"
    foreach ($o in $outcomes) {
        $k = [string]$o.outcome_key
        $g = [string]$o.grain
        $t = [string]$o.outcome_type
        Write-Host ("  " + $k.PadRight(26) + " | " + $g.PadRight(8) + " | " + $t)
    }
}

# ------------------------------- 3. READINESS FOR THE THREE OUTCOMES WITH DATA

Write-Section "3 - WOULD A RUN COMPLETE - THE THREE OUTCOMES THAT HAVE VALUES"

$targets = @("defect.class", "defect.severity", "defect.rate_per_m2")

foreach ($key in $targets) {
    Write-Section ("3." + ($targets.IndexOf($key) + 1) + " - " + $key + "  (grain=" + $Grain + ", windowDays=" + $WindowDays + ")")

    $qs = "?outcomeKey=" + [uri]::EscapeDataString($key) +
          "&grain=" + [uri]::EscapeDataString($Grain) +
          "&windowDays=" + $WindowDays

    $readiness = Invoke-Get -Path ("/api/analytics/advanced/readiness" + $qs) -Label "Readiness summary"
    if ($null -ne $readiness) { Show-Object -Obj $readiness }

    $gates = Invoke-Get -Path ("/api/analytics/advanced/readiness/gates" + $qs) -Label "Readiness GATES - this names the blocking dimension"
    if ($null -ne $gates) { Show-Object -Obj $gates }
}

# ------------------------------------------ 4. EXISTING RUNS, FOR CONTEXT

Write-Section "4 - RUNS ALREADY ON RECORD"

$runs = Invoke-Get -Path "/api/analytics/advanced/runs" -Label "Historical compute runs (outcome, grain, window, status)"
if ($null -ne $runs) { Show-Object -Obj $runs }

# ------------------------------------------------------------------ CLOSE

Write-Section "HOW TO READ THIS"

Write-Host @"
  Section 1 is a gate on everything else. If the counts are wrong, nothing below
  it means anything, because the API is reading ppiq_app.

  Section 3 is the answer.

  READY        The gates pass on at least one of the three outcomes.
               -> Scene 8 works as built. Run the analysis from the Toolbox on
                  that outcome, it writes results, and Scene 9 then has rows on
                  the outcome the Toolbox already defaults to. The ONLY code
                  change needed in this whole chain becomes the one-line
                  defect.edge_crack_rate default on the Findings page, plus
                  deleting the windowDays = 30 client defaults.

  BLOCKED      The gates name a failing dimension and a reason.
               -> That is the honest-abstain moat behaving correctly, and it is
                  demonstrable rather than embarrassing. Read the named gate
                  before deciding anything. Do NOT weaken a gate to make the
                  demo greener - the constitution forbids it and it is the one
                  thing a technical buyer will test.

  Section 4 should show the retired runs that produced the 320 orphaned rows.
  Their outcome keys and window values are worth comparing against what the
  engine writes today - that is the evidence for the M2 item on engine-versus-
  registry divergence.

  Nothing here has been run or written. The decision after this is yours.
"@

Write-Host ""
Write-Host "Probe complete. No analysis was triggered. Nothing was modified."
Write-Host ""
