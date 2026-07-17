# ============================================================================
# Finish-M1-18.ps1  -  the last 11 entries
#   - stages the root-level Certify-Journey.ps1 DELETION (the move staged only
#     the new location; the deletion is outside 'scripts')
#   - commits the D1 provider-binding fix as its own logical unit
#   - moves Set-OracleSchema.ps1 + Fix-ProviderTypeBinding.ps1 to scripts/verify
#   - gitignore, LINE-EXACT this time: the previous check used substring
#     Contains() and '/.ppiq-backups/' matched inside 'deploy/.ppiq-backups/',
#     so the root pattern was never added
# Run from repo root (presentation branch):
#   powershell -NoProfile -ExecutionPolicy Bypass -File .\Finish-M1-18.ps1
# ============================================================================
[CmdletBinding()]
param()

Set-StrictMode -Version 2.0
$ErrorActionPreference = 'Continue'
$RepoRoot = (Get-Location).Path
$branch = (& git rev-parse --abbrev-ref HEAD 2>&1).ToString().Trim()
Write-Host ("[BRANCH] " + $branch)
if ($branch -eq 'main') { Write-Host "[ABORT] on main." -ForegroundColor Red; exit 1 }

# ---- 1. gitignore, line-exact ----------------------------------------------
$gi = Join-Path $RepoRoot '.gitignore'
$lines = @([System.IO.File]::ReadAllLines($gi, [System.Text.Encoding]::UTF8))
$adds = @()
foreach ($p in @('.ppiq-backups/', 'OracleSchema_*.txt', 'wipetrap_state.json', 'Finish-M1-18_*.txt')) {
    $present = $false
    foreach ($l in $lines) { if ($l.Trim() -eq $p) { $present = $true; break } }
    if (-not $present) { $adds += $p }
}
if ($adds.Count -gt 0) {
    $all = @($lines) + @($adds)
    [System.IO.File]::WriteAllText($gi, (($all -join "`r`n").TrimEnd() + "`r`n"), (New-Object System.Text.UTF8Encoding($false)))
    Write-Host ("[GITIGNORE] + " + ($adds -join ', '))
}

# ---- 2. move the two remaining tools ---------------------------------------
foreach ($f in @('Set-OracleSchema.ps1', 'Fix-ProviderTypeBinding.ps1')) {
    $src = Join-Path $RepoRoot $f
    if (Test-Path $src) {
        Move-Item -LiteralPath $src -Destination (Join-Path $RepoRoot ('scripts\verify\' + $f)) -Force
        Write-Host ("      " + $f + "  ->  scripts\verify")
    }
}

# ---- 3. two logical commits ------------------------------------------------
Write-Host "[COMMIT 1] provider-binding fix:"
& git add -- 'Frontend/PlantProcess.Web/src/pages/Admin/AdminDbConfigurationTab.tsx' 2>&1 | Out-Null
$staged = @(& git diff --cached --name-only 2>&1 | Where-Object { $_ })
if (@($staged).Count -gt 0) {
    $msg = @"
fix(admin): resolve provider type case-insensitively in the profile form

The catalog publishes PascalCase ("Oracle"); stored profiles use lowercase
("oracle"). The form's select matched no option and fell back to the first,
displaying Oracle profiles as "CSV Snapshot". Display-only (state and saves
were correct); same comparison the list view already used. Suite 253/253.
"@
    $mf = Join-Path $env:TEMP 'ppiq_f1.txt'
    [System.IO.File]::WriteAllText($mf, $msg, (New-Object System.Text.UTF8Encoding($false)))
    & git commit -F $mf 2>&1 | Select-Object -First 2 | ForEach-Object { Write-Host ("    " + $_) }
    Remove-Item $mf -ErrorAction SilentlyContinue
} else { Write-Host "    nothing staged." }

Write-Host "[COMMIT 2] tooling relocation + root deletion + gitignore:"
& git add -A -- 'scripts' '.gitignore' 'Certify-Journey.ps1' 'Set-OracleSchema.ps1' 'Fix-ProviderTypeBinding.ps1' 2>&1 | Out-Null
$staged = @(& git diff --cached --name-only 2>&1 | Where-Object { $_ })
if (@($staged).Count -gt 0) {
    & git commit -m "chore(scripts): oracle schema + provider-binding tools under scripts/verify; ignore session artifacts" 2>&1 |
        Select-Object -First 2 | ForEach-Object { Write-Host ("    " + $_) }
} else { Write-Host "    nothing staged." }

# ---- 4. verdict ------------------------------------------------------------
Write-Host ""
$dirty = @(& git status --porcelain 2>&1 | Where-Object { $_ -and $_.ToString().Trim() -ne '' })
if (@($dirty).Count -eq 0) {
    Write-Host "[CLEAN] merge is unblocked:" -ForegroundColor Green
    Write-Host "    powershell -NoProfile -ExecutionPolicy Bypass -File .\scripts\verify\Protect-And-Merge.ps1 -Merge -IReviewedTheDiff"
} else {
    Write-Host ("[REMAINING] " + @($dirty).Count + ":") -ForegroundColor Yellow
    @($dirty) | ForEach-Object { Write-Host ("    " + $_) }
}
exit 0
