# ============================================================================
# Enable-PresentationProfile.ps1
# Two jobs:
#   1. UNBLOCK THE LAUNCHER. Both scripts/run/start-api.ps1 and
#      scripts/env/use-profile.ps1 carry [ValidateSet("local","test","server")]
#      - "presentation" is rejected by both. Adds it to each.
#      SAFE: you are on the 'presentation' branch, so these edits never reach
#      main. Return path is still just: git checkout main.
#   2. SNAPSHOT INVENTORY. Lists every .dump/.sql/.gz backup with its date and
#      size, and flags anything dated BEFORE 2026-07-14 13:00 as PRE-PURGE -
#      those are the ones holding the full ~38k dataset. This is the 2-minute
#      path to a populated presentation DB, if one exists.
# Contract: unique-anchor preflight -> byte backups -> replace -> self-check.
# Run from repo root (on the presentation branch):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Enable-PresentationProfile.ps1
# ============================================================================
[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Stop'

$RepoRoot = (Get-Location).Path
$Stamp = Get-Date -Format 'yyyyMMdd_HHmmss'
$BackupDir = Join-Path $RepoRoot ("deploy\.ppiq-backups\presentation-profile-" + $Stamp)

$branch = (& git rev-parse --abbrev-ref HEAD 2>&1)
Write-Host ("[BRANCH] " + $branch)
if ($branch -eq 'main') {
    Write-Host "[ABORT] you are on main. Run: git checkout presentation   first." -ForegroundColor Red
    exit 1
}

$targets = @(
    @{ File = 'scripts\run\start-api.ps1' },
    @{ File = 'scripts\env\use-profile.ps1' }
)
$Anchor  = '[ValidateSet("local", "test", "server")]'
$Replace = '[ValidateSet("local", "test", "server", "presentation")]'

New-Item -ItemType Directory -Path $BackupDir -Force | Out-Null
$applied = 0
foreach ($t in $targets) {
    $path = Join-Path $RepoRoot $t.File
    if (-not (Test-Path $path)) { Write-Host ("      SKIP (not found): " + $t.File) -ForegroundColor Yellow; continue }
    $text = [System.IO.File]::ReadAllText($path, [System.Text.Encoding]::UTF8)
    if ($text.Contains('"presentation"')) { Write-Host ("      ALREADY OK: " + $t.File) -ForegroundColor Green; continue }
    $count = 0; $idx = 0
    while (($idx = $text.IndexOf($Anchor, $idx, [System.StringComparison]::Ordinal)) -ge 0) { $count++; $idx += $Anchor.Length }
    if ($count -ne 1) {
        Write-Host ("      SKIP (anchor count=" + $count + "): " + $t.File) -ForegroundColor Yellow
        Write-Host "        Manual: add , `"presentation`" inside its ValidateSet." -ForegroundColor Yellow
        continue
    }
    Copy-Item -LiteralPath $path -Destination (Join-Path $BackupDir (Split-Path $path -Leaf)) -Force
    [System.IO.File]::WriteAllText($path, $text.Replace($Anchor, $Replace), (New-Object System.Text.UTF8Encoding($false)))
    $applied++
    Write-Host ("      APPLIED: " + $t.File) -ForegroundColor Green
}
Write-Host ("[PROFILE] " + $applied + " launcher(s) now accept -Profile presentation")
Write-Host ""

# ---- snapshot inventory ----------------------------------------------------
Write-Host "[SNAPSHOTS] searching for backups (the pre-purge fast path)..."
$purgeTime = Get-Date '2026-07-14 13:00:00'
$found = @()
foreach ($dir in @(
        (Join-Path $RepoRoot 'deploy\.ppiq-snapshots'),
        (Join-Path $RepoRoot 'deploy\.ppiq-backups'),
        (Join-Path $RepoRoot 'backups'),
        $RepoRoot,
        (Join-Path $env:USERPROFILE 'Downloads'))) {
    if (-not (Test-Path $dir)) { continue }
    $found += @(Get-ChildItem -Path $dir -Recurse -File -Include *.dump, *.backup, *.sql.gz, *.tar -ErrorAction SilentlyContinue |
        Where-Object { $_.Length -gt 500000 })
}
$found = @($found | Sort-Object LastWriteTime -Unique)
if (@($found).Count -eq 0) {
    Write-Host "      NONE found (>0.5 MB) in the searched locations." -ForegroundColor Yellow
    Write-Host "      Searched: deploy\.ppiq-snapshots, deploy\.ppiq-backups, backups\, repo root, Downloads"
    Write-Host "      If you archived one elsewhere, point me at it. Otherwise the IMPORT is the path."
} else {
    foreach ($f in $found) {
        $tag = 'post-purge (already-empty state - no use)'
        if ($f.LastWriteTime -lt $purgeTime) { $tag = '*** PRE-PURGE - LIKELY HOLDS THE FULL ~38k DATASET ***' }
        Write-Host ("      " + $f.LastWriteTime.ToString('yyyy-MM-dd HH:mm') + "  " + [Math]::Round($f.Length / 1MB, 1).ToString().PadLeft(6) + " MB  " + $f.Name)
        Write-Host ("           -> " + $tag)
    }
    Write-Host ""
    Write-Host "      To load a PRE-PURGE dump into the presentation DB (2 min):"
    Write-Host "        pg_restore -h 127.0.0.1 -p 5432 -U ppiq_dev -d ppiq_presentation --clean --if-exists `"<path>`""
    Write-Host "      (PGPASSWORD=ppiq_dev_local_only ; ppiq_app is NOT touched)"
}

Write-Host ""
Write-Host "NEXT: .\scripts\run\start-api.ps1 -Profile presentation"
Write-Host ("      backups: " + $BackupDir)
exit 0
