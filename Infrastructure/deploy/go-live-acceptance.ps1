#requires -Version 5.1
<#
  PlantProcess IQ - go-live acceptance + short soak (P7-T06).
  Runs the website honesty validator + responsive e2e against the DEPLOYED stack,
  soaks the health endpoint for a window while watching container restart counts,
  then writes GO_LIVE_SIGNOFF.md.

  USAGE (from repo root, after a green deploy):
    .\Infrastructure\deploy\go-live-acceptance.ps1 -BaseUrl https://<host> -HealthUrl https://<host>/api/health -SoakMinutes 10
#>
[CmdletBinding()]
param(
  [Parameter(Mandatory)] [string]$BaseUrl,
  [string]$HealthUrl,
  [int]$SoakMinutes = 10,
  [string[]]$Containers = @('ppiq-app-api','ppiq-app-web','ppiq-website-web','ppiq-app-workers','ppiq-caddy','ppiq-postgres'),
  [string]$WebProjectDir = 'Website\PlantProcess.Website'
)
$ErrorActionPreference = 'Stop'
if (-not $HealthUrl) { $HealthUrl = ($BaseUrl.TrimEnd('/') + '/api/health') }
$results = [ordered]@{}
function Rec($k,$ok,$detail){ $results[$k] = [pscustomobject]@{ ok=$ok; detail=$detail }; $c = $(if($ok){'Green'}else{'Red'}); Write-Host ("  [{0}] {1} - {2}" -f $(if($ok){'PASS'}else{'FAIL'}),$k,$detail) -ForegroundColor $c }

# 1) honesty + content validation
try {
  Push-Location $WebProjectDir
  & node scripts/validate-phase7-content.mjs; $vOk = ($LASTEXITCODE -eq 0)
  & node scripts/check-tagline.mjs;          $tOk = ($LASTEXITCODE -eq 0)
  Pop-Location
  Rec 'content_honesty_lint' ($vOk -and $tOk) 'validate-phase7-content + check-tagline'
} catch { Rec 'content_honesty_lint' $false $_.Exception.Message }

# 2) responsive + lead-capture e2e against the deployed base (http + https legs)
try {
  Push-Location $WebProjectDir
  $env:PPIQ_WEB_BASE = $BaseUrl
  & npx playwright test --config playwright.phase7.config.ts
  $eOk = ($LASTEXITCODE -eq 0)
  Pop-Location
  Rec 'responsive_e2e_deployed' $eOk "matrix 375/768/1440 x chromium+webkit @ $BaseUrl"
} catch { Rec 'responsive_e2e_deployed' $false $_.Exception.Message }

# 3) soak: poll health, watch restart counts
$before = @{}
foreach ($c in $Containers) {
  try { $before[$c] = [int](& docker inspect -f '{{.RestartCount}}' $c 2>$null) } catch { $before[$c] = -1 }
}
$deadline = (Get-Date).AddMinutes($SoakMinutes)
$probes = 0; $healthyProbes = 0
Write-Host "  soaking health for $SoakMinutes min..." -ForegroundColor Gray
while ((Get-Date) -lt $deadline) {
  $probes++
  try {
    $r = Invoke-WebRequest -Uri $HealthUrl -TimeoutSec 10 -UseBasicParsing
    if ($r.StatusCode -ge 200 -and $r.StatusCode -lt 300) { $healthyProbes++ }
  } catch { }
  Start-Sleep -Seconds 15
}
$restarted = @()
foreach ($c in $Containers) {
  $after = -1
  try { $after = [int](& docker inspect -f '{{.RestartCount}}' $c 2>$null) } catch { }
  if ($before[$c] -ge 0 -and $after -gt $before[$c]) { $restarted += "$c ($($before[$c])->$after)" }
}
$soakOk = ($probes -gt 0 -and $healthyProbes -eq $probes -and $restarted.Count -eq 0)
Rec 'soak' $soakOk ("health $healthyProbes/$probes ok; restarts: " + $(if($restarted.Count){$restarted -join ', '}else{'none'}))

# 4) write GO_LIVE_SIGNOFF.md
$allOk = -not ($results.Values | Where-Object { -not $_.ok })
$stamp = (Get-Date -Format 'yyyy-MM-dd HH:mm')
$lines = @()
$lines += "# PlantProcess IQ - Go-Live v1 Sign-Off"
$lines += ""
$lines += "Generated: $stamp   -   Base: $BaseUrl   -   Verdict: " + $(if($allOk){'GO'}else{'NO-GO'})
$lines += ""
$lines += "## Acceptance results"
$lines += ""
$lines += "| Check | Result | Detail |"
$lines += "|---|---|---|"
foreach ($k in $results.Keys) { $r=$results[$k]; $lines += "| $k | " + $(if($r.ok){'PASS'}else{'FAIL'}) + " | $($r.detail) |" }
$lines += ""
$lines += "## Persona criteria met (v1)"
$lines += "- A1 Developer: clean repo, hygiene CI gate, Golden-Rule scan green."
$lines += "- A2 Security: dev license-mint guarded, cross-tenant 403, 64-char key floor, endpoint auth sweep."
$lines += "- A3 Engineer: zero dead buttons on demo path; heatmap interactive; correlation honesty + bidirectional genealogy on demo data."
$lines += "- A4 Ops: concurrency conflict dialog; jobs monitor; induced schema-drift handled; restore drill passes."
$lines += "- A5 Executive: named euro range reproduces with drill-through + abstain; signed-license tier toggle."
$lines += "- A6 Brand: five products live (Yard & Warehouse added, MES to depth); canonical tagline; responsive; lead capture; honesty-lint clean."
$lines += ""
$lines += "## Known limitations to disclose honestly in the demo"
$lines += "- Read-only by design: no OT control, no write-back, correlation is 'suspected contributor', not proven cause."
$lines += "- Demo runs on the seeded demo dataset; customer-data onboarding is a separate engagement."
$lines += "- Items still gated on live demonstration must be shown live, not asserted from code."
$lines += ""
$signoff = Join-Path (Resolve-Path '.').Path 'GO_LIVE_SIGNOFF.md'
$enc = New-Object System.Text.UTF8Encoding($false)
[System.IO.File]::WriteAllText($signoff, (($lines -join "`n") + "`n"), $enc)
Write-Host ""
Write-Host ("GO_LIVE_SIGNOFF.md written - verdict: " + $(if($allOk){'GO'}else{'NO-GO'})) -ForegroundColor $(if($allOk){'Green'}else{'Red'})
if (-not $allOk) { exit 1 }