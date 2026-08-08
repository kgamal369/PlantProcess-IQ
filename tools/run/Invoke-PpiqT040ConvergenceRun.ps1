# PPIQ T-040 - THE GOLDEN GATE CONVERGENCE RUN. REVISION 01.
#
# Starts nothing. Refuses if the API or the web is not already serving, because
# this run must observe the presentation installation rather than one it spun up
# for itself.
#
#   .\tools\run\Invoke-PpiqT040ConvergenceRun.ps1

$ErrorActionPreference = "Continue"

function Say([string]$m) { Write-Host $m }
function Line() { Write-Host "------------------------------------------------------------" }

$RepoRoot = (Get-Location).Path
$WebRel = "Frontend\PlantProcess.Web"
$WebPath = Join-Path $RepoRoot $WebRel
$EvidencePath = Join-Path $RepoRoot "docs\m1\evidence\T-040"

if (-not (Test-Path (Join-Path $WebPath "package.json"))) { Say "REFUSED. Run from the repository root."; exit 1 }

Line
Say "PRECONDITIONS"
$apiOk = $false
try {
    $h = Invoke-WebRequest -Uri "http://localhost:5063/health" -UseBasicParsing -TimeoutSec 15
    Say ("  API  http://localhost:5063/health : HTTP " + $h.StatusCode)
    $apiOk = $true
} catch {
    Say "  API  http://localhost:5063/health : NOT SERVING"
    Say "       .\scripts\run\start-api.ps1 -Profile presentation"
}
$webOk = $false
try {
    $w = Invoke-WebRequest -Uri "http://localhost:5173" -UseBasicParsing -TimeoutSec 15
    Say ("  WEB  http://localhost:5173        : HTTP " + $w.StatusCode)
    $webOk = $true
} catch {
    Say "  WEB  http://localhost:5173        : NOT SERVING"
    Say "       .\scripts\run\start-web.ps1 -Profile presentation"
}
if (-not $apiOk -or -not $webOk) { Say ""; Say "REFUSED. Nothing was run."; exit 1 }

# The bundle, not the file. .env.local can be correct on disk while the running
# dev server still serves the value it read at start.
Say ""
Say "IS THE SERVED BUNDLE CURRENT"
try {
    $mod = Invoke-WebRequest -Uri "http://localhost:5173/src/state/AuthContext.tsx" -UseBasicParsing -TimeoutSec 15
    if ($mod.Content -match "change-me-before-production") {
        Say "  STALE. The running dev server still serves the old smoke password."
        Say "  .\scripts\run\free-ports.ps1 -Ports 5173 -Force"
        Say "  .\scripts\run\start-web.ps1 -Profile presentation"
        Say ""
        Say "REFUSED. Nothing was run."
        exit 1
    }
    Say "  ok    the served module does not carry the old literal"
} catch {
    Say "  UNKNOWN. The dev server did not serve that module for inspection; the run will decide instead."
}

$cli = ""
foreach ($candidate in @("node_modules\@playwright\test\cli.js", "node_modules\playwright\cli.js")) {
    if (Test-Path (Join-Path $WebPath $candidate)) { $cli = $candidate; break }
}
if ($cli -eq "") { Say ""; Say "REFUSED. No Playwright CLI under $WebRel\node_modules. Run npm install there."; exit 1 }
Say ("  ok    Playwright CLI at " + $cli)

Line
Say "CLEARING PREVIOUS EVIDENCE"
New-Item -ItemType Directory -Path $EvidencePath -Force | Out-Null
Get-ChildItem -Path $EvidencePath -File -ErrorAction SilentlyContinue | Remove-Item -Force -ErrorAction SilentlyContinue
Say ("  " + $EvidencePath)

Line
Say "RUNNING THE CONVERGENCE SPEC"
Push-Location $WebPath
& node $cli test --config=playwright.t040.config.ts
$code = $LASTEXITCODE
Pop-Location

Line
Say "EVIDENCE WRITTEN"
$files = @(Get-ChildItem -Path $EvidencePath -File -ErrorAction SilentlyContinue)
if ($files.Count -eq 0) {
    Say "  none - no Golden Gate line can be ticked from this run"
} else {
    foreach ($f in $files) { Say ("  " + $f.Name + "   " + [Math]::Round($f.Length / 1KB) + " KB") }
}
$manifest = Join-Path $EvidencePath "EVIDENCE.jsonl"
if (Test-Path $manifest) {
    Say ""
    Say "CLAIMS"
    foreach ($l in (Get-Content $manifest)) {
        $o = $l | ConvertFrom-Json
        Say ("  " + $o.gate.PadRight(10) + $o.evidence)
        Say ("             " + $o.claim)
    }
}

Line
if ($code -eq 0) {
    Say "GREEN. Every row in part 1 produced its named evidence file."
    Say "Commit the evidence with the closure record, not before it."
} else {
    Say ("RED. Playwright exited " + $code + ". The failure is above, with a trace under " + $WebRel + "\test-results\t040.")
    Say "The spec files are LEFT IN PLACE deliberately: they are test assets, not product code, and"
    Say "deleting them would destroy the only record of what the browser actually did."
}
exit $code