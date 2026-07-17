# ============================================================================
# Fix-MlFoundationAccess.ps1  v1.1  M1-21 blocker (verified, not guessed)
# v1.1: stops the API first - it locks the build DLLs and the gate then
#       reverts a good fix for a bad reason.
#
# THE EVIDENCE: Run-GoldenAnalysis got 403 on 8/8 POSTs to
# /api/ml/foundation/*, while GET /readiness and GET /outcomes returned data.
# That asymmetry is the signature of AccessControlMiddleware's static Matrix:
#   - unmapped POST  -> 403 "not mapped in the P01/P02 permission matrix"
#   - unmapped GET   -> slips through via the ("/", GET, anonymous) entry
# /api/ml/foundation is simply absent from the Matrix.
#
# CONSEQUENCE (this is the real finding): the correlation engine has NEVER
# been invokable through the API in this build. Journey step 9 (Run) cannot
# be demonstrated; the 320 findings in results_v2 are all historical.
#
# THE FIX: one Matrix line, mirroring the precedent already in the file -
# the M1-07 assistant entry, which carries a comment describing this exact
# failure. analysis.execute is the permission already used by
# /analytics/correlations and /analytics/ml (the sibling engine routes).
#
# Contract: unique-anchor preflight -> byte backup -> replace -> dotnet build
# gate -> auto-revert on red.
# Run from repo root:
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Fix-MlFoundationAccess.ps1
# ============================================================================
[CmdletBinding()]
param([switch]$SkipGate)

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$RepoRoot = (Get-Location).Path
$File = Join-Path $RepoRoot 'Backend\PlantProcess.Api\Security\PlantAccessControl.cs'
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$BackupDir = Join-Path $RepoRoot ("deploy\.ppiq-backups\ml-foundation-access-" + $Stamp)

# The API process holds the output DLLs; Ctrl+C in its window does NOT end it.
# Rebuild-PresentationDb learned this at 10:22 - same treatment here, or the
# build fails on locked files and the gate reverts a perfectly good fix.
$procs = @(Get-Process -Name 'PlantProcess.Api' -ErrorAction SilentlyContinue)
if ($procs.Count -eq 0) {
    Write-Host "[API] not running - build is clear."
} else {
    foreach ($pr in $procs) {
        Write-Host ("[API] stopping PID " + $pr.Id + " (it locks the build output)")
        Stop-Process -Id $pr.Id -Force -ErrorAction SilentlyContinue
    }
    Start-Sleep -Seconds 3
}

if (-not (Test-Path $File)) {
    Write-Host "[FAIL] PlantAccessControl.cs not found at the expected path." -ForegroundColor Red
    Write-Host "       Paste: dir /s /b Backend\*PlantAccessControl.cs"
    exit 1
}
New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
Copy-Item -LiteralPath $File -Destination (Join-Path $BackupDir 'PlantAccessControl.cs') -Force
function Restore-File {
    Copy-Item -LiteralPath (Join-Path $BackupDir 'PlantAccessControl.cs') -Destination $File -Force
    Write-Host ("[REVERT] restored. Backup: " + $BackupDir) -ForegroundColor Yellow
}

$text = [System.IO.File]::ReadAllText($File, [System.Text.Encoding]::UTF8)

if ($text -match '"/api/ml/foundation"') {
    Write-Host "[SKIP] /api/ml/foundation is already in the Matrix - the 403 has another cause."
    Write-Host "       Paste the API console output for the failing POST and we look again."
    exit 0
}

$Anchor = '        ("/api/ml/learning", new[] { "GET", "POST" }, "job.manage", false),'
$Replace = @'
        ("/api/ml/learning", new[] { "GET", "POST" }, "job.manage", false),
        // M1-21: the ML foundation group (feature store + correlation engine).
        // Without this line the middleware denies every POST to
        // /api/ml/foundation/compute/correlation and /feature-store/refresh
        // ("not mapped in the P01/P02 permission matrix"), while the GET
        // readiness/outcomes calls slip through anonymously via ("/", GET).
        // Consequence: the correlation engine cannot be invoked at all and
        // journey step 9 (Run) is undemonstrable. analysis.execute is the
        // permission already carried by /analytics/correlations and
        // /analytics/ml - the sibling engine routes.
        ("/api/ml/foundation", All(), "analysis.execute", false),
'@ -replace "`r`n", "`n" -replace "`n", "`r`n"

$count = 0; $idx = 0
while (($idx = $text.IndexOf($Anchor, $idx, [System.StringComparison]::Ordinal)) -ge 0) { $count++; $idx += $Anchor.Length }
if ($count -ne 1) {
    Write-Host ("[ABORT] anchor count=" + $count + " (expected 1). The Matrix drifted.") -ForegroundColor Red
    Write-Host "        Paste the Matrix block from PlantAccessControl.cs and I re-anchor."
    exit 1
}
[System.IO.File]::WriteAllText($File, $text.Replace($Anchor, $Replace), (New-Object System.Text.UTF8Encoding($false)))
Write-Host '      APPLIED ("/api/ml/foundation", All(), "analysis.execute", false)'

if ($SkipGate) { Write-Host "[GATE SKIPPED]"; exit 0 }

Write-Host "[GATE] dotnet build ..."
$out = & dotnet build (Join-Path $RepoRoot 'Backend\PlantProcess.Api\PlantProcess.Api.csproj') -v quiet --nologo 2>&1
$code = $LASTEXITCODE
@($out | Select-Object -Last 6) | ForEach-Object { Write-Host ("    " + $_) }
if ($code -ne 0) {
    Write-Host "[GATE RED] build failed." -ForegroundColor Red
    Restore-File
    exit 1
}
Write-Host "      build green." -ForegroundColor Green
Write-Host ""
Write-Host ("[DONE] Backup: " + $BackupDir)
Write-Host "RESTART THE API, then re-run the analysis:"
Write-Host "    .\scripts\run\start-api.ps1 -Profile presentation"
Write-Host "    powershell -NoProfile -ExecutionPolicy Bypass -File .\Run-GoldenAnalysis.ps1 -Execute"
exit 0
